// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;
using Icy.Data.Markup;

namespace Icy.Data
{
    /// <summary>
    /// Represents a default implementation of the assembly resolving service.
    /// </summary>
    public class AssemblyResolver : IAssemblyResolver
    {
        private readonly Dictionary<string, Type?> typesCache = [];

        /// <inheritdoc/>
        public Assembly DefaultAssembly { get; set; } = Assembly.GetExecutingAssembly();

        /// <inheritdoc/>
        public Assembly? FindAssembly(string name) => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == name);

        /// <inheritdoc/>
        public Type? FindType(string name)
        {
            if (typesCache.TryGetValue(name, out Type? value))
                return value;

            Type? result = null;
            try
            {
                result = Type.GetType(name) ?? DefaultAssembly.GetType(name);
                if (result == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        result = assembly.GetType(name);
                        if (result != null) break;

                        // Find only type with parameterless constructors
                        result = assembly.GetTypes().Where(x => x.GetConstructor([]) != null).FirstOrDefault(x => x.Name == name || x.FullName == name);
                        if (result != null) break;
                    }
                }
            }
            catch
            {
            }

            return typesCache[name] = result;
        }

        /// <inheritdoc/>
        public IEnumerable<Assembly> GetAssemblies() => AppDomain.CurrentDomain.GetAssemblies();
    }
}