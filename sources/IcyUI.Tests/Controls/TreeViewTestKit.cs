// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;

namespace Icy.Tests.Controls
{
    /// <summary>Shared builders and probes for the TreeView test files.</summary>
    internal static class TreeViewTestKit
    {
        public static DataTemplate RowTemplate() => LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>""");

        public static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            return (DataTemplate)new MarkupLoader(configuration).LoadObject(markup);
        }

        public static TreeViewNode Node(string header, params TreeViewNode[] children)
        {
            var node = new TreeViewNode(header);
            foreach (TreeViewNode child in children)
                node.Children.Add(child);
            return node;
        }

        public static TreeViewNode Expanded(string header, params TreeViewNode[] children)
        {
            TreeViewNode node = Node(header, children);
            node.IsExpanded = true;
            return node;
        }

        public static TreeView CreateTree(params TreeViewNode[] roots)
        {
            var tree = new TreeView { ItemTemplate = RowTemplate() };
            foreach (TreeViewNode root in roots)
                tree.Items.Add(root);
            return tree;
        }

        /// <summary>Lays the tree out without a canvas: the inner ScrollViewer realizes the visible rows.</summary>
        public static void Layout(TreeView tree, int width = 200, int height = 400)
        {
            tree.InvalidateArrange();
            tree.Arrange(new Rectangle(0, 0, width, height));
        }

        public static string[] Visible(TreeView tree) => [.. tree.Rows.Select(row => row.Item.ToString() ?? string.Empty)];

        public static int[] Depths(TreeView tree) => [.. tree.Rows.Select(row => row.Depth)];

        public static TreeViewItem Row(TreeView tree, int index) => (TreeViewItem)tree.List.Realized[index];

        public static (Canvas Canvas, FakeInputSystem Input) CreateCanvas(bool themed = false)
        {
            var input = new FakeInputSystem();
            if (themed)
            {
                var builder = new IcyConfigurationBuilder();
                builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new Size(800, 600) })
                       .ConfigureInput(input)
                       .ConfigureTypes()
                       .ConfigureAssets();
                return (new Canvas(builder.Build().UseDefaultTheme()) { IsInputEnabled = true, IsVisible = true }, input);
            }

            var config = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext { ViewportSize = new Size(800, 600) }, new ReflectionConfiguration());
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
        }

        /// <summary>Puts the tree at the canvas's top-left, 200 x 400, and renders once.</summary>
        public static void Host(Canvas canvas, TreeView tree)
        {
            tree.Width = 200;
            tree.Height = 400;
            tree.HorizontalAlignment = HorizontalAlignment.Left;
            tree.VerticalAlignment = VerticalAlignment.Top;
            canvas.Add(tree);
            canvas.Render();
        }

        public static void Tap(FakeInputSystem input, UIElement element)
        {
            Rectangle b = element.ActualBounds;
            input.Events.Touch.RaiseTap(new TouchInfo(new Point(b.X + (b.Width / 2), b.Y + (b.Height / 2)), 1));
        }
    }
}
