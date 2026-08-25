// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Diagnostics.Panels.Sections;

namespace Icy.Diagnostics.Panels
{
    /// <summary>
    /// The built-in <c>"DiagnosticsHud"</c> panel: frame time/FPS, GC memory, and the currently focused element -
    /// an engine-agnostic replacement for the ad hoc MonoGame-only diagnostics overlay the samples used before.
    /// </summary>
    public class DiagnosticsHudPanel : IDebugHudPanel
    {
        /// <inheritdoc/>
        public string Name => "DiagnosticsHud";

        /// <inheritdoc/>
        public IReadOnlyList<IDebugHudSection> Sections { get; } =
        [
            new FrameStatsSection(),
            new MemoryStatsSection(),
            new FocusedElementSection(),
        ];
    }
}
