using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icy.UI.Styles;

namespace Icy.UI
{
    public static class UIElementExtensions
    {
        /// <summary>
        /// Removes the <see cref="UIElement"/> instance from its parent.
        /// </summary>
        /// <param name="element">The element to detach.</param>
        public static void Detach(this UIElement element)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Applies a style to the UI element.
        /// </summary>
        /// <param name="style">Style instance to apply.</param>
        public static T WithStyle<T>(this T element, Style style)
            where T : UIElement
        {
            throw new NotImplementedException();
        }

        public static IEnumerable<UIElement> FindChildren(this UIElement control, Func<UIElement, bool> predicate)
        {
            throw new NotImplementedException();
        }

        public static T? FindControl<T>(string name)
            where T : UIElement
        {
            throw new NotImplementedException();
        }

        public static T FindRequiredControl<T>(string name)
            where T : UIElement
        {
            throw new NotImplementedException();
        }
    }
}
