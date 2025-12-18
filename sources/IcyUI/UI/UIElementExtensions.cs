using Icy.UI.Styles;

namespace Icy.UI
{
    public static class UIElementExtensions
    {
        /// <summary>
        /// Removes the <see cref="UIElement"/> instance from its parent. If the element is root, removes it from the <see cref="Canvas"/> instance.
        /// </summary>
        /// <param name="element">The element to detach.</param>
        public static void Detach(this UIElement element)
        {
            //if (element.Parent is IContainerControl container)
            //    container.Remove(element);
            //else
            //    element.Canvas?.Remove(element);
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

        public static IEnumerable<UIElement> EnumerateSubtree(this UIElement element)
        {
            //Stack<UIElement> items = new();
            //items.Push(element);
            //while (items.TryPop(out var target))
            //{
            //    yield return target;
            //    if (target is IContainerControl container)
            //    {
            //        foreach (var child in container.Children)
            //        {
            //            items.Push(child);
            //        }
            //    }
            //}
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
