using FontStashSharp;
using MysticUI.Controls;
using MysticUI.Extensions.Content;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace MysticUI
{
    /// <summary>
    /// Represents a class for the collection of cached fonts which are ready for the draw.
    /// </summary>
    public class FontCollection : IReadOnlyDictionary<string, SpriteFontBase?>
    {
        private readonly FontLoader fontLoader = LayoutSerializer.Default.AvailableSerializers.OfType<FontLoader>().FirstOrDefault() ?? new();
        private readonly Dictionary<string, SpriteFontBase> fonts = new();

        /// <summary>
        /// Gets or sets the font that will be returned if the requested font can not be loaded.
        /// </summary>
        public SpriteFontBase? FallbackFont { get; set; }

        /// <inheritdoc/>
        public SpriteFontBase? this[string name]
        {
            get
            {
                try
                {
                    string key = $"{name},{Control.DefaultFontSize}";
                    if (fonts.TryGetValue(key, out SpriteFontBase? spriteFont) || fonts.TryGetValue(name, out spriteFont)) return spriteFont;
                    SpriteFontBase font = (SpriteFontBase)fontLoader.Parse(typeof(SpriteFontBase), key);
                    if (font is StaticSpriteFont)
                    {
                        fonts.Add(name, font);
                    }
                    else
                    {
                        fonts.Add(key, font);
                    }
                    return font;
                }
                catch
                {
                    return FallbackFont;
                }
            }
        }

        /// <summary>
        /// Gets a font with given name and size.
        /// </summary>
        /// <param name="name">Name or path to the font.</param>
        /// <param name="size">Size of the font.</param>
        /// <returns></returns>
        public SpriteFontBase? this[string name, int size]
        {
            get
            {
                try
                {
                    string key = $"{name},{size}";
                    if (fonts.TryGetValue(key, out SpriteFontBase? spriteFont) || fonts.TryGetValue(name, out spriteFont)) return spriteFont;
                    SpriteFontBase font = (SpriteFontBase)fontLoader.Parse(typeof(SpriteFontBase), key);
                    if (font is StaticSpriteFont)
                    {
                        fonts.Add(name, font);
                    }
                    else
                    {
                        fonts.Add(key, font);
                    }
                    return font;
                }
                catch
                {
                    return FallbackFont;
                }
            }
        }

        /// <summary>
        /// Binds a font by provided name to a collection.
        /// </summary>
        /// <param name="name">Name to find the font by.</param>
        /// <param name="font">Font to save into a collection.</param>
        public void BindFont(string name, SpriteFontBase font)
        {
            fonts.Add(name, font);
        }

        /// <summary>
        /// Loads a font from the given key.
        /// </summary>
        /// <param name="name">Path to a font file or name of the font family.</param>
        /// <param name="fontSize">Size of the loaded font instance.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="FileNotFoundException"/>
        public SpriteFontBase LoadFont(string name, int fontSize = Control.DefaultFontSize)
        {
            string key = $"{name},{fontSize}";
            if (fonts.TryGetValue(key, out SpriteFontBase? spriteFont) || fonts.TryGetValue(name, out spriteFont)) return spriteFont;
            return (SpriteFontBase)fontLoader.Parse(typeof(SpriteFontBase), key);
        }

        /// <inheritdoc/>
        public IEnumerable<string> Keys => fonts.Keys;

        /// <inheritdoc/>
        public IEnumerable<SpriteFontBase> Values => fonts.Values;

        /// <inheritdoc/>
        public int Count => fonts.Count;

        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            return fonts.ContainsKey(key);
        }

        /// <summary>
        /// Clears all loaded fonts.
        /// </summary>
        public void Clear()
        {
            fonts.Clear();
        }

        /// <inheritdoc/>
#pragma warning disable CS8613

        public IEnumerator<KeyValuePair<string, SpriteFontBase>> GetEnumerator()
#pragma warning restore CS8613
        {
            return fonts.GetEnumerator();
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out SpriteFontBase value)
        {
            return fonts.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)fonts).GetEnumerator();
        }
    }
}