// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Diagnostics;
using Icy.Data.Bindings;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents an object that can be used in the dependency properties system.
    /// </summary>
    public abstract class DependencyObject : BindableObject, IDependencyObject
    {
        private readonly Dictionary<IDependencyProperty, object?> propertyValues = [];

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<ValidationResult?>>? ValidationFailed;

        /// <inheritdoc/>
        public void ClearValue(IDependencyProperty property)
        {
            propertyValues.Remove(property);
        }

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<IDependencyProperty, object?>> EnumerateLocalValues()
        {
            return propertyValues;
        }

        /// <inheritdoc/>
        public object? GetValue(IDependencyProperty property)
        {
            if (!this.ContainsProperty(property))
                return ThrowHelper.ThrowArgumentException<object?>(nameof(property), "Can't access the property in this dependency object.");

            // Case when the property has its local value.
            if (propertyValues.TryGetValue(property, out var value))
                return value;

            return GetDefaultValue(property);
        }

        /// <inheritdoc/>
        public void SetValue(IDependencyProperty property, object? value)
        {
            if (!this.ContainsProperty(property))
                ThrowHelper.ThrowArgumentException<object?>(nameof(property), "Can't access the property in this dependency object.");

            // Special cases: bindings, animations etc.
            if (value is IBinding binding && (property.Metadata as DependencyPropertyMetadata)?.IsBindable != false)
            {
                Bind(binding);
                return;
            }

            if (CheckAccess())
            {
                UpdateValue(property, value);
            }
            else
            {
                Dispatcher.Invoke(
                    () => UpdateValue(property, value),
                    DispatcherPriority.DataBind);
            }
        }

        /// <summary>
        /// Indicates that the property value has been changed.
        /// </summary>
        /// <param name="property">A dependency property that initiated the update.</param>
        /// <param name="oldValue">Previous property value.</param>
        /// <param name="newValue">New property value.</param>
        protected virtual void OnValueSet(IDependencyProperty property, object? oldValue, object? newValue)
        {
        }

        /// <summary>
        /// Gets the current default value for the property.
        /// </summary>
        /// <remarks>
        /// When overridden, it can get style or resources-defined data for the property.
        /// </remarks>
        /// <param name="property">The property to get default value for.</param>
        /// <returns>The current set default value for the property.</returns>
        protected virtual object? GetDefaultValue(IDependencyProperty property)
        {
            return property.Metadata.DefaultValue;
        }

        /// <summary>
        /// Updates the local value of a specified dependency property.
        /// </summary>
        /// <param name="property">A property to update value.</param>
        /// <param name="newValue">New property value to set.</param>
        private void UpdateValue(IDependencyProperty property, object? newValue)
        {
            if (!propertyValues.TryGetValue(property, out var oldValue) || oldValue != newValue)
            {
                var result = property.ValidationCallback?.Invoke(newValue!);
                if (result != ValidationResult.Success)
                {
                    ValidationFailed?.Invoke(this, result);
                    return;
                }

                propertyValues[property] = newValue;
                OnValueSet(property, oldValue, newValue);
                property.Metadata.PropertyChangedCallback?.Invoke(this, new(property.Name));
                OnPropertyChanged(property.Name);
            }
        }
    }
}