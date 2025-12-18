using System.Reflection;
using CommunityToolkit.Diagnostics;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents a strong-typed reference to a public instance property of a class.
    /// </summary>
    /// <typeparam name="TTarget">Target type that contains the property.</typeparam>
    /// <typeparam name="TValue">Property type to access values with.</typeparam>
    internal class PropertyReference<TTarget, TValue> : IPropertyReference<TValue>
    {
        private readonly PropertyInfo property;
        private readonly Func<TTarget, TValue> getter;
        private readonly Action<TTarget, TValue> setter;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyReference{TTarget, TValue}"/> class.
        /// </summary>
        /// <param name="property">The <see cref="PropertyInfo"/> representing the property to reference.</param>
        /// <param name="category">The category of the property.</param>
        /// <param name="metadata">The metadata associated with the property.</param>
        /// <param name="validationCallback">An optional callback for validating property values.</param>
        /// <exception cref="ArgumentException">Thrown when the property does not have a public getter or setter.</exception>
        public PropertyReference(PropertyInfo property, string category, PropertyMetadata metadata, ValidateValueCallback? validationCallback)
        {
            this.property = property;
            Guard.IsAssignableToType(OwnerType, typeof(TTarget));
            Guard.IsAssignableToType(PropertyType, typeof(TValue));
            Category = category;
            Metadata = metadata;
            ValidationCallback = validationCallback;
            getter = property.GetGetMethod()?.CreateDelegate<Func<TTarget, TValue>>() ?? ThrowHelper.ThrowArgumentException<Func<TTarget, TValue>>("Cannot make a public reference to non-public property.");
            setter = property.GetSetMethod()?.CreateDelegate<Action<TTarget, TValue>>() ?? ThrowHelper.ThrowArgumentException<Action<TTarget, TValue>>("Cannot make a reference with public setter to a property that doesn't have it.");
        }

        /// <inheritdoc/>
        public string Category { get; }

        /// <inheritdoc/>
        public PropertyMetadata Metadata { get; }

        /// <inheritdoc/>
        public string Name => property.Name;

        /// <inheritdoc/>
        public Type OwnerType => property.DeclaringType!;

        /// <inheritdoc/>
        public Type PropertyType => property.PropertyType;

        /// <inheritdoc/>
        public ValidateValueCallback? ValidationCallback { get; }

        /// <inheritdoc/>
        public object? GetRawValue(object target) => property.GetValue(target);

        /// <inheritdoc/>
        public TValue GetValue(object target) => getter((TTarget)target);

        /// <inheritdoc/>
        public void SetRawValue(object target, object? value) => property.SetValue(target, value);

        /// <inheritdoc/>
        public void SetValue(object target, TValue value) => setter((TTarget)target, value);
    }
}
