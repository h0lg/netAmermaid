using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    public class ClassDiagrammer
    {
        public Namespace[] Namespaces { get; set; } = null!;
        public Dictionary<string, string> OutsideReferences { get; set; } = null!;
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

        /// <summary>Models a relation from its owner to another <see cref="Type"/> in the <see cref="ClassDiagrammer"/> </summary>
        public class Relationship
        {
            /// <summary>A <see cref="Type.Id"/>.</summary>
            public string To { get; set; } = null!;

            /// <summary>An optional label for the relation arrow.</summary>
            public string? Label { get; set; }
        }

        /// <summary>A <see cref="Type"/>-like structure with property <see cref="Relationship"/>s.</summary>
        public abstract class Relationships
        {
            public Relationship[]? HasOne { get; set; }
            public Relationship[]? HasMany { get; set; }
        }

        /// <summary>Mermaid class diagram definitions and documentation information about a
        /// <see cref="System.Type"/> from the targeted assembly.</summary>
        public sealed class Type : Relationships
        {
            /// <summary>Uniquely identifies the <see cref="System.Type"/> in the scope of the targeted assembly
            /// as well as any HTML diagrammer rendered from it.
            /// Should match \w+ to be safe to use as select option value and
            /// part of the DOM id of the SVG node rendered for this type.
            /// May be the type name itself.</summary>
            public string Id { get; set; } = null!;

            /// <summary>The human-readable label for the type, if different from <see cref="Id"/>.
            /// Not guaranteed to be unique in the scope of the <see cref="ClassDiagrammer"/>.</summary>
            public string? Name { get; set; }

            /// <summary>Contains the definition of the type and its own (uninherited) members
            /// in mermaid class diagram syntax, see https://mermaid.js.org/syntax/classDiagram.html .</summary>
            public string DiagramDefinition { get; set; } = null!;

            /// <summary></summary>
            public Relationship? BaseType { get; set; }

            /// <summary></summary>
            public Relationship[]? Interfaces { get; set; }

            /// <summary>Contains the mermaid class diagram definitions for inherited members by their <see cref="IMember.DeclaringType"/>.
            /// for the consumer to choose which of them to display in an inheritance scenario.</summary>
            public IDictionary<string, InheritedMembers>? InheritedMembersByDeclaringType { get; set; }

            /// <summary>Contains the XML documentation comments for this type
            /// (using a <see cref="string.Empty"/> key) and its members, if available.</summary>
            public IDictionary<string, string>? XmlDocs { get; set; }

            public class InheritedMembers : Relationships
            {
                public string? FlatMembers { get; set; }
            }
        }
    }
}