namespace Icy.Rendering.Fonts
{
    public interface ICustomFontEffect
    {
        public void ApplyEffect(RenderGlyph glyph, IRenderContext renderContext);
    }
}
