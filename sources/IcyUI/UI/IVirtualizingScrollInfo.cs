// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Lets a <see cref="Controls.ScrollViewer"/> delegate extent/offset/viewport handling to its
    /// <see cref="Controls.ContentControl.Content"/>, instead of fully measuring and arranging it, when that
    /// content can realize only the portion of itself that's actually visible.
    /// </summary>
    /// <remarks>
    /// Mirrors WPF's <c>ScrollViewer</c> → <c>IScrollInfo</c> → <c>VirtualizingStackPanel</c> delegation. IcyUI has
    /// no separate virtualizing-panel type - <see cref="Controls.ItemsControl"/> implements this interface
    /// directly, since nothing else in the roadmap needs virtualized layout independent of an items list.
    /// </remarks>
    public interface IVirtualizingScrollInfo
    {
        /// <summary>
        /// Gets this content's full natural width, estimated or exact depending on how much of it has actually
        /// been measured so far.
        /// </summary>
        float ExtentWidth { get; }

        /// <summary>
        /// Gets this content's full natural height, estimated or exact depending on how much of it has actually
        /// been measured so far.
        /// </summary>
        float ExtentHeight { get; }

        /// <summary>
        /// Occurs when this content needs its host <see cref="Controls.ScrollViewer"/> to adjust
        /// <see cref="Controls.ScrollViewer.VerticalOffset"/> by a specific amount, so on-screen content doesn't
        /// visibly jump when something already realized (and positioned above the current viewport) changes size.
        /// </summary>
        /// <remarks>
        /// The event's <see langword="float"/> payload is the delta to apply
        /// (<c>VerticalOffset += delta</c>), not an absolute new value.
        /// </remarks>
        event EventHandler<float>? VerticalOffsetCorrectionRequested;

        /// <summary>
        /// Called by the host <see cref="Controls.ScrollViewer"/> whenever the offset or viewport size it's
        /// presenting this content through may have changed - the single point where this content decides what to
        /// realize, de-realize, and how to position it.
        /// </summary>
        /// <param name="horizontalOffset">How far the viewport is scrolled horizontally.</param>
        /// <param name="verticalOffset">How far the viewport is scrolled vertically.</param>
        /// <param name="viewportWidth">The visible viewport's width.</param>
        /// <param name="viewportHeight">The visible viewport's height.</param>
        void OnViewportChanged(float horizontalOffset, float verticalOffset, float viewportWidth, float viewportHeight);
    }
}
