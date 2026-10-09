// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Threading.Tasks;

namespace Icy.UI.Controls
{
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
