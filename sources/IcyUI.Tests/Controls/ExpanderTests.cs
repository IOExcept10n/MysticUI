using System.Drawing;
using System.Linq;
using Icy.Animations;
using Icy.Assets;
using Icy.Configuration;
using Icy.Data.Markup;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ExpanderTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var expander = new Expander();

            Assert.False(expander.IsExpanded);
            Assert.Equal(0f, expander.ExpansionProgress);
            Assert.Null(expander.Header);
            Assert.Null(expander.Content);
        }

        [Fact]
        public void Header_Set_ForwardsToTheHeaderToggleAndWiresParent()
        {
            var expander = new Expander();
            var header = new Border();

            expander.Header = header;

            Assert.Same(header, expander.Header);
            Assert.NotNull(header.Parent);
        }

        [Fact]
        public void Content_Set_WiresParentAndClipsToBounds()
        {
            var expander = new Expander();
            var content = new Border();

            expander.Content = content;

            Assert.Same(expander, content.Parent);
            Assert.True(content.ClipToBounds);
        }

        [Fact]
        public void Content_Replaced_UnparentsTheOldValue()
        {
            var expander = new Expander();
            var oldContent = new Border();
            var newContent = new Border();
            expander.Content = oldContent;

            expander.Content = newContent;

            Assert.Null(oldContent.Parent);
            Assert.Same(expander, newContent.Parent);
        }

        [Fact]
        public void Content_Set_StartsNotVisible_SinceExpansionProgressStartsAtZero()
        {
            var expander = new Expander();
            var content = new Border();

            expander.Content = content;

            Assert.False(content.IsVisible);
        }

        [Fact]
        public void GetVisualChildren_YieldsChromeAndContent()
        {
            var expander = new Expander();
            var content = new Border();
            expander.Content = content;

            var subtree = expander.EnumerateVisualSubtree().ToList();

            Assert.Contains(content, subtree);
            // The header toggle lives inside Chrome, distinct from Content.
            Assert.Contains(subtree, e => e is ExpanderHeader);
        }

        [Fact]
        public void IsExpanded_SetsExpandedControlStateFlag()
        {
            var expander = new Expander { IsExpanded = true };

            Assert.Equal(ControlState.Expanded, expander.ControlState & ControlState.Expanded);

            expander.IsExpanded = false;

            Assert.Equal(ControlState.Normal, expander.ControlState & ControlState.Expanded);
        }

        [Fact]
        public void IsExpandedChanged_FiresOncePerRealChange()
        {
            var expander = new Expander();
            int raisedCount = 0;
            expander.IsExpandedChanged += (_, _) => raisedCount++;

            expander.IsExpanded = true;
            expander.IsExpanded = true; // no-op re-assignment - must not raise again

            Assert.Equal(1, raisedCount);
        }

        [Fact]
        public void ExpanderHeader_ChevronRotation_DefaultsToZeroAndIsSettable()
        {
            var header = new ExpanderHeader();

            Assert.Equal(0f, header.ChevronRotation);

            header.ChevronRotation = 90f;

            Assert.Equal(90f, header.ChevronRotation);
        }

        [Fact]
        public void MeasureContent_ExpansionProgressZero_OnlyCountsTheHeader()
        {
            var expander = new Expander { Header = new Border { Width = 40, Height = 20 } };
            expander.Content = new Border { Width = 100, Height = 100 };

            Size measured = expander.Measure();

            Assert.Equal(20, measured.Height);
        }

        [Fact]
        public void MeasureContent_ScalesWithExpansionProgress()
        {
            var expander = new Expander { Header = new Border { Width = 40, Height = 20 } };
            expander.Content = new Border { Width = 100, Height = 100 };

            expander.ExpansionProgress = 0.5f;
            Size half = expander.Measure();

            Assert.Equal(20 + 50, half.Height); // header + half of content's 100px

            expander.ExpansionProgress = 1f;
            Size full = expander.Measure();

            Assert.Equal(20 + 100, full.Height); // header + full content
        }

        [Fact]
        public void ArrangeContent_ExpansionProgressZero_SkipsContentEntirely()
        {
            var expander = new Expander { Width = 200, Height = 300 };
            var content = new Border { Height = 100 };
            expander.Content = content;

            expander.Arrange(new Rectangle(0, 0, 200, 300));

            Assert.False(content.IsVisible);
            Assert.Equal(0, content.ActualBounds.Width);
            Assert.Equal(0, content.ActualBounds.Height);
        }

        [Fact]
        public void ArrangeContent_PartialExpansion_RevealsAProportionalHeight()
        {
            // Content is an un-sized wrapper (Height=NaN, Stretch) around a 100px-tall child, not an
            // explicitly-sized element itself - an explicit Height would keep the child's own ActualBounds at
            // its full natural size regardless of the smaller Arrange rect (only ClipToBounds would mask the
            // overflow visually), so this shape is what actually exercises the shrink-via-Arrange behavior,
            // matching a realistic Expander.Content (e.g. a Border/StackPanel with no fixed Height of its own).
            var expander = new Expander { Width = 200, Height = 300, ExpansionProgress = 0.5f };
            var content = new Border { Child = new Border { Height = 100 } };
            expander.Content = content;

            expander.Arrange(new Rectangle(0, 0, 200, 300));

            Assert.Equal(50, content.ActualBounds.Height);
        }

        [Fact]
        public void ArrangeContent_FullExpansion_RevealsTheFullContentHeight()
        {
            var expander = new Expander { Width = 200, Height = 300, ExpansionProgress = 1f };
            var content = new Border { Child = new Border { Height = 100 } };
            expander.Content = content;

            expander.Arrange(new Rectangle(0, 0, 200, 300));

            Assert.Equal(100, content.ActualBounds.Height);
        }

        [Fact]
        public void ArrangeContent_ExpansionProgressChangedAfterFirstArrange_ReArrangesContent()
        {
            // Regression, applied proactively (see the SplitPane postmortem in [[project_icyui_tier2_roadmap]]):
            // Arrange(rect) no-ops when the target's own IsArrangeInvalid is already false, regardless of whether
            // rect changed - Content must be force-invalidated before every Arrange call, not just the first one.
            var expander = new Expander { Width = 200, Height = 300, ExpansionProgress = 0.5f };
            var content = new Border { Child = new Border { Height = 100 } };
            expander.Content = content;
            expander.Arrange(new Rectangle(0, 0, 200, 300));
            int firstHeight = content.ActualBounds.Height;

            expander.ExpansionProgress = 1f;
            expander.Arrange(new Rectangle(0, 0, 200, 300));

            Assert.NotEqual(firstHeight, content.ActualBounds.Height);
            Assert.Equal(100, content.ActualBounds.Height);
        }

        private static ControlTemplate LoadTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (ControlTemplate)loader.LoadObject(markup);
        }

        [Fact]
        public void Template_WithPartHeader_UsesTheTemplatesOwnHeaderForClickAndContent()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="Expander">
                  <Border>
                    <ExpanderHeader x:Name="PART_Header"/>
                  </Border>
                </ControlTemplate>
                """);
            var expander = new Expander { Template = template, Header = new Border() };

            var templatedHeader = expander.EnumerateVisualSubtree().OfType<ExpanderHeader>().Single();

            Assert.Same(expander.Header, templatedHeader.Content);
        }

        [Fact]
        public void Template_WithPartHeader_ClickingItTogglesIsExpanded()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="Expander">
                  <Border>
                    <ExpanderHeader x:Name="PART_Header" Width="100" Height="30"/>
                  </Border>
                </ControlTemplate>
                """);
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            var expander = new Expander
            {
                Template = template,
                Width = 100,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(expander);
            canvas.Render();

            input.Events.Touch.RaiseTap(new TouchInfo(new Point(50, 15), 1));

            Assert.True(expander.IsExpanded);
        }

        [Fact]
        public void Template_WithoutPartHeader_StillArrangesWithoutThrowing()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Expander"><Border/></ControlTemplate>""");
            var expander = new Expander { Template = template, Width = 200, Height = 100, Content = new Border() };

            var exception = Record.Exception(() => expander.Arrange(new Rectangle(0, 0, 200, 100)));

            Assert.Null(exception);
        }

        [Fact]
        public void Template_ClearedAfterBeingSet_RestoresDefaultHeaderBehavior()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="Expander">
                  <Border>
                    <ExpanderHeader x:Name="PART_Header"/>
                  </Border>
                </ControlTemplate>
                """);
            var expander = new Expander { Template = template, Header = new Border() };

            expander.Template = null;

            var defaultHeader = expander.EnumerateVisualSubtree().OfType<ExpanderHeader>().Single();
            Assert.Same(expander.Header, defaultHeader.Content);
        }

        [Fact]
        public void ThemedExpander_AfterFullAnimation_ContentTextReachesItsFullNaturalHeight()
        {
            // End-to-end regression (real theme, real font, real animated VisualState transition) for the
            // Border.ArrangeContent bug found via manual smoke testing: a bare TextBlock (no explicit
            // Width/Height, no wrapping StackPanel) directly inside Expander.Content got permanently stuck at
            // whatever tiny size it happened to receive the first animation frame it became visible on, since
            // Border.ArrangeContent never force-invalidated its Child before re-arranging it into Content's own
            // (correctly growing) bounds each frame. Fixed in Border.cs; this guards the fix at the level it was
            // actually observed, on top of the more targeted BorderTests.cs unit test.
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build().UseDefaultTheme();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");
            config.Fonts.DefaultFontFamily = "Airfool";
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };

            var textBlock = new TextBlock { Text = "Simple collapsible content, revealed below the header." };
            var contentBorder = new Border { Padding = new Thickness(10) };
            contentBorder.Child = textBlock;

            var expander = new Expander
            {
                Width = 400,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Header = new TextBlock { Text = "Plain-text header" },
                Content = contentBorder,
            };
            canvas.Add(expander);
            canvas.Render();

            expander.IsExpanded = true;
            // Advance well past the theme's 200ms Expanded transition, rendering multiple frames along the way
            // (not just the final one) - the bug only manifested once the animation had already ticked forward
            // at least once, permanently freezing the child's first-arranged size.
            for (int i = 0; i < 5; i++)
            {
                Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromMilliseconds(50));
                canvas.Render();
            }

            Assert.Equal(1f, expander.ExpansionProgress);
            Assert.Equal(textBlock.Measure().Height, textBlock.ActualBounds.Height);
        }
    }
}
