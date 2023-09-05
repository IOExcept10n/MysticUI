using MysticUI.Extensions;
using Stride.Core.Mathematics;
using Stride.Input;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a draggable UI element with the header and contents.
    /// </summary>
    public class Window : SingleItemControlBase<StackPanel>
    {
        /// <summary>
        /// Gets the name of the default <see cref="CloseButton"/> style.
        /// </summary>
        public const string CloseButtonStyleName = "WindowCloseButtonStyle";

        private readonly TextBlock titleBlock;
        private Control? content;
        private Control? previousFocusedControl;

        private bool isWindowPlaced;

        /// <summary>
        /// Gets or sets the title of the window.
        /// </summary>
        public string? Title
        {
            get => titleBlock.Text;
            set => titleBlock.Text = value;
        }

        /// <summary>
        /// Gets the grid for the window header.
        /// </summary>
        public Grid HeaderGrid { get; private set; } = null!;

        /// <summary>
        /// Gets the button to close the window.
        /// </summary>
        public Button CloseButton { get; private set; } = null!;

        /// <summary>
        /// Gets or sets the content of the window.
        /// </summary>
        public Control? Content
        {
            get => content;
            set
            {
                if (value == content) return;
                if (content != null) Child.Remove(content);
                if (value != null) Child.Children.Insert(1, value);
                content = value;
            }
        }

        /// <summary>
        /// Occurs when the window is about to closing. This action can cancel the closure process.
        /// </summary>
        public event EventHandler<CancellableEventArgs<object>>? OnClosing;

        /// <summary>
        /// Occurs when the window is closed at all.
        /// </summary>
        public event EventHandler? OnClosed;

        /// <summary>
        /// Creates a new instance of the <see cref="Window"/> class.
        /// </summary>
        public Window()
        {
            AcceptFocus = true;
            IsModal = true;
            DragDirection = DragDirection.Both;
            titleBlock = new TextBlock().WithDefaultStyle();
            ResetChild();
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Child.Spacing = 0;
        }

        /// <summary>
        /// Aligns the window on the center of the parent control.
        /// </summary>
        public void CenterOnDesktop()
        {
            X = (ContainerBounds.Width - Width) / 2;
            Y = (ContainerBounds.Height - Height) / 2;
        }

        /// <summary>
        /// Shows the window on the desktop at the given point.
        /// </summary>
        /// <param name="desktop">A desktop to open on.</param>
        /// <param name="position">A position to show the window at.</param>
        public void Show(Desktop desktop, Point? position = null)
        {
            IsModal = false;
            ShowInternal(desktop, position);
        }

        /// <summary>
        /// Shows the window on the desktop center with blocking focus until the end of the presentation.
        /// </summary>
        /// <param name="desktop">A desktop to open on.</param>
        /// <param name="position">A position to show the window at.</param>
        public void ShowModal(Desktop desktop, Point? position = null)
        {
            IsModal = true;
            ShowInternal(desktop, position);
            previousFocusedControl = desktop.CurrentFocusedControl;
            if (AcceptFocus)
            {
                desktop.CurrentFocusedControl = this;
            }
        }

        /// <summary>
        /// Closes the window.
        /// </summary>
        public virtual void Close()
        {
            if (Desktop == null)
            {
                // It's already closed.
                return;
            }

            if (OnClosing != null)
            {
                var args = new CancellableEventArgs<object>();
                OnClosing(this, args);
                if (args.Cancel) return;
            }

            if (IsModal)
            {
                Desktop.CurrentFocusedControl = previousFocusedControl;
            }

            if (Desktop.Controls.Contains(this))
            {
                RemoveFromDesktop();
            }
            else
            {
                (Parent as IContainerControl)?.Remove(this);
            }

            OnClosed?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        protected internal override void ResetChildInternal()
        {
            Child = new() { OverrideChildrenAlignment = false };
            HeaderGrid = new()
            {
                ColumnSpacing = 8,
                VerticalAlignment = VerticalAlignment.Top
            };
            DragHandle = HeaderGrid;
            HeaderGrid.ColumnDefinitions.Add(new() { Width = GridLength.OneStar });
            HeaderGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            HeaderGrid.Add(titleBlock);
            CloseButton = new()
            {
                StyleName = CloseButtonStyleName,
                GridColumn = 1,
                IsCancel = true,
                DialogResult = DialogResult.Cancel,
            };
            CloseButton.Click += (_, __) => Close();
            HeaderGrid.Add(CloseButton);
            Child.Add(HeaderGrid);
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            base.ArrangeInternal();
            if (!isWindowPlaced)
            {
                CenterOnDesktop();
                isWindowPlaced = true;
            }
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchDown()
        {
            BringToFront();
            base.OnTouchDown();
            return true;
        }

        /// <inheritdoc/>
        protected internal override void OnKeyDown(Keys key)
        {
            base.OnKeyDown(key);
            if (key == Keys.Cancel)
                CloseButton.PerformClick();
        }

        private void BringToFront()
        {
            if (Desktop != null)
            {
                Desktop.RemoveControl(this);
                Desktop.Attach(this);
            }
        }

        private void ShowInternal(Desktop desktop, Point? position = null)
        {
            Desktop = desktop;
            Desktop.Attach(this);
            if (position != null)
            {
                X = position.Value.X;
                Y = position.Value.Y;
                isWindowPlaced = true;
            }
        }
    }
}