// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Design.Syntax;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// Routes a <see cref="UI.Controls.PropertyGrid"/>'s edits of one markup element into its
    /// <see cref="DesignDocument"/>, so they're undoable and saved.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>Values are read from the live object, so styles, bindings and defaults show what's on screen.</description></item>
    /// <item><description>
    /// A typed edit formats the value with <see cref="MarkupValueFormatter"/> and sets the attribute. Quick successive
    /// edits of one property coalesce into one undo step. A value with no markup form is refused, and
    /// <see cref="LastError"/> says why.
    /// </description></item>
    /// <item><description>
    /// An attribute holding a markup extension (<c>{Binding …}</c>, <c>{StaticResource …}</c>) is shown and edited as text,
    /// so a typed editor never replaces it.
    /// </description></item>
    /// <item><description>Reset removes the attribute, letting the style or default value apply again.</description></item>
    /// </list>
    /// </remarks>
    public sealed class MarkupPropertyAdapter : PropertyGridValueAdapter
    {
        private readonly DesignDocument document;
        private readonly NodeId node;
        private readonly MarkupValueFormatter formatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupPropertyAdapter"/> class.
        /// </summary>
        /// <param name="document">The document the element belongs to.</param>
        /// <param name="node">The element.</param>
        /// <param name="formatter">Formats typed values as attribute text.</param>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="formatter"/> is <see langword="null"/>.</exception>
        public MarkupPropertyAdapter(DesignDocument document, NodeId node, MarkupValueFormatter formatter)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(formatter);
            this.document = document;
            this.node = node;
            this.formatter = formatter;
        }

        /// <summary>
        /// Occurs after every write with the new <see cref="LastError"/>, including refusals that don't change the
        /// document.
        /// </summary>
        internal event Action<string?>? ErrorChanged;

        /// <summary>
        /// Gets why the last write was refused, or <see langword="null"/> when it succeeded.
        /// </summary>
        public string? LastError { get; private set; }

        /// <inheritdoc/>
        public override bool TrySetValue(PropertyGridEntry entry, object target, object? value)
        {
            if (entry.IsReadOnly)
                return Refuse($"{entry.Name} is read-only.");
            if (!formatter.TryFormat(value, entry.PropertyType, out string? text))
                return Refuse($"{entry.Name}: this value has no markup form.");
            return Apply(document.Editor.SetAttribute(node, entry.Name, text));
        }

        /// <inheritdoc/>
        public override string? GetExpression(PropertyGridEntry entry, object target) =>
            Attribute(entry) is { } attribute && IsExpression(attribute.Value) ? attribute.Value : null;

        /// <inheritdoc/>
        public override bool TrySetExpression(PropertyGridEntry entry, object target, string text) =>
            Apply(document.Editor.SetAttribute(node, entry.Name, text));

        /// <inheritdoc/>
        public override bool CanReset(PropertyGridEntry entry, object target) => Attribute(entry) != null;

        /// <inheritdoc/>
        public override void Reset(PropertyGridEntry entry, object target) => Apply(document.Editor.ClearAttribute(node, entry.Name));

        private static bool IsExpression(string value) =>
            value.StartsWith('{') && !value.StartsWith("{}", StringComparison.Ordinal);

        private AttributeSyntax? Attribute(PropertyGridEntry entry) => document.GetNode(node)?.FindAttribute(entry.Name);

        private bool Apply(EditResult result)
        {
            SetError(result.Succeeded ? null : result.Error?.Message ?? "The edit was refused.");
            return result.Succeeded;
        }

        private bool Refuse(string reason)
        {
            SetError(reason);
            return false;
        }

        private void SetError(string? error)
        {
            LastError = error;
            ErrorChanged?.Invoke(error);
        }
    }
}
