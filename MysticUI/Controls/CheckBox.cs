using Stride.Core;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a checkbox control that user can set or remove.
    /// </summary>
    [DataAlias(nameof(CheckBox))]
    public class CheckBox : ToggleButton
    {
        private readonly Checkmark checkmark;

        /// <summary>
        /// Content of the <see cref="CheckBox"/>
        /// </summary>
        public override object? Content
        {
            get => base.Content;
            set
            {
                base.Content = value;
                SetCheckBoxLikeChild();
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="CheckBox"/> class.
        /// </summary>
        public CheckBox()
        {
            checkmark = (Checkmark)LayoutSerializer.Default.ActivationFactory(typeof(Checkmark))!;
            ContentTextWrapping = true;
        }

        /// <inheritdoc/>
        protected internal override void OnCheckedChanged()
        {
            base.OnCheckedChanged();
            checkmark.IsChecked = IsChecked;
        }

        private void SetCheckBoxLikeChild()
        {
            Grid panel = new();
            panel.Children.Add(checkmark);
            if (Child != null)
            {
                panel.Children.Add(Child);
                Child.GridColumn = 1;
            }
            Child = panel;
            Child.Parent = this;
            Child.Desktop = Desktop;
        }
    }
}