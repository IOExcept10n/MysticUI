// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Data.Bindings
{
    /// <summary>
    /// Represents an exception that is thrown when the binding is in wrong state and can't be executed.
    /// </summary>
    [Serializable]
    public class BindingException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BindingException"/> class.
        /// </summary>
        public BindingException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BindingException"/> class.
        /// </summary>
        /// <inheritdoc cref="InvalidOperationException(string?)"/>
        public BindingException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BindingException"/> class.
        /// </summary>
        /// <inheritdoc cref="InvalidOperationException(string?, Exception)"/>
        public BindingException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}