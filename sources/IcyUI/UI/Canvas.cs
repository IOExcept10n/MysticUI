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
        private readonly Stopwatch frameTime = new();
        private readonly List<UIElement> rootElements = [];
        private readonly Dictionary<UIElement, UIElement?> scopeReturnFocus = [];
        private IBrush? background;
        private UIElement? hoveredElement;
        private bool inputRoutingInitialized;
        private Transform2D inverseTransform;
        private bool isInputEnabled;
        private bool isTransformInvalid = true;
        private bool isVisible = true;
        private Vector2 offset;
        private float opacity = 1f;
        private UIElement? pressedElement;
        private float rotation;
        private Vector2 scale = Vector2.One;
        private Transform2D transform;
        private Vector2 transformOrigin;

        /// <summary>
        /// Initializes a new instance of the <see cref="Canvas"/> class with the specified configuration.
        /// </summary>
        /// <param name="config">The configuration settings for the UI library.</param>
        public Canvas(IcyConfiguration config)
        {
            Configuration = config;
        }

        /// <summary>
        /// Gets or sets the background brush of the canvas.
        /// </summary>
        public IBrush? Background
        {
            get => background;
            set => SetProperty(ref background, value);
        }

        /// <summary>
        /// Gets the bounds of the content area of the canvas.
        /// </summary>
        public Rectangle ContentBounds => new(Point.Empty, Configuration.RenderContext.ViewportSize);

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
        /// Adds a UI element to the canvas.
        /// </summary>
        /// <param name="element">The UI element to add.</param>
        public void Add(UIElement element)
        {
            rootElements.Add(element);
            element.Canvas = this;
        }

        /// <summary>
        /// Invalidates the current transformation, marking it as needing an update.
        /// </summary>
        public void InvalidateTransform()
        {
            isTransformInvalid = true;
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
        /// Determines which element - if any - is under the specified point.
        /// </summary>
        /// <param name="screenPoint">A point in screen/window space (the same space pointer/touch positions arrive in).</param>
        /// <returns>The topmost hit-testable element under the point, or <see langword="null"/> if none is.</returns>
        public UIElement? HitTest(Point screenPoint)
        {
            if (isTransformInvalid)
                UpdateTransform();
            Vector2 canvasLocalPoint = inverseTransform.Apply(new Vector2(screenPoint.X, screenPoint.Y));
            foreach (UIElement element in rootElements.OrderByDescending(e => e.ZIndex))
            {
                UIElement? hit = element.HitTest(canvasLocalPoint);
                if (hit != null)
                    return hit;
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

            // Remember what was focused before entering a scope, so OnCloseModal can restore it later -
            // e.g. closing a dialog should return focus to whatever opened it.
            UIElement? enteredScope = FindEnclosingFocusScope(element);
            if (enteredScope != null && FindEnclosingFocusScope(FocusedElement) != enteredScope)
                scopeReturnFocus[enteredScope] = FocusedElement;

            FocusedElement?.SetFocused(false);
            FocusedElement = element;
            FocusedElement?.SetFocused(true);
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
            if (isTransformInvalid)
                UpdateTransform();

            Dispatcher.Update(DispatcherPriority.DataBind);
            Dispatcher.UpdateFrameBindings();

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

        /// <inheritdoc/>
        protected override void OnPropertyChanging(PropertyChangingEventArgs e)
        {
            VerifyAccess();
            base.OnPropertyChanging(e);
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
            events.Navigation.FocusNext += (_, _) => MoveFocus(forward: true);
            events.Navigation.FocusPrevious += (_, _) => MoveFocus(forward: false);
            events.Navigation.CloseModal += OnCloseModal;
        }

        private void OnTouchDown(object? sender, GenericEventArgs<Point> e)
        {
            pressedElement = HitTest(e.Data);
            if (pressedElement != null)
                pressedElement.ControlState |= ControlState.Pressed;
        }

        private void OnTouchUp(object? sender, GenericEventArgs<Point> e)
        {
            if (pressedElement != null)
            {
                pressedElement.ControlState &= ~ControlState.Pressed;
                pressedElement = null;
            }
        }

        private void OnTap(object? sender, GenericEventArgs<Icy.Input.Events.TouchInfo> e)
        {
            UIElement? hit = HitTest(e.Data.LastTouch);
            if (hit != null && hit.IsFocusable)
                Focus(hit);
        }

        private void OnCloseModal(object? sender, EventArgs e)
        {
            UIElement? scope = FindEnclosingFocusScope(FocusedElement);
            if (scope == null)
                return;

            UIElement? returnFocus = scopeReturnFocus.TryGetValue(scope, out UIElement? f) ? f : null;
            scopeReturnFocus.Remove(scope);
            Focus(returnFocus);
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

            context.End();
            context.Options.Scissor = oldScissor;
        }

        private void UpdateHover()
        {
            UIElement? hit = HitTest(Configuration.Input.Mouse.MouseInfo.Position);
            if (hit == hoveredElement)
                return;

            if (hoveredElement != null)
                hoveredElement.ControlState &= ~ControlState.Hovered;
            hoveredElement = hit;
            if (hoveredElement != null)
                hoveredElement.ControlState |= ControlState.Hovered;
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
        }

        private void UpdateTransform()
        {
            transform = Transform2D.Create(Offset, Rotation, TransformOrigin * Configuration.RenderContext.ViewportSize.AsVector(), Scale);
            if (Matrix3x2.Invert(transform.Matrix, out Matrix3x2 inverse))
                inverseTransform = Transform2D.Create(inverse);
            isTransformInvalid = false;
        }
    }
}