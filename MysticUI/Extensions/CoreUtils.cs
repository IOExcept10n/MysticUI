using CommunityToolkit.Diagnostics;
using Stride.Core.Reflection;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MysticUI.Extensions
{
    /// <summary>
    /// Extension methods for some library elements.
    /// </summary>
    public static class CoreUtils
    {
        /// <summary>
        /// Checks if the value isn't <see langword="null"/>
        /// </summary>
        /// <typeparam name="T">Type of the object.</typeparam>
        /// <param name="value">Value to check.</param>
        /// <returns>Value if not <see langword="null"/>, throws an exception otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NeverNull<T>(this T? value) where T : class
        {
            Guard.IsNotNull(value);
            return value;
        }

        /// <summary>
        /// Checks if the value is default for its property.
        /// </summary>
        /// <remarks>
        /// Default property value can be set with <see cref="DefaultValueAttribute"/>.
        /// </remarks>
        /// <param name="property">Property to check.</param>
        /// <param name="value">Value to check.</param>
        /// <returns><see langword="true"/> if the value is equal to property default value, <see langword="false"/> otherwise.</returns>
        public static bool IsDefaultValue(this PropertyInfo property, object? value)
        {
            var defaultAttribute = property.GetCustomAttribute<DefaultValueAttribute>();
            LayoutSerializer serializer = LayoutSerializer.Default;
            if (defaultAttribute != null)
            {
                if (defaultAttribute.Value is string attributeString)
                {
                    var state = serializer.GetPropertyState(property);
                    if (state == LayoutSerializer.PropertyState.Parsable)
                    {
                        object? testValue = serializer.ParseProperty(attributeString, property.PropertyType);
                        return Equals(value, testValue);
                    }
                }
                return Equals(value, defaultAttribute.Value);
            }
            else
            {
                return value?.GetType().IsValueType == true ? value.Equals(serializer.ActivationFactory(value.GetType())) : value == null;
            }
        }

        /// <summary>
        /// Searches a type with available parameterless constructor in all scanned assemblies.
        /// </summary>
        /// <param name="name">Name of the type.</param>
        /// <returns>Type if found, <see langword="null"/> if the type couldn't be found.</returns>
        public static Type? SearchType(string name)
        {
            Type? result = null;
            try
            {
                // Try to get the type from the one of the most actual assemblies to find: default, current (MysticUI) or calling.
                result = Type.GetType(name) ?? 
                    TryGetType(Assembly.GetExecutingAssembly(), name) ?? 
                    TryGetType(Assembly.GetCallingAssembly(), name);
                // Try to use the full search if the finding option is unavailable.
                if (result == null)
                {
                    foreach (var assembly in AssemblyRegistry.FindAll())
                    {
                        result = TryGetType(assembly, name);
                        if (result != null) break;
                    }
                }
            }
            catch { }
            return result;
        }

        private static Type? TryGetType(Assembly targetAssembly, string name)
        {
            return targetAssembly.GetType(name) ?? targetAssembly.GetTypes()
                .Where(x => x.GetConstructor(Array.Empty<Type>()) != null)
                .FirstOrDefault(x => x.Name == name || x.FullName == name);
        }
    }
}