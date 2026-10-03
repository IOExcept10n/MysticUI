// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents an object advanced once per frame by <see cref="Dispatcher.UpdateAnimations(TimeSpan)"/>, after the
    /// running animations - for motion that applies relative steps instead of writing an animated property value.
    /// </summary>
    internal interface IFrameTicker
    {
        /// <summary>
        /// Advances the ticker by one frame.
        /// </summary>
        /// <param name="delta">The time elapsed since the previous frame.</param>
        void Tick(TimeSpan delta);
    }
}
