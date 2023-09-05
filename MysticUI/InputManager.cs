using Stride.Core.Mathematics;
using Stride.Input;

namespace MysticUI
{
    /// <summary>
    /// The default implementation of the input manager for the UI system.
    /// </summary>
    public class InputManager : IInputManager
    {
        private readonly Stride.Input.InputManager Input = EnvironmentSettingsProvider.EnvironmentSettings.Game.Services.GetService<Stride.Input.InputManager>();

        /// <inheritdoc/>
        public IEnumerable<Keys> DownKeys => Input.DownKeys;

        /// <inheritdoc/>
        public MouseInfo Mouse => new((Point)Input.AbsoluteMousePosition,
                                      Input.Mouse.DownButtons.Aggregate(MouseButtonFlags.None, (x, y) => x |= (MouseButtonFlags)(1 << (int)y)),
                                      Input.MouseWheelDelta);

        /// <inheritdoc/>
        public Rectangle FrameBounds => new(0, 0,
            EnvironmentSettingsProvider.EnvironmentSettings.GraphicsDevice.Presenter.BackBuffer.Width,
            EnvironmentSettingsProvider.EnvironmentSettings.GraphicsDevice.Presenter.BackBuffer.Height);

        /// <inheritdoc/>
        public IEnumerable<TextInputEvent> TextInput => Input.Events.OfType<TextInputEvent>();

        /// <summary>
        /// Enables the text input processing for the input manager.
        /// </summary>
        public void EnableTextInput()
        {
            Input.TextInput.EnabledTextInput();
        }

        /// <summary>
        /// Disables the text input processing for the input manager.
        /// </summary>
        public void DisableTextInput()
        {
            Input.TextInput.DisableTextInput();
        }

        /// <inheritdoc/>
        public bool IsKeyDown(Keys key) => Input.IsKeyDown(key);

        /// <inheritdoc/>
        public bool IsMouseButtonDown(MouseButton button) => Input.IsMouseButtonDown(button);
    }
}