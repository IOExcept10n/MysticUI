using System.ComponentModel;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the separator for the menu.
    /// </summary>
    public class MenuSeparator : IMenuItem
    {
        internal Thumb Separator = null!;

        /// <inheritdoc/>
        [DefaultValue(null)]
        [Browsable(false)]
        [XmlIgnore]
        public string? Name { get; set; }

        /// <inheritdoc/>
        [Browsable(false)]
        [XmlIgnore]
        public Menu Menu { get; set; } = null!;

        /// <inheritdoc/>
        [Browsable(false)]
        [XmlIgnore]
        public char UnderscoreChar => default;

        /// <inheritdoc/>
        [Browsable(false)]
        [XmlIgnore]
        public int Index { get; set; }

        /// <inheritdoc/>
        public event EventHandler NameChanged = delegate { };
    }
}