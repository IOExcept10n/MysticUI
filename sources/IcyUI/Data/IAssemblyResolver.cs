// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;

namespace Icy.Data
{
    public interface IAssemblyResolver
    {
        Assembly DefaultAssembly { get; }

        Assembly FindAssembly(string name);

        Type FindType(string name);
    }
}