// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Data;
using Icy.Data.Markup;
using Icy.Design.Editor.Placement;
using Icy.Design.Syntax;
using Icy.Input.Devices;
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
    /// The frame adds two overlays to the canvas: a capture layer over the editor's scope (the whole surface when there
    /// is none) that takes the pointer in <see cref="EditorMode.Edit"/> and draws the adorners, and a small toolbar with
    /// the mode toggle, the selection, and why editing is blocked when it is. In <see cref="EditorMode.Interact"/> the capture layer lets the pointer through,
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
    /// <para>Scoped to one part of the UI, for example a tool's content area next to its own sidebar:</para>
    /// <code>
    /// EditorFrame editor = EditorFrame.Attach(canvas, designSession, contentArea);
    /// </code>
    /// <para>
    /// The sidebar keeps working with the pointer, the wheel and the middle button scroll the content area, and the keyboard
    /// stays with the editor in <see cref="EditorMode.Edit"/>. A canvas takes one editor at a time
    /// (see <see cref="EditorSession.FindAttached"/>).
    /// </para>
    /// </remarks>
    public sealed class EditorFrame : IDisposable
    {
        private const int ToolbarInset = 8;
        private readonly Canvas canvas;
        private readonly DesignSession design;
        private readonly TextBlock modeLabel = new() { Margin = new Thickness(8, 0, 0, 0) };
        private readonly TextBlock statusLabel = new() { Foreground = Color.Salmon, Margin = new Thickness(8, 0, 0, 0) };
        private EditorToolbarPlacement toolbarPlacement;
        private string? lastFailure;
        private MoveGesture? move;
        private ResizeGesture? resize;
        private (Rectangle Region, Size Toolbar, EditorToolbarPlacement Placement)? applied;
        private bool regionEmpty;
        private bool disposed;

        private EditorFrame(Canvas canvas, DesignSession design, UIElement? scope)
        {
            this.canvas = canvas;
            this.design = design;
            Session = EditorSession.Attach(design, canvas, scope);

            // The layer takes left and touch drags in Edit mode; the wheel and middle-button pans reach the page beneath.
            CaptureLayer = new EditorCaptureLayer(this) { PassesUnclaimedInput = true };
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
        /// Attaches an editor to the whole of <paramref name="canvas"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="canvas">The canvas whose pages to edit.</param>
        /// <param name="design">The design session tracking those pages.</param>
        /// <returns>The frame; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Another editor session is attached to <paramref name="canvas"/>.</exception>
        public static EditorFrame Attach(Canvas canvas, DesignSession design) => Attach(canvas, design, null);

        /// <summary>
        /// Attaches an editor to <paramref name="canvas"/>, limited to <paramref name="scope"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="canvas">The canvas whose pages to edit.</param>
        /// <param name="design">The design session tracking those pages.</param>
        /// <param name="scope">
        /// The subtree to edit (see <see cref="EditorSession.Scope"/>), or <see langword="null"/> for the whole canvas.
        /// The capture layer, the toolbar and the adorners keep to the scope's visible area, so the rest of the canvas
        /// stays usable with the pointer.
        /// </param>
        /// <returns>The frame; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="canvas"/> or <paramref name="design"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="scope"/> is not on <paramref name="canvas"/>.</exception>
        /// <exception cref="InvalidOperationException">Another editor session is attached to <paramref name="canvas"/>.</exception>
        public static EditorFrame Attach(Canvas canvas, DesignSession design, UIElement? scope)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(design);

            var frame = new EditorFrame(canvas, design, scope);
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
            if (Session.Mode != EditorMode.Edit)
                return;

            lastFailure = null;
            if (Session.Selection is { } selection && !Session.IsBlocked)
            {
                RectangleF bounds = AdornerGeometry.SurfaceBounds(selection.Instance);
                ResizeHandle handle = AdornerGeometry.HandleAt(bounds, HandleSize, AdornerGeometry.ScreenToSurface(screenPoint, canvas.EffectiveScale));
                if (handle != ResizeHandle.None)
                {
                    resize = Session.BeginResize(handle, screenPoint);
                    return;
                }
            }

            if (Session.ResolveSelectable(Session.HitTest(screenPoint)) is not { } target)
                return;
            if (!ReferenceEquals(Session.Selection?.Instance, target) && !Session.Select(target))
                return;

            move = Session.BeginMove(screenPoint);
        }

        internal void UpdateDrag(Point screenPoint)
        {
            // Alt is the usual "don't snap" modifier in design tools; touch has none, so snapping is the default.
            bool snap = design.Configuration.Input.Keyboard is not { } keyboard || (keyboard.ModifierKeys & ModifierKeys.Alt) == 0;
            resize?.Update(screenPoint, snap);
            move?.Update(screenPoint);
        }

        internal void EndDrag(Point screenPoint)
        {
            EditResult? result = null;
            if (resize != null)
            {
                result = resize.Complete();
                resize = null;
            }

            if (move != null)
            {
                move.Update(screenPoint);
                result = move.Complete();
                move = null;
            }

            if (result is { Succeeded: false })
            {
                lastFailure = result.Error?.Message;
                UpdateToolbar();
            }
        }

        internal void CancelDrag()
        {
            resize?.Cancel();
            move?.Cancel();
            resize = null;
            move = null;
        }

        internal AdornerScene BuildScene()
        {
            bool editing = Session.Mode == EditorMode.Edit;
            RectangleF? hover = null;
            if (editing && move == null && resize == null)
            {
                Point mouse = design.Configuration.Input.Mouse.MouseInfo.Position;
                if (Session.ResolveSelectable(Session.HitTest(mouse)) is { } hovered && !ReferenceEquals(hovered, Session.Selection?.Instance))
                    hover = AdornerGeometry.SurfaceBounds(hovered);
            }

            RectangleF? selected = null;
            IReadOnlyList<RectangleF> handles = [];
            if (Session.Selection is { } selection && selection.Instance.Canvas == canvas)
            {
                RectangleF bounds = AdornerGeometry.SurfaceBounds(selection.Instance);
                selected = bounds;
                if (editing && !Session.IsBlocked && move == null)
                    handles = [.. AdornerGeometry.Handles(bounds, HandleSize).Select(x => x.Area)];
            }

            RectangleF? indicator = null;
            RectangleF? ghost = null;
            bool line = false;
            if (move != null)
            {
                ghost = AdornerGeometry.ScreenToSurface(move.GhostBounds, canvas.EffectiveScale);
                if (move.Target is { } target && move.TargetContainer is { } container)
                {
                    indicator = AdornerGeometry.ToSurface(container, target.Indicator);
                    line = target.IndicatorIsLine;
                }
            }

            if (resize is { Indicator: { } area })
                indicator = AdornerGeometry.ToSurface(resize.Container, area);

            return new AdornerScene(hover, selected, handles, !editing, indicator, line, ghost);
        }

        internal void Render(IRenderContext context)
        {
            if (Session.Mode == EditorMode.Edit && !IsOnTop())
                Dispatcher.GetCurrentThreadDispatcher().Invoke(BringToTop);

            SyncRegion();
            if (regionEmpty)
                return;

            // The scene is in surface units; the layer sits at the region's origin and clips to it.
            Point origin = CaptureLayer.PointToSurface(Vector2.Zero);
            AdornerScene scene = BuildScene();
            if (scene.Hover is { } hover)
                Outline(context, Shift(hover, origin), HoverColor, 1);
            if (scene.Selection is { } selection)
                Outline(context, Shift(selection, origin), scene.Dimmed ? Color.FromArgb(110, SelectionColor) : SelectionColor, 1.5f);
            foreach (RectangleF handle in scene.Handles)
            {
                RectangleF area = Shift(handle, origin);
                context.FillRectangle(new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), Color.White);
                Outline(context, area, SelectionColor, 1);
            }

            if (scene.Ghost is { } ghost)
                Outline(context, Shift(ghost, origin), GhostColor, 1);
            if (scene.Indicator is { } indicator)
            {
                RectangleF area = Shift(indicator, origin);
                if (scene.IndicatorIsLine)
                    context.DrawLine(area.Left, area.Top, area.Right, area.Bottom, IndicatorColor, 3);
                else
                    Outline(context, area, IndicatorColor, 2);
            }
        }

        private static RectangleF Shift(RectangleF area, Point origin) =>
            new(area.X - origin.X, area.Y - origin.Y, area.Width, area.Height);

        private static void Outline(IRenderContext context, RectangleF area, Color color, float thickness) =>
            context.DrawRectangle(new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), color, thickness);

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
            if (disposed || Session.Mode != EditorMode.Edit)
                return;

            // Only taps the capture layer receives: a tap on the toolbar belongs to its buttons.
            Point point = e.Data.LastTouch;
            if (!ReferenceEquals(canvas.HitTest(point), CaptureLayer))
                return;

            if (Session.ResolveSelectable(Session.HitTest(point)) is { } target)
                Session.Select(target);
            else
                Session.Clear();
        }

        /// <summary>
        /// Fits the capture layer and the toolbar to the session's region. Runs every frame from the capture layer's render
        /// and changes layout only when the region, the toolbar's size or its placement changed.
        /// </summary>
        private void SyncRegion()
        {
            Rectangle region = Session.Region();
            bool empty = region.IsEmpty;
            if (empty != regionEmpty)
            {
                regionEmpty = empty;
                UpdateToolbar();
            }

            // Without a scope the layer stretches over the canvas and the toolbar uses alignments, as before scoping.
            if (Session.Scope == null || empty)
                return;

            Size toolbar = Toolbar.ActualBounds.Size;
            if (applied == (region, toolbar, toolbarPlacement))
                return;

            applied = (region, toolbar, toolbarPlacement);
            CaptureLayer.HorizontalAlignment = HorizontalAlignment.Left;
            CaptureLayer.VerticalAlignment = VerticalAlignment.Top;
            CaptureLayer.Margin = new Thickness(region.X, region.Y, 0, 0);
            CaptureLayer.Width = region.Width;
            CaptureLayer.Height = region.Height;
            PlaceToolbar(region, toolbar);
        }

        private void PlaceToolbar(Rectangle region, Size size)
        {
            Size surface = canvas.SurfaceSize;
            bool left = toolbarPlacement is EditorToolbarPlacement.TopLeft or EditorToolbarPlacement.BottomLeft;
            bool top = toolbarPlacement is EditorToolbarPlacement.TopLeft or EditorToolbarPlacement.TopRight;
            int x = left ? region.Left + ToolbarInset : region.Right - ToolbarInset - size.Width;
            int y = top ? region.Top + ToolbarInset : region.Bottom - ToolbarInset - size.Height;
            Toolbar.HorizontalAlignment = HorizontalAlignment.Left;
            Toolbar.VerticalAlignment = VerticalAlignment.Top;
            Toolbar.Margin = new Thickness(
                Math.Clamp(x, 0, Math.Max(0, surface.Width - size.Width)),
                Math.Clamp(y, 0, Math.Max(0, surface.Height - size.Height)),
                0,
                0);
        }

        private void UpdateToolbar()
        {
            CaptureLayer.IsHitTestVisible = Session.Mode == EditorMode.Edit && !regionEmpty;
            Toolbar.IsVisible = !regionEmpty;
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
