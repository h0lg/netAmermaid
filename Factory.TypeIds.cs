using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    using CD = ClassDiagrammer;

    public partial class ClassDiagrammerFactory
    {
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

        private string GetId(IType type) => GetIdAndOpenGeneric(type).id;

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
    }
}