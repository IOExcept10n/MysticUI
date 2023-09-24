// Code here is based on Myra's Widget.cs file: https://github.com/rds1983/Myra
using FontStashSharp;
using MysticUI.Brushes;
using MysticUI.Extensions;
using Newtonsoft.Json;
using Stride.Core.Mathematics;
using Stride.Input;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// A base class for all controls which are used in the UI system.
    /// </summary>
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class Control : UIElement, ITransformable, IBindingHandler, INotifyFocusChanged, INotifyPropertyChanged
    {
        /// <summary>
        /// Determines the default size of the font in pixels.
        /// </summary>
        public const int DefaultFontSize = 24;

        /// <summary>
        /// Default controls background, used to detect if the control has background unset.
        /// </summary>
        protected internal static SolidColorBrush DefaultBackground { get; } = new(Color.Transparent);

        #region Fields

        private readonly List<Binding> bindings = new();
        private int x;
        private int y;
        private int minWidth;
        private int maxWidth;
        private int minHeight;
        private int maxHeight;
        private int width;
        private int height;
        private Size2 sizeConstraints;
        private Thickness margin;
        private Thickness padding;
        private Thickness borderThickness = new(1);
        private HorizontalAlignment horizontalAlignment;
        private VerticalAlignment verticalAlignment;
        private int gridColumn;
        private int gridRow;
        private int gridColumnSpan = 1;
        private int gridRowSpan = 1;
        private bool enabled = true;
        private bool visible = true;
        private int zIndex;
        private Vector2 scale = Vector2.One;
        private Vector2 transformOrigin = new(0.5f, 0.5f);
        private float rotation;
        private Desktop? desktop;
        private bool isModal;
        private bool isActive;
        private float opacity = 1;
        private bool isMouseOver;
        private Transform? transform;
        private bool isInverseMatrixDirty;
        private bool isMeasureDirty;
        private Point lastMeasureSize;
        private Point lastMeasureResult;
        private bool isArrangeDirty;
        private Point? startPosition;
        private Point startLocation;
        private Matrix inverseMatrix;
        private Control? parent;
        private IBrush background;
        private IBrush? mouseOverBackground;
        private IBrush? disabledBackground;
        private IBrush? focusedBackground;
        private IBrush? touchingBackground;
        private IBrush? border;
        private IBrush? mouseOverBorder;
        private IBrush? disabledBorder;
        private IBrush? focusedBorder;
        private Color backgroundColor = Color.White;
        private Color? foregroundColor = Color.Black;
        private Color? disabledForegroundColor = Color.Black;
        private Color? activeForegroundColor = Color.Black;
        private Color? pressedForegroundColor = Color.Black;
        private SpriteFontBase? font;
        private string? fontFamily;
        private int fontSize = DefaultFontSize;
        private string styleName;

        #endregion Fields

        /// <summary>
        /// A property to quick access current environment settings.
        /// </summary>
        protected internal static IEnvironmentSettings EnvironmentSettings => EnvironmentSettingsProvider.EnvironmentSettings;

        /// <summary>
        /// Gets the time since the last frame rendering Needed for the animations and more features.
        /// </summary>
        protected internal static TimeSpan DeltaTime => EnvironmentSettings.Game.DrawTime.Elapsed;

        /// <summary>
        /// Name of the style that is used in this control.
        /// </summary>
        [Category("Integration")]
        public string StyleName
        {
            get => styleName;
            set
            {
                if (styleName == value) return;
                styleName = value;
                if (!string.IsNullOrEmpty(styleName) && Stylesheet.Default.ContainsKey(styleName)) SetStyle(styleName);
                else if (Stylesheet.Default.ContainsKey(GetType().Name + "Style")) this.WithDefaultStyle();
                else if (Stylesheet.Default.ContainsKey("ControlStyle")) SetStyle("ControlStyle");
                NotifyPropertyChanged(nameof(StyleName));
            }
        }

        /// <summary>
        /// X-coordinate of the left control side.
        /// </summary>
        [Category("Layout")]
        public int X
        {
            get => x;
            set
            {
                if (x == value) return;
                x = value;
                InvalidateTransform();
                FireLocationChanged();
                NotifyPropertyChanged(nameof(X));
            }
        }

        /// <summary>
        /// Y-coordinate of the top control side.
        /// </summary>
        [Category("Layout")]
        public int Y
        {
            get => y;
            set
            {
                if (y == value) return;
                y = value;
                InvalidateTransform();
                FireLocationChanged();
                NotifyPropertyChanged(nameof(Y));
            }
        }

        /// <summary>
        /// A point with location of the control.
        /// </summary>
        [Category("Layout")]
        public Point Location
        {
            get => new(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
                NotifyPropertyChanged(nameof(Location));
            }
        }

        /// <summary>
        /// Minimal allowed control width.
        /// </summary>
        [Category("Layout")]
        public int MinWidth
        {
            get => minWidth;
            set
            {
                if (minWidth == value) return;
                minWidth = value;
                InvalidateMeasure();
                FireSizeChanged();
                NotifyPropertyChanged(nameof(MinWidth));
            }
        }

        /// <summary>
        /// Maximal allowed control width.
        /// </summary>
        [Category("Layout")]
        public int MaxWidth
        {
            get => maxWidth;
            set
            {
                if (maxWidth == value) return;
                maxWidth = value;
                InvalidateMeasure();
                FireSizeChanged();
                NotifyPropertyChanged(nameof(MaxWidth));
            }
        }

        /// <summary>
        /// Minimal allowed control height.
        /// </summary>
        [Category("Layout")]
        public int MinHeight
        {
            get => minHeight;
            set
            {
                if (minHeight == value) return;
                minHeight = value;
                InvalidateMeasure();
                FireSizeChanged();
                NotifyPropertyChanged(nameof(MinHeight));
            }
        }

        /// <summary>
        /// Maximal allowed control height.
        /// </summary>
        [Category("Layout")]
        public int MaxHeight
        {
            get => maxHeight;
            set
            {
                if (maxHeight == value) return;
                maxHeight = value;
                InvalidateMeasure();
                FireSizeChanged();
                NotifyPropertyChanged(nameof(MaxHeight));
            }
        }

        /// <summary>
        /// Defined control width.
        /// </summary>
        [Category("Layout")]
        public int Width
        {
            get => width;
            set
            {
                if (width == value) return;
                width = sizeConstraints.Width = value;
                InvalidateMeasure();
                FireSizeChanged();
                NotifyPropertyChanged(nameof(Width));
            }
        }

        /// <summary>
        /// Defined control height.
        /// </summary>
        [Category("Layout")]
        public int Height
        {
            get => height;
            set
            {
                if (height == value) return;
                height = sizeConstraints.Height = value;
                InvalidateMeasure();
                FireSizeChanged();
                NotifyPropertyChanged(nameof(Height));
            }
        }

        /// <summary>
        /// Gets the constraints of the actual size computations set by user.
        /// </summary>
        /// <remarks>
        /// These values can be set by manual set of <see cref="Height"/> or <see cref="Width"/> properties.
        /// </remarks>
        /// <value>
        /// Used to calculate artificial target size of the control.
        /// </value>
        [XmlIgnore, Browsable(false)]
        internal protected Size2 SizeConstraints
        {
            get => sizeConstraints;
        }

        /// <summary>
        /// Control padding.
        /// </summary>
        [Category("Layout")]
        public Thickness Padding
        {
            get => padding;
            set
            {
                if (padding == value) return;
                padding = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(Padding));
            }
        }

        /// <summary>
        /// Control margin.
        /// </summary>
        [Category("Layout")]
        public Thickness Margin
        {
            get => margin;
            set
            {
                if (margin == value) return;
                margin = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(Margin));
            }
        }

        /// <summary>
        /// Control border thickness.
        /// </summary>
        [Category("Layout")]
        public Thickness BorderThickness
        {
            get => borderThickness;
            set
            {
                if (borderThickness == value) return;
                borderThickness = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(BorderThickness));
            }
        }

        /// <summary>
        /// Horizontal layout alignment.
        /// </summary>
        [Category("Layout")]
        public virtual HorizontalAlignment HorizontalAlignment
        {
            get => horizontalAlignment;
            set
            {
                if (horizontalAlignment == value) return;
                horizontalAlignment = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(HorizontalAlignment));
            }
        }

        /// <summary>
        /// Vertical layout alignment.
        /// </summary>
        [Category("Layout")]
        public virtual VerticalAlignment VerticalAlignment
        {
            get => verticalAlignment;
            set
            {
                if (verticalAlignment == value) return;
                verticalAlignment = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(VerticalAlignment));
            }
        }

        /// <summary>
        /// Index of the grid column for the control.
        /// </summary>
        [Category("Layout")]
        public int GridColumn
        {
            get => gridColumn;
            set
            {
                if (gridColumn == value)
                    return;
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                gridColumn = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(GridColumn));
            }
        }

        /// <summary>
        /// Index of the grid row for the control.
        /// </summary>
        [Category("Layout")]
        public int GridRow
        {
            get => gridRow;
            set
            {
                if (gridRow == value)
                    return;
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                gridRow = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(GridRow));
            }
        }

        /// <summary>
        /// Gird columns span amount.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(1)]
        public int GridColumnSpan
        {
            get => gridColumnSpan;
            set
            {
                if (gridColumnSpan == value)
                    return;
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                gridColumnSpan = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(GridColumnSpan));
            }
        }

        /// <summary>
        /// Grid rows span amount.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(1)]
        public int GridRowSpan
        {
            get => gridRowSpan;
            set
            {
                if (gridRowSpan == value)
                    return;
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                gridRowSpan = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(GridRowSpan));
            }
        }

        /// <summary>
        /// Determines if the control is enabled.
        /// </summary>
        [Category("Behavior")]
        public virtual bool Enabled
        {
            get
            {
                return enabled && ProcessParentRecursive(x => x?.Enabled != false);
            }
            set
            {
                if (enabled == value) return;
                enabled = value;
                EnabledChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(Enabled));
            }
        }

        /// <summary>
        /// Determines if the control is visible.
        /// </summary>
        [Category("Behavior")]
        public virtual bool Visible
        {
            get => visible;
            set
            {
                if (visible == value) return;
                visible = value;

                if (!visible)
                {
                    IsMouseOver = false;
                    IsTouching = false;
                }
                OnVisibilityChanged();
                NotifyPropertyChanged(nameof(Visible));
            }
        }

        /// <summary>
        /// Allowed drag directions.
        /// </summary>
        [Category("Behavior")]
        public virtual DragDirection DragDirection { get; set; }

        /// <summary>
        /// Determines if the control can be dragged.
        /// </summary>
        [Browsable(false), XmlIgnore, JsonIgnore]
        internal bool CanDrag => DragDirection != DragDirection.None;

        /// <summary>
        /// A control that handles the dragging process.
        /// <!-- I'm not sure why is it actually needed -->
        /// </summary>
        public Control DragHandle { get; set; }

        /// <summary>
        /// Z-index of the control.
        /// </summary>
        [Category("Layout")]
        public int ZIndex
        {
            get => zIndex;
            set
            {
                if (zIndex == value) return;
                zIndex = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(ZIndex));
            }
        }

        /// <summary>
        /// Scale of the control.
        /// </summary>
        [Category("Transform")]
        [DefaultValue("{1; 1}")]
        public Vector2 Scale
        {
            get => scale;
            set
            {
                if (scale == value) return;
                scale = value;
                InvalidateTransform();
                NotifyPropertyChanged(nameof(Scale));
            }
        }

        /// <summary>
        /// Transform origin point.
        /// </summary>
        [Category("Transform")]
        [DefaultValue("{0.5;0.5}")]
        public Vector2 TransformOrigin
        {
            get => transformOrigin;
            set
            {
                if (transformOrigin == value) return;
                transformOrigin = value;
                InvalidateTransform();
                NotifyPropertyChanged(nameof(TransformOrigin));
            }
        }

        /// <summary>
        /// Rotation by Z-axis for the control.
        /// </summary>
        [Category("Transform")]
        public float Rotation
        {
            get => rotation;
            set
            {
                if (rotation == value) return;
                rotation = value;
                InvalidateTransform();
                NotifyPropertyChanged(nameof(Rotation));
            }
        }

        /// <summary>
        /// Gets or sets the font to render text for the control if it's available.
        /// </summary>
        [Category("Layout")]
        public SpriteFontBase? Font
        {
            get => font;
            set
            {
                font = value;
                InvalidateMeasure();
                OnFontChanged();
                NotifyPropertyChanged(nameof(Font));
            }
        }

        /// <summary>
        /// Name or path to the file of the current font. If the font was set manually, this property may remain empty.
        /// </summary>
        public string? FontFamily
        {
            get => fontFamily;
            set
            {
                if (fontFamily == value) return;
                fontFamily = value;
                ResetFont();
                NotifyPropertyChanged(nameof(FontFamily));
            }
        }

        /// <summary>
        /// Size of the current font.
        /// </summary>
        [DefaultValue(DefaultFontSize)]
        public int FontSize
        {
            get => fontSize;
            set
            {
                if (fontSize == value) return;
                fontSize = value;
                ResetFont();
                NotifyPropertyChanged(nameof(FontSize));
            }
        }

        /// <summary>
        /// Determines if the control is placed into a desktop.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public bool IsPlaced => Desktop != null;

        /// <summary>
        /// Desktop on which the control is placed.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public virtual Desktop? Desktop
        {
            get => desktop;
            set
            {
                if (desktop != null && value == null)
                {
                    if (desktop.CurrentFocusedControl == this)
                    {
                        desktop.CurrentFocusedControl = null;
                    }

                    if (desktop.CurrentMouseOverControl == this)
                    {
                        desktop.CurrentMouseOverControl = null;
                    }
                }

                desktop = value;
                IsMouseOver = false;
                IsTouching = false;

                if (desktop != null)
                {
                    InvalidateMeasure();
                }

                SubscribeTouchMoved(IsPlaced && CanDrag);
                OnDesktopPlacedChanged();
                if (Desktop != null) ResetDataContext();
                NotifyPropertyChanged(nameof(Desktop));
            }
        }

        /// <summary>
        /// Parent to which this control is bounded.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Control? Parent
        {
            get => parent;
            set
            {
                parent = value;
                NotifyPropertyChanged(nameof(Parent));
            }
        }

        /// <summary>
        /// Determines whether this control is the root of the local control hierarchy.
        /// </summary>
        public bool IsRooted => Parent == null;

        /// <summary>
        /// Determines whether this control is modal.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public bool IsModal
        {
            get => isModal;
            set
            {
                if (isModal == value) return;
                isModal = value;
                InvalidateMeasure();
                NotifyPropertyChanged(nameof(IsModal));
            }
        }

        /// <summary>
        /// Determines whether this control is active (has mouse pointer focus).
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public bool IsActive
        {
            get => isActive;
            set
            {
                if (isActive == value) return;
                isActive = value;
                OnActiveChanged();
                NotifyPropertyChanged(nameof(IsActive));
            }
        }

        /// <summary>
        /// Opacity for the control.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(1)]
        [Range(0, 1)]
        public float Opacity
        {
            get => opacity;
            set
            {
                if (value > 1 || value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                opacity = value;
                NotifyPropertyChanged(nameof(Opacity));
            }
        }

        /// <summary>
        /// Custom layout calculation expression.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Layout2D LayoutExpression = Layout2D.Default;

        /// <summary>
        /// Default background brush.
        /// </summary>
        [Category("Appearance")]
        public IBrush Background
        {
            get => background;
            set
            {
                background = value;
                NotifyPropertyChanged(nameof(Background));
            }
        }

        /// <summary>
        /// Background brush for the mouse over state.
        /// </summary>
        [Category("Appearance")]
        public IBrush? MouseOverBackground
        {
            get => mouseOverBackground;
            set
            {
                mouseOverBackground = value;
                NotifyPropertyChanged(nameof(MouseOverBackground));
            }
        }

        /// <summary>
        /// Background brush for disabled state.
        /// </summary>
        [Category("Appearance")]
        public IBrush? DisabledBackground
        {
            get => disabledBackground;
            set
            {
                disabledBackground = value;
                NotifyPropertyChanged(nameof(DisabledBackground));
            }
        }

        /// <summary>
        /// Background brush for the focused state.
        /// </summary>
        [Category("Appearance")]
        public IBrush? FocusedBackground
        {
            get => focusedBackground;
            set
            {
                focusedBackground = value;
                NotifyPropertyChanged(nameof(FocusedBackground));
            }
        }

        /// <summary>
        /// Background brush for the touching state.
        /// </summary>
        [Category("Appearance")]
        public IBrush? TouchingBackground
        {
            get => touchingBackground;
            set
            {
                touchingBackground = value;
                NotifyPropertyChanged(nameof(TouchingBackground));
            }
        }

        /// <summary>
        /// Border brush.
        /// </summary>
        [Category("Appearance")]
        public IBrush? Border
        {
            get => border;
            set
            {
                border = value;
                NotifyPropertyChanged(nameof(Border));
            }
        }

        /// <summary>
        /// Border brush for the mouse over state.
        /// </summary>
        [Category("Appearance")]
        public IBrush? MouseOverBorder
        {
            get => mouseOverBorder;
            set
            {
                mouseOverBorder = value;
                NotifyPropertyChanged(nameof(MouseOverBorder));
            }
        }

        /// <summary>
        /// Border brush for the disabled state.
        /// </summary>
        [Category("Appearance")]
        public IBrush? DisabledBorder
        {
            get => disabledBorder;
            set
            {
                disabledBorder = value;
                NotifyPropertyChanged(nameof(DisabledBorder));
            }
        }

        /// <summary>
        /// Border brush for the focused state.
        /// </summary>
        [Category("Appearance")]
        public IBrush? FocusedBorder
        {
            get => focusedBorder;
            set
            {
                focusedBorder = value;
                NotifyPropertyChanged(nameof(FocusedBorder));
            }
        }

        /// <summary>
        /// Background applied color.
        /// </summary>
        [Category("Appearance")]
        public Color BackgroundColor
        {
            get => backgroundColor;
            set
            {
                backgroundColor = value;
                NotifyPropertyChanged(nameof(BackgroundColor));
            }
        }

        /// <summary>
        /// Foreground applied color.
        /// </summary>
        [Category("Appearance")]
        public virtual Color ForegroundColor
        {
            get => foregroundColor ?? Color.Black;
            set
            {
                foregroundColor = value;
                NotifyPropertyChanged(nameof(ForegroundColor));
            }
        }

        /// <summary>
        /// Determines whether the control should clip child elements to its bounds.
        /// </summary>
        [Category("Behavior")]
        public virtual bool ClipToBounds { get; set; }

        /// <summary>
        /// Determines whether the mouse pointer is over the control.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public bool IsMouseOver
        {
            get => isMouseOver;
            set
            {
                if (value && isMouseOver)
                {
                    if (Desktop != null)
                    {
                        Desktop.CurrentMouseOverControl = this;
                    }

                    OnMouseMove();
                }
                else if (value && !isMouseOver)
                {
                    if (Desktop != null)
                    {
                        Desktop.CurrentMouseOverControl = this;
                    }

                    OnMouseEntered();
                }
                else if (!value && isMouseOver)
                {
                    if (Desktop != null && Desktop.CurrentMouseOverControl == this)
                    {
                        Desktop.CurrentMouseOverControl = null;
                    }

                    OnMouseLeft();
                }

                isMouseOver = value;
                NotifyPropertyChanged(nameof(IsMouseOver));
            }
        }

        /// <summary>
        /// Determines whether the mouse on the control is touching down.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public bool IsTouching { get; private set; }

        /// <summary>
        /// Detects whether the control contains mouse position.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        internal bool ContainsMouse => Desktop != null && ContainsPoint(Desktop.MousePosition);

        /// <summary>
        /// Detects whether the control contains touching position even if it's not touching down.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public bool ContainsTouch => Desktop != null && ContainsPoint(Desktop.TouchPosition);

        /// <summary>
        /// Grouping tag for the control.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public object? Tag { get; set; }

        /// <summary>
        /// The bounds of the control without control position.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Rectangle ZeroBounds => new(0, 0, Width, Height);

        /// <summary>
        /// The bounds of the control, including position, width and height.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Rectangle Bounds
        {
            get
            {
                return new(X, Y, Width, Height);
            }
            set
            {
                X = value.X;
                Y = value.Y;
                width = value.Width;
                height = value.Height;
                NotifyPropertyChanged(nameof(Width));
                NotifyPropertyChanged(nameof(Height));
            }
        }

        /// <summary>
        /// Actual internal size of the element without paddings, margins etc.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Rectangle ActualBounds => ZeroBounds - Margin - BorderThickness - Padding;

        /// <summary>
        /// Bounds for the border.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Rectangle BorderBounds => ZeroBounds - Margin;

        /// <summary>
        /// Internal size without padding.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Rectangle InnerBounds => ZeroBounds - Margin - BorderThickness;

        /// <summary>
        /// Bounds of the parent container.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public Rectangle ContainerBounds { get; private set; }

        /// <summary>
        /// Width of all indentations including <see cref="Margin"/>, <see cref="Padding"/> and <see cref="Border"/>
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        protected internal int IndentationWidth => Margin.Width + BorderThickness.Width + Padding.Width;

        /// <summary>
        /// Height of all indentations including <see cref="Margin"/>, <see cref="Padding"/> and <see cref="Border"/>
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        protected internal int IndentationHeight => Margin.Height + BorderThickness.Height + Padding.Height;

        /// <summary>
        /// Determines whether the control accepts keyboard focus (using Tab key).
        /// </summary>
        public bool AcceptFocus { get; set; }

        /// <summary>
        /// Determines whether the control handles text input.
        /// </summary>
        public bool AcceptsTextInput { get; set; }

        /// <summary>
        /// Determines whether the control captures the mouse roll event when the mouse inside it.
        /// </summary>
        public bool CaptureMouseRoll { get; set; }

        /// <inheritdoc/>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public bool HasFocus { get; private set; }

        /// <summary>
        /// Transformation component for the control.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        internal Transform Transform
        {
            get
            {
                if (transform == null)
                {
                    var p = new Vector2(ZeroBounds.X + x, ZeroBounds.Y + y);

                    var localTransform = Transform.Create2DTransform(p,
                        TransformOrigin * new Vector2(Bounds.Width, Bounds.Height),
                        Scale,
                        Rotation * (float)Math.PI / 180);

                    if (Parent != null)
                    {
                        var transform = Parent.Transform;
                        transform.AddTransform(ref localTransform);
                        this.transform = transform;
                    }
                    else if (Desktop != null)
                    {
                        var transform = Desktop.Transform;
                        transform.AddTransform(ref localTransform);
                        this.transform = transform;
                    }
                    else
                    {
                        transform = localTransform;
                    }
                }

                return transform.Value;
            }
        }

        /// <summary>
        /// Gets a value that indicates if the control support visual interaction display.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(true)]
        public virtual bool IsVisualInteractive { get; set; } = true;

        /// <summary>
        /// Returns current interaction state for the control.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        protected internal ControlInteractionState InteractionState
        {
            get
            {
                if (!IsVisualInteractive) return ControlInteractionState.Default;
                if (!Enabled) return ControlInteractionState.Disabled;
                if (IsTouching && IsMouseOver && IsActive) return ControlInteractionState.Clicking;
                if (IsMouseOver && IsActive) return ControlInteractionState.MouseOver;
                if (HasFocus) return ControlInteractionState.Focused;
                return ControlInteractionState.Default;
            }
        }

        /// <summary>
        /// All bindings set to this target.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public IReadOnlyCollection<Binding> Bindings => bindings;

        /// <summary>
        /// Layout data context that will be used in binding calculations.
        /// </summary>
        protected internal object LayoutDataContext => GetDataContext();

        /// <summary>
        /// Gets or sets foreground color for the disabled control state.
        /// </summary>
        [Category("Appearance")]
        public Color DisabledForegroundColor
        {
            get => disabledForegroundColor ?? Color.Black;
            set
            {
                if (value == disabledForegroundColor)
                    return;
                disabledForegroundColor = value;
                NotifyPropertyChanged(nameof(DisabledForegroundColor));
            }
        }

        /// <summary>
        /// Gets or sets foreground color for the active (mouse over) control state.
        /// </summary>
        [Category("Appearance")]
        public Color ActiveForegroundColor
        {
            get => activeForegroundColor ?? Color.Black;
            set
            {
                if (value == activeForegroundColor)
                    return;
                activeForegroundColor = value;
                NotifyPropertyChanged(nameof(ActiveForegroundColor));
            }
        }

        /// <summary>
        /// Gets or sets foreground color for the pressed control state.
        /// </summary>
        [Category("Appearance")]
        public Color PressedForegroundColor
        {
            get => pressedForegroundColor ?? Color.Black;
            set
            {
                if (value == pressedForegroundColor)
                    return;
                pressedForegroundColor = value;
                NotifyPropertyChanged(nameof(PressedForegroundColor));
            }
        }

        /// <summary>
        /// Gets or sets the value indicating if the control accepts tab focus change
        /// or it should handle tab as the input key.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool AcceptsTab { get; set; } = true;

        /// <summary>
        /// Occurs when the value of the <see cref="Visible"/> property changes.
        /// </summary>
        public event EventHandler? VisibilityChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="Enabled"/> property changed.
        /// </summary>
        public event EventHandler? EnabledChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="IsActive"/> property changed.
        /// </summary>
        public event EventHandler? ActiveChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="IsPlaced"/> property changed.
        /// </summary>
        public event EventHandler? DesktopPlacedChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="ZeroBounds"/> property changed.
        /// </summary>
        public event EventHandler? LocationChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="Width"/> or <see cref="Height"/> properties changed.
        /// </summary>
        public event EventHandler? SizeChanged;

        /// <summary>
        /// Occurs when the control arrange is updated.
        /// </summary>
        public event EventHandler? ArrangeUpdated;

        /// <summary>
        /// Occurs when the value of the <see cref="Font"/> property changes.
        /// </summary>
        public event EventHandler? FontChanged;

        /// <summary>
        /// Occurs when the mouse pointer enters the control.
        /// </summary>
        public event EventHandler? MouseEnter;

        /// <summary>
        /// Occurs when the mouse pointer is moved while being over the control.
        /// </summary>
        public event EventHandler? MouseMove;

        /// <summary>
        /// Occurs when the mouse pointer is leaving the control bounds.
        /// </summary>
        public event EventHandler? MouseLeft;

        /// <summary>
        /// Occurs when the mouse pointer left the control bounds while pressing the left mouse button.
        /// </summary>
        public event EventHandler? TouchLeft;

        /// <summary>
        /// Occurs when the mouse pointer entered the control bounds while pressing left button.
        /// </summary>
        public event EventHandler? TouchEntered;

        /// <summary>
        /// Occurs when the mouse pointer is moving over the control bounds while pressing left button.
        /// </summary>
        public event EventHandler? TouchMove;

        /// <summary>
        /// Occurs when the mouse is started clicking while pointer is over the control.
        /// </summary>
        public event EventHandler? TouchDown;

        /// <summary>
        /// Occurs when the mouse is ended clicking while pointer is over the control.
        /// </summary>
        public event EventHandler? TouchUp;

        /// <summary>
        /// Occurs when the mouse has double clicked the control.
        /// </summary>
        public event EventHandler? DoubleClick;

        /// <summary>
        /// Occurs when the mouse has clicked right button while being over the control.
        /// </summary>
        public event EventHandler? RightClick;

        /// <inheritdoc/>
        public event EventHandler? FocusChanged;

        /// <summary>
        /// Occurs when the mouse wheel value changed.
        /// </summary>
        public event EventHandler<GenericEventArgs<float>>? MouseWheelRoll;

        /// <summary>
        /// Occurs when the keyboard key ended pressing in the control.
        /// </summary>
        public event EventHandler<GenericEventArgs<Keys>>? KeyUp;

        /// <summary>
        /// Occurs when the keyboard key started pressing in the control.
        /// </summary>
        public event EventHandler<GenericEventArgs<Keys>>? KeyDown;

        /// <summary>
        /// Occurs when user entered any character key.
        /// </summary>
        public event EventHandler<TextInputEvent>? TextInput;

        /// <summary>
        /// Actions to handle events before rendering process.
        /// </summary>
        public event Action<RenderContext> BeforeRender = delegate { };

        /// <summary>
        /// Actions to handle events after rendering process.
        /// </summary>
        public event Action<RenderContext> AfterRender = delegate { };

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates new instance of the <see cref="Control"/> class.
        /// </summary>
        public Control()
        {
            styleName = "";
            DragHandle = this;
            background = DefaultBackground;
        }

        /// <summary>
        /// Gets the current background which is needed for the current state.
        /// </summary>
        protected internal virtual IBrush? GetCurrentBackground() => InteractionState switch
        {
            ControlInteractionState.Disabled => DisabledBackground,
            ControlInteractionState.Focused => FocusedBackground,
            ControlInteractionState.MouseOver => MouseOverBackground,
            ControlInteractionState.Clicking => TouchingBackground ?? MouseOverBackground,
            _ => Background,
        } ?? Background;

        /// <summary>
        /// Gets the current border brush which is needed for the current state.
        /// </summary>
        protected internal virtual IBrush? GetCurrentBorder() => InteractionState switch
        {
            ControlInteractionState.Disabled => DisabledBorder,
            ControlInteractionState.Focused => FocusedBorder,
            ControlInteractionState.MouseOver => MouseOverBorder,
            _ => Border,
        } ?? Border;

        /// <summary>
        /// Gets the foreground color according to the current control state.
        /// </summary>
        /// <returns></returns>
        protected internal virtual Color GetCurrentForegroundColor() => InteractionState switch
        {
            ControlInteractionState.Disabled => disabledForegroundColor,
            ControlInteractionState.MouseOver => activeForegroundColor,
            ControlInteractionState.Clicking => pressedForegroundColor,
            _ => foregroundColor,
        } ?? foregroundColor ?? Color.Black;

        /// <summary>
        /// Renders the control.
        /// </summary>
        /// <param name="context">Context to render the control.</param>
        public void Render(RenderContext context)
        {
            if (!Visible)
                return;

            // Prepare context
            var oldTransform = context.Transform;
            context.Transform = Transform;

            Rectangle? oldScissorRectangle = null;
            if (ClipToBounds && context.Transform.Rotation2D == 0)
            {
                oldScissorRectangle = context.Scissor;
                var absoluteBounds = context.Transform.Apply(ZeroBounds);
                var newScissorRectangle = Rectangle.Intersect(context.Scissor, absoluteBounds);

                if (newScissorRectangle.Width == 0 || newScissorRectangle.Height == 0)
                {
                    context.Transform = oldTransform;
                    return;
                }

                context.Scissor = newScissorRectangle;
            }

            var oldOpacity = context.Opacity;
            context.Opacity *= Opacity;

            // Draw
            // Stage 1: draw background.
            GetCurrentBackground()?.Draw(context, InnerBounds, BackgroundColor);

            // Stage 2: Draw Borders
            var border = GetCurrentBorder();
            if (border != null)
            {
                var borderBounds = BorderBounds;
                if (BorderThickness.Left > 0)
                {
                    border.Draw(context, new Rectangle(borderBounds.X, borderBounds.Y, BorderThickness.Left, borderBounds.Height));
                }

                if (BorderThickness.Top > 0)
                {
                    border.Draw(context, new Rectangle(borderBounds.X, borderBounds.Y, borderBounds.Width, BorderThickness.Top));
                }

                if (BorderThickness.Right > 0)
                {
                    border.Draw(context, new Rectangle(borderBounds.Right - BorderThickness.Right, borderBounds.Y, BorderThickness.Right, borderBounds.Height));
                }

                if (BorderThickness.Bottom > 0)
                {
                    border.Draw(context, new Rectangle(borderBounds.X, borderBounds.Bottom - BorderThickness.Bottom, borderBounds.Width, BorderThickness.Bottom));
                }
            }

            // Stage 3: Internal rendering
            BeforeRender(context);
            RenderInternal(context);
            AfterRender(context);

            // Restore context after rendering.
            // Restore scissor
            if (oldScissorRectangle != null)
            {
                context.Scissor = oldScissorRectangle.Value;
            }

            // Optional debug rendering
            if (EnvironmentSettings.DebugOptions.DrawControlFrames)
            {
                context.DrawRectangle(ZeroBounds, Color.LightGreen);
            }

            if (EnvironmentSettings.DebugOptions.DrawFocusFrame && HasFocus)
            {
                context.DrawRectangle(ZeroBounds, Color.Red);
            }

            if (EnvironmentSettings.DebugOptions.DrawMouseHoverFrame && IsMouseOver)
            {
                context.DrawRectangle(ZeroBounds, Color.Yellow);
            }

            // Restore the rest of context options.
            context.Transform = oldTransform;
            context.Opacity = oldOpacity;
        }

        /// <summary>
        /// Custom internal rendering handler for the control.
        /// </summary>
        /// <param name="context">Context to render the control.</param>
        protected internal virtual void RenderInternal(RenderContext context)
        {
        }

        /// <summary>
        /// Measures available size for the control.
        /// </summary>
        /// <param name="availableSize">Available size for the measure.</param>
        /// <param name="clampBounds">Determines whether to clamp measure result according to actual control size.</param>
        /// <returns>Size needed for the control to display.</returns>
        public Point Measure(Point availableSize, bool clampBounds = true)
        {
            if (!isMeasureDirty && lastMeasureSize == availableSize && clampBounds)
            {
                return lastMeasureResult;
            }

            Point result;
            if (SizeConstraints.Width != 0 && availableSize.X > SizeConstraints.Width)
            {
                availableSize.X = SizeConstraints.Width;
            }
            else if (MaxWidth != 0 && availableSize.X > MaxWidth)
            {
                availableSize.X = MaxWidth;
            }

            if (SizeConstraints.Height != 0 && availableSize.Y > SizeConstraints.Height)
            {
                availableSize.Y = SizeConstraints.Height;
            }
            else if (MaxHeight != 0 && availableSize.Y > MaxHeight)
            {
                availableSize.Y = MaxHeight;
            }

            availableSize.X -= IndentationWidth;
            availableSize.Y -= IndentationHeight;

            result = MeasureInternal(availableSize);

            availableSize.X += IndentationWidth;
            availableSize.Y += IndentationHeight;

            if (clampBounds)
            {
                if (SizeConstraints.Width != 0)
                    result.X = SizeConstraints.Width;
                else
                    result.X = result.X.OptionalClamp(MinWidth, MaxWidth);
                if (SizeConstraints.Height != 0)
                    result.Y = SizeConstraints.Height;
                else
                    result.Y = result.Y.OptionalClamp(MinHeight, MaxHeight);
            }

            lastMeasureResult = result;
            lastMeasureSize = availableSize;
            isMeasureDirty = false;

            return result;
        }

        /// <summary>
        /// Internal custom measuring feature.
        /// </summary>
        /// <param name="availableSize">Available size for the measure.</param>
        /// <returns></returns>
        protected internal virtual Point MeasureInternal(Point availableSize)
        {
            return Point.Zero;
        }

        /// <summary>
        /// Arranges the control in the container bounds.
        /// </summary>
        /// <param name="containerBounds">Container bounds to arrange.</param>
        public void Arrange(Rectangle containerBounds)
        {
            if (!isArrangeDirty && ContainerBounds == containerBounds)
            {
                return;
            }

            isArrangeDirty = true;
            ContainerBounds = containerBounds;
            UpdateArrange();
        }

        /// <summary>
        /// Updates the arrange.
        /// </summary>
        public void UpdateArrange()
        {
            if (!isArrangeDirty)
                return;

            var containerSize = new Point(ContainerBounds.Width, ContainerBounds.Height);
            Point size = !(HorizontalAlignment == HorizontalAlignment.Stretch && VerticalAlignment == VerticalAlignment.Stretch)
                ? Measure(containerSize)
                : containerSize;

            size = size.Clamp(Point.Zero, containerSize);
            size.X = size.X.OptionalClamp(MinWidth, MaxWidth);
            size.Y = size.Y.OptionalClamp(MinHeight, MaxHeight);
            if (HorizontalAlignment == HorizontalAlignment.Stretch)
            {
                if (MaxWidth != 0)
                {
                    containerSize.X = Math.Clamp(containerSize.X, 0, MaxWidth);
                }
            }

            if (VerticalAlignment == VerticalAlignment.Stretch)
            {
                if (MaxHeight != 0)
                {
                    containerSize.Y = Math.Clamp(containerSize.Y, 0, MaxHeight);
                }
            }

            var layout = size.Align(containerSize, horizontalAlignment, verticalAlignment);
            layout.Offset(ContainerBounds.Location);
            Bounds = layout;
            InvalidateTransform();

            ArrangeInternal();
            ArrangeUpdated?.Invoke(this, EventArgs.Empty);

            isArrangeDirty = false;
        }

        /// <summary>
        /// Custom arrange handling method.
        /// </summary>
        protected internal virtual void ArrangeInternal()
        {
        }

        /// <summary>
        /// Invalidates the arrange so it has to be recomputed next time.
        /// </summary>
        public void InvalidateArrange()
        {
            isArrangeDirty = true;
        }

        /// <summary>
        /// Finds the first child that passes the condition.
        /// </summary>
        /// <remarks>
        ///
        /// </remarks>
        /// <param name="predicate">A function to select the control.</param>
        /// <returns>First control that passes the condition or <see langword="null"/> if the child wasn't found.</returns>
        public Control? FindChild(Func<Control, bool> predicate)
        {
            if (predicate(this))
                return this;

            if (this is IContainerControl container)
            {
                foreach (var child in container.Children)
                {
                    var result = child.FindChild(predicate);
                    if (result != null) return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds all children that pass the condition.
        /// </summary>
        /// <param name="predicate">A function to select controls.</param>
        /// <returns>An enumerable with all found children.</returns>
        public IEnumerable<Control> FindChildren(Func<Control, bool> predicate)
        {
            if (this is IContainerControl container)
            {
                var controls = container.Children.Where(predicate);
                if (predicate(this))
                    return Enumerable.Repeat(this, 1).Concat(controls);
                return controls;
            }

            return Enumerable.Repeat(this, 1);
        }

        /// <summary>
        /// Finds the child with given name.
        /// </summary>
        /// <param name="name">Name of the child to find.</param>
        /// <returns>First child with given name or <see langword="null"/> if there are no children with this name.</returns>
        public Control? FindControlByName(string name) => FindChild(x => x.Name == name);

        /// <summary>
        /// Finds the child with given name or throws the exception when it doesn't exist.
        /// </summary>
        /// <param name="name">Name to find.</param>
        /// <returns>The first child with given name.</returns>
        public Control FindRequiredControlByName(string name) => FindControlByName(name).NeverNull();

        /// <summary>
        /// Invalidates the transform so it has to be recomputed next time it's needed.
        /// </summary>
        internal virtual void InvalidateTransform()
        {
            transform = null;
            isInverseMatrixDirty = true;
        }

        /// <summary>
        /// Invalidates measure so it has to be recomputed.
        /// </summary>
        public virtual void InvalidateMeasure()
        {
            isMeasureDirty = true;
            InvalidateArrange();
            Parent?.InvalidateMeasure();
            Desktop?.InvalidateLayout();
        }

        /// <summary>
        /// Applies a style to this control.
        /// </summary>
        /// <param name="style">A style to apply.</param>
        public void ApplyStyle(ControlTemplate style)
        {
            style.Apply(this);
        }

        /// <summary>
        /// Applies a style with given name from the stylesheet.
        /// </summary>
        /// <param name="stylesheet">A stylesheet to find needed style.</param>
        /// <param name="styleName">Name of the style.</param>
        public void SetStyle(Stylesheet stylesheet, string styleName)
        {
            StyleName = styleName;

            if (StyleName != null && stylesheet.ContainsKey(styleName))
            {
                SetStyleInternal(stylesheet, styleName);
            }
        }

        /// <summary>
        /// Sets the style from default stylesheet to the control.
        /// </summary>
        /// <param name="name">Name of the style.</param>
        public void SetStyle(string name)
        {
            SetStyle(Stylesheet.Default, name);
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException"></exception>
        public virtual void SetBinding(Binding binding)
        {
            if (binding.Target != this)
                throw new InvalidOperationException($"Binding target should be this object to store inside the binding handler.");
            bindings.Add(binding);
            if (LayoutDataContext != null)
            {
                binding.Source = LayoutDataContext;
            }
        }

        /// <inheritdoc/>
        public virtual void RemoveBinding(Binding binding)
        {
            if (bindings.Remove(binding))
            {
                binding.Dispose();
            }
        }

        /// <summary>
        /// Internal handler to set the style.
        /// </summary>
        /// <param name="stylesheet">Stylesheet to find the style.</param>
        /// <param name="name">Name of the style to apply.</param>
        protected virtual void SetStyleInternal(Stylesheet stylesheet, string name)
        {
            stylesheet[name].Apply(this);
        }

        /// <summary>
        /// Calls the action for this control and all sub-controls of this control and returns the result of the operation.
        /// </summary>
        /// <param name="action">
        /// Action to do. If action returns <see langword="false"/>
        /// on one of sub-controls, the whole process operation stops.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if all action calls ended with the <see langword="true"/> result,
        /// <see langword="false"/> if at least one call returned <see langword="false"/>.
        /// </returns>
        public virtual bool ProcessControls(Func<Control, bool> action)
        {
            return action(this);
        }

        /// <summary>
        /// Calls the action for this control and all parents of this control and returns the result of the operation.
        /// </summary>
        /// <param name="action">
        /// Action to do. If action returns <see langword="false"/>
        /// on one of parents, the whole process operation stops.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if all action calls ended with the <see langword="true"/> result,
        /// <see langword="false"/> if at least one call returned <see langword="false"/>.
        /// </returns>
        public virtual bool ProcessParentRecursive(Func<Control?, bool> action)
        {
            Control? toCheck = this;
            bool result;
            do
            {
                toCheck = toCheck.Parent;
                result = action(toCheck);
            }
            while (result && toCheck != null);
            return result;
        }

        /// <summary>
        /// Resets the control's font according to new values of the <see cref="FontFamily"/> or <see cref="FontSize"/> properties.
        /// </summary>
        protected internal virtual void ResetFont()
        {
            if (FontFamily == null)
            {
                Font = null;
                return;
            }
            Font = EnvironmentSettings.DefaultAssets.Fonts[FontFamily, FontSize];
        }

        #region Event utils

        /// <summary>
        /// Notifies that the property value changes.
        /// </summary>
        /// <param name="propertyName">Name of changed property.</param>
        protected internal virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new(propertyName));
        }

        /// <summary>
        /// Invokes the <see cref="MouseLeft"/> event.
        /// </summary>
        protected internal virtual void OnMouseLeft()
        {
            MouseLeft?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="MouseEnter"/> event.
        /// </summary>
        protected internal virtual void OnMouseEntered()
        {
            MouseEnter?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="MouseMove"/> event.
        /// </summary>
        protected internal virtual void OnMouseMove()
        {
            MouseMove?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="DoubleClick"/> event.
        /// </summary>
        protected internal virtual void OnDoubleClick()
        {
            DoubleClick?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="RightClick"/> event.
        /// </summary>
        protected internal virtual void OnRightClick()
        {
            RightClick?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="MouseWheelRoll"/> event.
        /// </summary>
        protected internal virtual void OnMouseWheel(float delta)
        {
            MouseWheelRoll?.Invoke(this, delta);
        }

        /// <summary>
        /// Invokes the <see cref="TouchLeft"/> event.
        /// </summary>
        protected internal virtual void OnTouchLeft()
        {
            IsTouching = false;
            TouchLeft?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="TouchEntered"/> event.
        /// </summary>
        protected internal virtual void OnTouchEntered()
        {
            IsTouching = true;
            TouchEntered?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="TouchMove"/> event.
        /// </summary>
        protected internal virtual void OnTouchMoved()
        {
            IsTouching = true;
            TouchMove?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="TouchDown"/> event.
        /// </summary>
        protected internal virtual bool OnTouchDown()
        {
            IsTouching = true;

            if (Desktop == null)
                return false;

            if (Enabled && AcceptFocus)
            {
                Desktop.CurrentFocusedControl = this;
            }

            if (DragHandle?.ContainsTouch == true)
            {
                ITransformable? parent = Parent ?? (ITransformable)Desktop;
                startPosition = (Point)parent.ToLocal(new Vector2(Desktop.TouchPosition.X, Desktop.TouchPosition.Y));
                startLocation = Location;
            }

            TouchDown?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Invokes the <see cref="TouchUp"/> event.
        /// </summary>
        protected internal virtual bool OnTouchUp()
        {
            if (Desktop == null)
                return false;
            startPosition = null;
            IsTouching = false;
            TouchUp?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Invokes the <see cref="KeyDown"/> event.
        /// </summary>
        public void FireKeyDown(Keys key)
        {
            KeyDown?.Invoke(this, key);
        }

        /// <summary>
        /// Invokes the <see cref="KeyDown"/> event.
        /// </summary>
        protected internal virtual void OnKeyDown(Keys key)
        {
            FireKeyDown(key);
        }

        /// <summary>
        /// Invokes the <see cref="KeyUp"/> event.
        /// </summary>
        protected internal virtual void OnKeyUp(Keys key)
        {
            KeyUp?.Invoke(this, key);
        }

        /// <summary>
        /// Invokes the <see cref="TextInput"/> event.
        /// </summary>
        /// <param name="e">Text input info.</param>
        protected internal virtual void OnTextInput(TextInputEvent e)
        {
            TextInput?.Invoke(this, e);
        }

        /// <summary>
        /// Invokes the <see cref="DesktopPlacedChanged"/> event.
        /// </summary>
        protected virtual void OnDesktopPlacedChanged()
        {
            DesktopPlacedChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="LocationChanged"/> event.
        /// </summary>
        protected virtual void OnLocationChanged()
        {
            LocationChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="VisibilityChanged"/> event.
        /// </summary>
        protected internal virtual void OnVisibilityChanged()
        {
            InvalidateMeasure();
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invokes the <see cref="FontChanged"/> event.
        /// </summary>
        protected virtual void OnFontChanged()
        {
            FontChanged?.Invoke(this, EventArgs.Empty);
            if (font == null)
            {
                fontFamily = null;
                fontSize = 12;
            }
            else
            {
                fontSize = (int)font.FontSize;
            }
        }

        /// <summary>
        /// Invokes the <see cref="FocusChanged"/> event and loses keyboard focus.
        /// </summary>
        protected internal virtual void OnLostFocus()
        {
            FocusChanged?.Invoke(this, EventArgs.Empty);
            HasFocus = false;
        }

        /// <summary>
        /// Invokes the <see cref="FocusChanged"/> event and gets keyboard focus.
        /// </summary>
        protected internal virtual void OnGotFocus()
        {
            FocusChanged?.Invoke(this, EventArgs.Empty);
            HasFocus = true;
        }

        /// <summary>
        /// Invokes the <see cref="ActiveChanged"/> event.
        /// </summary>
        protected internal virtual void OnActiveChanged()
        {
            ActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Resets data context and all bindings.
        /// </summary>
        protected internal override void OnDataContextChanged()
        {
            base.OnDataContextChanged();
            ResetDataContext();
        }

        /// <summary>
        /// Resets data context and all bindings for this object.
        /// </summary>
        protected internal virtual void ResetDataContext()
        {
            var newDataContext = LayoutDataContext;
            foreach (var binding in bindings)
            {
                if (Desktop != null && binding.XPath != null)
                {
                    binding.Source = Desktop.GetControlByName(binding.XPath) ?? newDataContext;
                }
                else
                {
                    binding.Source = newDataContext;
                }
            }
        }

        /// <inheritdoc/>
        protected internal override void OnDetach()
        {
            foreach (var binding in bindings)
            {
                binding.Dispose();
            }
            bindings.Clear();
            InvalidateArrange();
        }

        /// <summary>
        /// Detaches the control from the parent.
        /// </summary>
        /// <remarks>
        /// Don't use this method when removing child from your custom control in <see cref="ContentControl.Content"/> or
        /// <see cref="IContainerControl.Remove(Control)"/> handlers because it calls following methods to remove it.
        /// That can make infinite recursion, use <see cref="ForceDetach"/> in these cases instead.
        /// </remarks>
        public void Detach()
        {
            bool handled = RemoveFromParent();
            handled |= RemoveFromDesktop();
            if (!handled) OnDetach();
        }

        /// <summary>
        /// Forces remove a control from the UI hierarchy. Use it only when you know that the control is handled correctly.
        /// </summary>
        /// <remarks>
        /// Note that this method can break UI logic because it doesn't check backlinks.
        /// </remarks>
        public void ForceDetach()
        {
            Parent = null;
            Desktop = null;
            OnDetach();
        }

        private object GetDataContext()
        {
            // Self context has the most priority.
            if (dataContext != null) return dataContext;
            // After it data context is the nearest parent existing context.
            if (Parent != null)
            {
                Control? parentToSearch = Parent;
                do
                {
                    if (parentToSearch.DataContext != null) return parentToSearch.DataContext;
                    parentToSearch = parentToSearch.Parent;
                }
                while (parentToSearch != null);
            }
            // Try to find desktop root data context.
            if (Desktop?.Root?.DataContext != null)
                return Desktop.Root.DataContext;
            // If there no objects that can provide data context, this object becomes a context for itself.
            return this;
        }

        /// <summary>
        /// Removes the control from its parent.
        /// </summary>
        public bool RemoveFromParent()
        {
            if (Parent is IContainerControl container)
            {
                return container.Remove(this);
            }
            else if (Parent is ContentControl content)
            {
                if (content.Content == this || content.Child == this)
                {
                    content.Content = null;
                    return true;
                }
            }
            else Parent = null;
            return false;
        }

        /// <summary>
        /// Removes the control from the desktop.
        /// </summary>
        public bool RemoveFromDesktop()
        {
            return Desktop?.RemoveControl(this) == true;
        }

        /// <summary>
        /// Fires the <see cref="LocationChanged"/> event.
        /// </summary>
        private void FireLocationChanged()
        {
            LocationChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Fires the <see cref="SizeChanged"/> event.
        /// </summary>
        private void FireSizeChanged()
        {
            SizeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets keyboard focus on this element.
        /// </summary>
        public void SetFocus()
        {
            if (Desktop != null)
                Desktop.CurrentFocusedControl = this;
        }

        private void SubscribeTouchMoved(bool value)
        {
            if (Parent != null)
            {
                Parent.TouchMove -= DesktopTouchMoved!;
                Parent.TouchUp -= DesktopTouchUp!;
                if (value)
                {
                    Parent.TouchMove += DesktopTouchMoved!;
                    Parent.TouchUp += DesktopTouchUp!;
                }
            }
            if (Desktop != null)
            {
                Desktop.TouchMove -= DesktopTouchMoved!;
                Desktop.TouchUp -= DesktopTouchUp!;
                if (value)
                {
                    Desktop.TouchMove += DesktopTouchMoved!;
                    Desktop.TouchUp += DesktopTouchUp!;
                }
            }
        }

        private void DesktopTouchMoved(object sender, EventArgs e)
        {
            if (startPosition == null || !CanDrag || Desktop == null)
                return;

            ITransformable parent = Parent ?? (ITransformable)Desktop;
            Vector2 newPos = parent.ToLocal(Desktop.TouchPosition);
            Vector2 delta = newPos - startPosition.GetValueOrDefault();

            Point newLocation = Location;
            if ((DragDirection & DragDirection.Horizontal) == DragDirection.Horizontal)
            {
                newLocation.X = startLocation.X + (int)delta.X;
            }
            if ((DragDirection & DragDirection.Vertical) == DragDirection.Vertical)
            {
                newLocation.Y = startLocation.Y + (int)delta.Y;
            }

            Rectangle parentBounds = Parent?.Bounds ?? Desktop.InternalBounds;
            if (newLocation.X < 0) newLocation.X = 0;
            if (newLocation.Y < 0) newLocation.Y = 0;
            if (newLocation.X + Bounds.Width > parentBounds.Width)
                newLocation.X = parentBounds.Width - Bounds.Width;
            if (newLocation.Y + Bounds.Height > parentBounds.Height)
                newLocation.Y = parentBounds.Height - Bounds.Height;

            Location = newLocation;
        }

        #endregion Event utils

        /// <inheritdoc/>
        public Vector2 ToGlobal(Vector2 position) => Transform.Apply(position);

        /// <summary>
        /// Converts local point coordinate to the global coordinate system.
        /// </summary>
        /// <param name="point">A point to convert.</param>
        /// <returns>New point with global coordinates.</returns>
        public Point ToGlobal(Point point) => Transform.Apply(point);

        /// <inheritdoc/>
        public Vector2 ToLocal(Vector2 position)
        {
            if (isInverseMatrixDirty)
            {
                inverseMatrix = Matrix.Invert(Transform.Matrix);
            }
            return Vector2.Transform(position, inverseMatrix).XY();
        }

        /// <summary>
        /// Converts global point coordinate to the local coordinate system.
        /// </summary>
        /// <param name="point">A point to convert.</param>
        /// <returns>New point with local coordinates.</returns>
        public Point ToLocal(Point point) => (Point)ToLocal((Vector2)point);

        /// <summary>
        /// Determines whether the global coordinate system point is inside the control bounds.
        /// </summary>
        /// <param name="globalPoint">Global point to check.</param>
        /// <returns><see langword="true"/> if the point is inside the control, <see langword="false"/> otherwise.</returns>
        public bool ContainsPoint(Point globalPoint)
        {
            var local = ToLocal(globalPoint);
            return BorderBounds.Contains(local);
        }

        private void DesktopTouchUp(object sender, EventArgs e)
        {
            startPosition = null;
            if (this is ButtonBase button) button.IsPressed = false;
        }

        private string GetDebuggerDisplay()
        {
            return $"({GetType().Name}) {Name}";
        }
    }
}