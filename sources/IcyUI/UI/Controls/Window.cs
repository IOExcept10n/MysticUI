// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Linq;
using Icy.Data.Markup.Attributes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A modal dialog - a <see cref="UIElement.IsFocusScope"/> that traps Tab/gamepad focus traversal within
    /// itself while <see cref="IsOpen"/>, and restores whatever was focused before it opened when it closes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No backdrop/pointer-input isolation in v1 - opening a <see cref="Window"/> only traps keyboard/gamepad
    /// focus traversal (via <see cref="UIElement.IsFocusScope"/>, see <see cref="UI.Canvas.MoveFocus"/>); elements
    /// behind it remain clickable. A caller wanting true pointer modality needs to layer that on top (e.g. a
    /// full-screen sibling element with a high enough <see cref="UIElement.ZIndex"/>) - not attempted here.
    /// </para>
    /// <para>
    /// Must be added to a <see cref="UI.Canvas"/> (directly or nested) like any other element - there's no
    /// separate popup/overlay layer; position it (e.g. via <see cref="UIElement.ZIndex"/>) as needed.
    /// </para>
    /// </remarks>
    public class Window : ContentControl
    {
        private bool isOpen;

        /// <summary>
        /// Initializes a new instance of the <see cref="Window"/> class.
        /// </summary>
        public Window()
        {
            IsFocusScope = true;
            IsVisible = false;
        }

        /// <summary>
        /// Occurs after the window closes (<see cref="IsOpen"/> becomes <see langword="false"/>).
        /// </summary>
        public event EventHandler? Closed;

        /// <summary>
        /// Occurs after the window opens (<see cref="IsOpen"/> becomes <see langword="true"/>).
        /// </summary>
        public event EventHandler? Opened;

        /// <summary>
        /// Gets or sets a value indicating whether the window is open. Setting this also sets
        /// <see cref="UIElement.IsVisible"/> and, when opening, focuses the window itself (if
        /// <see cref="UIElement.IsFocusable"/>) or otherwise its first focusable descendant.
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

                IsVisible = value;
                if (value)
                    OnOpened();
                else
                    OnClosed();
            }
        }

        /// <summary>
        /// Closes the window - equivalent to setting <see cref="IsOpen"/> to <see langword="false"/>.
        /// </summary>
        public void Close() => IsOpen = false;

        /// <summary>
        /// Opens the window - equivalent to setting <see cref="IsOpen"/> to <see langword="true"/>.
        /// </summary>
        public void Show() => IsOpen = true;

        /// <summary>
        /// Raises <see cref="Closed"/> and restores whatever was focused before this window was entered (via
        /// <see cref="UI.Canvas.CloseFocusScope"/>).
        /// </summary>
        protected virtual void OnClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
            Canvas?.CloseFocusScope(this);
        }

        /// <summary>
        /// Raises <see cref="Opened"/> and focuses this window (if focusable) or its first focusable descendant.
        /// </summary>
        protected virtual void OnOpened()
        {
            Opened?.Invoke(this, EventArgs.Empty);
            UIElement? target = IsFocusable ? this : EnumerateVisualSubtree().FirstOrDefault(element => element != this && element.IsFocusable);
            Canvas?.Focus(target);
        }
    }
}
