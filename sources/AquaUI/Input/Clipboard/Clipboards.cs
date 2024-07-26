using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AquaUI.Input.Clipboard
{
    /// <summary>
    /// Provides static methods for the clipboard access.
    /// </summary>
    public static class Clipboards
    {
        /// <summary>
        /// Gets an implementation of the current platform clipboard.
        /// </summary>
        /// <returns>An instance of the <see cref="IClipboard"/> interface for the current platform.</returns>
        /// <exception cref="PlatformNotSupportedException">Occurs when the current platform doesn't have the default clipboard implementation.</exception>
        public static IClipboard GetPlatformClipboard()
        {
            if (!TryGetPlatformClipboard(out var result))
                throw new PlatformNotSupportedException();
            return result;
        }

        /// <summary>
        /// Tries to get the current platform clipboard implementation.
        /// </summary>
        /// <param name="platformClipboard">
        /// An instance of the <see cref="IClipboard"/> interface for the current platform 
        /// or <see langword="null"/> if there are no clipboard implementations for the current platform.
        /// </param>
        /// <returns><see langword="true"/> if the clipboard for the current platform exists; otherwise <see langword="false"/>.</returns>
        public static bool TryGetPlatformClipboard([NotNullWhen(true)] out IClipboard? platformClipboard)
        {
            platformClipboard = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                platformClipboard = new WindowsClipboard();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platformClipboard = new LinuxClipboard();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platformClipboard = new OsxClipboard();
            }

            return platformClipboard != null;
        }

        /// <summary>
        /// Gets the default clipboard implementation.
        /// </summary>
        /// <returns>The platform clipboard implementation or <see cref="VirtualClipboard"/> if the platform clipboard is not supported</returns>
        public static IClipboard GetClipboard()
        {
            if (TryGetPlatformClipboard(out var result))
                return result;
            return new VirtualClipboard();
        }
    }
}
