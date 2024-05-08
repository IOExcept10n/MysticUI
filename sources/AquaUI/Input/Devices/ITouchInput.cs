using AquaUI.Data;
using System.Drawing;
using System.Numerics;

namespace AquaUI.Input.Devices
{
    /// <summary>
    /// Represents the basic touch input interface with support for simple gestures.
    /// </summary>
    public interface ITouchInput
    {
        /// <summary>
        /// Occurs on every short-time tap.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>> Tap;

        /// <summary>
        /// Occurs on every long-time tap.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>> Hold;

        /// <summary>
        /// Occurs on a flick action.
        /// </summary>
        event EventHandler<GenericEventArgs<TranslationInfo>> Flick;

        /// <summary>
        /// Occurs on a drag action.
        /// </summary>
        event EventHandler<GenericEventArgs<TranslationInfo>> Drag;
    }

    /// <summary>
    /// Represents a struct for the gesture translation events.
    /// </summary>
    public readonly struct TranslationInfo
    {
        /// <summary>
        /// Gets the position of the start of gesture.
        /// </summary>
        public readonly Point TranslationStart;

        /// <summary>
        /// Gets the translation since the last frame.
        /// </summary>
        public readonly Vector2 DeltaTranslation;

        /// <summary>
        /// Gets the translation for the whole touch gesture.
        /// </summary>
        public readonly Vector2 TotalTranslation;

        /// <summary>
        /// Gets the value that determines whether the translation event has already been performed.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="true"/> if the action is ended; otherwise <see langword="false"/>.
        /// </remarks>
        public readonly bool IsPerformed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranslationInfo"/> struct.
        /// </summary>
        /// <param name="translationStart">Start point of the translation.</param>
        /// <param name="deltaTranslation">Translation delta since the last frame.</param>
        /// <param name="totalTranslation">Total translation during the action.</param>
        /// <param name="isPerformed">Value that determines whether the action has been ended.</param>
        public TranslationInfo(Point translationStart, Vector2 deltaTranslation, Vector2 totalTranslation, bool isPerformed)
        {
            TranslationStart = translationStart;
            DeltaTranslation = deltaTranslation;
            TotalTranslation = totalTranslation;
            IsPerformed = isPerformed;
        }
    }
}