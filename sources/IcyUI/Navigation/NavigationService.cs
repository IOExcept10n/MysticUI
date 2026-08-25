// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Navigation
{
    /// <summary>
    /// The default <see cref="INavigationService"/> - every <see cref="Frame"/> owns exactly one, created for it in
    /// its constructor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Navigate(string)"/> resolves <c>path</c> against the owning <see cref="Frame"/>'s configuration
    /// (<see cref="Configuration.AssetConfiguration.DefaultAssetContext"/>), the same asset pipeline any other
    /// <c>.xml</c> markup file loads through - see <see cref="Markup.MarkupImporter"/>. <see cref="IAssetContext.IsAvailable"/>
    /// is what makes a missing page return <see langword="null"/> instead of throwing: everything else the loader
    /// can fail on (malformed markup, a root that isn't a <see cref="Page"/>) is a genuine error, not a "not found".
    /// </para>
    /// <para>
    /// Back/forward history is two stacks of <see cref="Page"/> instances. Navigating away from a page only pushes
    /// it onto the relevant stack when <see cref="Page.KeepAlive"/> is set - see its remarks for why. A fresh
    /// forward-navigation (<see cref="Navigate(string)"/> or <see cref="NavigateTo"/>) always clears the forward
    /// history, the same way a browser discards forward history once you navigate somewhere new instead of
    /// following "next" again.
    /// </para>
    /// </remarks>
    /// <param name="frame">The frame this service navigates.</param>
    public sealed class NavigationService(Frame frame) : INavigationService
    {
        private readonly List<Page> backStack = [];
        private readonly List<Page> forwardStack = [];

        /// <inheritdoc/>
        public event EventHandler<NavigationEventArgs>? Navigating;

        /// <inheritdoc/>
        public Page? CurrentPage { get; private set; }

        /// <inheritdoc/>
        public Page? Navigate(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            IcyConfiguration configuration = frame.RequireConfiguration();
            IAssetContext context = configuration.Assets.DefaultAssetContext;
            if (!context.IsAvailable(path))
                return null;

            UIElement loaded = configuration.Assets.AssetResolver.LoadAsset<UIElement>(context, path);
            if (loaded is not Page page)
            {
                throw new InvalidOperationException(
                    $"'{path}' declares a '{loaded.GetType().Name}' as its root, but {nameof(Navigate)} requires a '{nameof(Page)}'.");
            }

            page.Path = path;
            return NavigateTo(page);
        }

        /// <inheritdoc/>
        public Page NavigateTo(Page page)
        {
            ArgumentNullException.ThrowIfNull(page);

            Page? leaving = CurrentPage;
            if (!Commit(page))
                return CurrentPage!;

            RecordLeaving(leaving, backStack);
            forwardStack.Clear();
            return page;
        }

        /// <inheritdoc/>
        public Page? TryNavigateBack() => TryNavigateAlong(backStack, forwardStack);

        /// <inheritdoc/>
        public Page? TryNavigateNext() => TryNavigateAlong(forwardStack, backStack);

        /// <inheritdoc/>
        public void Clear()
        {
            backStack.Clear();
            forwardStack.Clear();
            CurrentPage = null;
            frame.Content = null;
        }

        /// <summary>
        /// Pushes <paramref name="leaving"/> onto <paramref name="stack"/> if it opted into
        /// <see cref="Page.KeepAlive"/>; otherwise it is simply dropped, per <see cref="Page.KeepAlive"/>'s remarks.
        /// </summary>
        private static void RecordLeaving(Page? leaving, List<Page> stack)
        {
            if (leaving != null && leaving.KeepAlive)
                stack.Add(leaving);
        }

        /// <summary>
        /// Pops the top of <paramref name="source"/> and navigates to it, pushing the page left behind onto
        /// <paramref name="destination"/> - the shared implementation of <see cref="TryNavigateBack"/> (the back
        /// history is <c>source</c>) and <see cref="TryNavigateNext"/> (the forward history is <c>source</c>).
        /// </summary>
        private Page? TryNavigateAlong(List<Page> source, List<Page> destination)
        {
            if (source.Count == 0)
                return null;

            Page target = source[^1];
            source.RemoveAt(source.Count - 1);

            Page? leaving = CurrentPage;
            if (!Commit(target))
            {
                source.Add(target);
                return null;
            }

            RecordLeaving(leaving, destination);
            return target;
        }

        /// <summary>
        /// Raises <see cref="Navigating"/> and, unless canceled, performs the navigation: notifies the outgoing
        /// page, swaps the frame's <see cref="ContentControl.Content"/>, then initializes/prepares/notifies the
        /// incoming one.
        /// </summary>
        /// <returns><see langword="true"/> if the navigation proceeded; <see langword="false"/> if a handler canceled it.</returns>
        private bool Commit(Page target)
        {
            var args = new NavigationEventArgs(CurrentPage, target);
            Navigating?.Invoke(this, args);
            if (args.Cancel)
                return false;

            Page? previous = CurrentPage;
            previous?.OnNavigatedFrom(this, args);

            CurrentPage = target;
            frame.Content = target;

            target.Initialize();
            target.Prepare();
            target.OnNavigatedTo(this, args);

            return true;
        }
    }
}
