namespace NetAmermaid
{
    internal static class StringExtensions
    {
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

        /// <summary>
        /// Determines whether the string contains another string while ignoring the case.
        /// </summary>
        /// <param name="self">The string that contains.</param>
        /// <param name="other">The string that is contained.</param>
        /// <returns>
        ///   <c>true</c> if the string contains the other string; otherwise, <c>false</c>.
        /// </returns>
        public static bool ContainsIgnoreCase(this string[] collection, string other)
        {
            if (collection == null) return false;

            foreach (var item in collection)
                if (item.Equals(other, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}