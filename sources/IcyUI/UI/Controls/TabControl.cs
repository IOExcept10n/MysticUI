// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A row of clickable tab headers, each showing/hiding its own <see cref="TabItem.Content"/> - realizes each
    /// <see cref="TabItem"/>'s header as a <see cref="SelectorItem"/> via the inherited
    /// <see cref="SelectingItemsControl"/> machinery, but never scrolls: unlike <see cref="ListBox"/>/
    /// <see cref="WrapGrid"/>, this control doesn't participate in <see cref="IVirtualizingScrollInfo"/>'s
    /// viewport-driven realize pipeline at all - it eagerly realizes every header itself, since a handful of tab
    /// headers never need virtualizing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ItemsControl.ItemsSource"/> holds <see cref="TabItem"/> objects. <see cref="CreateContainer"/>
    /// is overridden to build each header's <see cref="StackPanel"/>+<see cref="Icon"/>+<see cref="ContentPresenter"/>
    /// tree directly in code from <see cref="TabItem.Header"/>/<see cref="TabItem.Icon"/>, rather than going
    /// through <see cref="ItemsControl.ItemTemplate"/>/markup - this control has no dependency on any caller-supplied
    /// <see cref="ItemsControl.ItemTemplate"/>, and doesn't offer one as a header-customization hook (a caller who
    /// wants a different header layout composes a custom <see cref="TabItem.Header"/> instead).
    /// </para>
    /// <para>
    /// <see cref="ItemsControl"/>'s own realize path (<see cref="ItemsControl.EnsureRealized(int)"/>) always
    /// resolves a <see cref="Styles.DataTemplate"/> before calling <see cref="CreateContainer"/> - even though
    /// this override never calls <see cref="Styles.DataTemplate.Build(object)"/> on it - so
    /// <see cref="ItemsControl.ItemTemplate"/> is stamped with an empty, never-built sentinel
    /// <see cref="Styles.DataTemplate"/> at construction purely to satisfy that non-null requirement.
    /// </para>
    /// <para>
    /// <see cref="Control.Chrome"/> stays the plain decorative <see cref="UI.Border"/> spanning this control's
    /// whole <see cref="UIElement.ActualBounds"/> (the same convention every <see cref="ItemsControl"/> subclass
    /// already follows - realized items are extra visual children, not <see cref="Control.Chrome"/>'s child).
    /// A second, separately-managed <see cref="ContentPresenter"/> child (outside <see cref="Control.Chrome"/>,
    /// mirroring <see cref="Expander.Content"/>'s own "extra managed child" shape) shows the selected
    /// <see cref="TabItem.Content"/>, below the header row.
    /// </para>
    /// </remarks>
    public class TabControl : SelectingItemsControl
    {
        private const float HeaderSpacing = 4f;

        private readonly ContentPresenter contentPresenter = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TabControl"/> class.
        /// </summary>
        public TabControl()
        {
            contentPresenter.Parent = this;

            // Never built - see the class remarks - only assigned so ItemsControl.EnsureRealized's unconditional
            // ResolveTemplate() call doesn't throw for want of an ItemTemplate.
            ItemTemplate = new DataTemplate();

            // CreateContainer's header tree (below) has no {Binding} in it at all - unlike a markup-built
            // DataTemplate's tree, nothing in it reacts to ItemsControl.RentContainer's pooled-reuse path
            // reassigning Content.DataContext to the new item. Pooling a recycled header container across two
            // different TabItems would therefore silently keep showing the first tab's header text/icon after a
            // live ItemsSource replaces/re-adds a TabItem at the same index - disabled outright, for the same
            // "a handful of tab headers never needs it" reasoning this control already gives for skipping
            // virtualization.
            PoolingEnabled = false;
        }

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            foreach (UIElement child in base.GetVisualChildren())
                yield return child;
            yield return contentPresenter;
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            base.OnRender(context);
            if (contentPresenter.IsVisible)
                contentPresenter.Draw(context);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Eagerly realizes every header (see the class remarks - this control never virtualizes), then measures
        /// the header row's own natural size directly instead of going through <see cref="ItemsControl.ExtentWidth"/>/
        /// <see cref="ItemsControl.ExtentHeight"/> (which this control's headers never populate, since nothing
        /// calls <see cref="ItemsControl.OnViewportChanged(float, float, float, float)"/> for it).
        /// </remarks>
        protected override Size MeasureContent()
        {
            RealizeAllHeaders();

            int headerHeight = 0;
            int headerWidth = 0;
            for (int i = 0; i < ItemCount; i++)
            {
                Size headerSize = realizedContainers[i].Measure();
                headerHeight = Math.Max(headerHeight, headerSize.Height);
                headerWidth += headerSize.Width + (i > 0 ? (int)HeaderSpacing : 0);
            }

            Size contentSize = contentPresenter.Measure();
            return new Size(Math.Max(headerWidth, contentSize.Width), headerHeight + contentSize.Height);
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            int headerHeight = 0;
            for (int i = 0; i < ItemCount; i++)
                headerHeight = Math.Max(headerHeight, realizedContainers[i].Measure().Height);

            int x = ContentBounds.X;
            for (int i = 0; i < ItemCount; i++)
            {
                ItemContainer header = realizedContainers[i];
                Size headerSize = header.Measure();
                header.InvalidateArrange();
                header.Arrange(new Rectangle(x, ContentBounds.Y, headerSize.Width, headerHeight));
                x += headerSize.Width + (int)HeaderSpacing;
            }

            contentPresenter.InvalidateArrange();
            contentPresenter.Arrange(new Rectangle(
                ContentBounds.X,
                ContentBounds.Y + headerHeight,
                ContentBounds.Width,
                Math.Max(0, ContentBounds.Height - headerHeight)));

            Chrome.InvalidateArrange();
            Chrome.Arrange(ActualBounds);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Builds the header's visual tree directly in code - a <see cref="StackPanel"/> holding the source
        /// <see cref="TabItem.Icon"/> (when set) followed by a <see cref="ContentPresenter"/> wrapping
        /// <see cref="TabItem.Header"/> - instead of going through <paramref name="template"/> (see the class
        /// remarks for why <paramref name="template"/> is otherwise unused here).
        /// </remarks>
        /// <param name="template">Unused - see the class remarks.</param>
        /// <param name="item">The <see cref="TabItem"/> the new header is being realized for.</param>
        /// <returns>A freshly built <see cref="SelectorItem"/> hosting the header's visual tree.</returns>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
        {
            var tabItem = (TabItem)item;
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            if (tabItem.Icon != null)
                header.Children.Add(tabItem.Icon);
            header.Children.Add(new ContentPresenter
            {
                Content = tabItem.Header as UIElement ?? new TextBlock { Text = tabItem.Header?.ToString() ?? string.Empty },
                VerticalAlignment = VerticalAlignment.Center,
            });
            return new SelectorItem { Content = header };
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Also stamps the freshly realized header's <see cref="UIElement.IsEnabled"/> from the source
        /// <see cref="TabItem.IsEnabled"/> - a one-time snapshot at realize time, the same "stamped once, not kept
        /// reactively in sync" limitation <see cref="SelectingItemsControl.AttachContainer(ItemContainer, int)"/>'s
        /// own <see cref="SelectorItem.IsSelected"/> stamp has. Toggling <see cref="TabItem.IsEnabled"/> after this
        /// tab has already been realized doesn't update its live header until the next realize pass - acceptable
        /// for this control's actual use (tabs configured once, not dynamically enabled/disabled at runtime),
        /// flagged rather than solved with new reactive plumbing this control doesn't otherwise need.
        /// </remarks>
        protected override void AttachContainer(ItemContainer container, int index)
        {
            base.AttachContainer(container, index);
            container.IsEnabled = ((TabItem)GetItemAt(index)).IsEnabled;
        }

        /// <inheritdoc/>
        protected override void OnSelectionChanged()
        {
            contentPresenter.Content = (SelectedItem as TabItem)?.Content;
        }

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            contentPresenter.Canvas = Canvas;
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            contentPresenter.Canvas = null;
            base.OnDetached();
        }

        private void RealizeAllHeaders()
        {
            for (int i = 0; i < ItemCount; i++)
                EnsureRealized(i);
            DerealizeOutOfRange(index => index < ItemCount);
        }
    }
}
