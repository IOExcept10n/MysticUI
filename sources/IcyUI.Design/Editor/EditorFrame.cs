// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;
using Icy.Data.Markup;
using Icy.Design.Syntax;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The design-time editor overlay for a live page: click to select, drag to move, drag the handles to resize, and use
    /// the editor's key bindings, with every gesture recorded as an undoable markup edit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The frame adds two overlays to the canvas: a full-surface capture layer that takes the pointer in
    /// <see cref="EditorMode.Edit"/> and draws the adorners, and a small toolbar with the mode toggle, the selection, and
    /// why editing is blocked when it is. In <see cref="EditorMode.Interact"/> the capture layer lets the pointer through,
    /// so the page and the game work normally while the selection stays visible.
    /// </para>
    /// <para>
    /// The logic lives in <see cref="Session"/>; the frame only shows it and feeds it pointer gestures. Detaching keeps
    /// every edit; save the documents through <see cref="DesignDocument.Save"/>.
    /// </para>
    /// <para>Typical in-game use:</para>
    /// <code>
    /// EditorFrame editor = EditorFrame.Attach(canvas, designSession);
    /// // ... edit, playtest in Interact mode, edit more ...
    /// foreach (DesignDocument document in designSession.Documents)
    ///     document.Save();
    /// editor.Dispose();
    /// </code>
    /// </remarks>
    public sealed class EditorFrame : IDisposable
    {
        private readonly Canvas canvas;
        private readonly DesignSession design;
        private readonly TextBlock modeLabel = new() { Margin = new Thickness(8, 0, 0, 0) };
        private readonly TextBlock statusLabel = new() { Foreground = Color.Salmon, Margin = new Thickness(8, 0, 0, 0) };
        private EditorToolbarPlacement toolbarPlacement;
        private string? lastFailure;
        private bool disposed;

        private EditorFrame(Canvas canvas, DesignSession design)
        {
            this.canvas = canvas;
            this.design = design;
            Session = EditorSession.Attach(design, canvas);
            CaptureLayer = new EditorCaptureLayer(this);
            Toolbar = CreateToolbar();
            ToolbarPlacement = EditorToolbarPlacement.TopLeft;
        }

        /// <summary>
        /// Gets the session the frame shows; share it with other tools to share the selection.
        /// </summary>
        public EditorSession Session { get; }

        /// <summary>
        /// Gets or sets the corner the toolbar sits in. Defaults to <see cref="EditorToolbarPlacement.TopLeft"/>.
        /// </summary>
        public EditorToolbarPlacement ToolbarPlacement
        {
            get => toolbarPlacement;
            set
            {
                toolbarPlacement = value;
                bool left = value is EditorToolbarPlacement.TopLeft or EditorToolbarPlacement.BottomLeft;
                bool top = value is EditorToolbarPlacement.TopLeft or EditorToolbarPlacement.TopRight;
                Toolbar.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                Toolbar.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            }
        }

        /// <summary>Gets or sets the color of the selection outline and handles.</summary>
        public Color SelectionColor { get; set; } = Color.DeepSkyBlue;

        /// <summary>Gets or sets the color of the hover outline.</summary>
        public Color HoverColor { get; set; } = Color.FromArgb(160, Color.LightSkyBlue);

        /// <summary>Gets or sets the color of the drop indicator.</summary>
        public Color IndicatorColor { get; set; } = Color.Orange;

        /// <summary>Gets or sets the color of the dragged element's ghost outline.</summary>
        public Color GhostColor { get; set; } = Color.FromArgb(200, Color.White);

        /// <summary>Gets or sets the side of the square resize handles, in surface units. Defaults to 7.</summary>
        public float HandleSize { get; set; } = 7;

        internal EditorCaptureLayer CaptureLayer { get; }

        internal Border Toolbar { get; }

        internal string ToolbarLabel => modeLabel.Text ?? string.Empty;

        internal string? ToolbarStatus => string.IsNullOrEmpty(statusLabel.Text) ? null : statusLabel.Text;

        /// <summary>
        /// Attaches an editor to <paramref name="canvas"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="canvas">The canvas whose pages to edit.</param>
        /// <param name="design">The design session tracking those pages.</param>
        /// <returns>The frame; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public static EditorFrame Attach(Canvas canvas, DesignSession design)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(design);

            var frame = new EditorFrame(canvas, design);
            frame.Start();
            return frame;
        }

        /// <summary>
        /// Detaches the editor: removes its overlays and key bindings, and gives the canvas its focus and navigation back.
        /// Every edit stays.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            design.Configuration.Input.Events.Touch.Tap -= OnTap;
            Session.ModeChanged -= OnSessionChanged;
            Session.SelectionChanged -= OnSessionChanged;
            Session.BlockedChanged -= OnSessionChanged;
            canvas.RemoveOverlay(CaptureLayer);
            canvas.RemoveOverlay(Toolbar);
            Session.Dispose();
        }

        internal void BeginDrag(Point screenPoint)
        {
        }

        internal void UpdateDrag(Point screenPoint)
        {
        }

        internal void EndDrag(Point screenPoint)
        {
        }

        internal void CancelDrag()
        {
        }

        internal void Render(IRenderContext context)
        {
            if (Session.Mode == EditorMode.Edit && !IsOnTop())
                Dispatcher.GetCurrentThreadDispatcher().Invoke(BringToTop);
        }

        private static string Describe(EditorSelection selection)
        {
            ElementSyntax? element = EditorSession.Syntax(selection);
            if (element == null)
                return selection.Instance.GetType().Name;

            string? name = Editing.TreeMatcher.GetDirective(selection.Document.Syntax, element, MarkupDirectives.Name);
            return name != null ? $"{element.Name} \"{name}\"" : element.Name;
        }

        private Border CreateToolbar()
        {
            var toggle = new Button
            {
                Content = new TextBlock { Text = "Edit / Interact" },
                Padding = new Thickness(8, 4),
                Command = Session.Commands.ToggleMode,
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(toggle);
            row.Children.Add(modeLabel);
            row.Children.Add(statusLabel);
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 32, 32, 38)),
                Padding = new Thickness(6),
                Margin = new Thickness(8),
                Child = row,
            };
        }

        private void Start()
        {
            canvas.AddOverlay(CaptureLayer);
            canvas.AddOverlay(Toolbar);
            Session.OwnLayers.Add(CaptureLayer);
            Session.OwnLayers.Add(Toolbar);
            Session.ModeChanged += OnSessionChanged;
            Session.SelectionChanged += OnSessionChanged;
            Session.BlockedChanged += OnSessionChanged;
            design.Configuration.Input.Events.Touch.Tap += OnTap;
            UpdateToolbar();
        }

        private void OnSessionChanged(object? sender, EventArgs e) => UpdateToolbar();

        private void OnTap(object? sender, GenericEventArgs<TouchInfo> e)
        {
        }

        private void UpdateToolbar()
        {
            CaptureLayer.IsHitTestVisible = Session.Mode == EditorMode.Edit;
            string mode = Session.Mode == EditorMode.Edit ? "Edit" : "Interact";
            string what = Session.Selection is { } selection
                ? Describe(selection)
                : HasTrackedContent() ? "Click an element to select it" : "No tracked pages on this canvas";
            modeLabel.Text = $"{mode} · {what}";
            statusLabel.Text = Session.BlockedReason ?? lastFailure ?? string.Empty;
        }

        private bool HasTrackedContent()
        {
            foreach (DesignDocument document in design.Documents)
            {
                if (document.Syntax.Root is { } root && document.GetNodeId(root) is NodeId id
                    && document.GetObjects(id).OfType<UIElement>().Any(x => ReferenceEquals(x.Canvas, canvas)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOnTop()
        {
            IReadOnlyList<UIElement> overlays = canvas.Overlays;
            return overlays.Count >= 2 && ReferenceEquals(overlays[^1], Toolbar) && ReferenceEquals(overlays[^2], CaptureLayer);
        }

        private void BringToTop()
        {
            if (disposed || IsOnTop())
                return;

            canvas.RemoveOverlay(CaptureLayer);
            canvas.RemoveOverlay(Toolbar);
            canvas.AddOverlay(CaptureLayer);
            canvas.AddOverlay(Toolbar);
        }
    }
}
