// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.CompilerServices;
using Icy.Configuration;
using Icy.Design.Tracking;
using Icy.Markup;

namespace Icy.Design
{
    /// <summary>
    /// Tracks every markup page a configuration loads, so the pages can be edited live and saved back as markup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attach a session <b>before</b> loading the pages you want to edit; pages loaded earlier aren't tracked.
    /// Release builds of a game shouldn't attach one at all, and needn't reference this assembly:
    /// </para>
    /// <code>
    /// #if DEBUG
    /// using DesignSession session = DesignSession.Attach(configuration);
    /// #endif
    /// </code>
    /// <para>
    /// The session never keeps a page alive. When a page is collected, its document is dropped the next time the
    /// session is asked for documents.
    /// </para>
    /// </remarks>
    public sealed class DesignSession : IDisposable
    {
        private readonly DesignLoadObserver observer;
        private readonly List<DesignDocument> documents = [];
        private readonly ConditionalWeakTable<MarkupLoadScope, DesignDocument> scopeDocuments = new();
        private bool disposed;
        private Func<string, string?> sourcePathResolver = static path => File.Exists(path) ? Path.GetFullPath(path) : null;

        private DesignSession(IcyConfiguration configuration)
        {
            Configuration = configuration;
            observer = new DesignLoadObserver(this);
            Builder = new MarkupLoader(configuration);
        }

        /// <summary>
        /// Gets the configuration this session tracks loads for.
        /// </summary>
        public IcyConfiguration Configuration { get; }

        /// <summary>
        /// Gets the documents of every tracked page still alive.
        /// </summary>
        public IReadOnlyList<DesignDocument> Documents
        {
            get
            {
                documents.RemoveAll(x => !x.IsAlive);
                return [.. documents];
            }
        }

        /// <summary>
        /// Gets or sets how <see cref="DesignDocument.Save"/> turns a document's <see cref="DesignDocument.SourcePath"/>
        /// into a file to write.
        /// </summary>
        /// <remarks>
        /// A source path is whatever the page was loaded with, often an asset name such as <c>Pages/Main.xml</c>
        /// resolved through an <see cref="Icy.Assets.IAssetContext"/>. By default an existing file path resolves to its
        /// full path and anything else to <see langword="null"/>, which makes <see cref="DesignDocument.Save"/> throw.
        /// Set this to map asset names to source files in your project.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
        public Func<string, string?> SourcePathResolver
        {
            get => sourcePathResolver;
            set => sourcePathResolver = value ?? throw new ArgumentNullException(nameof(value));
        }

        internal IMarkupBuilder Builder { get; }

        internal bool IsDisposed => disposed;

        /// <summary>
        /// Starts tracking every page <paramref name="configuration"/> loads from now on.
        /// </summary>
        /// <param name="configuration">The configuration whose loads to track.</param>
        /// <returns>The new session. Dispose it to stop tracking.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="configuration"/> already has a <see cref="MarkupConfiguration.LoadObserver"/>, such as another
        /// session's.
        /// </exception>
        public static DesignSession Attach(IcyConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            MarkupConfiguration markup = configuration.Types.Markup;
            if (markup.LoadObserver != null)
                throw new InvalidOperationException("This configuration already has a markup load observer. Dispose the other design session first.");

            var session = new DesignSession(configuration);
            markup.LoadObserver = session.observer;
            return session;
        }

        /// <summary>
        /// Finds a source folder by walking up from <see cref="AppContext.BaseDirectory"/> to the first directory that
        /// contains <paramref name="marker"/>.
        /// </summary>
        /// <param name="marker">A file name that marks the project folder, such as <c>MyGame.csproj</c>.</param>
        /// <param name="relative">The path from that folder to the markup files, such as <c>Assets/UI</c>.</param>
        /// <returns>The combined path, or <see langword="null"/> when no ancestor contains the marker.</returns>
        /// <example>
        /// <code>
        /// if (DesignSession.FindSourceRoot("MyGame.csproj", "Assets/UI") is { } root)
        ///     session.UseSourceRoot(root);
        /// </code>
        /// </example>
        public static string? FindSourceRoot(string marker, string relative = "") =>
            FindSourceRoot(AppContext.BaseDirectory, marker, relative);

