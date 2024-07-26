// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;

namespace AquaUI.Data.Markup
{
    /// <summary>
    /// Represents a class that contains all the information about all dependency properties defined on each type.
    /// </summary>
    public static class DependencyPropertyRegistry
    {
        private static readonly Dictionary<Type, List<IDependencyProperty>> Registry = [];

        /// <inheritdoc cref="GetProperties(Type, bool)"/>
        public static IEnumerable<IDependencyProperty> GetProperties(Type type)
        {
            return GetProperties(type, false);
        }

        /// <summary>
        /// Checks if the type hhas been registered in the registry.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns><see langword="true"/> if the type is registered; otherwise <see langword="false"/>.</returns>
        public static bool IsRegistered(Type type)
        {
            return Registry.ContainsKey(type);
        }

        /// <summary>
        /// Registers all the dependency properties for the specified type.
        /// </summary>
        /// <remarks>
        /// The method skips initialization if the type has been already registered.
        /// </remarks>
        /// <param name="type">The type to register properties for.</param>
        public static void RegisterType(Type type)
        {
            if (IsRegistered(type))
                return;
            Registry[type] = DependencyObjectRegistrator.ResolveProperties(type);
        }

        /// <summary>
        /// Gets all the properties reigstered for the specified type.
        /// </summary>
        /// <param name="type">The type to get properties for.</param>
        /// <param name="inherit">Determines whether to include properties registered for the base types.</param>
        /// <returns>All the properties registered for the specified type.</returns>
        public static IEnumerable<IDependencyProperty> GetProperties(Type type, bool inherit)
        {
            IEnumerable<IDependencyProperty> result = [];
            do
            {
                if (Registry.TryGetValue(type, out var properties))
                {
                    result = [.. result, .. properties];
                }
            }
            while (inherit && type != null);
            return result;
        }

        /// <inheritdoc cref="GetProperty(Type, string, bool)"/>
        public static IDependencyProperty GetProperty(Type type, string name)
        {
            return GetProperty(type, name, false);
        }

        /// <summary>
        /// Gets the information about the property with the specified name for the specified type.
        /// </summary>
        /// <param name="type">The type that stores the property.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="inherit">Determines whether to search properties in parent types.</param>
        /// <returns>The definition of the registered dependency property.</returns>
        /// <exception cref="InvalidOperationException">Occurs when there are no properties with the specified name registered for the requested type.</exception>
        public static IDependencyProperty GetProperty(Type type, string name, bool inherit)
        {
            do
            {
                if (Registry.TryGetValue(type, out var properties))
                {
                    var property = properties.FirstOrDefault(p => p.Name == name);
                    if (property != null)
                        return property;
                }

                type = type.BaseType!;
            }
            while (inherit && type != null);

            return ThrowHelper.ThrowInvalidOperationException<IDependencyProperty>("Can't find the specified property.");
        }

        /// <inheritdoc cref="RegisterAttached(string, Type, Type, PropertyMetadata, ValidateValueCallback?)"/>
        public static IDependencyProperty RegisterAttached(string name, Type propertyType, Type ownerType)
        {
            return RegisterAttached(name, propertyType, ownerType, PropertyMetadata.Default, null);
        }

        /// <inheritdoc cref="RegisterAttached(string, Type, Type, PropertyMetadata, ValidateValueCallback?)"/>
        public static IDependencyProperty RegisterAttached(string name, Type propertyType, Type ownerType, PropertyMetadata metadata)
        {
            return RegisterAttached(name, propertyType, ownerType, metadata, null);
        }

        /// <summary>
        /// Registers new attached property.
        /// </summary>
        /// <inheritdoc cref="RegisterProperty(string, Type, Type, PropertyMetadata, ValidateValueCallback?)"/>
        /// <returns>An object that deinfes registered attached property.</returns>
        public static IDependencyProperty RegisterAttached(string name, Type propertyType, Type ownerType, PropertyMetadata metadata, ValidateValueCallback? validation)
        {
            var result = new DependencyProperty(ownerType, name, propertyType)
            {
                Metadata = DependencyPropertyMetadata.CreateAttached(metadata),
                ValidationCallback = validation,
            };
            RegisterInternal(ownerType, result);

            return result;
        }

        /// =<inheritdoc cref="RegisterProperty(string, Type, Type, PropertyMetadata, ValidateValueCallback)"/>
        public static IDependencyProperty RegisterProperty(string name, Type propertyType, Type ownerType)
        {
            return RegisterProperty(name, propertyType, ownerType, PropertyMetadata.Default, null);
        }

        /// =<inheritdoc cref="RegisterProperty(string, Type, Type, PropertyMetadata, ValidateValueCallback)"/>
        public static IDependencyProperty RegisterProperty(string name, Type propertyType, Type ownerType, PropertyMetadata metadata)
        {
            return RegisterProperty(name, propertyType, ownerType, metadata, null);
        }

        /// <summary>
        /// Registers a property for the specified type.
        /// </summary>
        /// <param name="name">Name of the property.</param>
        /// <param name="propertyType">Type of the property values.</param>
        /// <param name="ownerType">Type that stores the property.</param>
        /// <param name="metadata">Metadata of the property.</param>
        /// <param name="validation">Callback that validates property values.</param>
        /// <returns>Information about registered dependency property.</returns>
        public static IDependencyProperty RegisterProperty(string name, Type propertyType, Type ownerType, PropertyMetadata metadata, ValidateValueCallback? validation)
        {
            var result = new DependencyProperty(ownerType, name, propertyType)
            {
                Metadata = metadata,
                ValidationCallback = validation,
            };
            RegisterInternal(ownerType, result);

            return result;
        }

        /// <inheritdoc cref="TryGetProperty(Type, string, bool, out IDependencyProperty?)"/>
        public static bool TryGetProperty(Type type, string name, [NotNullWhen(true)] out IDependencyProperty? property)
        {
            return TryGetProperty(type, name, false, out property);
        }

        /// <summary>
        /// Tries to get the property with the specified name for the specified type.
        /// </summary>
        /// <param name="type">The type to search properties in.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="inherit">Determines whether to search properties in base types.</param>
        /// <param name="property">The property with the specified name, or <see langword="null"/> if the property not found.</param>
        /// <returns><see langword="true"/> if the property has been found; otherwise <see langword="false"/>.</returns>
        public static bool TryGetProperty(Type type, string name, bool inherit, [NotNullWhen(true)] out IDependencyProperty? property)
        {
            property = null;
            do
            {
                if (Registry.TryGetValue(type, out var properties))
                {
                    property = properties.FirstOrDefault(p => p.Name == name);
                    if (property != null)
                        return true;
                }

                type = type.BaseType!;
            }
            while (inherit && type != null);
            return false;
        }

        private static void RegisterInternal(Type ownerType, DependencyProperty result)
        {
            if (Registry.TryGetValue(ownerType, out List<IDependencyProperty>? value))
            {
                value.Add(result);
            }
            else
            {
                Registry.Add(ownerType, [result]);
            }
        }
    }
}