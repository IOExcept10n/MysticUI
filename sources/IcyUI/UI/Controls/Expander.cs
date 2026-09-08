// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Markup;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A collapsible region: a clickable <see cref="Header"/> row that shows/hides arbitrary
    /// <see cref="Content"/> below it, with an animated expand/collapse transition driven entirely by the
    /// existing themed <see cref="Styles.VisualState"/> system (see <see cref="ExpansionProgress"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clickable header is a composed <see cref="ExpanderHeader"/> (a plain <see cref="ToggleButton"/>
    /// subtype), exposed as a named template part - <c>PART_Header</c> - the same way <see cref="Slider"/>
    /// exposes <c>PART_Thumb</c> and <see cref="SplitPane"/> exposes <c>PART_Divider</c>. All of the header's
    /// hover/press/click/checked behavior comes from <see cref="ToggleButton"/> for free; <see cref="Expander"/>
    /// itself never handles pointer input directly.
    /// </para>
    /// <para>
    /// <see cref="Content"/> lives outside <see cref="Control.Chrome"/>, managed directly by
    /// <see cref="Expander"/> (mirrors <see cref="SplitPane.First"/>/<see cref="SplitPane.Second"/>) - needed
    /// because <see cref="Control.Background"/>/etc.'s hard <c>(Border)Chrome</c> cast means the single
    /// <c>Chrome.Child</c> slot can only hold one more visual, and that slot is already the header.
    /// </para>
    /// </remarks>
    [ContentProperty(nameof(Content))]
    public class Expander : Control
    {
        private readonly ExpanderHeader defaultHeader;
        private ExpanderHeader headerToggle;
        private UIElement? headerContent;
        private UIElement? content;
        private bool isExpanded;
        private float expansionProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="Expander"/> class.
        /// </summary>
        public Expander()
        {
            defaultHeader = new ExpanderHeader();
            headerToggle = defaultHeader;
            headerToggle.IsCheckedChanged += HeaderToggle_IsCheckedChanged;
            headerToggle.VerticalAlignment = VerticalAlignment.Top;

            // Safe here, at construction: Chrome is always the default Border until/unless Template is set
            // later, in which case OnApplyTemplate re-wires (or re-attaches) headerToggle appropriately.
            ((Border)Chrome).Child = headerToggle;
        }

        /// <summary>
        /// Occurs when <see cref="IsExpanded"/> changes.
        /// </summary>
        public event EventHandler? IsExpandedChanged;

        /// <summary>
        /// Gets or sets the element displayed in the always-visible header row, before the collapsible
        /// <see cref="Content"/>.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? Header
        {
            get => headerContent;
            set
            {
                if (headerContent == value)
                    return;
                headerContent = value;
                headerToggle.Content = value;
            }
        }

        /// <summary>
        /// Gets or sets the element shown/hidden below <see cref="Header"/> as <see cref="IsExpanded"/>
        /// toggles.
        /// </summary>
        /// <remarks>
        /// Skipped entirely (no measure, arrange, draw, or hit-test) while fully collapsed and idle - see
        /// <see cref="ExpansionProgress"/>.
        /// </remarks>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? Content
        {
            get => content;
            set
            {
                if (content == value)
                    return;
                if (content != null)
                {
                    content.Parent = null;
                    content.Canvas = null;
                }

                content = value;
                if (content != null)
                {
                    content.Parent = this;
                    content.Canvas = Canvas;
                    content.ClipToBounds = true;
                    content.IsVisible = ExpansionProgress > 0;
                }

                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="Content"/> is shown.
        /// </summary>
        /// <remarks>
        /// Setting this sets/clears <see cref="Styles.ControlState.Expanded"/>, which a themed
        /// <see cref="Styles.VisualState"/> transition uses to animate <see cref="ExpansionProgress"/> from
        /// <c>0</c> to <c>1</c> (or back) - see the class remarks. Mirrors <see cref="ToggleButton.IsChecked"/>'s
        /// own shape, and is kept two-way synchronized with the internal header toggle's own
        /// <see cref="ToggleButton.IsChecked"/> (clicking the header sets this; setting this also checks/unchecks
        /// the header).
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (!SetProperty(ref isExpanded, value))
                    return;
                ControlState = value ? ControlState | ControlState.Expanded : ControlState & ~ControlState.Expanded;
                headerToggle.IsChecked = value;
                IsExpandedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets how far along the expand/collapse transition <see cref="Content"/> is, from
        /// <c>0</c> (fully collapsed) to <c>1</c> (fully expanded).
        /// </summary>
        /// <remarks>
        /// A themable data slot (mirrors <see cref="CheckBox.CheckBrush"/>/<see cref="Slider.ThumbBrush"/>'s
        /// own shape) - normally driven by a themed <see cref="Styles.VisualState"/> transition on
        /// <see cref="Styles.ControlState.Expanded"/> rather than set directly, though nothing prevents a
        /// custom theme (or an untemplated caller) from doing so for an instant, non-animated toggle. Drives
        /// <see cref="Content"/>'s own <see cref="UIElement.IsVisible"/>, and how much of it
        /// <see cref="ArrangeContent"/> reveals.
        /// </remarks>
        [Category("Appearance")]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public float ExpansionProgress
        {
            get => expansionProgress;
            set
            {
                if (SetProperty(ref expansionProgress, float.Clamp(value, 0, 1)))
                {
                    if (content != null)
                        content.IsVisible = expansionProgress > 0;
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            yield return Chrome;
            if (Content != null)
                yield return Content;
        }

        /// <inheritdoc/>
        protected override void OnRender(Icy.Rendering.IRenderContext context)
        {
            Chrome.Draw(context);
            if (Content?.IsVisible == true)
                Content.Draw(context);
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            Size headerSize = headerToggle.Measure();
            if (Content?.IsVisible != true)
                return headerSize;

            Size contentSize = Content.Measure();
            int revealedHeight = (int)(contentSize.Height * ExpansionProgress);
            return new Size(Math.Max(headerSize.Width, contentSize.Width), headerSize.Height + revealedHeight);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Deliberately does <em>not</em> call <c>headerToggle.Arrange(...)</c> directly - <c>headerToggle</c>
        /// is structurally <see cref="Control.Chrome"/>'s own child (both when templated, via the template's
        /// own <c>PART_Header</c> nested inside its root element, and untemplated, via
        /// <c>((Border)Chrome).Child</c>), so <see cref="Control.Chrome"/>'s own arrange pass below would
        /// immediately re-arrange it anyway - and since <see cref="Control.Chrome"/> spans this whole control's
        /// bounds (header and <see cref="Content"/> combined, not just the header row),
        /// <c>headerToggle</c>'s default <see cref="UIElement.VerticalAlignment"/>.<see cref="VerticalAlignment.Stretch"/>
        /// would stretch-fill that entire (and, mid-animation, growing) height instead of staying header-sized.
        /// Positioning it correctly is therefore purely a matter of alignment: <c>headerToggle.VerticalAlignment</c>
        /// is set to <see cref="VerticalAlignment.Top"/> once, wherever <c>headerToggle</c> is (re)assigned (the
        /// constructor, <see cref="OnApplyTemplate"/>) - with that alignment, <see cref="Control.Chrome"/>'s own
        /// arrange of its child naturally sizes/positions <c>headerToggle</c> to its own natural (measured)
        /// height at the top, the same way <see cref="SplitPane"/>'s divider positions itself via
        /// <see cref="UIElement.Margin"/>/alignment rather than a direct <c>Arrange</c> call.
        /// </remarks>
        protected override void ArrangeContent()
        {
            Rectangle bounds = ContentBounds;
            int headerHeight = headerToggle.Measure().Height;

            if (Content?.IsVisible == true)
            {
                // Content gets InvalidateArrange() immediately before Arrange() - Arrange(rect) no-ops when the
                // target's own IsArrangeInvalid is already false, regardless of whether rect changed (see the
                // SplitPane postmortem in [[project_icyui_tier2_roadmap]]).
                int revealedHeight = (int)(Content.Measure().Height * ExpansionProgress);
                Content.InvalidateArrange();
                Content.Arrange(new Rectangle(bounds.X, bounds.Y + headerHeight, bounds.Width, revealedHeight));
            }

            Chrome.InvalidateArrange();
            Chrome.Arrange(ActualBounds);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Repoints <c>headerToggle</c> - the element <see cref="ArrangeContent"/>/<see cref="MeasureContent"/>
        /// already reference - to whichever <see cref="ExpanderHeader"/> is actually live right now:
        /// <see cref="Control.Template"/>'s own <c>PART_Header</c> when one is set, falling back to the built-in
        /// default header otherwise (mirrors <see cref="SplitPane.OnApplyTemplate"/> exactly). Re-syncs
        /// <see cref="Header"/>'s value and <see cref="IsExpanded"/> onto whichever header is now live, since
        /// <c>headerContent</c> - not <c>headerToggle.Content</c> itself - is this control's own source of truth
        /// for <see cref="Header"/>.
        /// </remarks>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            headerToggle.IsCheckedChanged -= HeaderToggle_IsCheckedChanged;

            if (Template != null && GetTemplateChild<ExpanderHeader>("PART_Header") is { } part)
            {
                headerToggle = part;
            }
            else
            {
                headerToggle = defaultHeader;
                if (Template == null)
                    ((Border)Chrome).Child = headerToggle;
            }

            headerToggle.VerticalAlignment = VerticalAlignment.Top;
            headerToggle.Content = headerContent;
            headerToggle.IsChecked = IsExpanded;
            headerToggle.IsCheckedChanged += HeaderToggle_IsCheckedChanged;
        }

        private void HeaderToggle_IsCheckedChanged(object? sender, EventArgs e) => IsExpanded = headerToggle.IsChecked;
    }
}
