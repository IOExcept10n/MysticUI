// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using Icy.Data.Bindings.Attributes;
using Icy.Data.Markup.Attributes;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Helper class to scan types for the defined dependency properties as attributes.
    /// </summary>
    internal static class DependencyObjectRegistration
    {
        /// <summary>
        /// Scans the type for the defined dependency properties and returns the list of found and formed properties (not yet registered).
        /// </summary>
        /// <param name="targetType">The type to scan.</param>
        /// <returns>The list of properties built by resolver.</returns>
        public static List<IDependencyProperty> ResolveProperties(Type targetType)
        {
            if (targetType.GetInterface(nameof(IDependencyObject)) == null)
            {
                return ThrowHelper.ThrowInvalidOperationException<List<IDependencyProperty>>($"Can't register dependency properties for object that doesn't implement {nameof(IDependencyObject)} interface.");
            }

            List<IDependencyProperty> result = [];

            foreach (PropertyInfo property in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!property.CanWrite)
                    continue;
                var dp = property.GetCustomAttribute<DependencyPropertyAttribute>();
                if (dp == null)
                    continue;
                string? validationCallback = dp.ValidationCallback,
                        updateCallback = dp.UpdateCallback;
                MethodInfo? validation = SearchMethod(property, validationCallback),
                            update = SearchMethod(property, updateCallback);
                bool affectsArrange = false,
                     affectsMeasure = false,
                     affectsParentMeasure = false,
                     defaultTwoWayBinding = true,
                     isAnimationProhibited = false,
                     isBindable = true;
                object? defaultPropertyValue = null;
                UpdateSourceTrigger defaultUpdateSourceTrigger = default;
                foreach (Attribute attribute in property.GetCustomAttributes())
                {
                    switch (attribute)
                    {
                        case AffectsArrangeAttribute:
                            affectsArrange = true; break;
                        case AffectsMeasureAttribute measure:
                            affectsMeasure = true;
                            affectsParentMeasure = measure.AffectsParentMeasure;
                            break;

                        case NonAnimatableAttribute:
                            isAnimationProhibited = true; break;
                        case NonBindableAttribute nonBindable:
                            isBindable = !nonBindable.AsTarget;
                            break;

                        case BindingOverrideAttribute bindingOverride:
                            defaultTwoWayBinding = bindingOverride.ModeOverride == Bindings.BindingMode.TwoWay;
                            break;

                        case DefaultValueAttribute defaultValue:
                            defaultPropertyValue = defaultValue.Value;
                            break;

                        case UpdateSourceTriggerOverrideAttribute triggerOverride:
                            defaultUpdateSourceTrigger = triggerOverride.UpdateSourceTrigger;
                            break;
                    }
                }

                var metadata = new DependencyPropertyMetadata(
                    defaultPropertyValue,
                    update?.CreateDelegate<PropertyChangedEventHandler>(),
                    affectsArrange,
                    affectsMeasure,
                    affectsParentMeasure,
                    defaultUpdateSourceTrigger,
                    defaultTwoWayBinding,
                    isAnimationProhibited,
                    false,
                    isBindable);

                result.Add(new DependencyProperty(
                    targetType,
                    property.Name,
                    property.PropertyType)
                {
                    Metadata = metadata,
                    ValidationCallback = validation?.CreateDelegate<ValidateValueCallback>(),
                });
            }

            return result;
        }

        private static MethodInfo? SearchMethod(PropertyInfo property, string? callbackName)
        {
            if (callbackName != null)
            {
                return property.DeclaringType?.GetMethod(callbackName, BindingFlags.Instance | BindingFlags.Public);
            }

            return null;
        }
    }
}