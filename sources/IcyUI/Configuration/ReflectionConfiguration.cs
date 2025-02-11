// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents configuration for the reflection-related service used in library.
    /// </summary>
    public class ReflectionConfiguration
    {
        /// <summary>
        /// Gets or sets an instance of the <see cref="IAssemblyResolver"/> used in library.
        /// </summary>
        public IAssemblyResolver AssemblyResolver { get; set; } = new AssemblyResolver();

        /// <summary>
        /// Gets or sets an instance of the <see cref="ITypeConverter"/> used in library.
        /// </summary>
        public ITypeConverter TypeConverter { get; set; } = new TypeConversionManager();
    }
}