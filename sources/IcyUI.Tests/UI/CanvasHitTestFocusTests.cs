using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.UI
{
    /// <summary>
    /// Phase 5 coverage: <see cref="Canvas.HitTest(Point)"/>, focus tracking, <see cref="UIElement.IsFocusScope"/>
    /// trap/restore, and aggregated-event routing (<see cref="ITouchEvents"/>/<see cref="INavigationEvents"/>) -
    /// all exercised via synthetic element trees and synthesized event payloads (see <see cref="FakeInputSystem"/>),
    /// not real device polling.
    /// </summary>
    public class CanvasHitTestFocusTests
    {
        private static (Canvas Canvas, FakeInputSystem Input, FakeRenderContext RenderContext) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), input, renderContext);
        }

        private static UIElement CreateLeaf(int x, int y, float size, float zIndex = 0, bool focusable = false) => new()
        {
            Width = size,
            Height = size,
            Margin = new Thickness(x, y, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ZIndex = zIndex,
            IsFocusable = focusable,
        };

        [Fact]
        public void HitTest_OverlappingElements_ReturnsHighestZIndex()
        {
            var (canvas, _, _) = CreateCanvas();
            UIElement back = CreateLeaf(0, 0, 100, zIndex: 0);
            UIElement front = CreateLeaf(0, 0, 100, zIndex: 1);
            canvas.Add(back);
            canvas.Add(front);

            UIElement? hit = canvas.HitTest(new Point(50, 50));

            Assert.Same(front, hit);
        }

        [Fact]
        public void HitTest_PointOutsideAnyElement_ReturnsNull()
        {
            var (canvas, _, _) = CreateCanvas();
            canvas.Add(CreateLeaf(0, 0, 100));

            UIElement? hit = canvas.HitTest(new Point(500, 500));

            Assert.Null(hit);
        }

        [Fact]
        public void Focus_MovesFocusAndTogglesIsFocusedOnBothElements()
        {
            var (canvas, _, _) = CreateCanvas();
            UIElement first = CreateLeaf(0, 0, 50, focusable: true);
            UIElement second = CreateLeaf(50, 0, 50, focusable: true);
            canvas.Add(first);
            canvas.Add(second);

            canvas.Focus(first);
            Assert.True(first.IsFocused);
            Assert.Same(first, canvas.FocusedElement);

            canvas.Focus(second);
            Assert.False(first.IsFocused);
            Assert.True(second.IsFocused);
            Assert.Same(second, canvas.FocusedElement);
        }

        [Fact]
        public void Focus_RaisesFocusChangedOnBothElements()
        {
            var (canvas, _, _) = CreateCanvas();
            UIElement first = CreateLeaf(0, 0, 50, focusable: true);
            UIElement second = CreateLeaf(50, 0, 50, focusable: true);
            canvas.Add(first);
            canvas.Add(second);
            canvas.Focus(first);

            bool firstRaised = false;
            bool secondRaised = false;
            first.FocusChanged += (_, _) => firstRaised = true;
            second.FocusChanged += (_, _) => secondRaised = true;

            canvas.Focus(second);

            Assert.True(firstRaised);
            Assert.True(secondRaised);
        }

        [Fact]
        public void Focus_IgnoresNonFocusableElement()
        {
            var (canvas, _, _) = CreateCanvas();
            UIElement notFocusable = CreateLeaf(0, 0, 50, focusable: false);
            canvas.Add(notFocusable);

            canvas.Focus(notFocusable);

            Assert.Null(canvas.FocusedElement);
        }

        [Fact]
        public void Focus_IgnoresInvisibleElement()
        {
            var (canvas, _, _) = CreateCanvas();
            UIElement invisible = CreateLeaf(0, 0, 50, focusable: true);
            invisible.IsVisible = false;
            canvas.Add(invisible);

            canvas.Focus(invisible);

            Assert.Null(canvas.FocusedElement);
        }

        [Fact]
        public void MoveFocus_CyclesForwardInInsertionOrderAndWraps()
        {
            var (canvas, _, _) = CreateCanvas();
            UIElement first = CreateLeaf(0, 0, 50, focusable: true);
            UIElement second = CreateLeaf(50, 0, 50, focusable: true);
            canvas.Add(first);
            canvas.Add(second);

            canvas.MoveFocus(forward: true);
            Assert.Same(first, canvas.FocusedElement);

            canvas.MoveFocus(forward: true);
            Assert.Same(second, canvas.FocusedElement);

            canvas.MoveFocus(forward: true);
            Assert.Same(first, canvas.FocusedElement);
        }

        [Fact]
        public void MoveFocus_Backward_WrapsToLastElement()
        {
            var (canvas, _, _) = CreateCanvas();
            UIElement first = CreateLeaf(0, 0, 50, focusable: true);
            UIElement second = CreateLeaf(50, 0, 50, focusable: true);
            canvas.Add(first);
            canvas.Add(second);

            canvas.MoveFocus(forward: false);

            Assert.Same(second, canvas.FocusedElement);
        }

        [Fact]
        public void MoveFocus_SkipsNonFocusableElements()
        {
            var (canvas, _, _) = CreateCanvas();
            UIElement decorative = CreateLeaf(0, 0, 50, focusable: false);
            UIElement focusable = CreateLeaf(50, 0, 50, focusable: true);
            canvas.Add(decorative);
            canvas.Add(focusable);

            canvas.MoveFocus(forward: true);

            Assert.Same(focusable, canvas.FocusedElement);
        }

        [Fact]
        public void MoveFocus_InsideFocusScope_DoesNotEscapeToOutsideSibling()
        {
            var (canvas, _, _) = CreateCanvas();
            var scope = new Panel { IsFocusScope = true };
            UIElement inScopeA = CreateLeaf(0, 0, 50, focusable: true);
            UIElement inScopeB = CreateLeaf(50, 0, 50, focusable: true);
            scope.Children.Add(inScopeA);
            scope.Children.Add(inScopeB);
            canvas.Add(scope);

            UIElement outsideSibling = CreateLeaf(200, 0, 50, focusable: true);
            canvas.Add(outsideSibling);

            canvas.Focus(inScopeA);
            canvas.MoveFocus(forward: true);
            Assert.Same(inScopeB, canvas.FocusedElement);

            // Should wrap back to inScopeA, never reaching outsideSibling.
            canvas.MoveFocus(forward: true);
            Assert.Same(inScopeA, canvas.FocusedElement);
        }

        [Fact]
        public void FindEnclosingFocusScope_WalksParentChainToNearestScope()
        {
            var scope = new Panel { IsFocusScope = true };
            UIElement child = CreateLeaf(0, 0, 50, focusable: true);
            scope.Children.Add(child);

            UIElement? found = Canvas.FindEnclosingFocusScope(child);

            Assert.Same(scope, found);
        }

        [Fact]
        public void FindEnclosingFocusScope_ReturnsNull_WhenNoAncestorIsScope()
        {
            var container = new Panel { IsFocusScope = false };
            UIElement child = CreateLeaf(0, 0, 50, focusable: true);
            container.Children.Add(child);

            UIElement? found = Canvas.FindEnclosingFocusScope(child);

            Assert.Null(found);
        }

        [Fact]
        public void CloseModal_RestoresFocusToElementFocusedBeforeEnteringScope()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;

            UIElement opener = CreateLeaf(0, 0, 50, focusable: true);
            var scope = new Panel { IsFocusScope = true };
            UIElement insideScope = CreateLeaf(200, 0, 50, focusable: true);
            scope.Children.Add(insideScope);
            canvas.Add(opener);
            canvas.Add(scope);

            canvas.Focus(opener);
            canvas.Focus(insideScope);

            // Wires up NavigationEvents.CloseModal on the fake input system.
            canvas.Render();

            input.Events.Navigation.RaiseCloseModal();

            Assert.Same(opener, canvas.FocusedElement);
        }

        [Fact]
        public void TouchDown_SetsPressedState_TouchUp_ClearsIt()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            UIElement target = CreateLeaf(0, 0, 100);
            canvas.Add(target);
            canvas.Render();

            input.Events.Touch.RaiseTouchDown(new Point(50, 50));
            Assert.Equal(ControlState.Pressed, target.ControlState & ControlState.Pressed);

            input.Events.Touch.RaiseTouchUp(new Point(50, 50));
            Assert.Equal(ControlState.Normal, target.ControlState & ControlState.Pressed);
        }

        [Fact]
        public void Tap_FocusesHitFocusableElement()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            UIElement target = CreateLeaf(0, 0, 100, focusable: true);
            canvas.Add(target);
            canvas.Render();

            input.Events.Touch.RaiseTap(new TouchInfo(new Point(50, 50), 1));

            Assert.Same(target, canvas.FocusedElement);
        }

        [Fact]
        public void Render_MouseOverElement_SetsHoveredState_MouseAway_ClearsIt()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            UIElement target = CreateLeaf(0, 0, 100);
            canvas.Add(target);

            input.Mouse.MouseInfo = new(new Point(50, 50));
            canvas.Render();
            Assert.Equal(ControlState.Hovered, target.ControlState & ControlState.Hovered);

            input.Mouse.MouseInfo = new(new Point(500, 500));
            canvas.Render();
            Assert.Equal(ControlState.Normal, target.ControlState & ControlState.Hovered);
        }
    }
}
