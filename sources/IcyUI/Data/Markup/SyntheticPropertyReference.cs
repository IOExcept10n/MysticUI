// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents a property reference that has no backing CLR member at all — its value simply lives in
    /// <see cref="AttachedProperties"/> storage, keyed by a name unique to the registering store.
    /// </summary>
    /// <remarks>
    /// Used for properties registered programmatically through <see cref="IPropertyStore.RegisterProperty(string, Type, PropertyMetadata, ValidateValueCallback?)"/>
    /// or <see cref="IPropertyStore.RegisterAttached(string, Type, PropertyMetadata, ValidateValueCallback?)"/>
    /// rather than discovered by scanning a real CLR property or a pair of getter/setter methods.
    /// </remarks>
    /// <typeparam name="TValue">Property type to access values with.</typeparam>
    internal sealed class SyntheticPropertyReference<TValue> : PropertyReferenceBase<object, TValue>
    {
        private readonly string storageKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyntheticPropertyReference{TValue}"/> class.
        /// </summary>
        /// <param name="storageKey">A key unique to the registering store, used to namespace the underlying storage.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="category">The category of the property.</param>
        /// <param name="metadata">The metadata associated with the property.</param>
        /// <param name="validationCallback">An optional callback for validating property values.</param>
        public SyntheticPropertyReference(string storageKey, string name, string category, PropertyMetadata metadata, ValidateValueCallback? validationCallback)
        {
            this.storageKey = storageKey;
            Name = name;
            Category = category;
            Metadata = metadata;
            ValidationCallback = validationCallback;
        }

        /// <inheritdoc/>
        public override string Category { get; }

        /// <inheritdoc/>
        public override PropertyMetadata Metadata { get; }

        /// <inheritdoc/>
        public override string Name { get; }

        /// <inheritdoc/>
        public override Type OwnerType => typeof(object);

        /// <inheritdoc/>
        public override Type PropertyType => typeof(TValue);

        /// <inheritdoc/>
        public override ValidateValueCallback? ValidationCallback { get; }

        /// <inheritdoc/>
        public override object? GetRawValue(object target) => AttachedProperties.GetValue<object?>(target, storageKey, Metadata.DefaultValue);

        /// <inheritdoc/>
        public override TValue GetValue(object target) => AttachedProperties.GetValue(target, storageKey, (TValue)(Metadata.DefaultValue ?? default(TValue))!);

        /// <inheritdoc/>
        public override void SetRawValue(object target, object? value) => AttachedProperties.SetValue(target, storageKey, value);

        /// <inheritdoc/>
        public override void SetValue(object target, TValue value) => AttachedProperties.SetValue(target, storageKey, (object?)value);
    }
}
