using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.Rendering.Fonts
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

    public readonly record struct FontRenderingOptions(Vector2 Position, Vector2? Scale, float CharacterSpacing, float LineSpacing, FontEffect Effect, int EffectCount)
    {
    }
}
