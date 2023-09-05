using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a toggle checkbox-like button which can be selected but can't be unselected.
    /// </summary>
    public class RadioButton : ToggleButton
    {
        internal const string CheckmarkStyleName = "RadioButtonCheckmarkStyle";

        private static readonly RadioButtonGroupManager groupManager = new();
        private readonly Checkmark checkmark;
        private RadioButtonGroup? group;

        /// <summary>
        /// Content of the <see cref="RadioButton"/>
        /// </summary>
        public override object? Content
        {
            get => base.Content;
            set
            {
                base.Content = value;
                SetCheckBoxLikeChild();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="RadioButton"/> is selected.
        /// </summary>
        /// <value>
        /// <see langword="true"/> when the <see cref="RadioButton"/> is selected, <see langword="false"/> otherwise.
        /// </value>
        /// <exception cref="ArgumentNullException">Value can't be set to null, it's not supported.</exception>
        [DefaultValue(false)]
        [NotNull, Stride.Core.Annotations.NotNull]
        public override bool? IsChecked
        {
            get => base.IsChecked == true;
            set
            {
                if (base.IsChecked == true && !GetGroup().Any(x => x.IsChecked == true && x != this))
                    return;

                base.IsChecked = value ?? throw new ArgumentNullException(nameof(value), "RadioButton's IsChecked property can not be set to null.");
            }
        }

        /// <summary>
        /// Name of <see cref="RadioButton"/> group. User can select only a single button from
        /// list of buttons presented in the same group or attached to the same parent.
        /// </summary>
        [Category("Behavior")]
        public string? GroupName
        {
            get => group?.Name;
            set
            {
                group?.Buttons?.Remove(this);
                group = null;
                if (value != null)
                {
                    group = groupManager.GetButtonGroup(value);
                    group.Buttons.Add(this);
                }
            }
        }

        /// <summary>
        /// Creates new instance of the <see cref="RadioButton"/> class.
        /// </summary>
        public RadioButton()
        {
            checkmark = new();
            checkmark.SetStyle(CheckmarkStyleName);
            IsChecked = false;
            ContentTextWrapping = true;
        }

        /// <inheritdoc/>
        protected internal override void OnCheckedChanged()
        {
            base.OnCheckedChanged();
            if (IsChecked == true)
            {
                foreach (var button in GetGroup())
                {
                    if (button != this)
                        button.IsChecked = false;
                }
            }
            checkmark.IsChecked = IsChecked;
        }

        private IEnumerable<RadioButton> GetGroup()
        {
            if (group != null)
                return group.Buttons;
            else if (Parent is IContainerControl container)
                return container.Children.OfType<RadioButton>();
            else return Enumerable.Empty<RadioButton>();
        }

        private void SetCheckBoxLikeChild()
        {
            Grid panel = new();
            panel.Children.Add(checkmark);
            if (Child != null)
            {
                panel.Children.Add(Child);
                Child.GridColumn = 1;
            }
            Child = panel;
            Child.Parent = this;
            Child.Desktop = Desktop;
        }

        private class RadioButtonGroup
        {
            public string Name { get; set; }

            public List<RadioButton> Buttons { get; set; }

            public RadioButtonGroup(string name)
            {
                Name = name;
                Buttons = new List<RadioButton>();
            }
        }

        private class RadioButtonGroupManager
        {
            private readonly Dictionary<string, RadioButtonGroup> groups = new();

            public RadioButtonGroup GetButtonGroup(string name)
            {
                if (groups.TryGetValue(name, out RadioButtonGroup? group))
                {
                    return group;
                }
                else
                {
                    var result = new RadioButtonGroup(name);
                    groups.Add(name, result);
                    return result;
                }
            }
        }
    }
}