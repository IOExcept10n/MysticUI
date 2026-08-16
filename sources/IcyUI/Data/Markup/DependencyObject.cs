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
        /// Initializes a new instance of the <see cref="DependencyObject"/> class.
        /// </summary>
        protected DependencyObject()
        {
            PropertyChanged += OnDependencyObjectPropertyChanged;
        }

        /// <summary>
        /// Gets the <see cref="IPropertyStore"/> registered for this instance's runtime type, resolving and
        /// registering it on first use.
        /// </summary>
        /// <returns>The property store for <see cref="object.GetType()"/>.</returns>
        public IPropertyStore GetPropertyStore() => PropertyRegistry.Instance.GetPropertyStore(GetType());

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
