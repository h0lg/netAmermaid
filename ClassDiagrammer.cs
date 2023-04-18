namespace NetAmermaid
{
    public class ClassDiagrammer
    {
        public MermaidClassDiagrammer.Namespace[] Namespaces { get; set; } = null!;
        internal string[] Excluded { get; set; } = null!;
    }
}