// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Assets
{
    /// <summary>
    /// Represents an exception that occurs when the requested asset wasn't found.
    /// </summary>
    [Serializable]
    public class AssetNotFoundException : KeyNotFoundException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssetNotFoundException"/> class.
        /// </summary>
        public AssetNotFoundException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetNotFoundException"/> class with the specified details message.
        /// </summary>
        /// <param name="message">Localized exception details message.</param>
        public AssetNotFoundException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetNotFoundException"/> class with the specified details message and inner exception instance.
        /// </summary>
        /// <param name="message">Localized exception details message.</param>
        /// <param name="inner">Inner exception instance that raised this exception.</param>
        public AssetNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}