// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;
using AquaUI.Data;
using AquaUI.Input.Devices;

namespace AquaUI.Input.Events
{
    /// <summary>
    /// Provides a default implementation for the <see cref="ITextEvents"/> interface.
    /// </summary>
    /// <remarks>
    /// Note that the text recognition is a platform-specific task so this simple wrapper will only detect characters depending on the input keys.
    /// </remarks>
    internal class TextEvents : ITextEvents
    {
        private bool isInputEnabled;
        private Keys lastKey;
        private TimeSpan delay;
        private bool repeat;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextEvents"/> class.
        /// </summary>
        /// <param name="inputSystem">An input system to handle all input events.</param>
        public TextEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
            if (inputSystem.Keyboard != null)
            {
                inputSystem.Keyboard.KeyDown += Keyboard_KeyDown;
                inputSystem.Keyboard.KeyUp += Keyboard_KeyUp;
            }

            inputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            inputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<ITextInputEventInfo>>? TextInput;

        /// <inheritdoc/>
        public event EventHandler? CopyText;

        /// <inheritdoc/>
        public event EventHandler? CutText;

        /// <inheritdoc/>
        public event EventHandler? PasteText;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public TimeSpan RepeatDelay { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <inheritdoc/>
        public TimeSpan RepeatStartDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void DisableTextInput()
        {
            isInputEnabled = false;
        }

        /// <inheritdoc/>
        public void EnableTextInput()
        {
            isInputEnabled = true;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            if (lastKey == Keys.None)
                return;
            delay += deltaTime;
            if (delay >= RepeatStartDelay || (repeat && delay >= RepeatDelay))
            {
                repeat = true;
                delay = default;
                Keyboard_KeyDown(this, lastKey);
            }
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            if (InputSystem.Keyboard != null)
            {
                InputSystem.Keyboard.KeyDown += Keyboard_KeyDown;
                InputSystem.Keyboard.KeyUp += Keyboard_KeyUp;
            }

            InputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            InputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        private static char? GetNumericShiftChar(Keys key, char? input) => key switch
        {
            Keys.D1 => '!',
            Keys.D2 => '@',
            Keys.D3 => '#',
            Keys.D4 => '$',
            Keys.D5 => '%',
            Keys.D6 => '^',
            Keys.D7 => '&',
            Keys.D8 => '*',
            Keys.D9 => '(',
            Keys.D0 => ')',
            _ => input,
        };

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            if (e is IKeyboardInput keyboard)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
                keyboard.KeyUp += Keyboard_KeyUp;
            }
        }

        private void Devices_DeviceDisconnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            if (e is IKeyboardInput keyboard)
            {
                keyboard.KeyDown -= Keyboard_KeyDown;
                keyboard.KeyUp -= Keyboard_KeyUp;
            }
        }

        private void Keyboard_KeyDown(object? sender, GenericEventArgs<Keys> e)
        {
            if (sender is not IKeyboardInput keyboard)
                return;

            bool isCtrlDown = (keyboard.ModifierKeys & ModifierKeys.Ctrl) != 0;
            if (isCtrlDown)
            {
                switch (e.Data)
                {
                    case Keys.X:
                        CutText?.Invoke(this, EventArgs.Empty); break;
                    case Keys.C:
                        CopyText?.Invoke(this, EventArgs.Empty); break;
                    case Keys.V:
                        PasteText?.Invoke(this, EventArgs.Empty); break;
                    default:
                        return;
                }
            }

            // Shift is possible modifier for input, but others stop text input.
            bool anyModifiers = (keyboard.ModifierKeys & ~ModifierKeys.Shift) != 0;
            if (!isInputEnabled || anyModifiers)
                return;

            if (e.Data != lastKey && !lastKey.IsModifier())
            {
                delay = default;
                repeat = false;
                lastKey = e.Data;
            }

            char? input = null;
            bool isShiftDown = (keyboard.ModifierKeys & ModifierKeys.Shift) != 0;
            switch (e.Data)
            {
                case >= Keys.A and <= Keys.Z:
                    // ASCII code for 'A' is 65. Key code for A is 44
                    input = (char)(e.Data + 21);

                    if (!isShiftDown)
                        input = char.ToLowerInvariant(input.Value);

                    break;

                case >= Keys.D0 and <= Keys.D9:
                    // ASCII code for '0' is 48. Key code for D0 is 34
                    if (!isShiftDown)
                    {
                        input = (char)(e.Data + 14);
                    }
                    else
                    {
                        input = GetNumericShiftChar(e.Data, input);
                    }

                    break;

                // NumPad numeric keys
                case >= Keys.NumPad0 and <= Keys.NumPad9:
                    // Key code for NumPad0 is 74.
                    input = (char)(e.Data - 26);
                    break;

                // NumPad keys
                case Keys.Decimal:
                    input = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                    break;

                case Keys.Divide:
                    input = '/'; break;
                case Keys.Add:
                    input = '+'; break;
                case Keys.Subtract:
                    input = '-'; break;
                case Keys.Multiply:
                    input = '*'; break;

                // Other punctuation characters
                case Keys.OemBackslash:
                    input = '\\'; break;
                case Keys.OemComma:
                    input = isShiftDown ? ',' : '<'; break;
                case Keys.OemOpenBrackets:
                    input = isShiftDown ? '{' : '['; break;
                case Keys.OemCloseBrackets:
                    input = isShiftDown ? '}' : ']'; break;
                case Keys.OemPeriod:
                    input = isShiftDown ? '.' : '>'; break;
                case Keys.OemPipe:
                    input = isShiftDown ? '|' : '\\'; break;
                case Keys.OemPlus:
                    input = isShiftDown ? '+' : '='; break;
                case Keys.OemMinus:
                    input = isShiftDown ? '_' : '-'; break;
                case Keys.OemQuestion:
                    input = isShiftDown ? '?' : '/'; break;
                case Keys.OemQuotes:
                    input = isShiftDown ? '"' : '\''; break;
                case Keys.OemSemicolon:
                    input = isShiftDown ? ':' : ';'; break;
                case Keys.OemTilde:
                    input = isShiftDown ? '~' : '`'; break;

                // Special keys
                case Keys.Space:
                    input = ' '; break;
                case Keys.Tab:
                    input = '\t'; break;
                case Keys.NumPadEnter:
                case Keys.Enter:
                    input = '\r'; break;
                case Keys.BackSpace:
                    input = '\b'; break;
            }

            if (input != null)
            {
                TextInput?.Invoke(this, new TextInputInfo(Range.All, input.Value.ToString(), TextInputEventType.Input));
            }
        }

        private void Keyboard_KeyUp(object? sender, GenericEventArgs<Keys> e)
        {
            delay = default;
            repeat = false;
            if (lastKey == e.Data)
            {
                lastKey = Keys.None;
            }
        }
    }
}