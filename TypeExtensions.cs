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

        /// <summary>Returns the implemented interface type and method for the specified
        /// <paramref name="implementation"/> (or default if it doesn't implement an interface method).</summary>
        internal static (Type, MethodInfo) GetInterfaceMethod(this MethodInfo implementation)
        {
            var signature = implementation.ToString();

            foreach (var contract in implementation.DeclaringType!.GetInterfaces())
            {
                var interfaceMap = implementation.DeclaringType.GetInterfaceMap(contract);

                foreach (var implemented in interfaceMap.TargetMethods)
                    if (implemented.ToString() == signature)
                    {
                        var index = Array.IndexOf(interfaceMap.TargetMethods, implemented);
                        return (interfaceMap.InterfaceType, interfaceMap.InterfaceMethods[index]);
                    }
            }

            return default;
        }
    }

    internal static class DictionaryExtensions
    {
        /// <summary>Returns the <paramref name="dictionary"/>s value for the specified <paramref name="key"/>
        /// if available and otherwise the default for <typeparamref name="Tout"/>.</summary>
        internal static Tout? GetValue<T, Tout>(this IDictionary<T, Tout> dictionary, T key)
            => dictionary.ContainsKey(key) ? dictionary[key] : default;
    }
}