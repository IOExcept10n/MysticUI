using MysticUI.Extensions;
using MysticUI.Extensions.Content;
using MysticUI.Extensions.Input;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;
using Stride.Input;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the core class of the UI system. All controls and UI elements are placed on the desktop.
    /// </summary>
    public sealed class Desktop : ITransformable, IDisposable
    {
        private readonly RenderContext renderContext = new();
        private Control? root;
        private Control? previousFocus;
        private bool isContextMenuOpened;
        private bool isFocusSet;
        private bool isInverseMatrixDirty;
        private bool isControlsListDirty;
        private Matrix inverseMatrix;
        private bool isLayoutDirty;
        private MouseInfo lastMouseInfo;
        private IEnumerable<Keys>? keys;
        private List<Keys>? lastKeys;
        private DateTime lastKeyDown;
        private int keyDownCount;
        private DateTime lastTouchDown;
        private Point touchPosition;
        private Point previousTouchPosition;
        private Point mousePosition;
        private Point previousMousePosition;
        private IEnumerable<Control> controls;
        private readonly ObservableCollection<Control> controlsObservable;
        private Rectangle internalBounds;
        private Control? currentFocusedControl;
        private Control? currentMouseOverControl;
        private Vector2 scale = Vector2.One;
        private Vector2 transformOrigin;
        private float rotation;
        private Transform? transform;
        private bool isTouchingDown;

        /// <summary>
        /// Time in milliseconds to split new and previous key.
        /// </summary>
        public static double RepeatKeyDownStart { get; set; } = 500;

        /// <summary>
        /// Maximal interval between two clicks to raise the <see cref="DoubleClick"/> event.
        /// </summary>
        public static double DoubleClickInterval { get; set; } = 500;

        /// <summary>
        /// Interval in milliseconds to repeat keys input.
        /// </summary>
        public static double RepeatKeyDownInterval { get; set; } = 50;

        /// <summary>
        /// Distance between two points to identify the <see cref="DoubleClick"/> event.
        /// </summary>
        public static double DoubleClickRadius { get; set; } = 2;

        /// <summary>
        /// Root element of the desktop.
        /// </summary>
        public Control? Root
        {
            get => root;
            set
            {
                if (value == root) return;
                HideContextMenu();
                if (root != null)
                {
                    controlsObservable.Remove(root);
                }
                //root?.Detach();
                root = value;
                if (root != null)
                {
                    root.OnDesktopRootAttach(this);
                    controlsObservable.Add(root);
                }
            }
        }

        /// <summary>
        /// Previous mouse position for the correct controls work.
        /// </summary>
        public Point PreviousMousePosition => previousMousePosition;

        /// <summary>
        /// Current mouse position.
        /// </summary>
        public Point MousePosition
        {
            get => mousePosition;
            private set
            {
                if (value == mousePosition) return;
                previousMousePosition = mousePosition;
                mousePosition = value;
                MouseMove(this, EventArgs.Empty);
                Controls.ProcessMouseMovement();
                if (IsTouchingDown)
                {
                    TouchPosition = MousePosition;
                }
            }
        }

        /// <summary>
        /// Position of the touch if user is pressing mouse button.
        /// </summary>
        public Point TouchPosition
        {
            get => touchPosition;
            set
            {
                if (value == touchPosition) return;
                previousTouchPosition = touchPosition;
                touchPosition = value;
                TouchMove(this, EventArgs.Empty);
                Controls.ProcessTouchMovement();
            }
        }

        /// <summary>
        /// Menu toolbar of the given element.
        /// </summary>
        public HorizontalToolbar? MenuBar { get; set; }

        /// <summary>
        /// All controls of the desktop.
        /// </summary>
        internal IEnumerable<Control> Controls
        {
            get
            {
                UpdateControls();
                return controls;
            }
        }

        internal Rectangle InternalBounds
        {
            get => internalBounds;
            set
            {
                if (value == internalBounds) return;
                internalBounds = value;
                InvalidateTransform();
            }
        }

        internal Rectangle LayoutBounds => new(0, 0, InternalBounds.Width, InternalBounds.Height);

        internal Point BoundsPoint => new(InternalBounds.Width, InternalBounds.Height);

        internal UIElement? ContextMenu { get; set; }

        /// <summary>
        /// Current keyboard focused control.
        /// </summary>
        public Control? CurrentFocusedControl
        {
            get => currentFocusedControl;
            set
            {
                if (value != null)
                    isFocusSet = true;
                if (value == currentFocusedControl)
                    return;
                var oldValue = currentFocusedControl;
                if (oldValue != null)
                {
                    CancellableEventArgs<Control> args = new(oldValue);
                    OnLosingFocus(this, args);
                    if (oldValue.Desktop != null && args.Cancel)
                        return;
                }
                currentFocusedControl = value;
                oldValue?.OnLostFocus();
                if (value != null)
                {
                    value.OnGotFocus();
                    OnGotFocus(this, value);
                }
                if (value?.AcceptsTextInput == true)
                {
                    InputManager.EnableTextInput();
                }
                else
                {
                    InputManager.DisableTextInput();
                }
            }
        }

        /// <summary>
        /// Current forward control which has mouse pointer over it.
        /// </summary>
        public Control? CurrentMouseOverControl
        {
            get => currentMouseOverControl;
            set
            {
                if (value == currentMouseOverControl)
                    return;
                currentMouseOverControl = value;
                MouseOverControlChanged(this, value);
            }
        }

        /// <summary>
        /// Opacity of the whole desktop.
        /// </summary>
        public float Opacity { get; set; } = 1f;

        /// <summary>
        /// Scale of the desktop.
        /// </summary>
        public Vector2 Scale
        {
            get => scale;
            set
            {
                if (value == scale) return;
                scale = value;
                InvalidateTransform();
            }
        }

        /// <summary>
        /// Transform origin.
        /// </summary>
        public Vector2 TransformOrigin
        {
            get => transformOrigin;
            set
            {
                if (value == transformOrigin) return;
                transformOrigin = value;
                InvalidateTransform();
            }
        }

        /// <summary>
        /// Rotation in degrees around the Z-axis.
        /// </summary>
        public float Rotation
        {
            get => rotation;
            set
            {
                if (value == rotation) return;
                rotation = value;
                InvalidateTransform();
            }
        }

        internal Transform Transform
        {
            get
            {
                transform ??= Transform.Create2DTransform((Vector2)internalBounds.Location, TransformOrigin * (Vector2)BoundsPoint, Scale, Rotation * (float)Math.PI / 180);
                return transform.Value;
            }
        }

        /// <summary>
        /// Indicates whether the mouse pointer is over any UI element.
        /// </summary>
        public bool IsMouseOverUI => IsPointOverGUI(MousePosition);

        /// <summary>
        /// Indicates whether the mouse is clicking over any UI element.
        /// </summary>
        public bool IsTouchingOverUI => IsPointOverGUI(TouchPosition);

        /// <summary>
        /// Indicates whether the Shift key is pressed.
        /// </summary>
        public bool IsShiftPressed => IsKeyDown(Keys.LeftShift) || IsKeyDown(Keys.RightShift);

        /// <summary>
        /// Indicates whether the Control key is pressed.
        /// </summary>
        public bool IsCtrlPressed => IsKeyDown(Keys.LeftCtrl) || IsKeyDown(Keys.RightCtrl);

        /// <summary>
        /// Indicates whether the Alt key is pressed.
        /// </summary>
        public bool IsAltPressed => IsKeyDown(Keys.LeftAlt) || IsKeyDown(Keys.RightAlt);

        /// <summary>
        /// Indicates whether the user is clicking the mouse during the frame.
        /// </summary>
        public bool IsTouchingDown
        {
            get => isTouchingDown;
            set
            {
                if (value == isTouchingDown) return;
                isTouchingDown = value;
                if (value)
                {
                    OnInputDown();
                    TouchDown(this, EventArgs.Empty);
                }
                else
                {
                    OnInputUp();
                    TouchUp(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Indicates whether any modal control is opened.
        /// </summary>
        public bool HasModalOpened => Controls.Any(x => x.Visible && x.Enabled && x.IsModal);

        /// <summary>
        /// Indicates whether the toolbar window is opened and active.
        /// </summary>
        public bool IsToolBarActive => MenuBar != null && (MenuBar.OpenedItem != null || IsAltPressed);

        /// <summary>
        /// Main background brush for the desktop.
        /// </summary>
        public IBrush? Background { get; set; }

        /// <summary>
        /// An object that provides input for the desktop.
        /// </summary>
        public IInputManager InputManager { get; set; }

        /// <summary>
        /// Gets or sets current asset context for the desktop.
        /// </summary>
        public AssetContext AssetContext { get; set; }

        /// <summary>
        /// Gets the <see cref="MysticUI.Dispatcher"/> instance for the current <see cref="Desktop"/> instance.
        /// </summary>
        /// <remarks>
        /// The dispatchers are used to synchronize UI update when using multithreading access or detaching elements inside of the update process.
        /// </remarks>
        public Dispatcher Dispatcher { get; } = new();

        /// <summary>
        /// Handles key down event.
        /// </summary>
        public Action<Keys> KeyDownHandler;

        /// <summary>
        /// Occurs when the mouse wheel rolls during the frame.
        /// </summary>
        public event EventHandler<GenericEventArgs<float>> MouseWheelRoll = delegate { };

        /// <summary>
        /// Occurs when the mouse moves during the frame.
        /// </summary>
        public event EventHandler MouseMove = delegate { };

        /// <summary>
        /// Occurs when the mouse left button is pressed.
        /// </summary>
        public event EventHandler TouchDown = delegate { };

        /// <summary>
        /// Occurs when the clicking mouse moves during frame.
        /// </summary>
        public event EventHandler TouchMove = delegate { };

        /// <summary>
        /// Occurs when the mouse click ends.
        /// </summary>
        public event EventHandler TouchUp = delegate { };

        /// <summary>
        /// Occurs when the user double-clicks the mouse.
        /// </summary>
        public event EventHandler DoubleClick = delegate { };

        /// <summary>
        /// Occurs when the user inputs any key.
        /// </summary>
        public event EventHandler<GenericEventArgs<Keys>> KeyDown = delegate { };

        /// <summary>
        /// Occurs when the user stops pressing any key.
        /// </summary>
        public event EventHandler<GenericEventArgs<Keys>> KeyUp = delegate { };

        /// <summary>
        /// Occurs when the user inputs any character.
        /// </summary>
        public event EventHandler<GenericEventArgs<char>> CharInput = delegate { };

        /// <summary>
        /// Occurs when the <see cref="ContextMenu"/> is closing.
        /// </summary>
        /// <remarks>
        /// This event allows you to cancel the closing process.
        /// </remarks>
        public event EventHandler<CancellableEventArgs<UIElement>> ContextMenuClosing = delegate { };

        /// <summary>
        /// Occurs when the context menu is closed.
        /// </summary>
        public event EventHandler<GenericEventArgs<Control>> ContextMenuClosed = delegate { };

        /// <summary>
        /// Occurs when value of the <see cref="CurrentMouseOverControl"/> property changes.
        /// </summary>
        public event EventHandler<GenericEventArgs<Control?>> MouseOverControlChanged = delegate { };

        /// <summary>
        /// Occurs when any control gets the keyboard focus.
        /// </summary>
        public event EventHandler<GenericEventArgs<Control>> OnGotFocus = delegate { };

        /// <summary>
        /// Occurs when any control loses the keyboard focus.
        /// </summary>
        /// <remarks>
        /// This event allows you cancel keyboard focus change.
        /// </remarks>
        public event EventHandler<CancellableEventArgs<Control>> OnLosingFocus = delegate { };

        /// <summary>
        /// Occurs when the control has lost the keyboard focus.
        /// </summary>
        public event EventHandler<GenericEventArgs<Control>> OnLostFocus = delegate { };

        /// <summary>
        /// Occurs when value of the <see cref="IsMouseOverUI"/> property changes.
        /// </summary>
        public event EventHandler IsMouseOverUIChanged = delegate { };

        /// <summary>
        /// Creates new instance of the <see cref="Desktop"/> class.
        /// </summary>
        public Desktop()
        {
            controls = Enumerable.Empty<Control>();
            controlsObservable = new();
            controlsObservable.CollectionChanged += OnControlsChanged;
            KeyDownHandler = OnKeyDown;
            InputManager = new InputManager();
            AssetContext = new(string.Empty);
        }

        /// <summary>
        /// Determines whether the given key is pressed.
        /// </summary>
        /// <param name="key">Key to press.</param>
        /// <returns><see langword="true"/> if the key is pressed down, <see langword="false"/> otherwise.</returns>
        public bool IsKeyDown(Keys key) => InputManager.IsKeyDown(key);

        /// <summary>
        /// Shows the context menu on the desktop.
        /// </summary>
        /// <param name="menu">A menu to show.</param>
        /// <param name="position">Position to present the menu.</param>
        public void ShowContextMenu(UIElement? menu, Point position)
        {
            // Hide old context menu if there are any.
            HideContextMenu();

            ContextMenu = menu;
            if (menu == null)
                return;

            if (menu is Control menuControl)
            {
                menuControl.HorizontalAlignment = HorizontalAlignment.Left;
                menuControl.VerticalAlignment = VerticalAlignment.Top;

                var measure = menuControl.Measure(BoundsPoint);
                if (measure.X + position.X > LayoutBounds.Right)
                {
                    position.X = LayoutBounds.Right - measure.X;
                }
                if (measure.Y + position.Y > LayoutBounds.Bottom)
                {
                    position.Y = LayoutBounds.Bottom - measure.Y;
                }

                menuControl.Location = position;
                menuControl.Visible = true;

                if (menuControl.AcceptFocus)
                {
                    previousFocus = CurrentFocusedControl;
                    CurrentFocusedControl = menuControl;
                }
            }

            isContextMenuOpened = true;
        }

        /// <summary>
        /// Hides the context menu.
        /// </summary>
        public void HideContextMenu()
        {
            if (ContextMenu != null && ContextMenu is Control controlContext)
            {
                controlContext.Visible = false;
                ContextMenuClosed(this, controlContext);
                ContextMenu = null;

                if (previousFocus != null)
                {
                    CurrentFocusedControl = previousFocus;
                    previousFocus = null;
                }
            }
        }

        /// <summary>
        /// Makes the layout needed to recompute.
        /// </summary>
        public void InvalidateLayout()
        {
            isLayoutDirty = true;
        }

        /// <summary>
        /// Updates the layout of the desktop.
        /// </summary>
        public void UpdateLayout()
        {
            var newBounds = InputManager.FrameBounds;
            if (InternalBounds != newBounds)
            {
                InvalidateLayout();
            }

            InternalBounds = newBounds;

            if (InternalBounds.IsEmpty || !isLayoutDirty) return;

            MenuBar = null;
            bool active = true;
            foreach (var control in Controls.Where(x => x.Visible))
            {
                control.Arrange(LayoutBounds);
                control.ProcessControls(x =>
                {
                    x.IsActive = active;
                    // Found and set MenuBar
                    if (MenuBar == null && x is HorizontalToolbar menu)
                        MenuBar = menu;

                    // To continue iteration.
                    return true;
                });

                // Everything after the first modal is not active.
                if (control.IsModal)
                    active = false;
            }

            UpdateLayoutRecursive(Controls);

            // Fire mouse movement to update IsMouseOver property
            previousMousePosition = MousePosition;
            Controls.ProcessMouseMovement();
            isLayoutDirty = false;
        }

        /// <summary>
        /// Updates input state.
        /// </summary>
        public void UpdateInput()
        {
            UpdateMouseInput();
            UpdateKeyboardInput();
        }

        /// <summary>
        /// Handles key input.
        /// </summary>
        /// <param name="key">A key to input.</param>
        public void OnKeyDown(Keys key)
        {
            KeyDown.Invoke(this, key);
            if (MenuBar?.IsActive == true)
            {
                MenuBar.OnKeyDown(key);
            }
            else
            {
                if (currentFocusedControl?.IsActive == true)
                {
                    currentFocusedControl.OnKeyDown(key);
                }
            }
            if (key == Keys.Escape && ContextMenu != null)
            {
                HideContextMenu();
            }
        }

        /// <summary>
        /// Renders all UI.
        /// </summary>
        public void Render()
        {
            UpdateInput();
            UpdateLayout();
            RenderVisual();
            Dispatcher.Update();
        }

        /// <summary>
        /// Determines whether the point is over UI.
        /// </summary>
        /// <param name="point">Point to check.</param>
        /// <returns><see langword="true"/> if the point is over any control on the desktop, <see langword="false"/> otherwise.</returns>
        public bool IsPointOverGUI(Point point) => Controls.Any(x => IsPointOverGUI(point, x));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsPointOverGUI(Point point, Control control) =>
            control.Visible &&
            control.ContainsPoint(point) &&
            (!control.FallsThrough(point) ||
            (control is IContainerControl container &&
            container.Children.Any(x => IsPointOverGUI(point, x))));

        private void OnControlsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    foreach (Control c in e.NewItems!)
                    {
                        c.Desktop = this;
                    }
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    foreach (Control c in e.OldItems!)
                    {
                        c.Desktop = null;
                    }
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    foreach (Control c in Controls)
                    {
                        c.Desktop = null;
                    }
                    break;
            }
            InvalidateLayout();
            isControlsListDirty = true;
        }

        private void UpdateControls()
        {
            if (!isControlsListDirty)
                return;
            controls = controlsObservable.OrderBy(x => x.ZIndex);
            isControlsListDirty = false;
        }

        private void RenderVisual()
        {
            var oldScissor = renderContext.Scissor;
            renderContext.Begin();
            renderContext.Transform = Transform;

            Rectangle bounds = Transform.Apply(LayoutBounds);
            renderContext.Scissor = bounds;
            renderContext.Opacity = Opacity;

            Background?.Draw(renderContext, LayoutBounds);

            foreach (var control in Controls)
                control.Render(renderContext);

            renderContext.End();
            renderContext.Scissor = oldScissor;
        }

        private void UpdateLayoutRecursive(IEnumerable<Control> controls)
        {
            foreach (var control in controls)
            {
                if (control.LayoutExpression.ContainsExpression)
                {
                    control.LayoutExpression.Compute(control, Controls);
                }

                if (control is IContainerControl container)
                    UpdateLayoutRecursive(container.Children);
            }
        }

        private void InvalidateTransform()
        {
            transform = null;
            isInverseMatrixDirty = true;
            Controls.ForEach(x => x.InvalidateTransform());
        }

        private void UpdateMouseInput()
        {
            if (InputManager == null) return;
            var mouseInfo = InputManager.Mouse;
            MousePosition = mouseInfo.Position;

            HandleMouseButton(mouseInfo.IsDown(MouseButton.Left), lastMouseInfo.IsDown(MouseButton.Left));
            HandleMouseButton(mouseInfo.IsDown(MouseButton.Middle), lastMouseInfo.IsDown(MouseButton.Middle));
            HandleMouseButton(mouseInfo.IsDown(MouseButton.Right), lastMouseInfo.IsDown(MouseButton.Right));
            HandleMouseButton(mouseInfo.IsDown(MouseButton.Extended1), lastMouseInfo.IsDown(MouseButton.Extended1));
            HandleMouseButton(mouseInfo.IsDown(MouseButton.Extended2), lastMouseInfo.IsDown(MouseButton.Extended2));

            if (mouseInfo.Wheel != 0)
            {
                MouseWheelRoll(this, mouseInfo.Wheel);
                Control? mouseCapture = null;
                if (CurrentFocusedControl?.CaptureMouseRoll == true)
                {
                    mouseCapture = CurrentFocusedControl;
                }
                else
                {
                    var control = CurrentMouseOverControl;
                    while (control != null)
                    {
                        if (control.CaptureMouseRoll)
                        {
                            mouseCapture = control;
                            break;
                        }
                        control = control.Parent;
                    }
                }

                mouseCapture?.OnMouseWheel(mouseInfo.Wheel);
            }

            lastMouseInfo = mouseInfo;
        }

        private void HandleMouseButton(bool isDown, bool wasDown)
        {
            if (isDown && !wasDown)
            {
                TouchPosition = MousePosition;
                IsTouchingDown = true;
                HandleDoubleClick();
            }
            else if (!isDown && wasDown)
            {
                IsTouchingDown = false;
            }
        }

        private void HandleDoubleClick()
        {
            if ((DateTime.UtcNow - lastTouchDown).TotalMilliseconds < DoubleClickInterval &&
                ((Vector2)touchPosition - previousTouchPosition).LengthSquared() <= DoubleClickRadius * DoubleClickRadius)
            {
                DoubleClick(this, EventArgs.Empty);

                Controls.ProcessDoubleClick();
                lastTouchDown = default;
            }
            else
            {
                lastTouchDown = DateTime.UtcNow;
            }
        }

        private void UpdateKeyboardInput()
        {
            if (InputManager == null) return;

            // Handle input keys.
            keys = InputManager.DownKeys;

            DateTime now = DateTime.UtcNow;
            var toCheck = lastKeys == null ? keys : keys.Union(lastKeys);
            foreach (var key in toCheck)
            {
                bool isPressed = keys.Contains(key);
                bool wasPressed = lastKeys?.Contains(key) == true;
                if (isPressed && !wasPressed)
                {
                    if (key == Keys.Tab && CurrentFocusedControl?.AcceptsTab == true)
                    {
                        if (IsShiftPressed)
                        {
                            FocusNextControl();
                        }
                        else
                        {
                            FocusPreviousControl();
                        }
                    }

                    KeyDownHandler?.Invoke(key);

                    lastKeyDown = now;
                    keyDownCount = 0;
                }
                else if (!isPressed && wasPressed)
                {
                    KeyUp(this, key);
                    if (CurrentFocusedControl?.IsActive == true)
                    {
                        CurrentFocusedControl.OnKeyUp(key);
                    }
                }
                else if (isPressed && wasPressed)
                {
                    if (lastKeyDown != default &&
                        ((keyDownCount == 0 && (now - lastKeyDown).TotalMilliseconds > RepeatKeyDownStart) ||
                        (keyDownCount > 0 && (now - lastKeyDown).TotalMilliseconds > RepeatKeyDownInterval)))
                    {
                        lastKeyDown = now;
                        keyDownCount++;
                    }
                }
            }

            (lastKeys ??= new()).Clear();
            lastKeys.AddRange(keys);

            // Handle text input.
            var textInput = InputManager.TextInput;
            if (textInput.Any())
            {
                foreach (var input in textInput)
                {
                    if (CurrentFocusedControl?.IsActive == true)
                    {
                        CurrentFocusedControl.OnTextInput(input);
                    }
                }
            }
        }

        private void OnContextMenuTouch()
        {
            if (ContextMenu == null || ContextMenu is Control { IsTouching: true })
                return;
            CancellableEventArgs<UIElement> args = new(ContextMenu);
            ContextMenuClosing(this, args);
            if (args.Cancel)
                return;
            HideContextMenu();
        }

        private void OnInputDown()
        {
            isContextMenuOpened = false;
            isFocusSet = false;
            Controls.ProcessTouchDown();
            if (!isFocusSet && CurrentFocusedControl != null)
            {
                CurrentFocusedControl = null;
            }
            if (!isContextMenuOpened)
                OnContextMenuTouch();
        }

        private void OnInputUp()
        {
            Controls.ProcessTouchUp();
        }

        private void FocusNextControl()
        {
            if (!Controls.Any()) return;

            var isCurrentFocused = CurrentFocusedControl == null;
            bool focusChanged = false;
            ProcessControls(x =>
            {
                if (isCurrentFocused)
                {
                    x.SetFocus();
                    focusChanged = true;
                    return false;
                }
                else if (x == CurrentFocusedControl)
                {
                    isCurrentFocused = true;
                }
                return true;
            });

            if (focusChanged || CurrentFocusedControl == null)
                return;

            ProcessControls(x =>
            {
                if (CanFocusControl(x))
                {
                    x.SetFocus();
                    return false;
                }
                return true;
            });
        }

        private void FocusPreviousControl()
        {
            if (!Controls.Any()) return;
            Control? previous = null;
            bool focusChanged = false;
            ProcessControls(x =>
            {
                if (x == CurrentFocusedControl && previous != null)
                {
                    previous.SetFocus();
                    focusChanged = true;
                    return false;
                }
                previous = x;
                return true;
            });
            if (focusChanged || CurrentFocusedControl == null) return;
            previous = null;
            ProcessControls(x =>
            {
                if (CanFocusControl(x)) previous = x;
                return true;
            });
            previous?.SetFocus();
        }

        private static bool CanFocusControl(Control control) =>
            control != null && control.Visible && control.IsActive &&
            control.Enabled && control.AcceptFocus;

        private void ProcessControls(Func<Control, bool> action)
        {
            foreach (var control in Controls)
            {
                bool result = control.ProcessControls(action);
                if (!result) return;
            }
        }

        /// <summary>
        /// Removes the control from the desktop.
        /// </summary>
        /// <param name="control">A control to remove.</param>
        /// <returns>
        /// <see langword="true"/> if the control was found in roots of the desktop hierarchy and was removed successfully.
        /// </returns>
        public bool RemoveControl(Control control)
        {
            control.Desktop = null;
            if (controlsObservable.Contains(control))
            {
                controlsObservable.Remove(control);
                control.OnDetach();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a control to the desktop in parallel to the <see cref="Root"/>.
        /// </summary>
        /// <param name="control">A control to attach to a <see cref="Desktop"/>.</param>
        public void Attach(Control control)
        {
            controlsObservable.Add(control);
            control.OnDesktopRootAttach(this);
        }

        /// <summary>
        /// Gets the control by predicate.
        /// </summary>
        /// <param name="predicate">A function to find first control that passes the condition.</param>
        /// <returns></returns>
        public Control? GetControlBy(Func<Control, bool> predicate) => GetControlsBy(predicate).FirstOrDefault();

        /// <summary>
        /// Gets all controls that pass the condition.
        /// </summary>
        /// <param name="predicate">A condition to find controls.</param>
        /// <returns>All controls that pass conditions.</returns>
        public IEnumerable<Control> GetControlsBy(Func<Control, bool> predicate) => Controls.Where(predicate);

        /// <summary>
        /// Gets the control by its name.
        /// </summary>
        /// <param name="name">Name of the control.</param>
        /// <returns>First control with given name.</returns>
        public Control? GetControlByName(string name) => GetControlBy(x => x.Name == name);

        /// <summary>
        /// Gets the control by its name or throws the exception if it doesn't exist.
        /// </summary>
        /// <param name="name">Name of the control.</param>
        /// <returns>First control that have given name.</returns>
        public Control GetRequiredControlByName(string name) => GetControlBy(x => x.Name == name).NeverNull();

        /// <summary>
        /// Count all controls on the desktop, including all children.
        /// </summary>
        /// <param name="onlyVisible">Set this to <see langword="true"/> to count only visible controls.</param>
        /// <returns>Amount of controls on the desktop.</returns>
        public int CountControls(bool onlyVisible) => Controls.Where(x => !(onlyVisible && !x.Visible)).Sum(x => 1 + (x is IContainerControl container ? container.TotalChildrenCount(onlyVisible) : 0));

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

        /// <inheritdoc/>
        public void Dispose()
        {
            ((IDisposable)renderContext).Dispose();
            Dispatcher.Cancel();
            GC.SuppressFinalize(this);
        }
    }
}