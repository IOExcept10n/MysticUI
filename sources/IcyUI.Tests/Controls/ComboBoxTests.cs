using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ComboBoxTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var comboBox = new ComboBox();

            Assert.Equal(-1, comboBox.SelectedIndex);
            Assert.Equal(string.Empty, GetTextBox(comboBox).Text);
        }

        [Fact]
        public void Filter_IsCaseInsensitiveContains()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana", "Pineapple" } };

            GetTextBox(comboBox).Text = "APP";

            var visible = InvokeGetFilteredDisplayTexts(comboBox);
            Assert.Equal(new[] { "Apple", "Pineapple" }, visible);
        }

        [Fact]
        public void TypingWhileClosed_OpensAndFiltersFromTheFirstKeystroke()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana" } };

            GetTextBox(comboBox).Text = "a";

            Assert.True(comboBox.IsOpen);
        }

        [Fact]
        public void EnterWithHighlightedMatch_CommitsAndUpdatesText()
        {
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            var input = new FakeInputSystem();
            SimulateFocused(comboBox, input);
            GetTextBox(comboBox).Text = "ban";
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));

            InvokeOnNavigationSelectElement(comboBox);

            Assert.Equal("Banana", comboBox.SelectedItem);
            Assert.Equal("Banana", GetTextBox(comboBox).Text);
            Assert.False(comboBox.IsOpen);
        }

        [Fact]
        public void EnterWithNoMatch_RevertsTextWithoutChangingSelection()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana" } };
            comboBox.SelectedItem = "Apple";
            GetTextBox(comboBox).Text = "zzz";

            InvokeOnNavigationSelectElement(comboBox);

            Assert.Equal("Apple", comboBox.SelectedItem);
            Assert.Equal("Apple", GetTextBox(comboBox).Text);
        }

        [Fact]
        public void Escape_RevertsTextAndCloses()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana" } };
            comboBox.SelectedItem = "Apple";
            comboBox.IsOpen = true;
            GetTextBox(comboBox).Text = "something else";

            InvokeOnNavigationCloseModal(comboBox);

            Assert.False(comboBox.IsOpen);
            Assert.Equal("Apple", GetTextBox(comboBox).Text);
            Assert.Equal("Apple", comboBox.SelectedItem);
        }

        [Fact]
        public void EnterAfterTheFilterNarrowsBelowTheHighlight_CommitsTheRemainingMatchWithoutThrowing()
        {
            // Regression coverage: HighlightedIndex was only ever clamped at arrow-key time, so narrowing the filter
            // below it left it stale - Enter then ran SelectedIndex = HighlightedIndex straight into Selector's own
            // ArgumentOutOfRangeException guard.
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Apricot", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            var input = new FakeInputSystem();
            SimulateFocused(comboBox, input);

            GetTextBox(comboBox).Text = "ap";
            Assert.Equal(new[] { "Apple", "Apricot" }, InvokeGetFilteredDisplayTexts(comboBox));

            // Highlight the second match (index 1 of the two-item filtered view)...
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));

            // ...then narrow the list to a single item, leaving index 1 out of range.
            GetTextBox(comboBox).Text = "appl";
            Assert.Equal(new[] { "Apple" }, InvokeGetFilteredDisplayTexts(comboBox));

            var exception = Record.Exception(() => InvokeOnNavigationSelectElement(comboBox));

            Assert.Null(exception);
            Assert.False(comboBox.IsOpen);

            // The stale highlight is dropped rather than committed, so this takes the revert path and selects
            // nothing - re-highlighting inside the narrowed list is what commits the remaining match.
            Assert.Null(comboBox.SelectedItem);

            GetTextBox(comboBox).Text = "appl";
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));
            InvokeOnNavigationSelectElement(comboBox);

            Assert.Equal("Apple", comboBox.SelectedItem);
            Assert.Equal("Apple", GetTextBox(comboBox).Text);
        }

        [Fact]
        public void AfterCommit_TheFullItemListIsAvailableAgain()
        {
            // Regression coverage: every commit/revert path left base.ItemsSource narrowed to (essentially) the
            // single committed item, so reopening the popup showed one row until the user typed again.
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana", "Cherry" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            var input = new FakeInputSystem();
            SimulateFocused(comboBox, input);
            GetTextBox(comboBox).Text = "ban";
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));

            InvokeOnNavigationSelectElement(comboBox);

            Assert.Equal("Banana", comboBox.SelectedItem);
            Assert.Equal(new[] { "Apple", "Banana", "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));
        }

        [Fact]
        public void AfterEscapeRevert_TheFullItemListIsAvailableAgain()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana", "Cherry" } };
            comboBox.SelectedItem = "Apple";
            comboBox.IsOpen = true;
            GetTextBox(comboBox).Text = "ban";

            InvokeOnNavigationCloseModal(comboBox);

            Assert.Equal(new[] { "Apple", "Banana", "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));
        }

        [Fact]
        public void LiveCollectionChangesOnTheRealSource_ReachTheFilteredView()
        {
            // Regression coverage: base.ItemsSource only ever pointed at a throwaway filtered snapshot, so
            // ItemsControl's own INotifyCollectionChanged observation never watched the user's real collection -
            // mutating it after binding was silently ignored.
            var source = new ObservableCollection<string> { "Apple", "Banana" };
            var comboBox = new ComboBox { ItemsSource = source };

            source.Add("Cherry");

            Assert.Equal(new[] { "Apple", "Banana", "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));

            source.Remove("Banana");

            Assert.Equal(new[] { "Apple", "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));
        }

        [Fact]
        public void LiveCollectionChanges_RespectTheCurrentFilter()
        {
            var source = new ObservableCollection<string> { "Apple", "Banana" };
            var comboBox = new ComboBox { ItemsSource = source };
            GetTextBox(comboBox).Text = "an";

            source.Add("Mango");

            Assert.Equal(new[] { "Banana", "Mango" }, InvokeGetFilteredDisplayTexts(comboBox));
        }

        [Fact]
        public void FilteringWhileOpen_ResizesThePopupToTheNarrowedList()
        {
            // Popup geometry used to be computed once, at open, and never again - so a ComboBox whose filter cut a
            // 20-item list down to one kept rendering a full-height popup over mostly empty space.
            var comboBox = new ComboBox
            {
                ItemsSource = Enumerable.Range(0, 20).Select(i => (object)$"Item {i}").ToList(),
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var input = new FakeInputSystem();
            Canvas canvas = SimulateFocused(comboBox, input);
            comboBox.IsOpen = true;
            canvas.Render();
            int fullHeight = canvas.Overlays.Single().ActualBounds.Height;

            GetTextBox(comboBox).Text = "Item 7";
            canvas.Render();

            Assert.Equal(new[] { "Item 7" }, InvokeGetFilteredDisplayTexts(comboBox));
            int narrowedHeight = canvas.Overlays.Single().ActualBounds.Height;
            Assert.True(narrowedHeight < fullHeight, $"Expected the popup to shrink with the filtered list, got {narrowedHeight} vs {fullHeight}.");
        }

        [Fact]
        public void ClickingAPopupItem_CommitsCorrectly_WithoutAPriorArrowKeyHighlight()
        {
            // Regression: a real click is TouchDown then Tap, not Tap alone - existing tests only ever raised Tap
            // in isolation, which never exercised OnOutsideTouchDown. That handler treats a TouchDown outside both
            // `this` and popupRoot as "clicked outside, close the popup" - if a tapped item's own ancestor walk
            // doesn't correctly reach popupRoot, TouchDown closes (and unsubscribes) the popup a moment before the
            // Tap that was actually meant to select it, so the click silently does nothing.
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var input = new FakeInputSystem();
            Canvas canvas = SimulateFocused(comboBox, input);
            comboBox.IsOpen = true;
            canvas.Render();

            var bananaItem = canvas.Overlays.Single().EnumerateVisualSubtree().OfType<SelectorItem>().ElementAt(1);
            System.Drawing.Point tapPoint = new(bananaItem.ActualBounds.X + 5, bananaItem.ActualBounds.Y + 5);

            input.Events.Touch.RaiseTouchDown(tapPoint);
            input.Events.Touch.RaiseTap(new TouchInfo(tapPoint, 1));

            Assert.Equal("Banana", comboBox.SelectedItem);
            Assert.Equal("Banana", GetTextBox(comboBox).Text);
            Assert.False(comboBox.IsOpen);
        }

        [Fact]
        public void ClickingAnItem_AlreadyMatchingTheLastSelection_StillUpdatesTheText()
        {
            // Regression: SelectedIndex's own setter no-ops (never fires SelectionChanged at all) when given the
            // value it already has. ApplyFilter's own "restore the previous selection if the filtered list still
            // contains it" logic can already have re-selected the very item about to be clicked - e.g. select
            // Cherry, then filter down to text that only Cherry matches (narrowing through a no-match state and
            // back, as typing/editing naturally does) - so OnPopupItemTap's SelectedIndex assignment becomes a
            // silent no-op, and a sync that only runs off SelectionChanged never fires, leaving textBox.Text
            // showing the stale filter text instead of "Cherry" even though the click "worked" (IsOpen closes).
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana", "Cherry" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var input = new FakeInputSystem();
            Canvas canvas = SimulateFocused(comboBox, input);
            comboBox.SelectedItem = "Cherry";

            // Narrow through a no-match state, then back to something only Cherry matches - ApplyFilter's own
            // "restore the previous selection if still valid" logic re-selects Cherry here, well before any click.
            GetTextBox(comboBox).Text = "xyz";
            Assert.Empty(InvokeGetFilteredDisplayTexts(comboBox));
            GetTextBox(comboBox).Text = "ry";
            Assert.Equal(new[] { "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));
            Assert.Equal("Cherry", comboBox.SelectedItem);

            canvas.Render();
            var cherryItem = canvas.Overlays.Single().EnumerateVisualSubtree().OfType<SelectorItem>().Single();
            System.Drawing.Point tapPoint = new(cherryItem.ActualBounds.X + 5, cherryItem.ActualBounds.Y + 5);

            input.Events.Touch.RaiseTouchDown(tapPoint);
            input.Events.Touch.RaiseTap(new TouchInfo(tapPoint, 1));

            Assert.Equal("Cherry", comboBox.SelectedItem);
            Assert.Equal("Cherry", GetTextBox(comboBox).Text);
            Assert.False(comboBox.IsOpen);
        }

        [Fact]
        public void NaturalHeight_WithNoExplicitHeightSet_IsNotZero()
        {
            // Regression, two compounding root causes: (1) ComboBox's chrome Grid never gave its implicit single
            // row an explicit RowDefinition, and Grid.MeasureContent() always measures a Star track (which an
            // empty RowDefinitions collection implies) as 0 during measure - deferred to arrange time, matching
            // WPF's own Star semantics - so the whole chrome's measured height was always 0 regardless of what its
            // children needed. (2) Separately, the internal textBox never has its own FontFamily set, and
            // TextBox.ResolveFont() (unlike TextBlock's own three-step fallback) used to return null outright for
            // an empty FontFamily rather than falling back to FontSystem.DefaultFontFamily - so even once the Grid
            // row was fixed, the internal textBox itself still measured as zero-sized. Together, a ComboBox with
            // no explicit Height (the common case - see SelectorDemo) rendered as nothing at all.
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 240,
            };
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(comboBox);

            var builder = new IcyConfigurationBuilder();
            var renderContext = new Icy.Tests.Rendering.FakeRenderContext();
            var input = new FakeInputSystem();
            builder.ConfigureRendering(renderContext)
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");
            config.Fonts.DefaultFontFamily = "Airfool";
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(stack);
            canvas.Render();

            Assert.True(comboBox.ActualBounds.Height > 0, $"ComboBox bounds: {comboBox.ActualBounds}");
        }

        [Fact]
        public void OpenPopup_FollowsItsAnchor_WhenTheUIScaleChanges()
        {
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var config = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(Icy.Assets.AssetContext.ApplicationContext), new Icy.Tests.Rendering.FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(comboBox);
            canvas.Render();
            comboBox.IsOpen = true;
            canvas.Render();
            UIElement popup = canvas.Overlays.Single();
            Assert.Equal(600, popup.Margin.Left);

            // 800 px viewport at 2x = a 400-unit surface, so the right-aligned anchor moves to x = 200.
            config.Scaling.UserScale = 2f;
            canvas.Render();

            Assert.Equal(200, popup.Margin.Left);
        }

        [Fact]
        public void CodeBuiltEnumComboBox_WithoutItemTemplate_OpensAndShowsEachMembersName()
        {
            // Regression: ScalingDemo's code-built ComboBox over Enum.GetValues<UIScaleMode>() threw
            // "'ItemsControl' has no 'ItemTemplate' or 'ItemTemplateSelector' to build item 'None' from." on open.
            var comboBox = new ComboBox { ItemsSource = Enum.GetValues<DayOfWeek>(), Width = 240 };
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new Icy.Tests.Rendering.FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");
            config.Fonts.DefaultFontFamily = "Airfool";
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(comboBox);
            canvas.Render();

            comboBox.IsOpen = true;
            canvas.Render();

            string[] shown = [.. canvas.Overlays.Single().EnumerateVisualSubtree().OfType<SelectorItem>()
                .Select(item => item.EnumerateVisualSubtree().OfType<TextBlock>().First().Text)];
            Assert.Equal("Sunday", shown[0]);
            Assert.Equal("Monday", shown[1]);
        }

        [Fact]
        public void ThemedComboBox_InternalTextBox_DoesNotGetItsOwnBorderOrPadding()
        {
            // Regression: an implicit (keyless, type-targeted) style applies to ANY element of a matching type
            // that has no Style set by the time it attaches - see UIElement.Style's own remarks - and this
            // internal textBox is a genuine TextBox instance, so applying the default theme gave it its own
            // Background/BorderBrush/BorderThickness/Padding on top of this ComboBox's own chrome: a second,
            // smaller border floating inside the first, eating into the already-narrow Grid cell twice over.
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana" }, Width = 240 };
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new Icy.Tests.Rendering.FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets();
            var config = builder.Build().UseDefaultTheme();
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(comboBox);
            canvas.Render();

            var textBox = GetTextBox(comboBox);

            Assert.Equal(Thickness.Zero, textBox.Padding);
            Assert.Equal(Thickness.Zero, textBox.BorderThickness);
        }

        [Fact]
        public void SelectingAnItem_ByAnyMeans_UpdatesTextBoxContents()
        {
            // Regression: only the Enter-key commit path (OnNavigationSelectElement) used to push the selected
            // item's display text into the internal textBox - a mouse/touch commit (Selector.OnPopupItemTap sets
            // SelectedIndex directly, bypassing OnNavigationSelectElement entirely) left the box showing stale
            // filter text instead of the item that was actually picked, even though SelectedItem itself was
            // correct. Setting SelectedIndex/SelectedItem directly - what a popup click, or any other future
            // commit path, ultimately does - must update the visible text the same way Enter always has.
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana" } };

            comboBox.SelectedIndex = 1;

            Assert.Equal("Banana", GetTextBox(comboBox).Text);
        }

        [Fact]
        public void LosingFocus_WithAHighlightedItem_CommitsItJustLikeEnterWould()
        {
            // Regression: losing focus (Tab away, clicking elsewhere) used to do nothing - an item arrow-keyed
            // into HighlightedIndex but never confirmed with Enter left textBox.Text showing stale filter text,
            // instead of snapping to the highlighted item exactly like Enter would have.
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            var input = new FakeInputSystem();
            Canvas canvas = SimulateFocused(comboBox, input);
            GetTextBox(comboBox).Text = "ban";
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));

            canvas.Focus(null);

            Assert.Equal("Banana", comboBox.SelectedItem);
            Assert.Equal("Banana", GetTextBox(comboBox).Text);
            Assert.False(comboBox.IsOpen);
        }

        private static TextBox GetTextBox(ComboBox comboBox) =>
            (TextBox)typeof(ComboBox).GetField("textBox", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(comboBox)!;

        private static List<string> InvokeGetFilteredDisplayTexts(ComboBox comboBox)
        {
            var itemsControl = (Icy.UI.Controls.ItemsControl)comboBox;
            int count = (int)typeof(ItemsControl).GetProperty("ItemCount", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(itemsControl)!;
            var getItemAt = typeof(ItemsControl).GetMethod("GetItemAt", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var results = new List<string>();
            for (int i = 0; i < count; i++)
                results.Add((string)getItemAt.Invoke(itemsControl, [i])!);
            return results;
        }

        private static void InvokeOnNavigationSelectElement(ComboBox comboBox) => comboBox.OnActivate();

        private static void InvokeOnNavigationCloseModal(ComboBox comboBox) =>
            typeof(Selector).GetMethod("OnNavigationCloseModal", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(comboBox, [null, System.EventArgs.Empty]);

        private static void InvokeOnNavigationFocusChanging(ComboBox comboBox, System.Numerics.Vector2 direction)
        {
            // Arrows only move the highlight while the popup is open; a closed ComboBox lets them go.
            comboBox.IsOpen = true;
            comboBox.OnNavigate(direction);
        }

        private static Canvas SimulateFocused(ComboBox comboBox, FakeInputSystem input)
        {
            var assets = new Icy.Configuration.AssetConfiguration(Icy.Assets.AssetContext.ApplicationContext);
            var renderContext = new Icy.Tests.Rendering.FakeRenderContext();
            var config = new Icy.Configuration.IcyConfiguration(input, assets, renderContext, new Icy.Configuration.ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(comboBox);
            canvas.Render();
            canvas.Focus(GetTextBox(comboBox));
            return canvas;
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new Icy.Configuration.IcyConfiguration(
                new FakeInputSystem(),
                new Icy.Configuration.AssetConfiguration(Icy.Assets.AssetContext.ApplicationContext),
                new Icy.Tests.Rendering.FakeRenderContext(),
                new Icy.Configuration.ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }
    }
}
