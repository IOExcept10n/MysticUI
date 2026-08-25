// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Markup.Attributes;
using Icy.Navigation;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="ContentControl"/> that a <see cref="Frame"/> can navigate to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A page is an ordinary <see cref="ContentControl"/> - its <see cref="ContentControl.Content"/> is the page's
    /// actual UI, settable via markup like any other content property. What makes it navigable is
    /// <see cref="INavigationService"/> knowing to construct/load it and swap it into a <see cref="Frame"/>'s
    /// <see cref="ContentControl.Content"/>, calling <see cref="OnNavigatedTo"/>/<see cref="OnNavigatedFrom"/> as it
    /// does.
    /// </para>
    /// <para>
    /// A markup file's root can be a <c>&lt;Page x:Class="MyApp.HomePage"&gt;</c> like any other element with an
    /// <c>x:Class</c> - see <see cref="Markup.MarkupDirectives.Class"/>. Loading a page through
    /// <see cref="INavigationService.Navigate"/> uses this the same way any other markup load does; there is no
    /// separate page-loading mechanism.
    /// </para>
    /// </remarks>
    public class Page : ContentControl
    {
        private bool keepAlive;
        private string? path;

        /// <summary>
        /// Gets a value indicating whether <see cref="Initialize"/> has already run for this instance.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether a <see cref="Navigation.NavigationService"/> should keep this
        /// page's instance in its back/forward history after navigating away from it.
        /// </summary>
        /// <remarks>
        /// A page that isn't kept alive is dropped from history as soon as its <see cref="Navigation.NavigationService"/>
        /// leaves it - navigating back past it isn't possible, and returning to the same logical page requires a
        /// fresh <see cref="INavigationService.Navigate"/> call. Keep it alive when the page holds state (scroll
        /// position, unsaved form input, a view-model) worth returning to unchanged.
        /// </remarks>
        [Category("Navigation")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool KeepAlive
        {
            get => keepAlive;
            set => SetProperty(ref keepAlive, value);
        }

        /// <summary>
        /// Gets or sets the path this page was loaded from, if it was loaded through
        /// <see cref="INavigationService.Navigate"/>.
        /// </summary>
        [Category("Navigation")]
        [Browsable(false)]
        [RegisterReference]
        public string? Path
        {
            get => path;
            set => SetProperty(ref path, value);
        }

        /// <summary>
        /// Runs this page's one-time setup, if it hasn't already.
        /// </summary>
        /// <remarks>
        /// Called by <see cref="Navigation.NavigationService"/> on every navigation to this page; the
        /// <see cref="IsInitialized"/> guard is what makes repeated calls (e.g. after a <see cref="KeepAlive"/>
        /// return trip) a no-op. Override <see cref="OnInitialize"/> for the actual one-time work rather than this
        /// method, so the guard can't accidentally be bypassed by a subclass forgetting to check it.
        /// </remarks>
        public void Initialize()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            OnInitialize();
        }

        /// <summary>
        /// Called every time a <see cref="Navigation.NavigationService"/> makes this page current, including
        /// repeat visits to a <see cref="KeepAlive"/> page - unlike <see cref="Initialize"/>, there is no
        /// once-only guard here.
        /// </summary>
        public virtual void Prepare()
        {
        }

        /// <summary>
        /// Called after a <see cref="Navigation.NavigationService"/> navigates to this page.
        /// </summary>
        /// <param name="sender">The <see cref="INavigationService"/> that performed the navigation.</param>
        /// <param name="args">The pages involved in the navigation.</param>
        protected internal virtual void OnNavigatedTo(object sender, NavigationEventArgs args)
        {
        }

        /// <summary>
        /// Called before a <see cref="Navigation.NavigationService"/> navigates away from this page.
        /// </summary>
        /// <param name="sender">The <see cref="INavigationService"/> performing the navigation.</param>
        /// <param name="args">The pages involved in the navigation.</param>
        protected internal virtual void OnNavigatedFrom(object sender, NavigationEventArgs args)
        {
        }

        /// <summary>
        /// This page's one-time setup. Runs once per instance, the first time <see cref="Initialize"/> is called.
        /// </summary>
        protected virtual void OnInitialize()
        {
        }
    }
}
