// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.UI.Controls;

namespace Icy.Navigation
{
    /// <summary>
    /// Carries the pages involved in a navigation, and lets a handler veto it before it takes effect.
    /// </summary>
    /// <remarks>
    /// The same instance is passed to <see cref="Page.OnNavigatedFrom"/> and <see cref="Page.OnNavigatedTo"/> for one
    /// navigation, so both sides see the same <see cref="PreviousPage"/>/<see cref="NextPage"/> pair.
    /// </remarks>
    /// <param name="previousPage">The page navigated away from, or <see langword="null"/> if the frame had none yet.</param>
    /// <param name="nextPage">The page navigated to.</param>
    public class NavigationEventArgs(Page? previousPage, Page? nextPage) : CancelEventArgs
    {
        /// <summary>
        /// Gets the page navigated away from, or <see langword="null"/> if the frame had none yet.
        /// </summary>
        public Page? PreviousPage { get; } = previousPage;

        /// <summary>
        /// Gets the page navigated to.
        /// </summary>
        public Page? NextPage { get; } = nextPage;
    }
}
