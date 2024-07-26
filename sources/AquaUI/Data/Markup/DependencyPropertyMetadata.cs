// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;

namespace AquaUI.Data.Markup
{
    /// <summary>
    /// Represents the metadata object for the <see cref="IDependencyProperty"/> instances.
    /// </summary>
    public class DependencyPropertyMetadata : PropertyMetadata
    {
        private readonly MetadataFlags flags;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyPropertyMetadata"/> class.
        /// </summary>
        /// <param name="defaultValue">The default value of the property.</param>
        /// <param name="updateCallback">The callback on the property value update.</param>
        /// <param name="affectsArrange">A value indicating whether the property value change affects the element arrange.</param>
        /// <param name="affectsMeasure">A value indicating whether the property value change affects the element measure.</param>
        /// <param name="affectsParentMeasure">A value indicating whether the property value change affects the parent element measure.</param>
        /// <param name="defaultUpdateSourceTrigger">A value that sets the default data binding <see cref="UpdateSourceTrigger"/> for this property.</param>
        /// <param name="defaultTwoWayBinding">A value indicating whether the property should enable two-way binding by default.</param>
        /// <param name="isAnimationProhibited">A value indicating whether the animation can't be applied for this property.</param>
        /// <param name="isAttached">A value indicating whether the property is attached property and doesn't exist in target type definition.</param>
        /// <param name="isBindable">A value indicating whether the property supports data bindings as target.</param>
        public DependencyPropertyMetadata(
            object? defaultValue,
            PropertyChangedEventHandler? updateCallback,
            bool affectsArrange,
            bool affectsMeasure,
            bool affectsParentMeasure,
            UpdateSourceTrigger defaultUpdateSourceTrigger,
            bool defaultTwoWayBinding,
            bool isAnimationProhibited,
            bool isAttached,
            bool isBindable)
            : base(defaultValue, updateCallback)
        {
            if (affectsArrange)
                flags |= MetadataFlags.AffectsArrange;
            if (affectsMeasure)
                flags |= MetadataFlags.AffectsMeasure;
            if (affectsParentMeasure)
                flags |= MetadataFlags.AffectsParentMeasure;
            DefaultUpdateSourceTrigger = defaultUpdateSourceTrigger;
            if (defaultTwoWayBinding)
                flags |= MetadataFlags.DefaultTwoWayBinding;
            if (isAnimationProhibited)
                flags |= MetadataFlags.IsAnimationProhibited;
            if (isAttached)
                flags |= MetadataFlags.IsAttached;
            if (isBindable)
                flags |= MetadataFlags.IsBindable;
        }

        [Flags]
        private enum MetadataFlags : byte
        {
            None = 0,
            AffectsArrange = 1 << 0,
            AffectsMeasure = 1 << 1,
            AffectsParentMeasure = 1 << 2,
            DefaultTwoWayBinding = 1 << 3,
            IsAnimationProhibited = 1 << 4,
            IsAttached = 1 << 5,
            IsBindable = 1 << 6,
        }

        /// <summary>
        /// Gets a value indicating whether the property value change affects the element arrange.
        /// </summary>
        public bool AffectsArrange => (flags & MetadataFlags.AffectsArrange) != 0;

        /// <summary>
        /// Gets a value indicating whether the property value change affects the element measure.
        /// </summary>
        public bool AffectsMeasure => (flags & MetadataFlags.AffectsMeasure) != 0;

        /// <summary>
        /// Gets a value indicating whether the property value change affects the parent element measure.
        /// </summary>
        public bool AffectsParentMeasure => (flags & MetadataFlags.AffectsParentMeasure) != 0;

        /// <summary>
        /// Gets a value indicating whether the property should enable two-way binding by default.
        /// </summary>
        public bool DefaultTwoWayBinding => (flags & MetadataFlags.DefaultTwoWayBinding) != 0;

        /// <summary>
        /// Gets a value that sets the default data binding <see cref="UpdateSourceTrigger"/> for this property.
        /// </summary>
        public UpdateSourceTrigger DefaultUpdateSourceTrigger { get; }

        /// <summary>
        /// Gets a value indicating whether the animation can't be applied for this property.
        /// </summary>
        public bool IsAnimationProhibited => (flags & MetadataFlags.IsAnimationProhibited) != 0;

        /// <summary>
        /// Gets a value indicating whether the property is attached property and doesn't exist in target type definition.
        /// </summary>
        public bool IsAttached => (flags & MetadataFlags.IsAttached) != 0;

        /// <summary>
        /// Gets a value indicating whether the property supports data bindings as target.
        /// </summary>
        public bool IsBindable => (flags & MetadataFlags.IsBindable) != 0;

        /// <summary>
        /// Creates a metadata for the attached property based on the defined metadata.
        /// </summary>
        /// <param name="prototype">An instance of the property metadata to make new one based on it.</param>
        /// <returns>New property metadata with the <see cref="IsAttached"/> property set to <see langword="true"/>.</returns>
        internal static DependencyPropertyMetadata CreateAttached(PropertyMetadata prototype)
        {
            if (prototype is DependencyPropertyMetadata dp)
            {
                return new(
                    dp.DefaultValue,
                    dp.PropertyChangedCallback,
                    dp.AffectsArrange,
                    dp.AffectsMeasure,
                    dp.AffectsParentMeasure,
                    dp.DefaultUpdateSourceTrigger,
                    dp.DefaultTwoWayBinding,
                    dp.IsAnimationProhibited,
                    true,
                    dp.IsBindable);
            }
            else
            {
                return new(
                    prototype.DefaultValue,
                    prototype.PropertyChangedCallback,
                    false,
                    false,
                    false,
                    UpdateSourceTrigger.PropertyChanged,
                    false,
                    false,
                    true,
                    false);
            }
        }
    }
}