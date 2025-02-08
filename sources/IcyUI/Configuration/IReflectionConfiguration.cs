// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents configuration for the library reflection-based services.
    /// </summary>
    public interface IReflectionConfiguration
    {
        /// <summary>
        /// Gets or sets an instance of the assembly resolving service.
        /// </summary>
        public IAssemblyResolver AssemblyResolver { get; set; }

        /// <summary>
        /// Gets or sets an instance for the type conversion service.
        /// </summary>
        public ITypeConverter TypeConverter { get; set; }
    }
}