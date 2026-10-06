// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TreeViewItemTests
    {
        [Fact]
        public void Update_StampsTheRowState()
        {
            var item = new TreeViewItem();
            var changed = new List<string?>();
            ((INotifyPropertyChanged)item).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            item.Update(depth: 2, indentWidth: 32, hasChildren: true, isExpanded: true, isSelectable: false);

            Assert.Equal(2, item.Depth);
            Assert.True(item.HasChildren);
            Assert.True(item.IsExpanded);
            Assert.False(item.IsSelectable);
            Assert.Equal(32f, item.IndentWidth);
            Assert.Contains(nameof(TreeViewItem.Depth), changed);
        }

        [Fact]
        public void TheThemeTemplate_IndentsAndShowsTheChevron()
        {
            (Canvas canvas, _) = CreateThemedCanvas();
            TreeViewItem item = AddItem(canvas);

            item.Update(depth: 2, indentWidth: 32, hasChildren: true, isExpanded: false, isSelectable: true);
            Assert.Equal(32f, item.IndentPart!.Width);
            Assert.Equal(IconKind.ChevronRight, item.Expander!.Kind);
            Assert.Equal(1f, item.Expander.Opacity);

            item.Update(depth: 2, indentWidth: 32, hasChildren: true, isExpanded: true, isSelectable: true);
            Assert.Equal(IconKind.ChevronDown, item.Expander.Kind);

            item.Update(depth: 0, indentWidth: 0, hasChildren: false, isExpanded: false, isSelectable: true);
            Assert.Equal(0f, item.Expander.Opacity);
            Assert.False(item.Expander.IsHitTestVisible);
        }

        [Fact]
        public void TappingTheChevron_RaisesExpanderTapped_AndNotTapped()
        {
            (Canvas canvas, FakeInputSystem input) = CreateThemedCanvas();
            TreeViewItem item = AddItem(canvas);
            item.Update(0, 0, hasChildren: true, isExpanded: false, isSelectable: true);
            canvas.Render();
            int expanderTaps = 0, rowTaps = 0;
            item.ExpanderTapped += (_, _) => expanderTaps++;
            item.Tapped += (_, _) => rowTaps++;

            Tap(input, item.Expander!);

            Assert.Equal(1, expanderTaps);
            Assert.Equal(0, rowTaps);
        }

        [Fact]
        public void TappingTheContent_RaisesTapped_AndNotExpanderTapped()
        {
            (Canvas canvas, FakeInputSystem input) = CreateThemedCanvas();
            TreeViewItem item = AddItem(canvas);
            item.Update(0, 0, hasChildren: true, isExpanded: false, isSelectable: true);
            canvas.Render();
            int expanderTaps = 0, rowTaps = 0;
            item.ExpanderTapped += (_, _) => expanderTaps++;
            item.Tapped += (_, _) => rowTaps++;

            Tap(input, item.Content!);
            Tap(input, item.Content!);

            Assert.Equal(0, expanderTaps);
            Assert.Equal(2, rowTaps);
        }

        private static TreeViewItem AddItem(Canvas canvas)
        {
            var item = new TreeViewItem
            {
                Content = new Border { Width = 60, Height = 20 },
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(item);
            canvas.Render();
            return item;
        }

        private static void Tap(FakeInputSystem input, UIElement element)
        {
            Rectangle b = element.ActualBounds;
            input.Events.Touch.RaiseTap(new TouchInfo(new Point(b.X + (b.Width / 2), b.Y + (b.Height / 2)), 1));
        }

        private static (Canvas Canvas, FakeInputSystem Input) CreateThemedCanvas()
        {
            var input = new FakeInputSystem();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new Size(800, 600) })
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets();
            IcyConfiguration config = builder.Build().UseDefaultTheme();
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
        }
    }
}
