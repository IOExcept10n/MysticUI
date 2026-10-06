// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Chooses the placement strategy for a container: the registration for the most derived of its types, or
    /// <see cref="MarginPlacement"/> when none matches.
    /// </summary>
    public sealed class PlacementRegistry
    {
        private readonly Dictionary<Type, IPlacementStrategy> strategies = [];
        private readonly MarginPlacement fallback = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PlacementRegistry"/> class with the built-in strategies:
        /// <see cref="StackPanelPlacement"/> for <see cref="UI.Controls.StackPanel"/> and <see cref="GridPlacement"/> for
        /// <see cref="UI.Controls.Grid"/>.
        /// </summary>
        public PlacementRegistry()
        {
            Register<UI.Controls.StackPanel>(new StackPanelPlacement());
            Register<UI.Controls.Grid>(new GridPlacement());
        }

        /// <summary>
        /// Registers the strategy for <typeparamref name="TContainer"/> and the containers derived from it, replacing any
        /// earlier registration for that type.
        /// </summary>
        /// <typeparam name="TContainer">The container type.</typeparam>
        /// <param name="strategy">The strategy.</param>
        public void Register<TContainer>(IPlacementStrategy strategy)
            where TContainer : UIElement => Register(typeof(TContainer), strategy);

        /// <summary>
        /// Registers the strategy for <paramref name="containerType"/> and the containers derived from it, replacing any
        /// earlier registration for that type.
        /// </summary>
        /// <param name="containerType">The container type; a <see cref="UIElement"/>.</param>
        /// <param name="strategy">The strategy.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="containerType"/> isn't a <see cref="UIElement"/>.</exception>
        public void Register(Type containerType, IPlacementStrategy strategy)
        {
            ArgumentNullException.ThrowIfNull(containerType);
            ArgumentNullException.ThrowIfNull(strategy);
            if (!typeof(UIElement).IsAssignableFrom(containerType))
                throw new ArgumentException($"'{containerType.Name}' isn't a UI element.", nameof(containerType));
            strategies[containerType] = strategy;
        }

        /// <summary>
        /// Gets the strategy for <paramref name="container"/>.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <returns>The registered strategy for its most derived registered type, or the margin fallback.</returns>
        public IPlacementStrategy Resolve(UIElement container)
        {
            ArgumentNullException.ThrowIfNull(container);
            for (Type? type = container.GetType(); type != null; type = type.BaseType)
            {
                if (strategies.TryGetValue(type, out IPlacementStrategy? strategy))
                    return strategy;
            }

            return fallback;
        }
    }
}
