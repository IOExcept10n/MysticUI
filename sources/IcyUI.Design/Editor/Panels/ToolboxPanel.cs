// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// Lists elements to insert into the page an <see cref="EditorSession"/> edits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Items"/> starts with the built-in controls (<see cref="ToolboxItem.CreateDefaults"/>) once a session is
    /// set. Add your game's own controls to it; a game control's snippet uses whatever prefix the page declares for it.
    /// </para>
    /// <para>
    /// Clicking an item calls <see cref="Insert"/>: into the selected element when it's a container (a
    /// <see cref="Panel"/>, or a <see cref="ContentControl"/> with no content yet), otherwise right after it. The new
    /// element is then selected. Dragging an item onto the page isn't supported yet.
    /// </para>
    /// </remarks>
    public class ToolboxPanel : ContentControl
    {
        private readonly StackPanel list = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly TextBlock status = new() { Margin = new Thickness(6, 2), Foreground = System.Drawing.Color.Salmon };
        private EditorSession? session;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolboxPanel"/> class.
        /// </summary>
        public ToolboxPanel()
        {
            Items.CollectionChanged += OnItemsChanged;
            var layout = new UI.Controls.Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var scroller = new ScrollViewer
            {
                Content = list,
                HorizontalScrollMode = ScrollMode.Disabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            UI.Controls.Grid.SetRow(status, 1);
            layout.Children.Add(scroller);
            layout.Children.Add(status);
            Content = layout;
        }

        /// <summary>
        /// Gets the elements the toolbox offers, in display order.
        /// </summary>
        public ObservableCollection<ToolboxItem> Items { get; } = [];

        /// <summary>
        /// Gets the status line: why the last insertion failed, or empty.
        /// </summary>
        public string StatusText => status.Text;

        /// <summary>
        /// Gets or sets the session to insert into, or <see langword="null"/>.
        /// </summary>
        /// <remarks>Setting a session while <see cref="Items"/> is empty fills it with the built-in controls.</remarks>
        public EditorSession? Session
        {
            get => session;
            set
            {
                session = value;
                if (value != null && Items.Count == 0)
                {
                    foreach (ToolboxItem item in ToolboxItem.CreateDefaults(value.Design.Configuration))
                        Items.Add(item);
                }
            }
        }

        /// <summary>
        /// Inserts <paramref name="item"/>'s snippet relative to the session's selection, and selects the new element.
        /// </summary>
        /// <param name="item">The item to insert.</param>
        /// <returns>The edit's result; a failure when nothing is selected or the snippet doesn't fit there.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public EditResult Insert(ToolboxItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (Session?.Selection is not { } selection || selection.Document.GetNode(selection.Node) is not { } element)
                return EditResult.Failure(default, "Select where to insert first.");

            DesignDocument document = selection.Document;
            bool container = selection.Instance is Panel or ContentControl { Content: null };
            NodeId parent;
            int index;
            if (container || element.Parent == null || document.GetNodeId(element.Parent) is not { } parentId)
            {
                parent = selection.Node;
                index = element.ContentElements.Count();
            }
            else
            {
                parent = parentId;
                index = element.Parent.ContentElements.ToList().IndexOf(element) + 1;
            }

            EditResult result = document.Editor.InsertElement(parent, index, item.Snippet);
            if (result.Succeeded && result.Node is { } inserted)
            {
                try
                {
                    Session.Select(document, inserted);
                }
                catch (ArgumentException)
                {
                    // Inserted outside the editor's scope: the edit stands, the selection stays.
                }
            }

            return result;
        }

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            list.Children.Clear();
            string? category = null;
            foreach (ToolboxItem item in Items)
            {
                if (item.Category != category)
                {
                    category = item.Category;
                    list.Children.Add(new TextBlock { Text = category, Margin = new Thickness(6, 6, 6, 2), Foreground = System.Drawing.Color.Silver });
                }

                list.Children.Add(new Button
                {
                    Content = new TextBlock { Text = item.DisplayName },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(4, 1),
                    Command = new EditorCommand(() => true, () => OnClicked(item)),
                });
            }
        }

        private void OnClicked(ToolboxItem item)
        {
            EditResult result = Insert(item);
            status.Text = result.Succeeded ? string.Empty : result.Error?.Message ?? "The element couldn't be inserted here.";
        }
    }
}
