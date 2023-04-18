using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    public class ClassDiagrammer
    {
        public Namespace[] Namespaces { get; set; } = null!;
        internal string[] Excluded { get; set; } = null!;

        /// <summary>A construct for grouping types included in an assembly by namespace
        /// to enable offering a structured type selection. Represents a <see cref="System.Type.Namespace"/>.</summary>
        public sealed class Namespace
        {
            /// <inheritdoc cref="System.Type.Namespace"/>
            public string? Name { get; set; }

            /// <summary>Types contained in the namespace for the consumer to decide which ones to display in detail on a diagram.</summary>
            public Type[] Types { get; set; } = null!;
        }

        /// <summary>Mermaid class diagram definitions and documentation information about a
        /// <see cref="System.Type"/> from the targeted assembly.</summary>
        public sealed class Type
        {
            /// <summary>Uniquely identifies the <see cref="System.Type"/> in the scope of the targeted assembly
            /// as well as any HTML diagrammer rendered from it.
            /// Should match \w+ to be safe to use as select option value and
            /// part of the DOM id of the SVG node rendered for this type.</summary>
            public string Id { get; set; } = null!;

            /// <summary>The human-readable label for the type. Doesn't include the namespace and is therefore
            /// not guaranteed to be unique in the scope of the targeted assembly or a diagram rendered from it.</summary>
            public string Name { get; set; } = null!;

            /// <summary>Contains the definition of the type and its own (uninherited) members
            /// in mermaid class diagram syntax, see https://mermaid.js.org/syntax/classDiagram.html .</summary>
            public string DiagramDefinition { get; set; } = null!;

            /// <summary>Contains the definition of the type and its own (uninherited) members
            /// in mermaid class diagram syntax, see https://mermaid.js.org/syntax/classDiagram.html .</summary>
            public Dictionary<string, string>? BaseType { get; set; }
            public IDictionary<string, string>? Interfaces { get; set; }

            /// <summary>Contains the mermaid class diagram definitions for inherited members by their <see cref="IMember.DeclaringType"/>.
            /// for the consumer to choose which of them to display in an inheritance scenario.</summary>
            public IDictionary<string, string>? InheritedMembersByDeclaringType { get; set; }

            /// <summary>Contains the XML documentation comments for this type
            /// (using a <see cref="string.Empty"/> key) and its members, if available.</summary>
            public IDictionary<string, string>? XmlDocs { get; set; }
        }
    }
}