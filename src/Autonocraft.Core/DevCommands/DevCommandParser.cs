using System;
using System.Globalization;

namespace Autonocraft.Core.DevCommands
{
    /// <summary>
    /// Utility helpers for span-based dev command parsing.
    /// </summary>
    internal static class DevCommandParser
    {
        /// <summary>
        /// Reads the next space-delimited token from the input span.
        /// Advances <paramref name="input"/> to the remaining span.
        /// </summary>
        public static bool TryReadNextToken(ref ReadOnlySpan<char> input, out ReadOnlySpan<char> token)
        {
            input = input.TrimStart();
            if (input.IsEmpty)
            {
                token = ReadOnlySpan<char>.Empty;
                return false;
            }

            int spaceIndex = input.IndexOf(' ');
            if (spaceIndex < 0)
            {
                token = input;
                input = ReadOnlySpan<char>.Empty;
                return true;
            }

            token = input[..spaceIndex];
            input = input[(spaceIndex + 1)..];
            return true;
        }

        public static bool EqualsIgnoreCase(ReadOnlySpan<char> span, string value)
        {
            return span.Equals(value.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseFloat(ReadOnlySpan<char> span, out float value)
        {
            return float.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseInt(ReadOnlySpan<char> span, out int value)
        {
            return int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}

