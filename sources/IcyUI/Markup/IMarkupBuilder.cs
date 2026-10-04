// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.UI;

namespace Icy.Markup
{
    /// <summary>
    /// Builds objects from markup fragments, or applies single attributes, exactly as <see cref="MarkupLoader"/>
    /// would while loading a whole document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the seam design tooling uses to mirror a markup edit onto an already loaded tree, reusing the loader's
    /// type resolution, value conversion, markup extensions and name scopes instead of duplicating them.
    /// <see cref="MarkupLoader"/> implements it: <c>IMarkupBuilder builder = new MarkupLoader(configuration);</c>.
    /// </para>
    /// <para>
    /// Everything built through a <see cref="MarkupLoadScope"/> is reported to that scope's observer with
    /// <see cref="IMarkupLoadObserver.ObjectCreated"/> and <see cref="IMarkupLoadObserver.MemberApplied"/>, as during
    /// the original load.
    /// </para>
    /// </remarks>
    public interface IMarkupBuilder
    {
        /// <summary>
        /// Parses a markup fragment with the same reader settings the loader uses for documents.
        /// </summary>
        /// <param name="text">The fragment: exactly one element, with any content.</param>
        /// <param name="namespaces">
        /// The namespace declarations the fragment inherits from where it sits in its document, keyed by prefix (the
        /// empty string for the default namespace). The <c>x</c> prefix is always predeclared.
        /// </param>
        /// <returns>The fragment's element, with line information relative to <paramref name="text"/>.</returns>
        /// <exception cref="MarkupException">The fragment isn't well-formed XML, or uses an undeclared prefix.</exception>
        XElement ParseFragment(string text, IReadOnlyDictionary<string, string> namespaces);

        /// <summary>
        /// Builds a fragment's object tree in the context of an observed document.
        /// </summary>
        /// <param name="scope">The scope of the document the fragment belongs to.</param>
        /// <param name="fragment">The fragment, as returned by <see cref="ParseFragment"/>.</param>
        /// <param name="liveParent">
        /// The live element the result will be added to, or <see langword="null"/>. <c>{StaticResource}</c>
        /// resolves through it and its ancestors, as it would through the elements under construction during a load.
        /// The result isn't added to it; that's the caller's job.
        /// </param>
        /// <returns>The built object. <c>x:Name</c>s inside it are registered in the scope's name scope.</returns>
        /// <exception cref="MarkupException">The fragment breaks a rule of the language.</exception>
        /// <exception cref="InvalidOperationException">The scope's tree has already been collected.</exception>
        object BuildFragment(MarkupLoadScope scope, XElement fragment, UIElement? liveParent);

        /// <summary>
        /// Applies one attribute to an already built object, exactly as the loader would.
        /// </summary>
        /// <param name="scope">The scope of the document <paramref name="target"/> belongs to.</param>
        /// <param name="target">The object to apply the attribute to.</param>
        /// <param name="attribute">
        /// The attribute. It must belong to an element (one from <see cref="ParseFragment"/>), so its namespace and
        /// any directive resolve.
        /// </param>
        /// <exception cref="MarkupException">The attribute or its value breaks a rule of the language.</exception>
        /// <exception cref="ArgumentException"><paramref name="attribute"/> doesn't belong to an element.</exception>
        /// <exception cref="InvalidOperationException">The scope's tree has already been collected.</exception>
        void ApplyAttribute(MarkupLoadScope scope, object target, XAttribute attribute);
    }
}
