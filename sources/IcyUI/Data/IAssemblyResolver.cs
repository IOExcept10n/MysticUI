// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;

namespace Icy.Data
{
    /// <summary>
    /// Interface for resolving assemblies and types.
    /// </summary>
    public interface IAssemblyResolver
    {
        /// <summary>
        /// Gets the default assembly used by the resolver.
        /// </summary>
        Assembly DefaultAssembly { get; }

        /// <summary>
        /// Finds an assembly by the specified name.
        /// </summary>
        /// <param name="name">The name of the assembly to find.</param>
        /// <returns>
        /// The found assembly if it exists; otherwise, <see langword="null"/>.
        /// </returns>
        Assembly? FindAssembly(string name);

        /// <summary>
        /// Finds a type by the specified name.
        /// </summary>
        /// <param name="name">The name of the type to find.</param>
        /// <returns>
        /// The found type if it exists; otherwise, <see langword="null"/>.
        /// </returns>
        Type? FindType(string name);
    }

}