using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icy.Rendering.Fonts
{
    public class FontSystem : KeyedCollection<FontInfo, IFont>
    {
        protected override FontInfo GetKeyForItem(IFont item) => item.Info;

        public IFont GetOrLoad(FontInfo info)
        {
            throw new NotImplementedException();
        }
    }
}
