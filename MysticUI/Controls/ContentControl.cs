using MysticUI.Brushes.TextureBrushes;
using MysticUI.Extensions;
using Stride.Graphics;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a control that can store and display content of any type.
    /// </summary>
#pragma warning disable CS8631

    public class ContentControl : SingleItemControlBase<Control?>
#pragma warning restore CS8631
    {
        private object? content;
        private ControlTemplate? contentTemplate;

        /// <summary>
        /// Gets or sets the content of the control.
        /// </summary>
        [Content]
        public virtual object? Content
        {
            get => content;
            set
            {
                if ((value == null) != (content == null))
                {
                    NotifyPropertyChanged(nameof(HasContent));
                }
                content = value;
                ResetChild();
                OnContentChanged();
                NotifyPropertyChanged(nameof(Content));
            }
        }

        /// <summary>
        /// Determines whether the control has content.
        /// </summary>
        public bool HasContent => Content != null;

        /// <summary>
        /// A template to display content values if they're not presented as controls.
        /// </summary>
        public ControlTemplate? ContentTemplate
        {
            get => contentTemplate;
            set
            {
                contentTemplate = value;
                ResetChild();
                NotifyPropertyChanged(nameof(ContentTemplate));
            }
        }

        /// <summary>
        /// Occurs when the value of the <see cref="Content"/> property changes.
        /// </summary>
        public event EventHandler? ContentChanged;

        /// <inheritdoc/>
        public ContentControl()
        {
            AcceptFocus = true;
        }

        /// <inheritdoc/>
        protected internal virtual void OnContentChanged()
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        protected internal override void ResetChildInternal()
        {
            if (content == null)
            {
                Child = null;
                return;
            }
            if (content is Control control)
            {
                Child = control;
            }
            else if (contentTemplate != null)
            {
                Child = (Control)contentTemplate.Instantiate(content);
            }
            else if (content is Texture texture)
            {
                var image = new ImageBrush(texture);
                Child = new Image(image);
            }
            else if (content is IImage image)
            {
                Child = new Image(image);
            }
            else
            {
                Child = new TextBlock(content.ToString(), Font);
            }
        }
    }
}