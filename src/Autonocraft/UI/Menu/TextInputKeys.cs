using Microsoft.Xna.Framework.Input;

namespace Autonocraft.UI.Menu
{
    public enum TextInputCharacterSet
    {
        Name,
        Technical,
        Digits,
        Chat,
        Console
    }

    public static class TextInputKeys
    {
        public static bool AppendPressedCharacters(
            KeyboardState keyboard,
            KeyboardState previousKeyboard,
            ref string target,
            int maxLength,
            TextInputCharacterSet characterSet)
        {
            bool changed = false;
            bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            foreach (Keys key in keyboard.GetPressedKeys())
            {
                if (previousKeyboard.IsKeyDown(key))
                {
                    continue;
                }

                char? character = ToChar(key, shift, characterSet);
                if (!character.HasValue || target.Length >= maxLength)
                {
                    continue;
                }

                target += character.Value;
                changed = true;
            }

            return changed;
        }

        public static char? ToChar(Keys key, bool shift, TextInputCharacterSet characterSet)
        {
            if (characterSet == TextInputCharacterSet.Digits)
            {
                return DigitChar(key);
            }

            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpperInvariant(c) : c;
            }

            if (characterSet == TextInputCharacterSet.Console)
            {
                char? consoleDigit = ConsoleDigitChar(key, shift);
                if (consoleDigit.HasValue)
                {
                    return consoleDigit.Value;
                }
            }

            char? digit = DigitChar(key);
            if (digit.HasValue)
            {
                return digit.Value;
            }

            return characterSet switch
            {
                TextInputCharacterSet.Name => NamePunctuation(key, shift),
                TextInputCharacterSet.Technical => TechnicalPunctuation(key, shift),
                TextInputCharacterSet.Chat => ChatPunctuation(key, shift),
                TextInputCharacterSet.Console => ConsolePunctuation(key, shift),
                _ => null
            };
        }

        private static char? ConsoleDigitChar(Keys key, bool shift)
        {
            if (!shift)
            {
                return DigitChar(key);
            }

            return key switch
            {
                Keys.D1 => '!',
                Keys.D2 => '@',
                Keys.D3 => '#',
                Keys.D4 => '$',
                Keys.D5 => '%',
                Keys.D6 => '^',
                Keys.D7 => '&',
                Keys.D8 => '*',
                Keys.D9 => '(',
                Keys.D0 => ')',
                _ => null
            };
        }

        private static char? DigitChar(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            return null;
        }

        private static char? NamePunctuation(Keys key, bool shift) => key switch
        {
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPeriod => '.',
            Keys.Space => ' ',
            _ => null
        };

        private static char? TechnicalPunctuation(Keys key, bool shift) => key switch
        {
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPeriod => '.',
            Keys.OemQuestion => shift ? '?' : '/',
            Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemPlus => shift ? '+' : '=',
            Keys.Space => ' ',
            _ => null
        };

        private static char? ChatPunctuation(Keys key, bool shift) => key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod => '.',
            Keys.OemComma => ',',
            Keys.OemQuestion => '?',
            _ => null
        };

        private static char? ConsolePunctuation(Keys key, bool shift) => key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod => shift ? '>' : '.',
            Keys.OemComma => shift ? '<' : ',',
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPlus => shift ? '+' : '=',
            Keys.OemQuestion => shift ? '?' : '/',
            Keys.OemSemicolon => shift ? ':' : ';',
            _ => null
        };
    }
}
