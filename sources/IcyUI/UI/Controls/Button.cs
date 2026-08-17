// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Windows.Input;
using Icy.Data.Markup.Attributes;
using Icy.Input.Events;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="ContentControl"/> that fires <see cref="Click"/>/executes <see cref="Command"/> when tapped
    /// (see <see cref="ITouchEvents"/>) and is a tab-stop by default.
    /// </summary>
    public class Button : ContentControl
    {
        private object? commandParameter;

        /// <summary>
        /// Initializes a new instance of the <see cref="Button"/> class.
        /// </summary>
        public Button()
        {
            IsFocusable = true;
        }

        /// <summary>
        /// Occurs when the button is tapped/activated.
        /// </summary>
        public event EventHandler? Click;

        /// <summary>
        /// Gets or sets the command to execute when the button is tapped/activated, in addition to raising <see cref="Click"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public ICommand? Command { get; set; }

        /// <summary>
        /// Gets or sets the parameter passed to <see cref="Command"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? CommandParameter
        {
            get => commandParameter;
            set => SetProperty(ref commandParameter, value);
        }

        /// <inheritdoc/>
        protected internal override void OnTap()
        {
            base.OnTap();
            OnClick();
        }

        /// <summary>
        /// Raises <see cref="Click"/> and executes <see cref="Command"/> if it can execute.
        /// </summary>
        protected virtual void OnClick()
        {
            Click?.Invoke(this, EventArgs.Empty);
            if (Command?.CanExecute(CommandParameter) == true)
                Command.Execute(CommandParameter);
        }
    }
}
