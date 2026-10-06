// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;

namespace Icy.SharedSamples
{
    /// <summary>
    /// <see cref="TreeView"/>: a static tree written in markup with non-selectable categories, and a live tree built in
    /// code whose buttons add, remove, rename and reveal nodes.
    /// </summary>
    public static class TreeViewDemo
    {
        /// <summary>
        /// The demo page.
        /// </summary>
        public const string Markup =
            """
            <StackPanel Orientation="Vertical" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="20">
              <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">TreeView</TextBlock>
              <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Click a row, its chevron, or use the arrow keys. Categories on the left can't be selected: clicking one opens or closes it.</TextBlock>
              <StackPanel Orientation="Horizontal">
                <TreeView x:Name="StaticTree" Width="260" Height="320" Margin="0,0,16,0">
                  <TreeViewNode Header="Basics" IsSelectable="False" IsExpanded="True">
                    <TreeViewNode Header="Controls"/>
                    <TreeViewNode Header="Styles"/>
                  </TreeViewNode>
                  <TreeViewNode Header="Items" IsSelectable="False">
                    <TreeViewNode Header="ListBox"/>
                    <TreeViewNode Header="WrapGrid"/>
                    <TreeViewNode Header="Nested" IsSelectable="False">
                      <TreeViewNode Header="Deep leaf"/>
                    </TreeViewNode>
                  </TreeViewNode>
                  <TreeViewNode Header="Design tools" IsSelectable="False">
                    <TreeViewNode Header="Design"/>
                    <TreeViewNode Header="Editor"/>
                  </TreeViewNode>
                </TreeView>
                <StackPanel Orientation="Vertical">
                  <TreeView x:Name="LiveTree" Width="300" Height="270"/>
                  <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                    <Button x:Name="AddButton" Padding="10,4" Margin="0,0,6,0">Add child</Button>
                    <Button x:Name="RemoveButton" Padding="10,4" Margin="0,0,6,0">Remove</Button>
                    <Button x:Name="RenameButton" Padding="10,4" Margin="0,0,6,0">Rename</Button>
                    <Button x:Name="RevealButton" Padding="10,4">Reveal deep</Button>
                  </StackPanel>
                </StackPanel>
              </StackPanel>
              <TextBlock x:Name="Status" FontSize="14" Foreground="WhiteSmoke" Margin="0,12,0,0">Nothing selected yet.</TextBlock>
            </StackPanel>
            """;

        /// <summary>
        /// Builds the demo.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The demo's root element.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(Markup, nameof(TreeViewDemo));
            var status = root.FindRequiredControl<TextBlock>("Status");
            var staticTree = root.FindRequiredControl<TreeView>("StaticTree");
            var live = root.FindRequiredControl<TreeView>("LiveTree");

            var roots = new ObservableCollection<DemoNode>
            {
                N("Europe", N("North", N("Oslo"), N("Helsinki")), N("South", N("Rome")), N("West", N("Lisbon"))),
                N("Asia", N("East", N("Tokyo", N("Shibuya", N("Street")))), N("South", N("Mumbai"))),
                N("Americas", N("North", N("Toronto")), N("South", N("Lima"))),
            };
            DemoNode deep = roots[1].Children[0].Children[0].Children[0].Children[0];
            int counter = 0;

            live.ItemTemplate = (DataTemplate)loader.LoadObject("""<DataTemplate><TextBlock Text="{Binding Name}" Foreground="WhiteSmoke"/></DataTemplate>""");
            live.ChildrenSelector = node => ((DemoNode)node).Children;
            live.ItemsSource = roots;

            staticTree.SelectionChanged += (_, _) => status.Text = $"Static tree: {staticTree.SelectedItem?.ToString() ?? "nothing"} selected.";
            live.SelectionChanged += (_, _) => status.Text = $"Live tree: {live.SelectedItem?.ToString() ?? "nothing"} selected.";
            live.ItemExpanded += (_, e) => status.Text = $"Live tree: {e.Item} expanded.";
            live.ItemCollapsed += (_, e) => status.Text = $"Live tree: {e.Item} collapsed.";

            root.FindRequiredControl<Button>("AddButton").Click += (_, _) =>
            {
                DemoNode parent = live.SelectedItem as DemoNode ?? roots[0];
                parent.Children.Add(new DemoNode($"New {++counter}"));
                live.Expand(parent);
            };
            root.FindRequiredControl<Button>("RemoveButton").Click += (_, _) =>
            {
                if (live.SelectedItem is not DemoNode selected)
                    return;
                if (!roots.Remove(selected))
                    FindParent(roots, selected)?.Children.Remove(selected);
            };
            root.FindRequiredControl<Button>("RenameButton").Click += (_, _) =>
            {
                if (live.SelectedItem is DemoNode selected)
                    selected.Name += "*";
            };
            root.FindRequiredControl<Button>("RevealButton").Click += (_, _) => live.SelectedItem = deep;

            return root;
        }

        private static DemoNode N(string name, params DemoNode[] children) => new(name, children);

        private static DemoNode? FindParent(ObservableCollection<DemoNode> nodes, DemoNode child)
        {
            foreach (DemoNode node in nodes)
            {
                if (node.Children.Contains(child))
                    return node;
                if (FindParent(node.Children, child) is { } parent)
                    return parent;
            }

            return null;
        }

        /// <summary>
        /// A node of the live tree: a renamable name and its children.
        /// </summary>
        /// <param name="name">The node's name.</param>
        /// <param name="children">The node's initial children.</param>
        public sealed class DemoNode(string name, params DemoNode[] children) : INotifyPropertyChanged
        {
            private string name = name;

            /// <inheritdoc/>
            public event PropertyChangedEventHandler? PropertyChanged;

            /// <summary>Gets or sets the node's name.</summary>
            public string Name
            {
                get => name;
                set
                {
                    name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }

            /// <summary>Gets the node's children.</summary>
            public ObservableCollection<DemoNode> Children { get; } = new(children);

            /// <inheritdoc/>
            public override string ToString() => name;
        }
    }
}
