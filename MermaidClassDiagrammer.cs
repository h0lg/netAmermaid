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
        private readonly XmlDocumentationFile xmlDocs;

        public MermaidClassDiagrammer(XmlDocumentationFile xmlDocs)
        {
            this.xmlDocs = xmlDocs;

            typeFormatter = new TypeFormatter("c#",
                getName: type =>
                {
                    if (string.IsNullOrEmpty(type.Namespace)) return type.Name; // no need to continue

                    /* prevent generic expressions that lead to parser errors;
                     * more than one generice type arg is currently not supported,
                     * see https://github.com/mermaid-js/mermaid/issues/3188 */
                    if (type.IsGenericType && (type.GenericTypeArguments.Length > 1
                        || type.FullName == null)) // workaround e.g. for parameter or return types for methods with a type parameter
                    {
                        var startOfGenericDef = type.Name.IndexOf('`');
                        var nonGenericName = startOfGenericDef < 0 ? type.Name : type.Name.Substring(0, startOfGenericDef);
                        return nonGenericName + $"~{type.GenericTypeArguments.Length}~";
                    }

                    var name = type.FullName ?? type.Name;

                    /* strip namespace of type completely, but only first occurance;
                     * method will be called for generic type arguments */
                    var start = name.IndexOf(type.Namespace);
                    return start < 0 ? name : name.Remove(start, type.Namespace.Length + 1);
                },
                postProcess: name => name.Replace('.', '_') // for nested types
                    .ReplaceAll(new[] { "<", ">" }, "~")); // translate generic type expressions
        }

        public IEnumerable<Namespace> GetDefinitions(Type[] types)
            => types.GroupBy(t => t.Namespace).Select(ns => new Namespace
            {
                Name = ns.Key,
                Types = ns.OrderBy(t => t.FullName).Select(type => type.IsEnum ? GetEnumDefinition(type) : GetDefinition(type, types)).ToArray()
            }).OrderBy(ns => ns.Name);

        private Namespace.Type GetEnumDefinition(Type type)
        {
            var name = GetName(type);
            var fields = type.GetFields().Where(f => !f.IsSpecialName).ToArray();
            var fieldDefinitions = fields.Select(f => f.Name).Join(Environment.NewLine + "    ", pad: true);

            Dictionary<string, string>? docs = default;

            if (xmlDocs.HasEntries)
            {
                docs = new Dictionary<string, string>();
                AddXmlDocEntry(docs, xmlDocs.ForType(type));
                foreach (var field in fields) AddXmlDocEntry(docs, xmlDocs.ForField(field), field);
            }

            return new Namespace.Type
            {
                Name = name,
                DiagramDefinition = $"class {name} {{{fieldDefinitions}<<Enumeration>>}}",
                XmlDocs = docs?.Keys.Any() == true ? docs : default
            };
        }

        private Namespace.Type GetDefinition(Type type, Type[] types)
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

            #region split members up by declaring type
            // to enable the diagrammer to exclude inherited members from sub classes if they are already rendered in a super class
            var flatPropertiesByType = properties.Except(hasOneRelations)
                .Except(hasManyRelations.Select(r => r.property)).GroupByDeclaringType();

            var hasOneRelationsByType = hasOneRelations.GroupByDeclaringType();
            var hasManyRelationsByType = hasManyRelations.GroupByDeclaringType(r => r.property);
            var fieldsByType = fields.GroupByDeclaringType();
            var methodsByType = methods.GroupByDeclaringType();
            #endregion

            #region build diagram definitions for the type itself and members declared by it
            var members = flatPropertiesByType.GetValue(type).FormatAll(FormatFlatProperty)
                .Concat(methodsByType.GetValue(type).FormatAll(FormatMethod))
                .Concat(fieldsByType.GetValue(type).FormatAll(FormatField))
                .Join(Environment.NewLine + "    ", pad: true);

            // see https://mermaid.js.org/syntax/classDiagram.html#annotations-on-classes
            var annotation = type.IsInterface ? "Interface" : type.IsAbstract ? type.IsSealed ? "Service" : "Abstract" : null;

            var body = annotation == null ? members.TrimEnd(' ') : members + $"<<{annotation}>>" + Environment.NewLine;
            var baseType = type.BaseType != null && type.BaseType != typeof(object) ? $"{GetName(type.BaseType)}<|--{typeName}" : null;

            var relationships = type.GetInterfaces().Where(i => types.Contains(i)).Select(i => $"{GetName(i)}<|..{typeName}")
                .Prepend(baseType)
                .Concat(FormatHasOneRelations(typeName, hasOneRelationsByType.GetValue(type)))
                .Concat(FormatHasManyRelations(typeName, hasManyRelationsByType.GetValue(type)))
                .Where(line => !string.IsNullOrEmpty(line))
                .Join(Environment.NewLine);
            #endregion

            #region gather XML documentation for the type and displayed members
            Dictionary<string, string>? docs = default;

            if (xmlDocs.HasEntries)
            {
                docs = new Dictionary<string, string>();
                AddXmlDocEntry(docs, xmlDocs.ForType(type));
                foreach (var m in methods) AddXmlDocEntry(docs, xmlDocs.ForMethod(m), m);
                foreach (var p in properties) AddXmlDocEntry(docs, xmlDocs.ForProperty(p), p);
                foreach (var f in fields) AddXmlDocEntry(docs, xmlDocs.ForField(f), f);
            }
            #endregion

            #region build diagram definitions for inherited members by declaring type
            var explicitTypePrefix = typeName + " : ";

            // get ancestor types this one is inheriting members from
            var inheritedMembersByType = flatPropertiesByType.Keys.Union(methodsByType.Keys).Union(fieldsByType.Keys)
                .Union(hasOneRelationsByType.Keys).Union(hasManyRelationsByType.Keys).Where(t => t != type)
                // and group inherited members by declaring type
                .ToDictionary(GetName, t => flatPropertiesByType.GetValue(t).FormatAll(p => explicitTypePrefix + FormatFlatProperty(p))
                    .Concat(methodsByType.GetValue(t).FormatAll(m => explicitTypePrefix + FormatMethod(m)))
                    .Concat(fieldsByType.GetValue(t).FormatAll(f => explicitTypePrefix + FormatField(f)))
                    .Concat(FormatHasOneRelations(typeName, hasOneRelationsByType.GetValue(t)))
                    .Concat(FormatHasManyRelations(typeName, hasManyRelationsByType.GetValue(t)))
                    .Join(Environment.NewLine));
            #endregion

            return new Namespace.Type
            {
                Name = typeName,
                DiagramDefinition = $"class {typeName} {{{body}}}" + twoLineBreaks + relationships,
                InheritedMembersByDeclaringType = inheritedMembersByType,
                XmlDocs = docs?.Keys.Any() == true ? docs : default
            };
        }

        private IEnumerable<string> FormatHasManyRelations(string typeName, IEnumerable<(PropertyInfo property, Type itemType)>? relations)
            => relations.FormatAll(relation =>
            {
                var (property, itemType) = relation;
                return $@"{typeName} --> ""*"" {GetName(itemType)} : {property.Name}";
            });

        private IEnumerable<string> FormatHasOneRelations(string typeName, IEnumerable<PropertyInfo>? relations)
            => relations.FormatAll(p => $"{typeName} --> {GetName(p.PropertyType)} : {p.Name}");

        private string FormatMethod(MethodInfo method)
        {
            var parameters = method.GetParameters().Select(p => $"{GetName(p.ParameterType)} {p.Name}").Join(", ");
            var modifier = method.IsAbstract ? "*" : method.IsStatic ? "$" : default;
            var name = method.Name;

            if (name.Contains(".")) // interface implementations
            {
                var (iface, meth) = method.GetInterfaceMethod();

                /* implemented interface method names could be prefixed with the interaface
                 * name here, but mermaid syntax doesn't seem to support that currently */
                name = meth.Name;
            }

            return $"{GetAccessibility(method)}{name}({parameters}){modifier} {GetName(method.ReturnType)}";
        }

        private string FormatFlatProperty(PropertyInfo property)
        {
            var visibility = new string(property.GetAccessors().Select(GetAccessibility).Distinct().ToArray());
            return $"{visibility}{GetName(property.PropertyType)} {property.Name}";
        }

        private string FormatField(FieldInfo field) => $"{GetAccessibility(field)}{GetName(field.FieldType)} {field.Name}";
        private string GetName(Type type) => typeFormatter.GetName(type); // to reduce noise

        private static void AddXmlDocEntry(Dictionary<string, string> docs, string? doc, MemberInfo? member = null)
        {
            if (!string.IsNullOrEmpty(doc)) docs[member?.Name ?? string.Empty] = doc;
        }

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
            public Type[] Types { get; set; } = null!;

            /// <summary>Mermaid class diagram definitions and documentation information about a
            /// <see cref="System.Type"/> from the targeted assembly.</summary>
            public sealed class Type
            {
                public string Name { get; set; } = null!;

                /// <summary>Contains the definition of the type and its own (uninherited) members
                /// in mermaid class diagram syntax, see https://mermaid.js.org/syntax/classDiagram.html .</summary>
                public string DiagramDefinition { get; set; } = null!;

                /// <summary>Contains the mermaid class diagram definitions for inherited members by their <see cref="MemberInfo.DeclaringType"/>.
                /// for the consumer to choose which of them to display in an inheritance scenario.</summary>
                public IDictionary<string, string>? InheritedMembersByDeclaringType { get; set; }

                /// <summary>Contains the XML documentation comments for this type (using a <see cref="string.Empty"/> key)
                /// and its members, if available.</summary>
                public IDictionary<string, string>? XmlDocs { get; set; }
            }
        }
    }
}