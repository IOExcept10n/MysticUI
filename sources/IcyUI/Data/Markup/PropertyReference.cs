using System.Reflection;
using CommunityToolkit.Diagnostics;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents a strong-typed reference to a public instance property of a class.
    /// </summary>
    /// <typeparam name="TTarget">Target type that contains the property.</typeparam>
    /// <typeparam name="TValue">Property type to access values with.</typeparam>
    internal class PropertyReference<TTarget, TValue> : PropertyReferenceBase<TTarget, TValue>
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
            if (!OwnerType.IsAssignableTo(typeof(TTarget)))
                ThrowHelper.ThrowArgumentException(nameof(property), $"Property owner type '{OwnerType}' must be assignable to '{typeof(TTarget)}'.");
            if (!PropertyType.IsAssignableTo(typeof(TValue)))
                ThrowHelper.ThrowArgumentException(nameof(property), $"Property type '{PropertyType}' must be assignable to '{typeof(TValue)}'.");
            Category = category;
            Metadata = metadata;
            ValidationCallback = validationCallback;
            getter = property.GetGetMethod(true)?.CreateDelegate<Func<TTarget, TValue>>() ?? ThrowHelper.ThrowArgumentException<Func<TTarget, TValue>>("Cannot make a reference to a property that doesn't have a getter.");
            setter = property.GetSetMethod(true)?.CreateDelegate<Action<TTarget, TValue>>() ?? ThrowHelper.ThrowArgumentException<Action<TTarget, TValue>>("Cannot make a reference to a property that doesn't have a setter.");
        }

        /// <inheritdoc/>
        public override string Category { get; }

        /// <inheritdoc/>
        public override PropertyMetadata Metadata { get; }

        /// <inheritdoc/>
        public override string Name => property.Name;

        /// <inheritdoc/>
        public override Type OwnerType => property.DeclaringType!;

        /// <inheritdoc/>
        public override Type PropertyType => property.PropertyType;

        /// <inheritdoc/>
        public override ValidateValueCallback? ValidationCallback { get; }

        /// <inheritdoc/>
        public override object? GetRawValue(object target) => property.GetValue(target);

        /// <inheritdoc/>
        public override TValue GetValue(object target) => getter((TTarget)target);

        /// <inheritdoc/>
        public override void SetRawValue(object target, object? value) => property.SetValue(target, value);

        /// <inheritdoc/>
        public override void SetValue(object target, TValue value) => setter((TTarget)target, value);
    }
}
