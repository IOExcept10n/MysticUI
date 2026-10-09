// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Data;
using Icy.UI.Controls;

namespace Icy.UI
{
    /// <content>
    /// Directional and activation navigation: routes presses through the focused element, and moves focus spatially
    /// when nothing claims them.
    /// </content>
    public partial class Canvas
    {
        // How far, in layout units, a candidate may overlap the focused element and still count as ahead.
        private const float SpatialTolerance = 4f;

        private readonly List<UIElement> spatialCandidates = [];
        private readonly List<Rectangle> spatialRectangles = [];
        private WeakReference<UIElement>? reverseOrigin;
        private WeakReference<UIElement>? reverseTarget;
        private Vector2 reverseDirection;

        // Focus survives Canvas.Remove; a removed element must not keep claiming presses (or get clicked by Enter).
        private UIElement? AttachedFocusedElement => FocusedElement?.Canvas == this ? FocusedElement : null;

        /// <summary>
        /// Moves focus to the nearest focusable element in <paramref name="direction"/>, the way arrow keys, the D-pad
        /// and the left stick do when the focused element doesn't use the press.
        /// </summary>
        /// <param name="direction">Any direction, UI space (+Y down). It's snapped to its dominant axis.</param>
        /// <returns><see langword="true"/> when focus moved.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Candidates are the focusable, visible, enabled elements of the focused element's focus scope (see <see cref="UIElement.IsFocusScope"/>), or of the whole canvas without one.</description></item>
        /// <item><description>Elements clipped away are skipped, except content scrolled out of a <see cref="ScrollViewer"/> that is itself on screen.</description></item>
        /// <item><description>Candidates straight ahead (overlapping on the cross axis) win over diagonal ones; then the nearest wins.</description></item>
        /// <item><description>Pressing the opposite direction right after a move returns to where it started.</description></item>
        /// <item><description>With nothing focused, the first element in Tab order is focused. There is no wrap-around.</description></item>
        /// </list>
        /// <para>The newly focused element is brought into view (see <see cref="UIElement.BringIntoView"/>).</para>
        /// </remarks>
        public bool MoveFocus(Vector2 direction)
        {
            direction = SpatialNavigation.Snap(direction);
            if (direction == Vector2.Zero)
                return false;

            UIElement? from = FocusedElement;
            if (from == null || from.Canvas != this)
            {
                UIElement? first = EnumerateFocusOrder(null).FirstOrDefault(IsSpatiallyReachable);
                if (first == null)
                    return false;
                FocusFromNavigation(first);
                return true;
            }

            UIElement? target = TakeReverseStep(from, direction) ?? FindSpatialTarget(from, direction);
            if (target == null)
                return false;

            FocusFromNavigation(target);
            reverseOrigin = new WeakReference<UIElement>(from);
            reverseTarget = new WeakReference<UIElement>(target);
            reverseDirection = direction;
            return true;
        }

        private static bool IsAncestorOf(UIElement candidate, UIElement element)
        {
            for (UIElement? current = element.Parent; current != null; current = current.Parent)
            {
                if (current == candidate)
                    return true;
            }

            return false;
        }

        private void Navigation_FocusChanging(object? sender, AcceptableEventArgs<Vector2> e)
        {
            Vector2 direction = SpatialNavigation.Snap(e.Data);
            if (direction == Vector2.Zero)
                return;

            // The focused control (a text box's caret, a combo box's highlight) gets the arrow even while keyboard
            // navigation is off; only moving the focus to another element is what the flag turns off.
            if (RouteNavigate(direction) || (IsKeyboardNavigationEnabled && MoveFocus(direction)))
                e.Handled = true;
        }

        private void Navigation_SelectElement(object? sender, EventArgs e)
        {
            if (!IsKeyboardNavigationEnabled)
                return;

            foreach (UIElement element in SelfAndAncestors(AttachedFocusedElement))
            {
                if (element.OnActivate())
                    return;
            }
        }

        private bool RouteNavigate(Vector2 direction)
        {
            foreach (UIElement element in SelfAndAncestors(AttachedFocusedElement))
            {
                if (element.OnNavigate(direction))
                    return true;
            }

            return false;
        }

        /// <summary>The Tab-order candidates of <paramref name="focused"/>'s focus scope (shared with <see cref="MoveFocus(bool)"/>).</summary>
        private IEnumerable<UIElement> EnumerateFocusOrder(UIElement? focused)
        {
            UIElement? scope = FindEnclosingFocusScope(focused);
            IEnumerable<UIElement> roots = scope != null ? scope.EnumerateVisualSubtree().Skip(1) : EnumerateAllElements();
            return roots.Where(e => e.IsFocusable && e.IsVisible);
        }

        private void FocusFromNavigation(UIElement element)
        {
            Focus(element);
            if (FocusedElement == element)
                element.BringIntoView();
        }

        private void ClearReverseStep()
        {
            reverseOrigin = null;
            reverseTarget = null;
        }

        private UIElement? TakeReverseStep(UIElement from, Vector2 direction)
        {
            if (direction != -reverseDirection
                || reverseTarget == null || !reverseTarget.TryGetTarget(out UIElement? target) || target != from
                || reverseOrigin == null || !reverseOrigin.TryGetTarget(out UIElement? origin))
            {
                return null;
            }

            return origin.Canvas == this && EnumerateFocusOrder(from).Contains(origin) && IsSpatiallyReachable(origin) ? origin : null;
        }

        private UIElement? FindSpatialTarget(UIElement from, Vector2 direction)
        {
            spatialCandidates.Clear();
            spatialRectangles.Clear();
            foreach (UIElement element in EnumerateFocusOrder(from))
            {
                if (element == from || IsAncestorOf(element, from) || !IsSpatiallyReachable(element))
                    continue;
                spatialCandidates.Add(element);
                spatialRectangles.Add(element.GetScreenBounds());
            }

            int best = SpatialNavigation.FindBest(from.GetNavigationOrigin(), direction, spatialRectangles, SpatialTolerance * EffectiveScale);
            UIElement? result = best < 0 ? null : spatialCandidates[best];
            spatialCandidates.Clear();
            return result;
        }

        /// <summary>
        /// Whether <paramref name="element"/> can be reached: effectively visible and enabled, and not clipped away. A
        /// <see cref="ScrollViewer"/> doesn't hide its own content (it's reachable by scrolling), but the walk then
        /// continues from the viewer's own bounds, so a viewer that is itself hidden hides everything in it.
        /// </summary>
        private bool IsSpatiallyReachable(UIElement element)
        {
            if (element.Canvas != this)
                return false;

            Rectangle visible = element.GetScreenBounds();
            for (UIElement current = element; ; current = current.Parent!)
            {
                if (!current.IsVisible || current.Opacity <= 0 || !current.IsEnabled)
                    return false;

                UIElement? parent = current.Parent;
                if (parent == null)
                    break;
                if (parent is ScrollViewer)
                    visible = parent.GetScreenBounds();
                else if (parent.ClipToBounds)
                    visible = Rectangle.Intersect(visible, parent.GetScreenBounds());
                if (visible.Width <= 0 || visible.Height <= 0)
                    return false;
            }

            visible = Rectangle.Intersect(visible, new Rectangle(Point.Empty, Configuration.RenderContext.ViewportSize));
            return visible.Width > 0 && visible.Height > 0;
        }
    }
}
