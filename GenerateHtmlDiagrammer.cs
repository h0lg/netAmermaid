using System.Net;
using System.Reflection;
using System.Text.Json;
using CommandLine;

namespace NetAmermaid
{
    /// <summary>The command for creating an HTML5 diagramming app with an API optimized for binding command line parameters.
    /// To use it outside of that context, set its properties and call <see cref="Run"/>.</summary>
    public class GenerateHtmlDiagrammer
    {
        internal const string RepoUrl = "https://github.com/h0lg/netAmermaid";

        private const string assembly = "assembly", diagrammer = "HTML diagrammer";

        [Option('a', assembly, Required = true,
            HelpText = $"The path or file:// URI of the .NET assembly to generate a {diagrammer} for.")]
        public string Assembly { get; set; } = null!; // validated to be non-null by Option.Required

        [Option('o', "output-folder", HelpText = $"The path of the folder to generate the {diagrammer} into.")]
        public string? OutputFolder { get; set; }

        [Option('b', "base-types", HelpText = "A regular expression matching the names of common base types in the " + assembly
            + $". Set to make displaying repetitive and noisy inheritance details on your diagrams optional via a control in the {diagrammer}.")]
        public string? BaseTypes { get; set; }

        public void Run()
        {
            var assemblyPath = GetPath(Assembly);
            var outputFolder = OutputFolder ?? Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, "netAmermaid");
            var types = FilterTypes(System.Reflection.Assembly.LoadFrom(assemblyPath));
            var diagrammer = new MermaidClassDiagrammer();

            // convert collections to dictionaries for easier access in JS
            var typeDefsByNamespace = diagrammer.GetDefinitions(types).ToDictionary(ns => ns.Name ?? string.Empty,
                ns => ns.Types.ToDictionary(t => t.Name, t => new { t.DiagramDefinition, t.InheritedMembersByDeclaringType }));

            var typeDefsJson = JsonSerializer.Serialize(typeDefsByNamespace, new JsonSerializerOptions { WriteIndented = true });
            var htmlSourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html");
            var htmlTemplate = File.ReadAllText(Path.Combine(htmlSourcePath, "template.html"));
            var script = File.ReadAllText(Path.Combine(htmlSourcePath, "script.js"));

            var html = htmlTemplate
                .Replace("{{assembly}}", Path.GetFileNameWithoutExtension(assemblyPath))
                .Replace("{{repoUrl}}", RepoUrl)
                .Replace("{{baseTypeRegex}}", WebUtility.HtmlEncode(BaseTypes))
                .Replace("{{typeDefinitionsByNamespace}}", typeDefsJson)
                .Replace("{{script}}", script);

            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
            File.WriteAllText(Path.Combine(outputFolder, "class-diagram-generator.html"), html);

            // copy required resources to output folder
            foreach (var resource in new[] { "styles.css", "netAmermaid.ico" })
                File.Copy(Path.Combine(htmlSourcePath, resource), Path.Combine(outputFolder, resource), overwrite: true);

            Console.WriteLine("Successfully generated HTML diagrammer.");
        }

        public virtual Type[] FilterTypes(Assembly assembly) => assembly.GetTypes().ExceptCompilerGenerated().ToArray();

        public virtual string GetPath(string pathOrUri)
        {
            // convert file:// style argument, see https://stackoverflow.com/a/38245329
            if (!Uri.TryCreate(pathOrUri, UriKind.RelativeOrAbsolute, out var uri))
                throw new ArgumentException("'{0}' is not a valid URI", pathOrUri);

            // support absolute paths as well as file:// URIs and interpret relative path as relative to the current directory
            return uri.IsAbsoluteUri ? uri.AbsolutePath : pathOrUri;
        }
    }
}