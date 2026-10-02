// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Icy.Configuration;
using Icy.Data;
using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.Input;
using Icy.Input.DragDrop;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.UI.Styles;

namespace Icy.UI
{
    /// <summary>
    /// Represents the root container for UI controls and manages the UI tree, input handling, and rendering.
    /// </summary>
    /// <remarks>
    /// The <see cref="Canvas"/> class serves as the main entry point for the UI system, managing the visual tree,
    /// handling input events, and coordinating rendering operations. It maintains a collection of root-level
    /// controls and provides services for focus management, input routing, and visual updates.
    /// </remarks>
    public class Canvas : ObservableDispatcherObject, IContainerLayout
    {
        private const float ScaleEpsilon = 0.0001f;
        private readonly Diagnostics.DebugHudHost debugHudHost;
        private readonly Stopwatch frameTime = new();
        private readonly List<UIElement> overlayElements = [];
        private readonly List<UIElement> rootElements = [];
        private readonly Dictionary<UIElement, UIElement?> scopeReturnFocus = [];
        private IBrush? background;
        private Transform2D contentTransform;
        private DragDropSession? dragDropSession;
        private UIElement? hoveredElement;
        private bool inputRoutingInitialized;
        private float effectiveScale = 1f;
        private Transform2D inverseTransform;
        private bool isInputEnabled;
        private bool isTransformInvalid = true;
        private bool isVisible = true;
        private Vector2 offset;
        private float opacity = 1f;
        private UIElement? draggedElement;
        private UIElement? pressedElement;
        private ReferenceFit? referenceFit;
        private Size? referenceSize;
        private float rotation;
        private Vector2 scale = Vector2.One;
        private UIScaleMode? scaleMode;
        private Transform2D surfaceTransform = Transform2D.Identity;
        private Transform2D transform;
        private Vector2 transformOrigin;

        /// <summary>
        /// Initializes a new instance of the <see cref="Canvas"/> class with the specified configuration.
        /// </summary>
        /// <param name="config">The configuration settings for the UI library.</param>
        public Canvas(IcyConfiguration config)
        {
            Configuration = config;
            debugHudHost = new(this);
            if (config.Theme.Theme != null)
                Resources.MergedDictionaries.Add(config.Theme.Theme);
        }

