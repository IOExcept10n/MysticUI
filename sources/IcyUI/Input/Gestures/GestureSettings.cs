// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Gestures
{
    /// <summary>
    /// Holds the thresholds of gesture recognition (see <see cref="IGestureEvents"/>).
    /// </summary>
    public class GestureSettings
    {
        /// <summary>
        /// Gets or sets how long a touch or left-mouse press must stay inside <see cref="Slop"/> to raise
        /// <see cref="IGestureEvents.Held"/>. Defaults to 0.5 seconds.
        /// </summary>
        public TimeSpan HoldDelay { get; set; } = TimeSpan.FromSeconds(0.5);

        /// <summary>
        /// Gets or sets the longest gap between two taps that still continues a multi-tap sequence
        /// (see <see cref="TapInfo.Count"/>). Defaults to 0.3 seconds.
        /// </summary>
        public TimeSpan MultiTapDelay { get; set; } = TimeSpan.FromSeconds(0.3);

        /// <summary>
        /// Gets or sets how far a pointer may move, in device-independent pixels, before a press becomes a drag.
        /// Defaults to <c>10</c>.
        /// </summary>
        /// <remarks>
        /// Converted to physical pixels through <see cref="DisplayScaleSource"/>, so a finger may wobble the same physical
        /// distance at every display scale. The UI scale (<see cref="UI.Canvas.EffectiveScale"/>) deliberately doesn't apply.
        /// </remarks>
        public float Slop { get; set; } = 10f;

        /// <summary>
        /// Gets or sets the source of the display scale used to convert <see cref="Slop"/> to physical pixels.
        /// </summary>
        /// <remarks>
        /// <see cref="Configuration.IcyConfiguration"/> wires it to <see cref="Rendering.IRenderContext.DisplayScale"/>.
        /// Defaults to a constant <c>1</c>. Non-finite or non-positive results are treated as <c>1</c>.
        /// </remarks>
        public Func<float> DisplayScaleSource { get; set; } = () => 1f;
    }
}
