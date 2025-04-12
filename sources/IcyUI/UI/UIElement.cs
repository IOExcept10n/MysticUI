// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Icy.Rendering;

namespace Icy.UI
{
    /// <summary>
    /// Represents the base class for all UI elements in the game interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UIElement"/> is the fundamental building block of the UI system, providing
    /// layout, rendering, and input handling capabilities. All visual elements
    /// in the game interface derive from this class.
    /// </para>
    /// <para>
    /// This class implements a dependency property system for efficient property
    /// change notification and value inheritance. It also provides a comprehensive
    /// event system for handling user input and state changes.
    /// </para>
    /// </remarks>
    public abstract class UIElement : DependencyObject//, INotifyFocusChanged
    {
        private UpdateFlags updateFlags;
        private Transform2D transform;
        private Transform2D inverseTransform;

        /// <summary>
        /// Occurs when the <see cref="UIElement"/> arrange is updated.
        /// </summary>
        public event EventHandler? ArrangeUpdated;

        /// <summary>
        /// Occurs when the <see cref="UIElement"/> size is updated.
        /// </summary>
        public event EventHandler? SizeChanged;

        /// <summary>
        /// Defines a flag set for the <see cref="UIElement"/> instance layout invalidation state.
        /// </summary>
        protected enum UpdateFlags
        {
            /// <summary>
            /// The UI element is up-to-date and don't need to recalculate layout.
            /// </summary>
            None,

            /// <summary>
            /// The UI element measures should be recalculated before rendering.
            /// </summary>
            Measure = 1 << 0,

            /// <summary>
            /// The UI element should be rearranged before rendering.
            /// </summary>
            Arrange = 1 << 1,

            /// <summary>
            /// The UI element transform should be recalculated before rendering.
            /// </summary>
            Transform = 1 << 2,

            /// <summary>
            /// All of the layout properties should be recalculated before rendering.
            /// </summary>
            All = Measure | Arrange | Transform,
        }

        /// <summary>
        /// Gets or sets the actual bounds of the <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [DependencyProperty]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public Rectangle ActualBounds { get => GetValue<Rectangle>(); protected set => SetValue(value); }

