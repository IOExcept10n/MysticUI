using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icy.Rendering
{
    public static class RenderExtensions
    {

        public static void Modify<TColor>(this ITexture texture, Func<TColor, TColor> modifier)
            where TColor : unmanaged
        {
            int size = texture.Size.Width * texture.Size.Height;
            var buffer = ArrayPool<TColor>.Shared.Rent(size);
            try
            {
                texture.GetTextureData(buffer);
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = modifier(buffer[i]);
                }
                texture.SetTextureData(buffer);
            }
            finally
            {
                ArrayPool<TColor>.Shared.Return(buffer);
            }
        }
    }
}
