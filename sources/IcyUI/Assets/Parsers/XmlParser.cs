// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Net.Mime;
using System.Xml.Serialization;

namespace Icy.Assets.Parsers
{
    /// <summary>
    /// Represents a simple implementation of the <c>XML</c> parser service.
    /// </summary>
    internal class XmlParser : IAssetParser
    {
        private readonly XmlSerializerFactory factory = new();

        /// <inheritdoc/>
        public string Format => MediaTypeNames.Text.Xml;

        /// <inheritdoc/>
        public T Parse<T>(Stream stream)
        {
            return (T)Parse(stream, typeof(T));
        }

        /// <inheritdoc/>
        public object Parse(Stream stream, Type targetType)
        {
            var serializer = factory.CreateSerializer(targetType);
            return serializer.Deserialize(stream)!;
        }
    }
}