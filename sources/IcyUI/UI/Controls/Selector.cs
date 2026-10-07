// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using Icy.Data;
using Icy.Data.Bindings;
using Icy.Data.Markup.Attributes;
using Icy.Input.Events;
using Icy.Rendering.Brushes;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Adds single selection and a popup-hosted item list on top of <see cref="SelectingItemsControl"/> - the shared base
    /// for <see cref="Dropdown"/> and <see cref="ComboBox"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Realizes <see cref="SelectorItem"/>s (see <see cref="SelectingItemsControl.CreateContainer(DataTemplate, object)"/>) instead of
    /// bare <see cref="ItemContainer"/>s. <see cref="Chrome"/> shows only the always-visible closed-state row
    /// (<c>PART_ToggleButton</c>, plus <c>PART_TextBox</c> for <see cref="ComboBox"/>); realized items live in a
    /// separate popup host added to the owning <see cref="UI.Canvas"/>'s <see cref="UI.Canvas.Overlays"/> only
    /// while <see cref="IsOpen"/> - see <see cref="RealizationBounds"/>/<see cref="AttachContainer(ItemContainer, int)"/>/
    /// <see cref="DetachContainer(ItemContainer)"/>, which redirect <see cref="ItemsControl"/>'s existing
    /// realize/de-realize/virtualization algorithm there without changing it.
    /// </para>
    /// <para>
    /// Keeps a single <see cref="UI.Canvas"/>-level focus point for the whole interaction (itself for
    /// <see cref="Dropdown"/>, a text box for <see cref="ComboBox"/> - see <see cref="HookFocusGate(UIElement)"/>)
    /// and never moves focus onto individual <see cref="SelectorItem"/>s.
    /// </para>
    /// </remarks>
    public abstract class Selector : SelectingItemsControl
    {
        private const float DefaultMaxDropDownHeight = 200f;

        private readonly ToggleButton defaultToggle;
        private readonly Panel popupHost;
        private readonly Border popupRoot;
        private readonly ScrollViewer popupScrollViewer;
        private DynamicPropertyPath? displayMemberPath;
        private string? displayMemberPathText;
        private UIElement? focusGate;
        private int highlightedIndex = -1;
        private bool isOpen;
        private float maxDropDownHeight = DefaultMaxDropDownHeight;
        private Canvas? openedOnCanvas;
        private object? selectedValue;
        private DynamicPropertyPath? selectedValuePath;
        private string? selectedValuePathText;
        private INavigationEvents? subscribedNavigation;
        private ITouchEvents? subscribedTouch;
        private ToggleButton toggle;

        /// <summary>
        /// Initializes a new instance of the <see cref="Selector"/> class.
        /// </summary>
        protected Selector()
        {
            popupHost = new PopupItemsHost(this);
            popupScrollViewer = new ScrollViewer { Content = popupHost, ClipToBounds = true };
            popupScrollViewer.ScrollChanged += PopupScrollViewer_ScrollChanged;
            popupRoot = new Border
            {
                Child = popupScrollViewer,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            defaultToggle = new ToggleButton();
            toggle = defaultToggle;
            toggle.IsCheckedChanged += Toggle_IsCheckedChanged;

            // ToggleButton/Button default to IsFocusable - left alone, tapping this Selector would focus the
            // internal toggle instead of this control's own focus gate (see HookFocusGate), so navigation/typeahead
            // would only ever engage after Tab-driven focus, never after a mouse/touch tap. OnApplyTemplate repeats
            // this for a templated PART_ToggleButton.
            toggle.IsFocusable = false;

            // Safe here, at construction: Chrome is always the default Border until/unless Template is set later,
            // in which case OnApplyTemplate re-wires appropriately (mirrors Expander/SplitPane).
            ((Border)Chrome).Child = toggle;

            HookFocusGate(this);
        }

        /// <summary>
        /// Gets or sets the path (resolved dynamically, tolerant of heterogeneous/unknown item types) used to
        /// derive each item's display text - <see cref="GetDisplayText(object)"/> resolves it, falling back to
        /// <see cref="object.ToString"/> when unset or when resolution fails for a given item.
        /// </summary>
        [Category("Data")]
        [DefaultValue(null)]
        [RegisterReference]
        public string? DisplayMemberPath
        {
            get => displayMemberPathText;
            set
            {
                if (!SetProperty(ref displayMemberPathText, value))
                    return;
                displayMemberPath = value == null ? null : new DynamicPropertyPath(value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Selector"/>'s popup is currently shown.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool IsOpen
        {
            get => isOpen;
            set
            {
                if (!SetProperty(ref isOpen, value))
                    return;
                toggle.IsChecked = value;
                if (value)
                    OpenPopup();
                else
                    ClosePopup();
            }
        }

        /// <summary>
        /// Gets or sets the maximum height, in pixels, the open popup grows to before an internal
        /// <see cref="ScrollViewer"/> takes over.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(DefaultMaxDropDownHeight)]
        [RegisterReference]
        public float MaxDropDownHeight
        {
            get => maxDropDownHeight;
            set
            {
                Guard.IsGreaterThan(value, 0f);
                if (SetProperty(ref maxDropDownHeight, value) && isOpen)
                    PositionPopup();
            }
        }

        /// <summary>
        /// Gets or sets the background brush of the open popup.
        /// </summary>
        /// <remarks>
        /// The popup is hosted in the owning <see cref="UI.Canvas"/>'s <see cref="UI.Canvas.Overlays"/>, outside this
        /// control's own <see cref="Control.Template"/>/<see cref="Control.Chrome"/>, so it can't be decorated by a
        /// template - this property (and <see cref="PopupBorderBrush"/>/<see cref="PopupBorderThickness"/>) is how a
        /// <see cref="Icy.UI.Styles.Style"/> reaches it.
        /// </remarks>
        [Category("Appearance")]
        [RegisterReference]
        public IBrush PopupBackground
        {
            get => popupRoot.Background;
            set => popupRoot.Background = value;
        }

        /// <summary>
        /// Gets or sets the border brush of the open popup.
        /// </summary>
        /// <remarks>
        /// See <see cref="PopupBackground"/> for why the popup carries its own decoration properties.
        /// </remarks>
        [Category("Appearance")]
        [RegisterReference]
        public IBrush? PopupBorderBrush
        {
            get => popupRoot.BorderBrush;
            set => popupRoot.BorderBrush = value;
        }

        /// <summary>
        /// Gets or sets the border thickness of the open popup.
        /// </summary>
        /// <remarks>
        /// See <see cref="PopupBackground"/> for why the popup carries its own decoration properties.
        /// </remarks>
        [Category("Appearance")]
        [RegisterReference]
        public Thickness PopupBorderThickness
        {
            get => popupRoot.BorderThickness;
            set => popupRoot.BorderThickness = value;
        }

        /// <summary>
        /// Gets the value <see cref="SelectedValuePath"/> resolves from <see cref="SelectingItemsControl.SelectedItem"/> -
        /// <see langword="null"/> when <see cref="SelectingItemsControl.SelectedItem"/>/<see cref="SelectedValuePath"/> is
        /// <see langword="null"/>, or when the path fails to resolve for the current item.
        /// </summary>
        [Category("Data")]
        [Browsable(false)]
        public object? SelectedValue => selectedValue;

        /// <summary>
        /// Gets or sets the path (resolved dynamically) used to derive <see cref="SelectedValue"/> from
        /// <see cref="SelectingItemsControl.SelectedItem"/>.
        /// </summary>
        [Category("Data")]
        [DefaultValue(null)]
        [RegisterReference]
        public string? SelectedValuePath
        {
            get => selectedValuePathText;
            set
            {
                if (!SetProperty(ref selectedValuePathText, value))
                    return;
                selectedValuePath = value == null ? null : new DynamicPropertyPath(value);
                UpdateSelectedValue();
            }
        }

        /// <summary>
        /// Gets the popup's toggle button - always live, whether the built-in default or a <see cref="Control.Template"/>'s own <c>PART_ToggleButton</c>.
        /// </summary>
        protected ToggleButton Toggle => toggle;

        /// <summary>
        /// Gets or sets the popup's current keyboard/gamepad-highlighted index, or <c>-1</c> for none. Not a
        /// public property - purely the "currently highlighted, not yet committed" cursor a subclass's
        /// navigation handling drives.
        /// </summary>
        protected int HighlightedIndex
        {
            get => highlightedIndex;
            set
            {
                if (highlightedIndex == value)
                    return;

                if (realizedContainers.TryGetValue(highlightedIndex, out ItemContainer? oldContainer))
                    ((SelectorItem)oldContainer).IsHighlighted = false;

                highlightedIndex = value;

                if (realizedContainers.TryGetValue(highlightedIndex, out ItemContainer? newContainer))
                    ((SelectorItem)newContainer).IsHighlighted = true;
            }
        }

        /// <inheritdoc/>
        protected override Rectangle RealizationBounds => popupHost.ContentBounds;

        /// <inheritdoc/>
        /// <remarks>
        /// While the popup is open, moves <see cref="HighlightedIndex"/> by one in the pressed direction and claims the
        /// press. While it's closed, lets the press go, so focus moves on to the next control; Enter or gamepad A opens it
        /// (see <see cref="OnActivate"/>).
        /// </remarks>
        protected internal override bool OnNavigate(Vector2 direction)
        {
            if (!IsOpen || ItemCount == 0)
                return false;

            int delta = Math.Abs(direction.X) > Math.Abs(direction.Y)
                ? (direction.X < 0 ? -1 : 1)
                : (direction.Y < 0 ? -1 : 1);
            int next = HighlightedIndex == -1 ? 0 : HighlightedIndex + delta;
            HighlightedIndex = Math.Clamp(next, 0, ItemCount - 1);
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Closed: opens the popup. Open with a valid <see cref="HighlightedIndex"/>: commits it to
        /// <see cref="SelectingItemsControl.SelectedIndex"/> and closes. Always claims the press.
        /// </remarks>
        protected internal override bool OnActivate()
        {
            if (!IsOpen)
            {
                IsOpen = true;
                return true;
            }

            // Upper bound guarded too: the item count can shrink underneath a stale highlight (a live collection
            // change, or ComboBox's filtering) - committing it unguarded would throw from SelectedIndex's own range check.
            if (HighlightedIndex >= 0 && HighlightedIndex < ItemCount)
            {
                SelectedIndex = HighlightedIndex;
                IsOpen = false;
            }

            return true;
        }

        /// <inheritdoc/>
        protected override void AttachContainer(ItemContainer container, int index)
        {
            var item = (SelectorItem)container;
            item.IsSelected = index == SelectedIndex;
            item.IsHighlighted = index == HighlightedIndex;
            containerIndices[item] = index;
            popupHost.Children.Add(container);
        }

        /// <inheritdoc/>
        protected override void DetachContainer(ItemContainer container)
        {
            containerIndices.Remove((SelectorItem)container);
            popupHost.Children.Remove(container);
        }

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            yield return Chrome;
        }

        /// <inheritdoc/>
        protected override Size MeasureContent() => Chrome.Measure();

        /// <inheritdoc/>
        protected override void OnRender(Icy.Rendering.IRenderContext context) => Chrome.Draw(context);

        /// <inheritdoc/>
        /// <remarks>
        /// Re-positions an open popup, so it stays attached when this control moves - e.g. after a window resize or a
        /// <see cref="Canvas.EffectiveScale"/> change re-lays out the surface. The reposition is deferred until every
        /// root element is arranged (this control's ancestors are still mid-arrange here) and runs before the canvas
        /// arranges its overlays, so the popup lands in the right place within the same frame.
        /// </remarks>
        protected override void OnArrangeUpdated()
        {
            base.OnArrangeUpdated();
            if (isOpen && openedOnCanvas != null)
                openedOnCanvas.QueueAfterRootLayout(RepositionOpenPopup);
        }

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            if (isOpen)
                OpenPopup();
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            // Canvas is already null by the time this runs (see UIElement.Canvas's setter) - RemoveOverlay must go
            // through the canvas this popup was actually opened on, captured when it was.
            openedOnCanvas?.RemoveOverlay(popupRoot);
            openedOnCanvas = null;
            isOpen = false;
            toggle.IsChecked = false;

            if (subscribedTouch != null)
            {
                subscribedTouch.TouchDown -= OnOutsideTouchDown;
                subscribedTouch.Tap -= OnPopupItemTap;
                subscribedTouch = null;
            }

            UnsubscribeNavigation();
            base.OnDetached();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Repoints <c>toggle</c> - the field every open/close path in this class already references - to whichever
        /// <see cref="ToggleButton"/> is actually live right now: <see cref="Control.Template"/>'s own
        /// <c>PART_ToggleButton</c> when one is set, falling back to the built-in default toggle otherwise (mirrors
        /// <see cref="Expander.OnApplyTemplate"/>/<see cref="SplitPane.OnApplyTemplate"/> exactly). Without this, the
        /// default theme's <see cref="Dropdown"/> <see cref="Control.Template"/> would orphan the constructor's own
        /// toggle and the control would render as an inert empty box.
        /// </remarks>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            toggle.IsCheckedChanged -= Toggle_IsCheckedChanged;

            if (Template != null && GetTemplateChild<ToggleButton>("PART_ToggleButton") is { } part)
            {
                toggle = part;
            }
            else
            {
                toggle = defaultToggle;
                if (Template == null)
                    ((Border)Chrome).Child = toggle;
            }

            toggle.IsFocusable = false;
            toggle.IsChecked = isOpen;
            toggle.IsCheckedChanged += Toggle_IsCheckedChanged;
        }

        /// <summary>
        /// Re-runs the popup's position/size calculation if it's currently open - a no-op otherwise. A subclass calls
        /// this after something that changes how much space the popup needs (e.g. <see cref="ComboBox"/> after
        /// filtering changes the item count).
        /// </summary>
        protected void RefreshPopupPosition()
        {
            if (isOpen)
                PositionPopup();
        }

        /// <summary>
        /// Re-points which element's <see cref="UIElement.FocusChanged"/> gates the Escape/gamepad-B subscription
        /// (see <see cref="SubscribeNavigation"/>) - <see langword="this"/> by default (wired once, at construction).
        /// <see cref="ComboBox"/> calls this again with its own text box, since that's the element that actually holds
        /// <see cref="UI.Canvas"/> focus for it.
        /// </summary>
        /// <remarks>
        /// Arrows and Enter don't depend on the gate: they arrive through <see cref="OnNavigate(Vector2)"/> and
        /// <see cref="OnActivate"/>, routed by <see cref="UI.Canvas"/> from the focused element up through its ancestors.
        /// </remarks>
        /// <param name="gate">The element whose focus state should drive the subscription from now on.</param>
        protected void HookFocusGate(UIElement gate)
        {
            if (focusGate != null)
                focusGate.FocusChanged -= FocusGate_FocusChanged;
            focusGate = gate;
            focusGate.FocusChanged += FocusGate_FocusChanged;
        }

        /// <summary>
        /// Subscribes to <see cref="Icy.Input.Events.INavigationEvents.CloseModal"/> (Escape/gamepad B) - idempotent,
        /// and a no-op while unattached. Arrows and Enter arrive through <see cref="OnNavigate(Vector2)"/> and
        /// <see cref="OnActivate"/> instead.
        /// </summary>
        protected void SubscribeNavigation()
        {
            if (Configuration == null || subscribedNavigation != null)
                return;
            subscribedNavigation = Configuration.Input.Events.Navigation;
            subscribedNavigation.CloseModal += OnNavigationCloseModal;
        }

        /// <summary>
        /// Unsubscribes from <see cref="Icy.Input.Events.INavigationEvents.CloseModal"/> (Escape/gamepad B) -
        /// idempotent. Arrows and Enter arrive through <see cref="OnNavigate(Vector2)"/> and <see cref="OnActivate"/>
        /// instead.
        /// </summary>
        protected void UnsubscribeNavigation()
        {
            if (subscribedNavigation == null)
                return;
            subscribedNavigation.CloseModal -= OnNavigationCloseModal;
            subscribedNavigation = null;
        }

        /// <summary>
        /// Handles Escape/gamepad-B while the focus gate is focused - closes the popup without changing
        /// <see cref="SelectingItemsControl.SelectedIndex"/>.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnNavigationCloseModal(object? sender, EventArgs e) => IsOpen = false;

        private void FocusGate_FocusChanged(object? sender, EventArgs e)
        {
            if (focusGate!.IsFocused)
                SubscribeNavigation();
            else
                UnsubscribeNavigation();
        }

        /// <summary>
        /// Resolves <see cref="DisplayMemberPath"/> against <paramref name="item"/>, falling back to
        /// <see cref="object.ToString"/> when unset or unresolvable.
        /// </summary>
        /// <param name="item">The item to derive display text for.</param>
        /// <returns>The resolved display text.</returns>
        protected string GetDisplayText(object item)
        {
            object? resolved = displayMemberPath?.GetValue(item);
            return resolved?.ToString() ?? item.ToString() ?? string.Empty;
        }

        private void UpdateSelectedValue()
        {
            selectedValue = SelectedItem == null || selectedValuePath == null ? null : selectedValuePath.GetValue(SelectedItem);
        }

        /// <inheritdoc/>
        protected override void OnSelectionChanged() => UpdateSelectedValue();

        private void Toggle_IsCheckedChanged(object? sender, EventArgs e) => IsOpen = toggle.IsChecked;

        private void OpenPopup()
        {
            if (Canvas == null)
                return;

            openedOnCanvas = Canvas;
            PositionPopup();
            Canvas.AddOverlay(popupRoot);
            ((IVirtualizingScrollInfo)this).OnViewportChanged(0, popupScrollViewer.VerticalOffset, popupScrollViewer.ViewportWidth, popupScrollViewer.ViewportHeight);

            // The first PositionPopup above sized the popup from ExtentHeight while every item was still an
            // estimate; the realize pass just above has since measured some of them, so re-run it against the now
            // more accurate extent rather than leaving the popup stuck at its pre-realize guess.
            PositionPopup();

            subscribedTouch = Canvas.Configuration.Input.Events.Touch;
            subscribedTouch.TouchDown += OnOutsideTouchDown;
            subscribedTouch.Tap += OnPopupItemTap;
        }

        private void ClosePopup()
        {
            // The highlight is a per-open-session cursor: left standing, reopening would show a stale highlight, and
            // a stale value that outlives a shrinking item count is what makes an out-of-range commit reachable.
            HighlightedIndex = -1;

            openedOnCanvas?.RemoveOverlay(popupRoot);
            openedOnCanvas = null;

            if (subscribedTouch != null)
            {
                subscribedTouch.TouchDown -= OnOutsideTouchDown;
                subscribedTouch.Tap -= OnPopupItemTap;
                subscribedTouch = null;
            }
        }

        private void OnOutsideTouchDown(object? sender, GenericEventArgs<Point> e)
        {
            UIElement? hit = openedOnCanvas?.HitTest(e.Data);
            for (UIElement? current = hit; current != null; current = current.Parent)
            {
                if (current == this || current == popupRoot)
                    return;
            }

            IsOpen = false;
        }

        private void OnPopupItemTap(object? sender, GenericEventArgs<Icy.Input.Events.TouchInfo> e)
        {
            UIElement? hit = openedOnCanvas?.HitTest(e.Data.LastTouch);
            for (UIElement? current = hit; current != null; current = current.Parent)
            {
                if (current is SelectorItem item && containerIndices.TryGetValue(item, out int index))
                {
                    HighlightedIndex = index;
                    SelectedIndex = index;
                    IsOpen = false;
                    return;
                }
            }
        }

        private void RepositionOpenPopup()
        {
            // The popup may have closed (or this control detached) between queuing and the layout pass.
            if (isOpen && openedOnCanvas != null)
                PositionPopup();
        }

        private void PositionPopup()
        {
            if (Canvas == null)
                return;

            popupHost.Width = ActualBounds.Width;

            // Popups are overlays, which live in surface space (see Canvas.SurfaceSize), not physical pixels.
            Point topLeft = PointToSurface(Vector2.Zero);
            Point bottomLeft = PointToSurface(new Vector2(0, ActualBounds.Height));
            Size viewport = Canvas.SurfaceSize;

            float desiredHeight = Math.Min(ExtentHeight, MaxDropDownHeight);
            float spaceBelow = viewport.Height - bottomLeft.Y;
            bool placeBelow = spaceBelow >= desiredHeight;

            popupRoot.Width = ActualBounds.Width;
            popupScrollViewer.Height = desiredHeight;
            int right = PointToSurface(new Vector2(ActualBounds.Width, 0)).X;
            int left = PopupPlacement.FitHorizontally(topLeft.X, right, (int)ActualBounds.Width, viewport.Width);
            popupRoot.Margin = placeBelow
                ? new Thickness(left, bottomLeft.Y, 0, 0)
                : new Thickness(left, (int)(topLeft.Y - desiredHeight), 0, 0);
        }

        private void PopupScrollViewer_ScrollChanged(object? sender, EventArgs e) =>
            ((IVirtualizingScrollInfo)this).OnViewportChanged(0, popupScrollViewer.VerticalOffset, popupScrollViewer.ViewportWidth, popupScrollViewer.ViewportHeight);

        /// <summary>
        /// The plain panel realized <see cref="SelectorItem"/>s are parented to inside the popup - implements
        /// <see cref="IVirtualizingScrollInfo"/> purely as a forwarding shim to the owning <see cref="Selector"/>
        /// (whose own realize/de-realize/virtualization state this panel never touches itself), so
        /// <c>PART_PopupScrollViewer</c>'s automatic <see cref="ScrollViewer.ArrangeContent"/> virtualizing delegation
        /// applies here exactly as it would for any other <see cref="ItemsControl"/>-hosted content.
        /// </summary>
        /// <remarks>
        /// Without this, <see cref="ScrollViewer"/>'s non-virtualizing branch would re-arrange every child to this
        /// panel's own full content bounds on every layout pass (see <see cref="Panel.ArrangeContent"/>), clobbering
        /// the explicit per-item positions <see cref="ItemsControl"/>'s own realize walk already set via
        /// <see cref="RealizationBounds"/>/<see cref="AttachContainer(ItemContainer, int)"/>.
        /// </remarks>
        private sealed class PopupItemsHost : Panel, IVirtualizingScrollInfo
        {
            private readonly Selector owner;

            public PopupItemsHost(Selector owner)
            {
                this.owner = owner;
            }

            /// <inheritdoc/>
            public event EventHandler<float>? VerticalOffsetCorrectionRequested
            {
                add => ((IVirtualizingScrollInfo)owner).VerticalOffsetCorrectionRequested += value;
                remove => ((IVirtualizingScrollInfo)owner).VerticalOffsetCorrectionRequested -= value;
            }

            /// <inheritdoc/>
            public event EventHandler<float>? ScrollToVerticalOffsetRequested
            {
                add => ((IVirtualizingScrollInfo)owner).ScrollToVerticalOffsetRequested += value;
                remove => ((IVirtualizingScrollInfo)owner).ScrollToVerticalOffsetRequested -= value;
            }

            /// <inheritdoc/>
            public float ExtentWidth => ((IVirtualizingScrollInfo)owner).ExtentWidth;

            /// <inheritdoc/>
            public float ExtentHeight => ((IVirtualizingScrollInfo)owner).ExtentHeight;

            /// <inheritdoc/>
            public void OnViewportChanged(float horizontalOffset, float verticalOffset, float viewportWidth, float viewportHeight) =>
                ((IVirtualizingScrollInfo)owner).OnViewportChanged(horizontalOffset, verticalOffset, viewportWidth, viewportHeight);

            /// <inheritdoc/>
            protected override void ArrangeContent()
            {
                // Deliberately does nothing - children are positioned entirely by ItemsControl.RealizeRange's own
                // explicit Arrange(targetRect) calls (via Selector.RealizationBounds/AttachContainer), the same
                // discipline ItemsControl.ArrangeContent itself follows for its own realized containers. The base
                // Panel.ArrangeContent would otherwise re-arrange every child to this panel's own full ContentBounds
                // on every layout pass, destroying that positioning.
            }

            /// <inheritdoc/>
            protected override Size MeasureContent() => new((int)ExtentWidth, (int)ExtentHeight);
        }
    }
}
