﻿namespace MysticUI.Controls
{
    /// <summary>
    /// An interface for the navigation service for the UI system.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Current opened page.
        /// </summary>
        public IPage? CurrentPage { get; }

        /// <summary>
        /// Occurs when user initializes the navigation process.
        /// </summary>
        public event EventHandler<NavigationEventArgs>? Navigation;

        /// <summary>
        /// Navigates to the given URL.
        /// </summary>
        /// <param name="url">Short path to open the page.</param>
        public void Navigate(string url);

        /// <summary>
        /// Navigates to the given <see cref="Uri"/>
        /// </summary>
        /// <param name="url">Path of the page.</param>
        public void NavigateTo(Uri url);

        /// <summary>
        /// Tries to navigate to the previous page if it's available.
        /// </summary>
        /// <returns>Opened page or <see langword="null"/> if there are no pages to open or back navigation is locked.</returns>
        public IPage? TryNavigateBack();

        /// <summary>
        /// Tries to navigate to the next page if it's available.
        /// </summary>
        /// <returns>Opened page or <see langword="null"/> if there are no pages to open or forward navigation is locked.</returns>
        public IPage? TryNavigateNext();

        /// <summary>
        /// Opens the first page of the current context.
        /// </summary>
        /// <returns>Opened page.</returns>
        public IPage OpenFromRoot();

        /// <summary>
        /// Closes the navigation service.
        /// </summary>
        public void Close();

        /// <summary>
        /// Clears the cache of the navigation service, including dispose of all opened and saved pages and their resources.
        /// </summary>
        public void Clear();
    }
}