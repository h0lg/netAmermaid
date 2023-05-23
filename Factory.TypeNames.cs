using System.Diagnostics;
using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    public partial class ClassDiagrammerFactory
    {
        /// <summary>Returns a cached display name for <paramref name="type"/>.</summary>
        private string GetName(IType type)
        {
            if (labels!.ContainsKey(type)) return labels[type]; // return cached value
            return labels[type] = GenerateName(type); // generate and cache new value
        }

        /// <summary>Generates a display name for <paramref name="type"/>.</summary>
        private string GenerateName(IType type)
        {
            // non-generic types
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

            // nullable types
            if (type.TryGetNullableType(out var nullableType)) return GetName(nullableType) + "?";

            // other generic types
            string typeArguments = type.TypeArguments.Select(GetName).Join(", ");
            return type.Name + $"❰{typeArguments}❱";
        }
    }
}