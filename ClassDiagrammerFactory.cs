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

        private ITypeDefinition[]? selectedTypes;
        private Dictionary<IType, string>? uniqueIds;
        private Dictionary<IType, string>? labels;
        private Dictionary<string, string>? outsideReferences;

        public ClassDiagrammerFactory(XmlDocumentationFormatter? xmlDocs)
        {
            this.xmlDocs = xmlDocs;

            decompilerSettings = new DecompilerSettings(LanguageVersion.Latest)
            {
                AutomaticProperties = true // for IsHidden to return true for backing fields
            };
        }

        public CD BuildModel(string assemblyPath, string? include, string? exclude)
        {
            CSharpDecompiler decompiler = new(assemblyPath, decompilerSettings);
            IEnumerable<ITypeDefinition> allTypes = decompiler.TypeSystem.MainModule.TypeDefinitions;

            selectedTypes = FilterTypes(allTypes,
                include == null ? null : new(include, RegexOptions.Compiled),
                exclude == null ? null : new(exclude, RegexOptions.Compiled)).ToArray();

            // generate dict to read names from later
            uniqueIds = GenerateUniqueIds(selectedTypes);
            labels = new();
            outsideReferences = new();

            Dictionary<string, CD.Type[]> typesByNamespace = selectedTypes.GroupBy(t => t.Namespace).OrderBy(g => g.Key).ToDictionary(g => g.Key,
                ns => ns.OrderBy(t => t.FullName).Select(type => type.Kind == TypeKind.Enum ? BuildEnum(type) : BuildType(type)).ToArray());

            MetadataModule mainModule = decompiler.TypeSystem.MainModule;
            string[] excluded = allTypes.Except(selectedTypes).Select(t => t.ReflectionName).ToArray();

            return new CD
            {
                SourceAssemblyName = mainModule.AssemblyName,
                SourceAssemblyVersion = mainModule.AssemblyVersion.ToString(),
                TypesByNamespace = typesByNamespace,
                OutsideReferences = outsideReferences,
                Excluded = excluded
            };
        }

        /// <summary>The default strategy for pre-filtering the <paramref name="typeDefinitions"/> available in the HTML diagrammer.
        /// Applies <see cref="IsIncludedByDefault(ITypeDefinition)"/> as well as
        /// matching by <paramref name="include"/> and not by <paramref name="exclude"/>.</summary>
        /// <returns>The types to effectively include in the HTML diagrammer.</returns>
        protected virtual IEnumerable<ITypeDefinition> FilterTypes(IEnumerable<ITypeDefinition> typeDefinitions, Regex? include, Regex? exclude)
            => typeDefinitions.Where(type => IsIncludedByDefault(type)
                && (include == null || include.IsMatch(type.ReflectionName)) // applying optional whitelist filter
                && (exclude == null || !exclude.IsMatch(type.ReflectionName))); // applying optional blacklist filter

        /// <summary>The strategy for deciding whether a <paramref name="type"/> should be included
        /// in the HTML diagrammer by default. Excludes compiler-generated and their nested types.</summary>
        protected virtual bool IsIncludedByDefault(ITypeDefinition type)
            => !type.IsCompilerGeneratedOrIsInCompilerGeneratedClass();
    }
}