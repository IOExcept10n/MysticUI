// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Animations
{
    /// <summary>
    /// Provides convenience extension methods for animating <see cref="UIElement"/> properties.
    /// </summary>
    public static class AnimationExtensions
    {
        /// <summary>
        /// Creates and starts an <see cref="Animations.Animation"/> that plays <paramref name="timeline"/> on <paramref name="element"/>.
        /// </summary>
        /// <param name="element">The element to animate.</param>
        /// <param name="timeline">The animation to play.</param>
        /// <returns>
        /// The started <see cref="Animations.Animation"/> - keep a reference to <see cref="Animations.Animation.Stop"/>
        /// it early or observe <see cref="Animations.Animation.Completed"/>.
        /// </returns>
        public static Animation Animate(this UIElement element, Timeline timeline)
        {
            var animation = new Animation(element, timeline);
            animation.Start();
            return animation;
        }
    }
}
