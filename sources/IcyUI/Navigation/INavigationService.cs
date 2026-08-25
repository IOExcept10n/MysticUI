// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI.Controls;

namespace Icy.Navigation
{
    /// <summary>
    /// Navigates a <see cref="Frame"/> between <see cref="Page"/>s, keeping a back/forward history.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Occurs before a navigation takes effect. Setting <see cref="System.ComponentModel.CancelEventArgs.Cancel"/>
        /// leaves the frame showing whatever it already was.
        /// </summary>
        event EventHandler<NavigationEventArgs>? Navigating;

        /// <summary>
        /// Gets the page currently hosted by this service's <see cref="Frame"/>, or <see langword="null"/> if none
        /// has been navigated to yet.
        /// </summary>
        Page? CurrentPage { get; }

        /// <summary>
        /// Loads the page at <paramref name="path"/> through the asset pipeline and navigates to it.
        /// </summary>
        /// <param name="path">A path resolved against the owning <see cref="Frame"/>'s configuration's default asset context.</param>
        /// <returns>The page navigated to, or <see langword="null"/> if <paramref name="path"/> doesn't exist.</returns>
        Page? Navigate(string path);

        /// <summary>
        /// Navigates to an already-constructed page instance.
        /// </summary>
        /// <param name="page">The page to navigate to.</param>
        /// <returns>
        /// <paramref name="page"/>, or the frame's unchanged <see cref="CurrentPage"/> if a <see cref="Navigating"/>
        /// handler canceled the navigation.
        /// </returns>
        Page NavigateTo(Page page);

        /// <summary>
        /// Navigates to the previous page in the back history, if any.
        /// </summary>
        /// <returns>The page navigated to, or <see langword="null"/> if there is no back history, or a handler canceled it.</returns>
        Page? TryNavigateBack();

        /// <summary>
        /// Navigates to the next page in the forward history, if any.
        /// </summary>
        /// <returns>The page navigated to, or <see langword="null"/> if there is no forward history, or a handler canceled it.</returns>
        Page? TryNavigateNext();

        /// <summary>
        /// Clears the back/forward history and the frame's current content.
        /// </summary>
        void Clear();
    }
}
