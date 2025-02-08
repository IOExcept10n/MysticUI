// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Net.Mime;
using System.Text.Json;

namespace Icy.Assets.Parsers
{
    /// <summary>
    /// Represents a simple implementation of the <c>JSON</c> parser service.
    /// </summary>
    internal class JsonParser : IAssetParser
    {
        /// <inheritdoc/>
        public string Format => MediaTypeNames.Application.Json;

        /// <inheritdoc/>
        public T Parse<T>(Stream stream)
        {
            return JsonSerializer.Deserialize<T>(stream)!;
        }

        /// <inheritdoc/>
        public object Parse(Stream stream, Type targetType)
        {
            return JsonSerializer.Deserialize(stream, targetType)!;
        }
    }
}