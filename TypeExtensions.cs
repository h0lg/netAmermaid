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
}