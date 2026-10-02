// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Display
{
    /// <summary>
    /// Tracks the display scale of a native window by polling an <see cref="IDisplayScaleProvider"/>.
    /// </summary>
    /// <remarks>
    /// Engine integrations create one per render context and call <see cref="Poll"/> once per update. Polling is a
    /// single cheap OS call and avoids competing with the engine for its window event queue.
    /// </remarks>
    public sealed class DisplayScaleTracker
    {
        private const float Epsilon = 0.001f;
        private readonly IDisplayScaleProvider provider;
        private readonly Func<NativeWindowInfo> windowSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayScaleTracker"/> class.
        /// </summary>
        /// <param name="provider">The provider used to read the scale, typically <see cref="DisplayScales.GetProvider"/>.</param>
        /// <param name="windowSource">A callback returning the current native window info. It must not throw.</param>
        public DisplayScaleTracker(IDisplayScaleProvider provider, Func<NativeWindowInfo> windowSource)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(windowSource);
            this.provider = provider;
            this.windowSource = windowSource;
        }

        /// <summary>
        /// Occurs when <see cref="Scale"/> changes during a <see cref="Poll"/> call.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets the display scale read by the last <see cref="Poll"/> call; <c>1.0</c> before the first poll.
        /// </summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>
        /// Reads the current display scale and raises <see cref="Changed"/> if it differs from <see cref="Scale"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the scale changed; otherwise <see langword="false"/>.</returns>
        /// <remarks>Values that aren't finite and greater than zero are treated as <c>1.0</c>.</remarks>
        public bool Poll()
        {
            float value = provider.GetScale(windowSource());
            if (!float.IsFinite(value) || value <= 0)
                value = 1f;

            if (MathF.Abs(value - Scale) < Epsilon)
                return false;

            Scale = value;
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
