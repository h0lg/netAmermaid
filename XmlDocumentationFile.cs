using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetAmermaid
{
    /// <summary>Loads XML documentation comments from a file and makes them accessible.
    /// Make sure to enable XML documentation output , see
    /// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/#create-xml-documentation-output .
    /// Inspired by https://stackoverflow.com/a/20790428 .</summary>
    public sealed class XmlDocumentationFile
    {
        private readonly Dictionary<string, string>? symbols;

        public bool HasEntries => symbols?.Keys.Any() == true;

        public XmlDocumentationFile(string xmlDocsPath, string[]? strippedNamespaces)
        {
            if (!File.Exists(xmlDocsPath)) return;

            const string linePadding = @"^[ \t]+|[ \t]+$"; // matches XML indent

            /*  matches reference tags indluding "see href", "see cref" and "paramref name"
                with the cref value being prefixed by symbol-specific letter and a colon (see GetComment usage)
                including the quotes around the attribute value and the closing slash of the tag containing the attribute */
            const string referenceAttributes = @"(see\s.ref=""(.:)?)|(paramref\sname="")|(""\s/)";

            var removableNamespaces = strippedNamespaces?.Any() == true
                // builds an OR | combined regex for replacing namespaces
                ? strippedNamespaces.Select(ns => $"({ns.Replace(".", "\\.")}\\.)").Join("|")
                : null;

            var regexes = new[] { linePadding, referenceAttributes, removableNamespaces }.Where(regex => regex != null);
            var noiseAndPadding = new Regex(regexes.Join("|"), RegexOptions.Multiline);

            symbols = XDocument.Load(xmlDocsPath).Root?.Element("members")?.Elements().ToDictionary(
                member => member.Attribute("name")?.Value ?? string.Empty,
                member =>
                {
                    var summary = member.Element("summary");
                    if (summary == null) return string.Empty;

                    var comment = summary.GetInnerXml()
                        .Replace("<para>", Environment.NewLine).Replace("</para>", Environment.NewLine).Trim() // to format
                        .Replace('<', '[').Replace('>', ']'); // to prevent ugly escaped output

                    return noiseAndPadding.Replace(comment, string.Empty).NormalizeHorizontalWhiteSpace();
                });
        }

        public XmlDocumentationFile(Assembly assembly, string[]? strippedNamespaces)
            : this(Path.ChangeExtension(assembly.Location, ".xml"), strippedNamespaces) { }

        public string? ForType(Type type) => GetComment("T:" + type.FullName);
        public string? ForProperty(PropertyInfo property) => GetComment($"P:{property.DeclaringType?.FullName}.{property.Name}");
        public string? ForField(FieldInfo field) => GetComment($"F:{field.DeclaringType?.FullName}.{field.Name}");

        public string? ForMethod(MethodInfo method)
        {
            var signature = method.ToString();
            var signatureWithoutReturnType = signature?[signature.IndexOf(method.Name)..];
            return GetComment($"M:{method.DeclaringType?.FullName}.{signatureWithoutReturnType}");
        }

        // helps getting the documentation comment for a symbol
        private string? GetComment(string symbolName)
            => symbols?.TryGetValue(symbolName, out var comment) == true ? comment : null;
    }

    internal static class XElementExtensions
    {
        // from by https://stackoverflow.com/a/1704579
        internal static string GetInnerXml(this XElement summary)
            => summary.Nodes().Aggregate(string.Empty, (b, node) => b += node.ToString());
    }
}