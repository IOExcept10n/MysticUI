// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Icy.UI;

namespace Icy.Diagnostics
{
    /// <summary>
    /// The <c>Debug.Visualization</c> attached property: a per-element override of which debug overlays
    /// (<see cref="IDebugOverlay"/>) apply to one element, independent of its <see cref="UI.Canvas.ActiveDebugTools"/>.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> (unset, the default) inherits the owning <see cref="UI.Canvas.ActiveDebugTools"/>
    /// selection; an empty string forces none, excluding one noisy element from an otherwise canvas-wide
    /// selection; a comma-separated list of overlay names (e.g. <c>"Bounds,Focus"</c>) forces exactly those,
    /// even while nothing is active canvas-wide - the "drill into one element" workflow.
    /// </remarks>
    [AttachedProperty(nameof(GetVisualization), nameof(SetVisualization), PropertyName = "Visualization")]
    public static class Debug
    {
        /// <summary>
        /// Gets the <c>Debug.Visualization</c> override for <paramref name="element"/>.
        /// </summary>
        /// <param name="element">The element to get the attached value for.</param>
        /// <returns>The override, or <see langword="null"/> if none was set.</returns>
        public static string? GetVisualization(UIElement element) =>
            AttachedProperties.GetValue<string?>(element, "Visualization", defaultValue: null);

        /// <summary>
        /// Sets the <c>Debug.Visualization</c> override for <paramref name="element"/>.
        /// </summary>
        /// <param name="element">The element to set the attached value for.</param>
        /// <param name="value">The override to assign - see this type's remarks for what each value means.</param>
        public static void SetVisualization(UIElement element, string? value) =>
            AttachedProperties.SetValue(element, "Visualization", value);
    }
}
