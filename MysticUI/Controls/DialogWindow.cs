using Stride.Core.Mathematics;

namespace MysticUI.Controls
{
    // This enum was made with backward compatibility with the WinForms DialogResult enum.

    /// <summary>
    /// Represents a set of the most common results of any dialog windows.
    /// </summary>
    public enum DialogResult
    {
        /// <summary>
        /// <see langword="Nothing"/> is returned from the dialog box. That means that the modal dialog continues running.
        /// </summary>
        None,

        /// <summary>
        /// The dialog box return value is <see langword="OK"/>.
        /// </summary>
        OK,

        /// <summary>
        /// The dialog box return value is <see langword="Cancel"/>.
        /// </summary>
        Cancel,

        /// <summary>
        /// The dialog box return value is <see langword="Abort"/>.
        /// </summary>
        Abort,

        /// <summary>
        /// The dialog box return value is <see langword="Retry"/>.
        /// </summary>
        Retry,

        /// <summary>
        /// The dialog box return value is <see langword="Ignore"/>.
        /// </summary>
        Ignore,

        /// <summary>
        /// The dialog box return value is <see langword="Yes"/>.
        /// </summary>
        Yes,

        /// <summary>
        /// The dialog box return value is <see langword="No"/>.
        /// </summary>
        No,

        /// <summary>
        /// The dialog box return value is <see langword="Try Again"/>.
        /// </summary>
        TryAgain = 10,

        /// <summary>
        /// The dialog box return value is <see langword="Continue"/>.
        /// </summary>
        Continue = 11
    }

    /// <summary>
    /// Represents a base class for the modal dialog windows.
    /// </summary>
    /// <remarks>
    /// You can call the <see cref="DialogWindow"/> to display the modal dialog box in your game.
    /// You are able to call it using either async method
    /// or by call the <see cref="Window.ShowModal(Desktop, Point?)"/> method
    /// and wait for the <see cref="Window.OnClosed"/> event to handle its result.
    /// </remarks>
    public class DialogWindow : Window
    {
        /// <summary>
        /// Gets or sets the result of the modal dialog window.
        /// </summary>
        public DialogResult Result { get; set; }

        /// <summary>
        /// Shows the dialog modal window and waits for its closure to get the result of the dialog.
        /// </summary>
        /// <param name="desktop">The desktop to place the window into.</param>
        /// <param name="position">Position to place the window in.</param>
        /// <returns>Result of the dialog modal box.</returns>
        public Task<DialogResult> ShowDialogAsync(Desktop desktop, Point? position = null)
        {
            TaskCompletionSource<DialogResult> completionSource = new();
            OnClosed += (_, __) => completionSource.SetResult(Result);
            ShowModal(desktop, position);
            return completionSource.Task;
        }
    }
}