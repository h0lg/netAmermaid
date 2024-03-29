﻿using System.Text.RegularExpressions;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.TypeSystem;

namespace NetAmermaid
{
    /// <summary>Wraps the <see cref="IDocumentationProvider"/> to prettify XML documentation comments.
    /// Make sure to enable XML documentation output, see
    /// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/#create-xml-documentation-output .</summary>
    public class XmlDocumentationFormatter
    {
        /// <summary>Matches XML indent.</summary>
        protected const string linePadding = @"^[ \t]+|[ \t]+$";

        /// <summary>Matches reference tags indluding "see href", "see cref" and "paramref name"
        /// with the cref value being prefixed by symbol-specific letter and a colon
        /// including the quotes around the attribute value and the closing slash of the tag containing the attribute.</summary>
        protected const string referenceAttributes = @"(see\s.ref=""(.:)?)|(paramref\sname="")|(""\s/)";

        private readonly IDocumentationProvider docs;
        private readonly Regex noiseAndPadding;

        public XmlDocumentationFormatter(IDocumentationProvider docs, string[]? strippedNamespaces)
        {
            this.docs = docs;
            List<string> regexes = new() { linePadding, referenceAttributes };

            if (strippedNamespaces?.Any() == true)
                regexes.AddRange(strippedNamespaces.Select(ns => $"({ns.Replace(".", "\\.")}\\.)"));

            noiseAndPadding = new Regex(regexes.Join("|"), RegexOptions.Multiline); // builds an OR | combined regex
        }

        internal Dictionary<string, string>? GetXmlDocs(ITypeDefinition type, params IMember[][] memberCollections)
        {
            Dictionary<string, string>? docs = new();
            AddXmlDocEntry(docs, type);

            foreach (IMember[] members in memberCollections)
                foreach (IMember member in members)
                    AddXmlDocEntry(docs, member);

            return docs?.Keys.Any() == true ? docs : default;
        }

        protected virtual string? GetDoco(IEntity entity)
        {
            string? comment = docs.GetDocumentation(entity)?
                .ReplaceAll(new[] { "<summary>", "</summary>" }, null)
                .ReplaceAll(new[] { "<para>", "</para>" }, ClassDiagrammer.NewLine).Trim() // to format
                .Replace('<', '[').Replace('>', ']'); // to prevent ugly escaped output

            return comment == null ? null : noiseAndPadding.Replace(comment, string.Empty).NormalizeHorizontalWhiteSpace();
        }

        private void AddXmlDocEntry(Dictionary<string, string> docs, IEntity entity)
        {
            string? doc = GetDoco(entity);
            if (string.IsNullOrEmpty(doc)) return;
            string key = entity is IMember member ? member.Name : string.Empty;
            docs[key] = doc;
        }
    }
}