using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace Icy.Input.Devices
{
    /// <summary>
    /// Represents a key combination for commands handling.
    /// </summary>
    /// <param name="Key">Id of the pressed key.</param>
    /// <param name="Modifiers">All modifier keys pressed along with <paramref name="Key"/>.</param>
    public readonly record struct KeyGesture(Keys Key, ModifierKeys Modifiers = ModifierKeys.None) : ISpanParsable<KeyGesture>
    {
        /// <inheritdoc/>
        public static KeyGesture Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            Keys key;
            int lastPlus;

            // Special case when combination includes '+' as main key
            if (s.EndsWith("++"))
            {
                key = Keys.OemPlus;
                lastPlus = s.Length - 2;
            }
            else
            {
                lastPlus = s.LastIndexOf('+');
                key = ParseKey(s[(lastPlus + 1)..]);
            }

            ModifierKeys modifiers = ModifierKeys.None;

            if (lastPlus != -1)
            {
                s = s[..lastPlus];

                foreach (var token in s.Tokenize('+'))
                {
                    modifiers |= ParseModifier(token);
                }
            }

            return new(key, modifiers);
        }

        /// <inheritdoc/>
        public static KeyGesture Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        /// <inheritdoc/>
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out KeyGesture result)
        {
            result = default;
            Keys key;
            int lastPlus;

            // Special case when combination includes '+' as main key
            if (s.EndsWith("++"))
            {
                key = Keys.OemPlus;
                lastPlus = s.Length - 2;
            }
            else
            {
                lastPlus = s.LastIndexOf('+');
                if (!TryParseKey(s[(lastPlus + 1)..], out key))
                    return false;
            }

            s = s[..lastPlus];

            ModifierKeys modifiers = ModifierKeys.None;
            foreach (var token in s.Tokenize('+'))
            {
                if (!TryParseModifier(token, out var mod))
                    return false;
                modifiers |= mod;
            }

            result = new(key, modifiers);
            return true;
        }

        /// <inheritdoc/>
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out KeyGesture result) =>
            TryParse(s.AsSpan(), provider, out result);

        private static bool TryParseKey(ReadOnlySpan<char> key, out Keys result)
        {
            result = key switch
            {
                "+" => Keys.OemPlus,
                "-" => Keys.OemMinus,
                "." => Keys.OemPeriod,
                "," => Keys.OemComma,
                { } when int.TryParse(key, out int val) => Keys.D0 + val,
                { } when Enum.TryParse<Keys>(key, true, out var item) => item,
                _ => Keys.None,
            };
            return result != Keys.None;
        }

        private static Keys ParseKey(ReadOnlySpan<char> key) => key switch
        {
            "+" => Keys.OemPlus,
            "-" => Keys.OemMinus,
            "." => Keys.OemPeriod,
            "," => Keys.OemComma,
            { } when int.TryParse(key, out int val) => Keys.D0 + val,
            _ => Enum.Parse<Keys>(key, true),
        };

        private static bool TryParseModifier(ReadOnlySpan<char> modifier, out ModifierKeys value)
        {
            Guard.IsLessThan(modifier.Length, 20);
            Span<char> lower = stackalloc char[modifier.Length];
            modifier.ToLowerInvariant(lower);
            value = lower switch
            {
                "control" => ModifierKeys.Ctrl,
                "cmd" or "meta" or "⌘" => ModifierKeys.Win,
                { } when Enum.TryParse<ModifierKeys>(modifier, true, out var item) => item,
                _ => ModifierKeys.None,
            };
            return value != ModifierKeys.None;
        }

        private static ModifierKeys ParseModifier(ReadOnlySpan<char> modifier)
        {
            Guard.IsLessThan(modifier.Length, 20);
            Span<char> lower = stackalloc char[modifier.Length];
            modifier.ToLowerInvariant(lower);
            return lower switch
            {
                "control" => ModifierKeys.Ctrl,
                "cmd" or "meta" or "⌘" => ModifierKeys.Win,
                _ => Enum.Parse<ModifierKeys>(modifier, true),
            };
        }
    }
}
