// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Assets.Parsers
{
    /// <summary>
    /// Represents an interface for services that parse assets from the specified data formats.
    /// </summary>
    public interface IAssetParser
    {
        /// <summary>
        /// Gets the format for the serialization.
        /// </summary>
        string Format { get; }

        /// <summary>
        /// Performs the deserialization from the specified data stream.
        /// </summary>
        /// <typeparam name="T">Requested type to load.</typeparam>
        /// <param name="stream">Stream to read data from.</param>
        /// <returns>An instance of the deserialized object.</returns>
        T Parse<T>(Stream stream);

        /// <summary>
        /// Performs the deserialization from the specified data stream.
        /// </summary>
        /// <param name="stream">Stream to read data from.</param>
        /// <param name="targetType">Requested type to load.</param>
        /// <returns>An instance of the deserialized object.</returns>
        object Parse(Stream stream, Type targetType);
    }
}