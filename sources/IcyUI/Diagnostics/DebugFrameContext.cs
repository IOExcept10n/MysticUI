// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Diagnostics
{
    /// <summary>
    /// Per-frame information an <see cref="IDebugHudSection"/> needs to refresh itself, without reaching into
    /// <see cref="Canvas"/>'s own internals.
    /// </summary>
    /// <param name="Canvas">The canvas the HUD is rendering for.</param>
    /// <param name="Elapsed">The previous frame's duration.</param>
    public readonly record struct DebugFrameContext(Canvas Canvas, TimeSpan Elapsed);
}
