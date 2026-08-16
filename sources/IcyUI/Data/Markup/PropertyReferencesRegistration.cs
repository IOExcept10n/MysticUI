// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Reflection;
using Icy.Data.Bindings.Attributes;
using Icy.Data.Markup.Attributes;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Helper class to scan types for the defined dependency properties as attributes.
    /// </summary>
    internal static class PropertyReferencesRegistration
    {
        /// <summary>
        /// Scans the type for the defined dependency properties and returns the list of found and formed properties (not yet registered).
        /// </summary>
        /// <param name="targetType">The type to scan.</param>
        /// <returns>The list of properties built by resolver.</returns>
        public static List<IPropertyReference> ResolveProperties(Type targetType)
        {
            List<IPropertyReference> result = [];

            foreach (PropertyInfo property in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!property.CanWrite)
                    continue;
                var dp = property.GetCustomAttribute<RegisterReferenceAttribute>();
                if (dp == null)
                    continue;
                string? validationCallback = dp.ValidationCallback;
                string? categoryName = null;
                MethodInfo? validation = SearchMethod(property, validationCallback);
                bool affectsArrange = false,
                     affectsTransform = false,
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
                        case AffectsTransformAttribute:
                            affectsTransform = true; break;

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
                        case CategoryAttribute category:
                            categoryName = category.Category;
                            break;
                    }
                }

                var metadata = new UIPropertyMetadata(
                    defaultPropertyValue,
                    affectsTransform,
                    affectsArrange,
                    affectsMeasure,
                    affectsParentMeasure,
                    defaultUpdateSourceTrigger,
                    defaultTwoWayBinding,
                    isAnimationProhibited,
                    false,
                    isBindable);

                result.Add((IPropertyReference)Activator.CreateInstance(
                    typeof(PropertyReference<,>).MakeGenericType(targetType, property.PropertyType),
                    [
                        property,
                        categoryName,
                        metadata,
                        validation?.CreateDelegate<ValidateValueCallback>()
                    ])!);
            }

            foreach (var attachedDefinition in targetType.GetCustomAttributes<AttachedPropertyAttribute>())
            {
                var getterMethod = targetType.GetMethod(attachedDefinition.GetterName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var setterMethod = targetType.GetMethod(attachedDefinition.SetterName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (getterMethod == null || setterMethod == null)
                    continue;
                var getParams = getterMethod.GetParameters();
                var setParams = setterMethod.GetParameters();

                // Check if the accessors are valid to simulate the property: Get(TTarget) : TValue, Set(TTarget, TValue) : void.
                if (getParams.Length != 1 ||
                    setParams.Length != 2 ||
                    getParams[0].ParameterType != setParams[0].ParameterType ||
                    getterMethod.ReturnType != setParams[1].ParameterType)
                    continue;

                Type attachedTargetType = getParams[0].ParameterType;
                Type valueType = getterMethod.ReturnType;
                string propertyName = attachedDefinition.PropertyName ?? DeriveAttachedName(attachedDefinition.GetterName);
                MethodInfo? attachedValidation = SearchMethod(targetType, attachedDefinition.ValidationCallback);
                var attachedMetadata = new UIPropertyMetadata(null, isAttached: true);

                Type referenceType = typeof(AttachedPropertyReference<,>).MakeGenericType(attachedTargetType, valueType);
                Type getterDelegateType = typeof(Func<,>).MakeGenericType(attachedTargetType, valueType);
                Type setterDelegateType = typeof(Action<,>).MakeGenericType(attachedTargetType, valueType);

                result.Add((IPropertyReference)Activator.CreateInstance(
                    referenceType,
                    [
                        getterMethod.CreateDelegate(getterDelegateType),
                        setterMethod.CreateDelegate(setterDelegateType),
                        propertyName,
                        null!,
                        attachedMetadata,
                        attachedValidation?.CreateDelegate<ValidateValueCallback>()
                    ])!);
            }

            return result;
        }

        private static MethodInfo? SearchMethod(PropertyInfo property, string? callbackName)
        {
            if (callbackName != null)
            {
                return property.DeclaringType?.GetMethod(callbackName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return null;
        }

        private static MethodInfo? SearchMethod(Type declaringType, string? callbackName)
        {
            if (callbackName != null)
            {
                return declaringType.GetMethod(callbackName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return null;
        }

        private static string DeriveAttachedName(string getterName)
        {
            return getterName.StartsWith("Get", StringComparison.Ordinal) && getterName.Length > 3
                ? getterName[3..]
                : getterName;
        }
    }
}