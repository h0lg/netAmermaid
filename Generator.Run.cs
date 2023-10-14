using System.Text.Json;
using System.Text.Json.Serialization;
using ICSharpCode.Decompiler.Documentation;

namespace NetAmermaid
{
    partial class GenerateHtmlDiagrammer
    {
        public void Run()
        {
            var assemblyPath = GetPath(Assembly);
            XmlDocumentationFormatter? xmlDocs = CreateXmlDocsFormatter(assemblyPath);
            ClassDiagrammerFactory factory = new(xmlDocs);
            ClassDiagrammer model = factory.BuildModel(assemblyPath, Include, Exclude);
            GenerateOutput(assemblyPath, model);
        }

        private XmlDocumentationFormatter? CreateXmlDocsFormatter(string assemblyPath)
        {
            var xmlDocsPath = XmlDocs == null ? Path.ChangeExtension(assemblyPath, ".xml") : GetPath(XmlDocs);
            XmlDocumentationFormatter? xmlDocs = null;

            if (File.Exists(xmlDocsPath)) xmlDocs = new XmlDocumentationFormatter(
                new XmlDocumentationProvider(xmlDocsPath), StrippedNamespaces?.ToArray());
            else Console.WriteLine("No XML documentation file found. Continuing without.");

            return xmlDocs;
        }

        private static string SerializeModel(ClassDiagrammer diagrammer)
        {
            var jsonModel = new
            {
                diagrammer.OutsideReferences,

                /* convert collections to dictionaries for easier access in ES using
                 * for (let [key, value] of Object.entries(dictionary)) */
                TypesByNamespace = diagrammer.TypesByNamespace.ToDictionary(ns => ns.Key,
                    ns => ns.Value.ToDictionary(t => t.Id, t => t))
            };

            return JsonSerializer.Serialize(jsonModel, new JsonSerializerOptions
            {
                WriteIndented = true,
                // avoid outputting null properties unnecessarily
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        private void GenerateOutput(string assemblyPath, ClassDiagrammer model)
        {
            var htmlSourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html");
            string modelJson = SerializeModel(model);
            var htmlTemplate = File.ReadAllText(Path.Combine(htmlSourcePath, "template.html"));
            var script = File.ReadAllText(Path.Combine(htmlSourcePath, "script.js"));

            var html = htmlTemplate
                .Replace("{{sourceAssemblyName}}", model.SourceAssemblyName)
                .Replace("{{sourceAssemblyVersion}}", model.SourceAssemblyVersion)
                .Replace("{{builderVersion}}", AssemblyInfo.Version)
                .Replace("{{repoUrl}}", RepoUrl)
                .Replace("{{model}}", modelJson)
                .Replace("{{script}}", script);

            var outputFolder = OutputFolder ?? Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, "netAmermaid");

            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
            File.WriteAllText(Path.Combine(outputFolder, "class-diagrammer.html"), html);

            // copy required resources to output folder
            foreach (var resource in new[] { "styles.css", "netAmermaid.ico" })
                File.Copy(Path.Combine(htmlSourcePath, resource), Path.Combine(outputFolder, resource), overwrite: true);

            Console.WriteLine("Successfully generated HTML diagrammer.");

            if (ReportExludedTypes)
            {
                string excludedTypes = model.Excluded.Join(Environment.NewLine);
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