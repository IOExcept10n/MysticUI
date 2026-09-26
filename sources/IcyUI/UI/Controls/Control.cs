// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Markup;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Base class for controls that need background/border decoration without reimplementing it - composes an
    /// internal element (<see cref="Chrome"/>, a <see cref="UI.Border"/> by default) that fills this control's
    /// entire <see cref="UIElement.ActualBounds"/> and forwards <see cref="Background"/>/<see cref="BorderBrush"/>/
    /// <see cref="BorderThickness"/>/<see cref="Padding"/> to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UIElement"/> itself carries no decoration (per the <see cref="UI.Border"/> extraction) - this
    /// class exists so every decorated control doesn't have to compose its own <see cref="UI.Border"/> by hand.
    /// Subclasses that need child content should derive from <see cref="ContentControl"/> instead, which exposes
    /// <see cref="ContentControl.Content"/> as <see cref="Chrome"/>'s content.
    /// </para>
    /// <para>
    /// Setting <see cref="Template"/> replaces <see cref="Chrome"/> with markup content of the template author's
    /// choosing instead of the built-in <see cref="UI.Border"/> - see <see cref="Template"/>'s own remarks. Any
    /// future property added to this class that currently forwards into <see cref="Chrome"/> the way
    /// <see cref="Background"/>/<see cref="BorderBrush"/>/<see cref="BorderThickness"/>/<see cref="Padding"/> do
    /// must gain the same dual-path treatment those four have (an untemplated branch forwarding into
    /// <c>(Border)Chrome</c>, a templated branch storing locally) - there is no generic mechanism that does this
    /// automatically, so it's easy to forget.
    /// </para>
    /// <para>
    /// The untemplated branch of that same dual-path treatment also participates in the value-precedence system
    /// (see <see cref="Icy.Data.Markup.PropertyValuePrecedence"/>): a direct C# assignment to
    /// <see cref="Background"/>/<see cref="BorderBrush"/>/<see cref="BorderThickness"/>/<see cref="Padding"/>
    /// while untemplated now raises this control's own <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>
    /// (guarded by an equality check, so re-assigning the current value is a no-op), so it registers as a local
    /// value that outranks a later-applied <see cref="Styles.Style"/>/<see cref="Styles.VisualState"/>/animation
    /// setter for the same property - exactly like every other <c>[RegisterReference]</c> property on this class.
    /// A local value blocks every precedence tier below it outright, not just <see cref="Styles.Style"/> - a
    /// control that locally sets one of these four properties in its own constructor (the common pattern for a
    /// custom-themed sub-widget, e.g. a <see cref="Slider"/>'s gradient-filled <see cref="Background"/>) permanently opts that
    /// property out of its type's own theme, including any <see cref="Styles.VisualState"/> that targets it.
    /// </para>
    /// </remarks>
    public class Control : UIElement, IContainerLayout
    {
        private ControlTemplate? template;
        private IBrush templatedBackground = new SolidColorBrush(Color.Transparent);
        private IBrush? templatedBorderBrush;
        private Thickness templatedBorderThickness;
        private Thickness templatedPadding;

        /// <summary>
        /// Initializes a new instance of the <see cref="Control"/> class.
        /// </summary>
        public Control()
        {
            Chrome.Parent = this;
        }

        /// <summary>
        /// Gets or sets the background brush drawn behind this control's content.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(typeof(SolidColorBrush), "Transparent")]
        [RegisterReference]
        public IBrush Background
        {
            get => Template == null ? ((Border)Chrome).Background : templatedBackground;
            set
            {
                if (Template == null)
                {
                    if (Equals(((Border)Chrome).Background, value))
                        return;
                    ((Border)Chrome).Background = value;
                    OnPropertyChanged(nameof(Background));
                }
                else
                {
                    SetProperty(ref templatedBackground, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the brush used to paint this control's border.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(null)]
        [RegisterReference]
        public IBrush? BorderBrush
        {
            get => Template == null ? ((Border)Chrome).BorderBrush : templatedBorderBrush;
            set
            {
                if (Template == null)
                {
                    if (Equals(((Border)Chrome).BorderBrush, value))
                        return;
                    ((Border)Chrome).BorderBrush = value;
                    OnPropertyChanged(nameof(BorderBrush));
                }
                else
                {
                    SetProperty(ref templatedBorderBrush, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the thickness of this control's border, and the amount by which its content is inset from
        /// <see cref="ContentBounds"/>.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public Thickness BorderThickness
        {
            get => Template == null ? ((Border)Chrome).BorderThickness : templatedBorderThickness;
            set
            {
                if (Template == null)
                {
                    if (Equals(((Border)Chrome).BorderThickness, value))
                        return;

                    // Border.BorderThickness's own setter already calls InvalidateMeasure/InvalidateArrange on
                    // Chrome, which propagates up to this control via UIElement.InvalidateMeasure's Parent walk
                    // (Chrome.Parent is this control) - no need to invalidate again here.
                    ((Border)Chrome).BorderThickness = value;
                    OnPropertyChanged(nameof(BorderThickness));
                }
                else if (SetProperty(ref templatedBorderThickness, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the padding of this control, i.e. the amount by which its content is inset from
        /// <see cref="Chrome"/>'s own bounds (inside <see cref="BorderThickness"/>).
        /// </summary>
        /// <remarks>
        /// Hides (rather than overrides - <see cref="UIElement.Padding"/> isn't <see langword="virtual"/>)
        /// <see cref="UIElement.Padding"/> to forward it to <see cref="Chrome"/> when untemplated, the same way
        /// <see cref="Background"/>/<see cref="BorderBrush"/>/<see cref="BorderThickness"/> already do - so
        /// <see cref="Background"/>/<see cref="BorderBrush"/> (drawn across <see cref="Chrome"/>'s full bounds,
        /// unaffected by padding - the standard box model) cover this control's whole area including the padding
        /// band, and only the actual content ends up inset by it. <see cref="UIElement.Measure()"/>'s own generic
        /// padding step still applies exactly once when untemplated - to <see cref="Chrome"/>, when
        /// <see cref="MeasureContent"/> calls <see cref="Chrome"/>'s <see cref="UIElement.Measure()"/> - since
        /// this control's own (hidden, always-zero) base <see cref="UIElement.Padding"/> field is never written
        /// to.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public new Thickness Padding
        {
            get => Template == null ? Chrome.Padding : templatedPadding;
            set
            {
                if (Template == null)
                {
                    if (Equals(Chrome.Padding, value))
                        return;

                    // Same reasoning as BorderThickness above: Chrome's own Padding setter already invalidates
                    // Chrome, which propagates up to this control.
                    Chrome.Padding = value;
                    OnPropertyChanged(nameof(Padding));
                }
                else if (SetProperty(ref templatedPadding, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the template that replaces this control's default decorated visual tree
        /// (<see cref="Chrome"/>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see langword="null"/> (the default) keeps this control's built-in <see cref="UI.Border"/>-based
        /// decoration, with <see cref="Background"/>/<see cref="BorderBrush"/>/<see cref="BorderThickness"/>/
        /// <see cref="Padding"/> forwarding into it exactly as always. Setting a template swaps <see cref="Chrome"/>
        /// for markup content of the template author's choosing instead - those same four properties then store
        /// locally, reflected into the template's content only where it explicitly asks for them via
        /// <c>{TemplateBinding}</c> (see <see cref="Icy.Markup.Extensions.TemplateBindingExtension"/>). Their
        /// values are preserved across the switch either way.
        /// </para>
        /// <para>
        /// Simple decorated controls like <see cref="Button"/>/<see cref="ContentControl"/>/<see cref="CheckBox"/>/
        /// <see cref="ToggleButton"/> are always template-safe. Structural controls whose own code reaches into
        /// their visual tree by name - such as <see cref="Slider"/> (<c>PART_Thumb</c>) or
        /// <see cref="ProgressBar"/> (<c>PART_Fill</c>) - are safe too, via named template parts: see
        /// <see cref="OnApplyTemplate"/>/<see cref="GetTemplateChild{T}(string)"/>. A control that hasn't been
        /// retrofitted with an <see cref="OnApplyTemplate"/> override yet - currently <see cref="ScrollViewer"/>,
        /// <see cref="TextBox"/>, and <see cref="ColorPicker"/> - still accepts a <see cref="Template"/> without
        /// throwing, but its own behavior stays wired to its built-in default parts regardless of what the template
        /// actually provides.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="value"/>'s own target type isn't assignable to this control's runtime type.
        /// </exception>
        [Category("Appearance")]
        [DefaultValue(null)]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public ControlTemplate? Template
        {
            get => template;
            set
            {
                if (template == value)
                    return;

                if (value != null && !value.TargetType.IsAssignableFrom(GetType()))
                {
                    throw new ArgumentException(
                        $"A '{value.TargetType.Name}' template can't be applied to a '{GetType().Name}' - its target type must be assignable to this control's own type.",
                        nameof(value));
                }

                // Snapshot every value about to change storage location, read through the OLD Template's routing -
                // must happen before `template` itself changes below, or these getters would already read the new
                // (empty) branch.
                IBrush snapshotBackground = Background;
                IBrush? snapshotBorderBrush = BorderBrush;
                Thickness snapshotBorderThickness = BorderThickness;
                Thickness snapshotPadding = Padding;
                object? snapshotState = CaptureTemplateState();

                SetProperty(ref template, value);

                // Commit the snapshot into whichever storage is now authoritative - directly into the backing
                // fields, never through the public Background/BorderBrush/BorderThickness/Padding setters (or
                // RestoreTemplateState going through a subclass's own public setter, e.g. ContentControl.Content).
                // Those setters raise PropertyChanged, which DependencyObject.OnDependencyObjectPropertyChanged
                // reports to the value-precedence system as NotifyLocalValueChanged - indistinguishable from a
                // genuine new local/manual assignment. That would permanently pin these properties to whatever
                // they were at the moment of the swap and silently block every future Style/VisualState
                // contribution to them - this is a re-commit of an already-decided value, not a new one.
                //
                // For the new-template direction this must also happen *before* LoadContent below, so a
                // {TemplateBinding}'s very first read (during LoadContent) already sees the restored value
                // directly, with no PropertyChanged needed to "catch up" afterward.
                if (template != null)
                {
                    templatedBackground = snapshotBackground;
                    templatedBorderBrush = snapshotBorderBrush;
                    templatedBorderThickness = snapshotBorderThickness;
                    templatedPadding = snapshotPadding;
                    RestoreTemplateState(snapshotState);
                }

                UIElement oldChrome = Chrome;
                UIElement newChrome = template?.LoadContent(this) ?? new Border();

                if (template == null)
                {
                    // Reverting to the default Border: it's brand new, so writing its own Background/etc.
                    // directly is an ordinary first assignment, not a precedence-poisoning one - nothing else has
                    // ever touched (or could ever have touched) this specific Border's own properties before.
                    var defaultBorder = (Border)newChrome;
                    defaultBorder.Background = snapshotBackground;
                    defaultBorder.BorderBrush = snapshotBorderBrush;
                    defaultBorder.BorderThickness = snapshotBorderThickness;
                    defaultBorder.Padding = snapshotPadding;
                }

                // The old Chrome subtree's {TemplateBinding}s subscribed to this control's PropertyChanged - this
                // control outlives the orphaned subtree, so without unbinding, the old tree (and every Binding it
                // created) leaks for this control's whole remaining lifetime and keeps refreshing forever.
                foreach (UIElement element in oldChrome.EnumerateVisualSubtree())
                    element.UnbindAll();
                oldChrome.Parent = null;
                oldChrome.Canvas = null;

                Chrome = newChrome;
                Chrome.Parent = this;
                Chrome.Canvas = Canvas;

                if (template == null)
                    RestoreTemplateState(snapshotState);

                OnApplyTemplate();

                InvalidateMeasure();
                InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets the area available to this control's decorated content, i.e. this control's own
        /// <see cref="UIElement.ActualBounds"/> inset by <see cref="BorderThickness"/> and <see cref="Padding"/>.
        /// </summary>
        /// <remarks>
        /// Computed from this control's own <see cref="UIElement.ActualBounds"/>, not <see cref="Chrome"/>'s -
        /// normally the two agree (<see cref="Chrome"/> fills <see cref="UIElement.ActualBounds"/> exactly), but
        /// a subclass like <see cref="ScrollViewer"/> deliberately arranges <see cref="Chrome"/> larger than this
        /// control's own bounds (so <see cref="ContentControl.Content"/> can overflow for scrolling) - callers of
        /// <see cref="ContentBounds"/> mean this control's own visible content area, not however big
        /// <see cref="Chrome"/> currently happens to be.
        /// </remarks>
        public Rectangle ContentBounds => ActualBounds - BorderThickness - Padding;

        /// <summary>
        /// Gets the internal element this control composes for its background/border decoration - a
        /// <see cref="UI.Border"/> by default, or <see cref="Template"/>'s instantiated content when one is set.
        /// </summary>
        protected UIElement Chrome { get; private set; } = new Border();

        /// <inheritdoc/>
        /// <remarks>
        /// Force-invalidates <see cref="Chrome"/> before arranging it - same reasoning as
        /// <see cref="UI.Border.ArrangeContent"/>: <see cref="UIElement.Arrange(Rectangle)"/> no-ops when the
        /// target's own <c>IsArrangeInvalid</c> is already <see langword="false"/>, regardless of whether the
        /// rect changed, so without this <see cref="Chrome"/> gets stuck at whatever size it happened to
        /// receive the first time this control was arranged, even after this control's own
        /// <see cref="UIElement.ActualBounds"/> later changes size around it.
        /// </remarks>
        protected override void ArrangeContent()
        {
            Chrome.InvalidateArrange();
            Chrome.Arrange(ActualBounds);
        }

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            yield return Chrome;
        }

        /// <inheritdoc/>
        protected override Size MeasureContent() => Chrome.Measure();

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            Chrome.Canvas = Canvas;
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            base.OnDetached();
            Chrome.Canvas = null;
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context) => Chrome.Draw(context);

        /// <summary>
        /// Captures whatever subclass-specific state needs to survive a <see cref="Template"/> swap, beyond the
        /// four decoration properties this class already preserves on its own.
        /// </summary>
        /// <returns>Opaque state later passed back to <see cref="RestoreTemplateState(object?)"/>.</returns>
        /// <remarks>
        /// Returns <see langword="null"/> by default. <see cref="ContentControl"/> overrides this pair to also
        /// preserve <see cref="ContentControl.Content"/> across a swap. This is also the seed of what a future
        /// named-template-parts addition is expected to build on: it runs once per swap, at whichever of the two
        /// points <see cref="RestoreTemplateState(object?)"/>'s own remarks describe - exactly where discovering a
        /// new template's named parts would need to happen too.
        /// </remarks>
        protected virtual object? CaptureTemplateState() => null;

        /// <summary>
        /// Restores state captured by <see cref="CaptureTemplateState"/> after a <see cref="Template"/> swap.
        /// </summary>
        /// <param name="state">The state <see cref="CaptureTemplateState"/> returned before the swap.</param>
        /// <remarks>
        /// Called exactly once per swap, at one of two different points depending on direction - check
        /// <see cref="Template"/> (already the new value by the time this runs) to tell which:
        /// <list type="bullet">
        /// <item><description>
        /// Switching <em>to</em> a template (<see cref="Template"/> is non-<see langword="null"/>): called
        /// <em>before</em> the new template's content is built, so a <c>{TemplateBinding}</c> inside it captures
        /// the restored value on its very first read. Restore state directly into a backing field here, never
        /// through a public property setter that participates in the value-precedence system (mirrors how this
        /// class's own <see cref="Background"/>/<see cref="BorderBrush"/>/<see cref="BorderThickness"/>/
        /// <see cref="Padding"/> are restored - see <see cref="Template"/>'s setter for why).
        /// </description></item>
        /// <item><description>
        /// Switching <em>away from</em> a template (<see cref="Template"/> is <see langword="null"/>): called
        /// <em>after</em> <see cref="Chrome"/> has already been replaced by the fresh default <see cref="UI.Border"/>
        /// - restore state into that Border's own properties directly; it's brand new, so there's no
        /// precedence-poisoning risk there.
        /// </description></item>
        /// </list>
        /// </remarks>
        protected virtual void RestoreTemplateState(object? state)
        {
        }

        /// <summary>
        /// Finds a named element inside the <em>current</em> <see cref="Template"/>'s content - a "template part".
        /// </summary>
        /// <typeparam name="T">The type the named element is expected to have.</typeparam>
        /// <param name="name">
        /// The name to look up, as declared by <c>x:Name</c> on an element inside the template's markup. No
        /// naming convention is enforced by this method itself, but the established one (mirroring WPF) is a
        /// <c>PART_</c> prefix, e.g. <c>PART_Thumb</c> - document it on whichever property/field a subclass
        /// exposes the found element through.
        /// </param>
        /// <returns>
        /// The named element, or <see langword="null"/> when <see cref="Template"/> is <see langword="null"/>,
        /// the template has no element by that name, or it isn't a <typeparamref name="T"/>.
        /// </returns>
        /// <remarks>
        /// Deliberately does <em>not</em> reuse <see cref="UIElementExtensions.FindControl{T}(UIElement, string)"/>
        /// - that extension searches from <c>element.GetRoot()</c>, which would walk straight past
        /// <see cref="Chrome"/> up to whatever document this control itself is attached inside, missing the
        /// template's own isolated <see cref="MarkupNameScope"/> entirely (or, worse, silently matching an
        /// unrelated same-named element elsewhere in that outer document). This looks up
        /// <see cref="MarkupNameScope.GetScope(UIElement)"/> directly on <see cref="Chrome"/> instead, which is
        /// exactly the root <see cref="Icy.Markup.MarkupLoader.LoadTemplateContent"/> attaches the template's own
        /// scope to.
        /// </remarks>
        protected T? GetTemplateChild<T>(string name)
            where T : UIElement
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return MarkupNameScope.GetScope(Chrome)?.Find(name) as T;
        }

        /// <summary>
        /// Called once, every time <see cref="Template"/> changes (in either direction - applied, cleared, or
        /// switched to a different template), right after <see cref="Chrome"/> and every decoration/content
        /// property have been fully re-wired for the new state.
        /// </summary>
        /// <remarks>
        /// Does nothing by default. A subclass that reaches into its own visual tree by reference - e.g.
        /// <see cref="Slider"/> wiring its drag logic directly to its thumb element - overrides this to call
        /// <see cref="GetTemplateChild{T}(string)"/> and repoint that reference to whatever the new
        /// <see cref="Template"/> (or, when reverting, the fresh default <see cref="UI.Border"/>) actually
        /// provides, instead of hardcoding it once at construction. This is what actually makes a control
        /// "template-safe": until a control overrides this appropriately, applying a custom <see cref="Template"/>
        /// to it silently orphans whatever elements its own code still holds direct references to.
        /// </remarks>
        protected virtual void OnApplyTemplate()
        {
        }
    }
}
