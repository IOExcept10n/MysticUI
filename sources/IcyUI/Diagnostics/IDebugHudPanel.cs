// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Diagnostics
{
    /// <summary>
    /// A named group of <see cref="IDebugHudSection"/>s, drawn as one block in the screen-space debug HUD.
    /// </summary>
    public interface IDebugHudPanel
    {
        /// <summary>
        /// Gets the name this panel is registered and activated under - matched against
        /// <see cref="UI.Canvas.ActiveDebugTools"/>.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets this panel's sections, in the order they're laid out.
        /// </summary>
        IReadOnlyList<IDebugHudSection> Sections { get; }
    }
}
