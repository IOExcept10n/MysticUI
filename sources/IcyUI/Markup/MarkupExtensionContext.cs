// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml;
using Icy.Configuration;
using Icy.UI;

namespace Icy.Markup
{
    /// <summary>
    /// What a <see cref="IMarkupExtension"/> is given to resolve its value: the object and property it is being
    /// resolved for, and what the loader knows about the surrounding document.
    /// </summary>
    /// <param name="target">The object whose property the extension is resolving a value for.</param>
    /// <param name="member">The property the extension is resolving a value for.</param>
    /// <param name="configuration">The configuration the loader is resolving the document through.</param>
    /// <param name="names">The document's <c>x:Name</c> scope.</param>
    /// <param name="elementStack">The elements currently under construction, innermost last.</param>
    /// <param name="node">The extension's position in the source document, for error reporting.</param>
    /// <param name="sourcePath">The document's path, for error reporting.</param>
    /// <param name="templatedControl">
    /// The control a <see cref="Icy.UI.Styles.ControlTemplate"/>'s content is being built for, when this context
    /// belongs to template content - <see langword="null"/> for an ordinary document.
    /// </param>
    public sealed class MarkupExtensionContext(
        object target,
        MarkupMember member,
        IcyConfiguration configuration,
        MarkupNameScope names,
        IReadOnlyList<UIElement> elementStack,
        IXmlLineInfo? node,
        string? sourcePath,
        UIElement? templatedControl = null)
    {
        /// <summary>
        /// Gets the object whose property the extension is resolving a value for.
        /// </summary>
        public object Target { get; } = target;

        /// <summary>
        /// Gets the property the extension is resolving a value for.
        /// </summary>
        public MarkupMember Member { get; } = member;

        /// <summary>
        /// Gets the configuration the loader is resolving the document through.
        /// </summary>
        public IcyConfiguration Configuration { get; } = configuration;

        /// <summary>
        /// Gets the document's <c>x:Name</c> scope, for extensions that resolve an element by name.
        /// </summary>
        public MarkupNameScope Names { get; } = names;

        /// <summary>
        /// Gets the <see cref="UIElement"/>s currently under construction, innermost last - see
        /// <see cref="Markup.MarkupLoader"/>'s private <c>MarkupLoadContext.ElementStack</c> for why this can't
        /// simply be <see cref="UIElement.Parent"/>.
        /// </summary>
        public IReadOnlyList<UIElement> ElementStack { get; } = elementStack;

        /// <summary>
        /// Gets the extension's position in the source document, for error reporting.
        /// </summary>
        public IXmlLineInfo? Node { get; } = node;

        /// <summary>
        /// Gets the document's path, for error reporting.
        /// </summary>
        public string? SourcePath { get; } = sourcePath;

        /// <summary>
        /// Gets the control a <see cref="Icy.UI.Styles.ControlTemplate"/>'s content is being built for, or
        /// <see langword="null"/> when this context belongs to an ordinary (non-template) document.
        /// </summary>
        /// <remarks>
        /// This is what <c>{TemplateBinding}</c> (see <see cref="Icy.Markup.Extensions.TemplateBindingExtension"/>)
        /// reads as its binding source - it has no other way to reach "the control this template decorates," since
        /// that control is never itself part of <see cref="ElementStack"/> (it isn't being constructed by the same
        /// document load).
        /// </remarks>
        public UIElement? TemplatedControl { get; } = templatedControl;
    }
}
