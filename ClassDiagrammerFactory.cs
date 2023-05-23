﻿using System.Text.RegularExpressions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    using CD = ClassDiagrammer;

    /* See class diagram syntax
     * reference (may be outdated!) https://mermaid.js.org/syntax/classDiagram.html
     * lexical definition https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/diagrams/class/parser/classDiagram.jison */

    /// <summary>Produces mermaid class diagram syntax for a filtered list of types from a specified .Net assembly.</summary>
    public partial class ClassDiagrammerFactory
    {
        private readonly XmlDocumentationFormatter? xmlDocs;
        private readonly DecompilerSettings decompilerSettings;
        private readonly CSharpDecompiler decompiler;

        private ITypeDefinition[]? selectedTypes;
        private Dictionary<IType, string>? uniqueIds;
        private Dictionary<IType, string>? labels;
        private Dictionary<string, string>? outsideReferences;

        public ClassDiagrammerFactory(string assemblyPath, XmlDocumentationFormatter? xmlDocs)
        {
            this.xmlDocs = xmlDocs;
            decompilerSettings = new DecompilerSettings(LanguageVersion.Latest);
            decompilerSettings.AutomaticProperties = true; // for IsHidden to return true for backing fields
            decompiler = new CSharpDecompiler(assemblyPath, decompilerSettings);
        }

        public CD BuildModel(string? include, string? exclude)
        {
            IEnumerable<ITypeDefinition> allTypes = decompiler.TypeSystem.MainModule.TypeDefinitions;

            selectedTypes = FilterTypes(allTypes,
                include == null ? null : new(include, RegexOptions.Compiled),
                exclude == null ? null : new(exclude, RegexOptions.Compiled)).ToArray();

            // generate dict to read names from later
            uniqueIds = GenerateUniqueIds(selectedTypes);
            labels = new();
            outsideReferences = new();

            var namespaces = selectedTypes.GroupBy(t => t.Namespace).Select(ns => new CD.Namespace
            {
                Name = ns.Key,
                Types = ns.OrderBy(t => t.FullName).Select(type =>
                    type.Kind == TypeKind.Enum ? BuildEnum(type) : BuildType(type)).ToArray()
            }).OrderBy(ns => ns.Name).ToArray();

            string[] excluded = allTypes.Except(selectedTypes).Select(t => t.ReflectionName).ToArray();
            return new CD { Namespaces = namespaces, OutsideReferences = outsideReferences, Excluded = excluded };
        }

        protected virtual IEnumerable<ITypeDefinition> FilterTypes(IEnumerable<ITypeDefinition> typeDefinitions, Regex? include, Regex? exclude)
            => typeDefinitions.Where(type => !type.IsCompilerGeneratedOrIsInCompilerGeneratedClass() // exlude compiler-generated and their nested types
                && (include == null || include.IsMatch(type.ReflectionName)) // applying optional whitelist filter
                && (exclude == null || !exclude.IsMatch(type.ReflectionName))); // applying optional blacklist filter

        internal string GetSourceAssemblyVersion()
            => decompiler.TypeSystem.MainModule.PEFile.Metadata.GetAssemblyDefinition().Version.ToString();
    }
}