namespace NetAmermaid
{
    internal static class StringExtensions
    {
        /// <summary>Replaces all consecutive horizontal white space characters in
        /// <paramref name="input"/> with <paramref name="normalizeTo"/> while leaving line breaks intact.</summary>
        internal static string NormalizeHorizontalWhiteSpace(this string input, string normalizeTo = " ")
            => System.Text.RegularExpressions.Regex.Replace(input, @"[ \t]+", normalizeTo);

        /// <summary>Replaces all occurances of <paramref name="oldValues"/> in
        /// <paramref name="input"/> with <paramref name="newValue"/>.</summary>
        internal static string ReplaceAll(this string input, IEnumerable<string> oldValues, string newValue)
            => oldValues.Aggregate(input, (aggregate, oldValue) => aggregate.Replace(oldValue, newValue));

        /// <summary>Joins the specified <paramref name="strings"/> to a single one
        /// using the specified <paramref name="separator"/> as a delimiter.</summary>
        /// <param name="pad">Whether to pad the start and end of the string with the <paramref name="separator"/> as well.</param>
        internal static string Join(this IEnumerable<string?>? strings, string separator, bool pad = false)
        {
            if (strings == null) return string.Empty;
            var joined = string.Join(separator, strings);
            return pad ? string.Concat(separator, joined, separator) : joined;
        }

        /// <summary>Formats all items in <paramref name="collection"/> using the supplied <paramref name="format"/> strategy
        /// and returns a string collection - even if the incoming <paramref name="collection"/> is null.</summary>
        internal static IEnumerable<string> FormatAll<T>(this IEnumerable<T>? collection, Func<T, string> format)
            => collection?.Select(format) ?? Enumerable.Empty<string>();

        /// <summary>Determines whether the <paramref name="collection"/> contains the
        /// <paramref name="value"/> while ignoring the case during comparison.</summary>
        public static bool ContainsIgnoreCase(this string[] collection, string value)
        {
            foreach (var item in collection)
                if (value.Equals(item, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}