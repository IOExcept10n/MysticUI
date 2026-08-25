// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Configuration;
using Icy.Navigation;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="ContentControl"/> that hosts the <see cref="Page"/> its own <see cref="Navigation"/> service
    /// currently points to.
    /// </summary>
    /// <remarks>
    /// Unlike the rest of a document's tree, a frame's <see cref="ContentControl.Content"/> isn't meant to be set
    /// directly - navigate its <see cref="Navigation"/> service instead, which sets it as a side effect of
    /// <see cref="INavigationService.NavigateTo"/>. A document can host more than one frame (e.g. a persistent nav
    /// pane next to a content frame); each gets its own independent history.
    /// </remarks>
    public class Frame : ContentControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Frame"/> class.
        /// </summary>
        public Frame()
        {
            Navigation = new NavigationService(this);
        }

        /// <summary>
        /// Gets the navigation service that controls this frame's <see cref="ContentControl.Content"/>.
        /// </summary>
        [Browsable(false)]
        public INavigationService Navigation { get; }

        /// <summary>
        /// Gets this frame's configuration, for <see cref="NavigationService"/> to resolve assets through.
        /// </summary>
        /// <returns>This frame's <see cref="IcyConfiguration"/>.</returns>
        /// <exception cref="InvalidOperationException">The frame isn't attached to a <see cref="UI.Canvas"/> yet.</exception>
        internal IcyConfiguration RequireConfiguration() =>
            Configuration ?? throw new InvalidOperationException(
                $"'{nameof(Frame)}' must be attached to a '{nameof(UI.Canvas)}' before its '{nameof(Navigation)}' service can load a page.");
    }
}
