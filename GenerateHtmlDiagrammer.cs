using CommandLine;

namespace NetAmermaid
{
    /// <summary>The command for creating an HTML5 diagramming app with an API optimized for binding command line parameters.
    /// To use it outside of that context, set its properties and call <see cref="Run"/>.</summary>
    public partial class GenerateHtmlDiagrammer
    {
        internal const string RepoUrl = "https://github.com/h0lg/netAmermaid";

        private const string assembly = "assembly", diagrammer = "HTML diagrammer",
            exclude = "exclude", include = "include";

        [Option('a', assembly, Required = true,
            HelpText = $"The path or file:// URI of the .NET assembly to generate a {diagrammer} for.")]
        public string Assembly { get; set; } = null!; // validated to be non-null by Option.Required

        [Option('o', "output-folder", HelpText = $"The path of the folder to generate the {diagrammer} into." +
            $" This defaults to a 'netAmermaid' folder in the directory of the '{assembly}', which will be created if required.")]
        public string? OutputFolder { get; set; }

        [Option('i', include, HelpText = "A regular expression matching Type.FullName used to whitelist types.")]
        public string? Include { get; set; }

        [Option('e', exclude, HelpText = "A regular expression matching Type.FullName used to blacklist types.")]
        public string? Exclude { get; set; }

        [Option('r', "report-excluded", HelpText = $"Outputs a report of types excluded from the {diagrammer}" +
            $" - whether by default because compiler-generated, explicitly by '--{exclude}' or implicitly by '--{include}'." +
            $" You may find this useful to develop and debug your regular expressions.")]
        public bool ReportExludedTypes { get; set; }

        /// <summary>Namespaces to strip from <see cref="XmlDocs"/>.
        /// Implemented as a list of exact replacements instead of a single, more powerful RegEx because replacement in
        /// <see cref="XmlDocumentationFormatter.GetDoco(ICSharpCode.Decompiler.TypeSystem.IEntity)"/>
        /// happens on the unstructured string where matching and replacing the namespaces of referenced types, members and method parameters
        /// using regexes would add a lot of complicated regex-heavy code for a rather unimportant feature.</summary>
        [Option('n', "strip-namespaces", HelpText = "Space-separated namespace names that are removed for brevity from XML documentation comments." +
            " Note that the order matters: e.g. replace 'System.Collections' before 'System' to remove both of them completely.")]
        public IEnumerable<string>? StrippedNamespaces { get; set; }

        [Option('d', "docs", HelpText = $"The path or file:// URI of the XML file containing the {assembly}'s documentation comments." +
            $" You only need to set this if a) you want your diagrams annotated with them and b) the file name differs from that of the '{assembly}'." +
            $" To enable XML documentation output for your '{assembly}' see https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/#create-xml-documentation-output .")]
        public string? XmlDocs { get; set; }
    }
}