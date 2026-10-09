// sources/IcyUI/Data/PropertyGridValueAdapter.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

namespace Icy.Data
{
    /// <summary>
    /// Decides how a <see cref="UI.Controls.PropertyGrid"/> reads and writes the properties it shows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default adapter (<see cref="Default"/>) reads and writes the target's properties directly. Derive from this
    /// class to route edits elsewhere, for example into an undoable document, or to show some properties as raw text
    /// expressions instead of typed editors.
    /// </para>
    /// <para>
    /// Every member is virtual and has a working default, so an adapter overrides only what it changes.
    /// </para>
    /// <para>
    /// The default adapter offers Reset for a property that declares a
    /// <see cref="System.ComponentModel.DefaultValueAttribute"/> and currently holds another value.
    /// </para>
    /// </remarks>
    public class PropertyGridValueAdapter
    {
        /// <summary>
        /// Gets the adapter that reads and writes properties directly through reflection.
        /// </summary>
        public static PropertyGridValueAdapter Default { get; } = new();

        /// <summary>
        /// Reads the value <paramref name="entry"/>'s editor shows.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <returns>The value; the default reads it with <see cref="PropertyGridEntry.GetValue(object)"/>.</returns>
        public virtual object? GetValue(PropertyGridEntry entry, object target) => entry.GetValue(target);

        /// <summary>
        /// Writes a value the user entered in <paramref name="entry"/>'s typed editor.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <param name="value">The new value, already converted to <see cref="PropertyGridEntry.PropertyType"/>.</param>
        /// <returns><see langword="true"/> when the value was accepted.</returns>
        public virtual bool TrySetValue(PropertyGridEntry entry, object target, object? value) => entry.TrySetValue(target, value);

        /// <summary>
        /// Gets the raw text to show instead of a typed editor, such as a <c>{Binding}</c> expression.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <returns>
        /// The text, or <see langword="null"/> (the default) for a typed editor. A non-<see langword="null"/> text turns
        /// the row into a text box that commits through <see cref="TrySetExpression"/> on Enter or when it loses focus.
        /// </returns>
        public virtual string? GetExpression(PropertyGridEntry entry, object target) => null;

        /// <summary>
        /// Writes the text the user entered in an expression row.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <param name="text">The entered text.</param>
        /// <returns>
        /// <see langword="true"/> when the text was accepted; on <see langword="false"/> (the default) the row is marked
        /// <see cref="UI.Styles.ControlState.Invalid"/>.
        /// </returns>
        public virtual bool TrySetExpression(PropertyGridEntry entry, object target, string text) => false;

        /// <summary>
        /// Determines whether the row shows a Reset button.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <returns>
        /// The default: <see langword="true"/> when the property declares a
        /// <see cref="System.ComponentModel.DefaultValueAttribute"/>, is writable, and currently holds another value.
        /// </returns>
        public virtual bool CanReset(PropertyGridEntry entry, object target)
        {
            if (!entry.HasDefaultValue || entry.IsReadOnly)
                return false;

            try
            {
                return !Equals(GetValue(entry, target), entry.DefaultValue);
            }
            catch (Exception)
            {
                // A getter that throws can't be compared; offering Reset would act on a value nobody can see.
                return false;
            }
        }

        /// <summary>
        /// Resets the property when the user presses the row's Reset button.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <remarks>The default writes <see cref="PropertyGridEntry.DefaultValue"/> through <see cref="TrySetValue"/>.</remarks>
        public virtual void Reset(PropertyGridEntry entry, object target)
        {
            if (entry.HasDefaultValue)
                TrySetValue(entry, target, entry.DefaultValue);
        }
    }
}
