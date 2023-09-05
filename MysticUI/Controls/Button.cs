namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a basic clickable control.
    /// Button can present content of any type, such as text, image or other controls.
    /// </summary>
    public class Button : ButtonBase
    {
        /// <summary>
        /// Gets or sets the value that is returned to the parent dialog window when the button is clicked.
        /// </summary>
        public DialogResult DialogResult { get; set; }

        /// <summary>
        /// Creates a new instance of the <see cref="Button"/> class.
        /// </summary>
        public Button()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Button"/> class with content.
        /// </summary>
        /// <param name="content">Content to set into a button.</param>
        public Button(object? content)
        {
            Content = content;
        }

        /// <inheritdoc/>
        protected internal override void OnClick()
        {
            base.OnClick();
            if (DialogResult != DialogResult.None)
            {
                ProcessParentRecursive(x =>
                {
                    if (x is DialogWindow window)
                    {
                        window.Result = DialogResult;
                        window.Close();
                        return false;
                    }
                    return true;
                });
            }
        }
    }
}