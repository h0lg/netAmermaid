using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    using CD = ClassDiagrammer;

    partial class ClassDiagrammerFactory
    {
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
            IField[] fields = GetFields(type, properties);

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
    }
}