using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class SplitPaneTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var pane = new SplitPane();

            Assert.Equal(Orientation.Horizontal, pane.Orientation);
            Assert.Equal(0.5f, pane.SplitterPosition);
            Assert.Equal(0f, pane.MinFirstSize);
            Assert.Equal(0f, pane.MinSecondSize);
            Assert.Equal(6f, pane.DividerSize);
            Assert.Null(pane.First);
            Assert.Null(pane.Second);
        }

        [Fact]
        public void First_Set_WiresParent()
        {
            var pane = new SplitPane();
            var child = new Border();

            pane.First = child;

            Assert.Same(pane, child.Parent);
        }

        [Fact]
        public void Second_Set_WiresParent()
        {
            var pane = new SplitPane();
            var child = new Border();

            pane.Second = child;

            Assert.Same(pane, child.Parent);
        }

        [Fact]
        public void First_Replaced_UnparentsTheOldValue()
        {
            var pane = new SplitPane();
            var oldChild = new Border();
            var newChild = new Border();
            pane.First = oldChild;

            pane.First = newChild;

            Assert.Null(oldChild.Parent);
            Assert.Same(pane, newChild.Parent);
        }

        [Fact]
        public void GetVisualChildren_YieldsChromeThenFirstThenSecond()
        {
            var pane = new SplitPane();
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            var subtree = pane.EnumerateVisualSubtree().ToList();

            int firstIndex = subtree.IndexOf(first);
            int secondIndex = subtree.IndexOf(second);
            Assert.True(firstIndex >= 0 && secondIndex >= 0 && firstIndex < secondIndex);
            // The default divider (Chrome's Child) is present too, distinct from First/Second.
            Assert.True(subtree.Count(e => e is Border) >= 4); // Chrome + divider + inner line + First + Second
        }

        [Fact]
        public void DividerSize_ChangedAfterConstruction_ResizesTheDivider()
        {
            var pane = new SplitPane { DividerSize = 20f };

            var divider = pane.EnumerateVisualSubtree().OfType<Border>().Skip(1).First(); // Chrome, then divider
            Assert.Equal(20f, divider.Width);
        }

        [Fact]
        public void ArrangeContent_Horizontal_SplitsProportionally()
        {
            var pane = new SplitPane { Width = 200, Height = 100, SplitterPosition = 0.5f };
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            pane.Arrange(new Rectangle(0, 0, 200, 100));

            // available = 200, roaming = 200 - DividerSize(6) = 194; firstWidth = 194 * 0.5 = 97.
            // Second starts after First AND the divider band: 97 + DividerSize(6) = 103.
            Assert.Equal(97, first.ActualBounds.Width);
            Assert.Equal(100, first.ActualBounds.Height);
            Assert.Equal(103, second.ActualBounds.X);
            Assert.Equal(200 - 97 - 6, second.ActualBounds.Width);
        }

        [Fact]
        public void ArrangeContent_Vertical_SplitsAlongHeight()
        {
            var pane = new SplitPane { Width = 100, Height = 200, Orientation = Orientation.Vertical, SplitterPosition = 0.25f };
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            pane.Arrange(new Rectangle(0, 0, 100, 200));

            // roaming = 200 - 6 = 194; firstHeight = 194 * 0.25 = 48 (truncated).
            Assert.Equal(48, first.ActualBounds.Height);
            Assert.Equal(100, first.ActualBounds.Width);
            Assert.Equal(48 + 6, second.ActualBounds.Y);
        }

        [Fact]
        public void ArrangeContent_RespectsMinFirstAndMinSecondSize()
        {
            var pane = new SplitPane
            {
                Width = 200,
                Height = 100,
                SplitterPosition = 0.05f, // would put First far below MinFirstSize without clamping
                MinFirstSize = 50,
                MinSecondSize = 50,
            };
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            pane.Arrange(new Rectangle(0, 0, 200, 100));

            Assert.True(first.ActualBounds.Width >= 50, $"First was {first.ActualBounds.Width}, expected >= 50");
            Assert.True(second.ActualBounds.Width >= 50, $"Second was {second.ActualBounds.Width}, expected >= 50");
        }

        [Fact]
        public void ArrangeContent_UndersizedContainer_DegradesWithoutThrowing()
        {
            // MinFirstSize + MinSecondSize + DividerSize (50+50+6=106) exceeds the 80px available - must not throw,
            // and must still produce a stable (if imperfect) split.
            var pane = new SplitPane { Width = 80, Height = 100, MinFirstSize = 50, MinSecondSize = 50 };
            pane.First = new Border();
            pane.Second = new Border();

            var exception = Record.Exception(() => pane.Arrange(new Rectangle(0, 0, 80, 100)));

            Assert.Null(exception);
        }

        [Fact]
        public void MeasureContent_SumsFirstAndSecondPlusDividerSize()
        {
            var pane = new SplitPane { DividerSize = 6 };
            pane.First = new Border { Width = 80, Height = 40 };
            pane.Second = new Border { Width = 60, Height = 30 };

            Size measured = pane.Measure();

            Assert.Equal(80 + 60 + 6, measured.Width);
            Assert.Equal(40, measured.Height);
        }

        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), input);
        }

        private static ControlTemplate LoadTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (ControlTemplate)loader.LoadObject(markup);
        }

        [Fact]
        public void Drag_UpdatesSplitterPositionBasedOnPointerPosition()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var pane = new SplitPane
            {
                Width = 200,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                First = new Border(),
                Second = new Border(),
            };
            canvas.Add(pane);
            canvas.Render();

            // Divider sits at roaming(194)*0.5=97..103 after the first render (SplitterPosition starts at 0.5).
            input.Events.Drag.RaiseDragStarted(new Point(100, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(150, 50));

            // roaming = 194; ratio = 150/194 (PointToLocal aligns with content origin here since Padding/BorderThickness are 0).
            Assert.Equal(150f / 194f, pane.SplitterPosition, 3);
        }

        [Fact]
        public void Drag_BeyondPaneEdge_ClampsToOne()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var pane = new SplitPane
            {
                Width = 200,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                First = new Border(),
                Second = new Border(),
            };
            canvas.Add(pane);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(100, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(1000, 50));

            Assert.Equal(1f, pane.SplitterPosition);
        }

        [Fact]
        public void Drag_RespectsMinSecondSize()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var pane = new SplitPane
            {
                Width = 200,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                MinSecondSize = 50,
                First = new Border(),
                Second = new Border(),
            };
            canvas.Add(pane);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(100, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(1000, 50));

            pane.Arrange(new Rectangle(0, 0, 200, 100));
            Assert.True(pane.Second!.ActualBounds.Width >= 50, $"Second was {pane.Second.ActualBounds.Width}, expected >= 50");
        }

        [Fact]
        public void SplitterPositionChanged_FiresOnce_PerActualChange()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var pane = new SplitPane
            {
                Width = 200,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                First = new Border(),
                Second = new Border(),
            };
            canvas.Add(pane);
            canvas.Render();
            // Drag-start alone already moves SplitterPosition off its 0.5 default (100/194 != 0.5) and would raise the
            // event once, unobserved - attach the handler only after that settles, so exactly one more move (drag-
            // performing) isolates exactly one raise, rather than conflating both changes into an ambiguous count.
            input.Events.Drag.RaiseDragStarted(new Point(100, 50));
            canvas.Render();
            int raiseCount = 0;
            pane.SplitterPositionChanged += (_, _) => raiseCount++;

            input.Events.Drag.RaiseDragPerforming(new Point(150, 50));

            Assert.Equal(1, raiseCount);
        }

        [Fact]
        public void Template_WithPartDivider_ArrangePositionsTheTemplatesOwnDivider()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="SplitPane">
                  <Border>
                    <Border x:Name="PART_Divider" Width="6" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
                  </Border>
                </ControlTemplate>
                """);
            var pane = new SplitPane { Template = template, Width = 200, Height = 100, First = new Border(), Second = new Border() };

            pane.Arrange(new Rectangle(0, 0, 200, 100));

            var templatedDivider = pane.EnumerateVisualSubtree().OfType<Border>().Single(b => b.Width == 6);
            Assert.Equal(97, templatedDivider.Margin.Left); // roaming(194)*0.5 = 97, same math as Task 2's arrange test.
        }

        [Fact]
        public void Template_WithPartDivider_DragUpdatesSplitterPositionAndTheTemplatesOwnDivider()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="SplitPane">
                  <Border>
                    <Border x:Name="PART_Divider" Width="6" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
                  </Border>
                </ControlTemplate>
                """);
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var pane = new SplitPane
            {
                Template = template,
                Width = 200,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                First = new Border(),
                Second = new Border(),
            };
            canvas.Add(pane);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(100, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(150, 50));
            canvas.Render();

            Assert.True(pane.SplitterPosition > 0.5f);
            var templatedDivider = pane.EnumerateVisualSubtree().OfType<Border>().Single(b => b.Width == 6);
            Assert.True(templatedDivider.Margin.Left > 97);
        }

        [Fact]
        public void Template_WithoutPartDivider_StillArrangesWithoutThrowing()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="SplitPane"><Border/></ControlTemplate>""");
            var pane = new SplitPane { Template = template, Width = 200, Height = 100, First = new Border(), Second = new Border() };

            var exception = Record.Exception(() => pane.Arrange(new Rectangle(0, 0, 200, 100)));

            Assert.Null(exception);
        }

        [Fact]
        public void Template_ClearedAfterBeingSet_RestoresDefaultDividerBehavior()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="SplitPane">
                  <Border>
                    <Border x:Name="PART_Divider" Width="6" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
                  </Border>
                </ControlTemplate>
                """);
            var pane = new SplitPane { Template = template, Width = 200, Height = 100, First = new Border(), Second = new Border() };
            pane.Arrange(new Rectangle(0, 0, 200, 100));

            pane.Template = null;
            pane.Arrange(new Rectangle(0, 0, 200, 100));

            // Back on the default divider - DividerBrush must route to a real element actually in the visual tree
            // (the default inner line), not the orphaned template's PART_Divider.
            var newBrush = new Icy.Rendering.Brushes.SolidColorBrush(Color.Red);
            pane.DividerBrush = newBrush;

            Assert.Same(newBrush, pane.DividerBrush);
            Assert.Contains(pane.EnumerateVisualSubtree().OfType<Border>(), b => ReferenceEquals(b.Background, newBrush));
        }

        [Fact]
        public void DividerBrush_DefaultsToWhiteAndIsSettable()
        {
            var pane = new SplitPane();

            var defaultBrush = Assert.IsType<Icy.Rendering.Brushes.SolidColorBrush>(pane.DividerBrush);
            Assert.Equal(Color.White.ToArgb(), defaultBrush.Color.ToArgb());

            var newBrush = new Icy.Rendering.Brushes.SolidColorBrush(Color.Red);
            pane.DividerBrush = newBrush;

            Assert.Same(newBrush, pane.DividerBrush);
        }
    }
}
