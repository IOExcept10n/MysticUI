using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a set of control styles.
    /// </summary>
    public class Stylesheet : IReadOnlyDictionary<string, ControlTemplate>, IEnumerable<ControlTemplate>
    {
        private readonly Dictionary<string, ControlTemplate> styles = new();

        /// <inheritdoc/>
        public ControlTemplate this[string key] => styles[key];

        /// <summary>
        /// Default stylesheet instance for the UI system.
        /// </summary>
        public static Stylesheet Default
        {
            get => EnvironmentSettingsProvider.EnvironmentSettings.DefaultAssets.DefaultStylesheet ??= new();
        }

        /// <inheritdoc/>
        public IEnumerable<string> Keys => styles.Keys;

        /// <inheritdoc/>
        public IEnumerable<ControlTemplate> Values => styles.Values;

        /// <inheritdoc/>
        public int Count => styles.Count;

        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            return styles.ContainsKey(key);
        }

        /// <inheritdoc/>
        public IEnumerator<ControlTemplate> GetEnumerator()
        {
            return styles.Values.Cast<ControlTemplate>().GetEnumerator();
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out ControlTemplate value)
        {
            return styles.TryGetValue(key, out value);
        }

        /// <summary>
        /// Removes and releases all styles from the stylesheet.
        /// </summary>
        public void Unload()
        {
            foreach (var style in Values)
            {
                style.Dispose();
            }
        }

        /// <summary>
        /// Adds a style to the stylesheet.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="style"></param>
        public void AddStyle(string name, ControlTemplate style)
        {
            styles.Add(name, style);
        }

        /// <summary>
        /// Loads a stylesheet from the XML
        /// </summary>
        /// <param name="element">Element with the stylesheet definition.</param>
        /// <exception cref="FormatException"></exception>
        public void LoadXml(XElement element)
        {
            foreach (var style in element.Elements())
            {
                styles.Add(style.Attribute(LayoutSerializer.KeyAttribute)?.Value ?? throw new FormatException("Styles in stylesheet require keys to identify."),
                        LayoutSerializer.Default.LoadLayout<ControlTemplate>(style));
            }
            foreach (var style in styles)
            {
                style.Value.Setup(this);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return styles.Values.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, ControlTemplate>> IEnumerable<KeyValuePair<string, ControlTemplate>>.GetEnumerator()
        {
            return styles.GetEnumerator();
        }
    }
}