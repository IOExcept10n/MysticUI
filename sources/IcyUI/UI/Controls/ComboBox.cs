// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Icy.Data.Markup.Attributes;
using Icy.UI.Styles;

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
    /// text. <see cref="SelectingItemsControl.SelectedItem"/>/<see cref="SelectingItemsControl.SelectedIndex"/> reflect the filtered
    /// subset only while actively filtering - full selection is restored on every commit path (a matching
    /// Enter, or a revert via Escape/non-matching Enter).
    /// </remarks>
    public class ComboBox : Selector
    {
        private readonly TextBox textBox;
        private object? lastCommittedItem;
        private INotifyCollectionChanged? observedRealSource;
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

            // A no-op Style, not left null: an implicit (keyless, type-targeted) TextBox style applies to ANY
            // TextBox instance that has no Style set by the time it attaches - see UIElement.Style's own remarks -
            // and this textBox is a genuine nested TextBox, so it would otherwise pick up the theme's own
            // Background/BorderBrush/BorderThickness/Padding on top of this ComboBox's OWN chrome, drawing a
            // second, smaller border+background floating inside the first and eating into the already-narrow
            // Grid cell twice over. This textBox is meant to look like a bare text-entry surface sitting directly
            // on ComboBox's own chrome, not a self-bordered control in its own right.
            textBox.Style = new Style(typeof(TextBox));

            // Order matters: swap chromePanel into Chrome BEFORE adding its children. Border.Child's setter
            // unconditionally nulls the OLD child's Parent/Canvas when replaced - if Toggle were added to
            // chromePanel first (setting Toggle.Parent = chromePanel via Panel.OnChildAdded), the immediately
            // following `Chrome.Child = chromePanel` assignment would still see `toggle` as Chrome's current old
            // child and null out Toggle.Parent/Canvas right back out from under it (Border.Child has no way to
            // know Toggle was already reparented elsewhere) - exactly the hazard ContentControl.Content's own
            // remarks describe for the same reason. Swapping chromePanel in first means Chrome's old-child
            // cleanup fires while chromePanel is still empty (a legitimate unparent, not a clobber), and Toggle's
            // Parent is set correctly afterward.
            //
            // A Grid, not a horizontal StackPanel: StackPanel sizes every child to its own natural/measured
            // width and never distributes leftover space, so textBox and Toggle would both collapse to their
            // tiny natural sizes and leave the rest of this control's own Width empty. A two-column Grid (Star +
            // Auto) gives textBox the whole remaining width and Toggle its own natural size on the right.
            //
            // The row must be explicit Auto, not left as Grid's implicit single row: Grid.MeasureContent()
            // deliberately measures every Star track as 0 (Star sizing is only resolved later, at arrange time,
            // once real available space is known - see Grid's own remarks) - and an empty RowDefinitions
            // collection means that lone implicit row IS Star. Left implicit, this whole Grid's (and therefore
            // this ComboBox's, when nothing gives it an explicit Height) measured height for layout is always 0,
            // regardless of what textBox/Toggle actually need - Auto instead sizes the row to the taller child.
            var chromePanel = new Grid();
            chromePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            chromePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            chromePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ((Border)Chrome).Child = chromePanel;
            chromePanel.Children.Add(textBox);
            chromePanel.Children.Add(Toggle);
            Grid.SetColumn(textBox, 0);
            Grid.SetColumn(Toggle, 1);

            IsFocusable = false;
            HookFocusGate(textBox);
            textBox.FocusChanged += TextBox_FocusChanged;

            // Regression: a mouse/touch commit (Selector.OnPopupItemTap sets SelectedIndex directly, then closes
            // the popup) never routed through this ComboBox's own OnNavigationSelectElement (the Enter-key path,
            // the only place that used to call SetTextFromSelection) - so clicking a popup item updated
            // SelectedItem/lastCommittedItem's bookkeeping but left textBox.Text showing whatever the user had
            // typed to filter, never the item they actually picked. SelectionChanged is the one hook every commit
            // path already funnels through (Enter's own SelectedIndex assignment fires it too), so syncing the
            // text here - once, guarded exactly like OnNavigationSelectElement's own explicit call already is -
            // covers every current and future way a selection can be committed.
            SelectionChanged += (_, _) =>
            {
                if (suppressSelectionChanged)
                    return;

                lastCommittedItem = SelectedItem;

                suppressTextChanged = true;
                SetTextFromSelection();
                suppressTextChanged = false;

                // The popup was filtered down to whatever the user had typed before committing - without this,
                // the list stays narrowed to (essentially) the just-picked item the next time the popup opens.
                ApplyFilter(string.Empty);
            };

            // Regression: SelectedIndex's own setter no-ops - never firing SelectionChanged at all - when the
            // value it's given already equals the current selection. ApplyFilter's "restore the previous
            // selection if the still-filtered list still contains it" logic (a few lines up) can already have
            // re-selected the very item the user is about to click - e.g. filter down to a single match that
            // happens to be the item already selected from an earlier commit - so OnPopupItemTap's own
            // SelectedIndex assignment silently no-ops and the SelectionChanged-based sync above never runs,
            // leaving textBox.Text showing stale filter text instead of the (unchanged, but just re-confirmed)
            // selection. Syncing here instead - unconditionally, whenever the popup closes, for ANY reason
            // (commit or revert) - never depends on SelectedIndex having actually changed value.
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(IsOpen) || IsOpen)
                    return;

                lastCommittedItem = SelectedItem;
                suppressTextChanged = true;
                SetTextFromSelection();
                suppressTextChanged = false;
                ApplyFilter(string.Empty);
            };
        }

        /// <summary>
        /// Gets or sets the collection this control's items are drawn from - shadows
        /// <see cref="ItemsControl.ItemsSource"/> to apply <see cref="TextBox.Text"/>'s filter on top.
        /// </summary>
        /// <remarks>
        /// Because the base <see cref="ItemsControl.ItemsSource"/> is only ever pointed at a throwaway filtered
        /// snapshot, <see cref="ItemsControl"/>'s own <see cref="INotifyCollectionChanged"/> observation never sees
        /// the real collection - so this setter subscribes to it here instead, re-running the filter whenever it
        /// changes (see <see cref="OnDetached"/> for the matching unsubscribe).
        /// </remarks>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public new IEnumerable? ItemsSource
        {
            get => realItemsSource;
            set
            {
                if (observedRealSource != null)
                    observedRealSource.CollectionChanged -= RealSource_CollectionChanged;

                realItemsSource = value;
                observedRealSource = value as INotifyCollectionChanged;
                if (observedRealSource != null)
                    observedRealSource.CollectionChanged += RealSource_CollectionChanged;

                ApplyFilter(textBox.Text);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Unsubscribes from the real (unfiltered) <see cref="ItemsSource"/>'s
        /// <see cref="INotifyCollectionChanged.CollectionChanged"/>, mirroring what
        /// <see cref="ItemsControl.OnDetached"/> already does for the base class's own observed source - otherwise a
        /// long-lived view-model collection keeps this whole control alive after its host is torn down.
        /// </remarks>
        protected override void OnDetached()
        {
            base.OnDetached();

            if (observedRealSource != null)
                observedRealSource.CollectionChanged -= RealSource_CollectionChanged;
        }

        /// <inheritdoc/>
        protected override void OnNavigationSelectElement(object? sender, EventArgs e)
        {
            if (!IsOpen)
            {
                IsOpen = true;
                return;
            }

            CommitHighlightOrRevert();
        }

        /// <inheritdoc/>
        protected override void OnNavigationCloseModal(object? sender, EventArgs e)
        {
            RevertText();
            IsOpen = false;
        }

        /// <summary>
        /// Commits <see cref="Selector.HighlightedIndex"/> as the new selection if it's still valid, otherwise
        /// reverts <see cref="TextBox.Text"/> back to the last real selection - and closes the popup either way.
        /// </summary>
        /// <remarks>
        /// Shared by both the Enter-key commit path (<see cref="OnNavigationSelectElement"/>, after its own
        /// "open if closed" check) and losing focus (<see cref="TextBox_FocusChanged"/>) - standard combobox UX
        /// commits pending navigation on blur exactly like Enter would, it just must never also reopen a closed
        /// popup the way Enter's own leading check does.
        /// </remarks>
        private void CommitHighlightOrRevert()
        {
            // Upper bound guarded too - see Selector.OnNavigationSelectElement's own remarks: filtering can shrink
            // the list underneath a stale highlight between the arrow press that set it and this commit.
            if (HighlightedIndex >= 0 && HighlightedIndex < ItemCount)
            {
                // The SelectionChanged subscription above takes care of lastCommittedItem/SetTextFromSelection/
                // ApplyFilter(string.Empty) - every commit path funnels through this one assignment.
                SelectedIndex = HighlightedIndex;
            }
            else
            {
                RevertText();
            }

            IsOpen = false;
        }

        private void TextBox_FocusChanged(object? sender, EventArgs e)
        {
            // Regression: losing focus (Tab away, clicking elsewhere) used to do nothing at all - a highlighted-
            // but-not-yet-committed item (arrow-keyed but Enter never pressed) left textBox.Text showing stale
            // filter text instead of snapping to either the highlighted item or the last real selection, exactly
            // like standard combobox UX (and Enter) already does.
            if (!textBox.IsFocused)
                CommitHighlightOrRevert();
        }

        private void ApplyFilter(string filterText)
        {
            if (realItemsSource == null)
            {
                base.ItemsSource = null;
                HighlightedIndex = -1;
                RefreshPopupPosition();
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

            // The highlight indexes into the filtered subset that just went away - keeping it would both show a
            // highlight on the wrong row and, once the list narrows below it, throw from SelectedIndex on commit.
            HighlightedIndex = -1;

            // The popup was sized for the previous (possibly far longer) match list - resize it to the new one.
            RefreshPopupPosition();
        }

        private void RealSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplyFilter(textBox.Text);

        private void RevertText()
        {
            suppressTextChanged = true;
            SetTextFromSelection();
            suppressTextChanged = false;

            // Deliberately not ApplyFilter(textBox.Text): reverting restores the committed item's own text, which as
            // a filter would narrow the list to (essentially) that one item for the next time the popup opens.
            ApplyFilter(string.Empty);
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
