using System.CodeDom;
using System.CodeDom.Compiler;

namespace NetAmermaid
{
    /// <summary>Formats type names for human-readable output.
    /// Inspired by https://stackoverflow.com/a/6402967.</summary>
    public class TypeFormatter
    {
        private readonly CodeDomProvider provider;
        private readonly Func<Type, string> getName;
        private readonly Func<string, string> postProcess;

        public TypeFormatter(string language, Func<Type, string>? getName = null,
            Func<string, string>? postProcess = null)
        {
            provider = CodeDomProvider.CreateProvider(language);

            // safeguard against input being null because some types don't have a namespace
            this.getName = getName ?? (type => type.FullName ?? type.Name);

            this.postProcess = postProcess ?? (name => name);
        }

        public string GetName(Type type)
        {
            if (type.IsGenericParameter) return type.Name;
            var typeName = getName(type);

            if (type.IsGenericType)
            {
                var underlyingNullable = Nullable.GetUnderlyingType(type);

                if (underlyingNullable == null) // other generic type definition
                {
                    foreach (var paramType in type.GetGenericArguments().OrderByDescending(t => t.Namespace))
                    {
                        if (paramType.FullName == null) continue;
                        var newName = GetName(paramType);

                        if (typeName.Contains(paramType.FullName))
                            typeName = typeName.Replace(paramType.FullName, newName);
                    }
                }
                else if (underlyingNullable.FullName != null) // simplify nullable type name
                    typeName = typeName.Replace(underlyingNullable.FullName, GetName(underlyingNullable));
            }

            var readableName = provider.GetTypeOutput(new CodeTypeReference(typeName))
                // simplify primitive types
                .Replace("Void", "void").Replace(nameof(Boolean), "bool")
                .Replace(nameof(Int32), "int").Replace(nameof(Int64), "long")
                .Replace(nameof(Decimal), "decimal").Replace(nameof(String), "string")
                .Replace("@", null); // primitive types are prefixed with that for some reason in the output

            return postProcess(readableName);
        }
    }
}