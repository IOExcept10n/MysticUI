// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents a strong-typed reference to an attached markup property implemented by separate getter and setter methods.
    /// </summary>
    /// <typeparam name="TTarget">Target type that defined the property.</typeparam>
    /// <typeparam name="TValue">Property type to access values with.</typeparam>
    internal class AttachedPropertyReference<TTarget, TValue> : IPropertyReference<TValue>
    {
        private readonly Func<TTarget, TValue> getter;
        private readonly Action<TTarget, TValue> setter;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttachedPropertyReference{TTarget, TValue}"/> class.
        /// </summary>
        /// <param name="getter">The delegate that provide read access to the attached property value.</param>
        /// <param name="setter">The delegate that provide write access to the attached property value.</param>
        /// <param name="name">Name of the attached property.</param>
        /// <param name="category">The category of the property.</param>
        /// <param name="metadata">The metadata associated with the property.</param>
        /// <param name="validationCallback">An optional callback for validating property values.</param>
        /// <exception cref="ArgumentException">Thrown when the property does not have a public getter or setter.</exception>
        public AttachedPropertyReference(Func<TTarget, TValue> getter, Action<TTarget, TValue> setter, string name, string category, PropertyMetadata metadata, ValidateValueCallback? validationCallback)
        {
            Category = category;
            Name = name;
            Metadata = metadata;
            ValidationCallback = validationCallback;
            this.getter = getter;
            this.setter = setter;
        }

        /// <inheritdoc/>
        public string Category { get; }

        /// <inheritdoc/>
        public PropertyMetadata Metadata { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public Type OwnerType => typeof(TTarget);

        /// <inheritdoc/>
        public Type PropertyType => typeof(TValue);

        /// <inheritdoc/>
        public ValidateValueCallback? ValidationCallback { get; }

        /// <inheritdoc/>
        public object? GetRawValue(object target) => getter((TTarget)target);

        /// <inheritdoc/>
        public TValue GetValue(object target) => getter((TTarget)target);

        /// <inheritdoc/>
        public void SetRawValue(object target, object? value) => setter((TTarget)target, (TValue)(value ?? default(TValue))!);

        /// <inheritdoc/>
        public void SetValue(object target, TValue value) => setter((TTarget)target, value);
    }
}