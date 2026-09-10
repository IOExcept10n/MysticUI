// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using Icy.Data.Bindings;
using Icy.Data.Markup.Attributes;
using Icy.Input.Events;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Adds single selection and a popup-hosted item list on top of <see cref="ItemsControl"/> - the shared base
    /// for <see cref="Dropdown"/> and <see cref="ComboBox"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Realizes <see cref="SelectorItem"/>s (see <see cref="CreateContainer(DataTemplate, object)"/>) instead of
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
    public abstract class Selector : ItemsControl
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
        private int selectedIndex = -1;
        private object? selectedItem;
        private object? selectedValue;
        private DynamicPropertyPath? selectedValuePath;
        private string? selectedValuePathText;
        private INavigationEvents? subscribedNavigation;
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

            // Safe here, at construction: Chrome is always the default Border until/unless Template is set later,
            // in which case OnApplyTemplate re-wires appropriately (mirrors Expander/SplitPane).
            ((Border)Chrome).Child = toggle;

            HookFocusGate(this);
        }

        /// <summary>
        /// Occurs when <see cref="SelectedIndex"/>/<see cref="SelectedItem"/> changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

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
        /// Gets or sets the currently selected item's index, or <c>-1</c> for no selection.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than <c>-1</c> or greater than or equal to the item count.</exception>
        [Category("Behavior")]
        [DefaultValue(-1)]
        [RegisterReference]
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                Guard.IsGreaterThanOrEqualTo(value, -1);
                if (value != -1)
                    Guard.IsLessThan(value, ItemCount);
                if (selectedIndex == value)
                    return;

                if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? oldContainer))
                    ((SelectorItem)oldContainer).IsSelected = false;

                selectedIndex = value;
                selectedItem = selectedIndex == -1 ? null : GetItemAt(selectedIndex);
                UpdateSelectedValue();

                if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? newContainer))
                    ((SelectorItem)newContainer).IsSelected = true;

                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the currently selected item, or <see langword="null"/> for no selection. Setting an item
        /// not present in <see cref="ItemsControl.ItemsSource"/> clears selection instead of throwing.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? SelectedItem
        {
            get => selectedItem;
            set => SelectedIndex = IndexOfItem(value);
        }

        /// <summary>
        /// Gets the value <see cref="SelectedValuePath"/> resolves from <see cref="SelectedItem"/> -
        /// <see langword="null"/> when <see cref="SelectedItem"/>/<see cref="SelectedValuePath"/> is
        /// <see langword="null"/>, or when the path fails to resolve for the current item.
        /// </summary>
        [Category("Data")]
        [Browsable(false)]
        public object? SelectedValue => selectedValue;

        /// <summary>
        /// Gets or sets the path (resolved dynamically) used to derive <see cref="SelectedValue"/> from
        /// <see cref="SelectedItem"/>.
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
        protected override void AttachContainer(ItemContainer container, int index)
        {
            var item = (SelectorItem)container;
            item.IsSelected = index == SelectedIndex;
            item.IsHighlighted = index == HighlightedIndex;
            popupHost.Children.Add(container);
        }

        /// <inheritdoc/>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
            => new SelectorItem { Content = template.Build(item) };

        /// <inheritdoc/>
        protected override void DetachContainer(ItemContainer container) => popupHost.Children.Remove(container);

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
            UnsubscribeNavigation();
            base.OnDetached();
        }

        /// <summary>
        /// Re-points which element's <see cref="UIElement.FocusChanged"/> is expected to gate keyboard/gamepad
        /// navigation subscription - <see langword="this"/> by default (wired once, at construction).
        /// </summary>
        /// <param name="gate">The element whose focus state should drive navigation subscription from now on.</param>
        /// <remarks>
        /// This task (properties/composition/popup lifecycle) only establishes the <c>focusGate</c> field slot a
        /// later task's full keyboard/gamepad navigation wiring builds on - it does not yet subscribe to
        /// <paramref name="gate"/>'s <see cref="UIElement.FocusChanged"/> or drive
        /// <see cref="Icy.Input.Events.INavigationEvents"/> subscription from it. See that task for the complete
        /// behavior <see cref="ComboBox"/> relies on to redirect this to its own text box.
        /// </remarks>
        protected void HookFocusGate(UIElement gate) => focusGate = gate;

        /// <summary>
        /// Unsubscribes from <see cref="Icy.Input.Events.INavigationEvents"/> - idempotent.
        /// </summary>
        /// <remarks>
        /// Scaffolding for the same later navigation wiring <see cref="HookFocusGate(UIElement)"/>'s remarks
        /// describe - nothing in this task ever populates the backing subscription, so this is a safe no-op until
        /// that wiring is added; kept here (rather than deferred entirely) because <see cref="OnDetached"/> must
        /// always be able to call it safely regardless of which task last touched this file.
        /// </remarks>
        protected void UnsubscribeNavigation() => subscribedNavigation = null;

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
            selectedValue = selectedItem == null || selectedValuePath == null ? null : selectedValuePath.GetValue(selectedItem);
        }

        private void Toggle_IsCheckedChanged(object? sender, EventArgs e) => IsOpen = toggle.IsChecked;

        private void OpenPopup()
        {
            if (Canvas == null)
                return;

            openedOnCanvas = Canvas;
            PositionPopup();
            Canvas.AddOverlay(popupRoot);
            ((IVirtualizingScrollInfo)this).OnViewportChanged(0, popupScrollViewer.VerticalOffset, popupScrollViewer.ViewportWidth, popupScrollViewer.ViewportHeight);
        }

        private void ClosePopup()
        {
            openedOnCanvas?.RemoveOverlay(popupRoot);
            openedOnCanvas = null;
        }

        private void PositionPopup()
        {
            if (Canvas == null)
                return;

            popupHost.Width = ActualBounds.Width;

            Point topLeft = PointToScreen(Vector2.Zero);
            Point bottomLeft = PointToScreen(new Vector2(0, ActualBounds.Height));
            Size viewport = Canvas.Configuration.RenderContext.ViewportSize;

            float desiredHeight = Math.Min(ExtentHeight, MaxDropDownHeight);
            float spaceBelow = viewport.Height - bottomLeft.Y;
            bool placeBelow = spaceBelow >= desiredHeight;

            popupRoot.Width = ActualBounds.Width;
            popupScrollViewer.Height = desiredHeight;
            popupRoot.Margin = placeBelow
                ? new Thickness(bottomLeft.X, bottomLeft.Y, 0, 0)
                : new Thickness(topLeft.X, (int)(topLeft.Y - desiredHeight), 0, 0);
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
