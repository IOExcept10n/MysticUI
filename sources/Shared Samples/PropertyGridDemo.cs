// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Numerics;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Controls.PropertyGrid"/> (Phase 9 of the Tier-2 roadmap):
    /// every v1 editor type (string, bool, unranged and <see cref="RangeAttribute"/>-ranged numeric, enum,
    /// <see cref="Color"/>, and a <c>System.Numerics</c> vector) on one target, plus a button that swaps
    /// <see cref="PropertyGrid.Target"/> between two differently-shaped objects in place - the visually
    /// inspectable counterpart of <c>PropertyGridTests.Target_ReassignedToDifferentlyShapedObject_*</c>, which
    /// exercises the same reassignment with <see cref="ItemsControl.PoolingEnabled"/> off (see
    /// <see cref="Icy.UI.Controls.PropertyGrid"/>'s own remarks for why) purely through unit assertions.
    /// </summary>
    /// <remarks>
    /// Like <see cref="ColorPickerDemo"/>, this demo's <see cref="Character"/>/<see cref="WorldSettings"/> model
    /// types have no markup representation of their own - they're plain data POCOs the grid reflects over via
    /// <see cref="Icy.Data.PropertyGridEntry.EnumerateFor(object)"/>, wired into the loaded <c>Grid</c> from code
    /// after loading, same "no markup child-item syntax, wire from code after loading" convention as
    /// <see cref="ItemsControlDemo"/>/<see cref="WrapGridDemo"/>/<see cref="SelectorDemo"/>.
    /// </remarks>
    public static class PropertyGridDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="TabControlDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">PropertyGrid (Phase 9)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Every v1 editor type below. "Swap Target" re-points the grid at a differently-shaped object in place - watch for stale widgets from the previous target.</TextBlock>
                <Button x:Name="SwapButton" Margin="0,0,0,12" HorizontalAlignment="Left">Swap Target</Button>
                <ScrollViewer Width="420" Height="360" HorizontalAlignment="Left">
                  <PropertyGrid x:Name="Grid"/>
                </ScrollViewer>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>, pointing the loaded <c>Grid</c>
        /// <see cref="Icy.UI.Controls.PropertyGrid"/> at a freshly constructed <see cref="Character"/>, and
        /// wiring the loaded <c>SwapButton</c> to toggle <see cref="Icy.UI.Controls.PropertyGrid.Target"/>
        /// between that <see cref="Character"/> and a freshly constructed <see cref="WorldSettings"/> on every
        /// click.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration the loader resolves types, converters, and properties through. Its
        /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> is set to <paramref name="fontFamily"/>
        /// here, which is what gives the document's text a font.
        /// </param>
        /// <param name="fontFamily">
        /// The font family every text element resolves. Import it beforehand (see <c>FontSystem.ImportFont</c>) for
        /// text to actually render.
        /// </param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the markup language.</exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(Markup, nameof(PropertyGridDemo));

            var character = new Character();
            var world = new WorldSettings();

            PropertyGrid grid = root.FindRequiredControl<PropertyGrid>("Grid");
            grid.Target = character;

            bool showingCharacter = true;
            root.FindRequiredControl<Button>("SwapButton").Click += (_, _) =>
            {
                showingCharacter = !showingCharacter;
                grid.Target = showingCharacter ? character : world;
            };

            return root;
        }

        /// <summary>
        /// The classes a <see cref="Character"/> may belong to - just enough values to exercise the grid's enum
        /// editor (a <see cref="Icy.UI.Controls.ComboBox"/> over <see cref="Icy.Data.EnumValueCache.GetValues(Type)"/>).
        /// </summary>
        private enum CharacterClass
        {
            Warrior,
            Mage,
            Rogue,
        }

        /// <summary>
        /// The demo's primary <see cref="Icy.UI.Controls.PropertyGrid.Target"/> - a small POCO exercising every
        /// v1 editor type this feature supports: <see cref="Name"/> (string), <see cref="IsEnabled"/> (bool),
        /// <see cref="Level"/> (unranged numeric), <see cref="Health"/> (<see cref="RangeAttribute"/>-ranged
        /// numeric, so it gets the Slider+TextBox pairing), <see cref="Class"/> (enum), <see cref="TintColor"/>
        /// (<see cref="Color"/>), and <see cref="Position"/> (a <c>System.Numerics</c> vector).
        /// </summary>
        private sealed class Character
        {
            /// <summary>
            /// Gets or sets the character's name.
            /// </summary>
            [Category("Identity")]
            public string Name { get; set; } = "Aria";

            /// <summary>
            /// Gets or sets whether the character is currently active.
            /// </summary>
            [Category("Identity")]
            public bool IsEnabled { get; set; } = true;

            /// <summary>
            /// Gets or sets the character's level - an unranged numeric, so the grid shows a plain
            /// <see cref="Icy.UI.Controls.TextBox"/> editor for it (contrast <see cref="Health"/>).
            /// </summary>
            [Category("Stats")]
            public int Level { get; set; } = 5;

            /// <summary>
            /// Gets or sets the character's health, from 0 to 100 - a <see cref="RangeAttribute"/>-ranged
            /// numeric, so the grid shows a <see cref="Icy.UI.Controls.Slider"/>+<see cref="Icy.UI.Controls.TextBox"/>
            /// pair for it (contrast <see cref="Level"/>).
            /// </summary>
            [Category("Stats")]
            [Range(0, 100)]
            public int Health { get; set; } = 75;

            /// <summary>
            /// Gets or sets the character's class.
            /// </summary>
            [Category("Stats")]
            public CharacterClass Class { get; set; } = CharacterClass.Warrior;

            /// <summary>
            /// Gets or sets the color used to tint the character's appearance.
            /// </summary>
            [Category("Appearance")]
            public Color TintColor { get; set; } = Color.SkyBlue;

            /// <summary>
            /// Gets or sets the character's world position.
            /// </summary>
            [Category("Appearance")]
            public Vector3 Position { get; set; } = new(0f, 1f, 0f);
        }

        /// <summary>
        /// The demo's secondary <see cref="Icy.UI.Controls.PropertyGrid.Target"/> - deliberately shaped nothing
        /// like <see cref="Character"/> (different property count, names, and types), so swapping
        /// <see cref="Icy.UI.Controls.PropertyGrid.Target"/> to it makes any stale widget left over from
        /// <see cref="Character"/>'s rows (the pooling-off regression this demo exists to make visually
        /// inspectable - see the class remarks) obvious on sight.
        /// </summary>
        private sealed class WorldSettings
        {
            /// <summary>
            /// Gets or sets the current region's name.
            /// </summary>
            [Category("World")]
            public string RegionName { get; set; } = "Overworld";

            /// <summary>
            /// Gets or sets whether it is currently raining in the region.
            /// </summary>
            [Category("World")]
            public bool IsRaining { get; set; }

            /// <summary>
            /// Gets or sets the number of in-game days elapsed.
            /// </summary>
            [Category("World")]
            public int DayCount { get; set; } = 1;
        }
    }
}
