using System.Reflection;
using System.Runtime.CompilerServices;

namespace NetAmermaid
{
    internal static class TypeExtensions
    {
        /// <summary>Returns the collection element type if <paramref name="type"/>
        /// is a collection or null if not.</summary>
        internal static Type? GetItemType(this Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType) return type.GetGenericArguments()[0];
            return default;
        }

        /// <summary>Returns <paramref name="types"/> without compiler
        /// is a collection or null if not.</summary>
        internal static IEnumerable<Type> ExceptCompilerGenerated(this Type[] types)
        {
            var compilerGenerated = types.Where(t => t.GetCustomAttribute<CompilerGeneratedAttribute>() != null).ToArray();
            var nestedInCompilerGenerated = types.Where(t => t.IsNested && compilerGenerated.Contains(t.DeclaringType));
            return types.Except(compilerGenerated).Except(nestedInCompilerGenerated);
        }
    }

    internal static class MemberInfoExtensions
    {
        /// <summary>Groups the <paramref name="members"/> into a dictionary
        /// with <see cref="MemberInfo.DeclaringType"/> keys.</summary>
        internal static Dictionary<Type, T[]> GroupByDeclaringType<T>(this IEnumerable<T> members) where T : MemberInfo
            => members.GroupByDeclaringType(m => m);

        /// <summary>Groups the <paramref name="objectsWithMembers"/> into a dictionary
        /// with <see cref="MemberInfo.DeclaringType"/> keys using <paramref name="getMember"/>.</summary>
        internal static Dictionary<Type, T[]> GroupByDeclaringType<T>(this IEnumerable<T> objectsWithMembers, Func<T, MemberInfo> getMember)
            => objectsWithMembers.GroupBy(m => getMember(m).DeclaringType!).ToDictionary(g => g.Key, g => g.ToArray());
    }

    internal static class DictionaryExtensions
    {
        internal static Tout? GetValue<T, Tout>(this IDictionary<T, Tout> dictionary, T key)
            => dictionary.ContainsKey(key) ? dictionary[key] : default;
    }
}