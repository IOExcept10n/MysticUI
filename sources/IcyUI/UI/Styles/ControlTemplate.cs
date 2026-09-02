// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.Configuration;
using Icy.Markup;

namespace Icy.UI.Styles
{
    /// <summary>
    /// Replaces a <see cref="Icy.UI.Controls.Control"/>'s entire decorated visual tree (its internal <c>Chrome</c>)
    /// with markup content of the author's own choosing, instead of the control's built-in default.
    /// </summary>
    /// <param name="targetType">The type of control this template is meant to be applied to.</param>
    /// <remarks>
    /// <para>
    /// A <see cref="ControlTemplate"/> holds a single element's worth of raw markup, captured once when the
    /// template itself is loaded (see <see cref="Icy.Markup.MarkupLoader"/>'s own <see cref="ControlTemplate"/>
    /// special case), and instantiated fresh - via <see cref="LoadContent(UIElement)"/> - for every control that
    /// applies it. Two controls sharing one <see cref="ControlTemplate"/> each get their own independent visual
    /// subtree; nothing built from this template is ever shared or cached across control instances.
    /// </para>
    /// <para>
    /// Content elements can pull values from the control being templated via <c>{TemplateBinding}</c> (see
    /// <see cref="Icy.Markup.Extensions.TemplateBindingExtension"/>), and a
    /// <see cref="Icy.UI.Controls.ContentControl"/>'s template typically hosts the control's real
    /// <see cref="Icy.UI.Controls.ContentControl.Content"/> through a
    /// <see cref="Icy.UI.Controls.ContentPresenter"/>.
    /// </para>
    /// <para>
    /// <see cref="Style"/>/<see cref="VisualState"/> setters are unaffected by templating - they still only ever
    /// target the templated control's own top-level properties (e.g. <c>Background</c>), which a
    /// <c>{TemplateBinding}</c> inside this template's content then reflects.
    /// </para>
    /// </remarks>
    public class ControlTemplate(Type targetType)
    {
        private XElement? content;
        private IcyConfiguration? configuration;
        private string? sourcePath;

        /// <summary>
        /// Gets the type of control this template is meant to be applied to.
        /// </summary>
        public Type TargetType { get; } = targetType;

        /// <summary>
        /// Builds a fresh visual tree from this template's content, for <paramref name="templatedControl"/>.
        /// </summary>
        /// <param name="templatedControl">The control this content is being built for.</param>
        /// <returns>The freshly built root element - never shared with any other call, even for the same control.</returns>
        /// <exception cref="InvalidOperationException">This template was never given any content.</exception>
        /// <exception cref="Icy.Markup.MarkupException">The content is malformed, or breaks a rule of the markup language.</exception>
        public UIElement LoadContent(UIElement templatedControl)
        {
            ArgumentNullException.ThrowIfNull(templatedControl);
            if (content == null || configuration == null)
                throw new InvalidOperationException($"This '{nameof(ControlTemplate)}' has no content to build - it was never loaded from markup.");

            var loader = new MarkupLoader(configuration);
            return loader.LoadTemplateContent(content, templatedControl, sourcePath);
        }

        /// <summary>
        /// Captures this template's content, ready for later per-control instantiation via
        /// <see cref="LoadContent(UIElement)"/>.
        /// </summary>
        /// <param name="content">The template's single root element.</param>
        /// <param name="configuration">The configuration to build content through.</param>
        /// <param name="sourcePath">The template's source document path, for error messages.</param>
        /// <remarks>
        /// Called once, by <see cref="Icy.Markup.MarkupLoader"/>'s own <see cref="ControlTemplate"/> special case,
        /// immediately after this instance is constructed - never by ordinary application code.
        /// </remarks>
        internal void SetContent(XElement content, IcyConfiguration configuration, string? sourcePath)
        {
            this.content = content;
            this.configuration = configuration;
            this.sourcePath = sourcePath;
        }
    }
}
