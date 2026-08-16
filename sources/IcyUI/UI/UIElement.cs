// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using CommunityToolkit.Diagnostics;
using Icy.Configuration;
using Icy.Data;
using Icy.Data.Bindings.Attributes;
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.UI.Styles;

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
    /// This class implements a property references system for efficient property
    /// change notification and value inheritance. It also provides a comprehensive
    /// event system for handling user input and state changes.
    /// </para>
    /// </remarks>
    public class UIElement : DependencyObject, INotifyFocusChanged
    {
        private readonly Dictionary<VisualStateGroup, VisualState?> activeStates = [];
        private readonly List<VisualStateGroup> stateGroups = [];
        private Rectangle actualBounds;
        private Canvas? canvas;
        private bool clipToBounds = true;
        private ControlState controlState;
        private bool isFocusable;
        private bool isFocused;
        private bool isFocusScope;
        private Size desiredSize;
        private Color foreground = Color.Black;
        private float height = float.NaN;
        private HorizontalAlignment horizontalAlignment = HorizontalAlignment.Stretch;
        private Transform2D inverseLayoutTransform;
        private bool isVisible = true;
        private float layerIndex;
        private Vector2 layoutOffset;
        private float layoutRotation;
        private Vector2 layoutScale = Vector2.One;
        private Transform2D layoutTransform;
        private Vector2 layoutTransformOrigin = new(0.5f, 0.5f);
        private Thickness margin;
        private float maxHeight = float.NaN;
        private float maxWidth = float.NaN;
        private float minHeight = float.NaN;
        private float minWidth = float.NaN;
        private string? name;
        private float opacity = 1;
        private Thickness padding;
        private UIElement? parent;
        private Vector2 renderOffset;
        private float renderRotation;
        private Vector2 renderScale = Vector2.One;
        private Transform2D renderTransform;
        private Vector2 renderTransformOrigin = new(0.5f, 0.5f);
        private Style? style;
        private UpdateFlags updateFlags = UpdateFlags.All;
        private VerticalAlignment verticalAlignment = VerticalAlignment.Stretch;
        private float width = float.NaN;

        /// <summary>
        /// Occurs when the <see cref="UIElement"/> arrange is updated.
        /// </summary>
        public event EventHandler? ArrangeUpdated;

        /// <summary>
        /// Occurs when the location of the <see cref="UIElement"/> is changed.
        /// </summary>
        public event EventHandler? LocationChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="Opacity"/> property of the <see cref="UIElement"/> is changed.
        /// </summary>
        public event EventHandler? OpacityChanged;

        /// <summary>
        /// Occurs when the <see cref="UIElement"/> size is updated.
        /// </summary>
        public event EventHandler? SizeChanged;

        /// <summary>
        /// Occurs when the <see cref="UIElement"/> transform is updated.
        /// </summary>
        public event EventHandler? TransformUpdated;

        /// <summary>
        /// Occurs when the value of the <see cref="IsVisible"/> property of the <see cref="UIElement"/> is changed.
        /// </summary>
        public event EventHandler? VisibilityChanged;

        /// <summary>
        /// Occurs when the <see cref="UIElement"/> is attached to the <see cref="UI.Canvas"/> instance.
        /// </summary>
        public event EventHandler? Attached;

        /// <summary>
        /// Occurs when the <see cref="UIElement"/> is detached from the <see cref="UI.Canvas"/> instance.
        /// </summary>
        public event EventHandler? Detached;

        /// <inheritdoc/>
        public event EventHandler? FocusChanged;

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
            /// The UI element rendering parameters need to be recalculated.
            /// </summary>
            Visual = 1 << 3,

            /// <summary>
            /// All of the layout properties should be recalculated before rendering.
            /// </summary>
            All = Measure | Arrange | Transform | Visual,
        }

        /// <summary>
        /// Gets or sets the actual bounds of the <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [RegisterReference]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public Rectangle ActualBounds
        {
            get => actualBounds;
            protected set
            {
                if (value.Location != actualBounds.Location)
                {
                    OnLocationChanged();
                }

                SetProperty(ref actualBounds, value);
            }
        }

        /// <summary>
        /// Gets or sets an instance of <see cref="UI.Canvas"/> that is used as the root for the hierarchy for this <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public Canvas? Canvas
        {
            get => canvas;
            protected internal set
            {
                var oldValue = canvas;
                if (SetProperty(ref canvas, value))
                {
                    if (oldValue != null) OnDetached();
                    if (value != null) OnAttached();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control contents should be clipped to the actual bounds when rendering.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(true)]
        [RegisterReference]
        public bool ClipToBounds { get => clipToBounds; set => SetProperty(ref clipToBounds, value); }

        /// <summary>
        /// Gets or sets the size the <see cref="UIElement"/> instance wants to be.
        /// </summary>
        [Category("Layout")]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public Size DesiredSize
        {
            get => desiredSize;
            protected set
            {
                if (SetProperty(ref desiredSize, value))
                {
                    OnSizeChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the foreground color of the <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Black")]
        [RegisterReference]
        public Color Foreground { get => foreground; set => SetProperty(ref foreground, value); }

        /// <summary>
        /// Gets or sets the height of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// If set to <see cref="float.NaN"/>, the <see cref="UIElement"/> instance will size to its content.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public float Height
        {
            get => height;
            set
            {
                if (!float.IsNaN(value))
                {
                    Guard.IsGreaterThanOrEqualTo(value, 0);
                }

                if (SetProperty(ref height, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the horizontal alignment of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// Determines how the <see cref="UIElement"/> instance is positioned horizontally within its parent.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(HorizontalAlignment.Stretch)]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public HorizontalAlignment HorizontalAlignment
        {
            get => horizontalAlignment;
            set
            {
                if (SetProperty(ref horizontalAlignment, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="UIElement"/> is attached to any <see cref="UI.Canvas"/> instance.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        [Browsable(false)]
        [MemberNotNullWhen(true, nameof(Configuration))]
        public bool IsAttached => Canvas != null;

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="UIElement"/> instance is visible.
        /// </summary>
        /// <remarks>
        /// When set to <see langword="false"/>, the <see cref="UIElement"/> instance and its children are not rendered.
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(true)]
        [RegisterReference]
        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (SetProperty(ref isVisible, value))
                {
                    OnVisibilityChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this element can receive focus.
        /// </summary>
        /// <remarks>
        /// Defaults to <see langword="false"/> — most elements (decorative ones like <see cref="Border"/> or
        /// <see cref="TextBlock"/>-like content) aren't focus targets. Interactive controls set this to
        /// <see langword="true"/>.
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool IsFocusable
        {
            get => isFocusable;
            set => SetProperty(ref isFocusable, value);
        }

        /// <inheritdoc/>
        [Category("Behavior")]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public bool IsFocused => isFocused;

        /// <summary>
        /// Gets or sets a value indicating whether this element is a focus scope — a boundary that keyboard/gamepad
        /// focus traversal (<c>Tab</c>, <c>FocusNext</c>/<c>FocusPrevious</c>) won't cross, and whose last-focused
        /// descendant is restored when focus returns to the scope from outside it.
        /// </summary>
        /// <remarks>
        /// Useful for modal dialogs (<c>Window</c>), dropdowns, and context menus — anything that should trap
        /// focus while it's open.
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool IsFocusScope
        {
            get => isFocusScope;
            set => SetProperty(ref isFocusScope, value);
        }

        /// <summary>
        /// Gets or sets the offset applied to the <see cref="UIElement"/> at layout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value represents pixels offset from the original layout location for the <see cref="UIElement"/> instance.
        /// </para>
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(typeof(Vector2), "0,0")]
        [RegisterReference]
        [AffectsTransform]
        public Vector2 LayoutOffset
        {
            get => layoutOffset;
            set
            {
                if (SetProperty(ref layoutOffset, value))
                {
                    OnLocationChanged();
                    InvalidateTransform();
                }
            }
        }

        /// <summary>
        /// Gets or sets the rotation of the <see cref="UIElement"/> instance in degrees.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Values are normalized to the range 0-360 degrees.
        /// </para>
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(0.0f)]
        [Range(0.0f, 360.0f)]
        [RegisterReference]
        [AffectsTransform]
        public float LayoutRotation
        {
            get => layoutRotation;
            set
            {
                value = (value + 360f) % 360f;
                if (SetProperty(ref layoutRotation, value))
                {
                    InvalidateTransform();
                }
            }
        }

        /// <summary>
        /// Gets or sets the scale of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A scale of (1,1) represents the original size.
        /// </para>
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(typeof(Vector2), "1,1")]
        [RegisterReference]
        [AffectsTransform]
        public Vector2 LayoutScale
        {
            get => layoutScale;
            set
            {
                if (SetProperty(ref layoutScale, value))
                {
                    InvalidateTransform();
                }
            }
        }

        /// <summary>
        /// Gets or sets the origin point for transforms applied to <see cref="UIElement"/> after layout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The origin is specified as a relative point, where (0,0) is the top-left
        /// and (1,1) is the bottom-right of the <see cref="UIElement"/> instance.
        /// </para>
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(typeof(Vector2), "0.5,0.5")]
        [RegisterReference]
        [AffectsTransform]
        public Vector2 LayoutTransformOrigin
        {
            get => layoutTransformOrigin;
            set
            {
                if (SetProperty(ref layoutTransformOrigin, value))
                {
                    InvalidateTransform();
                }
            }
        }

        /// <summary>
        /// Gets the logical parent of the <see cref="UIElement"/> instance.
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        [Browsable(false)]
        public IContainerLayout? LogicalParent => (IContainerLayout?)Parent ?? Canvas;

        /// <summary>
        /// Gets or sets the margin around the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// The margin is the space between this <see cref="UIElement"/> instance and its parent or adjacent <see cref="UIElement"/> instances.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public Thickness Margin
        {
            get => margin;
            set
            {
                if (SetProperty(ref margin, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximal <see cref="UIElement"/> height.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public float MaxHeight
        {
            get => maxHeight;
            set
            {
                if (!float.IsNaN(value))
                {
                    Guard.IsGreaterThanOrEqualTo(value, 0);
                }

                if (SetProperty(ref maxHeight, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximal <see cref="UIElement"/> width.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public float MaxWidth
        {
            get => maxWidth;
            set
            {
                if (!float.IsNaN(value))
                {
                    Guard.IsGreaterThanOrEqualTo(value, 0);
                }

                if (SetProperty(ref maxWidth, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimal <see cref="UIElement"/> height.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public float MinHeight
        {
            get => minHeight;
            set
            {
                if (!float.IsNaN(value))
                {
                    Guard.IsGreaterThanOrEqualTo(value, 0);
                }

                if (SetProperty(ref minHeight, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimal <see cref="UIElement"/> width.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public float MinWidth
        {
            get => minWidth;
            set
            {
                if (!float.IsNaN(value))
                {
                    Guard.IsGreaterThanOrEqualTo(value, 0);
                }

                if (SetProperty(ref minWidth, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the name of the <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Design")]
        [DefaultValue(null)]
        [NonBindable]
        [NonAnimatable]
        public string? Name { get => name; set => SetProperty(ref name, value); }

        /// <summary>
        /// Gets or sets the opacity of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// Values must be between 0.0 (fully transparent) and 1.0 (fully opaque).
        /// </remarks>
        [Category("Appearance")]
        [DefaultValue(1.0f)]
        [Range(0.0f, 1.0f)]
        [RegisterReference]
        public float Opacity
        {
            get => opacity;
            set
            {
                // Guard.IsInRange uses an exclusive upper bound ([min, max)); Opacity's valid range - and its own
                // [DefaultValue(1.0f)] - is inclusive at both ends ([0, 1]), so IsBetweenOrEqualTo is the correct guard.
                Guard.IsBetweenOrEqualTo(value, 0, 1);

                if (SetProperty(ref opacity, value))
                {
                    OnOpacityChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the padding of the <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public Thickness Padding
        {
            get => padding;
            set
            {
                if (SetProperty(ref padding, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets the parent of this <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Layout")]
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public UIElement? Parent { get => parent; internal set => SetProperty(ref parent, value); }

        /// <summary>
        /// Gets or sets the offset applied to the <see cref="UIElement"/> at rendering.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value represents pixels offset from the original rendering location for the <see cref="UIElement"/> instance.
        /// </para>
        /// <para>
        /// The <see cref="RenderOffset"/> is used only at rendering and does not affect real element layout.
        /// </para>
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(typeof(Vector2), "0,0")]
        [RegisterReference]
        [AffectsTransform]
        public Vector2 RenderOffset
        {
            get => renderOffset;
            set
            {
                if (SetProperty(ref renderOffset, value))
                {
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Gets or sets the rotation of the <see cref="UIElement"/> instance in degrees, applied to element while rendering.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Values are normalized to the range 0-360 degrees.
        /// </para>
        /// <para>
        /// The <see cref="RenderRotation"/> is used only at rendering and does not affect real element layout.
        /// </para>
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(0.0f)]
        [Range(0.0f, 360.0f)]
        [RegisterReference]
        [AffectsTransform]
        public float RenderRotation
        {
            get => renderRotation;
            set
            {
                value = (value + 360f) % 360f;

                if (SetProperty(ref renderRotation, value))
                {
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Gets or sets the rendering scale of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A scale of (1,1) represents the original size.
        /// </para>
        /// <para>
        /// The <see cref="RenderScale"/> is used only at rendering and does not affect real element layout.
        /// </para>
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(typeof(Vector2), "1,1")]
        [RegisterReference]
        [AffectsTransform]
        public Vector2 RenderScale
        {
            get => renderScale;
            set
            {
                if (SetProperty(ref renderScale, value))
                {
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Gets the actual render size of the component.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        [JsonIgnore]
        public Size RenderSize => new((int)(ActualBounds.Width * LayoutScale.X * RenderScale.X), (int)(ActualBounds.Height * LayoutScale.Y * RenderScale.Y));

        /// <summary>
        /// Gets or sets the origin point for transforms used at <see cref="UIElement"/> rendering.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The origin is specified as a relative point, where (0,0) is the top-left
        /// and (1,1) is the bottom-right of the <see cref="UIElement"/> instance.
        /// </para>
        /// <para>
        /// The <see cref="RenderTransformOrigin"/> is used only at rendering and does not affect real element layout.
        /// </para>
        /// </remarks>
        [Category("Transform")]
        [DefaultValue(typeof(Vector2), "0.5,0.5")]
        [RegisterReference]
        [AffectsTransform]
        public Vector2 RenderTransformOrigin
        {
            get => renderTransformOrigin;
            set
            {
                if (SetProperty(ref renderTransformOrigin, value))
                {
                    InvalidateVisual();
                }
            }
        }

        /// <summary>
        /// Gets or sets the style applied to the <see cref="UIElement"/> instance.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(null)]
        [RegisterReference]
        public Style? Style
        {
            get => style;
            set
            {
                Style? oldStyle = style;
                if (!SetProperty(ref style, value))
                    return;

                if (oldStyle != null)
                {
                    RevertStyle(oldStyle);
                }

                value?.Apply(this);
            }
        }

        /// <summary>
        /// Gets or sets the combination of <see cref="ControlState"/> flags currently active on this element.
        /// </summary>
        /// <remarks>
        /// Setting this re-evaluates every group registered via <see cref="RegisterStateGroup(VisualStateGroup)"/>,
        /// switching each group's active <see cref="VisualState"/> (if any) to whichever state's flags form the
        /// largest subset of the new value.
        /// </remarks>
        [Category("Appearance")]
        [DefaultValue(ControlState.Normal)]
        [RegisterReference]
        public ControlState ControlState
        {
            get => controlState;
            set
            {
                if (!SetProperty(ref controlState, value))
                    return;
                foreach (VisualStateGroup group in stateGroups)
                {
                    ApplyBestMatchingState(group);
                }
            }
        }

        /// <summary>
        /// Gets or sets the vertical alignment of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// Determines how the <see cref="UIElement"/> is positioned vertically within its parent.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(VerticalAlignment.Stretch)]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public VerticalAlignment VerticalAlignment
        {
            get => verticalAlignment;
            set
            {
                if (SetProperty(ref verticalAlignment, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the width of the <see cref="UIElement"/> instance.
        /// </summary>
        /// <remarks>
        /// If set to <see cref="float.NaN"/>, the <see cref="UIElement"/> will size to its content.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(float.NaN)]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public float Width
        {
            get => width;
            set
            {
                if (!float.IsNaN(value))
                {
                    Guard.IsGreaterThanOrEqualTo(value, 0);
                }

                if (SetProperty(ref width, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the Z-layer position of the <see cref="UIElement"/> instance. The less the number, the closer <see cref="UIElement"/> instance to the screen front.
        /// </summary>
        /// <remarks>
        /// Controls with the same Z-index are placed in visual tree order – those that are closer to root are farther from the screen.
        /// </remarks>
        [Category("Appearance")]
        [DefaultValue(0)]
        [RegisterReference]
        public float ZIndex
        {
            get => layerIndex;
            set
            {
                if (SetProperty(ref layerIndex, value))
                {
                    InvalidateVisual();
                }
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
        /// Gets or sets a value indicating whether the visual configuration of the <see cref="UIElement"/> is invalid.
        /// </summary>
        protected bool IsVisualInvalid
        {
            get => (LayoutInvalid & UpdateFlags.Visual) != 0;
            set
            {
                if (value)
                    LayoutInvalid |= UpdateFlags.Visual;
                else
                    LayoutInvalid &= ~UpdateFlags.Visual;
            }
        }

        /// <summary>
        /// Gets or sets the flags that indicate whether the <see cref="UIElement"/> instance should recalculate any of layout properties.
        /// </summary>
        protected UpdateFlags LayoutInvalid
        {
            get => updateFlags;
            set
            {
                if (SetProperty(ref updateFlags, value))
                    Parent?.InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets the library configuration for the <see cref="UI.Canvas"/> instance the <see cref="UIElement"/> is attached to.
        /// </summary>
        protected IcyConfiguration? Configuration => Canvas?.Configuration;

        /// <summary>
        /// Arranges the <see cref="UIElement"/> instance in specified container bounds.
        /// </summary>
        public void Arrange()
        {
            if (!IsArrangeInvalid) return;
            Rectangle containerBounds = LogicalParent?.ContentBounds ?? default;

            // Get current desired size and actual margin to calculate effective size and margin.
            var effectiveSize = Measure();
            var effectiveMargin = Margin;

            // Calculate effective margins and size (without modifying properties)
            CalculateOverflow(
                containerBounds,
                effectiveSize.Width + effectiveMargin.Width,
                effectiveSize.Height + effectiveMargin.Height,
                ref effectiveMargin,
                ref effectiveSize);

            // Calculate position using effective margins and size
            Point location = CalculateLocation(containerBounds, effectiveSize, effectiveMargin);

            ActualBounds = new(location, effectiveSize);

            InvalidateTransform();
            ArrangeContent();
            OnArrangeUpdated();
            IsArrangeInvalid = false;
        }

        /// <summary>
        /// Draws the <see cref="UIElement"/> instance using specified render context.
        /// </summary>
        /// <param name="context">The context to render <see cref="UIElement"/> with.</param>
        public void Draw(IRenderContext context)
        {
            if (!IsVisible || Opacity <= 0)
                return;

            // Apply opacity
            context.Options.Opacity *= Opacity;

            // Save current transform
            var oldTransform = context.Transform;

            // Apply UI element transform
            if (IsTransformInvalid) UpdateTransformMatrix();
            if (IsVisualInvalid) UpdateVisual();
            var newTransform = oldTransform;
            newTransform.AddTransform(layoutTransform);
            newTransform.AddTransform(renderTransform);
            context.Transform = newTransform;

            Rectangle oldScissor = context.Options.Scissor;

            if (ClipToBounds)
            {
                context.Options.Scissor = context.Options.Scissor.Cut(ActualBounds);
            }

            // Draw content
            OnRender(context);

            // Restore rendering context options.
            context.Transform = oldTransform;
            context.Options.Opacity /= Opacity;
            context.Options.Scissor = oldScissor;
        }

        /// <summary>
        /// Determines which element — this one, one of its descendants, or none — contains the specified point.
        /// </summary>
        /// <param name="pointInParentLocalSpace">
        /// A point expressed in this element's parent's own local (post-<see cref="Draw(IRenderContext)"/>-transform)
        /// space — the same space a child's <see cref="Draw(IRenderContext)"/> call receives via <c>context.Transform</c>
        /// once the parent's own layout/render transform has been applied.
        /// </param>
        /// <returns>The topmost hit-testable element containing the point, or <see langword="null"/> if none does.</returns>
        /// <remarks>
        /// Tests <see cref="GetVisualChildren"/> in reverse order (topmost/last-painted first — see
        /// <see cref="ZIndex"/>), so a child that visually overlaps and paints on top of a sibling is hit before
        /// that sibling. Only accounts for <see cref="LayoutOffset"/>/<see cref="LayoutRotation"/>/
        /// <see cref="LayoutScale"/> (the same transform that determines <see cref="ActualBounds"/>) —
        /// <see cref="RenderOffset"/>/<see cref="RenderRotation"/>/<see cref="RenderScale"/> are treated as purely
        /// cosmetic for v1 and don't affect where clicks land.
        /// </remarks>
        public UIElement? HitTest(Vector2 pointInParentLocalSpace)
        {
            if (!IsVisible || Opacity <= 0)
                return null;

            if (IsTransformInvalid)
                UpdateTransformMatrix();
            Vector2 localPoint = inverseLayoutTransform.Apply(pointInParentLocalSpace);

            bool withinBounds = localPoint.X >= 0 && localPoint.Y >= 0 &&
                localPoint.X <= ActualBounds.Width && localPoint.Y <= ActualBounds.Height;
            if (ClipToBounds && !withinBounds)
                return null;

            foreach (UIElement child in GetVisualChildren().Reverse())
            {
                UIElement? hit = child.HitTest(localPoint);
                if (hit != null)
                    return hit;
            }

            return withinBounds ? this : null;
        }

        /// <summary>
        /// Enumerates this element and its entire visual subtree (self first, then children depth-first, via
        /// <see cref="GetVisualChildren"/>), in paint order.
        /// </summary>
        /// <returns>This element followed by every descendant in the visual tree.</returns>
        public IEnumerable<UIElement> EnumerateVisualSubtree()
        {
            yield return this;
            foreach (UIElement child in GetVisualChildren())
            {
                foreach (UIElement descendant in child.EnumerateVisualSubtree())
                    yield return descendant;
            }
        }

        /// <summary>
        /// Enumerates this element's immediate visual children, in paint order (back-to-front), for hit-testing
        /// and tree traversal.
        /// </summary>
        /// <returns>The element's immediate children. The base implementation yields none (a leaf element).</returns>
        /// <remarks>
        /// Container elements (e.g. <see cref="Panel"/>, <see cref="Border"/>) override this to expose their
        /// children, in the exact order they're drawn in — <see cref="HitTest(Vector2)"/> depends on this matching
        /// actual paint order, or clicks will target the wrong, visually-obscured element.
        /// </remarks>
        protected virtual IEnumerable<UIElement> GetVisualChildren() => [];

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
        /// Invalidates visual configuration of the <see cref="UIElement"/> to recalculate it on next rendering.
        /// </summary>
        public void InvalidateVisual()
        {
            IsVisualInvalid = true;
        }

        /// <summary>
        /// Registers a visual-state group on this element, immediately applying whichever of its states best
        /// matches the current <see cref="ControlState"/>.
        /// </summary>
        /// <param name="group">The group to register.</param>
        public void RegisterStateGroup(VisualStateGroup group)
        {
            if (!stateGroups.Contains(group))
            {
                stateGroups.Add(group);
                ApplyBestMatchingState(group);
            }
        }

        /// <summary>
        /// Unregisters a visual-state group previously added via <see cref="RegisterStateGroup(VisualStateGroup)"/>,
        /// clearing whichever of its states is currently active.
        /// </summary>
        /// <param name="group">The group to unregister.</param>
        public void UnregisterStateGroup(VisualStateGroup group)
        {
            if (stateGroups.Remove(group) && activeStates.Remove(group, out VisualState? active))
            {
                ClearStateSetters(active);
            }
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
        /// Resets the transform to its default values.
        /// </summary>
        public void ResetTransform()
        {
            LayoutScale = Vector2.One;
            LayoutRotation = 0;
            LayoutTransformOrigin = new(0.5f, 0.5f);
            LayoutOffset = Vector2.Zero;
            InvalidateTransform();
        }

        /// <summary>
        /// Resets the rendering transform to its default values.
        /// </summary>
        public void ResetVisual()
        {
            RenderScale = Vector2.One;
            RenderRotation = 0;
            RenderTransformOrigin = new(0.5f, 0.5f);
            RenderOffset = Vector2.Zero;
            InvalidateVisual();
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
        /// Raises the <see cref="ArrangeUpdated"/> event.
        /// </summary>
        protected virtual void OnArrangeUpdated()
        {
            ArrangeUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="LocationChanged"/> event.
        /// </summary>
        protected virtual void OnLocationChanged()
        {
            LocationChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="OpacityChanged"/> event.
        /// </summary>
        protected virtual void OnOpacityChanged()
        {
            OpacityChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles custom render logic when overridden in any <see cref="UIElement"/> instance.
        /// </summary>
        /// <param name="context">The context to render with.</param>
        protected virtual void OnRender(IRenderContext context)
        {
            // Base implementation does nothing
        }

        /// <summary>
        /// Raises the <see cref="SizeChanged"/> event.
        /// </summary>
        protected virtual void OnSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="TransformUpdated"/> event.
        /// </summary>
        protected virtual void OnTransformUpdated()
        {
            TransformUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="VisibilityChanged"/> event.
        /// </summary>
        protected virtual void OnVisibilityChanged()
        {
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="Attached"/> event.
        /// </summary>
        protected virtual void OnAttached()
        {
            Attached?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="Detached"/> event.
        /// </summary>
        protected virtual void OnDetached()
        {
            Detached?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Recalculates the render transform based on the current <see cref="RenderOffset"/>, <see cref="RenderRotation"/>,
        /// <see cref="RenderTransformOrigin"/>, and <see cref="RenderScale"/> properties.
        /// </summary>
        protected void UpdateVisual()
        {
            renderTransform = Transform2D.Create(
                RenderOffset,
                float.DegreesToRadians(RenderRotation),
                RenderTransformOrigin * new Vector2(ActualBounds.Width, ActualBounds.Height),
                RenderScale);
        }

        /// <summary>
        /// Computes the default local-space <see cref="TextureRenderingOptions"/> for this <see cref="UIElement"/>
        /// instance, covering its full <see cref="ActualBounds"/> at full opacity/white tint, ready to hand to an <see cref="IBrush"/>.
        /// </summary>
        /// <returns>The default rendering options for this element's content area.</returns>
        protected TextureRenderingOptions GetDefaultRenderOptions() => new(
            Destination: new(Point.Empty, ActualBounds.Size),
            Source: null,
            Color: Color.White,
            Rotation: 0,
            Origin: Vector2.Zero,
            Depth: ZIndex);

        /// <summary>
        /// Calculates transform matrix and inverse matrix based on the current transform properties.
        /// </summary>
        protected void UpdateTransformMatrix()
        {
            if (IsTransformInvalid)
            {
                Arrange();

                // ActualBounds.Location is computed cumulatively (Arrange() positions this element within
                // LogicalParent.ContentBounds, which itself already carries the parent's own absolute position) -
                // but the render transform chain (Draw() composes each ancestor's layoutTransform/renderTransform
                // together) *also* accumulates translation hierarchically. Using the raw absolute ActualBounds.Location
                // here would double (or further multiply, for deeper nesting) every ancestor's contribution once per
                // level. Subtracting the parent's own content origin leaves only this element's own relative offset,
                // which is what the hierarchical transform chain expects to accumulate.
                Point parentContentOrigin = LogicalParent?.ContentBounds.Location ?? Point.Empty;
                Vector2 relativeLocation = new(ActualBounds.X - parentContentOrigin.X, ActualBounds.Y - parentContentOrigin.Y);

                layoutTransform = Transform2D.Create(
                    LayoutOffset + relativeLocation,
                    float.DegreesToRadians(LayoutRotation),
                    LayoutTransformOrigin * ActualBounds.Size.AsVector(),
                    LayoutScale);
                if (Matrix3x2.Invert(layoutTransform.Matrix, out var inverseMatrix))
                    inverseLayoutTransform = Transform2D.Create(inverseMatrix);
                OnTransformUpdated();
                IsTransformInvalid = false;
            }
        }

        /// <summary>
        /// Sets whether this element currently has focus, raising <see cref="FocusChanged"/> and updating
        /// <see cref="ControlState"/> if the value actually changes.
        /// </summary>
        /// <param name="value">Whether this element has focus.</param>
        /// <remarks>
        /// Only <see cref="Canvas"/>'s focus-management logic should call this — it's the single source of truth
        /// for which element is focused, so <see cref="IsFocused"/> stays consistent with it.
        /// </remarks>
        internal void SetFocused(bool value)
        {
            if (isFocused == value)
                return;
            isFocused = value;
            ControlState = value ? ControlState | ControlState.Focused : ControlState & ~ControlState.Focused;
            FocusChanged?.Invoke(this, EventArgs.Empty);
        }

        private Point CalculateLocation(Rectangle containerBounds, Size effectiveSize, Thickness effectiveMargin)
        {
            int availableWidth = containerBounds.Width - effectiveSize.Width;
            int availableHeight = containerBounds.Height - effectiveSize.Height;

            int x = containerBounds.X + HorizontalAlignment switch
            {
                HorizontalAlignment.Center | HorizontalAlignment.Stretch => (availableWidth / 2) + effectiveMargin.Left,
                HorizontalAlignment.Right => availableWidth - effectiveMargin.Right,
                _ => effectiveMargin.Left,
            };
            int y = containerBounds.Y + VerticalAlignment switch
            {
                VerticalAlignment.Center | VerticalAlignment.Stretch => (availableHeight / 2) + effectiveMargin.Top,
                VerticalAlignment.Bottom => availableHeight - effectiveMargin.Bottom,
                _ => effectiveMargin.Top,
            };

            return new(x, y);
        }

        private void CalculateOverflow(Rectangle containerBounds, int totalWidth, int totalHeight, ref Thickness effectiveMargin, ref Size effectiveSize)
        {
            int availableWidth = containerBounds.Width - effectiveMargin.Width;
            int availableHeight = containerBounds.Height - effectiveMargin.Height;

            // Handle horizontal size and overflow
            if (HorizontalAlignment == HorizontalAlignment.Stretch && float.IsNaN(Width))
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
            if (VerticalAlignment == VerticalAlignment.Stretch && float.IsNaN(Height))
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
        }

        private void RevertStyle(Style oldStyle)
        {
            IPropertyStore store = PropertyRegistry.Instance.GetPropertyStore(GetType());
            foreach (string propertyName in oldStyle.Setters.Keys)
            {
                if (store.TryGetProperty(propertyName, out IPropertyReference? property))
                {
                    property.ClearTierValue(this, PropertyValuePrecedence.Style);
                }
            }

            foreach (VisualStateGroup group in oldStyle.StateGroups)
            {
                UnregisterStateGroup(group);
            }
        }

        private void ApplyBestMatchingState(VisualStateGroup group)
        {
            VisualState? best = null;
            int bestBitCount = -1;
            foreach (VisualState candidate in group.States)
            {
                if ((ControlState & candidate.State) == candidate.State)
                {
                    int bitCount = System.Numerics.BitOperations.PopCount((uint)candidate.State);
                    if (bitCount > bestBitCount)
                    {
                        best = candidate;
                        bestBitCount = bitCount;
                    }
                }
            }

            activeStates.TryGetValue(group, out VisualState? previous);
            if (previous == best)
                return;

            ClearStateSetters(previous);
            activeStates[group] = best;
            ApplyStateSetters(best);
        }

        private void ApplyStateSetters(VisualState? state)
        {
            if (state == null)
                return;
            IPropertyStore store = PropertyRegistry.Instance.GetPropertyStore(GetType());
            foreach (KeyValuePair<string, object?> setter in state.Setters)
            {
                if (store.TryGetProperty(setter.Key, out IPropertyReference? property))
                {
                    property.SetTierValue(this, PropertyValuePrecedence.VisualState, setter.Value);
                }
            }
        }

        private void ClearStateSetters(VisualState? state)
        {
            if (state == null)
                return;
            IPropertyStore store = PropertyRegistry.Instance.GetPropertyStore(GetType());
            foreach (string propertyName in state.Setters.Keys)
            {
                if (store.TryGetProperty(propertyName, out IPropertyReference? property))
                {
                    property.ClearTierValue(this, PropertyValuePrecedence.VisualState);
                }
            }
        }
    }
}