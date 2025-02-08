using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Icy.Input.Clipboard
{
    /// <summary>
    /// Represents a default implementation of the <see langword="Windows"/> clipboard.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal partial class WindowsClipboard : IClipboard
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

                nint handle, pointer = nint.Zero;
                try
                {
                    OpenClipboard();
                    handle = GetClipboardData(ClipboardFormatUnicodeText);
                    if (handle == nint.Zero) return null;
                    pointer = GlobalLock(handle);
                    if (pointer == nint.Zero) return null;
                    int size = GlobalSize(handle);
                    var buffer = new byte[size];
                    Marshal.Copy(pointer, buffer, 0, size);
                    return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
                }
                finally
                {
                    if (pointer != nint.Zero)
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
                nint memory = nint.Zero;
                try
                {
                    // Allocate Windows string for the clipboard.
                    int bytes = (value.Length + 1) * 2; // +1 for null terminator
                    memory = Marshal.AllocHGlobal(bytes);
                    if (memory == nint.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    // Prepare data for the clipboard.
                    try
                    {
                        // Convert the string to a byte array and copy it to the allocated memory
                        byte[] data = Encoding.Unicode.GetBytes(value + "\0"); // Add null terminator
                        Marshal.Copy(data, 0, memory, data.Length);
                    }
                    catch
                    {
                        // Ensure we unlock and free memory in case of an exception
                        GlobalUnlock(memory);
                        throw;
                    }

                    if (SetClipboardData(ClipboardFormatUnicodeText, memory) == nint.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    memory = nint.Zero; // Prevent freeing the handle
                }
                finally
                {
                    if (memory != nint.Zero)
                    {
                        Marshal.FreeHGlobal(memory);
                    }

                    CloseClipboard();
                }
            }
        }

        private static void OpenClipboard()
        {
            for (int i = 0; i < 10; i++)
            {
                if (OpenClipboard(nint.Zero)) return;
                Thread.Sleep(100);
            }

            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [LibraryImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsClipboardFormatAvailable(uint format);

        [LibraryImport("User32.dll", SetLastError = true)]
        private static partial nint GetClipboardData(uint uFormat);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial nint GlobalLock(nint hMem);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GlobalUnlock(nint hMem);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool OpenClipboard(nint hWndNewOwner);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseClipboard();

        [LibraryImport("user32.dll", SetLastError = true)]
        private static partial nint SetClipboardData(uint uFormat, nint data);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EmptyClipboard();

        [LibraryImport("Kernel32.dll", SetLastError = true)]
        private static partial int GlobalSize(nint hMem);
    }
}
