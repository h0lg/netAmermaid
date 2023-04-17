using System.Diagnostics;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    /* See class diagram syntax
     * reference (may be outdated!) https://mermaid.js.org/syntax/classDiagram.html
     * lexical definition https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/diagrams/class/parser/classDiagram.jison */

    /// <summary>Produces mermaid class diagram syntax for a filtered list of types from a specified .Net assembly.</summary>
    public class MermaidClassDiagrammer
    {
        private static readonly string twoLineBreaks = Environment.NewLine + Environment.NewLine;

        private readonly XmlDocumentationFormatter? xmlDocs;
        private readonly Func<IEntity, bool> IsHidden;
        private readonly ITypeDefinition[] types;

        public MermaidClassDiagrammer(string assemblyPath, XmlDocumentationFormatter? xmlDocs)
        {
            this.xmlDocs = xmlDocs;

            DecompilerSettings settings = new(LanguageVersion.Latest)
            {
                AutomaticProperties = true, // this setting is important for IsHidden to return true for backing fields
                //ShowXmlDocumentation = true
            };

            CSharpDecompiler decompiler = new(assemblyPath, settings)
            {
                //DocumentationProvider = docs
            };

            IsHidden = (IEntity entity) => CSharpDecompiler.MemberIsHidden(entity.ParentModule.PEFile, entity.MetadataToken, settings);

            //var assembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
            types = decompiler.TypeSystem.MainModule.TypeDefinitions
                .Where(type => !type.IsCompilerGeneratedOrIsInCompilerGeneratedClass())
                .ToArray();
        }

        public IEnumerable<Namespace> GetDefinitions() => types.GroupBy(t => t.Namespace).Select(ns => new Namespace
        {
            Name = ns.Key,
            Types = ns.OrderBy(t => t.FullName)
                .Select(type => type.Kind == TypeKind.Enum ? GetEnumDefinition(type) : GetDefinition(type, types)).ToArray()
        }).OrderBy(ns => ns.Name);

        private Namespace.Type GetEnumDefinition(ITypeDefinition type)
        {
            IField[] fields = type.GetFields(f => f.IsConst && f.IsStatic && f.Accessibility == Accessibility.Public).ToArray();
            Dictionary<string, string>? docs = xmlDocs?.GetXmlDocs(type, fields);
            string name = GetName(type), typeId = GetId(type);

            var body = fields.Select(f => f.Name).Prepend("<<Enumeration>>")
                .Join(Environment.NewLine + "    ", pad: true).TrimEnd(' ');

            return new Namespace.Type
            {
                Id = typeId,
                Name = name,
                DiagramDefinition = $"class {typeId} [\"{name}\"] {{{body}}}",
                XmlDocs = docs
            };
        }

        private Namespace.Type GetDefinition(ITypeDefinition type, ITypeDefinition[] types)
        {
            string typeId = GetId(type);
            IMethod[] methods = GetMethods(type).ToArray();
            IProperty[] properties = type.GetProperties().ToArray();
            IProperty[] hasOneRelations = properties.Where(property => types.Contains(property.ReturnType)).ToArray();
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
            string? annotation = type.Kind == TypeKind.Interface ? "Interface" : type.IsAbstract ? type.IsSealed ? "Service" : "Abstract" : null;

            string body = annotation == null ? members.TrimEnd(' ') : members + $"<<{annotation}>>" + Environment.NewLine;

            string relationships = FormatHasOneRelations(typeId, hasOneRelationsByType.GetValue(type))
                .Concat(FormatHasManyRelations(typeId, hasManyRelationsByType.GetValue(type)))
                .Where(line => !string.IsNullOrEmpty(line))
                .Join(Environment.NewLine);
            #endregion

            Dictionary<string, string>? docs = xmlDocs?.GetXmlDocs(type, fields, properties, methods);

            #region build diagram definitions for inherited members by declaring type
            string explicitTypePrefix = typeId + " : ";

            // get ancestor types this one is inheriting members from
            Dictionary<string, string> inheritedMembersByType = type.GetNonInterfaceBaseTypes().Where(t => t != type && !t.IsObject())
                // and group inherited members by declaring type
                .ToDictionary(GetId, t => flatPropertiesByType.GetValue(t).FormatAll(p => explicitTypePrefix + FormatFlatProperty(p))
                    .Concat(methodsByType.GetValue(t).FormatAll(m => explicitTypePrefix + FormatMethod(m)))
                    .Concat(fieldsByType.GetValue(t).FormatAll(f => explicitTypePrefix + FormatField(f)))
                    .Concat(FormatHasOneRelations(typeId, hasOneRelationsByType.GetValue(t)))
                    .Concat(FormatHasManyRelations(typeId, hasManyRelationsByType.GetValue(t)))
                    .Join(Environment.NewLine));
            #endregion

            string typeName = GetName(type);
            (string? baseTypeId, string? baseTypeDefinition) = FormatBaseType(type, typeId) ?? default;
            Dictionary<string, string>? interfaces = FormatInterfaces(type, typeId);

            return new Namespace.Type
            {
                Id = typeId,
                Name = typeName,
                DiagramDefinition = $"class {typeId} [\"{typeName}\"] {{{body}}}" + twoLineBreaks + relationships,
                BaseType = baseTypeDefinition == default ? null : new() { { baseTypeId, baseTypeDefinition } },
                Interfaces = interfaces,
                InheritedMembersByDeclaringType = inheritedMembersByType,
                XmlDocs = docs
            };
        }

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

                return isGeneric == true && types!.Contains(elementType) ? (property, elementType) : default;
            }).Where(pair => pair != default).ToArray();

        private (string, string)? FormatBaseType(IType type, string typeId)
        {
            IType? relevantBaseType = type.DirectBaseTypes.SingleOrDefault(t =>
                t.Kind != TypeKind.Interface && !t.IsObject());

            if (relevantBaseType == null) return default;
            string baseTypeId = GetId(relevantBaseType);
            var parameterized = relevantBaseType as ParameterizedType;
            string? relationLabel = parameterized == null ? null : $" : {GetName(relevantBaseType)}";
            string relatedLabel = LabelRelated(parameterized == null ? relevantBaseType : parameterized.GenericType, baseTypeId);
            return (baseTypeId, $"{baseTypeId} <|-- {typeId}" + relationLabel + relatedLabel);
        }

        private Dictionary<string, string>? FormatInterfaces(ITypeDefinition type, string typeId)
        {
            var interfaces = type.DirectBaseTypes.Where(t => t.Kind == TypeKind.Interface).ToArray();
            if (interfaces.Length == 0) return null;

            return interfaces.Select(iface =>
            {
                string interfaceId = GetId(iface);
                return (interfaceId, definition: $"{interfaceId} <|.. {typeId}" + LabelRelated(iface, interfaceId));
            }).ToDictionary(t => t.interfaceId, t => t.definition);
        }

        private IEnumerable<string> FormatHasManyRelations(string typeId, IEnumerable<(IProperty, IType)>? relations)
            => relations.FormatAll(relation =>
            {
                (IProperty property, IType elementType) = relation;
                string relatedId = GetId(elementType);
                return $@"{typeId} --> ""*"" {relatedId} : {property.Name}" + LabelRelated(elementType, relatedId);
            });

        private IEnumerable<string> FormatHasOneRelations(string typeId, IEnumerable<IProperty>? relations)
            => relations.FormatAll(p =>
            {
                string relatedId = GetId(p.ReturnType);
                return $"{typeId} --> {relatedId} : {p.Name}" + LabelRelated(p.ReturnType, relatedId);
            });

        private string LabelRelated(IType type, string typeId) => Environment.NewLine + $"class {typeId} [\"{GetName(type)}\"]";

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

        private string GetId(IType type)
        {
            if (type is ParameterizedType generic) type = generic.GenericType;
            string? typeParams = type.TypeParameterCount == 0 ? null : ("_" + type.TypeParameters.Select(GetId).Join("_"));

            return type.FullName.Replace('.', '_')
                .Replace('<', '_').Replace('>', '_') // for module
                + typeParams; // to achive uniqueness for types with same FullName (i.e. generic overloads)
        }

        private string GetName(IType type)
        {
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

            if (type.IsKnownType(KnownTypeCode.NullableOfT)) return GetName(type.TypeArguments.Single()) + "?";

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

        /// <summary>A construct for grouping types included in an assambly by namespace
        /// to enable offering a structured type selection. Represents a <see cref="System.Type.Namespace"/>.</summary>
        public sealed class Namespace
        {
            /// <inheritdoc cref="System.Type.Namespace"/>
            public string? Name { get; set; }

            /// <summary>Types contained in the namespace for the consumer to decide which ones to display in detail on a diagram.</summary>
            public Type[] Types { get; set; } = null!;

            /// <summary>Mermaid class diagram definitions and documentation information about a
            /// <see cref="System.Type"/> from the targeted assembly.</summary>
            public sealed class Type
            {
                /// <summary>Uniquely identifies the <see cref="System.Type"/> in the scope of the targeted assembly
                /// as well as any HTML diagrammer rendered from it.
                /// Should match \w+ to be safe to use as select option value and
                /// part of the DOM id of the SVG node rendered for this type.</summary>
                public string Id { get; set; } = null!;

                /// <summary>The human-readable label for the type. Doesn't include the namespace and is therefore
                /// not guaranteed to be unique in the scope of the targeted assembly or a diagram rendered from it.</summary>
                public string Name { get; set; } = null!;

                /// <summary>Contains the definition of the type and its own (uninherited) members
                /// in mermaid class diagram syntax, see https://mermaid.js.org/syntax/classDiagram.html .</summary>
                public string DiagramDefinition { get; set; } = null!;

                /// <summary>Contains the definition of the type and its own (uninherited) members
                /// in mermaid class diagram syntax, see https://mermaid.js.org/syntax/classDiagram.html .</summary>
                public Dictionary<string, string>? BaseType { get; set; }
                public IDictionary<string, string>? Interfaces { get; set; }

                /// <summary>Contains the mermaid class diagram definitions for inherited members by their <see cref="IMember.DeclaringType"/>.
                /// for the consumer to choose which of them to display in an inheritance scenario.</summary>
                public IDictionary<string, string>? InheritedMembersByDeclaringType { get; set; }

                /// <summary>Contains the XML documentation comments for this type
                /// (using a <see cref="string.Empty"/> key) and its members, if available.</summary>
                public IDictionary<string, string>? XmlDocs { get; set; }
            }
        }
    }
}