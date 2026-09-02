// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Markup.Attributes;
using Icy.Markup;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="Control"/> that hosts a single arbitrary <see cref="UIElement"/> as its content, decorated by
    /// the inherited <see cref="Control.Background"/>/<see cref="Control.BorderBrush"/>/<see cref="Control.BorderThickness"/>.
    /// </summary>
    /// <remarks>
    /// When <see cref="Control.Template"/> is set, the template's content is responsible for actually hosting
    /// <see cref="Content"/> - typically via a <see cref="ContentPresenter"/> bound with
    /// <c>{TemplateBinding Content}</c>. <see cref="Content"/> itself keeps working the same way either way; only
    /// where it's stored (and who parents it) changes, mirroring <see cref="Control"/>'s own dual-path
    /// <see cref="Control.Background"/>/etc.
    /// </remarks>
    [ContentProperty(nameof(Content))]
    public class ContentControl : Control
    {
        private UIElement? templatedContent;

        /// <summary>
        /// Gets or sets the element displayed as this control's content.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? Content
        {
            get => Template == null ? ((Border)Chrome).Child : templatedContent;
            set
            {
                if (Template == null)
                {
                    ((Border)Chrome).Child = value;
                    return;
                }

                if (templatedContent == value)
                    return;

                UIElement? old = templatedContent;

                // Deliberately NOT wiring the new value's Parent/Canvas here: when templated, this control
                // doesn't host Content directly anymore - whatever in the template actually displays it
                // (typically a ContentPresenter bound with {TemplateBinding Content}) does its own Parent/Canvas
                // wiring, reactively, the moment SetProperty below raises PropertyChanged (synchronously - see
                // Binding's Reactive mode). Wiring it here too would run AFTER that and silently overwrite the
                // correct parent back to this control - found the hard way: it happened to go unnoticed on a
                // templated Button whose own BorderThickness matched the template's hardcoded one by coincidence,
                // masking the wrong ContentBounds it produced, but broke visibly on a CheckBox where they differ.
                SetProperty(ref templatedContent, value);

                // The old value's detachment, on the other hand, is safe to do unconditionally here regardless -
                // it's being replaced either way, whoever currently parents it.
                if (old != null)
                {
                    old.Parent = null;
                    old.Canvas = null;
                }

                InvalidateMeasure();
            }
        }

        /// <inheritdoc/>
        protected override object? CaptureTemplateState() => Content;

        /// <inheritdoc/>
        protected override void RestoreTemplateState(object? state)
        {
            var content = (UIElement?)state;
            if (Template == null)
            {
                // Chrome has already been replaced by the fresh default Border by this point (see the base
                // Control.Template setter's remarks on RestoreTemplateState's two call points) - assigning
                // through its own Child setter directly is fine, it's brand new.
                ((Border)Chrome).Child = content;
                return;
            }

            // Direct field write, not through the public Content setter above - going through it here would
            // raise PropertyChanged and poison Content's own value-precedence entry as a phantom local
            // assignment, exactly the bug Control.Template's own remarks describe for Background/etc.
            templatedContent = content;
            if (content != null)
            {
                content.Parent = this;
                content.Canvas = Canvas;
            }
        }
    }
}