        /// <summary>
        /// Finds a source folder by walking up from <paramref name="startDirectory"/> to the first directory that contains
        /// <paramref name="marker"/>.
        /// </summary>
        /// <param name="startDirectory">The directory to start from.</param>
        /// <param name="marker">A file name that marks the project folder.</param>
        /// <param name="relative">The path from that folder to the markup files.</param>
        /// <returns>The combined path, or <see langword="null"/> when no ancestor contains the marker.</returns>
        public static string? FindSourceRoot(string startDirectory, string marker, string relative)
        {
            ArgumentException.ThrowIfNullOrEmpty(startDirectory);
            ArgumentException.ThrowIfNullOrEmpty(marker);
            for (DirectoryInfo? directory = new(startDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, marker)))
                    return Path.Combine(directory.FullName, relative ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar);
            }

            return null;
        }

        /// <summary>
        /// Finds the document a live object was built from, and the markup element it was built from.
        /// </summary>
        /// <param name="instance">A live object, typically a <see cref="Icy.UI.UIElement"/> of a tracked page.</param>
        /// <param name="node">The markup element <paramref name="instance"/> was built from.</param>
        /// <returns>
        /// The document, or <see langword="null"/> when <paramref name="instance"/> doesn't come from a tracked page:
        /// it was added by code at runtime, or its page was loaded before the session was attached.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        public DesignDocument? FindDocument(object instance, out NodeId node)
        {
            ArgumentNullException.ThrowIfNull(instance);

            foreach (DesignDocument document in Documents)
            {
                if (document.TryGetNodeId(instance, out node))
                    return document;
            }

            node = default;
            return null;
        }

        /// <summary>
        /// Saves every modified document that can be saved.
        /// </summary>
        /// <returns>
        /// The modified documents that weren't saved, with the reason: <see cref="DesignDocument.SaveBlockedReason"/>,
        /// or the message of the error that stopped the write. Never throws for one document's failure.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public IReadOnlyList<(DesignDocument Document, string Reason)> SaveAll()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var skipped = new List<(DesignDocument, string)>();
            foreach (DesignDocument document in Documents)
            {
                if (!document.IsModified)
                    continue;
                if (document.SaveBlockedReason is { } reason)
                {
                    skipped.Add((document, reason));
                    continue;
                }

                try
                {
                    document.Save();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    // InvalidOperationException: the source file vanished between the SaveBlockedReason check and the write.
                    skipped.Add((document, ex.Message));
                }
            }

            return skipped;
        }

        /// <summary>
        /// Makes <see cref="DesignDocument.Save"/> write each page to <paramref name="root"/> combined with its source
        /// path, for pages loaded by asset name such as <c>UI/MainMenu.xml</c>.
        /// </summary>
        /// <param name="root">The folder holding the source markup files, usually inside the game project.</param>
        /// <remarks>
        /// Only existing files resolve, so a typo in the root disables Save with a reason instead of creating files in the
        /// wrong place. Use <see cref="FindSourceRoot(string, string)"/> to locate the folder from the running game.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="root"/> is <see langword="null"/> or empty.</exception>
        public void UseSourceRoot(string root)
        {
            ArgumentException.ThrowIfNullOrEmpty(root);
            string full = Path.GetFullPath(root);
            SourcePathResolver = path =>
            {
                string candidate = Path.GetFullPath(Path.Combine(full, path));
                return File.Exists(candidate) ? candidate : null;
            };
        }

        /// <summary>
        /// Stops tracking loads, and detaches every document. Their editors throw <see cref="ObjectDisposedException"/>
        /// from then on.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            MarkupConfiguration markup = Configuration.Types.Markup;
            if (ReferenceEquals(markup.LoadObserver, observer))
                markup.LoadObserver = null;

            documents.Clear();
        }

        internal DesignDocument? GetDocument(MarkupLoadScope scope) =>
            !disposed && scopeDocuments.TryGetValue(scope, out DesignDocument? document) ? document : null;

        internal void OnDocumentStarted(MarkupLoadScope scope)
        {
            if (disposed)
                return;

            string text = scope.SourceText ?? string.Empty;
            DesignDocument? document = null;
            if (scope.SourcePath != null)
            {
                foreach (DesignDocument candidate in documents)
                {
                    if (candidate.SourcePath == scope.SourcePath && candidate.Text == text)
                    {
                        document = candidate;
                        break;
                    }
                }
            }

            document ??= new DesignDocument(this, scope.SourcePath, text);
            document.AddScope(scope);
            scopeDocuments.AddOrUpdate(scope, document);
        }

        internal void OnDocumentCompleted(MarkupLoadScope scope)
        {
            if (GetDocument(scope) is { } document && !documents.Contains(document))
                documents.Add(document);
        }

        internal void OnDocumentFailed(MarkupLoadScope scope)
        {
            if (GetDocument(scope) is not { } document)
                return;

            document.RemoveScope(scope);
            scopeDocuments.Remove(scope);
        }
    }
}
