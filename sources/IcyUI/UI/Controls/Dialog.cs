// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Linq;
using System.Threading.Tasks;
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
            // dialog is the topmost overlay of any kind, so one Escape press doesn't close every currently-open
            // Dialog at once, AND doesn't close this dialog out from under a nested ComboBox/Dropdown popup that
            // Escape should only close by itself (popups are also Canvas.Overlays entries, added after this
            // dialog - see Selector.cs's own AddOverlay call - so when one's open it's topmost, not this dialog).
            if (shownOnCanvas is { } canvas && canvas.Overlays.LastOrDefault() == this)
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

    /// <summary>
    /// A <see cref="Dialog"/> paired with a <see cref="Task{TResult}"/>-returning show/close API - the
    /// general-purpose modal-hosting primitive most callers use directly (or through <see cref="MessageBox"/>'s
    /// convenience methods). Not a <see cref="UIElement"/> itself; wraps a plain <see cref="Dialog"/> instance,
    /// which is what's actually added to a <see cref="Canvas"/> and themed - a closed generic type's runtime
    /// name embeds its type argument, so an open-generic <c>Style TargetType="Dialog"</c> entry would never
    /// match a <c>Dialog&lt;TResult&gt;</c> if it were a <see cref="UIElement"/> itself.
    /// </summary>
    /// <typeparam name="TResult">The type of value this dialog closes with.</typeparam>
    public sealed class Dialog<TResult>
    {
        private readonly Dialog dialog = new();
        private TaskCompletionSource<TResult?>? completionSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialog{TResult}"/> class.
        /// </summary>
        public Dialog()
        {
            dialog.Closed += (_, _) =>
            {
                // Escape (Dialog.OnCloseModal) or any other route to Dialog.Close() that didn't go through
                // this class's own Close(TResult?) - complete with default the same way a direct Close(TResult?)
                // would have.
                if (completionSource is { } source)
                {
                    completionSource = null;
                    source.SetResult(default);
                }
            };
        }

        /// <summary>Gets or sets the content this dialog displays.</summary>
        public UIElement? Content
        {
            get => dialog.Content;
            set => dialog.Content = value;
        }

        /// <summary>Gets a value indicating whether this dialog is currently shown.</summary>
        public bool IsOpen => dialog.IsOpen;

        /// <summary>
        /// Shows this dialog as a modal overlay on <paramref name="canvas"/> and returns a task that completes
        /// with whatever value <see cref="Close(TResult?)"/> is called with (or <see langword="default"/>, if
        /// dismissed via Escape). Calling this again while already open returns the same in-flight task instead
        /// of showing a second copy.
        /// </summary>
        /// <param name="canvas">The canvas to show this dialog on.</param>
        /// <returns>A task that completes with the dialog's result once it closes.</returns>
        public Task<TResult?> ShowAsync(Canvas canvas)
        {
            if (completionSource != null)
                return completionSource.Task;

            completionSource = new TaskCompletionSource<TResult?>();
            dialog.Show(canvas);
            return completionSource.Task;
        }

        /// <summary>
        /// Closes this dialog with <paramref name="result"/>, completing the task <see cref="ShowAsync(Canvas)"/>
        /// returned. A no-op if this dialog isn't currently open.
        /// </summary>
        /// <param name="result">The result to complete the <see cref="ShowAsync(Canvas)"/> task with.</param>
        public void Close(TResult? result)
        {
            if (completionSource is not { } source)
                return;

            completionSource = null;
            dialog.Close();
            source.SetResult(result);
        }
    }
}
