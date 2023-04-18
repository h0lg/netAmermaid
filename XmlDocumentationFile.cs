using System.Diagnostics;
using System.Text.RegularExpressions;
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

        protected const string referencedTypeNamespaces = @"(?<=see cref=""T:)(.+\.)(?=\w+"" /)";
        protected const string referencedMemberNamespaces = @"(?<=see cref=""[PFM]:)(.+\.)(?=\w+\.[\w`]+(?>\(.*\))?"" /)";
        protected const string referencedMethodParameterLists = @"(?<=see cref=""M:[\w\.`]+\()(.+)(?=\))";

        private readonly IDocumentationProvider docs;
        private readonly Regex? strippedNamespaces;
        private readonly Regex noiseAndPadding, referenceNamespaces;

        public XmlDocumentationFormatter(IDocumentationProvider docs, string? strippedNamespaces)
        {
            this.docs = docs;
            this.strippedNamespaces = strippedNamespaces == null ? null : new Regex(strippedNamespaces, RegexOptions.Multiline | RegexOptions.Compiled);
            List<string> regexes = new() { linePadding, referenceAttributes };

            // build OR | combined regexes
            noiseAndPadding = new Regex(regexes.Join("|"), RegexOptions.Multiline | RegexOptions.Compiled);
            referenceNamespaces = new Regex(new[] { referencedTypeNamespaces, referencedMemberNamespaces }.Join("|"), RegexOptions.Multiline | RegexOptions.Compiled);
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
                .ReplaceAll(new[] { "<para>", "</para>" }, Environment.NewLine).Trim() // to format
                .Replace('<', '[').Replace('>', ']'); // to prevent ugly escaped output

            if (comment == null) return null;
            if (Regex.IsMatch(comment, "[PFMT]:")) Debugger.Break();

            if (strippedNamespaces != null) comment = referenceNamespaces.Replace(comment, (Match match)
                => strippedNamespaces.Replace(match.Value, string.Empty).TrimStart('.'));

            //TODO match and replace namespaces in referencedMethodParameterLists

            comment = noiseAndPadding.Replace(comment, string.Empty).NormalizeHorizontalWhiteSpace();
            return comment;
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