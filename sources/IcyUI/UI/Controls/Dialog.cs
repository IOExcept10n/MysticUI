// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Linq;
using Icy.Input.Events;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A modal overlay - a full-viewport dimming backdrop (<see cref="Control.Chrome"/>, themed) behind a
    /// centered content box, blocking pointer input to everything behind it (see the type's own remarks) and
    /// trapping keyboard/gamepad focus while <see cref="IsOpen"/>. Carries no result of its own - see
    /// <see cref="Dialog{TResult}"/> for the <see cref="System.Threading.Tasks.Task"/>-returning API most
    /// callers actually want; this class is its non-generic (themeable) building block.
    /// </summary>
    /// <remarks>
    /// Reuses <see cref="Canvas.HitTest(System.Drawing.Point)"/>'s existing overlay-first, topmost-wins
    /// behavior for pointer modality - no new <see cref="Canvas"/> mechanism is needed. A backdrop click is
    /// swallowed (nothing behind the overlay is ever hit-tested) but never closes the dialog by itself; only
    /// an explicit <see cref="Close()"/> call (e.g. a button) or Escape does.
    /// </remarks>
    public class Dialog : ContentControl
    {
        private Canvas? shownOnCanvas;
        private INavigationEvents? subscribedNavigation;

        /// <summary>Occurs after this dialog closes (<see cref="IsOpen"/> becomes <see langword="false"/>).</summary>
        public event EventHandler? Closed;

        /// <summary>Gets a value indicating whether this dialog is currently shown.</summary>
        public bool IsOpen => shownOnCanvas != null;

        /// <summary>
        /// Shows this dialog as a modal overlay on <paramref name="canvas"/> - adds it to
        /// <see cref="Canvas.Overlays"/>, enters a focus scope (mirrors <see cref="Window.OnOpened"/>), and
        /// subscribes Escape (<see cref="INavigationEvents.CloseModal"/>) to <see cref="Close()"/>. A no-op if
        /// already open.
        /// </summary>
        /// <param name="canvas">The canvas to show this dialog on.</param>
        public void Show(Canvas canvas)
        {
            if (shownOnCanvas != null)
                return;

            shownOnCanvas = canvas;
            canvas.AddOverlay(this);

            IsFocusScope = true;
            UIElement? focusTarget = IsFocusable ? this : EnumerateVisualSubtree().FirstOrDefault(element => element != this && element.IsFocusable);
            canvas.Focus(focusTarget);

            subscribedNavigation = canvas.Configuration.Input.Events.Navigation;
            subscribedNavigation.CloseModal += OnCloseModal;
        }

        /// <summary>
        /// Closes this dialog - removes the overlay, restores whatever was focused before <see cref="Show(Canvas)"/>,
        /// and raises <see cref="Closed"/>. A no-op if this dialog isn't currently open.
        /// </summary>
        public void Close() => Close(restoreFocus: true);

        private void OnCloseModal(object? sender, EventArgs e)
        {
            // Canvas's own generic OnCloseModal handler (see Canvas.cs's EnsureInputRoutingInitialized/
            // OnCloseModal) is already subscribed to this same event, and - since it's subscribed once, lazily,
            // on the canvas's first Render() call, which always happens well before any Dialog is ever shown in
            // a real app - it always runs first. It already finds whatever focus scope is currently active and
            // restores focus for it, consuming the one-shot scopeReturnFocus entry as it does. So this handler
            // must NOT also call CloseFocusScope for the common case (that would find the entry already gone and
            // clear focus to null instead of leaving it restored) - and it must only react at all when THIS
            // dialog is the topmost one, so one Escape press doesn't close every currently-open Dialog at once.
            if (shownOnCanvas is { } canvas && canvas.Overlays.OfType<Dialog>().LastOrDefault() == this)
                Close(restoreFocus: false);
        }

        private void Close(bool restoreFocus)
        {
            if (shownOnCanvas == null)
                return;

            subscribedNavigation!.CloseModal -= OnCloseModal;
            subscribedNavigation = null;

            Canvas canvas = shownOnCanvas;
            shownOnCanvas = null;
            canvas.RemoveOverlay(this);

            Closed?.Invoke(this, EventArgs.Empty);

            if (restoreFocus)
                canvas.CloseFocusScope(this);
        }
    }
}
