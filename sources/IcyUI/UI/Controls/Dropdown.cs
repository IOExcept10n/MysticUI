// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input.Devices;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="Selector"/> with a fixed-text closed-state display (<see cref="Selector.SelectedItem"/>'s
    /// display text) and letter-key typeahead - no free text entry.
    /// </summary>
    /// <remarks>
    /// Typeahead is driven by the raw <see cref="IKeyboardInput.KeyDown"/> event, never
    /// <see cref="Icy.Input.Events.ITextEvents"/>/<c>EnableTextInput</c> - a <see cref="Dropdown"/> never accepts
    /// free text entry, so it must never put the input system into "text input" mode. On several platforms that
    /// mode is what triggers an IME candidate window or an on-screen keyboard; enabling it just because a
    /// <see cref="Dropdown"/> gained focus would surprise a user (e.g. someone typing in Japanese) with an IME
    /// popup over a control that has nowhere to actually show composed text. <see cref="ComboBox"/>'s internal
    /// text box is the control that legitimately needs real text input and enables it accordingly.
    /// </remarks>
    public class Dropdown : Selector
    {
        private IKeyboardInput? subscribedKeyboard;

        /// <summary>
        /// Initializes a new instance of the <see cref="Dropdown"/> class.
        /// </summary>
        public Dropdown()
        {
            IsFocusable = true;
            SelectionChanged += (_, _) => UpdateToggleContent();
            FocusChanged += Dropdown_FocusChanged;
            UpdateToggleContent();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <see cref="Selector.OnApplyTemplate"/> has just repointed <see cref="Selector.Toggle"/> at whichever
        /// <see cref="ToggleButton"/> is now live - a freshly loaded <c>PART_ToggleButton</c> carries no content, so
        /// the closed-state display text has to be pushed onto it again here.
        /// </remarks>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            UpdateToggleContent();
        }

        /// <summary>
        /// Resolves a raw key press to the letter it represents, for typeahead matching - only the top-row
        /// alphabetic keys are considered, matching this control's "letter-key typeahead" scope. Deliberately not
        /// a full keyboard-layout/shift-state resolution (that's exactly what <see cref="Icy.Input.Events.ITextEvents"/>
        /// exists for, and is exactly the machinery this class avoids enabling - see the class remarks).
        /// </summary>
        /// <param name="key">The raw key that was pressed.</param>
        /// <returns>The uppercase letter it represents, or <see langword="null"/> if it isn't a letter key.</returns>
        private static char? KeyToLetter(Keys key) =>
            key >= Keys.A && key <= Keys.Z ? (char)('A' + (key - Keys.A)) : null;

        private void Dropdown_FocusChanged(object? sender, EventArgs e)
        {
            if (IsFocused)
            {
                if (Configuration?.Input.Keyboard is { } keyboard)
                {
                    subscribedKeyboard = keyboard;
                    subscribedKeyboard.KeyDown += OnKeyDown;
                }
            }
            else if (subscribedKeyboard != null)
            {
                subscribedKeyboard.KeyDown -= OnKeyDown;
                subscribedKeyboard = null;
            }
        }

        private void OnKeyDown(object? sender, GenericEventArgs<Keys> e)
        {
            if (ItemCount == 0 || KeyToLetter(e.Data) is not { } letter)
                return;

            string search = letter.ToString();

            int start = (SelectedIndex + 1) % ItemCount;
            for (int offset = 0; offset < ItemCount; offset++)
            {
                int index = (start + offset) % ItemCount;
                if (GetDisplayText(GetItemAt(index)).StartsWith(search, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedIndex = index;
                    return;
                }
            }
        }

        /// <summary>
        /// Pushes <see cref="Selector.SelectedItem"/>'s display text onto <see cref="Selector.Toggle"/>. Falls back
        /// to a single space rather than an empty string when nothing is selected - <see cref="TextBlock"/>
        /// measures as <see cref="System.Drawing.Size.Empty"/> for a truly empty <see cref="TextBlock.Text"/> (by
        /// design, so an optional/conditional label doesn't reserve space when it has nothing to show), which for
        /// this toggle would mean the whole <see cref="Dropdown"/> visibly changed height the moment a selection
        /// was first made. A space keeps <see cref="TextBlock"/> measuring a real (font-metrics-based) line height
        /// throughout, matching how <see cref="TextBox"/> already avoids the same trap for its own empty text.
        /// </summary>
        private void UpdateToggleContent() =>
            Toggle.Content = new TextBlock { Text = SelectedItem == null ? " " : GetDisplayText(SelectedItem) };
    }
}
