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
            ClassDiagrammer model = BuildModel(assemblyPath, xmlDocs);
            GenerateOutput(assemblyPath, model);
        }

        protected virtual XmlDocumentationFormatter? CreateXmlDocsFormatter(string assemblyPath)
        {
            var xmlDocsPath = XmlDocs == null ? Path.ChangeExtension(assemblyPath, ".xml") : GetPath(XmlDocs);
            XmlDocumentationFormatter? xmlDocs = null;

            if (File.Exists(xmlDocsPath)) xmlDocs = new XmlDocumentationFormatter(
                new XmlDocumentationProvider(xmlDocsPath), StrippedNamespaces?.ToArray());
            else Console.WriteLine("No XML documentation file found. Continuing without.");

            return xmlDocs;
        }

        protected virtual ClassDiagrammer BuildModel(string assemblyPath, XmlDocumentationFormatter? xmlDocs)
            => new ClassDiagrammerFactory(xmlDocs).BuildModel(assemblyPath, Include, Exclude);

        private string SerializeModel(ClassDiagrammer diagrammer)
        {
            object jsonModel = new
            {
                diagrammer.OutsideReferences,

                /* convert collections to dictionaries for easier access in ES using
                 * for (let [key, value] of Object.entries(dictionary)) */
                TypesByNamespace = diagrammer.TypesByNamespace.ToDictionary(ns => ns.Key,
                    ns => ns.Value.ToDictionary(t => t.Id, t => t))
            };

            // wrap model including the data required for doing the template replacement in a JS build task
            if (JsonOnly) jsonModel = new
            {
                diagrammer.SourceAssemblyName,
                diagrammer.SourceAssemblyVersion,
                BuilderVersion = AssemblyInfo.Version,
                RepoUrl,
                // pre-serialize to a string so that we don't have to re-serialize it in the JS build task
                Model = Serialize(jsonModel)
            };

            return Serialize(jsonModel);
        }

        private static string Serialize(object json)
            => JsonSerializer.Serialize(json, new JsonSerializerOptions
            {
                WriteIndented = true,
                // avoid outputting null properties unnecessarily
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

        private void GenerateOutput(string assemblyPath, ClassDiagrammer model)
        {
            var htmlSourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html");
            const string mermaidJsPath = @"node_modules\mermaid\dist\mermaid.min.js";
            string modelJson = SerializeModel(model);

            var outputFolder = OutputFolder ??
                /* If no out folder is specified and export mode is JsonOnly,
                 * default to the HTML diagrammer source folder -  that's where it's most likely used.
                 * Otherwise default to a "netAmermaid" folder next to the input assembly. */
                (JsonOnly ? htmlSourcePath : Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, "netAmermaid"));

            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            if (JsonOnly)
            {
                File.WriteAllText(Path.Combine(outputFolder, "model.json"), modelJson);
                CopyResources(mermaidJsPath);
                Console.WriteLine("Successfully generated model.json for HTML diagrammer.");
            }
            else
            {
                var htmlTemplate = File.ReadAllText(Path.Combine(htmlSourcePath, "template.html"));

                var html = htmlTemplate
                    .Replace("{{SourceAssemblyName}}", model.SourceAssemblyName)
                    .Replace("{{SourceAssemblyVersion}}", model.SourceAssemblyVersion)
                    .Replace("{{BuilderVersion}}", AssemblyInfo.Version)
                    .Replace("{{RepoUrl}}", RepoUrl)
                    .Replace("{{Model}}", modelJson);

                File.WriteAllText(Path.Combine(outputFolder, "class-diagrammer.html"), html);
                CopyResources("styles.css", "netAmermaid.ico", mermaidJsPath, "script.js");
                Console.WriteLine("Successfully generated HTML diagrammer.");
            }

            if (ReportExludedTypes)
            {
                string excludedTypes = model.Excluded.Join(Environment.NewLine);
                File.WriteAllText(Path.Combine(outputFolder, "excluded types.txt"), excludedTypes);
            }

            // copy required resources to output folder while flattening paths
            void CopyResources(params string[] pathsRelativeToHtmlSource)
            {
                foreach (var path in pathsRelativeToHtmlSource)
                    File.Copy(Path.Combine(htmlSourcePath, path), Path.Combine(outputFolder, Path.GetFileName(path)), overwrite: true);
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