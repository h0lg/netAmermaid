using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using Microsoft.Extensions.DependencyModel;

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

        [Option('o', "output-folder", HelpText = $"The path of the folder to generate the {diagrammer} into." +
            $" This defaults to a 'netAmermaid' folder in the directory of the '{assembly}', which will be created if required.")]
        public string? OutputFolder { get; set; }

        [Option('b', "base-types", HelpText = "A regular expression matching the names of common base types in the " + assembly
            + $". Set to make displaying repetitive and noisy inheritance details on your diagrams optional via a control in the {diagrammer}.")]
        public string? BaseTypes { get; set; }

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

            // see https://jonathancrozier.com/blog/how-to-dynamically-load-different-versions-of-an-assembly-in-the-same-dot-net-application
            // see https://learn.microsoft.com/en-us/dotnet/framework/deployment/best-practices-for-assembly-loading
            AssemblyLoadContext? loadContext = null;

            try
            {
                // Create a new context and mark it as 'collectible'.
                string tempLoadContextName = Guid.NewGuid().ToString();

                loadContext = new InMemoryContext(assemblyPath);

                loadContext.Resolving += LoadContext_Resolving;


                Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);




                var types = FilterTypes(assembly);

                #region XML docs
                var stripNamespaces = StrippedNamespaces?.ToArray();

                var xmlDocs = XmlDocs == null ? new XmlDocumentationFile(assembly, stripNamespaces)
                    : new XmlDocumentationFile(GetPath(XmlDocs), stripNamespaces);

                if (!xmlDocs.HasEntries) Console.WriteLine("No XML documentation found. Continuing without.");
                #endregion

                var diagrammer = new MermaidClassDiagrammer(xmlDocs);

                // convert collections to dictionaries for easier access in JS
                var typeDefsByNamespace = diagrammer.GetDefinitions(types).ToDictionary(ns => ns.Name ?? string.Empty,
                    ns => ns.Types.ToDictionary(t => t.Id, t => new { t.Name, t.DiagramDefinition, t.InheritedMembersByDeclaringType, t.XmlDocs }));

                var typeDefsJson = JsonSerializer.Serialize(typeDefsByNamespace, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

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
                File.WriteAllText(Path.Combine(outputFolder, "class-diagrammer.html"), html);

                // copy required resources to output folder
                foreach (var resource in new[] { "styles.css", "netAmermaid.ico" })
                    File.Copy(Path.Combine(htmlSourcePath, resource), Path.Combine(outputFolder, resource), overwrite: true);

                Console.WriteLine("Successfully generated HTML diagrammer.");
            }
            finally
            {
                // Unload the context.
                loadContext?.Unload();
            }
        }

        // from https://stackoverflow.com/a/40921746
        private Assembly? LoadContext_Resolving(AssemblyLoadContext context, AssemblyName name)
        {
            // avoid loading *.resources dlls, because of: https://github.com/dotnet/coreclr/issues/8416
            if (name.Name.EndsWith("resources"))
            {
                return null;
            }

            //loadContext.dep

            var dependencies = DependencyContext.Default.RuntimeLibraries;

            foreach (var library in dependencies)
            {
                if (IsCandidateLibrary(library, name))
                {
                    return context.LoadFromAssemblyName(new AssemblyName(library.Name));
                }
            }

            Assembly assembly1 = context.Assemblies.First();

            string? folder = Path.GetDirectoryName(assembly1.Location);
            var foundDlls = Directory.GetFileSystemEntries(folder, name.Name + ".dll", SearchOption.AllDirectories);
            if (foundDlls.Any())
            {
                return context.LoadFromAssemblyPath(foundDlls[0]);
            }

            Assembly dep = context.LoadFromAssemblyName(name);
            return dep;
        }

        // from https://stackoverflow.com/a/40921746
        private static bool IsCandidateLibrary(RuntimeLibrary library, AssemblyName assemblyName)
        {
            return (library.Name == (assemblyName.Name))
                    || (library.Dependencies.Any(d => d.Name.StartsWith(assemblyName.Name)));
        }

        public virtual Type[] FilterTypes(Assembly assembly) => assembly.GetTypes().ExceptCompilerGenerated().ToArray();

        private protected virtual string GetPath(string pathOrUri)
        {
            // convert file:// style argument, see https://stackoverflow.com/a/38245329
            if (!Uri.TryCreate(pathOrUri, UriKind.RelativeOrAbsolute, out Uri? uri))
                throw new ArgumentException("'{0}' is not a valid URI", pathOrUri);

            // support absolute paths as well as file:// URIs and interpret relative path as relative to the current directory
            return uri.IsAbsoluteUri ? uri.AbsolutePath : pathOrUri;
        }

        //from https://www.reddit.com/r/dotnet/comments/ojl3ah/any_resources_for_resolving_assembly_dependencies/
        class InMemoryContext : AssemblyLoadContext
        {
            private AssemblyDependencyResolver resolver;

            public InMemoryContext(string readerLocation) : base(readerLocation, true)
            {
                this.resolver = new AssemblyDependencyResolver(readerLocation);
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                var path = resolver.ResolveAssemblyToPath(assemblyName);

                if (!string.IsNullOrEmpty(path))
                {
                    return LoadFromAssemblyPath(path);
                }

                return null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (!string.IsNullOrEmpty(path))
                {
                    return LoadUnmanagedDllFromPath(path);
                }

                return IntPtr.Zero;
            }
        }
    }
}