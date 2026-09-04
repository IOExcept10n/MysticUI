// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Rendering;
using Icy.UI;
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
    /// </remarks>
    public class ItemsControl : Control, IVirtualizingScrollInfo
    {
        private readonly List<object> items = [];
        private readonly List<float?> knownHeights = [];
        private readonly Dictionary<int, ItemContainer> realizedContainers = [];
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
        private float horizontalOffset;
        private float verticalOffset;
        private float viewportWidth;
        private float viewportHeight;

        // anchorIndex is read by RecordHeight's above-viewport correction (Task 5); anchorOffset is still
        // write-only, kept up to date here so it's ready when the anchoring logic landing in Tasks 6-7 starts
        // consuming it.
        private int anchorIndex;
#pragma warning disable CS0414
        private float anchorOffset;
#pragma warning restore CS0414

        /// <inheritdoc/>
        public event EventHandler<float>? VerticalOffsetCorrectionRequested;

        /// <summary>
        /// Gets or sets the estimated height given to an item that hasn't been realized/measured yet - used only
        /// before anything in <see cref="ItemsSource"/> has ever been realized; once at least one item has a real
        /// measured height, the running average of known heights is used instead.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(40f)]
        [RegisterReference]
        public float DefaultEstimatedItemHeight
        {
            get => defaultEstimatedItemHeight;
            set
            {
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
        public float ExtentHeight => sumOfKnownHeights + ((items.Count - knownCount) * AverageHeight);

        /// <summary>
        /// Gets or sets the template used to build each item's visual tree, when <see cref="ItemTemplateSelector"/>
        /// doesn't resolve one for a given item.
        /// </summary>
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

        private float AverageHeight => knownCount > 0 ? sumOfKnownHeights / knownCount : DefaultEstimatedItemHeight;

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
        /// Resolves the <see cref="DataTemplate"/> to use for <paramref name="item"/> -
        /// <see cref="ItemTemplateSelector"/> first, falling back to <see cref="ItemTemplate"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Neither resolves a template for this item.</exception>
        private DataTemplate ResolveTemplate(object item)
        {
            DataTemplate? resolved = ItemTemplateSelector?.Invoke(item) ?? ItemTemplate;
            return resolved ?? throw new InvalidOperationException(
                $"'{nameof(ItemsControl)}' has no '{nameof(ItemTemplate)}' or '{nameof(ItemTemplateSelector)}' to build item '{item}' from.");
        }

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

            var container = new ItemContainer { Content = template.Build(item) };
            containerTemplates[container] = template;
            return container;
        }

        /// <summary>
        /// Realizes <paramref name="index"/> if it isn't already, wiring it into the visual tree, measuring it,
        /// and folding its real height into the height cache (see <see cref="RecordHeight(int, float)"/>).
        /// </summary>
        private void EnsureRealized(int index)
        {
            if (realizedContainers.ContainsKey(index))
                return;

            object item = items[index];
            DataTemplate template = ResolveTemplate(item);
            ItemContainer container = RentContainer(template, item);

            container.Parent = this;
            container.Canvas = Canvas;
            realizedContainers[index] = container;

            float measuredHeight = container.Measure().Height;
            RecordHeight(index, measuredHeight);
        }

        /// <summary>
        /// De-realizes <paramref name="index"/> if it's currently realized - remeasures it one last time (folding
        /// any final size change into the height cache, see the Phase 2 design spec §6), detaches it, and returns
        /// it to its template's pool when <see cref="PoolingEnabled"/>.
        /// </summary>
        private void Derealize(int index)
        {
            if (!realizedContainers.Remove(index, out ItemContainer? container))
                return;

            float finalHeight = container.Measure().Height;
            if (knownHeights[index] != finalHeight)
                RecordHeight(index, finalHeight);

            container.Parent = null;
            container.Canvas = null;

            if (PoolingEnabled && containerTemplates.TryGetValue(container, out DataTemplate? template))
            {
                if (!pools.TryGetValue(template, out Stack<ItemContainer>? pool))
                    pools[template] = pool = new Stack<ItemContainer>();
                pool.Push(container);
            }

            containerTemplates.Remove(container);
        }

        /// <inheritdoc/>
        public virtual void OnViewportChanged(float newHorizontalOffset, float newVerticalOffset, float newViewportWidth, float newViewportHeight)
        {
            horizontalOffset = newHorizontalOffset;
            verticalOffset = newVerticalOffset;
            viewportWidth = newViewportWidth;
            viewportHeight = newViewportHeight;
        }

        /// <inheritdoc/>
        protected override void ArrangeContent() => Chrome.Arrange(ActualBounds);

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
        protected override void OnRender(IRenderContext context)
        {
            Chrome.Draw(context);
            foreach (int index in realizedContainers.Keys.OrderBy(i => i))
                realizedContainers[index].Draw(context);
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
        }

        private void ResetRealization()
        {
            foreach (ItemContainer container in realizedContainers.Values)
            {
                container.Parent = null;
                container.Canvas = null;
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

        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Implemented in Task 8.
        }
    }
}
