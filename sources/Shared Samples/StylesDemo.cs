// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Configuration;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;

namespace Icy.SharedSamples
{
    /// <summary>
    /// Builds a single scrollable page exercising the styling system: dictionary-based <see cref="Style"/>, the
    /// strongly-typed fluent <see cref="Style{TTarget}"/> builder, state-driven styling via
    /// <see cref="VisualStateGroup"/>/<see cref="VisualState{TTarget}"/>, and style inheritance via
    /// <see cref="Style.BasedOn"/>/<see cref="Style{TTarget}.InheritsFrom(Style)"/>.
    /// </summary>
    /// <remarks>
    /// Linked into both <c>MonoGame Sample</c> and <c>Stride Sample</c> (see each project's <c>.csproj</c>) rather
    /// than compiled into either one directly, so the same demo runs identically on both engine backends - same
    /// pattern as <see cref="ControlsDemo"/>. Only depends on the engine-independent <c>Icy.*</c> namespaces.
    /// </remarks>
    public static class StylesDemo
    {
        /// <summary>
        /// Builds the demo page's root element.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration to use. Only its <see cref="IcyConfiguration.Fonts"/> service is consulted
        /// here (to check <paramref name="fontFamily"/> is worth using), but callers should have imported
        /// <paramref name="fontFamily"/> beforehand (see <c>FontSystem.ImportFont</c>) for any text to render.
        /// </param>
        /// <param name="fontFamily">The font family every text-displaying control in the demo is set to use.</param>
        /// <returns>
        /// The root element to add to a <see cref="Canvas"/> (e.g. <c>canvas.Add(StylesDemo.Build(...))</c>).
        /// Sized to fill its container - give the canvas (or whatever it's added to) real screen dimensions.
        /// </returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            _ = configuration;

            var content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            content.Children.Add(CreateLabel("IcyUI Styling Demo", fontFamily, 28));
            content.Children.Add(CreateLabel("Style, Style<T>, VisualStateGroup, and BasedOn/InheritsFrom - hover, press, and Tab to the buttons below.", fontFamily, 14));
            content.Children.Add(BuildDictionaryStyleSection(fontFamily));
            content.Children.Add(BuildFluentStyleSection(fontFamily));
            content.Children.Add(BuildStateDrivenSection(fontFamily));
            content.Children.Add(BuildInheritanceSection(fontFamily));

            return new ScrollViewer
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
        }

        private static UIElement BuildDictionaryStyleSection(string fontFamily)
        {
            // The loose, string-keyed dictionary path - what markup/XML authoring will eventually target too.
            var style = new Style(typeof(Button));
            style.Setters["Background"] = new SolidColorBrush(Color.FromArgb(255, 100, 60, 170));
            style.Setters["BorderBrush"] = new SolidColorBrush(Color.White);
            style.Setters["BorderThickness"] = new Thickness(2);

            var button = new Button
            {
                Content = CreateLabel("Dictionary Style", fontFamily),
                Padding = new Thickness(12, 0),
                Style = style,
            };

            return CreateSection("Style (dictionary-based)", fontFamily, button);
        }

        private static UIElement BuildFluentStyleSection(string fontFamily)
        {
            // The strongly-typed, compile-time-checked path - both this and the dictionary path above write into
            // the same underlying Style.Setters, so they're fully interchangeable; this is just nicer to author.
            var style = new Style<Button>()
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 40, 140, 100)))
                .Set(x => x.BorderBrush, new SolidColorBrush(Color.White))
                .Set(x => x.BorderThickness, new Thickness(2));

            var button = new Button
            {
                Content = CreateLabel("Fluent Style<T>", fontFamily),
                Padding = new Thickness(12, 0),
                Style = style,
            };

            return CreateSection("Style<T> (fluent, compile-time-checked)", fontFamily, button);
        }

        private static UIElement BuildStateDrivenSection(string fontFamily)
        {
            var buttonStates = new VisualStateGroup("CommonStates");
            buttonStates.States.Add(new VisualState<Button>("Normal", ControlState.Normal)
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 60, 100, 200))));
            buttonStates.States.Add(new VisualState<Button>("Hovered", ControlState.Hovered)
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 90, 135, 230))));
            buttonStates.States.Add(new VisualState<Button>("Pressed", ControlState.Pressed)
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 35, 65, 145))));
            buttonStates.States.Add(new VisualState<Button>("Focused", ControlState.Focused)
                .Set(x => x.BorderBrush, new SolidColorBrush(Color.Yellow))
                .Set(x => x.BorderThickness, new Thickness(2)));

            var themedButton = new Button
            {
                Content = CreateLabel("Hover / Press / Tab to me", fontFamily),
                Padding = new Thickness(12, 0),
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 100, 200)),
            };
            themedButton.RegisterStateGroup(buttonStates);

            // A themed CheckBox - CheckBox is deliberately just a ToggleButton subtype with no built-in visual
            // (see its own doc comment) precisely so a style like this one is what gives it checkbox appearance.
            var checkStates = new VisualStateGroup("CheckStates");
            checkStates.States.Add(new VisualState<CheckBox>("Normal", ControlState.Normal)
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 60, 60, 70))));
            checkStates.States.Add(new VisualState<CheckBox>("Checked", ControlState.Checked)
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 60, 165, 95))));

            var themedCheckBox = new CheckBox
            {
                Content = CreateLabel("Themed CheckBox", fontFamily),
                Padding = new Thickness(12, 0),
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 70)),
            };
            themedCheckBox.RegisterStateGroup(checkStates);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(themedButton);
            row.Children.Add(themedCheckBox);

            return CreateSection("VisualStateGroup (state-driven: hover / press / focus / checked)", fontFamily, row);
        }

        private static UIElement BuildInheritanceSection(string fontFamily)
        {
            var baseStyle = new Style<Button>()
                .Set(x => x.Padding, new Thickness(14, 8))
                .Set(x => x.BorderThickness, new Thickness(2))
                .Set(x => x.BorderBrush, new SolidColorBrush(Color.White));

            var primaryStyle = new Style<Button>()
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 60, 100, 200)))
                .InheritsFrom(baseStyle);

            var dangerStyle = new Style<Button>()
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 190, 60, 60)))
                .InheritsFrom(baseStyle);

            var primaryButton = new Button
            {
                Content = CreateLabel("Primary", fontFamily),
                Style = primaryStyle,
                Margin = new Thickness(0, 0, 10, 0),
            };
            var dangerButton = new Button
            {
                Content = CreateLabel("Danger", fontFamily),
                Style = dangerStyle,
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(primaryButton);
            row.Children.Add(dangerButton);

            return CreateSection("BasedOn / InheritsFrom (shared base style, per-variant overrides)", fontFamily, row);
        }

        private static Border CreateSection(string title, string fontFamily, UIElement body)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(CreateLabel(title, fontFamily, 18));
            stack.Children.Add(body);

            return new Border
            {
                Child = stack,
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 16),
                Background = new SolidColorBrush(Color.FromArgb(255, 38, 38, 44)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 70, 70, 82)),
                BorderThickness = new Thickness(1),
            };
        }

        private static TextBlock CreateLabel(string text, string fontFamily, float fontSize = 16) => new()
        {
            Text = text,
            FontFamily = fontFamily,
            FontSize = fontSize,
            Foreground = Color.WhiteSmoke,
            Margin = new Thickness(0, 0, 0, 6),
        };
    }
}
