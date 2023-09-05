using System.Globalization;
using System.Runtime.InteropServices;

namespace MysticUI.Extensions.Text
{
    /// <summary>
    /// Provides the utilities for the keyboard layout.
    /// </summary>
    public static class KeyboardLayout
    {
        /// <summary>
        /// Gets the culture info of the current enabled keyboard layout.
        /// </summary>
        public static CultureInfo GetCurrentKeyboardLayout()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CultureInfo.GetCultureInfo(WindowsKeyboard.GetKeyboardLayout());
            }
            return CultureInfo.InvariantCulture;
        }

        private class WindowsKeyboard
        {
            public static ushort GetKeyboardLayout()
            {
                return (ushort)GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
            }

            [DllImport("user32.dll")]
            private static extern IntPtr GetKeyboardLayout(int idThread);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern int GetWindowThreadProcessId([In] IntPtr hWnd, [Out, Optional] IntPtr lpdwProcessId);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr GetForegroundWindow();
        }
    }
}