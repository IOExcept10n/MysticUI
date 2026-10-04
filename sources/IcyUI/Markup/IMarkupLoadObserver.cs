// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.Data.Bindings;

namespace Icy.Markup
{
    /// <summary>
    /// Receives notifications about what <see cref="MarkupLoader"/> builds, and from which markup node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Install one through <see cref="MarkupConfiguration.LoadObserver"/>. Design tooling uses it to map each live
    /// object back to the markup it came from. When no observer is installed (the default), the loader does no extra
    /// work and allocates nothing extra.
    /// </para>
    /// <para>
    /// For every observed load the calls arrive on the loading thread, in this order:
    /// </para>
    /// <list type="number">
    /// <item><description><see cref="DocumentStarted"/>, once.</description></item>
    /// <item><description>
    /// <see cref="ObjectCreated"/> for each element, before any of its attributes are applied, then
    /// <see cref="MemberApplied"/> for each attribute or property element applied to it, in document order.
    /// </description></item>
    /// <item><description><see cref="DocumentCompleted"/> or <see cref="DocumentFailed"/>, once.</description></item>
    /// </list>
    /// <para>
    /// Calls made through <see cref="IMarkupBuilder"/> report <see cref="ObjectCreated"/> and
    /// <see cref="MemberApplied"/> against the scope that was passed in, without a start or completion.
    /// </para>
    /// <para>
    /// Every node's <see cref="System.Xml.IXmlLineInfo"/> points at the first character of its name.
    /// </para>
    /// </remarks>
    public interface IMarkupLoadObserver
    {
        /// <summary>
        /// Gets the kinds of load this observer wants to hear about. The loader creates no scope and makes no call
        /// for any other kind.
        /// </summary>
        MarkupLoadScopeKind ObservedKinds { get; }

        /// <summary>
        /// Called when an observed load starts.
        /// </summary>
        /// <param name="scope">The new load's scope.</param>
        void DocumentStarted(MarkupLoadScope scope);

        /// <summary>
        /// Called right after an element's object is constructed, before its attributes are applied.
        /// </summary>
        /// <param name="scope">The load's scope.</param>
        /// <param name="node">The element the object was built from.</param>
        /// <param name="instance">The constructed object.</param>
        void ObjectCreated(MarkupLoadScope scope, XElement node, object instance);

        /// <summary>
        /// Called right after an attribute, property element or element text is applied to a member.
        /// </summary>
        /// <param name="scope">The load's scope.</param>
        /// <param name="node">The <see cref="XAttribute"/> or <see cref="XElement"/> the value came from.</param>
        /// <param name="target">The object whose member was set.</param>
        /// <param name="member">The member that was set.</param>
        /// <param name="value">
        /// The value assigned, or <see cref="MarkupValue.Unset"/> when a markup extension chose not to assign one.
        /// </param>
        /// <param name="binding">
        /// The binding a markup extension such as <c>{Binding}</c> attached to <paramref name="target"/> while
        /// resolving this value, or <see langword="null"/> when it attached none.
        /// </param>
        void MemberApplied(MarkupLoadScope scope, XObject node, object target, MarkupMember member, object? value, IBinding? binding);

        /// <summary>
        /// Called when an observed load completes successfully.
        /// </summary>
        /// <param name="scope">The load's scope.</param>
        /// <param name="root">The root object the load produced.</param>
        void DocumentCompleted(MarkupLoadScope scope, object root);

        /// <summary>
        /// Called when an observed load fails, just before the loader rethrows <paramref name="error"/>.
        /// </summary>
        /// <param name="scope">The load's scope.</param>
        /// <param name="error">The error the load is about to throw.</param>
        void DocumentFailed(MarkupLoadScope scope, MarkupException error);
    }
}
