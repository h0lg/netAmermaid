using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using ICSharpCode.Decompiler.Documentation;

namespace NetAmermaid
{
    /// <summary>The command for creating an HTML5 diagramming app with an API optimized for binding command line parameters.
    /// To use it outside of that context, set its properties and call <see cref="Run"/>.</summary>
    public class GenerateHtmlDiagrammer
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

        public void Run()
        {
            var assemblyPath = GetPath(Assembly);
            var outputFolder = OutputFolder ?? Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, "netAmermaid");

            var xmlDocsPath = XmlDocs == null ? Path.ChangeExtension(assemblyPath, ".xml") : GetPath(XmlDocs);
            XmlDocumentationFormatter? xmlDocs = null;

            if (File.Exists(xmlDocsPath)) xmlDocs = new XmlDocumentationFormatter(
                new XmlDocumentationProvider(xmlDocsPath), StrippedNamespaces?.ToArray());
            else Console.WriteLine("No XML documentation file found. Continuing without.");

            ClassDiagrammerFactory factory = new(assemblyPath, xmlDocs);
            var diagrammer = factory.BuildModel(Include, Exclude);

            // convert collections to dictionaries for easier access in JS
            var typeDefsByNamespace = diagrammer.Namespaces.ToDictionary(ns => ns.Name ?? string.Empty,
                ns => ns.Types.ToDictionary(t => t.Id, t => new
                {
                    t.Name,
                    t.DiagramDefinition,
                    t.BaseType,
                    t.Interfaces,
                    t.InheritedMembersByDeclaringType,
                    t.XmlDocs
                }));

            var typeDefsJson = JsonSerializer.Serialize(typeDefsByNamespace, new JsonSerializerOptions
            {
                WriteIndented = true,
                // avoid outputting null properties unnecessarily
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var htmlSourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html");
            var htmlTemplate = File.ReadAllText(Path.Combine(htmlSourcePath, "template.html"));
            var script = File.ReadAllText(Path.Combine(htmlSourcePath, "script.js"));

            var html = htmlTemplate
                .Replace("{{assembly}}", Path.GetFileNameWithoutExtension(assemblyPath))
                .Replace("{{repoUrl}}", RepoUrl)
                .Replace("{{typeDefinitionsByNamespace}}", typeDefsJson)
                .Replace("{{script}}", script);

            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
            File.WriteAllText(Path.Combine(outputFolder, "class-diagrammer.html"), html);

            // copy required resources to output folder
            foreach (var resource in new[] { "styles.css", "netAmermaid.ico" })
                File.Copy(Path.Combine(htmlSourcePath, resource), Path.Combine(outputFolder, resource), overwrite: true);

            Console.WriteLine("Successfully generated HTML diagrammer.");

            if (ReportExludedTypes)
            {
                string excludedTypes = diagrammer.Excluded.Join(Environment.NewLine);
                File.WriteAllText(Path.Combine(outputFolder, "excluded types.txt"), excludedTypes);
            }
        }

        private protected virtual string GetPath(string pathOrUri)
        {
            // convert file:// style argument, see https://stackoverflow.com/a/38245329
            if (!Uri.TryCreate(pathOrUri, UriKind.RelativeOrAbsolute, out Uri? uri))
                throw new ArgumentException("'{0}' is not a valid URI", pathOrUri);

            // support absolute paths as well as file:// URIs and interpret relative path as relative to the current directory
            return uri.IsAbsoluteUri ? uri.AbsolutePath : pathOrUri;
        }
    }
}