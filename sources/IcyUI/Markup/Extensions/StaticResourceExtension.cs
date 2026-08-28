// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Markup.Extensions
{
    /// <summary>
    /// <c>{StaticResource Key}</c>: resolves a named resource by walking outward from the innermost element
    /// currently under construction (see <see cref="MarkupExtensionContext.ElementStack"/>) to the document root,
    /// checking each element's own <see cref="UIElement.Resources"/> in turn.
    /// </summary>
    /// <remarks>
    /// Resolved once, at load time - not re-evaluated if the resource dictionary changes afterward (there is no
    /// mechanism for that yet; this matches how <see cref="ContentPropertyAttribute"/> content and every other
    /// non-<c>{Binding}</c> value in markup is a one-time assignment).
    /// </remarks>
    [MarkupExtensionDefaultProperty(nameof(Key))]
    public sealed class StaticResourceExtension : IMarkupExtension
    {
        /// <summary>
        /// Gets or sets the key to look up.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <inheritdoc/>
        /// <exception cref="MarkupException">
        /// No ancestor's <see cref="UIElement.Resources"/> (own entries or merged dictionaries) has an entry for
        /// <see cref="Key"/>.
        /// </exception>
        public object? ProvideValue(MarkupExtensionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            for (int i = context.ElementStack.Count - 1; i >= 0; i--)
            {
                UIElement element = context.ElementStack[i];
                if (element.HasResources && element.Resources.TryGetValue(Key, out object? value))
                    return value;
            }

            throw MarkupException.At($"No resource named '{Key}' was found.", context.Node, context.SourcePath);
        }
    }
}
