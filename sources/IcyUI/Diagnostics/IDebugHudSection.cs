// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Diagnostics
{
    /// <summary>
    /// A reusable, self-contained fragment of an <see cref="IDebugHudPanel"/> - e.g. focused element name and
    /// hierarchy, or resource references.
    /// </summary>
    public interface IDebugHudSection
    {
        /// <summary>
        /// Gets this section's display name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Builds this section's element subtree. Called once, when its panel becomes active.
        /// </summary>
        /// <returns>The root of this section's subtree, to be appended into its panel's own tree.</returns>
        UIElement Build();

        /// <summary>
        /// Updates the subtree <see cref="Build"/> returned in place - e.g. a <c>TextBlock.Text</c> - to reflect
        /// the current frame. Called every frame this section's panel is active.
        /// </summary>
        /// <param name="built">The subtree <see cref="Build"/> returned.</param>
        /// <param name="frame">The current frame's information.</param>
        void Refresh(UIElement built, in DebugFrameContext frame);
    }
}
