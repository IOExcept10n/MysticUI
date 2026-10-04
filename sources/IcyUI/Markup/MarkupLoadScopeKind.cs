// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// Identifies what kind of load a <see cref="MarkupLoadScope"/> covers. Combine values to choose what an
    /// <see cref="IMarkupLoadObserver"/> wants to hear about through <see cref="IMarkupLoadObserver.ObservedKinds"/>.
    /// </summary>
    [Flags]
    public enum MarkupLoadScopeKind
    {
        /// <summary>
        /// No load at all. An observer whose <see cref="IMarkupLoadObserver.ObservedKinds"/> is this is never called.
        /// </summary>
        None = 0,

        /// <summary>
        /// A document loaded through one of the <see cref="MarkupLoader"/> <c>Load</c>/<c>LoadObject</c> overloads.
        /// </summary>
        Document = 1,

        /// <summary>
        /// A resource dictionary document loaded on behalf of another document's
        /// <c>&lt;ResourceDictionary Source="..."/&gt;</c>.
        /// </summary>
        MergedDictionary = 2,

        /// <summary>
        /// A <see cref="Icy.UI.Styles.ControlTemplate"/>'s content, built for one templated control. This happens
        /// every time a control applies a template, so it can be very frequent.
        /// </summary>
        TemplateContent = 4,

        /// <summary>
        /// A <see cref="Icy.UI.Styles.DataTemplate"/>'s content, built for one data item. Pooled item containers do
        /// this whenever they need a new item view, so it can be very frequent.
        /// </summary>
        DataTemplateContent = 8,

        /// <summary>
        /// Every kind of load.
        /// </summary>
        All = Document | MergedDictionary | TemplateContent | DataTemplateContent,
    }
}
