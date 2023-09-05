namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the selectable content control for the <see cref="ListBox"/> control.
    /// </summary>
    public class ListBoxItem : ContentControl
    {
        /// <summary>
        /// Determines whether the item is selected.
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Uses the brush for the selection item.
        /// </summary>
        public IBrush? SelectionBrush { get; set; }

        /// <inheritdoc/>
        protected internal override IBrush? GetCurrentBackground()
        {
            IBrush? result;
            var state = InteractionState;
            if (state == ControlInteractionState.Clicking) result = TouchingBackground;
            else if (state == ControlInteractionState.MouseOver) result = MouseOverBackground;
            else if (IsSelected) result = SelectionBrush;
            else if (state == ControlInteractionState.Focused) result = FocusedBackground;
            else result = Background;
            result ??= Background;
            return result;
        }
    }
}