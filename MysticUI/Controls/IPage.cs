using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// An interface for the navigable UI page.
    /// </summary>
    public interface IPage : IDisposable
    {
        /// <summary>
        /// A service that provides navigation for the UI system.
        /// </summary>
        public INavigationService NavigationService { get; }

        /// <summary>
        /// Determines whether the page should be kept in the navigation route to open when it's available without reloading.
        /// </summary>
        public bool KeepAlive { get; set; }

        /// <summary>
        /// Path to the page.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Renders the page content into the provided render context.
        /// </summary>
        /// <param name="context">Context to render the page.</param>
        public void Render(RenderContext context);

        /// <summary>
        /// Initializes the <see cref="IPage"/> instance.
        /// </summary>
        public void Initialize();

        /// <summary>
        /// Prepares the page for processing after loading or reopening.
        /// </summary>
        public void Prepare();

        /// <summary>
        /// Called when the navigation service navigates to the page.
        /// </summary>
        /// <param name="sender">Event sender instance (by default, the <see cref="NavigationService"/>).</param>
        /// <param name="args">Arguments for the navigation.</param>
        public void OnNavigatedTo(object sender, NavigationEventArgs args);

        /// <summary>
        /// Called when the navigation service navigates from the page.
        /// </summary>
        /// <param name="sender">Event sender instance (by default, the <see cref="NavigationService"/>).</param>
        /// <param name="args">Arguments for the navigation.</param>
        public void OnNavigatedFrom(object sender, NavigationEventArgs args);

        /// <summary>
        /// Opens the page into the provided <see cref="Desktop"/> instance.
        /// </summary>
        /// <param name="target">Instance of the desktop to open in.</param>
        public void Open(Desktop target);

        /// <summary>
        /// Closes the page and removes its content from the desktop.
        /// </summary>
        public void Close();
    }

    /// <summary>
    /// Represents arguments for the navigation events.
    /// </summary>
    public class NavigationEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Page from which navigation was performed.
        /// </summary>
        public IPage? PreviousPage { get; }

        /// <summary>
        /// Page to which navigation was performed.
        /// </summary>
        public IPage? NextPage { get; }

        /// <summary>
        /// Creates a new instance of the <see cref="NavigationEventArgs"/> class.
        /// </summary>
        public NavigationEventArgs()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="NavigationEventArgs"/> class.
        /// </summary>
        /// <param name="previousPage">Page before navigation.</param>
        /// <param name="nextPage">Page after navigation.</param>
        public NavigationEventArgs(IPage? previousPage, IPage? nextPage)
        {
            PreviousPage = previousPage;
            NextPage = nextPage;
        }
    }
}