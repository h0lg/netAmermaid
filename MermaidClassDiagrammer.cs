using System.Reflection;
using System.Runtime.CompilerServices;

namespace NetAmermaid
{
    public class MermaidClassDiagrammer
    {
        private static readonly string twoLineBreaks = Environment.NewLine + Environment.NewLine;

        private static readonly BindingFlags memberBindingFlags = BindingFlags.Static | BindingFlags.Instance
            | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly TypeFormatter typeFormatter;

        public MermaidClassDiagrammer() => typeFormatter = new TypeFormatter("c#",
            getName: type =>
            {
                if (string.IsNullOrEmpty(type.Namespace)) return type.Name; // no need to continue

                /* prevent generic expressions that lead to parser errors;
                 * more than one generice type arg is currently not supported,
                 * see https://github.com/mermaid-js/mermaid/issues/3188 */
                if (type.IsGenericType && (type.GenericTypeArguments.Length > 1
                    || type.FullName == null)) // workaround e.g. for parameter or return types for methods with a type parameter
                    return type.Name.Substring(0, type.Name.IndexOf('`')) + $"~{type.GenericTypeArguments.Length}~";

                var name = type.FullName ?? type.Name;

                /* strip namespace of type completely, but only first occurance;
                 * method will be called for generic type arguments */
                var start = name.IndexOf(type.Namespace);
                return start < 0 ? name : name.Remove(start, type.Namespace.Length + 1);
            },
            postProcess: name => name.Replace('.', '_') // for nested types
                .ReplaceAll(new[] { "<", ">" }, "~")); // translate generic type expressions

        public IEnumerable<Namespace> GetDefinitions(Type[] types)
            => types.GroupBy(t => t.Namespace).Select(ns => new Namespace
            {
                Name = ns.Key,
                Types = ns.OrderBy(t => t.FullName).ToDictionary(type => GetName(type), type => type.IsEnum ? GetEnumDefinition(type) : GetDefinition(type, types))
            }).OrderBy(ns => ns.Name);

        private string GetEnumDefinition(Type type)
        {
            var name = GetName(type);

            var fields = type.GetFields().Where(f => !f.IsSpecialName)
                .Select(f => f.Name).Join(Environment.NewLine + "    ", pad: true);

            return $"class {name} {{{fields}<<Enumeration>>}}";
        }

        private string GetDefinition(Type type, Type[] types)
        {
            var typeName = GetName(type);
            var methods = GetMethods(type);
            var properties = type.GetProperties(memberBindingFlags);
            var hasOneRelations = properties.Where(p => types.Contains(p.PropertyType)).ToArray();

            var hasManyRelations = properties.Select(property =>
            {
                var itemType = property.PropertyType.GetItemType();
                return itemType != null && types.Contains(itemType) ? (property, itemType) : default;
            }).Where(pair => pair != default).ToArray();

            var propertyNames = properties.Select(p => p.Name).ToArray();

            var fields = type.GetFields(memberBindingFlags)
                // only display fields that are not backing properties of the same name and type
                .Where(f => f.GetCustomAttribute<CompilerGeneratedAttribute>() == null
                    && !propertyNames.ContainsIgnoreCase(f.Name) && !propertyNames.ContainsIgnoreCase("_" + f.Name)).ToArray();

            var members = properties.Select(FormatDataProperty)
               .Concat(methods.Select(FormatMethod))
               .Concat(fields.Select(FormatField))
               .Join(Environment.NewLine + "    ", pad: true);

            // see https://mermaid.js.org/syntax/classDiagram.html#annotations-on-classes
            var annotation = type.IsInterface ? "Interface" : type.IsAbstract ? type.IsSealed ? "Service" : "Abstract" : null;

            var body = annotation == null ? members.TrimEnd(' ') : members + $"<<{annotation}>>" + Environment.NewLine;
            var baseType = type.BaseType != null && type.BaseType != typeof(object) ? $"{GetName(type.BaseType)}<|--{typeName}" : null;

            var relationships = type.GetInterfaces().Where(i => types.Contains(i)).Select(i => $"{GetName(i)}<|..{typeName}")
                .Prepend(baseType)
                .Concat(FormatHasOneRelations(typeName, hasOneRelations))
                .Concat(FormatHasManyRelations(typeName, hasManyRelations))
                .Where(line => !string.IsNullOrEmpty(line))
                .Join(Environment.NewLine);

            return $"class {typeName} {{{body}}}" + twoLineBreaks + relationships;
        }

        private IEnumerable<string> FormatHasManyRelations(string typeName, IEnumerable<(PropertyInfo property, Type itemType)> relations)
            => relations.Select(relation =>
            {
                var (property, itemType) = relation;
                return $@"{typeName} --> ""*"" {GetName(itemType)} : {property.Name}";
            });

        private IEnumerable<string> FormatHasOneRelations(string typeName, IEnumerable<PropertyInfo> relations)
            => relations.Select(p => $"{typeName} --> {GetName(p.PropertyType)} : {p.Name}");

        private string FormatMethod(MethodInfo method)
        {
            var parameters = method.GetParameters().Select(p => $"{GetName(p.ParameterType)} {p.Name}").Join(", ");
            var modifier = method.IsAbstract ? "*" : method.IsStatic ? "$" : default;
            return $"{GetAccessibility(method)}{method.Name}({parameters}){modifier} {GetName(method.ReturnType)}";
        }

        private string FormatDataProperty(PropertyInfo property)
        {
            var visibility = new string(property.GetAccessors().Select(GetAccessibility).Distinct().ToArray());
            return $"{visibility}{GetName(property.PropertyType)} {property.Name}";
        }

        private string FormatField(FieldInfo field) => $"{GetAccessibility(field)}{GetName(field.FieldType)} {field.Name}";
        private string GetName(Type type) => typeFormatter.GetName(type); // to reduce noise

        // see https://stackoverflow.com/a/16024302 for accessibility modifier flags
        private char GetAccessibility(MethodInfo m) => m.IsPublic ? '+' : m.IsFamily ? '#' : m.IsAssembly ? '~' : '-';
        private char GetAccessibility(FieldInfo m) => m.IsPublic ? '+' : m.IsFamily ? '#' : m.IsAssembly ? '~' : '-';

        #region GetMethods
        // for removing property accessors and event subscription methods, see https://stackoverflow.com/q/16344844
        private static readonly string[] ignoredMethodPrefixes = new[] { "get_", "set_", "add_", "remove_" };

        internal static IEnumerable<MethodInfo> GetMethods(Type type) => type.GetMethods(memberBindingFlags)
            .Where(method => !method.IsSpecialName // operator overloads
                && method.GetCustomAttribute<CompilerGeneratedAttribute>() == null // compiler-generated
                /* remove methods declared by object and their overrides */
                && method.DeclaringType != typeof(object) && method.GetBaseDefinition()?.DeclaringType != typeof(object)
                && !ignoredMethodPrefixes.Any(prefix => method.Name.StartsWith(prefix)));
        #endregion

        /// <summary>A construct for grouping types included in an assambly by namespace
        /// to enable offering a structured type selection. Represents a <see cref="System.Type.Namespace"/>.</summary>
        public sealed class Namespace
        {
            /// <inheritdoc cref="System.Type.Namespace"/>
            public string? Name { get; set; }

            /// <summary>Types contained in the namespace for the consumer to decide which ones to display in detail on a diagram.</summary>
            public Dictionary<string, string> Types { get; set; } = null!;
        }
    }
}