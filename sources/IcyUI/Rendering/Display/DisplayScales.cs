// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Icy.Rendering.Display
{
    /// <summary>
    /// Provides static methods to get the display-scale provider for the current platform.
    /// </summary>
    public static class DisplayScales
    {
        /// <summary>
        /// Gets the display-scale provider for the current platform.
        /// </summary>
        /// <returns>An instance of the <see cref="IDisplayScaleProvider"/> interface for the current platform.</returns>
        /// <exception cref="PlatformNotSupportedException">The current platform has no display-scale provider.</exception>
        public static IDisplayScaleProvider GetPlatformProvider()
        {
            if (!TryGetPlatformProvider(out var provider))
                throw new PlatformNotSupportedException();
            return provider;
        }

        /// <summary>
        /// Tries to get the display-scale provider for the current platform.
        /// </summary>
        /// <param name="provider">The provider for the current platform, or <see langword="null"/> if there is none.</param>
        /// <returns><see langword="true"/> if the current platform has a provider; otherwise <see langword="false"/>.</returns>
        public static bool TryGetPlatformProvider([NotNullWhen(true)] out IDisplayScaleProvider? provider)
        {
            provider = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                provider = new WindowsDisplayScaleProvider();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                provider = new DrawableRatioDisplayScaleProvider();

            return provider != null;
        }

        /// <summary>
        /// Gets the display-scale provider for the current platform, or a <see cref="FixedDisplayScaleProvider"/>
        /// reporting <c>1.0</c> when the platform has none.
        /// </summary>
        /// <returns>A display-scale provider; never <see langword="null"/>.</returns>
        public static IDisplayScaleProvider GetProvider() =>
            TryGetPlatformProvider(out var provider) ? provider : new FixedDisplayScaleProvider();
    }
}
