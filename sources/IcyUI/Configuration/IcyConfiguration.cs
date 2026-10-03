// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Input;
using Icy.Rendering;
using Icy.Rendering.Fonts;

namespace Icy.Configuration
{
    /// <summary>
    /// Aggregates library services and provides fluent configuration for all of these.
    /// </summary>
    public class IcyConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IcyConfiguration"/> class.
        /// </summary>
        /// <param name="input">An input service that is used by library in this application.</param>
        /// <param name="assets">Configuration of the assets system used by library in this application.</param>
        /// <param name="renderContext">The rendering service instance used by library in this application.</param>
        /// <param name="types">An instance of the reflection-related services used in library.</param>
        /// <param name="theme">
        /// The default-theme configuration used by the library. Defaults to an empty <see cref="ThemeConfiguration"/>
        /// (no theme applied) when omitted.
        /// </param>
        /// <param name="scaling">
        /// The UI scaling defaults used by the library. Defaults to a new <see cref="ScalingConfiguration"/>
        /// (<see cref="UI.UIScaleMode.Dpi"/>) when omitted.
        /// </param>
        public IcyConfiguration(IInputSystem input, AssetConfiguration assets, IRenderContext renderContext, ReflectionConfiguration types, ThemeConfiguration? theme = null, ScalingConfiguration? scaling = null)
        {
            Input = input;

            // Gesture slop is measured in DIPs: it follows the OS display scale, never the UI scale.
            input.Events.Gestures.Settings.DisplayScaleSource = () => renderContext.DisplayScale;
            Assets = assets;
            RenderContext = renderContext;
            Types = types;
            Theme = theme ?? new();
            Scaling = scaling ?? new();
            Fonts = new(this);
        }

        /// <summary>
        /// Gets a service for access to the library fonts.
        /// </summary>
        public FontSystem Fonts { get; }

        /// <summary>
        /// Gets the input service instance used by the library.
        /// </summary>
        public IInputSystem Input { get; }

        /// <summary>
        /// Gets the assets configuration section.
        /// </summary>
        public AssetConfiguration Assets { get; }

        /// <summary>
        /// Gets the rendering service instance used by the library.
        /// </summary>
        public IRenderContext RenderContext { get; }

        /// <summary>
        /// Gets the services for the reflection purposes.
        /// </summary>
        public ReflectionConfiguration Types { get; }

        /// <summary>
        /// Gets the default-theme configuration used by the library.
        /// </summary>
        public ThemeConfiguration Theme { get; }

        /// <summary>
        /// Gets the application-wide UI scaling defaults used by every <see cref="UI.Canvas"/> built against this configuration.
        /// </summary>
        public ScalingConfiguration Scaling { get; }
    }
}