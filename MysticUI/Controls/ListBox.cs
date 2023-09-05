namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the default implementation of the <see cref="ItemsControl"/> with ability for multiple selection.
    /// </summary>
    public class ListBox : ItemsControl
    {
        private readonly List<object> selectedItems = new();

        /// <summary>
        /// Provides the collection with all currently selected items.
        /// </summary>
        public IReadOnlyCollection<object> SelectedItems => selectedItems;

        /// <inheritdoc/>
        protected internal override void OnSelectionChanged()
        {
            base.OnSelectionChanged();
            selectedItems.Clear();
            if (ItemsSource != null)
            {
                var temp = ItemsSource.Cast<object>();
                foreach (int index in SelectedIndices)
                {
                    var item = temp.ElementAt(index);
                    selectedItems.Add(item);
                }
            }
            else
            {
                foreach (int index in SelectedIndices)
                {
                    selectedItems.Add(Items[index]);
                }
            }
            int i = 0;
            foreach (var item in Items)
            {
                if (item is ListBoxItem boxItem)
                {
                    boxItem.IsSelected = SelectedIndices.Contains(i);
                }
                i++;
            }
        }
    }
}