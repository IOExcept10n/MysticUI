using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.Rendering.Fonts;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TextBoxTests
    {
        [Fact]
        public void MeasureString_TrailingCharacterWithNoInk_StillContributesItsAdvanceWidth()
        {
            // Regression: SpriteFont.CalculateBounds computed a tight ink bounding box (the union of each glyph's
            // own visible Size) instead of the cursor's final advance position. A space glyph has Size=(0,0) by
            // design (there's no ink to draw) - for a space in the MIDDLE of a string this was invisible, because
            // the callback captures each glyph's own *starting* position (before its own advance), so the NEXT
            // glyph's starting position (already past the space) is what actually grew the box - the bug only
            // ever dropped the very last character's own advance, whatever it was. For a single trailing space
            // (nothing after it to reveal that missing advance), the measured width came out as exactly the
            // pre-text cursor position - here, zero. This affected both TextBox's own box sizing and its
            // caret-position math, since both route through this same MeasureString call.
            //
            // Measures the font directly (bypassing TextBox's own chrome/Padding, which would otherwise add a
            // constant offset that masks a "width == 0" assertion) for a precise, font-metric-independent check.
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            IFont font = config.Fonts.GetOrLoad(new FontInfo("Airfool", 16, FontStyle.Regular))!;
            var options = new FontRenderingOptions(
                Position: Vector2.Zero,
                Scale: null,
                Rotation: 0,
                Origin: Vector2.Zero,
                CharacterSpacing: 0,
                LineSpacing: 0,
                Color: Color.Black,
                Depth: 0,
                Effect: null);

            Vector2 singleSpaceSize = font.MeasureString(" ", options);

            Assert.True(singleSpaceSize.X > 0, $"A lone space must measure to its real (nonzero) advance width, not disappear entirely - got {singleSpaceSize.X}.");
        }

        [Fact]
        public void MeasureString_ContentWithNoDescender_StillReportsTheFontsFullLineHeight()
        {
            // Regression, same root cause as the width test above but on the vertical axis: min.Y/max.Y used to
            // grow only from actual glyph ink, so a string with none at all (a lone space - what TextBox measures
            // in place of a truly empty Text, see its own MeasureContent) reported zero height. A single line's
            // height must never depend on which glyphs happen to be present - it should match the font's own
            // Ascent-to-Descent metrics every time, exactly like TextBox.OnRender's caret-height calculation
            // already computes it. This is what made an empty TextBox (or a Dropdown before anything is selected,
            // or ComboBox's internal TextBox) collapse to a visibly shorter box than the same control holding
            // real text, instead of keeping a stable height throughout.
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            IFont font = config.Fonts.GetOrLoad(new FontInfo("Airfool", 16, FontStyle.Regular))!;
            var options = new FontRenderingOptions(
                Position: Vector2.Zero,
                Scale: null,
                Rotation: 0,
                Origin: Vector2.Zero,
                CharacterSpacing: 0,
                LineSpacing: 0,
                Color: Color.Black,
                Depth: 0,
                Effect: null);

            float expectedLineHeight = font.Metrics.Ascent - font.Metrics.Descent;
            Vector2 emptyLineSize = font.MeasureString(" ", options);

            Assert.True(
                emptyLineSize.Y >= expectedLineHeight - 1,
                $"Expected height around the font's own line height ({expectedLineHeight}), got {emptyLineSize.Y}.");
        }

        [Fact]
        public void MeasureAdvance_TrailingSpaceAfterAnOverhangingGlyph_StillAdvancesTheCursor()
        {
            // Regression: MeasureString/CalculateBounds intentionally report an ink bounding box - the tightest
            // box containing visible pixels - and a preceding glyph's own ink can extend further right than its
            // own advance width (Airfool's 'b' does). That means "ab" can already measure as wide as - or wider
            // than - "ab "'s true cursor position, so the earlier ink-based width fix (which only extended the
            // ink box up to the FINAL cursor position) never actually moved for a trailing space added after
            // such a glyph: the ink from 'b' was already past where the cursor would land. Ink bounds and cursor
            // advance are different questions; MeasureAdvance answers the second one exclusively, never clamped
            // by ink from earlier characters.
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            IFont font = config.Fonts.GetOrLoad(new FontInfo("Airfool", 16, FontStyle.Regular))!;
            var options = new FontRenderingOptions(
                Position: Vector2.Zero, Scale: null, Rotation: 0, Origin: Vector2.Zero,
                CharacterSpacing: 0, LineSpacing: 0, Color: Color.Black, Depth: 0, Effect: null);

            Vector2 ab = font.MeasureAdvance("ab", options);
            Vector2 abSpace = font.MeasureAdvance("ab ", options);

            Assert.True(abSpace.X > ab.X, $"ab={ab.X}, ab_space={abSpace.X}");
        }

        [Fact]
        public void Caret_MovesRight_WhenTrailingSpaceIsTyped()
        {
            // End-to-end regression for the same root cause as the MeasureAdvance test above: TextBox.OnRender
            // used to compute the caret's screen position via MeasureString (ink-based), so a user typing a
            // trailing space after a glyph that overhangs its own advance (like Airfool's 'b') would see the
            // caret not move at all - indistinguishable from the space having been silently dropped.
            var renderContext = new FakeRenderContext();
            var input = new FakeInputSystem();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(renderContext)
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            var textBox = new TextBox { FontFamily = "Airfool", FontSize = 16, Width = 300, Height = 28 };
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);

            input.Events.Text.RaiseTextInput("ab");
            renderContext.DrawCalls.Clear();
            canvas.Render();
            var caretBeforeSpace = renderContext.DrawCalls.Last(d => d.Texture == renderContext.WhiteTexture && d.Options.Destination.Width == 1);

            input.Events.Text.RaiseTextInput(" ");
            renderContext.DrawCalls.Clear();
            canvas.Render();
            var caretAfterSpace = renderContext.DrawCalls.Last(d => d.Texture == renderContext.WhiteTexture && d.Options.Destination.Width == 1);

            Assert.True(caretAfterSpace.Options.Destination.X > caretBeforeSpace.Options.Destination.X,
                $"Caret should move right after a trailing space. Before={caretBeforeSpace.Options.Destination.X}, After={caretAfterSpace.Options.Destination.X}");
        }

        [Fact]
        public void Caret_StaysWithinTheVisibleArea_WhenTextIsWiderThanTheBox()
        {
            // Regression/feature: TextBox had no horizontal scrolling at all - typing past the visible width just
            // let the caret's true (unscrolled) position run off past the box's own right edge, off-screen, with
            // no way to bring it back into view (matching a real text field's "keep typing, the box scrolls to
            // follow your cursor" behavior, which WPF's TextBox has and this one didn't).
            var builder = new IcyConfigurationBuilder();
            var renderContext = new FakeRenderContext();
            var input = new FakeInputSystem();
            builder.ConfigureRendering(renderContext)
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            var textBox = new TextBox
            {
                FontFamily = "Airfool",
                FontSize = 16,
                Width = 60,
                Height = 24,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);

            input.Events.Text.RaiseTextInput("Hello World Testing");
            renderContext.DrawCalls.Clear();
            canvas.Render();
            var caret = renderContext.DrawCalls.Last(d => d.Texture == renderContext.WhiteTexture && d.Options.Destination.Width == 1);

            int contentLeft = textBox.Padding.Left + textBox.BorderThickness.Left;
            int contentRight = textBox.ActualBounds.Width - textBox.Padding.Right - textBox.BorderThickness.Right;
            int caretXWithinControl = caret.Options.Destination.X - textBox.ActualBounds.X;

            Assert.True(
                caretXWithinControl >= contentLeft && caretXWithinControl <= contentRight,
                $"Caret should stay within the visible content area ({contentLeft}..{contentRight}) - was at {caretXWithinControl}.");
        }

        [Fact]
        public void Caret_NavigatingAwayFromTheEnd_KeepsAMarginOnBothSides()
        {
            // Feature: navigating mid-string (not just typing at the tail, covered by the test above) should keep
            // a small margin of already-visible characters on both sides of the caret, not just snap it flush to
            // an edge - so the user can see a couple of characters they're about to type over/delete, matching
            // how a WPF TextBox scrolls.
            var builder = new IcyConfigurationBuilder();
            var renderContext = new FakeRenderContext();
            var input = new FakeInputSystem();
            builder.ConfigureRendering(renderContext)
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            var textBox = new TextBox
            {
                FontFamily = "Airfool",
                FontSize = 16,
                Width = 60,
                Height = 24,
                Padding = new Thickness(2),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);

            input.Events.Text.RaiseTextInput("Hello World Testing");
            for (int i = 0; i < 10; i++)
                input.Keyboard.RaiseKeyDown(Keys.Left);
            renderContext.DrawCalls.Clear();
            canvas.Render();
            var caret = renderContext.DrawCalls.Last(d => d.Texture == renderContext.WhiteTexture && d.Options.Destination.Width == 1);

            int contentLeft = textBox.ActualBounds.X + textBox.Padding.Left + textBox.BorderThickness.Left;
            int contentRight = textBox.ActualBounds.Right - textBox.Padding.Right - textBox.BorderThickness.Right;
            int caretX = caret.Options.Destination.X;

            Assert.True(
                caretX > contentLeft && caretX < contentRight,
                $"Caret at a mid-string position should sit with room on both sides ({contentLeft}..{contentRight}), not flush against an edge - was at {caretX}.");
        }

        [Fact]
        public void MeasureContent_WithNoFontFamilySet_FallsBackToTheConfiguredDefault()
        {
            // Regression: unlike TextBlock.ResolveFont's three-step fallback (own FontFamily, then
            // FontSystem.DefaultFontFamily, then FallbackFont), TextBox.ResolveFont used to return null outright
            // whenever FontFamily was left unset - which ComboBox's internal text box always does. That made the
            // internal text box (and therefore, before its own chrome-layout fix, the whole ComboBox) render no
            // text, no caret, and measure as zero-sized even with a perfectly good document-wide default font.
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");
            config.Fonts.DefaultFontFamily = "Airfool";

            var textBox = new TextBox { Text = "Hello", HorizontalAlignment = HorizontalAlignment.Left };
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(textBox);
            canvas.Render();

            Assert.True(textBox.ActualBounds.Width > 0, $"Expected nonzero width from the fallback font, got {textBox.ActualBounds.Width}.");
        }

        [Fact]
        public void MeasureContent_ReservesRoomForPaddingAboveAndBelowTheTextLine_NotJustTheLarger()
        {
            // Regression: MeasureContent computed Height as Math.Max(chromeSize.Height, textSize.Y) - unlike
            // Width, which correctly SUMS chromeSize.Width + textSize.X. chromeSize already includes this
            // element's own Padding/BorderThickness (see Control.MeasureContent => Chrome.Measure()); Max'ing it
            // against the text's own line height means the padding above the text and the padding below it are
            // never BOTH reserved once the text's line height alone exceeds chromeSize.Height (the common case
            // for any real font) - the box measures only as tall as the taller of the two, so the text (drawn at
            // a fixed Padding.Top + BorderThickness.Top offset from the top) overflows past the box's own bottom
            // edge by roughly Padding.Bottom + BorderThickness.Bottom. Visually: text bleeding out of/below its
            // own TextBox.
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            var config = builder.Build();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            var textBox = new TextBox
            {
                FontFamily = "Airfool",
                FontSize = 16,
                Text = "Hello",
                Padding = new Thickness(6, 4),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(textBox);
            canvas.Render();

            IFont font = config.Fonts.GetOrLoad(new FontInfo("Airfool", 16, FontStyle.Regular))!;
            float lineHeight = font.Metrics.Ascent - font.Metrics.Descent;
            float textBottom = (textBox.Padding.Top + textBox.BorderThickness.Top) + lineHeight;
            float reservedBottom = textBox.Padding.Bottom + textBox.BorderThickness.Bottom;

            Assert.True(
                textBox.ActualBounds.Height >= textBottom + reservedBottom - 1,
                $"Text (ending at y={textBottom}) plus the bottom padding/border ({reservedBottom}) should fit inside the box - box height was only {textBox.ActualBounds.Height}.");
        }

        private static (Canvas Canvas, FakeInputSystem Input, TextBox TextBox) CreateFocusedTextBox()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            var textBox = new TextBox { Width = 100, Height = 20 };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);
            return (canvas, input, textBox);
        }

        [Fact]
        public void Text_SetNull_CoercesToEmptyString()
        {
            var textBox = new TextBox { Text = null! };

            Assert.Equal(string.Empty, textBox.Text);
        }

        [Fact]
        public void Focus_EnablesTextInput_Unfocus_DisablesIt()
        {
            var (canvas, input, textBox) = CreateFocusedTextBox();

            Assert.True(input.Events.Text.IsTextInputEnabled);

            canvas.Focus(null);

            Assert.False(input.Events.Text.IsTextInputEnabled);
        }

        [Fact]
        public void TextInput_WhileFocused_AppendsToText()
        {
            var (_, input, textBox) = CreateFocusedTextBox();

            input.Events.Text.RaiseTextInput("abc");

            Assert.Equal("abc", textBox.Text);
        }

        [Fact]
        public void TextInput_WhileNotFocused_IsIgnored()
        {
            var (canvas, input, textBox) = CreateFocusedTextBox();
            canvas.Focus(null);

            input.Events.Text.RaiseTextInput("abc");

            Assert.Equal(string.Empty, textBox.Text);
        }

        [Fact]
        public void LeftArrow_MovesCaretForSubsequentInsert()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("ac");

            input.Keyboard.RaiseKeyDown(Keys.Left);
            input.Events.Text.RaiseTextInput("b");

            Assert.Equal("abc", textBox.Text);
        }

        [Fact]
        public void Backspace_RemovesCharacterBeforeCaret()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("abc");

            input.Keyboard.RaiseKeyDown(Keys.Back);

            Assert.Equal("ab", textBox.Text);
        }

        [Fact]
        public void TextInput_ControlCharacter_IsIgnored()
        {
            // Regression: Windows' WM_CHAR (which MonoGame's Window.TextInput is backed by) fires for control
            // characters too, not just printable ones - Backspace produces '\b', Enter '\r', Tab '\t'. Those
            // already have dedicated handling via the raw KeyDown event; letting them through here as "typed
            // text" would splice a literal control character into the text and double-process a single key press.
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("ab");

            input.Events.Text.RaiseTextInput("\b");

            Assert.Equal("ab", textBox.Text);
        }

        [Fact]
        public void Backspace_Twice_RemovesTwoCharactersNotOne()
        {
            // Regression: Text's setter clamps caretIndex to the new (shorter) length as a side effect, and
            // Keys.Back used to decrement caretIndex again on top of that - a no-op bug at the end of the text
            // (the common case) drove the caret two positions left per backspace instead of one, so a second
            // backspace would silently miss a character instead of removing the next one.
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("abc");

            input.Keyboard.RaiseKeyDown(Keys.Back);
            input.Keyboard.RaiseKeyDown(Keys.Back);

            Assert.Equal("a", textBox.Text);
        }

        [Fact]
        public void Backspace_RepeatedlyToEmpty_DoesNotThrow()
        {
            // Regression: backspacing the last character used to drive caretIndex to -1 (crashed on Stride).
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("ab");

            input.Keyboard.RaiseKeyDown(Keys.Back);
            input.Keyboard.RaiseKeyDown(Keys.Back);
            input.Keyboard.RaiseKeyDown(Keys.Back);

            Assert.Equal(string.Empty, textBox.Text);
        }

        [Fact]
        public void Backspace_ThenInsert_InsertsAtEndNotOneShort()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("abc");

            input.Keyboard.RaiseKeyDown(Keys.Back);
            input.Events.Text.RaiseTextInput("z");

            Assert.Equal("abz", textBox.Text);
        }

        [Fact]
        public void Delete_RemovesCharacterAfterCaret()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("abc");
            input.Keyboard.RaiseKeyDown(Keys.Left);
            input.Keyboard.RaiseKeyDown(Keys.Left);
            input.Keyboard.RaiseKeyDown(Keys.Left);

            input.Keyboard.RaiseKeyDown(Keys.Delete);

            Assert.Equal("bc", textBox.Text);
        }

        [Fact]
        public void Home_MovesCaretToStartForSubsequentInsert()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("bc");

            input.Keyboard.RaiseKeyDown(Keys.Home);
            input.Events.Text.RaiseTextInput("a");

            Assert.Equal("abc", textBox.Text);
        }

        [Fact]
        public void End_MovesCaretToEndForSubsequentInsert()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            input.Events.Text.RaiseTextInput("ab");
            input.Keyboard.RaiseKeyDown(Keys.Home);

            input.Keyboard.RaiseKeyDown(Keys.End);
            input.Events.Text.RaiseTextInput("c");

            Assert.Equal("abc", textBox.Text);
        }

        [Fact]
        public void TextChanged_FiresOnEdit()
        {
            var (_, input, textBox) = CreateFocusedTextBox();
            int raisedCount = 0;
            textBox.TextChanged += (_, _) => raisedCount++;

            input.Events.Text.RaiseTextInput("a");

            Assert.Equal(1, raisedCount);
        }
    }
}
