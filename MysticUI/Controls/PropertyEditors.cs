using MysticUI.Extensions;
using SharpDX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a base class for the properties edit.
    /// </summary>
    public abstract class PropertyEditorBase
    {
        /// <summary>
        /// Gets the object to which the editor is set.
        /// </summary>
        public object Owner { get; }

        /// <summary>
        /// Gets the target property of the object.
        /// </summary>
        public PropertyInfo TargetProperty { get; }

        /// <summary>
        /// Gets or sets the setting that determines whether to enable XML bindings for the editors that support them.
        /// </summary>
        public virtual bool EnableBindings { get; set; }

        /// <summary>
        /// Gets the grid in which the element is created.
        /// </summary>
        /// <remarks>
        /// According to the rules of the grid, it contains 6 columns with the same one-star size. They're needed to align the editors inside.
        /// </remarks>
        public Grid ContentGrid { get; }

        /// <summary>
        /// Gets the starting row of the grid for the current property.
        /// </summary>
        public int StartingRow { get; }

        /// <summary>
        /// Gets the parent grid of the editor.
        /// </summary>
        public PropertyGrid ParentGrid { get; }

        /// <summary>
        /// Occurs when the editor tries to update the target property value.
        /// </summary>
        public event EventHandler<object?>? ValueUpdated;

        /// <summary>
        /// Creates a new instance of the <see cref="PropertyEditorBase"/> class.
        /// </summary>
        /// <param name="grid">The grid that owes the editor.</param>
        /// <param name="targetProperty">A property to target for.</param>
        /// <param name="contentGrid">A grid to add the content to.</param>
        public PropertyEditorBase(PropertyGrid grid, PropertyInfo targetProperty, Grid contentGrid)
        {
            TargetProperty = targetProperty;
            Owner = grid.Target!;
            ParentGrid = grid;
            ContentGrid = contentGrid;
            StartingRow = contentGrid.RowDefinitions.Count;
            if (Owner is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged += OnPropertyUpdated;
            }
        }

        /// <summary>
        /// Handles the update of the property editor value.
        /// </summary>
        /// <param name="result">The new result of the editor.</param>
        protected void OnValueUpdated(object? result) =>
            ValueUpdated?.Invoke(this, result);

        /// <summary>
        /// Handles the update of the property value and updates the presentation according to new property value.
        /// </summary>
        /// <param name="sender">A target object that updated its property.</param>
        /// <param name="e">An event arguments with the property values.</param>
        public virtual void OnPropertyUpdated(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == TargetProperty.Name)
            {
                UpdateView();
            }
        }

        public virtual void OnRemove()
        {
            if (Owner is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged -= OnPropertyUpdated;
            }
        }

        /// <summary>
        /// Updates the data in the editor according to the new values.
        /// </summary>
        /// <remarks>
        /// Note that this method shouldn't raise the <see cref="ValueUpdated"/> event.
        /// </remarks>
        internal protected abstract void UpdateView();

        /// <summary>
        /// Gets the localized key with the <see cref="PropertyGrid"/> instance prefix.
        /// </summary>
        /// <param name="key">The key to localize.</param>
        /// <returns>The localized value or the original value if the localization is not available.</returns>
        protected string GetLocalization(string key) =>
            EnvironmentSettingsProvider.EnvironmentSettings.LocalizationProvider?.Localize(ParentGrid.LocalizationKey + key) ?? key;

        /// <summary>
        /// Gets the value of the property using the editor reading rules.
        /// </summary>
        /// <param name="target">The target object to get the property value.</param>
        /// <param name="property">The property info to get value of.</param>
        /// <returns>The value of the property or the value of the <see cref="AmbientValueAttribute"/> if this attribute exists on the property.</returns>
        public static object? GetValue(object? target, PropertyInfo property)
        {
            var attribute = property.GetCustomAttribute<AmbientValueAttribute>();
            return attribute?.Value ?? property.GetValue(target);
        }
    }

    // TODO: implement bindings handling.
    public abstract class SimpleTypeEditField<TEditor> : PropertyEditorBase where TEditor : Control
    {
        protected internal readonly TextBlock propertyLabel;
        protected internal readonly TEditor editControl;

        public SimpleTypeEditField(PropertyGrid grid, PropertyInfo targetProperty, Grid contentGrid) : base(grid, targetProperty, contentGrid)
        {
            propertyLabel = new TextBlock().WithDefaultStyle();
            propertyLabel.Text = GetLocalization(targetProperty.Name);
            editControl = CreateEditControl().WithDefaultStyle();
            editControl.GridColumn = PropertyGrid.GridColumnsAmount / 2;
            propertyLabel.GridColumnSpan = editControl.GridColumnSpan = PropertyGrid.GridColumnsAmount / 2;
            GenerateRow();
            UpdateView();
        }

        protected internal override void UpdateView()
        {
            UpdateDisplayValue(GetValue(Owner, TargetProperty));
        }

        protected internal virtual void GenerateRow()
        {
            ContentGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            propertyLabel.GridRow = editControl.GridRow = StartingRow;
            //propertyLabel.VerticalAlignment = editControl.VerticalAlignment = VerticalAlignment.Top;
            ContentGrid.Add(propertyLabel);
            ContentGrid.Add(editControl);
        }

        protected abstract void UpdateDisplayValue(object? value);

        protected abstract TEditor CreateEditControl();
    }

    public class StringEditField : SimpleTypeEditField<TextBox>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="StringEditField"/> class.
        /// </summary>
        public StringEditField(PropertyGrid grid, PropertyInfo targetProperty, Grid contentGrid) : base(grid, targetProperty, contentGrid)
        {
        }

        protected override TextBox CreateEditControl()
        {
            TextBox result = new();
            result.FocusChanged += (s, e) =>
            {
                if (!result.HasFocus)
                {
                    var value = LayoutSerializer.Default.ParseProperty(result.Text, TargetProperty.PropertyType);
                    OnValueUpdated(value);
                }
            };
            result.IsReadOnly = ParentGrid.IsReadOnly;
            return result;
        }

        protected override void UpdateDisplayValue(object? value)
        {
            editControl.Text = value?.ToString() ?? string.Empty;
        }
    }

    public class BoolEditField : SimpleTypeEditField<CheckBox>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="BoolEditField"/> class.
        /// </summary>
        public BoolEditField(PropertyGrid grid, PropertyInfo targetProperty, Grid contentGrid) : base(grid, targetProperty, contentGrid)
        {
        }

        protected override CheckBox CreateEditControl()
        {
            CheckBox result = new();
            result.CheckedChanged += (s, e) =>
            {
                OnValueUpdated(result.IsChecked);
            };
            return result;
        }

        protected override void UpdateDisplayValue(object? value)
        {
            editControl.IsChecked = value == null ? null : Convert.ToBoolean(value);
        }
    }

    public class EnumEditField : SimpleTypeEditField<Selector>
    {
        private string[] availableValues;

        /// <summary>
        /// Creates a new instance of the <see cref="EnumEditField"/> class.
        /// </summary>
        public EnumEditField(PropertyGrid grid, PropertyInfo targetProperty, Grid contentGrid) : base(grid, targetProperty, contentGrid)
        {
        }

        protected override Selector CreateEditControl()
        {
            Selector result = new();
            availableValues = Enum.GetNames(TargetProperty.PropertyType);
            foreach (var value in availableValues)
            {
                result.Add(new ListBoxItem()
                {
                    Content = value
                }.WithDefaultStyle());
            }
            result.SelectedIndexChanged += (s, e) =>
            {
                object? value = LayoutSerializer.Default.ParseProperty(((ContentControl)result.SelectedItem).Content!.ToString()!, TargetProperty.PropertyType);
                OnValueUpdated(value);
            };
            return result;
        }

        protected override void UpdateDisplayValue(object? value)
        {
            editControl.SelectedIndex = Array.IndexOf(availableValues, value);
        }
    }

    public class CollectionEditField : SimpleTypeEditField<Button>
    {
        public CollectionEditField(PropertyGrid grid, PropertyInfo targetProperty, Grid contentGrid) : base(grid, targetProperty, contentGrid)
        {
        }

        protected override Button CreateEditControl()
        {
            return new Button()
            {
                Content = "Edit..."
            };
        }

        protected override void UpdateDisplayValue(object? value)
        {
            // Actually do nothing.
        }

        protected internal override void UpdateView()
        {
            // Do nothing too. It just don't need to update.
        }
    }
}
