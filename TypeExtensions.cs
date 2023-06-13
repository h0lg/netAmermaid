using System.Diagnostics.CodeAnalysis;
using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    internal static class TypeExtensions
    {
        internal static bool IsObject(this IType t) => t.IsKnownType(KnownTypeCode.Object);
        internal static bool IsInterface(this IType t) => t.Kind == TypeKind.Interface;

        internal static bool TryGetNullableType(this IType type, [MaybeNullWhen(false)] out IType typeArg)
        {
            bool isNullable = type.IsKnownType(KnownTypeCode.NullableOfT);
            typeArg = isNullable ? type.TypeArguments.Single() : null;
            return isNullable;
        }
    }

    internal static class MemberInfoExtensions
    {
        /// <summary>Groups the <paramref name="members"/> into a dictionary
        /// with <see cref="IMember.DeclaringType"/> keys.</summary>
        internal static Dictionary<IType, T[]> GroupByDeclaringType<T>(this IEnumerable<T> members) where T : IMember
            => members.GroupByDeclaringType(m => m);

        /// <summary>Groups the <paramref name="objectsWithMembers"/> into a dictionary
        /// with <see cref="IMember.DeclaringType"/> keys using <paramref name="getMember"/>.</summary>
        internal static Dictionary<IType, T[]> GroupByDeclaringType<T>(this IEnumerable<T> objectsWithMembers, Func<T, IMember> getMember)
            => objectsWithMembers.GroupBy(m => getMember(m).DeclaringType).ToDictionary(g => g.Key, g => g.ToArray());
    }

    internal static class DictionaryExtensions
    {
        /// <summary>Returns the <paramref name="dictionary"/>s value for the specified <paramref name="key"/>
        /// if available and otherwise the default for <typeparamref name="Tout"/>.</summary>
        internal static Tout? GetValue<T, Tout>(this IDictionary<T, Tout> dictionary, T key)
            => dictionary.ContainsKey(key) ? dictionary[key] : default;
    }
}