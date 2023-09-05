using Stride.Core.Mathematics;
using Stride.Input;

namespace MysticUI
{
    /// <summary>
    /// An interface for input provider for the current UI environment.
    /// </summary>
    public interface IInputManager
    {
        /// <summary>
        /// An enumerable of all down keys.
        /// </summary>
        public IEnumerable<Keys> DownKeys { get; }

        /// <summary>
        /// Current mouse state.
        /// </summary>
        public MouseInfo Mouse { get; }

        /// <summary>
        /// Bounds of the game screen.
        /// </summary>
        public Rectangle FrameBounds { get; }

        /// <summary>
        /// All text input events since the last frame.
        /// </summary>
        public IEnumerable<TextInputEvent> TextInput { get; }

        /// <summary>
        /// Disables the text input handling.
        /// </summary>
        void DisableTextInput();

        /// <summary>
        /// Enables the text input handling.
        /// </summary>
        void EnableTextInput();

        /// <summary>
        /// Determines whether the given key is down.
        /// </summary>
        /// <param name="key">A key to check.</param>
        /// <returns><see langword="true"/> if the key is pressed down during the frame, <see langword="false"/> otherwise.</returns>
        public bool IsKeyDown(Keys key);

        /// <summary>
        /// Determines whether the given mouse button is down during the frame.
        /// </summary>
        /// <param name="button">A button to check.</param>
        /// <returns><see langword="true"/> if the button is down, <see langword="false"/> otherwise.</returns>
        public bool IsMouseButtonDown(MouseButton button);
    }
}