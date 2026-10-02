// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI.Controls;

namespace Icy.UI.Styles
{
    /// <summary>
    /// Builds a visual tree from markup content for an arbitrary data object - the templating mechanism
    /// <see cref="Controls.ItemsControl"/> uses to turn each bound item into a realized element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Like <see cref="ControlTemplate"/>, content is captured once when the <see cref="DataTemplate"/> itself is
    /// loaded (see <see cref="Icy.Markup.MarkupLoader"/>'s own <see cref="DataTemplate"/> special case) and
    /// instantiated fresh - via <see cref="Build(object)"/> - every time it's needed. Two calls to
    /// <see cref="Build(object)"/>, even for the same data item, each get their own independent visual subtree.
    /// </para>
    /// <para>
    /// Unlike <see cref="ControlTemplate"/>, this has no <c>TargetType</c> (it works against any data object's
    /// runtime type) and its content can't use <c>{TemplateBinding}</c> (there's no templated control to bind back
    /// to) - instead, <see cref="Build(object)"/> sets the built tree's root <see cref="UIElement.DataContext"/> to
    /// the data item, and ordinary <c>{Binding}</c> inside the template resolves against that.
    /// </para>
    /// </remarks>
    public class DataTemplate
    {
        private readonly Func<UIElement>? factory;
        private XElement? content;
        private IcyConfiguration? configuration;
        private string? sourcePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataTemplate"/> class with no content. Content is supplied by
        /// <see cref="Icy.Markup.MarkupLoader"/> when the template is loaded from markup.
        /// </summary>
        public DataTemplate()
        {
        }

        private DataTemplate(Func<UIElement> factory)
        {
            this.factory = factory;
        }

        /// <summary>
        /// Gets the template <see cref="Controls.ItemsControl"/> falls back to for items when neither
        /// <see cref="Controls.ItemsControl.ItemTemplateSelector"/> nor <see cref="Controls.ItemsControl.ItemTemplate"/>
        /// resolves one: a <see cref="TextBlock"/> showing the item's <see cref="object.ToString"/>.
        /// </summary>
        /// <remarks>
        /// The text follows <see cref="UIElement.DataContext"/> through <see cref="UIElement.DataContextChanged"/>, so a
        /// pooled container reused for another item shows that item's text rather than the previous one's.
        /// </remarks>
        internal static DataTemplate Default { get; } = new(CreateDefaultContent);

        /// <summary>
        /// Builds a fresh visual tree from this template's content, for <paramref name="dataItem"/>.
        /// </summary>
        /// <param name="dataItem">
        /// The data object the built tree's root <see cref="UIElement.DataContext"/> is set to - what every
        /// <c>{Binding}</c> inside the template resolves against.
        /// </param>
        /// <returns>The freshly built root element - never shared with any other call, even for the same data item.</returns>
        /// <exception cref="InvalidOperationException">This template was never given any content.</exception>
        /// <exception cref="Icy.Markup.MarkupException">The content is malformed, or breaks a rule of the markup language.</exception>
        public UIElement Build(object dataItem)
        {
            ArgumentNullException.ThrowIfNull(dataItem);
            if (factory != null)
            {
                UIElement built = factory();
                built.DataContext = dataItem;
                return built;
            }

            if (content == null || configuration == null)
                throw new InvalidOperationException($"This '{nameof(DataTemplate)}' has no content to build - it was never loaded from markup.");

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.LoadDataTemplateContent(content, sourcePath);
            root.DataContext = dataItem;
            return root;
        }

        /// <summary>
        /// Captures this template's content, ready for later per-item instantiation via <see cref="Build(object)"/>.
        /// </summary>
        /// <param name="content">The template's single root element.</param>
        /// <param name="configuration">The configuration to build content through.</param>
        /// <param name="sourcePath">The template's source document path, for error messages.</param>
        /// <remarks>
        /// Called once, by <see cref="Icy.Markup.MarkupLoader"/>'s own <see cref="DataTemplate"/> special case,
        /// immediately after this instance is constructed - never by ordinary application code.
        /// </remarks>
        internal void SetContent(XElement content, IcyConfiguration configuration, string? sourcePath)
        {
            this.content = content;
            this.configuration = configuration;
            this.sourcePath = sourcePath;
        }

        private static UIElement CreateDefaultContent()
        {
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            text.DataContextChanged += (_, _) => text.Text = text.DataContext?.ToString() ?? string.Empty;
            return text;
        }
    }
}
