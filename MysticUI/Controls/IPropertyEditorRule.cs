using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MysticUI.Controls
{
    public interface IPropertyEditorRule
    {
        public PropertyEditorBase CreateEditor(PropertyGrid grid, PropertyInfo property, Grid contentGrid);

        public bool CanEditProperty(PropertyInfo property);
    }

    internal abstract class GenericPropertyEditorRule<T> : IPropertyEditorRule
    {
        public abstract PropertyEditorBase CreateEditor(PropertyGrid grid, PropertyInfo property, Grid contentGrid);

        public virtual bool CanEditProperty(PropertyInfo property) => property.PropertyType.IsAssignableTo(typeof(T));
    }

    internal class BoolPropertyEditorRule : GenericPropertyEditorRule<bool>
    {
        public override PropertyEditorBase CreateEditor(PropertyGrid grid, PropertyInfo property, Grid contentGrid) => new BoolEditField(grid, property, contentGrid);
    }

    internal class EnumPropertyEditorRule : IPropertyEditorRule
    {
        public bool CanEditProperty(PropertyInfo property) => property.PropertyType.IsEnum;

        public PropertyEditorBase CreateEditor(PropertyGrid grid, PropertyInfo property, Grid contentGrid) => new EnumEditField(grid, property, contentGrid);
    }

    internal class CollectionEditorRule : GenericPropertyEditorRule<ICollection>
    {
        public override PropertyEditorBase CreateEditor(PropertyGrid grid, PropertyInfo property, Grid contentGrid) => new CollectionEditField(grid, property, contentGrid);
    }
}