        /// <summary>
        /// Gets or sets the thickness of the border around <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [DependencyProperty]
        [AffectsArrange]
        [AffectsMeasure]
        public Thickness BorderThickness { get => GetValue<Thickness>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets an instance of <see cref="UI.Canvas"/> that is used as the root for the hierarchy for this <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public Canvas? Canvas { get; protected set; }

        /// <summary>
        /// Gets or sets the size the <see cref="UIElement"/> instance wants to be.
        /// </summary>
        [Category("Layout")]
        [DependencyProperty]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public Size DesiredSize { get => GetValue<Size>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the height of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// If set to <see cref="float.NaN"/>, the <see cref="UIElement"/> instance will size to its content.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [DependencyProperty]
        [AffectsMeasure]
        [AffectsArrange]
        public float Height { get => GetValue<float>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the horizontal alignment of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// Determines how the <see cref="UIElement"/> instance is positioned horizontally within its parent.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(HorizontalAlignment.Stretch)]
        [DependencyProperty]
        [AffectsMeasure]
        [AffectsArrange]
        public HorizontalAlignment HorizontalAlignment { get => GetValue<HorizontalAlignment>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the margin around the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// The margin is the space between this <see cref="UIElement"/> instance and its parent or adjacent <see cref="UIElement"/> instances.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [DependencyProperty]
        [AffectsMeasure]
        [AffectsArrange]
        public Thickness Margin { get => GetValue<Thickness>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the maximal <see cref="UIElement"/> height.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [DependencyProperty]
        [AffectsArrange]
        [AffectsMeasure]
        public float MaxHeight { get => GetValue<float>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the maximal <see cref="UIElement"/> width.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [DependencyProperty]
        [AffectsArrange]
        [AffectsMeasure]
        public float MaxWidth { get => GetValue<float>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the minimal <see cref="UIElement"/> height.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [DependencyProperty]
        [AffectsArrange]
        [AffectsMeasure]
        public float MinHeight { get => GetValue<float>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the minimal <see cref="UIElement"/> width.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [DependencyProperty]
        [AffectsArrange]
        [AffectsMeasure]
        public float MinWidth { get => GetValue<float>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the padding of the <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [DependencyProperty]
        public Thickness Padding { get => GetValue<Thickness>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the parent of this <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public UIElement? Parent { get; protected set; }

        /// <summary>
        /// Gets or sets the origin point for transforms.
        /// </summary>
        /// <remarks>
        /// The origin is specified as a relative point, where (0,0) is the top-left
        /// and (1,1) is the bottom-right of the <see cref="UIElement"/> instance.
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(typeof(Vector2), "0.5,0.5")]
        [DependencyProperty]
        [AffectsArrange]
        [AffectsTransform]
        public Vector2 RenderTransformOrigin { get => GetValue<Vector2>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the rotation of the <see cref="UIElement"/> instance in degrees.
        /// </summary>
        /// <remarks>
        /// Values are normalized to the range 0-360 degrees.
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(0.0f)]
        [Range(0.0f, 360.0f)]
        [DependencyProperty]
        [AffectsArrange]
        public float Rotation { get => GetValue<float>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the scale of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// A scale of (1,1) represents the original size.
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(typeof(Vector2), "1,1")]
        [DependencyProperty]
        [AffectsArrange]
        [AffectsTransform]
        public Vector2 Scale { get => GetValue<Vector2>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the width of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// If set to <see cref="float.NaN"/>, the <see cref="UIElement"/> will size to its content.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [DependencyProperty]
        [AffectsMeasure]
        [AffectsArrange]
        public float Width { get => GetValue<float>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the vertical alignment of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// Determines how the <see cref="UIElement"/> is positioned vertically within its parent.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(VerticalAlignment.Stretch)]
        [DependencyProperty]
        [AffectsArrange]
        [AffectsMeasure]
        public VerticalAlignment VerticalAlignment { get => GetValue<VerticalAlignment>(); set => SetValue(value); }

        /// <summary>
        /// Gets or sets the flags that indicate whether the <see cref="UIElement"/> instance should recalculate any of layout properties.
        /// </summary>
        protected UpdateFlags LayoutInvalid
        {
            get => updateFlags;
            set
            {
                if (updateFlags == value) return;
                updateFlags = value;
                Parent?.InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the transform of the <see cref="UIElement"/> is invalid.
        /// </summary>
        protected bool IsTransformInvalid
        {
            get => (LayoutInvalid & UpdateFlags.Transform) != 0;
            set
            {
                if (value)
                    LayoutInvalid |= UpdateFlags.Transform;
                else
                    LayoutInvalid &= ~UpdateFlags.Transform;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the measure of the <see cref="UIElement"/> is invalid.
        /// </summary>
        protected bool IsMeasureInvalid
        {
            get => (LayoutInvalid & UpdateFlags.Measure) != 0;
            set
            {
                if (value)
                    LayoutInvalid |= UpdateFlags.Measure;
                else
                    LayoutInvalid &= ~UpdateFlags.Measure;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether whether the arrangement of the <see cref="UIElement"/> is invalid.
        /// </summary>
        protected bool IsArrangeInvalid
        {
            get => (LayoutInvalid & UpdateFlags.Arrange) != 0;
            set
            {
                if (value)
                    LayoutInvalid |= UpdateFlags.Arrange;
                else
                    LayoutInvalid &= ~UpdateFlags.Arrange;
            }
        }

        /// <summary>
        /// Arranges the <see cref="UIElement"/> instance in specified container bounds.
        /// </summary>
        public void Arrange()
        {
            if (!IsArrangeInvalid) return;

            var containerBounds = Rectangle.Empty;
            if (Parent != null)
            {
                containerBounds = Parent.ActualBounds - Parent.Padding;
            }
            else if (Canvas != null)
            {
                containerBounds = Canvas.Viewport;
            }

            var desiredSize = Measure();

            // Every property call redirects to DependencyObject lookup so cache this property for fast access
            var margin = Margin;

            // Calculate total required space (content + margins)
            var totalWidth = desiredSize.Width + margin.Width;
            var totalHeight = desiredSize.Height + margin.Height;

            // Calculate effective margins and size (without modifying properties)
            var effectiveMargin = margin;
            var effectiveSize = desiredSize;

            var availableWidth = containerBounds.Width - margin.Width;
            var availableHeight = containerBounds.Height - margin.Height;

            // Handle horizontal size and overflow
            if (HorizontalAlignment == HorizontalAlignment.Stretch)
            {
                effectiveSize.Width = (int)float.Clamp(availableWidth, MinWidth, MaxWidth);
            }
            else if (totalWidth > containerBounds.Width)
            {
                // Handle width overflow
                if (availableWidth >= MinWidth)
                {
                    effectiveSize.Width = (int)float.Clamp(availableWidth, MinWidth, MaxWidth);
                }
                else
                {
                    var marginRatio = (float)containerBounds.Width / totalWidth;
                    effectiveMargin = effectiveMargin with
                    {
                        Left = (int)(margin.Left * marginRatio),
                        Right = (int)(margin.Right * marginRatio),
                    };
                }
            }

            // Handle vertical size and overflow
            if (VerticalAlignment == VerticalAlignment.Stretch)
            {
                effectiveSize.Height = (int)float.Clamp(availableHeight, MinHeight, MaxHeight);
            }
            else if (totalHeight > containerBounds.Height)
            {
                // Handle vertical overflow
                if (availableHeight >= MinHeight)
                {
                    effectiveSize.Height = (int)float.Clamp(availableHeight, MinHeight, MaxHeight);
                }
                else
                {
                    var marginRatio = (float)containerBounds.Height / totalHeight;
                    effectiveMargin = effectiveMargin with
                    {
                        Top = (int)(margin.Top * marginRatio),
                        Bottom = (int)(margin.Bottom * marginRatio),
                    };
                }
            }

            // Calculate position using effective margins and size
            availableWidth = containerBounds.Width - effectiveSize.Width;
            availableHeight = containerBounds.Height - effectiveSize.Height;

            int x = containerBounds.X + HorizontalAlignment switch
            {
                HorizontalAlignment.Center => (availableWidth / 2) + effectiveMargin.Left,
                HorizontalAlignment.Right => availableWidth - effectiveMargin.Right,
                _ => effectiveMargin.Left,
            };

            int y = containerBounds.Y + VerticalAlignment switch
            {
                VerticalAlignment.Center => (availableHeight / 2) + effectiveMargin.Top,
                VerticalAlignment.Bottom => availableHeight - effectiveMargin.Bottom,
                _ => effectiveMargin.Top,
            };

            // Adjust for border
            x += BorderThickness.Left;
            y += BorderThickness.Top;

            Point location = new(x, y);
            ActualBounds = new(location, effectiveSize);

            InvalidateTransform();
            ArrangeContent();
            ArrangeUpdated?.Invoke(this, EventArgs.Empty);
            IsArrangeInvalid = false;
        }

        /// <summary>
        /// Calculates desired <see cref="UIElement"/> size.
        /// </summary>
        /// <returns>Size recommended to display the <see cref="UIElement"/> instance.</returns>
        public Size Measure()
        {
            if (IsMeasureInvalid)
            {
                Size s = MeasureContent();
                if (!float.IsNaN(Width))
                    s.Width = (int)Width;
                if (!float.IsNaN(Height))
                    s.Height = (int)Height;
                float resultWidth = float.Clamp(s.Width, MinWidth, MaxWidth);
                float resultHeight = float.Clamp(s.Height, MinHeight, MaxHeight);
                var internalThickness = Padding;
                resultWidth += internalThickness.Width;
                resultHeight += internalThickness.Height;
                DesiredSize = new((int)resultWidth, (int)resultHeight);
                IsMeasureInvalid = false;
            }

            return DesiredSize;
        }

        /// <summary>
        /// Invalidates <see cref="UIElement"/> arrange to recalculate on next draw call.
        /// </summary>
        public void InvalidateArrange()
        {
            IsArrangeInvalid = true;
            Parent?.InvalidateArrange();
        }

        /// <summary>
        /// Invalidates <see cref="UIElement"/> measure to recalculate on next draw call.
        /// </summary>
        public void InvalidateMeasure()
        {
            IsMeasureInvalid = true;
            Parent?.InvalidateMeasure();
            InvalidateArrange();
        }

        /// <summary>
        /// Invalidates <see cref="UIElement"/> transform to recalculate on next draw call.
        /// </summary>
        public void InvalidateTransform()
        {
            IsTransformInvalid = true;
        }

        /// <summary>
        /// Resets the transform to its default values.
        /// </summary>
        public void ResetTransform()
        {
            Scale = Vector2.One;
            Rotation = 0;
            RenderTransformOrigin = new(0.5f, 0.5f);
            InvalidateTransform();
        }

        /// <summary>
        /// Handles custom measure logic when overridden in any <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is called during the measure pass to determine the desired size of the element.
        /// The returned size should represent the natural size of the element's content, without considering
        /// any constraints, margins, or padding.
        /// </para>
        /// <para>
        /// When implementing this method:
        /// <list type="bullet">
        ///    <item>Return the natural size needed to display the content;</item>
        ///    <item>Do not include padding, margins, or borders in the returned size;</item>
        ///    <item>Consider the content's actual dimensions (e.g., text length, image size);</item>
        ///    <item>Return <see cref="Size.Empty"/> if the element has no content.</item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <returns>Size recommended to display the <see cref="UIElement"/>.</returns>
        protected virtual Size MeasureContent()
        {
            // Base implementation returns (0,0)
            return Size.Empty;
        }

        /// <summary>
        /// Handles custom arrange logic when overridden in any <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is called during the arrange pass to position and size the element's content
        /// within its final bounds. The element's position and size are already determined by the
        /// layout system and available in the <see cref="ActualBounds"/> property.
        /// </para>
        /// <para>
        /// When implementing this method:
        /// <list type="bullet">
        ///     <item>Position child elements within the available space;</item>
        ///     <item>Respect the element's padding when positioning content;</item>
        ///     <item>Handle any content-specific layout requirements;</item>
        ///     <item>Call <see cref="Arrange"/> on child elements if needed.</item>
        /// </list>
        /// </para>
        /// </remarks>
        protected virtual void ArrangeContent()
        {
            // Base implementation does nothing.
        }

        /// <summary>
        /// Calculates transform matrix and inverse matrix based on the current transform properties.
        /// </summary>
        protected void UpdateTransformMatrix()
        {
            Arrange();
            transform = Transform2D.Create(
                ActualBounds.Location.ToVector(),
                float.DegreesToRadians(Rotation),
                RenderTransformOrigin * new Vector2(ActualBounds.Width, ActualBounds.Height),
                Scale);
            if (Matrix3x2.Invert(transform.Matrix, out var inverseMatrix))
                inverseTransform = Transform2D.Create(inverseMatrix);
            IsTransformInvalid = false;
        }

        /// <summary>
        /// Gets a value for the specified dependency property of the current <see cref="UIElement"/> instance.
        /// </summary>
        /// <typeparam name="T">Requested value type (check for nullability).</typeparam>
        /// <param name="propertyName">Name of the dependency property to get value for.</param>
        /// <returns>
        /// A value of the specified dependency property of the current <see cref="UIElement"/> instance.
        /// If value is not set, the default value from the property metadata is used.
        /// </returns>
        protected T GetValue<T>([CallerMemberName][DisallowNull] string? propertyName = null!)
        {
            Guard.IsNotNull(propertyName);
            object? actualValue = this.GetValue(propertyName) ??
                                  this.GetDefaultValue(propertyName);
            return (T)(actualValue ?? default(T));
        }

        /// <summary>
        /// Sets a value to the specified dependency property of the current <see cref="UIElement"/> instance.
        /// </summary>
        /// <typeparam name="T">Type of the value to set.</typeparam>
        /// <param name="value">Value to set.</param>
        /// <param name="propertyName">Name of the dependency property to set value for.</param>
        protected void SetValue<T>(T value, [CallerMemberName][DisallowNull] string? propertyName = null!)
        {
            Guard.IsNotNull(propertyName);
            this.SetValue(propertyName, value);
        }

        /// <inheritdoc/>
        protected override void OnValueSet(IDependencyProperty property, object? oldValue, object? newValue)
        {
            if (property.Metadata is DependencyPropertyMetadata metadata)
            {
                if (metadata.AffectsTransform) InvalidateTransform();
                if (metadata.AffectsArrange) InvalidateArrange();
                if (metadata.AffectsMeasure) InvalidateMeasure();
                if (metadata.AffectsParentMeasure) Parent?.InvalidateMeasure();
            }

            base.OnValueSet(property, oldValue, newValue);
        }
    }
}