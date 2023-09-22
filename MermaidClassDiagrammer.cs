using System.Diagnostics;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    using CD = ClassDiagrammer;

    /* See class diagram syntax
     * reference (may be outdated!) https://mermaid.js.org/syntax/classDiagram.html
     * lexical definition https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/diagrams/class/parser/classDiagram.jison */

    /// <summary>Produces mermaid class diagram syntax for a filtered list of types from a specified .Net assembly.</summary>
    public class ClassDiagrammerFactory
    {
        private readonly XmlDocumentationFormatter? xmlDocs;
        private readonly DecompilerSettings decompilerSettings;
        private readonly CSharpDecompiler decompiler;

        private ITypeDefinition[]? selectedTypes;
        private Dictionary<IType, string>? uniqueIds;
        private Dictionary<IType, string>? labels;
        private Dictionary<string, string>? outsideReferences;

        public ClassDiagrammerFactory(string assemblyPath, XmlDocumentationFormatter? xmlDocs)
        {
            this.xmlDocs = xmlDocs;
            decompilerSettings = new DecompilerSettings(LanguageVersion.Latest);
            decompilerSettings.AutomaticProperties = true; // for IsHidden to return true for backing fields
            decompiler = new CSharpDecompiler(assemblyPath, decompilerSettings);
        }

        /// <summary>Wraps a <see cref="CSharpDecompiler"/> method configurable via <see cref="decompilerSettings"/>
        /// that can be used to determine whether a member should be hidden.</summary>
        private bool IsHidden(IEntity entity) => CSharpDecompiler.MemberIsHidden(entity.ParentModule!.PEFile, entity.MetadataToken, decompilerSettings);

        public CD BuildModel(string? include, string? exclude)
        {
            IEnumerable<ITypeDefinition> allTypes = decompiler.TypeSystem.MainModule.TypeDefinitions;

            selectedTypes = FilterTypes(allTypes,
                include == null ? null : new(include, RegexOptions.Compiled),
                exclude == null ? null : new(exclude, RegexOptions.Compiled)).ToArray();

            // generate dict to read names from later
            uniqueIds = GenerateUniqueIds(selectedTypes);
            labels = new();
            outsideReferences = new();

            var namespaces = selectedTypes.GroupBy(t => t.Namespace).Select(ns => new CD.Namespace
            {
                Name = ns.Key,
                Types = ns.OrderBy(t => t.FullName).Select(type =>
                    type.Kind == TypeKind.Enum ? BuildEnum(type) : BuildType(type)).ToArray()
            }).OrderBy(ns => ns.Name).ToArray();

            string[] excluded = allTypes.Except(selectedTypes).Select(t => t.ReflectionName).ToArray();
            return new CD { Namespaces = namespaces, OutsideReferences = outsideReferences, Excluded = excluded };
        }

        protected virtual IEnumerable<ITypeDefinition> FilterTypes(IEnumerable<ITypeDefinition> typeDefinitions, Regex? include, Regex? exclude)
            => typeDefinitions.Where(type => !type.IsCompilerGeneratedOrIsInCompilerGeneratedClass() // exlude compiler-generated and their nested types
                && (include == null || include.IsMatch(type.ReflectionName)) // applying optional whitelist filter
                && (exclude == null || !exclude.IsMatch(type.ReflectionName))); // applying optional blacklist filter

        /// <summary>Generates a dictionary of unique and short, but human readable identifiers for
        /// <paramref name="types"/>to be able to safely reference them in any combination.</summary>
        private static Dictionary<IType, string> GenerateUniqueIds(IEnumerable<ITypeDefinition> types)
        {
            Dictionary<IType, string> uniqueIds = new();
            var groups = types.GroupBy(t => t.Name);

            // simplified handling for the majority of unique types
            foreach (var group in groups.Where(g => g.Count() == 1))
                uniqueIds[group.First()] = group.Key;

            // number non-unique types
            foreach (var group in groups.Where(g => g.Count() > 1))
            {
                var counter = 0;
                foreach (var type in group) uniqueIds[type] = type.Name + ++counter;
            }

            return uniqueIds;
        }

        private CD.Type BuildEnum(ITypeDefinition type)
        {
            IField[] fields = type.GetFields(f => f.IsConst && f.IsStatic && f.Accessibility == Accessibility.Public).ToArray();
            Dictionary<string, string>? docs = xmlDocs?.GetXmlDocs(type, fields);
            string name = GetName(type), typeId = GetId(type);

            var body = fields.Select(f => f.Name).Prepend("<<Enumeration>>")
                .Join(Environment.NewLine + "    ", pad: true).TrimEnd(' ');

            return new CD.Type
            {
                Id = typeId,
                Name = name == typeId ? null : name,
                DiagramDefinition = $"class {typeId} {{{body}}}",
                XmlDocs = docs
            };
        }

        private CD.Type BuildType(ITypeDefinition type)
        {
            string typeId = GetId(type);
            IMethod[] methods = GetMethods(type).ToArray();
            IProperty[] properties = type.GetProperties().ToArray();
            IProperty[] hasOneRelations = GetHasOneRelations(properties);
            (IProperty property, IType elementType)[] hasManyRelations = GetManyRelations(properties);

            var propertyNames = properties.Select(p => p.Name).ToArray();

            // only display fields that are not backing properties of the same name and type
            IField[] fields = type.GetFields(f => !IsHidden(f) // removes compiler-generated backing fields
                /* tries to remove remaining manual backing fields by matching type and name */
                && !properties.Any(p => f.ReturnType.Equals(p.ReturnType)
                    && Regex.IsMatch(f.Name, "_?" + p.Name, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking))).ToArray();

            #region split members up by declaring type
            // enables the diagrammer to exclude inherited members from derived types if they are already rendered in a base type
            Dictionary<IType, IProperty[]> flatPropertiesByType = properties.Except(hasOneRelations)
                .Except(hasManyRelations.Select(r => r.property)).GroupByDeclaringType();

            Dictionary<IType, IProperty[]> hasOneRelationsByType = hasOneRelations.GroupByDeclaringType();
            Dictionary<IType, (IProperty property, IType elementType)[]> hasManyRelationsByType = hasManyRelations.GroupByDeclaringType(r => r.property);
            Dictionary<IType, IField[]> fieldsByType = fields.GroupByDeclaringType();
            Dictionary<IType, IMethod[]> methodsByType = methods.GroupByDeclaringType();
            #endregion

            #region build diagram definitions for the type itself and members declared by it
            string members = flatPropertiesByType.GetValue(type).FormatAll(FormatFlatProperty)
                .Concat(methodsByType.GetValue(type).FormatAll(FormatMethod))
                .Concat(fieldsByType.GetValue(type).FormatAll(FormatField))
                .Join(Environment.NewLine + "    ", pad: true);

            // see https://mermaid.js.org/syntax/classDiagram.html#annotations-on-classes
            string? annotation = type.IsInterface() ? "Interface" : type.IsAbstract ? type.IsSealed ? "Service" : "Abstract" : null;

            string body = annotation == null ? members.TrimEnd(' ') : members + $"<<{annotation}>>" + Environment.NewLine;
            #endregion

            Dictionary<string, string>? docs = xmlDocs?.GetXmlDocs(type, fields, properties, methods);

            #region build diagram definitions for inherited members by declaring type
            string explicitTypePrefix = typeId + " : ";

            // get ancestor types this one is inheriting members from
            Dictionary<string, CD.Type.InheritedMembers> inheritedMembersByType = type.GetNonInterfaceBaseTypes().Where(t => t != type && !t.IsObject())
                // and group inherited members by declaring type
                .ToDictionary(GetId, t =>
                {
                    IEnumerable<string> flatMembers = flatPropertiesByType.GetValue(t).FormatAll(p => explicitTypePrefix + FormatFlatProperty(p))
                        .Concat(methodsByType.GetValue(t).FormatAll(m => explicitTypePrefix + FormatMethod(m)))
                        .Concat(fieldsByType.GetValue(t).FormatAll(f => explicitTypePrefix + FormatField(f)));

                    return new CD.Type.InheritedMembers
                    {
                        FlatMembers = flatMembers.Any() ? flatMembers.Join(Environment.NewLine) : null,
                        HasOne = MapHasOneRelations(hasOneRelationsByType, t),
                        HasMany = MapHasManyRelations(hasManyRelationsByType, t)
                    };
                });
            #endregion

            string typeName = GetName(type);

            return new CD.Type
            {
                Id = typeId,
                Name = typeName == typeId ? null : typeName,
                DiagramDefinition = $"class {typeId} {{{body}}}",
                HasOne = MapHasOneRelations(hasOneRelationsByType, type),
                HasMany = MapHasManyRelations(hasManyRelationsByType, type),
                BaseType = GetBaseType(type),
                Interfaces = GetInterfaces(type)?.ToArray(),
                InheritedMembersByDeclaringType = inheritedMembersByType,
                XmlDocs = docs
            };
        }

        private IProperty[] GetHasOneRelations(IProperty[] properties) => properties.Where(property =>
        {
            IType type = property.ReturnType;
            if (type.TryGetNullableType(out var typeArg)) type = typeArg;
            return selectedTypes!.Contains(type);
        }).ToArray();

        private (IProperty property, IType elementType)[] GetManyRelations(IProperty[] properties)
            => properties.Select(property =>
            {
                IType elementType = property.ReturnType.GetElementTypeFromIEnumerable(property.Compilation, true, out bool? isGeneric);

                if (isGeneric == false && elementType.IsObject())
                {
                    IProperty[] indexers = property.ReturnType.GetProperties(
                        p => p.IsIndexer && !p.ReturnType.IsObject(),
                        GetMemberOptions.IgnoreInheritedMembers).ToArray(); // TODO mayb order by declaring type instead of filtering

                    if (indexers.Length > 0) elementType = indexers.First().ReturnType;
                }

                return isGeneric == true && selectedTypes!.Contains(elementType) ? (property, elementType) : default;
            }).Where(pair => pair != default).ToArray();

        private void AddOutsideReference(string typeId, IType type)
        {
            if (!selectedTypes!.Contains(type) && outsideReferences?.ContainsKey(typeId) == false)
                outsideReferences.Add(typeId, type.Namespace + '.' + GetName(type));
        }

        private CD.Relationship? GetBaseType(IType type)
        {
            IType? relevantBaseType = type.DirectBaseTypes.SingleOrDefault(t => !t.IsInterface() && !t.IsObject());
            return relevantBaseType == null ? default : BuildRelationship(relevantBaseType);
        }

        private IEnumerable<CD.Relationship>? GetInterfaces(ITypeDefinition type)
        {
            var interfaces = type.DirectBaseTypes.Where(t => t.IsInterface()).ToArray();
            return interfaces.Length == 0 ? null : interfaces.Select(i => BuildRelationship(i));
        }

        /// <summary>Builds references to super types and (one/many) relations,
        /// recording outside references on the way and applying labels if required.</summary>
        /// <param name="type">The type to reference.</param>
        /// <param name="propertyName">Used only for property one/many relations.</param>
        private CD.Relationship BuildRelationship(IType type, string? propertyName = null)
        {
            (string id, IType? openGeneric) = GetIdAndOpenGeneric(type);
            AddOutsideReference(id, openGeneric ?? type);

            // label the relation with the property name if provided or the closed generic type for super types
            return new CD.Relationship { To = id, Label = propertyName ?? (openGeneric == null ? null : GetName(type)) };
        }

        private CD.Relationship[]? MapHasOneRelations(Dictionary<IType, IProperty[]> hasOneRelationsByType, IType type)
            => hasOneRelationsByType.GetValue(type)?.Select(p =>
            {
                IType type = p.ReturnType;
                string label = p.Name;

                if (type.TryGetNullableType(out var typeArg))
                {
                    type = typeArg;
                    label += " ?";
                }

                return BuildRelationship(type, label);
            }).ToArray();

        private CD.Relationship[]? MapHasManyRelations(Dictionary<IType, (IProperty property, IType elementType)[]> hasManyRelationsByType, IType type)
            => hasManyRelationsByType.GetValue(type)?.Select(relation =>
            {
                (IProperty property, IType elementType) = relation;
                return BuildRelationship(elementType, property.Name);
            }).ToArray();

        private string FormatMethod(IMethod method)
        {
            string parameters = method.Parameters.Select(p => $"{GetName(p.Type)} {p.Name}").Join(", ");
            string? modifier = method.IsAbstract ? "*" : method.IsStatic ? "$" : default;
            string name = method.Name;

            if (method.IsExplicitInterfaceImplementation)
            {
                IMember member = method.ExplicitlyImplementedInterfaceMembers.Single();
                name = GetName(member.DeclaringType) + '.' + member.Name;
            }

            string? typeArguments = method.TypeArguments.Count == 0 ? null : $"❰{method.TypeArguments.Select(GetName).Join(", ")}❱";
            return $"{GetAccessibility(method.Accessibility)}{name}{typeArguments}({parameters}){modifier} {GetName(method.ReturnType)}";
        }

        private string FormatFlatProperty(IProperty property)
        {
            char? visibility = GetAccessibility(property.Accessibility);
            string? modifier = property.IsAbstract ? "*" : property.IsStatic ? "$" : default;
            return $"{visibility}{GetName(property.ReturnType)} {property.Name}{modifier}";
        }

        private string FormatField(IField field)
        {
            string? modifier = field.IsAbstract ? "*" : field.IsStatic ? "$" : default;
            return $"{GetAccessibility(field.Accessibility)}{GetName(field.ReturnType)} {field.Name}{modifier}";
        }

        private string GetId(IType type) => GetIdAndOpenGeneric(type).Item1;

        /// <summary>For a non- or open generic <paramref name="type"/>, returns a unique identifier and null.
        /// For a closed generic <paramref name="type"/>, returns the open generic type and the unique identifier of it.
        /// That helps connecting closed generic references (e.g. Store&lt;int>) to their corresponding
        /// open generic <see cref="CD.Type"/> (e.g. Store&lt;T>) like in <see cref="BuildRelationship(IType, string?)"/>.</summary>
        private (string id, IType? openGeneric) GetIdAndOpenGeneric(IType type)
        {
            var openGeneric = type is ParameterizedType closed ? closed.GenericType : null;
            type = openGeneric ?? type; // reference open instead of closed generic type
            if (uniqueIds!.TryGetValue(type, out var uniqueId)) return (uniqueId, openGeneric); // types included by FilterTypes

            // types excluded by FilterTypes
            string? typeParams = type.TypeParameterCount == 0 ? null : ("_" + type.TypeParameters.Select(GetId).Join("_"));

            var id = type.FullName.Replace('.', '_')
                .Replace('<', '_').Replace('>', '_') // for module
                + typeParams; // to achive uniqueness for types with same FullName (i.e. generic overloads)

            uniqueIds![type] = id; // update dictionary to avoid re-generation
            return (id, openGeneric);
        }

        private string GetName(IType type)
        {
            if (labels!.ContainsKey(type)) return labels[type]; // return cached value
            return labels[type] = GenerateName(type); // generate and cache new value
        }

        private string GenerateName(IType type)
        {
            // non-generic types
            if (type.TypeParameterCount < 1)
            {
                if (type is ArrayType array) return GetName(array.ElementType) + "[]";
                if (type is ByReferenceType byReference) return "&" + GetName(byReference.ElementType);
                ITypeDefinition? typeDefinition = type.GetDefinition();

                if (typeDefinition == null)
                {
                    if (type.Kind != TypeKind.TypeParameter && type.Kind != TypeKind.Dynamic) Debugger.Break();
                    return type.Name;
                }

                if (typeDefinition.KnownTypeCode == KnownTypeCode.None)
                {
                    if (type.DeclaringType == null) return type.Name; // for module
                    else return type.DeclaringType.Name + '+' + type.Name; // nested types
                }

                return KnownTypeReference.GetCSharpNameByTypeCode(typeDefinition.KnownTypeCode) ?? type.Name;
            }

            // nullable types
            if (type.TryGetNullableType(out var nullableType)) return GetName(nullableType) + "?";

            // other generic types
            string typeArguments = type.TypeArguments.Select(GetName).Join(", ");
            return type.Name + $"❰{typeArguments}❱";
        }

        // see https://stackoverflow.com/a/16024302 for accessibility modifier flags
        private char? GetAccessibility(Accessibility access)
        {
            switch (access)
            {
                case Accessibility.Private: return '-';
                case Accessibility.ProtectedAndInternal:
                case Accessibility.Internal: return '~';
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal: return '#';
                case Accessibility.Public: return '+';
                case Accessibility.None:
                default: return default;
            }
        }

        internal static IEnumerable<IMethod> GetMethods(ITypeDefinition type) => type.GetMethods(m =>
            !m.IsOperator && !m.IsCompilerGenerated()
            && (m.DeclaringType == type // include methods if self-declared
                /* but exclude methods declared by object and their overrides, if inherited */
                || !m.DeclaringType.IsObject()
                    && (!m.IsOverride || !InheritanceHelper.GetBaseMember(m).DeclaringType.IsObject())));

        internal string GetSourceAssemblyVersion()
            => decompiler.TypeSystem.MainModule.PEFile.Metadata.GetAssemblyDefinition().Version.ToString();
    }
}