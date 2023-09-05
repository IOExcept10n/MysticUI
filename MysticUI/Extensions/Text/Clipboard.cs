// All code here was taken from the Myra GitHub repo: https://github.com/rds1983/Myra/tree/master/src/Myra/TextCopy
using CommunityToolkit.Diagnostics;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MysticUI.Extensions.Text
{
    /// <summary>
    /// Provides cross-platform clipboard API for the UI library.
    /// </summary>
    public static class Clipboard
    {
        /// <summary>
        /// Determines whether to use the local clipboard instead of platform-dependent clipboard implementation.
        /// </summary>
        public static bool UseLocalClipboard { get; set; }

        private static readonly Action<string> ClipboardSet = GetClipboardSetAction();
        private static readonly Func<string?> ClipboardGet = GetClipboardGetFunc();

        /// <summary>
        /// Sets the clipboard value.
        /// </summary>
        /// <param name="value">Value to set into the clipboard.</param>
        public static void SetText(string value)
        {
            Guard.IsNotNull(value);
            if (UseLocalClipboard)
            {
                EnvironmentSettingsProvider.EnvironmentSettings.InternalClipboard = value;
            }
            else
            {
                ClipboardSet(value);
            }
        }

        /// <summary>
        /// Gets the clipboard value.
        /// </summary>
        /// <returns>Value stored in the clipboard.</returns>
        public static string? GetText()
        {
            if (UseLocalClipboard)
            {
                return EnvironmentSettingsProvider.EnvironmentSettings.InternalClipboard;
            }
            return ClipboardGet();
        }

        private static Action<string> GetClipboardSetAction()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return WindowsClipboard.SetText;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return LinuxClipboard.SetText;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OsxClipboard.SetText;
            }
            return s => throw new PlatformNotSupportedException();
        }

        private static Func<string?> GetClipboardGetFunc()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return WindowsClipboard.GetText;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return LinuxClipboard.GetText;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OsxClipboard.GetText;
            }
            return () => throw new PlatformNotSupportedException();
        }

        private static class WindowsClipboard
        {
            private const int ClipboardFormatUnicodeText = 13;

            public static void SetText(string text)
            {
                OpenClipboard();
                EmptyClipboard();
                IntPtr hGlobal = IntPtr.Zero;
                try
                {
                    // allocate Windows string for the clipboard.
                    int bytes = (text.Length + 1) * 2;
                    hGlobal = Marshal.AllocHGlobal(bytes);
                    if (hGlobal == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    IntPtr target = GlobalLock(hGlobal);
                    if (target == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    // Prepare data for the clipboard.
                    try
                    {
                        Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
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

            public static string? GetText()
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

        private static class OsxClipboard
        {
            private static readonly IntPtr nsString = objc_getClass("NSString");
            private static readonly IntPtr nsPasteboard = objc_getClass("NSPasteboard");
            private static readonly IntPtr nsStringPboardType;
            private static readonly IntPtr utfTextType;
            private static readonly IntPtr generalPasteboard;

            static OsxClipboard()
            {
                utfTextType = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), "public.utf8-plain-text");
                nsStringPboardType = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), "NSStringPboardType");

                generalPasteboard = objc_msgSend(nsPasteboard, sel_registerName("generalPasteboard"));
            }

            public static string? GetText()
            {
                var ptr = objc_msgSend(generalPasteboard, sel_registerName("stringForType:"), nsStringPboardType);
                var charArray = objc_msgSend(ptr, sel_registerName("UTF8String"));
                return Marshal.PtrToStringAnsi(charArray);
            }

            public static void SetText(string text)
            {
                IntPtr str = IntPtr.Zero;
                try
                {
                    str = objc_msgSend(objc_msgSend(nsString, sel_registerName("alloc")), sel_registerName("initWithUTF8String:"), text);
                    objc_msgSend(generalPasteboard, sel_registerName("clearContents"));
                    objc_msgSend(generalPasteboard, sel_registerName("setString:forType:"), str, utfTextType);
                }
                finally
                {
                    if (str != IntPtr.Zero)
                    {
                        objc_msgSend(str, sel_registerName("release"));
                    }
                }
            }

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", CharSet = CharSet.Unicode)]
            private static extern IntPtr objc_getClass(string className);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", CharSet = CharSet.Unicode)]
            private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
            private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

            [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", CharSet = CharSet.Unicode)]
            private static extern IntPtr sel_registerName(string selectorName);
        }

        private static class LinuxClipboard
        {
            public static void SetText(string text)
            {
                var tempFileName = Path.GetTempFileName();
                File.WriteAllText(tempFileName, text);
                try
                {
                    BashRunner.Run($"cat {tempFileName} | xclip");
                }
                finally
                {
                    File.Delete(tempFileName);
                }
            }

            public static string? GetText()
            {
                var tempFileName = Path.GetTempFileName();
                try
                {
                    BashRunner.Run($"xclip -o > {tempFileName}");
                    return File.ReadAllText(tempFileName);
                }
                finally
                {
                    File.Delete(tempFileName);
                }
            }

            private static class BashRunner
            {
                public static string Run(string commandLine)
                {
                    var errorBuilder = new StringBuilder();
                    var outputBuilder = new StringBuilder();
                    var arguments = $"-c \"{commandLine}\"";
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "bash",
                            Arguments = arguments,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = false,
                        }
                    };
                    process.Start();
                    process.OutputDataReceived += (sender, args) => { outputBuilder.AppendLine(args.Data); };
                    process.BeginOutputReadLine();
                    process.ErrorDataReceived += (sender, args) => { errorBuilder.AppendLine(args.Data); };
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(500))
                    {
                        var timeoutError = $@"Process timed out. Command line: bash {arguments}.
Output: {outputBuilder}
Error: {errorBuilder}";
                        throw new Exception(timeoutError);
                    }
                    if (process.ExitCode == 0)
                    {
                        return outputBuilder.ToString();
                    }

                    var error = $@"Could not execute process. Command line: bash {arguments}.
Output: {outputBuilder}
Error: {errorBuilder}";
                    throw new Exception(error);
                }
            }
        }
    }
}