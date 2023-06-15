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

        /// <summary>Returns the relevant direct super type <paramref name="type"/> inherits from
        /// in a format matching <see cref="CD.Type.BaseType"/>.</summary>
        private Dictionary<string, string?>? GetBaseType(IType type)
        {
            IType? relevantBaseType = type.DirectBaseTypes.SingleOrDefault(t => !t.IsInterface() && !t.IsObject());
            return relevantBaseType == null ? default : new[] { BuildRelationship(relevantBaseType) }.ToDictionary(r => r.to, r => r.label);
        }

        /// <summary>Returns the direct interfaces implemented by <paramref name="type"/>
        /// in a format matching <see cref="CD.Type.Interfaces"/>.</summary>
        private Dictionary<string, string?>? GetInterfaces(ITypeDefinition type)
        {
            var interfaces = type.DirectBaseTypes.Where(t => t.IsInterface()).ToArray();
            return interfaces.Length == 0 ? null : interfaces.Select(i => BuildRelationship(i)).ToDictionary(r => r.to, r => r.label);
        }

        /// <summary>Returns the one-to-one relations from <paramref name="type"/> to other <see cref="CD.Type"/>s
        /// in a format matching <see cref="CD.Relationships.HasOne"/>.</summary>
        private Dictionary<string, string>? MapHasOneRelations(Dictionary<IType, IProperty[]> hasOneRelationsByType, IType type)
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
            }).ToDictionary(r => r.label!, r => r.to);

        /// <summary>Returns the one-to-many relations from <paramref name="type"/> to other <see cref="CD.Type"/>s
        /// in a format matching <see cref="CD.Relationships.HasMany"/>.</summary>
        private Dictionary<string, string>? MapHasManyRelations(Dictionary<IType, (IProperty property, IType elementType)[]> hasManyRelationsByType, IType type)
            => hasManyRelationsByType.GetValue(type)?.Select(relation =>
            {
                (IProperty property, IType elementType) = relation;
                return BuildRelationship(elementType, property.Name);
            }).ToDictionary(r => r.label!, r => r.to);

        /// <summary>Builds references to super types and (one/many) relations,
        /// recording outside references on the way and applying labels if required.</summary>
        /// <param name="type">The type to reference.</param>
        /// <param name="propertyName">Used only for property one/many relations.</param>
        private (string to, string? label) BuildRelationship(IType type, string? propertyName = null)
        {
            (string id, IType? openGeneric) = GetIdAndOpenGeneric(type);
            AddOutsideReference(id, openGeneric ?? type);

            // label the relation with the property name if provided or the closed generic type for super types
            string? label = propertyName ?? (openGeneric == null ? null : GetName(type));

            return (to: id, label);
        }

        private void AddOutsideReference(string typeId, IType type)
        {
            if (!selectedTypes!.Contains(type) && outsideReferences?.ContainsKey(typeId) == false)
                outsideReferences.Add(typeId, type.Namespace + '.' + GetName(type));
        }
    }
}