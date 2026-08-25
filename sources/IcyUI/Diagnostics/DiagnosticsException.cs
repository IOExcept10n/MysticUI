// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Diagnostics
{
    /// <summary>
    /// The exception raised for a debug-tool registration error - a name collision, or a type that doesn't
    /// implement the interface it's being registered as.
    /// </summary>
    public class DiagnosticsException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsException"/> class.
        /// </summary>
        /// <param name="message">A description of what went wrong.</param>
        public DiagnosticsException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsException"/> class.
        /// </summary>
        public DiagnosticsException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsException"/> class.
        /// </summary>
        /// <param name="message">A description of what went wrong.</param>
        /// <param name="innerException">The underlying exception, when this error wraps one.</param>
        public DiagnosticsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
