using MysticUI.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MysticUI.Controls
{
    public class PropertyGrid : SingleItemControlBase<ScrollViewer>
    {
        public const int GridColumnsAmount = 6;

        private Grid grid;
        private object? target;
        private bool isReadOnly;
        private List<PropertyEditorBase> editors;

        /// <summary>
        /// Gets or sets the target object to view and edit its properties.
        /// </summary>
        public object? Target
        {
            get => target;
            set
            {
                if (target == value)
                {
                    UpdateView();
                    return;
                }
                target = value;
                Rebuild();
                NotifyPropertyChanged(nameof(Target));
            }
        }

        /// <summary>
        /// Gets or sets the value that determines whether the <see cref="PropertyGrid"/> instance is editable.
        /// </summary>
        public virtual bool IsReadOnly
        {
            get => isReadOnly;
            set
            {
                if (isReadOnly == value) return;
                isReadOnly = value;
                Rebuild();
                NotifyPropertyChanged(nameof(IsReadOnly));
            }
        }

        /// <summary>
        /// Gets or sets the list of available editor creation rules.
        /// </summary>
        public List<IPropertyEditorRule> AvailableEditorRules { get; set; } = DefaultEditorRules;

        /// <summary>
        /// Gets or sets the value that determines whether to apply changes when getting the changes from any of editors.
        /// </summary>
        public bool ApplyChangesImmediately { get; set; }

        /// <summary>
        /// Gets the value to localize all properties of the grid element.
        /// The key will be added to the start of the property name.
        /// </summary>
        public string? LocalizationKey { get; set; }

        /// <summary>
        /// Provides the default set of the editor rules. Modify it to add your custom rules to all of the property editors.
        /// </summary>
        public static List<IPropertyEditorRule> DefaultEditorRules { get; set; } = new()
        {
            new EnumPropertyEditorRule(),
            new BoolPropertyEditorRule(),
            new CollectionEditorRule()
        };

        public PropertyGrid()
        {
            grid = CreateGrid();
            editors = new();
            ResetChild();
        }

        /// <summary>
        /// Updates the editor elements view according to the actual property values.
        /// </summary>
        public void UpdateView()
        {
            foreach (var editor in editors)
            {
                editor.UpdateView();
            }
        }

        protected internal override void ResetChildInternal()
        {
            Child = new()
            {
                Content = grid
            };
        }

        /// <summary>
        /// Clears and builds all editor elements according to the current object properties.
        /// </summary>
        protected void Rebuild()
        {
            grid.Clear();
            foreach (var editor in editors)
                editor.OnRemove();
            editors.Clear();
            if (Target == null) return;
            HashSet<object> targets = new();
            BuildGrid(Target, grid, targets);
            grid.RowDefinitions.Add(new() { Height = GridLength.OneStar });
            UpdateView();
            InvalidateMeasure();
        }

        protected virtual IEnumerable<PropertyInfo> ParseProperties(object value)
        {
            return from property in value.GetType().GetProperties()
                   where property.GetCustomAttribute<BrowsableAttribute>() != BrowsableAttribute.No &&
                         property.GetIndexParameters().Length == 0
                   select property;
        }

        protected virtual IEnumerable<IGrouping<string?, PropertyInfo>> GroupProperties(IEnumerable<PropertyInfo> properties)
        {
            return from property in properties
                   group property by property.GetCustomAttribute<CategoryAttribute>()?.Category into category
                   // Order categories to keep all elements without category below marked.
                   orderby category.Key == null ? 1 : 0 select category;
        }

        private void BuildGrid(object target, Grid parent, HashSet<object> paths)
        {
            paths.Add(target);
            if (LayoutSerializer.Default.IsParsable(target.GetType()))
            {
                TextBlock block = new TextBlock().WithDefaultStyle();
                block.GridColumnSpan = GridColumnsAmount;
                block.Text = "Building complex editor grid for the parsable objects is not available.";
                block.TextAlignment = FontStashSharp.RichText.TextHorizontalAlignment.Center;
                block.VerticalAlignment = VerticalAlignment.Top;
                parent.Add(block);
                // Don't serialize the parsable objects.
                return;
            }
            var categories = GroupProperties(ParseProperties(target));
            foreach (var category in categories)
            {
                Grid toAdd = parent;
                if (category.Key != null)
                {
                    DropdownMenu menu = new DropdownMenu().WithDefaultStyle();
                    menu.Text = category.Key;
                    toAdd = CreateGrid();
                    menu.Content = toAdd;
                }
                foreach (var property in category)
                {
                    var state = LayoutSerializer.Default.GetPropertyState(property, false);
                    if (state == LayoutSerializer.PropertyState.DontSerialize)
                        continue;
                    // Here we should select an editor for the property:
                    /* 
                     * 1. String edit field - edit parsable properties without any serialization problems
                     * 2. Extended string edit field - edit parsable properties with the embedded dialog-formed editor window
                     * that can be called by clicking the button after the text field
                     * 3. Embedded editor field - Any custom formed editor object that handles the property edit process
                     * 4. Complex type edit block - needed to edit any complex by-link accessible types. May include any editors inside.
                     */
                    var rule = AvailableEditorRules.FirstOrDefault(x => x.CanEditProperty(property));
                    if (rule != null)
                    {
                        PropertyEditorBase editor = rule.CreateEditor(this, property, toAdd);
                        editors.Add(editor);
                    }
                    else if (state == LayoutSerializer.PropertyState.Parsable)
                    {
                        var editor = new StringEditField(this, property, toAdd);
                        editors.Add(editor);
                    }
                    else
                    {
                        var value = property.GetValue(target);
                        if (value == null || paths.Contains(value))
                            continue;
                        DropdownMenu complexHeader = new DropdownMenu().WithDefaultStyle();
                        complexHeader.Text = EnvironmentSettings.LocalizationProvider?.Localize(property.Name, CultureInfo.CurrentUICulture) ?? property.Name;
                        Grid grid = CreateGrid();
                        BuildGrid(value, grid, paths);
                        complexHeader.Content = grid;
                        toAdd.Add(complexHeader);
                    }
                }
            }
        }

        internal static Grid CreateGrid()
        {
            Grid grid = new Grid().WithDefaultStyle();
            for (int i = 0; i < GridColumnsAmount; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.OneStar });
            return grid;
        }

    }
}
