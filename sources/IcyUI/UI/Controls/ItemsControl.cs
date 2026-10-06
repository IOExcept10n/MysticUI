// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup.Attributes;
using Icy.Rendering;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Generates one <see cref="ItemContainer"/> per item in <see cref="ItemsSource"/>, via
    /// <see cref="ItemTemplate"/>/<see cref="ItemTemplateSelector"/>, and virtualizes them - only the containers
    /// intersecting the current viewport (plus a small scroll-ahead buffer) are ever realized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="IVirtualizingScrollInfo"/> directly so a hosting <see cref="ScrollViewer"/> can
    /// delegate extent/offset/viewport handling to it instead of fully measuring/arranging every item - see the
    /// Phase 2 design spec (<c>docs/superpowers/specs/2026-09-04-itemscontrol-design.md</c>) for the full
    /// virtualization/estimation design this class implements.
    /// </para>
    /// <para>
    /// Carries no selection state - see <see cref="ItemContainer"/>'s own remarks for why, and where selection is
    /// expected to be added later.
    /// </para>
    /// <para>
    /// Realization only ever happens inside <see cref="OnViewportChanged"/> - nothing here drives that call
    /// itself. It's the hosting <see cref="ScrollViewer"/> (or any other host implementing the
    /// <see cref="IVirtualizingScrollInfo"/> delegation pattern) that calls it whenever the offset or viewport
    /// size changes. Hosting an <see cref="ItemsControl"/> outside such a host - directly inside a <c>Grid</c> or
    /// <c>StackPanel</c>, say - means <see cref="OnViewportChanged"/> is never called at all, so nothing is ever
    /// realized and the control silently renders empty.
    /// </para>
    /// </remarks>
    public class ItemsControl : Control, IVirtualizingScrollInfo
    {
        private readonly List<object> items = [];
        private readonly List<float?> knownHeights = [];
        protected readonly Dictionary<int, ItemContainer> realizedContainers = [];
        private readonly Dictionary<DataTemplate, Stack<ItemContainer>> pools = [];
        private readonly Dictionary<ItemContainer, DataTemplate> containerTemplates = [];

        private IEnumerable? itemsSource;
        private INotifyCollectionChanged? observedSource;
        private DataTemplate? itemTemplate;
        private Func<object, DataTemplate>? itemTemplateSelector;
        private bool poolingEnabled = true;
        private float defaultEstimatedItemHeight = 40f;

        private float sumOfKnownHeights;
        private int knownCount;

        /// <summary>
        /// The horizontal scroll offset, in pixels, most recently reported via <see cref="OnViewportChanged(float, float, float, float)"/>.
        /// </summary>
        protected float horizontalOffset;

        /// <summary>
        /// The vertical scroll offset, in pixels, most recently reported via <see cref="OnViewportChanged(float, float, float, float)"/>.
        /// </summary>
        protected float verticalOffset;

        /// <summary>
        /// The visible viewport width, in pixels, most recently reported via <see cref="OnViewportChanged(float, float, float, float)"/>.
        /// </summary>
        protected float viewportWidth;

        /// <summary>
        /// The visible viewport height, in pixels, most recently reported via <see cref="OnViewportChanged(float, float, float, float)"/>.
        /// </summary>
        protected float viewportHeight;

        // anchorIndex/anchorOffset track the item at the top of the viewport - anchorIndex is read by
        // RecordHeight's above-viewport correction (Task 5); both are read and written by LocateViewportStart's
        // anchor walk (Task 7).
        private int anchorIndex;
        private float anchorOffset;

        // Guards OnViewportChanged against reentrancy: RealizeRange's trailing de-realize loop can call Derealize
        // -> RecordHeight -> VerticalOffsetCorrectionRequested, which a host ScrollViewer handles by setting
        // VerticalOffset, which calls back into OnViewportChanged while the outer call is still mid-walk. See
        // OnViewportChanged's own remarks for why the nested call must not re-enter the realize/de-realize logic.
        private bool isRealizingViewport;

        /// <summary>
        /// Scroll-ahead buffer, in pixels, added past the visible viewport before an item that's scrolled out is
        /// de-realized - shared by this class's own <see cref="RealizeRange(int, float)"/> and any subclass that
        /// overrides it with different geometry (e.g. a uniform grid), so the same forward-scroll headroom applies
        /// everywhere without needing to be tuned in more than one place.
        /// </summary>
        protected const float ScrollAheadBuffer = 100f;

        /// <inheritdoc/>
        public event EventHandler<float>? VerticalOffsetCorrectionRequested;

        /// <inheritdoc/>
        public event EventHandler<float>? ScrollToVerticalOffsetRequested;

        /// <summary>
        /// Gets or sets the estimated height given to an item that hasn't been realized/measured yet - used only
        /// before anything in <see cref="ItemsSource"/> has ever been realized; once at least one item has a real
        /// measured height, the running average of known heights is used instead.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value being set is not greater than zero.</exception>
        [Category("Layout")]
        [DefaultValue(40f)]
        [RegisterReference]
        public float DefaultEstimatedItemHeight
        {
            get => defaultEstimatedItemHeight;
            set
            {
                Guard.IsGreaterThan(value, 0f);

                if (SetProperty(ref defaultEstimatedItemHeight, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <inheritdoc/>
        public float ExtentWidth => viewportWidth;

        /// <inheritdoc/>
        public float ExtentHeight => ComputeExtentHeight();

        /// <summary>
        /// Computes <see cref="ExtentHeight"/> - the running-average single-column estimate by default. A subclass
        /// with different virtualization geometry (e.g. a uniform grid) overrides this with its own formula instead.
        /// </summary>
        /// <returns>The total height of all items in the collection, estimated via a running average of known heights.</returns>
        protected virtual float ComputeExtentHeight() => sumOfKnownHeights + ((items.Count - knownCount) * AverageHeight);

        /// <summary>
        /// Gets or sets the template used to build each item's visual tree, when <see cref="ItemTemplateSelector"/>
        /// doesn't resolve one for a given item.
        /// </summary>
        /// <remarks>
        /// When neither this nor <see cref="ItemTemplateSelector"/> provides a template, each item is shown as a
        /// <see cref="TextBlock"/> with its <see cref="object.ToString"/> text.
        /// </remarks>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public DataTemplate? ItemTemplate
        {
            get => itemTemplate;
            set
            {
                if (SetProperty(ref itemTemplate, value))
                    ResetRealization();
            }
        }

        /// <summary>
        /// Gets or sets a delegate that picks a <see cref="DataTemplate"/> per item, overriding
        /// <see cref="ItemTemplate"/> for items it returns one for.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public Func<object, DataTemplate>? ItemTemplateSelector
        {
            get => itemTemplateSelector;
            set
            {
                if (SetProperty(ref itemTemplateSelector, value))
                    ResetRealization();
            }
        }

        /// <summary>
        /// Gets or sets the collection this control generates one <see cref="ItemContainer"/> per item from.
        /// </summary>
        /// <remarks>
        /// When the assigned value implements <see cref="INotifyCollectionChanged"/>, this control stays
        /// live-reactive to it for as long as it remains assigned (see <see cref="OnSourceCollectionChanged"/>).
        /// A plain <see cref="IEnumerable"/> is enumerated once, into a private snapshot - later external mutation
        /// of that source is not observed.
        /// </remarks>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public IEnumerable? ItemsSource
        {
            get => itemsSource;
            set
            {
                if (SetProperty(ref itemsSource, value))
                    ResetItems();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether de-realized <see cref="ItemContainer"/>s are pooled for reuse by
        /// a later item that resolves the same <see cref="DataTemplate"/>, instead of being discarded.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        [RegisterReference]
        public bool PoolingEnabled
        {
            get => poolingEnabled;
            set
            {
                if (SetProperty(ref poolingEnabled, value) && !value)
                    pools.Clear();
            }
        }

        /// <summary>
        /// Gets the number of items currently in <see cref="ItemsSource"/>.
        /// </summary>
        protected int ItemCount => items.Count;

        /// <summary>
        /// Gets the item at <paramref name="index"/> in <see cref="ItemsSource"/>.
        /// </summary>
        /// <param name="index">A zero-based index within <c>[0, <see cref="ItemCount"/>)</c>.</param>
        protected object GetItemAt(int index) => items[index];

        /// <summary>
        /// Gets the index of <paramref name="item"/> within <see cref="ItemsSource"/>.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>The zero-based index of <paramref name="item"/>, or <c>-1</c> if it isn't in the collection (including when <paramref name="item"/> is <see langword="null"/>).</returns>
        protected int IndexOfItem(object? item) => item == null ? -1 : items.IndexOf(item);

        /// <summary>
        /// Gets the rectangle realized containers are positioned within - <see cref="Control.ContentBounds"/> by
        /// default. A subclass that displays realized containers somewhere other than its own content area (e.g. a
        /// popup) overrides this to redirect positioning, without changing the realize/de-realize/virtualization
        /// algorithm itself.
        /// </summary>
        protected virtual Rectangle RealizationBounds => ContentBounds;

        private float AverageHeight => knownCount > 0 ? sumOfKnownHeights / knownCount : DefaultEstimatedItemHeight;

        /// <summary>
        /// Gets the offset-from-anchor distance beyond which a walk (spec §5's "small delta" path) is abandoned in
        /// favor of a direct landing-index estimate (the "big jump" path) - a scrollbar-thumb drag lands far from
        /// the current anchor almost every time, where a step-by-step walk would visit most of the collection just
        /// to get there.
        /// </summary>
        private float BigJumpThreshold => Math.Max(viewportHeight * 3f, 1f);

        /// <inheritdoc/>
        public virtual void OnViewportChanged(float newHorizontalOffset, float newVerticalOffset, float newViewportWidth, float newViewportHeight)
        {
            horizontalOffset = newHorizontalOffset;
            verticalOffset = newVerticalOffset;
            viewportWidth = newViewportWidth;
            viewportHeight = newViewportHeight;

            // Re-entering while an outer OnViewportChanged call is still realizing/de-realizing (e.g. Derealize ->
            // RecordHeight -> VerticalOffsetCorrectionRequested -> ScrollViewer.VerticalOffset -> back in here,
            // see this field's declaration) must still update the offset/viewport bookkeeping above, but must NOT
            // re-enter the realize walk - the outer call is mid-iteration over realizedContainers/stillRealized
            // and would otherwise resume with stale assumptions and wrongly de-realize containers the nested call
            // just legitimately realized.
            if (isRealizingViewport)
                return;

            if (items.Count == 0)
            {
                foreach (int index in realizedContainers.Keys.ToList())
                    Derealize(index);
                return;
            }

            isRealizingViewport = true;
            try
            {
                (anchorIndex, anchorOffset) = LocateViewportStart();
                RealizeRange(anchorIndex, anchorOffset);
            }
            finally
            {
                isRealizingViewport = false;
            }
        }

        /// <summary>
        /// Scrolls the hosting <see cref="ScrollViewer"/> just enough to show the item at <paramref name="index"/> entirely.
        /// </summary>
        /// <param name="index">The index of the item to show, within <c>[0, item count)</c>.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>An item above the viewport is aligned with its top edge, and one below with its bottom edge.</description></item>
        /// <item><description>An item that is already fully visible doesn't scroll anything.</description></item>
        /// <item><description>
        /// Positions come from measured heights where they're known and from the running estimate elsewhere. Once the
        /// item is realized, the usual height correction (<see cref="IVirtualizingScrollInfo.VerticalOffsetCorrectionRequested"/>)
        /// settles any difference.
        /// </description></item>
        /// <item><description>Without a host that has laid this control out, nothing happens.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the item range.</exception>
        public void ScrollIntoView(int index)
        {
            Guard.IsInRange(index, 0, items.Count);
            if (viewportHeight <= 0)
                return;

            (float top, float height) = GetItemExtent(index);
            float target;
            if (top < verticalOffset)
                target = top;
            else if (top + height > verticalOffset + viewportHeight)
                target = Math.Min(top, top + height - viewportHeight);
            else
                return;

            ScrollToVerticalOffsetRequested?.Invoke(this, target);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Force-invalidates <see cref="Control.Chrome"/> before arranging it - see
        /// <see cref="Control.ArrangeContent"/>'s own remarks (this override replaces that base implementation
        /// entirely, so it needs the same fix independently).
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
            foreach (int index in realizedContainers.Keys.OrderBy(i => i))
                yield return realizedContainers[index];
        }

        /// <inheritdoc/>
        protected override Size MeasureContent() => new((int)ExtentWidth, (int)ExtentHeight);

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// Calls <see cref="ResetItems"/> to re-snapshot <see cref="ItemsSource"/> and re-subscribe to its
        /// <see cref="INotifyCollectionChanged.CollectionChanged"/> - not just a re-subscribe, because this control
        /// can be re-attached as the exact same instance without <see cref="ItemsSource"/> ever being reassigned.
        /// </para>
        /// <para>
        /// This matters for a <see cref="Page.KeepAlive"/> <see cref="Page"/> (see
        /// <see cref="Icy.Navigation.NavigationService"/>): navigating away detaches its <see cref="ItemsControl"/>
        /// (triggering <see cref="OnDetached"/>'s unsubscribe below), and navigating back re-attaches the very same
        /// instance - but <see cref="Page.Initialize"/> only ever runs once, so nothing re-sets
        /// <see cref="ItemsSource"/> to re-trigger its setter. Without this override, such a page's
        /// <see cref="ItemsControl"/> would stay subscribed to nothing forever after the first navigate-away,
        /// silently missing every mutation of its bound collection made while it was off-screen. Re-running
        /// <see cref="ResetItems"/> (rather than only re-subscribing) also re-enumerates <see cref="ItemsSource"/>
        /// fresh, picking up any mutations that happened while detached and unobserved, and clears
        /// <see cref="realizedContainers"/> via <see cref="ResetRealization"/> so the height cache and anchor no
        /// longer describe a collection that may have changed shape while unobserved - every container gets a
        /// clean re-realize pass on the next viewport update rather than trusting stale bookkeeping.
        /// </para>
        /// </remarks>
        protected override void OnAttached()
        {
            base.OnAttached();
            ResetItems();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Unsubscribes from <see cref="observedSource"/>'s <see cref="INotifyCollectionChanged.CollectionChanged"/>
        /// (mirroring the unsubscribe already done in <see cref="ResetItems"/> when <see cref="ItemsSource"/> is
        /// reassigned) - otherwise a long-lived <see cref="ItemsSource"/> view-model keeps this whole control (and
        /// its realized <see cref="ItemContainer"/> subtree) alive after whatever hosted it is torn down.
        /// </remarks>
        protected override void OnDetached()
        {
            base.OnDetached();

            if (observedSource != null)
                observedSource.CollectionChanged -= OnSourceCollectionChanged;
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            Chrome.Draw(context);
            foreach (int index in realizedContainers.Keys.OrderBy(i => i))
                realizedContainers[index].Draw(context);
        }

        /// <summary>
        /// Gets where the item at <paramref name="index"/> sits in this control's scrollable content - walked from the
        /// current viewport anchor through measured heights and the running estimate, the same geometry
        /// <see cref="RealizeRange(int, float)"/> positions containers with. A subclass with different geometry (e.g. a
        /// uniform grid) overrides this.
        /// </summary>
        /// <param name="index">The item's index.</param>
        /// <returns>The item's top offset and its height.</returns>
        protected virtual (float Top, float Height) GetItemExtent(int index)
        {
            float top = anchorOffset;
            if (index >= anchorIndex)
            {
                for (int i = anchorIndex; i < index; i++)
                    top += Math.Max(HeightOrEstimate(i), 1f);
            }
            else
            {
                for (int i = index; i < anchorIndex; i++)
                    top -= Math.Max(HeightOrEstimate(i), 1f);
            }

            return (top, Math.Max(HeightOrEstimate(index), 1f));
        }

        private float HeightOrEstimate(int index) => knownHeights[index] ?? AverageHeight;

        /// <summary>
        /// Finds the item whose slot contains the current <c>verticalOffset</c> - via a short walk from the last
        /// anchor for a small scroll delta, or a direct estimate for a big jump (spec §5).
        /// A subclass with different virtualization geometry overrides this to replace the algorithm entirely.
        /// </summary>
        /// <returns>A tuple containing the index of the first item to realize and the pixel offset of its top edge.</returns>
        protected virtual (int Index, float Offset) LocateViewportStart()
        {
            float distanceFromAnchor = Math.Abs(verticalOffset - anchorOffset);
            if (realizedContainers.Count == 0 || distanceFromAnchor > BigJumpThreshold)
            {
                float average = AverageHeight;
                int estimatedIndex = average > 0 ? (int)(verticalOffset / average) : 0;
                estimatedIndex = Math.Clamp(estimatedIndex, 0, items.Count - 1);
                return (estimatedIndex, estimatedIndex * average);
            }

            int index = Math.Clamp(anchorIndex, 0, items.Count - 1);
            float offset = anchorOffset;
            while (offset > verticalOffset && index > 0)
            {
                index--;
                offset -= Math.Max(HeightOrEstimate(index), 1f);
            }

            while (index < items.Count - 1 && offset + Math.Max(HeightOrEstimate(index), 1f) <= verticalOffset)
            {
                offset += Math.Max(HeightOrEstimate(index), 1f);
                index++;
            }

            return (index, offset);
        }

        /// <summary>
        /// Realizes every item whose slot overlaps the viewport (plus a forward-only scroll-ahead buffer),
        /// starting the walk at <paramref name="firstIndex"/>/<paramref name="firstOffset"/>; positions each
        /// realized container, and de-realizes anything realized but no longer in range.
        /// A subclass with different virtualization geometry overrides this to replace the algorithm entirely.
        /// </summary>
        /// <param name="firstIndex">The index of the first item to realize in this range walk.</param>
        /// <param name="firstOffset">The pixel offset of the top edge of the first item to realize.</param>
        protected virtual void RealizeRange(int firstIndex, float firstOffset)
        {
            float rangeEnd = verticalOffset + viewportHeight + ScrollAheadBuffer;

            var stillRealized = new HashSet<int>();
            int index = firstIndex;
            float offset = firstOffset;
            while (index < items.Count && offset < rangeEnd)
            {
                EnsureRealized(index);

                // Floored at 1px: a zero-height item (an empty/degenerate template, or nothing known yet with
                // DefaultEstimatedItemHeight at its own floor) must never stall this walk's forward progress -
                // offset has to advance every iteration, or the loop realizes the entire collection instead of
                // just the viewport range.
                float height = Math.Max(HeightOrEstimate(index), 1f); // may just have become known, via EnsureRealized above
                stillRealized.Add(index);

                ItemContainer container = realizedContainers[index];
                Rectangle bounds = RealizationBounds;
                var targetRect = new Rectangle(
                    bounds.X,
                    bounds.Y + (int)(offset - verticalOffset),
                    bounds.Width,
                    (int)height);
                container.InvalidateArrange();
                container.Arrange(targetRect);

                offset += height;
                index++;
            }

            DerealizeOutOfRange(stillRealized.Contains);
        }

        /// <summary>
        /// De-realizes every currently realized index for which <paramref name="isStillInRange"/> returns
        /// <see langword="false"/> - the shared trailing cleanup step of <see cref="RealizeRange(int, float)"/>,
        /// factored out so a subclass with different virtualization geometry (e.g. a uniform grid) can reuse the
        /// same de-realize sweep instead of re-implementing it.
        /// </summary>
        /// <param name="isStillInRange">Reports whether a currently realized index is still within range.</param>
        protected void DerealizeOutOfRange(Func<int, bool> isStillInRange)
        {
            foreach (int realizedIndex in realizedContainers.Keys.ToList())
            {
                if (!isStillInRange(realizedIndex))
                    Derealize(realizedIndex);
            }
        }

        /// <summary>
        /// Records <paramref name="newHeight"/> as <paramref name="index"/>'s real measured height, folding the
        /// change into <see cref="ExtentHeight"/>'s running totals - see the Phase 2 design spec §5/§6.
        /// </summary>
        private void RecordHeight(int index, float newHeight)
        {
            float? previous = knownHeights[index];
            if (previous == null)
            {
                sumOfKnownHeights += newHeight;
                knownCount++;
                knownHeights[index] = newHeight;
                return;
            }

            float delta = newHeight - previous.Value;
            if (delta == 0)
                return;

            sumOfKnownHeights += delta;
            knownHeights[index] = newHeight;

            // §6: an already-realized item resizing above the current top-visible index (anchorIndex) would
            // otherwise visibly shift everything on screen, since nothing above the viewport is supposed to move
            // it. Correct by shifting the offset itself by the same exact delta, and tell the host ScrollViewer -
            // this control's own `verticalOffset` field is a private mirror of what ScrollViewer last reported;
            // the event is what actually moves ScrollViewer.VerticalOffset (the value the scrollbar/user see).
            if (index < anchorIndex)
            {
                verticalOffset += delta;
                VerticalOffsetCorrectionRequested?.Invoke(this, delta);
            }
        }

        /// <summary>
        /// Folds a freshly measured (<see cref="EnsureRealized(int)"/>) or finally-measured
        /// (<see cref="Derealize(int)"/>) item's height into the height cache via <see cref="RecordHeight(int, float)"/>
        /// by default. A subclass whose virtualization geometry doesn't depend on measured content height (e.g. a
        /// uniform grid, where every cell uses a fixed, known-upfront size) overrides this to no-op - honoring it
        /// there would misapply the single-column height-cache's above-viewport anchor correction (see
        /// <see cref="RecordHeight(int, float)"/>) to geometry it has no bearing on.
        /// </summary>
        /// <param name="index">The item index being realized or de-realized.</param>
        /// <param name="height">The container's just-measured height.</param>
        protected virtual void RecordRealizedHeight(int index, float height) => RecordHeight(index, height);

        /// <summary>
        /// Resolves the <see cref="DataTemplate"/> to use for <paramref name="item"/> -
        /// <see cref="ItemTemplateSelector"/> first, then <see cref="ItemTemplate"/>, then the built-in
        /// <see cref="DataTemplate.Default"/> (the item's text).
        /// </summary>
        private DataTemplate ResolveTemplate(object item) =>
            ItemTemplateSelector?.Invoke(item) ?? ItemTemplate ?? DataTemplate.Default;

        /// <summary>
        /// Builds a fresh <see cref="ItemContainer"/> to host <paramref name="item"/>'s built visual tree - the
        /// default body <see cref="RentContainer(DataTemplate, object)"/> falls back to whenever pooling can't supply
        /// one. A <c>Selector</c> overrides this to realize <c>SelectorItem</c>s instead.
        /// </summary>
        /// <param name="template">The template to build <paramref name="item"/>'s content with.</param>
        /// <param name="item">The data item the new container is being realized for.</param>
        /// <returns>The freshly built container.</returns>
        protected virtual ItemContainer CreateContainer(DataTemplate template, object item)
            => new() { Content = template.Build(item) };

        /// <summary>
        /// Gets a container for <paramref name="item"/> built with <paramref name="template"/> - popped from
        /// <paramref name="template"/>'s pool and rebound when <see cref="PoolingEnabled"/> and one's available,
        /// otherwise built fresh via <see cref="DataTemplate.Build(object)"/>.
        /// </summary>
        private ItemContainer RentContainer(DataTemplate template, object item)
        {
            if (PoolingEnabled && pools.TryGetValue(template, out Stack<ItemContainer>? pool) && pool.Count > 0)
            {
                ItemContainer pooled = pool.Pop();
                if (pooled.Content != null)
                    pooled.Content.DataContext = item;
                pooled.InvalidateMeasure();
                containerTemplates[pooled] = template;
                return pooled;
            }

            ItemContainer container = CreateContainer(template, item);
            containerTemplates[container] = template;
            return container;
        }

        /// <summary>
        /// Wires a freshly realized <paramref name="container"/> into the visual tree - <see cref="UIElement.Parent"/>/
        /// <see cref="UIElement.Canvas"/> point directly at <see langword="this"/> by default. A subclass that hosts
        /// realized containers elsewhere (e.g. a popup panel) overrides this to redirect them there instead.
        /// </summary>
        /// <param name="container">The freshly realized container.</param>
        /// <param name="index">The item index <paramref name="container"/> was realized for.</param>
        protected virtual void AttachContainer(ItemContainer container, int index)
        {
            container.Parent = this;
            container.Canvas = Canvas;
        }

        /// <summary>
        /// Detaches <paramref name="container"/> from wherever <see cref="AttachContainer(ItemContainer, int)"/> wired
        /// it - the exact inverse.
        /// </summary>
        /// <param name="container">The container being de-realized.</param>
        protected virtual void DetachContainer(ItemContainer container)
        {
            container.Parent = null;
            container.Canvas = null;
        }

        /// <summary>
        /// Realizes <paramref name="index"/> if it isn't already, wiring it into the visual tree, measuring it,
        /// and folding its real height into the height cache (see <see cref="RecordHeight(int, float)"/>).
        /// </summary>
        /// <param name="index">The index of the item to ensure is realized.</param>
        protected void EnsureRealized(int index)
        {
            if (realizedContainers.ContainsKey(index))
                return;

            object item = items[index];
            DataTemplate template = ResolveTemplate(item);
            ItemContainer container = RentContainer(template, item);

            AttachContainer(container, index);
            realizedContainers[index] = container;

            float measuredHeight = container.Measure().Height;
            RecordRealizedHeight(index, measuredHeight);
        }

        /// <summary>
        /// De-realizes <paramref name="index"/> if it's currently realized - remeasures it one last time (folding
        /// any final size change into the height cache, see the Phase 2 design spec §6), detaches it, and returns
        /// it to its template's pool when <see cref="PoolingEnabled"/>.
        /// </summary>
        /// <param name="index">The index of the item to derealize.</param>
        protected void Derealize(int index)
        {
            if (!realizedContainers.Remove(index, out ItemContainer? container))
                return;

            float finalHeight = container.Measure().Height;
            if (knownHeights[index] != finalHeight)
                RecordRealizedHeight(index, finalHeight);

            DetachContainer(container);

            if (PoolingEnabled && containerTemplates.TryGetValue(container, out DataTemplate? template))
            {
                if (!pools.TryGetValue(template, out Stack<ItemContainer>? pool))
                    pools[template] = pool = new Stack<ItemContainer>();
                pool.Push(container);
            }

            containerTemplates.Remove(container);
        }

        /// <summary>
        /// Called after <c>items</c> has been mutated by any means - a full <see cref="ItemsSource"/> reassignment
        /// (<see cref="ResetItems"/>), a live incremental collection-change notification, or a full
        /// <see cref="NotifyCollectionChangedAction.Reset"/> re-snapshot. Empty by default. A subclass with state
        /// that depends on item identity/count (e.g. <see cref="SelectingItemsControl"/>'s
        /// <see cref="SelectingItemsControl.SelectedIndex"/>) overrides this to keep it valid.
        /// </summary>
        protected virtual void OnItemsChanged()
        {
        }

        private void ResetItems()
        {
            if (observedSource != null)
                observedSource.CollectionChanged -= OnSourceCollectionChanged;

            items.Clear();
            if (itemsSource != null)
            {
                foreach (object item in itemsSource)
                    items.Add(item);
            }

            observedSource = itemsSource as INotifyCollectionChanged;
            if (observedSource != null)
                observedSource.CollectionChanged += OnSourceCollectionChanged;

            ResetRealization();
            OnItemsChanged();
        }

        private void ResetRealization()
        {
            foreach (ItemContainer container in realizedContainers.Values)
            {
                DetachContainer(container);
            }

            realizedContainers.Clear();
            pools.Clear();
            containerTemplates.Clear();

            knownHeights.Clear();
            for (int i = 0; i < items.Count; i++)
                knownHeights.Add(null);
            sumOfKnownHeights = 0;
            knownCount = 0;
            anchorIndex = 0;
            anchorOffset = 0;

            InvalidateMeasure();
            InvalidateArrange();
        }

        /// <summary>
        /// Applies a single <see cref="INotifyCollectionChanged.CollectionChanged"/> notification from
        /// <see cref="ItemsSource"/> - dispatching to the matching incremental handler for
        /// <see cref="NotifyCollectionChangedAction.Add"/>/<see cref="NotifyCollectionChangedAction.Remove"/>/
        /// <see cref="NotifyCollectionChangedAction.Replace"/>/<see cref="NotifyCollectionChangedAction.Move"/>, or
        /// re-snapshotting <see cref="ItemsSource"/> entirely (like <see cref="ResetItems"/> does) for
        /// <see cref="NotifyCollectionChangedAction.Reset"/> and any unrecognized action.
        /// </summary>
        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertItems(e.NewStartingIndex, e.NewItems!);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveItems(e.OldStartingIndex, e.OldItems!.Count);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    ReplaceItems(e.OldStartingIndex, e.NewItems!);
                    break;

                case NotifyCollectionChangedAction.Move:
                    MoveItems(e.OldStartingIndex, e.NewStartingIndex, e.OldItems!.Count);
                    break;

                case NotifyCollectionChangedAction.Reset:
                default:
                    items.Clear();
                    if (itemsSource != null)
                    {
                        foreach (object item in itemsSource)
                            items.Add(item);
                    }

                    ResetRealization();
                    OnItemsChanged();
                    return;
            }

            InvalidateMeasure();
            InvalidateArrange();
            OnItemsChanged();
        }

        /// <summary>
        /// Inserts <paramref name="newItems"/> into <c>items</c> starting at <paramref name="startIndex"/>, with a
        /// matching run of unknown (<see langword="null"/>) heights - after first de-realizing everything from
        /// <paramref name="startIndex"/> onward, since it's about to be shifted to a new index.
        /// </summary>
        private void InsertItems(int startIndex, IList newItems)
        {
            DerealizeFromIndex(startIndex);

            for (int i = 0; i < newItems.Count; i++)
            {
                items.Insert(startIndex + i, newItems[i]!);
                knownHeights.Insert(startIndex + i, null);
            }
        }

        /// <summary>
        /// Removes <paramref name="count"/> items starting at <paramref name="startIndex"/> from <c>items</c>,
        /// folding any of their known heights out of the running <see cref="ExtentHeight"/> totals - after first
        /// de-realizing everything from <paramref name="startIndex"/> onward, since it's about to shift down to a
        /// new index.
        /// </summary>
        private void RemoveItems(int startIndex, int count)
        {
            DerealizeFromIndex(startIndex);

            for (int i = 0; i < count; i++)
            {
                if (knownHeights[startIndex] is { } removedHeight)
                {
                    sumOfKnownHeights -= removedHeight;
                    knownCount--;
                }

                items.RemoveAt(startIndex);
                knownHeights.RemoveAt(startIndex);
            }
        }

        /// <summary>
        /// Overwrites <paramref name="newItems"/> in place at <paramref name="startIndex"/>, forgetting each
        /// replaced slot's known height (it described the old item, not the new one) so it's re-measured fresh.
        /// </summary>
        private void ReplaceItems(int startIndex, IList newItems)
        {
            // The item object at each of these indexes changed - whatever height was known for the slot described
            // the OLD item, not this one, so it must be forgotten rather than kept.
            DerealizeFromIndex(startIndex);

            for (int i = 0; i < newItems.Count; i++)
            {
                int index = startIndex + i;
                items[index] = newItems[i]!;

                if (knownHeights[index] is { } previousHeight)
                {
                    sumOfKnownHeights -= previousHeight;
                    knownCount--;
                }

                knownHeights[index] = null;
            }
        }

        /// <summary>
        /// Moves <paramref name="count"/> items (and their known heights, carried along unchanged) from
        /// <paramref name="oldStartIndex"/> to <paramref name="newStartIndex"/> - after first de-realizing
        /// everything from the earlier of the two indexes onward, since every index in that span is about to
        /// identify a different item.
        /// </summary>
        private void MoveItems(int oldStartIndex, int newStartIndex, int count)
        {
            DerealizeFromIndex(Math.Min(oldStartIndex, newStartIndex));

            var movedItems = items.GetRange(oldStartIndex, count);
            var movedHeights = knownHeights.GetRange(oldStartIndex, count);
            items.RemoveRange(oldStartIndex, count);
            knownHeights.RemoveRange(oldStartIndex, count);

            items.InsertRange(newStartIndex, movedItems);
            knownHeights.InsertRange(newStartIndex, movedHeights);
        }

        /// <summary>
        /// De-realizes every currently-realized container at or after <paramref name="index"/> - a splice at
        /// <paramref name="index"/> changes every later index's identity, so their realized containers (keyed by
        /// index) would otherwise silently start representing the wrong item.
        /// </summary>
        private void DerealizeFromIndex(int index)
        {
            foreach (int realizedIndex in realizedContainers.Keys.Where(i => i >= index).ToList())
                Derealize(realizedIndex);
        }
    }
}
