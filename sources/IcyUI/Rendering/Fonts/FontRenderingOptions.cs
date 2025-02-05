using System.Drawing;
using System.Numerics;

namespace Icy.Rendering.Fonts
{
    // TODO

    /// <summary>
    /// Defines an effect for the font render step.
    /// </summary>
    public enum FontEffect
    {
        /// <summary>
        /// No effects to apply.
        /// </summary>
        None,

        /// <summary>
        /// Applies blur to the text.
        /// </summary>
        Blurry,

        /// <summary>
        /// Applies stroke to the text.
        /// </summary>
        Stroked,
    }

    /// <summary>
    /// Represents the options for rendering text with a custom font.
    /// </summary>
    /// <param name="Position">The position of the text in the rendering space.</param>
    /// <param name="Scale">The scale factor for the text. If null, the default scale will be used.</param>
    /// <param name="Rotation">The rotation angle of the text in degrees.</param>
    /// <param name="Origin">The origin point for the text rotation.</param>
    /// <param name="CharacterSpacing">The spacing between characters in the text.</param>
    /// <param name="LineSpacing">The spacing between lines of text.</param>
    /// <param name="Color">The color of the text.</param>
    /// <param name="Depth">The depth value for rendering, used for layering in 3D space.</param>
    /// <param name="Effect">An optional custom font effect to apply to the text.</param>
    public readonly record struct FontRenderingOptions(Vector2 Position, Vector2? Scale, float Rotation, Vector2 Origin, float CharacterSpacing, float LineSpacing, Color Color, float Depth, ICustomFontEffect? Effect)
    {
    }
}
