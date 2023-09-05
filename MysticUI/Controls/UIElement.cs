using Newtonsoft.Json;
using System.ComponentModel;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Base class for all UI elements.
    /// </summary>
    public class UIElement : INameIdentifiable
    {
        private string? name;

        /// <summary>
        /// An instance of the element data context. Needed to access context without raising any events.
        /// </summary>
        protected object? dataContext;

        /// <inheritdoc/>
        [Category("Integration")]
        public string? Name
        {
            get => name;
            set
            {
                if (name == value) return;
                name = value;
                OnNameChanged();
            }
        }

        /// <summary>
        /// All custom attributes and resources for the element.
        /// </summary>
        /// <remarks>
        /// Here will be kept all attributes which are not listed in element properties and resources to work with.
        /// </remarks>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Provides a data source for control so all bindings will set to this context. If there are no context sources found,
        /// </summary>
        [Category("Integration")]
        public object? DataContext
        {
            get => dataContext;
            set
            {
                dataContext = value;
                OnDataContextChanged();
            }
        }

        /// <inheritdoc/>
        public event EventHandler NameChanged = delegate { };

        /// <summary>
        /// Occurs when element data context changes.
        /// </summary>
        public event EventHandler DataContextChanged = delegate { };

        /// <summary>
        /// Raises the <see cref="NameChanged"/> event.
        /// </summary>
        protected internal virtual void OnNameChanged()
        {
            NameChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="DataContextChanged"/> event.
        /// </summary>
        protected internal virtual void OnDataContextChanged()
        {
            DataContextChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles UI element attaching to the desktop as root.
        /// </summary>
        /// <param name="desktop">Desktop that attaches this <see cref="UIElement"/> instance.</param>
        protected internal virtual void OnDesktopRootAttach(Desktop desktop)
        {
        }

        /// <summary>
        /// Handles when the element is detached from the desktop.
        /// </summary>
        /// <remarks>
        /// This method is needed to free resources bent to this element. Note that it can be called multiple times when detaching.
        /// </remarks>
        protected internal virtual void OnDetach()
        {
        }
    }
}