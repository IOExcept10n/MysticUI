// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Bindings;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents an object that can store attached properties, and whose registered properties
    /// (see <see cref="Attributes.RegisterReferenceAttribute"/>) participate in the value-precedence
    /// system used by styles, visual states, and animations.
    /// </summary>
    public abstract partial class DependencyObject : BindableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyObject"/> class, binding it to the
        /// <see cref="Markup.PropertyRegistry.Current"/> registry for its lifetime.
        /// </summary>
        protected DependencyObject()
        {
            PropertyRegistry = Markup.PropertyRegistry.Current;
            PropertyChanged += OnDependencyObjectPropertyChanged;
        }

        /// <summary>
        /// Gets the <see cref="Markup.PropertyRegistry"/> this instance resolves its properties through, captured
        /// when it was constructed.
        /// </summary>
        /// <remarks>
        /// Captured once rather than read per access so the object keeps using one registry even if a different one
        /// later becomes <see cref="Markup.PropertyRegistry.Current"/> - an object whose property references came
        /// from two registries would have its value-precedence bookkeeping split between them. Use
        /// <see cref="Markup.PropertyRegistry.For(object)"/> to reach this from code holding an arbitrary target.
        /// </remarks>
        public PropertyRegistry PropertyRegistry { get; }

        /// <summary>
        /// Gets the <see cref="IPropertyStore"/> registered for this instance's runtime type, resolving and
        /// registering it on first use.
        /// </summary>
        /// <returns>The property store for <see cref="object.GetType()"/>.</returns>
        public IPropertyStore GetPropertyStore() => PropertyRegistry.GetPropertyStore(GetType());

        private void OnDependencyObjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // A direct assignment to a property (as opposed to a style/visual-state/animation tier
            // contribution) always becomes the new fallback value once every tier is cleared. Properties
            // that no precedence tier has ever touched are skipped at effectively no cost here.
            if (e.PropertyName is null)
                return;
            if (GetPropertyStore().TryGetProperty(e.PropertyName, out IPropertyReference? property))
                property.NotifyLocalValueChanged(this);
        }
    }
}
