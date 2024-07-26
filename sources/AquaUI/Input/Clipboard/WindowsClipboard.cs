using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace AquaUI.Input.Clipboard
{
    /// <summary>
    /// Represents a default implementation of the <see langword="Windows"/> clipboard.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class WindowsClipboard : IClipboard
    {
        private const int ClipboardFormatUnicodeText = 13;

        /// <inheritdoc/>
        [DisallowNull]
        public string? Text
        {
            get
            {
                if (!IsClipboardFormatAvailable(ClipboardFormatUnicodeText))
                {
                    return null;
                }

                IntPtr handle, pointer = IntPtr.Zero;
                try
                {
                    OpenClipboard();
                    handle = GetClipboardData(ClipboardFormatUnicodeText);
                    if (handle == IntPtr.Zero) return null;
                    pointer = GlobalLock(handle);
                    if (pointer == IntPtr.Zero) return null;
                    int size = GlobalSize(handle);
                    var buffer = new byte[size];
                    Marshal.Copy(pointer, buffer, 0, size);
                    return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
                }
                finally
                {
                    if (pointer != IntPtr.Zero)
                    {
                        GlobalUnlock(pointer);
                    }

                    CloseClipboard();
                }
            }

            set
            {
                OpenClipboard();
                EmptyClipboard();
                IntPtr hGlobal = IntPtr.Zero;
                try
                {
                    // allocate Windows string for the clipboard.
                    int bytes = (value.Length + 1) * 2;
                    hGlobal = Marshal.AllocHGlobal(bytes);
                    if (hGlobal == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    IntPtr target = GlobalLock(hGlobal);
                    if (target == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    // Prepare data for the clipboard.
                    try
                    {
                        Marshal.Copy(value.ToCharArray(), 0, target, value.Length);
                    }
                    finally
                    {
                        GlobalUnlock(target);
                    }

                    if (SetClipboardData(ClipboardFormatUnicodeText, hGlobal) == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    hGlobal = IntPtr.Zero;
                }
                finally
                {
                    if (hGlobal != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(hGlobal);
                    }

                    CloseClipboard();
                }
            }
        }

        private static void OpenClipboard()
        {
            for (int i = 0; i < 10; i++)
            {
                if (OpenClipboard(IntPtr.Zero)) return;
                Thread.Sleep(100);
            }

            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern int GlobalSize(IntPtr hMem);
    }
}
