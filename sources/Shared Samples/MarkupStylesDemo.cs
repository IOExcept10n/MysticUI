// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Runtime.CompilerServices;
using Icy.Animations;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen exercising every M4 (Phase 9) markup feature end to end: an implicit (keyless)
    /// <see cref="Style"/>, a hover <see cref="VisualState"/> transition declared via
    /// <see cref="Style.StateGroups"/>, a <see cref="Timeline"/> resource played on a button click, and a
    /// <see cref="ResourceDictionary"/> merged in from a real bundled file via <c>Source=</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Linked into both <c>MonoGame Sample</c> and <c>Stride Sample</c> like the other shared demos - markup and
    /// styling are pure core-library concerns, so this file needs no per-engine variant.
    /// </para>
    /// <para>
    /// <b>Why the merge isn't literally nested under <c>&lt;Border.Resources&gt;</c>:</b> the task brief that
    /// produced this file expected <c>&lt;Panel.Resources&gt;</c> to directly contain a
    /// <c>&lt;ResourceDictionary.MergedDictionaries&gt;</c> property element. That was tried first and does not
    /// work with the loader as implemented: <c>MarkupLoader.ApplyPropertyElement</c> always routes a property
    /// whose current value is dictionary-typed (which <see cref="UIElement.Resources"/> always is - its getter
    /// eagerly allocates) through the keyed-entries population path, which has no awareness of nested property
    /// elements - it either throws "Unknown type 'ResourceDictionary.MergedDictionaries'" (when that element
    /// appears as a raw sibling) or "needs an x:Key" (when wrapped in a bare <c>&lt;ResourceDictionary&gt;</c>).
    /// <c>&lt;ResourceDictionary.MergedDictionaries&gt;</c> is only recognized where <c>ApplyChildren</c> itself
    /// dispatches per-child property-element checks against the object being built - which happens for a
    /// document's own root (see <c>ResourceDictionaryMergingTests</c>), not for a value being populated into an
    /// already-non-null property. This was confirmed empirically (two throwaway xunit probes reproducing exactly
    /// the brief's described shape, both failing as predicted, not committed) rather than assumed from reading
    /// alone. Fixing this would mean changing <c>MarkupLoader</c>'s production logic, which is out of this task's
    /// charter (sample/glue code only) - so instead, <see cref="ResourcesMarkup"/> is its own small
    /// <see cref="ResourceDictionary"/> document (the one shape that <em>does</em> support
    /// <see cref="ResourceDictionary.MergedDictionaries"/> in markup), and <see cref="Build(IcyConfiguration, string)"/>
    /// merges it onto the visual tree's root in the one line of C# <see cref="UIElement.Resources"/>'s
    /// setter-less design requires anyway. Flagged here for whoever picks up markup work next - see also the M4
    /// design spec.
    /// </para>
    /// <para>
    /// <b>The registration gap:</b> a <c>ResourceDictionary Source="..."</c> loads through the real asset pipeline
    /// (<see cref="Icy.Assets.IAssetResolver.LoadAsset{T}"/>), which needs a
    /// <see cref="ResourceDictionaryImporter"/> registered for it to find. Neither engine's default configuration
    /// registers one - <c>MonoGameBuildingExtensions.WithDefaultMonoGameConfiguration</c> and
    /// <c>StrideBuildingExtensions.WithDefaultStrideConfiguration</c> each only add their own <c>TextureImporter</c>
    /// - so <see cref="Build(IcyConfiguration, string)"/> registers it itself, the same way
    /// <c>NavigationServiceTests.CreateAttachedFrame()</c> registers <see cref="MarkupImporter"/> for its own tests.
    /// </para>
    /// </remarks>
    public static class MarkupStylesDemo
    {
        /// <summary>
        /// Tracks which <see cref="IcyConfiguration"/>s <see cref="Build(IcyConfiguration, string)"/> has already
        /// registered a <see cref="ResourceDictionaryImporter"/> against, so calling it twice against the same
        /// configuration doesn't grow the importer list a second time (harmless either way - see
        /// <see cref="Build(IcyConfiguration, string)"/>'s remarks - but there's no reason to bother).
        /// </summary>
        private static readonly ConditionalWeakTable<IcyConfiguration, object> RegisteredConfigurations = [];

        /// <summary>
        /// The markup this demo loads for its visible content, kept inline so the sample stays self-contained -
        /// same convention as <see cref="MarkupDemo.Markup"/>. Its two buttons are both plain <c>&lt;Button&gt;</c>
        /// elements with no <c>Style=</c> of their own; everything they end up looking like comes from
        /// <see cref="ResourcesMarkup"/>, merged onto the root by <see cref="Build(IcyConfiguration, string)"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">

                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Styling &amp; animation, declared in markup (M4)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Hover the first button - the hover transition comes from an implicit Style merged in from a small resource document. Click it to play a Timeline resource. The second button's own Style comes from a real bundled file (AccentTheme.xml), merged two levels deep.</TextBlock>

                <Button x:Name="pulseButton" Padding="12,6" HorizontalAlignment="Left">Hover me, then click to pulse</Button>
                <Button x:Name="accentButton" Padding="12,6" HorizontalAlignment="Left" Margin="0,10,0,0">Styled from AccentTheme.xml</Button>

              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// A standalone <see cref="ResourceDictionary"/> document: an implicit (keyless) <c>&lt;Style
        /// TargetType="Button"&gt;</c> with a <c>Style.StateGroups</c> hover transition, a <c>Timeline</c>
        /// resource, and a <c>ResourceDictionary.MergedDictionaries</c> entry pulling in
        /// <c>Resources/Themes/AccentTheme.xml</c> - a real bundled file, loaded through the asset pipeline via
        /// <c>Source=</c> (Task 6's design). This is the one document shape that actually supports
        /// <c>MergedDictionaries</c> as markup - see this type's own remarks for why it isn't nested directly
        /// under <c>&lt;Border.Resources&gt;</c> instead.
        /// </summary>
        private const string ResourcesMarkup =
            """
            <ResourceDictionary>
              <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Themes/AccentTheme.xml"/>
              </ResourceDictionary.MergedDictionaries>

              <Style TargetType="Button" Background="#FF3C64C8">
                <Style.StateGroups>
                  <VisualStateGroup Name="CommonStates">
                    <VisualState Name="Hovered" State="Hovered" Duration="0:0:0.2" Easing="EaseOutCubic" Background="#FF5A87E6"/>
                  </VisualStateGroup>
                </Style.StateGroups>
              </Style>

              <Timeline x:Key="PulseTimeline" TargetProperty="Opacity" Duration="0:0:1">
                <AnimationKeyframe Offset="0" Value="0.5"/>
                <AnimationKeyframe Offset="1" Value="1.0"/>
              </Timeline>
            </ResourceDictionary>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/> and merging <see cref="ResourcesMarkup"/>
        /// (which itself merges in the real bundled <c>Resources/Themes/AccentTheme.xml</c>) onto it.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration the loader resolves types, converters, and properties through. Its
        /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> is set to <paramref name="fontFamily"/>
        /// here, and a <see cref="ResourceDictionaryImporter"/> is registered against its
        /// <see cref="Icy.Assets.IAssetResolver"/> if one isn't already - see this type's remarks for why that's
        /// necessary on both engines' default configuration.
        /// </param>
        /// <param name="fontFamily">
        /// The font family every text element resolves. Import it beforehand (see <c>FontSystem.ImportFont</c>) for
        /// text to actually render.
        /// </param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">
        /// A document is malformed, or <c>Resources/Themes/AccentTheme.xml</c> can't be found - either would be a
        /// bug in this file or its bundled resource, not caller error.
        /// </exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            if (!RegisteredConfigurations.TryGetValue(configuration, out _))
            {
                configuration.Assets.AssetResolver.RegisterImporter(new ResourceDictionaryImporter(configuration));
                RegisteredConfigurations.Add(configuration, configuration);
            }

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(Markup, nameof(MarkupStylesDemo));

            // UIElement.Resources has no setter (only Add-style population), so merging a whole loaded dictionary
            // onto the root can only happen here, in code - see this type's remarks for why that's also the only
            // place the MergedDictionaries markup itself could go.
            object loadedResources = loader.LoadObject(ResourcesMarkup, $"{nameof(MarkupStylesDemo)}.Resources");
            root.Resources.MergedDictionaries.Add((ResourceDictionary)loadedResources);

            // pulseButton resolves its implicit Style (and the Style's hover VisualState transition) on its own
            // once it attaches to a Canvas - see UIElement.OnAttached/ResolveImplicitStyle. All that's left to wire
            // by hand is finding the PulseTimeline resource and playing it on click.
            Button pulseButton = root.FindRequiredControl<Button>("pulseButton");
            if (root.Resources.TryGetValue("PulseTimeline", out var timelineResource) && timelineResource is Timeline pulseTimeline)
                pulseButton.Click += (_, _) => pulseButton.Animate(pulseTimeline);

            // Proves the merge chain resolves end-to-end: "AccentButtonStyle" lives in the bundled AccentTheme.xml,
            // merged into ResourcesMarkup's own dictionary, which is in turn merged into root's - two hops deep,
            // through a real file loaded via the asset pipeline both times.
            Button accentButton = root.FindRequiredControl<Button>("accentButton");
            if (root.Resources.TryGetValue("AccentButtonStyle", out var accentStyleResource) && accentStyleResource is Style accentStyle)
                accentButton.Style = accentStyle;

            return root;
        }
    }
}
