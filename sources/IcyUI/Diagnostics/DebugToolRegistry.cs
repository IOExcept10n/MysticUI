// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Diagnostics.Overlays;
using Icy.Diagnostics.Panels;

namespace Icy.Diagnostics
{
    /// <summary>
    /// The catalog of debug overlays and HUD panels a configuration knows about, keyed by the name markup/
    /// <see cref="UI.Canvas.ActiveDebugTools"/> activates them under.
    /// </summary>
    /// <remarks>
    /// Reached as <see cref="Configuration.ReflectionConfiguration.Diagnostics"/>, and configured fluently
    /// through <see cref="Configuration.BuildingExtensions.ConfigureDiagnostics"/>. This is the catalog of what
    /// tools <em>exist</em> - deliberately separate from <see cref="UI.Canvas.ActiveDebugTools"/>, which is the
    /// per-canvas runtime <em>selection</em> of which of them are currently on, so toggling tools on/off at
    /// runtime never touches the catalog.
    /// </remarks>
    public class DebugToolRegistry
    {
        private readonly Dictionary<string, IDebugOverlay> overlays = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IDebugHudPanel> panels = new(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugToolRegistry"/> class, seeded with the built-in
        /// "Bounds"/"Focus" overlays and the "DiagnosticsHud" panel.
        /// </summary>
        public DebugToolRegistry()
        {
            RegisterOverlay(new BoxModelOverlay());
            RegisterOverlay(new FocusHighlightOverlay());
            RegisterPanel(new DiagnosticsHudPanel());
        }

        /// <summary>
        /// Gets the registered overlays, keyed by <see cref="IDebugOverlay.Name"/>.
        /// </summary>
        public IReadOnlyDictionary<string, IDebugOverlay> Overlays => overlays;

        /// <summary>
        /// Gets the registered HUD panels, keyed by <see cref="IDebugHudPanel.Name"/>.
        /// </summary>
        public IReadOnlyDictionary<string, IDebugHudPanel> Panels => panels;

        /// <summary>
        /// Registers <typeparamref name="T"/> as a debug overlay, constructed with its parameterless constructor.
        /// </summary>
        /// <typeparam name="T">The overlay type to register.</typeparam>
        /// <returns>This registry, for chaining.</returns>
        public DebugToolRegistry RegisterOverlay<T>()
            where T : IDebugOverlay, new() => RegisterOverlay(new T());

        /// <summary>
        /// Registers <paramref name="overlay"/> as a debug overlay, under its own <see cref="IDebugOverlay.Name"/>.
        /// </summary>
        /// <param name="overlay">The overlay instance to register.</param>
        /// <returns>This registry, for chaining.</returns>
        /// <exception cref="DiagnosticsException">
        /// A different overlay type is already registered under <paramref name="overlay"/>'s name.
        /// </exception>
        public DebugToolRegistry RegisterOverlay(IDebugOverlay overlay)
        {
            ArgumentNullException.ThrowIfNull(overlay);

            if (overlays.TryGetValue(overlay.Name, out IDebugOverlay? existing) && existing.GetType() != overlay.GetType())
            {
                throw new DiagnosticsException(
                    $"Can't register '{overlay.GetType().FullName}' as '{overlay.Name}' because '{existing.GetType().FullName}' is already registered under that name.");
            }

            overlays[overlay.Name] = overlay;
            return this;
        }

        /// <summary>
        /// Registers <paramref name="type"/> as a debug overlay, constructed with its parameterless constructor.
        /// </summary>
        /// <param name="type">The overlay type to register. Must implement <see cref="IDebugOverlay"/>.</param>
        /// <returns>This registry, for chaining.</returns>
        /// <exception cref="DiagnosticsException"><paramref name="type"/> doesn't implement <see cref="IDebugOverlay"/>.</exception>
        public DebugToolRegistry RegisterOverlay(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (!typeof(IDebugOverlay).IsAssignableFrom(type))
                throw new DiagnosticsException($"'{type.FullName}' can't be registered as a debug overlay because it doesn't implement {nameof(IDebugOverlay)}.");

            return RegisterOverlay((IDebugOverlay)Activator.CreateInstance(type)!);
        }

        /// <summary>
        /// Registers <typeparamref name="T"/> as a HUD panel, constructed with its parameterless constructor.
        /// </summary>
        /// <typeparam name="T">The panel type to register.</typeparam>
        /// <returns>This registry, for chaining.</returns>
        public DebugToolRegistry RegisterPanel<T>()
            where T : IDebugHudPanel, new() => RegisterPanel(new T());

        /// <summary>
        /// Registers <paramref name="panel"/> as a HUD panel, under its own <see cref="IDebugHudPanel.Name"/>.
        /// </summary>
        /// <param name="panel">The panel instance to register.</param>
        /// <returns>This registry, for chaining.</returns>
        /// <exception cref="DiagnosticsException">
        /// A different panel type is already registered under <paramref name="panel"/>'s name.
        /// </exception>
        public DebugToolRegistry RegisterPanel(IDebugHudPanel panel)
        {
            ArgumentNullException.ThrowIfNull(panel);

            if (panels.TryGetValue(panel.Name, out IDebugHudPanel? existing) && existing.GetType() != panel.GetType())
            {
                throw new DiagnosticsException(
                    $"Can't register '{panel.GetType().FullName}' as '{panel.Name}' because '{existing.GetType().FullName}' is already registered under that name.");
            }

            panels[panel.Name] = panel;
            return this;
        }

        /// <summary>
        /// Registers <paramref name="type"/> as a HUD panel, constructed with its parameterless constructor.
        /// </summary>
        /// <param name="type">The panel type to register. Must implement <see cref="IDebugHudPanel"/>.</param>
        /// <returns>This registry, for chaining.</returns>
        /// <exception cref="DiagnosticsException"><paramref name="type"/> doesn't implement <see cref="IDebugHudPanel"/>.</exception>
        public DebugToolRegistry RegisterPanel(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (!typeof(IDebugHudPanel).IsAssignableFrom(type))
                throw new DiagnosticsException($"'{type.FullName}' can't be registered as a debug HUD panel because it doesn't implement {nameof(IDebugHudPanel)}.");

            return RegisterPanel((IDebugHudPanel)Activator.CreateInstance(type)!);
        }
    }
}
