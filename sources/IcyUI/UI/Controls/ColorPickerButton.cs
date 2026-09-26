// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using Icy.Data;
using Icy.Data.Markup.Attributes;
using Icy.Input.Events;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A swatch-preview button that opens a full <see cref="Controls.ColorPicker"/> in a popup.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="Selector"/>'s own relationship to <see cref="ToggleButton"/> exactly: this class derives
    /// from <see cref="Control"/> (not <see cref="ToggleButton"/>) and composes an internal <see cref="ToggleButton"/>
    /// as <see cref="Control.Chrome"/>'s child, since this control owns popup-lifecycle/<see cref="IsOpen"/>/
    /// <see cref="SelectedColor"/> state that doesn't belong on <see cref="ToggleButton"/> itself. The popup is a
    /// <see cref="UI.Canvas.AddOverlay(UIElement)"/>/<see cref="UI.Canvas.RemoveOverlay(UIElement)"/> non-modal
    /// popup anchored under the button, with outside-click dismiss - the exact mechanism <see cref="Selector"/>
    /// already established for <see cref="Dropdown"/>/<see cref="ComboBox"/>, reused wholesale here.
    /// </remarks>
    public class ColorPickerButton : Control
    {
        private readonly ToggleButton toggle = new();
        private readonly ColorPicker picker = new();
        private readonly Border popupRoot;
        private bool isOpen;
        private Canvas? openedOnCanvas;
        private ITouchEvents? subscribedTouch;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorPickerButton"/> class.
        /// </summary>
        public ColorPickerButton()
        {
            // A deliberate tradeoff, not an oversight: assigning Background here (while toggle.Template is still
            // null) pins it as a local value - per Control's own value-precedence rules, that permanently outranks
            // every tier below it, including DefaultTheme.xml's ToggleButton Hovered/Pressed/Checked Background
            // VisualStates. This toggle is a colour-preview swatch, not a conventional button - always showing
            // the exact picked colour matters more here than hover/press feedback.
            toggle.Background = new SolidColorBrush(picker.SelectedColor);
            toggle.IsCheckedChanged += (_, _) => IsOpen = toggle.IsChecked;
            picker.ColorChanged += (_, _) =>
            {
                toggle.Background = new SolidColorBrush(picker.SelectedColor);
                ColorChanged?.Invoke(this, EventArgs.Empty);
            };

            popupRoot = new Border
            {
                Child = picker,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            ((Border)Chrome).Child = toggle;
        }

        /// <summary>
        /// Occurs when <see cref="SelectedColor"/> changes.
        /// </summary>
        public event EventHandler? ColorChanged;

        /// <summary>
        /// Gets or sets the currently picked color.
        /// </summary>
        [Category("Appearance")]
        [RegisterReference]
        public Color SelectedColor
        {
            get => picker.SelectedColor;
            set => picker.SelectedColor = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the popup is currently shown.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool IsOpen
        {
            get => isOpen;
            set
            {
                if (!SetProperty(ref isOpen, value))
                    return;
                toggle.IsChecked = value;
                if (value)
                    OpenPopup();
                else
                    ClosePopup();
            }
        }

        /// <summary>
        /// Gets the caller-supplied palette shown in the popup's "Swatches" tab.
        /// </summary>
        /// <remarks>
        /// Forwards directly to <see cref="Controls.ColorPicker.SwatchColors"/> - call this class's own
        /// <see cref="RefreshSwatches"/> after mutating this collection (the internal <see cref="ColorPicker"/>
        /// instance is private, so its own <see cref="Controls.ColorPicker.RefreshSwatches"/> isn't reachable from
        /// outside this class).
        /// </remarks>
        [Category("Content")]
        [RegisterReference]
        public IList<Color> SwatchColors => picker.SwatchColors;

        /// <summary>
        /// Rebuilds the popup's internal <see cref="Controls.ColorPicker"/>'s "Swatches" tab's buttons from the
        /// current <see cref="SwatchColors"/>.
        /// </summary>
        public void RefreshSwatches() => picker.RefreshSwatches();

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            if (isOpen)
                OpenPopup();
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            openedOnCanvas?.RemoveOverlay(popupRoot);
            openedOnCanvas = null;
            isOpen = false;
            toggle.IsChecked = false;

            if (subscribedTouch != null)
            {
                subscribedTouch.TouchDown -= OnOutsideTouchDown;
                subscribedTouch = null;
            }

            base.OnDetached();
        }

        private void OpenPopup()
        {
            if (Canvas == null)
                return;

            openedOnCanvas = Canvas;
            PositionPopup();
            Canvas.AddOverlay(popupRoot);

            subscribedTouch = Canvas.Configuration.Input.Events.Touch;
            subscribedTouch.TouchDown += OnOutsideTouchDown;
        }

        private void ClosePopup()
        {
            openedOnCanvas?.RemoveOverlay(popupRoot);
            openedOnCanvas = null;

            if (subscribedTouch != null)
            {
                subscribedTouch.TouchDown -= OnOutsideTouchDown;
                subscribedTouch = null;
            }
        }

        private void PositionPopup()
        {
            if (Canvas == null)
                return;

            Point bottomLeft = PointToScreen(new Vector2(0, ActualBounds.Height));
            popupRoot.Margin = new Thickness(bottomLeft.X, bottomLeft.Y, 0, 0);
        }

        private void OnOutsideTouchDown(object? sender, GenericEventArgs<Point> e)
        {
            UIElement? hit = openedOnCanvas?.HitTest(e.Data);
            for (UIElement? current = hit; current != null; current = current.Parent)
            {
                if (current == this || current == popupRoot)
                    return;
            }

            IsOpen = false;
        }
    }
}