        /// <summary>
        /// Gets the names of the registered debug tools (see
        /// <see cref="Icy.Configuration.ReflectionConfiguration.Diagnostics"/>) currently active for this canvas -
        /// empty by default, so debug visualization costs nothing until something is added here.
        /// </summary>
        /// <remarks>
        /// A name here that matches a registered <c>IDebugOverlay</c> applies to every element unless overridden
        /// by its own <c>Debug.Visualization</c>; a name that matches a registered <c>IDebugHudPanel</c> shows
        /// that panel in the surface-space HUD. The two catalogs are looked up independently, so one active set can
        /// freely mix overlay and panel names.
        /// </remarks>
        public ISet<string> ActiveDebugTools { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the canvas-wide resource dictionary - the root of the resource chain every attached
        /// <see cref="UIElement"/> can reach, one level above its own outermost ancestor's <see cref="UIElement.Resources"/>.
        /// </summary>
        /// <remarks>
        /// Only consulted for implicit (keyless, per-type) <see cref="Styles.Style"/> resolution, as the final
        /// fallback once an element's own ancestor-<see cref="UIElement.Parent"/> chain is exhausted -
        /// <c>{StaticResource}</c> resolves entirely at markup-load time against the document's own root, so it
        /// never reaches this far. Populated with <see cref="Icy.Configuration.ThemeConfiguration.Theme"/>
        /// automatically at construction when set (see
        /// <see cref="Icy.Configuration.BuildingExtensions.UseDefaultTheme(Icy.Configuration.IcyConfiguration)"/>);
        /// an application can add further shared resources here directly.
        /// </remarks>
        public ResourceDictionary Resources { get; } = new();

        /// <summary>
        /// Gets the ordered list of elements drawn last, every frame, directly in surface space (see
        /// <see cref="SurfaceSize"/>) - unclipped, ignoring this canvas's own pan/rotate/scale transform (see
        /// <see cref="Offset"/>/<see cref="Rotation"/>/<see cref="Scale"/>) but scaled by <see cref="EffectiveScale"/>.
        /// The last entry draws on top.
        /// </summary>
        /// <remarks>
        /// Deliberately minimal - no z-ordering beyond insertion order, no adorner/anchoring system. An entry
        /// positions itself via its own <see cref="UIElement.Margin"/>/<see cref="UIElement.HorizontalAlignment"/>/
        /// <see cref="UIElement.VerticalAlignment"/> against the full <see cref="SurfaceSize"/>, arranged the same way
        /// <see cref="Diagnostics.DebugHudHost"/>'s own surface-space HUD already is. Overlay elements now participate
        /// in <see cref="HitTest(Point)"/> (checked first, so topmost/last-added win) but not in focus traversal or
        /// <see cref="Add(UIElement)"/>'s root-element list - an overlay (a <see cref="DragDropSession.Preview"/>,
        /// a future <c>Dialog</c>'s backdrop, a future <c>ComboBox</c>'s dropdown) sits visually above everything
        /// and is hittable for pointer/touch input, but doesn't participate in keyboard focus or tab order. Use
        /// <see cref="AddOverlay(UIElement)"/>/<see cref="RemoveOverlay(UIElement)"/> to change it, not direct list
        /// mutation - those also wire <see cref="UIElement.Canvas"/>.
        /// </remarks>
        public IReadOnlyList<UIElement> Overlays => overlayElements;

        /// <summary>
        /// Gets or sets a value indicating whether a <c>Debug.Visualization</c> override on an individual element
        /// is honored even while <see cref="ActiveDebugTools"/> is empty - letting one element be "drilled into"
        /// without turning anything on canvas-wide. <see langword="false"/> by default, so the fully-disabled path
        /// costs one collection-count check and one boolean check per element, and never touches per-element
        /// attached-property storage.
        /// </summary>
        public bool AllowPerElementDebugOverrides { get; set; }

        /// <summary>
        /// Gets or sets the background brush of the canvas.
        /// </summary>
        public IBrush? Background
        {
            get => background;
            set => SetProperty(ref background, value);
        }

        /// <summary>
        /// Gets the bounds of the content area of the canvas, in surface units (see <see cref="SurfaceSize"/>).
        /// </summary>
        public Rectangle ContentBounds => new(Point.Empty, SurfaceSize);

        /// <summary>
        /// Gets the scale factor between surface units and physical pixels, as computed by the last
        /// <see cref="RefreshScale"/> call.
        /// </summary>
        /// <remarks>
        /// <c>EffectiveScale = base × </c><see cref="ScalingConfiguration.UserScale"/>, where the base factor comes from
        /// <see cref="ScaleMode"/> (or <see cref="ScalingConfiguration.Mode"/> when unset):
        /// <list type="bullet">
        /// <item><description><see cref="UIScaleMode.None"/>: <c>1</c>.</description></item>
        /// <item><description><see cref="UIScaleMode.Dpi"/>: <see cref="IRenderContext.DisplayScale"/>.</description></item>
        /// <item><description><see cref="UIScaleMode.ReferenceResolution"/>: the viewport-to-<see cref="ReferenceSize"/> ratio picked by <see cref="ReferenceFit"/>.</description></item>
        /// </list>
        /// Recomputed at the start of every <see cref="Render"/>. Raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> when it changes.
        /// </remarks>
        public float EffectiveScale => effectiveScale;

        /// <summary>
        /// Gets the size of the logical drawing surface: the physical viewport divided by <see cref="EffectiveScale"/>,
        /// rounded down to whole units.
        /// </summary>
        /// <remarks>
        /// Root elements, overlays and the debug HUD are all laid out against this size.
        /// </remarks>
        public Size SurfaceSize
        {
            get
            {
                Size physical = Configuration.RenderContext.ViewportSize;
                return new((int)MathF.Floor(physical.Width / effectiveScale), (int)MathF.Floor(physical.Height / effectiveScale));
            }
        }

        /// <summary>
        /// Gets or sets the scale mode of this canvas, overriding <see cref="ScalingConfiguration.Mode"/>.
        /// <see langword="null"/> (the default) inherits the configuration value.
        /// </summary>
        public UIScaleMode? ScaleMode
        {
            get => scaleMode;
            set
            {
                if (SetProperty(ref scaleMode, value))
                    RefreshScale();
            }
        }

        /// <summary>
        /// Gets or sets the reference resolution of this canvas, overriding <see cref="ScalingConfiguration.ReferenceSize"/>.
        /// <see langword="null"/> (the default) inherits the configuration value.
        /// </summary>
        public Size? ReferenceSize
        {
            get => referenceSize;
            set
            {
                if (SetProperty(ref referenceSize, value))
                    RefreshScale();
            }
        }

        /// <summary>
        /// Gets or sets the reference fit policy of this canvas, overriding <see cref="ScalingConfiguration.ReferenceFit"/>.
        /// <see langword="null"/> (the default) inherits the configuration value.
        /// </summary>
        public ReferenceFit? ReferenceFit
        {
            get => referenceFit;
            set
            {
                if (SetProperty(ref referenceFit, value))
                    RefreshScale();
            }
        }

        /// <summary>
        /// Gets the element currently holding input focus, or <see langword="null"/> if none does.
        /// </summary>
        public UIElement? FocusedElement { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether input is enabled for the canvas.
        /// </summary>
        public bool IsInputEnabled
        {
            get => isInputEnabled;
            set => SetProperty(ref isInputEnabled, value);
        }

        /// <summary>
        /// Gets a value indicating whether the mouse is currently over the GUI.
        /// </summary>
        public bool IsMouseOverGUI { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the canvas is visible.
        /// </summary>
        public bool IsVisible
        {
            get => isVisible;
            set => SetProperty(ref isVisible, value);
        }

        /// <summary>
        /// Gets or sets the offset of the canvas.
        /// </summary>
        public Vector2 Offset
        {
            get => offset;
            set
            {
                if (SetProperty(ref offset, value))
                    InvalidateTransform();
            }
        }

        /// <summary>
        /// Gets or sets the opacity of the canvas.
        /// </summary>
        public float Opacity
        {
            get => opacity;
            set => SetProperty(ref opacity, value);
        }

        /// <summary>
        /// Gets or sets the rotation angle of the canvas in degrees.
        /// </summary>
        public float Rotation
        {
            get => rotation;
            set
            {
                if (SetProperty(ref rotation, value))
                    InvalidateTransform();
            }
        }

        /// <summary>
        /// Gets or sets the scale factor of the canvas.
        /// </summary>
        public Vector2 Scale
        {
            get => scale;
            set
            {
                if (SetProperty(ref scale, value))
                    InvalidateTransform();
            }
        }

        /// <summary>
        /// Gets or sets the origin point for transformations applied to the canvas.
        /// </summary>
        public Vector2 TransformOrigin
        {
            get => transformOrigin;
            set
            {
                if (SetProperty(ref transformOrigin, value))
                    InvalidateTransform();
            }
        }

        /// <summary>
        /// Gets the UI library configuration instance.
        /// </summary>
        internal IcyConfiguration Configuration { get; }

        /// <summary>
        /// Gets the transform from surface units to physical pixels (a uniform <see cref="EffectiveScale"/> scale).
        /// Overlays and the debug HUD draw with it.
        /// </summary>
        internal Transform2D SurfaceTransform
        {
            get
            {
                if (isTransformInvalid)
                    UpdateTransform();
                return surfaceTransform;
            }
        }

        /// <summary>
        /// Adds a UI element to the canvas.
        /// </summary>
        /// <param name="element">The UI element to add.</param>
        public void Add(UIElement element)
        {
            rootElements.Add(element);
            element.Canvas = this;
        }

        /// <summary>
        /// Adds an element to <see cref="Overlays"/> and wires its <see cref="UIElement.Canvas"/>.
        /// </summary>
        /// <param name="element">The element to add.</param>
        public void AddOverlay(UIElement element)
        {
            overlayElements.Add(element);
            element.Canvas = this;
        }

        /// <summary>
        /// Removes an element from <see cref="Overlays"/> and clears its <see cref="UIElement.Canvas"/>.
        /// </summary>
        /// <param name="element">The element to remove.</param>
        /// <returns><see langword="true"/> if the element was found and removed; otherwise, <see langword="false"/>.</returns>
        public bool RemoveOverlay(UIElement element)
        {
            if (overlayElements.Remove(element))
            {
                element.Canvas = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Invalidates the current transformation, marking it as needing an update.
        /// </summary>
        public void InvalidateTransform()
        {
            isTransformInvalid = true;
        }

        /// <summary>
        /// Recomputes <see cref="EffectiveScale"/> from the current configuration, display scale, viewport and overrides,
        /// and re-arranges all content if it changed.
        /// </summary>
        /// <remarks>
        /// Called automatically at the start of every <see cref="Render"/> and when an override changes. The canvas
        /// deliberately doesn't subscribe to configuration or render-context events: it has no disposal point, and a
        /// subscription would keep every discarded canvas alive.
        /// </remarks>
        public void RefreshScale()
        {
            ScalingConfiguration scaling = Configuration.Scaling;
            IRenderContext context = Configuration.RenderContext;
            float newScale = UIScaleCalculator.Compute(
                ScaleMode ?? scaling.Mode,
                context.DisplayScale,
                context.ViewportSize,
                ReferenceSize ?? scaling.ReferenceSize,
                ReferenceFit ?? scaling.ReferenceFit,
                scaling.UserScale);

            if (MathF.Abs(newScale - effectiveScale) < ScaleEpsilon)
                return;

            effectiveScale = newScale;
            InvalidateTransform();
            foreach (UIElement element in rootElements)
                element.InvalidateArrange();
            foreach (UIElement overlay in overlayElements)
                overlay.InvalidateArrange();

            OnPropertyChanged(nameof(EffectiveScale));
            OnPropertyChanged(nameof(SurfaceSize));
        }

        /// <summary>
        /// Handles the resize event, invalidating the arrangement of child elements.
        /// </summary>
        public void OnResize()
        {
            foreach (var element in rootElements)
                element.InvalidateArrange();
        }

        /// <summary>
        /// Removes a UI element from the canvas.
        /// </summary>
        /// <param name="element">The UI element to remove.</param>
        /// <returns><c>true</c> if the element was successfully removed; otherwise, <c>false</c>.</returns>
        public bool Remove(UIElement element)
        {
            if (rootElements.Remove(element))
            {
                element.Canvas = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines which element - if any - is under the specified point. Checks <see cref="Overlays"/> first
        /// (topmost/last-added wins), falling back to <see cref="rootElements"/> - see <see cref="Overlays"/>'s own
        /// remarks for why overlay content participates here (unlike focus traversal, which it still doesn't).
        /// </summary>
        /// <param name="screenPoint">A point in screen/window space (the same space pointer/touch positions arrive in).</param>
        /// <returns>The topmost hit-testable element under the point, or <see langword="null"/> if none is.</returns>
        public UIElement? HitTest(Point screenPoint)
        {
            // Overlays are tested in surface space - the physical point divided by EffectiveScale (last-added =
            // topmost = checked first, matching Overlays' own draw order). Unlike rootElements, they're arranged
            // against SurfaceSize without this canvas's own content transform (see RenderVisual/UpdateLayout), so
            // hit-testing must match rather than going through ScreenToCanvasSpace.
            Vector2 surfacePoint = ScreenToSurface(screenPoint);
            for (int i = overlayElements.Count - 1; i >= 0; i--)
            {
                UIElement? hit = overlayElements[i].HitTest(surfacePoint);
                if (hit != null)
                    return hit;
            }

            Vector2 canvasLocalPoint = ScreenToCanvasSpace(screenPoint);
            foreach (UIElement element in rootElements.OrderByDescending(e => e.ZIndex))
            {
                UIElement? hit = element.HitTest(canvasLocalPoint);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        /// <summary>
        /// Finds the nearest enclosing <see cref="UIElement.IsFocusScope"/> ancestor of the specified element
        /// (walking up through <see cref="UIElement.Parent"/>), or <see langword="null"/> if none of its ancestors
        /// (or itself) are a focus scope.
        /// </summary>
        /// <param name="element">The element to find the enclosing focus scope of.</param>
        /// <returns>The nearest enclosing focus scope, or <see langword="null"/> if there isn't one.</returns>
        public static UIElement? FindEnclosingFocusScope(UIElement? element)
        {
            for (UIElement? current = element; current != null; current = current.Parent)
            {
                if (current.IsFocusScope)
                    return current;
            }

            return null;
        }

        /// <summary>
        /// Moves input focus to the specified element.
        /// </summary>
        /// <param name="element">
        /// The element to focus, or <see langword="null"/> to clear focus. Ignored (no-op) if the element isn't
        /// <see cref="UIElement.IsFocusable"/> or isn't <see cref="UIElement.IsVisible"/>.
        /// </param>
        public void Focus(UIElement? element)
        {
            if (element != null && (!element.IsFocusable || !element.IsVisible))
                return;
            if (FocusedElement == element)
                return;

            // Remember what was focused before entering a scope, so CloseFocusScope can restore it later -
            // e.g. closing a dialog should return focus to whatever opened it.
            UIElement? enteredScope = FindEnclosingFocusScope(element);
            if (enteredScope != null && FindEnclosingFocusScope(FocusedElement) != enteredScope)
                scopeReturnFocus[enteredScope] = FocusedElement;

            FocusedElement?.SetFocused(false);
            FocusedElement = element;
            FocusedElement?.SetFocused(true);
        }

        /// <summary>
        /// Restores whatever was focused before <paramref name="scope"/> was entered (see <see cref="Focus"/>),
        /// as if the user had pressed cancel/back while focus was inside it.
        /// </summary>
        /// <param name="scope">
        /// The <see cref="UIElement.IsFocusScope"/> element to close, e.g. a <c>Window</c> being dismissed via
        /// <c>Window.Close()</c>. A no-op if nothing was ever focused before entering this scope.
        /// </param>
        /// <remarks>
        /// This is the same restore logic <see cref="Icy.Input.Events.INavigationEvents.CloseModal"/> triggers
        /// (e.g. pressing Escape/a cancel button) - exposed so API-driven scope closes (like a <c>Window</c>'s
        /// <c>Close()</c> method) restore focus consistently too, not just input-driven ones.
        /// </remarks>
        public void CloseFocusScope(UIElement scope)
        {
            UIElement? returnFocus = scopeReturnFocus.TryGetValue(scope, out UIElement? f) ? f : null;
            scopeReturnFocus.Remove(scope);
            Focus(returnFocus);
        }

        /// <summary>
        /// Moves focus to the next or previous focusable element, wrapping around, and respecting the focused
        /// element's enclosing <see cref="UIElement.IsFocusScope"/> boundary (if any) - focus won't move outside it.
        /// </summary>
        /// <param name="forward">
        /// <see langword="true"/> to move to the next element in traversal order; <see langword="false"/> for the previous one.
        /// </param>
        public void MoveFocus(bool forward)
        {
            UIElement? scope = FindEnclosingFocusScope(FocusedElement);
            IEnumerable<UIElement> roots = scope != null ? scope.EnumerateVisualSubtree().Skip(1) : EnumerateAllElements();
            List<UIElement> focusable = [.. roots.Where(e => e.IsFocusable && e.IsVisible)];
            if (focusable.Count == 0)
                return;

            int currentIndex = FocusedElement != null ? focusable.IndexOf(FocusedElement) : -1;
            int nextIndex = currentIndex == -1
                ? (forward ? 0 : focusable.Count - 1)
                : ((currentIndex + (forward ? 1 : -1)) + focusable.Count) % focusable.Count;
            Focus(focusable[nextIndex]);
        }

        /// <summary>
        /// Renders the canvas and its child elements.
        /// </summary>
        /// <remarks>
        /// This method updates the input state, layout, and visual representation of the canvas.
        /// It should be called every frame to ensure the UI is rendered correctly.
        /// </remarks>
        public void Render()
        {
            frameTime.Stop();
            RefreshScale();
            if (isTransformInvalid)
                UpdateTransform();

            Dispatcher.Update(DispatcherPriority.DataBind);
            Dispatcher.UpdateFrameBindings();
            Dispatcher.UpdateAnimations(frameTime.Elapsed);

            // Skip input and rendering for invisible control,
            // but keep animations and bindings work.
            if (!IsVisible || Opacity <= 0)
                return;

            UpdateInput();
            Dispatcher.Update(DispatcherPriority.Input);

            UpdateLayout();
            RenderVisual();

            Dispatcher.Update();
            frameTime.Restart();
        }

        /// <summary>
        /// Converts a point in screen/window space (the same space pointer/touch/drag positions arrive in) into
        /// this canvas's own local content space.
        /// </summary>
        /// <param name="screenPoint">A point in screen/window space.</param>
        /// <returns>The equivalent point in canvas-local content space.</returns>
        internal Vector2 ScreenToCanvasSpace(Point screenPoint)
        {
            if (isTransformInvalid)
                UpdateTransform();
            return inverseTransform.Apply(new Vector2(screenPoint.X, screenPoint.Y));
        }

        /// <summary>
        /// Converts a physical screen point (where pointer input arrives) into surface units.
        /// </summary>
        /// <param name="screenPoint">A point in physical screen/window pixels.</param>
        /// <returns>The same point in surface units.</returns>
        internal Vector2 ScreenToSurface(Point screenPoint) => new(screenPoint.X / effectiveScale, screenPoint.Y / effectiveScale);

        /// <summary>
        /// Converts a point in this canvas's local content space into surface units, i.e. applies only the canvas's own
        /// <see cref="Offset"/>/<see cref="Rotation"/>/<see cref="Scale"/>, not the surface scale.
        /// </summary>
        /// <param name="canvasLocalPoint">A point in canvas-local content space.</param>
        /// <returns>The same point in surface units.</returns>
        internal Vector2 CanvasToSurfaceSpace(Vector2 canvasLocalPoint)
        {
            if (isTransformInvalid)
                UpdateTransform();
            return contentTransform.Apply(canvasLocalPoint);
        }

        /// <summary>
        /// Converts a point in this canvas's own local content space into screen/window space - the exact inverse of
        /// <see cref="ScreenToCanvasSpace(Point)"/>.
        /// </summary>
        /// <param name="canvasLocalPoint">A point in this canvas's own local content space.</param>
        /// <returns>The equivalent point in screen/window space.</returns>
        internal Vector2 CanvasToScreenSpace(Vector2 canvasLocalPoint)
        {
            if (isTransformInvalid)
                UpdateTransform();
            return transform.Apply(canvasLocalPoint);
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanging(PropertyChangingEventArgs e)
        {
            VerifyAccess();
            base.OnPropertyChanging(e);
        }

        private static void PositionOverlayAtScreenPoint(UIElement overlay, Point screenPoint)
        {
            overlay.HorizontalAlignment = HorizontalAlignment.Left;
            overlay.VerticalAlignment = VerticalAlignment.Top;
            overlay.Margin = new Thickness(screenPoint.X, screenPoint.Y, 0, 0);
        }

        /// <summary>
        /// Enumerates <paramref name="element"/> followed by every ancestor up to the root, via <see cref="UIElement.Parent"/>.
        /// </summary>
        /// <remarks>
        /// Used to bubble routed pointer effects (<see cref="ControlState.Hovered"/>/<see cref="ControlState.Pressed"/>,
        /// <see cref="UIElement.OnTap"/>, the drag hooks) from the exact hit-tested leaf up through every ancestor
        /// that might care - e.g. hovering a <c>Button</c>'s label <c>TextBlock</c> should still mark the
        /// <c>Button</c> itself as hovered, the same way CSS's <c>:hover</c> cascades to ancestors.
        /// </remarks>
        private static IEnumerable<UIElement> SelfAndAncestors(UIElement? element)
        {
            for (UIElement? current = element; current != null; current = current.Parent)
                yield return current;
        }

        private IEnumerable<UIElement> EnumerateAllElements()
        {
            foreach (UIElement root in rootElements)
            {
                foreach (UIElement element in root.EnumerateVisualSubtree())
                    yield return element;
            }
        }

        private void EnsureInputRoutingInitialized()
        {
            if (inputRoutingInitialized)
                return;
            inputRoutingInitialized = true;

            var events = Configuration.Input.Events;
            events.Touch.TouchDown += OnTouchDown;
            events.Touch.TouchUp += OnTouchUp;
            events.Touch.Tap += OnTap;
            events.Drag.DragStarted += OnDragStarted;
            events.Drag.DragPerforming += OnDragPerforming;
            events.Drag.DragEnded += OnDragEnded;
            events.Scroll.Scroll += OnScroll;
            events.Navigation.FocusNext += (_, _) => MoveFocus(forward: true);
            events.Navigation.FocusPrevious += (_, _) => MoveFocus(forward: false);
            events.Navigation.CloseModal += OnCloseModal;
        }

        private void OnDragEnded(object? sender, GenericEventArgs<Point> e)
        {
            foreach (UIElement element in SelfAndAncestors(draggedElement))
                element.OnDragEnded(e.Data);
            draggedElement = null;

            if (dragDropSession is { } session)
            {
                session.ScreenPoint = e.Data;
                if (session.CurrentTarget is { } target && target.CanDrop(session))
                    target.OnDrop(session);
                if (session.Preview != null)
                    RemoveOverlay(session.Preview);
                dragDropSession = null;
            }
        }

        private void OnDragPerforming(object? sender, GenericEventArgs<Point> e)
        {
            foreach (UIElement element in SelfAndAncestors(draggedElement))
                element.OnDragPerforming(e.Data);

            if (dragDropSession is { } session)
            {
                session.ScreenPoint = e.Data;
                if (session.Preview != null)
                    PositionOverlayAtScreenPoint(session.Preview, e.Data);
                UpdateDragDropTarget(session, e.Data);
            }
        }

        private void OnDragStarted(object? sender, AcceptableEventArgs<Point> e)
        {
            draggedElement = HitTest(e.Data);
            foreach (UIElement element in SelfAndAncestors(draggedElement))
                element.OnDragStarted(e.Data);

            foreach (UIElement element in SelfAndAncestors(draggedElement))
            {
                if (element is IDragSource source && source.TryBeginDrag(e.Data, out object? payload, out UIElement? preview))
                {
                    // TryBeginDrag's own contract requires a non-null payload on a true return.
                    dragDropSession = new DragDropSession(element, payload!, e.Data) { Preview = preview };
                    if (preview != null)
                    {
                        // The preview's top-left sits exactly on the cursor (see PositionOverlayAtScreenPoint) and
                        // UIElement.HitTest's bounds check is inclusive at the origin, so an ordinarily hit-testable
                        // preview would win every HitTest of the drag point - and, since an overlay has no Parent,
                        // UpdateDragDropTarget's ancestor walk would terminate on it and never reach the real drop
                        // target underneath. The ghost visual must never be a target itself.
                        preview.IsHitTestVisible = false;
                        AddOverlay(preview);
                        PositionOverlayAtScreenPoint(preview, e.Data);
                    }

                    break;
                }
            }
        }

        private void UpdateDragDropTarget(DragDropSession session, Point screenPoint)
        {
            UIElement? hit = HitTest(screenPoint);
            IDropTarget? newTarget = null;
            foreach (UIElement element in SelfAndAncestors(hit))
            {
                if (element is IDropTarget candidate && candidate.CanDrop(session))
                {
                    newTarget = candidate;
                    break;
                }
            }

            if (newTarget != session.CurrentTarget)
            {
                session.CurrentTarget?.OnDragLeave(session);
                session.CurrentTarget = newTarget;
                newTarget?.OnDragEnter(session);
            }
            else
            {
                newTarget?.OnDragOver(session);
            }
        }

        private void OnTouchDown(object? sender, GenericEventArgs<Point> e)
        {
            pressedElement = HitTest(e.Data);
            foreach (UIElement element in SelfAndAncestors(pressedElement))
                element.ControlState |= ControlState.Pressed;
        }

        private void OnTouchUp(object? sender, GenericEventArgs<Point> e)
        {
            foreach (UIElement element in SelfAndAncestors(pressedElement))
                element.ControlState &= ~ControlState.Pressed;
            pressedElement = null;
        }

        private void OnTap(object? sender, GenericEventArgs<Icy.Input.Events.TouchInfo> e)
        {
            UIElement? hit = HitTest(e.Data.LastTouch);
            if (hit == null)
                return;

            UIElement? focusable = SelfAndAncestors(hit).FirstOrDefault(element => element.IsFocusable);
            if (focusable != null)
                Focus(focusable);

            foreach (UIElement element in SelfAndAncestors(hit))
                element.OnTap();
        }

        private void OnCloseModal(object? sender, EventArgs e)
        {
            UIElement? scope = FindEnclosingFocusScope(FocusedElement);
            if (scope != null)
                CloseFocusScope(scope);
        }

        private void OnScroll(object? sender, GenericEventArgs<Icy.Input.Events.ScrollInfo> e)
        {
            // Stop at the first element that actually consumes the scroll (see UIElement.OnScroll's remarks) -
            // otherwise a scrollable region nested inside another scrollable region also scrolled every ancestor
            // around it, since every one of them received the same wheel/swipe event.
            foreach (UIElement element in SelfAndAncestors(hoveredElement))
            {
                if (element.OnScroll(e.Data))
                    break;
            }
        }

        private void RenderVisual()
        {
            var context = Configuration.RenderContext;
            var oldScissor = context.Options.Scissor;
            context.Begin();

            if (isTransformInvalid)
                UpdateTransform();
            context.Transform = transform;
            context.Options.Scissor = transform.Apply(ContentBounds);

            Background?.Draw(context, new(ContentBounds));

            foreach (var element in rootElements)
            {
                element.Draw(context);
            }

            if (overlayElements.Count > 0)
            {
                // Surface space, not part of the content transform these elements' HorizontalAlignment/
                // VerticalAlignment/Margin would otherwise be subject to - drop the canvas's own pan/rotate/scale but
                // keep the surface scale, mirroring DebugHudHost.Render. The scissor stays in physical pixels.
                context.Transform = surfaceTransform;
                context.Options.Scissor = new Rectangle(Point.Empty, context.ViewportSize);
                foreach (UIElement overlay in overlayElements)
                    overlay.Draw(context);
            }

            if (ActiveDebugTools.Count > 0)
                debugHudHost.Render(context, frameTime.Elapsed);

            context.End();
            context.Options.Scissor = oldScissor;
        }

        private void UpdateHover()
        {
            UIElement? hit = HitTest(Configuration.Input.Mouse.MouseInfo.Position);
            if (hit == hoveredElement)
                return;

            // Diff the old/new ancestor chains rather than blindly clearing-then-resetting Hovered on both, so an
            // ancestor that's hovered before and after (e.g. the mouse moved between two leaves within the same
            // Button) doesn't flicker its VisualState off and back on for no visible reason.
            HashSet<UIElement> oldChain = [.. SelfAndAncestors(hoveredElement)];
            HashSet<UIElement> newChain = [.. SelfAndAncestors(hit)];

            foreach (UIElement element in oldChain)
            {
                if (!newChain.Contains(element))
                    element.ControlState &= ~ControlState.Hovered;
            }

            foreach (UIElement element in newChain)
            {
                if (!oldChain.Contains(element))
                    element.ControlState |= ControlState.Hovered;
            }

            hoveredElement = hit;
        }

        private void UpdateInput()
        {
            if (!IsInputEnabled)
                return;

            EnsureInputRoutingInitialized();

            foreach (var device in Configuration.Input)
            {
                if (device is IUpdateableInput updateable)
                {
                    updateable.Update(frameTime.Elapsed);
                }
            }

            UpdateHover();
        }

        private void UpdateLayout()
        {
            foreach (var element in rootElements)
                element.Arrange();

            if (overlayElements.Count > 0)
            {
                var viewport = new Rectangle(Point.Empty, SurfaceSize);
                foreach (UIElement overlay in overlayElements)
                    overlay.Arrange(viewport);
            }
        }

        private void UpdateTransform()
        {
            // Content (Offset/Rotation/Scale, in surface units) is applied first, then the surface scale maps to
            // physical pixels. AddTransform(other) applies `other` before the existing matrix.
            contentTransform = Transform2D.Create(Offset, Rotation, TransformOrigin * SurfaceSize.AsVector(), Scale);
            surfaceTransform = Transform2D.Create(Matrix3x2.CreateScale(effectiveScale));
            transform = surfaceTransform;
            transform.AddTransform(contentTransform);
            if (Matrix3x2.Invert(transform.Matrix, out Matrix3x2 inverse))
                inverseTransform = Transform2D.Create(inverse);
            isTransformInvalid = false;
        }
    }
}