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
        private IBrush? background;
        private Transform2D inverseTransform;
        private bool isInputEnabled;
        private bool isTransformInvalid = true;
        private bool isVisible = true;
        private Vector2 offset;
        private float opacity = 1f;
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

        private void UpdateInput()
        {
            if (!IsInputEnabled)
                return;
            foreach (var device in Configuration.Input)
            {
                if (device is IUpdateableInput updateable)
                {
                    updateable.Update(frameTime.Elapsed);
                }
            }
        }

        private void UpdateLayout()
        {
            foreach (var element in rootElements)
                element.Arrange();
        }

        private void UpdateTransform()
        {
            transform = Transform2D.Create(Offset, Rotation, TransformOrigin * Configuration.RenderContext.ViewportSize.AsVector(), Scale);
            isTransformInvalid = false;
        }
    }
}