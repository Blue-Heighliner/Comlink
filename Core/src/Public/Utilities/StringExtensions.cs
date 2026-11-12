namespace BlueHeighliner.Comlink;

/// <summary>Extensions for text.</summary>
public static class StringExtensions
{
    extension(string text)
    {
        /// <summary>
        /// Puts a space before each word of Pascal case (or camel case) text: <c>TwoWords</c> becomes <c>Two Words</c>. A run of capitals is one word, so <c>HTTPServer</c> becomes <c>HTTP Server</c>, and digits stay with the word they follow,
        /// so <c>Level2Access</c> becomes <c>Level2 Access</c>. Text that already has a space or underscore before a capital is not given a second one.
        /// </summary>
        /// <returns>The text with its words separated by spaces.</returns>
        public string AddSpacesToPascalCase()
        {
            StringBuilder result = new(text.Length + 4);
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (i > 0 && char.IsUpper(current) && text[i - 1] is not (' ' or '_' or '-'))
                {
                    bool afterLowerOrDigit = char.IsLower(text[i - 1]) || char.IsDigit(text[i - 1]);
                    bool endsCapitalRun = char.IsUpper(text[i - 1]) && i + 1 < text.Length && char.IsLower(text[i + 1]);
                    if (afterLowerOrDigit || endsCapitalRun)
                    {
                        result.Append(' ');
                    }
                }

                result.Append(current);
            }

            return result.ToString();
        }
    }
}
