// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Data;

namespace Icy.UI
{
    /// <content>
    /// Directional and activation navigation: routes presses through the focused element, and moves focus spatially
    /// when nothing claims them.
    /// </content>
    public partial class Canvas
    {
        private void Navigation_FocusChanging(object? sender, AcceptableEventArgs<Vector2> e)
        {
            if (!IsKeyboardNavigationEnabled)
                return;

            Vector2 direction = SpatialNavigation.Snap(e.Data);
            if (direction == Vector2.Zero)
                return;

            if (RouteNavigate(direction))
                e.Handled = true;
        }

        private void Navigation_SelectElement(object? sender, EventArgs e)
        {
            if (!IsKeyboardNavigationEnabled)
                return;

            foreach (UIElement element in SelfAndAncestors(FocusedElement))
            {
                if (element.OnActivate())
                    return;
            }
        }

        private bool RouteNavigate(Vector2 direction)
        {
            foreach (UIElement element in SelfAndAncestors(FocusedElement))
            {
                if (element.OnNavigate(direction))
                    return true;
            }

            return false;
        }
    }
}
