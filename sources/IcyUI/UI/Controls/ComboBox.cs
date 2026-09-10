// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.ComponentModel;
using Icy.Data.Markup.Attributes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="Selector"/> with an editable text field that live-filters items by a case-insensitive
    /// <c>Contains</c> match against <see cref="Selector.DisplayMemberPath"/> - selection-only, not free text
    /// entry (see the class remarks on <see cref="ItemsSource"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="ItemsSource"/> shadows (does not override - <see cref="ItemsControl.ItemsSource"/> isn't
    /// virtual) the inherited property, the same way <see cref="Control.Padding"/> already shadows
    /// <see cref="UIElement.Padding"/>: this control's real, full source is held here, and the base
    /// <see cref="ItemsControl.ItemsSource"/> is kept pointed at whatever subset currently matches the filter
    /// text. <see cref="Selector.SelectedItem"/>/<see cref="Selector.SelectedIndex"/> reflect the filtered
    /// subset only while actively filtering - full selection is restored on every commit path (a matching
    /// Enter, or a revert via Escape/non-matching Enter).
    /// </remarks>
    public class ComboBox : Selector
    {
        private readonly TextBox textBox;
        private object? lastCommittedItem;
        private IEnumerable? realItemsSource;
        private bool suppressSelectionChanged;
        private bool suppressTextChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComboBox"/> class.
        /// </summary>
        public ComboBox()
        {
            textBox = new TextBox();
            textBox.TextChanged += TextBox_TextChanged;

            // Order matters: swap chromePanel into Chrome BEFORE adding Toggle to it. Border.Child's setter
            // unconditionally nulls the OLD child's Parent/Canvas when replaced - if Toggle were added to
            // chromePanel first (setting Toggle.Parent = chromePanel via Panel.OnChildAdded), the immediately
            // following `Chrome.Child = chromePanel` assignment would still see `toggle` as Chrome's current old
            // child and null out Toggle.Parent/Canvas right back out from under it (Border.Child has no way to
            // know Toggle was already reparented elsewhere) - exactly the hazard ContentControl.Content's own
            // remarks describe for the same reason. Swapping chromePanel in first means Chrome's old-child
            // cleanup fires while chromePanel is still empty (a legitimate unparent, not a clobber), and Toggle's
            // Parent is set correctly afterward.
            var chromePanel = new StackPanel { Orientation = Orientation.Horizontal };
            ((Border)Chrome).Child = chromePanel;
            chromePanel.Children.Add(textBox);
            chromePanel.Children.Add(Toggle);

            IsFocusable = false;
            HookFocusGate(textBox);

            SelectionChanged += (_, _) =>
            {
                if (!suppressSelectionChanged)
                    lastCommittedItem = SelectedItem;
            };
        }

        /// <summary>
        /// Gets or sets the collection this control's items are drawn from - shadows
        /// <see cref="ItemsControl.ItemsSource"/> to apply <see cref="TextBox.Text"/>'s filter on top.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public new IEnumerable? ItemsSource
        {
            get => realItemsSource;
            set
            {
                realItemsSource = value;
                ApplyFilter(textBox.Text);
            }
        }

        /// <inheritdoc/>
        protected override void OnNavigationSelectElement(object? sender, EventArgs e)
        {
            if (!IsOpen)
            {
                IsOpen = true;
                return;
            }

            if (HighlightedIndex >= 0)
            {
                SelectedIndex = HighlightedIndex;
                lastCommittedItem = SelectedItem;
                SetTextFromSelection();
                IsOpen = false;
            }
            else
            {
                RevertText();
                IsOpen = false;
            }
        }

        /// <inheritdoc/>
        protected override void OnNavigationCloseModal(object? sender, EventArgs e)
        {
            RevertText();
            IsOpen = false;
        }

        private void ApplyFilter(string filterText)
        {
            if (realItemsSource == null)
            {
                base.ItemsSource = null;
                return;
            }

            List<object> matches = [];
            foreach (object item in realItemsSource)
            {
                if (string.IsNullOrEmpty(filterText) || GetDisplayText(item).Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    matches.Add(item);
            }

            base.ItemsSource = matches;

            // Suppressed: this assignment reflects the filtered subset's own selection state (see the class
            // remarks on ItemsSource), not a genuine user commit - without this guard, the SelectionChanged
            // subscription above would overwrite lastCommittedItem with whatever this filtering pass happens to
            // leave selected (including null, on every keystroke that filters the committed item out), corrupting
            // the value RevertText/OnNavigationSelectElement rely on to restore full selection later.
            suppressSelectionChanged = true;
            SelectedItem = lastCommittedItem != null && matches.Contains(lastCommittedItem) ? lastCommittedItem : null;
            suppressSelectionChanged = false;
        }

        private void RevertText()
        {
            suppressTextChanged = true;
            SetTextFromSelection();
            suppressTextChanged = false;
            ApplyFilter(textBox.Text);
        }

        private void SetTextFromSelection() =>
            textBox.Text = lastCommittedItem == null ? string.Empty : GetDisplayText(lastCommittedItem);

        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            if (suppressTextChanged)
                return;
            IsOpen = true;
            ApplyFilter(textBox.Text);
        }
    }
}
