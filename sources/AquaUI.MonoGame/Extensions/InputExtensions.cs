// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Input.Devices;
using MKeys = Microsoft.Xna.Framework.Input.Keys;

namespace AquaUI.MonoGame.Extensions
{
    /// <summary>
    /// Provides some extensions to help with MonoGame-specific input support.
    /// </summary>
    public static class InputExtensions
    {
        private static readonly Dictionary<MKeys, Keys> toAquaKeys = new()
        {
            { MKeys.None, Keys.None },
            { MKeys.A, Keys.A },
            { MKeys.B, Keys.B },
            { MKeys.C, Keys.C },
            { MKeys.D, Keys.D },
            { MKeys.E, Keys.E },
            { MKeys.F, Keys.F },
            { MKeys.G, Keys.G },
            { MKeys.H, Keys.H },
            { MKeys.I, Keys.I },
            { MKeys.J, Keys.J },
            { MKeys.K, Keys.K },
            { MKeys.L, Keys.L },
            { MKeys.M, Keys.M },
            { MKeys.N, Keys.N },
            { MKeys.O, Keys.O },
            { MKeys.P, Keys.P },
            { MKeys.Q, Keys.Q },
            { MKeys.R, Keys.R },
            { MKeys.S, Keys.S },
            { MKeys.T, Keys.T },
            { MKeys.U, Keys.U },
            { MKeys.V, Keys.V },
            { MKeys.W, Keys.W },
            { MKeys.X, Keys.X },
            { MKeys.Y, Keys.Y },
            { MKeys.Z, Keys.Z },
            { MKeys.Back, Keys.Back },
            { MKeys.Tab, Keys.Tab },
            { MKeys.Enter, Keys.Enter },
            { MKeys.Escape, Keys.Escape },
            { MKeys.Space, Keys.Space },
            { MKeys.Left, Keys.Left },
            { MKeys.Right, Keys.Right },
            { MKeys.Up, Keys.Up },
            { MKeys.Down, Keys.Down },
            { MKeys.Delete, Keys.Delete },
            { MKeys.Pause, Keys.Pause },
            { MKeys.Home, Keys.Home },
            { MKeys.End, Keys.End },
            { MKeys.CapsLock, Keys.CapsLock },
            { MKeys.Kana, Keys.KanaMode },
            { MKeys.Add, Keys.Add },
            { MKeys.Apps, Keys.Apps },
            { MKeys.BrowserBack, Keys.BrowserBack },
            { MKeys.BrowserFavorites, Keys.BrowserFavorites },
            { MKeys.BrowserForward, Keys.BrowserForward },
            { MKeys.BrowserHome, Keys.BrowserHome },
            { MKeys.BrowserRefresh, Keys.BrowserRefresh },
            { MKeys.BrowserSearch, Keys.BrowserSearch },
            { MKeys.BrowserStop, Keys.BrowserStop },
            { MKeys.D0, Keys.D0 },
            { MKeys.D1, Keys.D1 },
            { MKeys.D2, Keys.D2 },
            { MKeys.D3, Keys.D3 },
            { MKeys.D4, Keys.D4 },
            { MKeys.D5, Keys.D5 },
            { MKeys.D6, Keys.D6 },
            { MKeys.D7, Keys.D7 },
            { MKeys.D8, Keys.D8 },
            { MKeys.D9, Keys.D9 },
            { MKeys.Decimal, Keys.Decimal },
            { MKeys.Divide, Keys.Divide },
            { MKeys.Execute, Keys.Execute },
            { MKeys.F1, Keys.F1 },
            { MKeys.F2, Keys.F2 },
            { MKeys.F3, Keys.F3 },
            { MKeys.F4, Keys.F4 },
            { MKeys.F5, Keys.F5 },
            { MKeys.F6, Keys.F6 },
            { MKeys.F7, Keys.F7 },
            { MKeys.F8, Keys.F8 },
            { MKeys.F9, Keys.F9 },
            { MKeys.F10, Keys.F10 },
            { MKeys.F11, Keys.F11 },
            { MKeys.F12, Keys.F12 },
            { MKeys.F13, Keys.F13 },
            { MKeys.F14, Keys.F14 },
            { MKeys.F15, Keys.F15 },
            { MKeys.F16, Keys.F16 },
            { MKeys.F17, Keys.F17 },
            { MKeys.F18, Keys.F18 },
            { MKeys.F19, Keys.F19 },
            { MKeys.F20, Keys.F20 },
            { MKeys.F21, Keys.F21 },
            { MKeys.F22, Keys.F22 },
            { MKeys.F23, Keys.F23 },
            { MKeys.F24, Keys.F24 },
            { MKeys.Help, Keys.Help },
            { MKeys.ImeConvert, Keys.ImeConvert },
            { MKeys.ImeNoConvert, Keys.ImeNonConvert },
            { MKeys.Insert, Keys.Insert },
            { MKeys.Kanji, Keys.KanjiMode },
            { MKeys.LaunchApplication1, Keys.LaunchApplication1 },
            { MKeys.LaunchApplication2, Keys.LaunchApplication2 },
            { MKeys.LaunchMail, Keys.LaunchMail },
            { MKeys.LeftAlt, Keys.LeftAlt },
            { MKeys.LeftControl, Keys.LeftCtrl },
            { MKeys.LeftShift, Keys.LeftShift },
            { MKeys.LeftWindows, Keys.LeftWin },
            { MKeys.MediaNextTrack, Keys.MediaNextTrack },
            { MKeys.MediaPlayPause, Keys.MediaPlayPause },
            { MKeys.MediaPreviousTrack, Keys.MediaPreviousTrack },
            { MKeys.MediaStop, Keys.MediaStop },
            { MKeys.Multiply, Keys.Multiply },
            { MKeys.NumLock, Keys.NumLock },
            { MKeys.NumPad0, Keys.NumPad0 },
            { MKeys.NumPad1, Keys.NumPad1 },
            { MKeys.NumPad2, Keys.NumPad2 },
            { MKeys.NumPad3, Keys.NumPad3 },
            { MKeys.NumPad4, Keys.NumPad4 },
            { MKeys.NumPad5, Keys.NumPad5 },
            { MKeys.NumPad6, Keys.NumPad6 },
            { MKeys.NumPad7, Keys.NumPad7 },
            { MKeys.NumPad8, Keys.NumPad8 },
            { MKeys.NumPad9, Keys.NumPad9 },
            { MKeys.Oem8, Keys.Oem8 },
            { MKeys.OemBackslash, Keys.OemBackslash },
            { MKeys.OemClear, Keys.OemClear },
            { MKeys.OemCloseBrackets, Keys.OemCloseBrackets },
            { MKeys.OemComma, Keys.OemComma },
            { MKeys.OemMinus, Keys.OemMinus },
            { MKeys.OemOpenBrackets, Keys.OemOpenBrackets },
            { MKeys.OemPeriod, Keys.OemPeriod },
            { MKeys.OemPipe, Keys.OemPipe },
            { MKeys.OemPlus, Keys.OemPlus },
            { MKeys.OemQuestion, Keys.OemQuestion },
            { MKeys.OemQuotes, Keys.OemQuotes },
            { MKeys.OemSemicolon, Keys.OemSemicolon },
            { MKeys.OemTilde, Keys.OemTilde },
            { MKeys.Pa1, Keys.Pa1 },
            { MKeys.PageDown, Keys.PageDown },
            { MKeys.PageUp, Keys.PageUp },
            { MKeys.Play, Keys.Play },
            { MKeys.PrintScreen, Keys.PrintScreen },
            { MKeys.Select, Keys.Select },
            { MKeys.Print, Keys.Print },
            { MKeys.RightWindows, Keys.RightWin },
            { MKeys.Sleep, Keys.Sleep },
            { MKeys.Separator, Keys.Separator },
            { MKeys.Subtract, Keys.Subtract },
            { MKeys.Scroll, Keys.Scroll },
            { MKeys.RightShift, Keys.RightShift },
            { MKeys.RightControl, Keys.RightCtrl },
            { MKeys.RightAlt, Keys.RightAlt },
            { MKeys.VolumeMute, Keys.VolumeMute },
            { MKeys.VolumeDown, Keys.VolumeDown },
            { MKeys.VolumeUp, Keys.VolumeUp },
            { MKeys.SelectMedia, Keys.SelectMedia }
        };

        private static readonly Dictionary<Keys, MKeys> toMonoGameKeys = [];

        static InputExtensions()
        {
            foreach (var pair in toAquaKeys)
            {
                toMonoGameKeys[pair.Value] = pair.Key;
            }
        }

        /// <summary>
        /// Maps <see cref="MKeys"/> value to <see cref="Keys"/> value.
        /// </summary>
        /// <param name="keys">Value to map.</param>
        /// <returns>A value of <see cref="Keys"/> enum according to the specified argument value.</returns>
        public static Keys RemapKeys(this MKeys keys) => toAquaKeys[keys];

        /// <summary>
        /// Maps <see cref="Keys"/> value to <see cref="MKeys"/> value.
        /// </summary>
        /// <param name="keys">Value to map.</param>
        /// <returns>A value of <see cref="MKeys"/> enum according to the specified argument value.</returns>
        public static MKeys RemapKeys(this Keys keys) => toMonoGameKeys[keys];
    }
}