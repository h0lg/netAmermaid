using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    using CD = ClassDiagrammer;

    partial class ClassDiagrammerFactory
    {
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

        private void AddOutsideReference(string typeId, IType type)
        {
            if (!selectedTypes!.Contains(type) && outsideReferences?.ContainsKey(typeId) == false)
                outsideReferences.Add(typeId, type.Namespace + '.' + GetName(type));
        }
    }
}