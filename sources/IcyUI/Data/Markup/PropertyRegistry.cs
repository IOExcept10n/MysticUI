// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Stores the <see cref="IPropertyStore"/> for every type whose registered properties have been resolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A registry is an ordinary instance, not a singleton: it is a service on
    /// <see cref="Configuration.ReflectionConfiguration"/> alongside <see cref="IAssemblyResolver"/> and
    /// <see cref="ITypeConverter"/>, so a configuration can be isolated from the rest of the process (tests being
    /// the common case). <see cref="Default"/> is the shared registry everything uses unless told otherwise.
    /// </para>
    /// <para>
    /// <b>All access for a given object must go through one registry.</b> Two registries resolve the same property
    /// to two different <see cref="IPropertyReference"/> instances, and each reference keeps its own
    /// value-precedence bookkeeping - so mixing them would split an object's Local/Style/VisualState/Animation
    /// contributions across two tables and compute the wrong effective value. To make that hard to get wrong,
    /// every <see cref="DependencyObject"/> captures its registry once at construction
    /// (<see cref="DependencyObject.PropertyRegistry"/>), and <see cref="For(object)"/> is the way to reach the
    /// right registry for an arbitrary target.
    /// </para>
    /// </remarks>
    public class PropertyRegistry : KeyedCollection<Type, IPropertyStore>
    {
        private static readonly AsyncLocal<PropertyRegistry?> CurrentValue = new();

        private readonly object gate = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyRegistry"/> class with no resolved types.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="Default"/> unless isolation is actually needed - a fresh registry re-scans every type
        /// it is asked about, and objects must not be shared across registries (see the remarks on
        /// <see cref="PropertyRegistry"/>).
        /// </remarks>
        public PropertyRegistry()
        {
        }

        /// <summary>
        /// Gets the process-wide default registry, used whenever no other registry is in scope.
        /// </summary>
        public static PropertyRegistry Default { get; } = [];

        /// <summary>
        /// Gets the registry currently in scope - the one set by the innermost active <see cref="UseScope"/>, or
        /// <see cref="Default"/> when none is active.
        /// </summary>
        /// <remarks>
        /// This is what a newly constructed <see cref="DependencyObject"/> captures, and what resolution falls back
        /// to when there is no object to ask.
        /// </remarks>
        public static PropertyRegistry Current => CurrentValue.Value ?? Default;

        /// <summary>
        /// Makes <paramref name="registry"/> the <see cref="Current"/> registry until the returned scope is disposed.
        /// </summary>
        /// <param name="registry">The registry to use for the duration of the scope.</param>
        /// <returns>A scope that restores the previous <see cref="Current"/> registry when disposed.</returns>
        /// <remarks>
        /// Scopes flow with the async context and nest. Objects constructed inside a scope keep that scope's registry
        /// for their whole lifetime, so they stay consistent after it ends.
        /// </remarks>
        public static IDisposable UseScope(PropertyRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            var scope = new RegistryScope(CurrentValue.Value);
            CurrentValue.Value = registry;
            return scope;
        }

        /// <summary>
        /// Gets the registry that owns <paramref name="target"/>'s property references.
        /// </summary>
        /// <param name="target">The object whose registry is needed.</param>
        /// <returns>
        /// <paramref name="target"/>'s own registry when it is a <see cref="DependencyObject"/>; otherwise
        /// <see cref="Current"/>.
        /// </returns>
        /// <remarks>
        /// Use this instead of <see cref="Current"/> whenever a target object is available - it is what keeps an
        /// object's property access on the single registry it was built against (see the remarks on
        /// <see cref="PropertyRegistry"/>).
        /// </remarks>
        public static PropertyRegistry For(object target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return target is DependencyObject dependencyObject ? dependencyObject.PropertyRegistry : Current;
        }

        /// <summary>
        /// Registers a property store for a specific type.
        /// </summary>
        /// <param name="propertyStore">The property store to register.</param>
        public void RegisterPropertyStore(IPropertyStore propertyStore)
        {
            ArgumentNullException.ThrowIfNull(propertyStore);
            lock (gate)
            {
                Add(propertyStore);
            }
        }

        /// <summary>
        /// Retrieves the property store for a specific type, creating and registering the default reflection-based
        /// store for it on first request if none has been registered yet.
        /// </summary>
        /// <param name="type">The type for which to retrieve the property store.</param>
        /// <returns>The property store associated with the specified type.</returns>
        /// <remarks>
        /// Call <see cref="RegisterPropertyStore(IPropertyStore)"/> ahead of time for a type if it needs a custom
        /// <see cref="IPropertyStore"/> implementation instead of the default reflection-based one.
        /// </remarks>
        public IPropertyStore GetPropertyStore(Type type)
        {
            lock (gate)
            {
                if (!Contains(type))
                {
                    var store = (IPropertyStore)Activator.CreateInstance(typeof(PropertyStore<>).MakeGenericType(type), [this])!;
                    Add(store);
                }

                return this[type];
            }
        }

        /// <inheritdoc/>
        protected override Type GetKeyForItem(IPropertyStore item) => item.TargetType;

        private sealed class RegistryScope(PropertyRegistry? previous) : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                CurrentValue.Value = previous;
            }
        }
    }
}
