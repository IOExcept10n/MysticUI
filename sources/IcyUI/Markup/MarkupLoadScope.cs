// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// An opaque handle to one observed load: one document, merged dictionary or template instantiation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scope exists only while a <see cref="MarkupConfiguration.LoadObserver"/> is installed and observes the
    /// load's <see cref="Kind"/>. Without an observer the loader creates none, so ordinary loads pay nothing for
    /// this.
    /// </para>
    /// <para>
    /// A scope never keeps the loaded tree alive: <see cref="Root"/> and <see cref="NameScope"/> are weak, and
    /// become <see langword="null"/> once the tree has been collected. Pass the scope back to
    /// <see cref="IMarkupBuilder"/> to build or update objects in the same document context.
    /// </para>
    /// </remarks>
    public sealed class MarkupLoadScope
    {
        [ThreadStatic]
        private static int createdOnCurrentThread;

        private readonly WeakReference<MarkupNameScope> nameScope;
        private WeakReference<object>? root;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupLoadScope"/> class.
        /// </summary>
        /// <param name="kind">The kind of load.</param>
        /// <param name="sourcePath">The document path, or <see langword="null"/>.</param>
        /// <param name="sourceText">The parsed text, or <see langword="null"/> for templates.</param>
        /// <param name="nameScope">The name scope the load registers into.</param>
        /// <param name="observer">The observer to report to.</param>
        internal MarkupLoadScope(MarkupLoadScopeKind kind, string? sourcePath, string? sourceText, MarkupNameScope nameScope, IMarkupLoadObserver observer)
        {
            Kind = kind;
            SourcePath = sourcePath;
            SourceText = sourceText;
            this.nameScope = new WeakReference<MarkupNameScope>(nameScope);
            Observer = observer;
            createdOnCurrentThread++;
        }

        /// <summary>
        /// Gets the kind of load this scope covers.
        /// </summary>
        public MarkupLoadScopeKind Kind { get; }

        /// <summary>
        /// Gets the path the document was loaded from, as passed to the loader, or <see langword="null"/>.
        /// </summary>
        /// <remarks>
        /// Often an asset name resolved through an <see cref="Icy.Assets.IAssetContext"/> rather than a file path.
        /// </remarks>
        public string? SourcePath { get; }

        /// <summary>
        /// Gets the exact text the loader parsed, without any byte order mark, or <see langword="null"/> for
        /// <see cref="MarkupLoadScopeKind.TemplateContent"/> and <see cref="MarkupLoadScopeKind.DataTemplateContent"/>
        /// scopes, which are built from an already-parsed element.
        /// </summary>
        public string? SourceText { get; }

        /// <summary>
        /// Gets the name scope the load registers <c>x:Name</c>s into, or <see langword="null"/> once the loaded tree
        /// has been collected.
        /// </summary>
        public MarkupNameScope? NameScope => nameScope.TryGetTarget(out MarkupNameScope? value) ? value : null;

        /// <summary>
        /// Gets the root object the load produced, or <see langword="null"/> before it completes and after the root
        /// has been collected.
        /// </summary>
        public object? Root => root != null && root.TryGetTarget(out object? value) ? value : null;

        /// <summary>
        /// Gets how many scopes were created on the calling thread so far. Tests use it to prove that unobserved
        /// loads create none.
        /// </summary>
        internal static int CreatedOnCurrentThread => createdOnCurrentThread;

        /// <summary>
        /// Gets the observer this scope reports to.
        /// </summary>
        internal IMarkupLoadObserver Observer { get; }

        /// <summary>
        /// Records the root object once the load completed.
        /// </summary>
        /// <param name="value">The root object.</param>
        internal void SetRoot(object value) => root = new WeakReference<object>(value);
    }
}
