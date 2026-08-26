using System.Drawing;
using System.Numerics;
using Icy.Rendering;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    /// <summary>
    /// Regression coverage for a bug found while building Phase 5's hit-testing: ActualBounds.Location is computed
    /// cumulatively (Arrange() positions an element within LogicalParent.ContentBounds, which already carries the
    /// parent's own absolute position), but the render transform chain (UIElement.Draw composes each ancestor's
    /// layoutTransform/renderTransform together) also accumulated translation hierarchically - double-counting
    /// every ancestor's own offset for anything nested two or more levels deep. Never visible before because
    /// nothing exercised 2+ levels of nesting (samples only add elements directly to Canvas).
    /// </summary>
    public class NestedTransformTests
    {
        private class FixedBoundsContainer : UIElement, IContainerLayout
        {
            public List<UIElement> Children { get; } = [];

            public Rectangle ContentBounds => ActualBounds - Padding;

            public void SetActualBoundsForTest(Rectangle bounds)
            {
                ActualBounds = bounds;
                IsArrangeInvalid = false;
            }

            protected override Size MeasureContent() => Size.Empty;

            protected override void ArrangeContent()
            {
                foreach (UIElement child in Children)
                    child.Arrange();
            }

            protected override void OnRender(IRenderContext context)
            {
                foreach (UIElement child in Children)
                    child.Draw(context);
            }
        }

        private class RecordingElement : UIElement
        {
            public Vector2? WorldOrigin { get; private set; }

            protected override Size MeasureContent() => Size.Empty;

            protected override void ArrangeContent()
            {
                // Test element doesn't need to arrange content
            }

            protected override void OnRender(IRenderContext context)
            {
                WorldOrigin = context.Transform.Apply(Vector2.Zero);
            }
        }

        [Fact]
        public void NestedElement_RendersAtCorrectAbsoluteWorldPosition()
        {
            // Simulates a Panel positioned at (50,50) within its own (unmodeled) parent, containing a child
            // offset by a further (10,10) margin - the child should render at the absolute position (60,60),
            // not (110,110) (50+60, double-counting the container's own offset).
            var container = new FixedBoundsContainer();
            container.SetActualBoundsForTest(new Rectangle(50, 50, 400, 300));

            var child = new RecordingElement
            {
                Width = 100,
                Height = 100,
                Margin = new Thickness(10, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            container.Children.Add(child);
            child.Parent = container;

            var context = new FakeRenderContext { Transform = Transform2D.Identity };
            container.Draw(context);

            Assert.NotNull(child.WorldOrigin);
            Assert.Equal(60f, child.WorldOrigin!.Value.X);
            Assert.Equal(60f, child.WorldOrigin!.Value.Y);
        }

        [Fact]
        public void ThreeLevelsDeep_StillRendersAtCorrectAbsolutePosition()
        {
            var outer = new FixedBoundsContainer();
            outer.SetActualBoundsForTest(new Rectangle(20, 20, 600, 400));

            var inner = new FixedBoundsContainer
            {
                Width = 300,
                Height = 200,
                Margin = new Thickness(30, 30, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            outer.Children.Add(inner);
            inner.Parent = outer;

            var leaf = new RecordingElement
            {
                Width = 50,
                Height = 50,
                Margin = new Thickness(5, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            inner.Children.Add(leaf);
            leaf.Parent = inner;

            var context = new FakeRenderContext { Transform = Transform2D.Identity };
            outer.Draw(context);

            // inner arranges to (20+30, 20+30) = (50,50) within outer; leaf arranges to (50+5, 50+5) = (55,55).
            Assert.NotNull(leaf.WorldOrigin);
            Assert.Equal(55f, leaf.WorldOrigin!.Value.X);
            Assert.Equal(55f, leaf.WorldOrigin!.Value.Y);
        }

        [Fact]
        public void ChildOfPaddedContainer_RendersInsetByPadding_NotPulledBackToContainersRawOrigin()
        {
            // Regression for a real bug: UpdateTransformMatrix used to subtract LogicalParent.ContentBounds.Location
            // (ActualBounds inset by Padding) from this element's own absolute ActualBounds, on the assumption that
            // the render transform chain already carried that same inset by the time this element's Draw() runs.
            // It never did - Draw() never applies a separate translation for a parent's own Padding before drawing
            // its children - so context.Transform flowing into a child's Draw() always equals the parent's raw
            // ActualBounds-based transform. Every child of a padded container rendered pulled back by exactly the
            // parent's Padding (e.g. a centered Button label rendering near the container's unpadded corner instead
            // of at its own correctly-arranged, padding-inset ActualBounds - invisible whenever Padding was zero).
            var container = new FixedBoundsContainer { Padding = new Thickness(20, 15, 0, 0) };
            container.SetActualBoundsForTest(new Rectangle(100, 100, 400, 300));

            var child = new RecordingElement
            {
                Width = 50,
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            container.Children.Add(child);
            child.Parent = container;

            var context = new FakeRenderContext { Transform = Transform2D.Identity };
            container.Draw(context);

            // child.ActualBounds.Location = container.ContentBounds.Location = (100+20, 100+15) = (120,115) - the
            // draw-time world position must match that exactly, not the container's unpadded (100,100).
            Assert.NotNull(child.WorldOrigin);
            Assert.Equal(120f, child.WorldOrigin!.Value.X);
            Assert.Equal(115f, child.WorldOrigin!.Value.Y);
        }
    }
}
