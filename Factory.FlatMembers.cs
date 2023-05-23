using System.Text.RegularExpressions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    partial class ClassDiagrammerFactory
    {
        /// <summary>Wraps a <see cref="CSharpDecompiler"/> method configurable via <see cref="decompilerSettings"/>
        /// that can be used to determine whether a member should be hidden.</summary>
        private bool IsHidden(IEntity entity) => CSharpDecompiler.MemberIsHidden(entity.ParentModule!.PEFile, entity.MetadataToken, decompilerSettings);

        private IField[] GetFields(ITypeDefinition type, IProperty[] properties)
            // only display fields that are not backing properties of the same name and type
            => type.GetFields(f => !IsHidden(f) // removes compiler-generated backing fields
                /* tries to remove remaining manual backing fields by matching type and name */
                && !properties.Any(p => f.ReturnType.Equals(p.ReturnType)
                    && Regex.IsMatch(f.Name, "_?" + p.Name, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking))).ToArray();

        private static IEnumerable<IMethod> GetMethods(ITypeDefinition type) => type.GetMethods(m =>
            !m.IsOperator && !m.IsCompilerGenerated()
            && (m.DeclaringType == type // include methods if self-declared
                /* but exclude methods declared by object and their overrides, if inherited */
                || !m.DeclaringType.IsObject()
                    && (!m.IsOverride || !InheritanceHelper.GetBaseMember(m).DeclaringType.IsObject())));

        private string FormatMethod(IMethod method)
        {
            string parameters = method.Parameters.Select(p => $"{GetName(p.Type)} {p.Name}").Join(", ");
            string? modifier = method.IsAbstract ? "*" : method.IsStatic ? "$" : default;
            string name = method.Name;

            if (method.IsExplicitInterfaceImplementation)
            {
                IMember member = method.ExplicitlyImplementedInterfaceMembers.Single();
                name = GetName(member.DeclaringType) + '.' + member.Name;
            }

            string? typeArguments = method.TypeArguments.Count == 0 ? null : $"❰{method.TypeArguments.Select(GetName).Join(", ")}❱";
            return $"{GetAccessibility(method.Accessibility)}{name}{typeArguments}({parameters}){modifier} {GetName(method.ReturnType)}";
        }

        private string FormatFlatProperty(IProperty property)
        {
            char? visibility = GetAccessibility(property.Accessibility);
            string? modifier = property.IsAbstract ? "*" : property.IsStatic ? "$" : default;
            return $"{visibility}{GetName(property.ReturnType)} {property.Name}{modifier}";
        }

        private string FormatField(IField field)
        {
            string? modifier = field.IsAbstract ? "*" : field.IsStatic ? "$" : default;
            return $"{GetAccessibility(field.Accessibility)}{GetName(field.ReturnType)} {field.Name}{modifier}";
        }

        // see https://stackoverflow.com/a/16024302 for accessibility modifier flags
        private char? GetAccessibility(Accessibility access)
        {
            switch (access)
            {
                case Accessibility.Private: return '-';
                case Accessibility.ProtectedAndInternal:
                case Accessibility.Internal: return '~';
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal: return '#';
                case Accessibility.Public: return '+';
                case Accessibility.None:
                default: return default;
            }
        }
    }
}