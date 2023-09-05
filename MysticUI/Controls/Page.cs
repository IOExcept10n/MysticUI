using CommunityToolkit.Diagnostics;
using MysticUI.Extensions;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a base class for pages that can be used to simplify UI navigation process.
    /// </summary>
    public class Page : UIElement, IPage
    {
        private Control root = new();
        private Desktop? desktop;
        private bool disposedValue;

        /// <summary>
        /// Gets a value indicating if the page is already initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets or sets a name of the type of the created page. This is needed for serialization, don't use it manually.
        /// </summary>
        [InstanceType]
        public string? LinkedType { get; set; }

        /// <summary>
        /// Gets or sets the root control of the page. This will be used as a desktop root when the page is selected.
        /// </summary>
        [Content]
        [Browsable(false)]
        public Control Root
        {
            get => root;
            set
            {
                if (value == root) return;
                root = value;
                if (root != null && IsInitialized) OnRootReset();
            }
        }

        /// <summary>
        /// Gets or sets the desktop of the page.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Desktop? Desktop
        {
            get => desktop;
            private set
            {
                if (desktop == value) return;
                desktop = value;
                OnRootReset();
            }
        }

        /// <inheritdoc/>
        public bool KeepAlive { get; set; }

        /// <inheritdoc/>
        public INavigationService NavigationService { get; set; } = null!;

        /// <inheritdoc/>
        public string Path { get; set; } = null!;

        /// <summary>
        /// Loads the page instance from the XML document.
        /// </summary>
        /// <param name="document">Document to load</param>
        /// <returns>An instance of the created page.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IPage Load(XDocument document)
        {
            Guard.IsNotNull(document);
            Guard.IsNotNull(document.Root);
            return LayoutSerializer.Default.LoadLayout<IPage>(document.Root);
        }

        /// <inheritdoc/>
        public virtual void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public virtual void OnNavigatedFrom(object sender, NavigationEventArgs args)
        {
        }

        /// <inheritdoc/>
        public virtual void OnNavigatedTo(object sender, NavigationEventArgs args)
        {
        }

        /// <inheritdoc/>
        public virtual void Prepare()
        {
        }

        /// <inheritdoc/>
        public virtual void Render(RenderContext context)
        {
            Root.Render(context);
        }

        /// <inheritdoc/>
        public void Open(Desktop target)
        {
            target.Root = Root;
            Desktop = target;
            OnRootReset();
        }

        /// <inheritdoc/>
        public void Close()
        {
            if (Desktop != null && Desktop.Root == Root)
            {
                Desktop.Dispatcher.Dispatch(() =>
                {
                    Root.Detach();
                    Desktop = null;
                });
            }
        }

        private void OnRootReset()
        {
            Root.Desktop = Desktop;
            Root.ResetDataContext();
            if (Desktop == null && !KeepAlive) Root.OnDetach();
        }

        /// <summary>
        /// Releases all page resources.
        /// </summary>
        /// <param name="disposing">Indicates whether the page is disposed manually.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Root?.Detach();
                }
                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}