// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Design.Editor.Panels;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The in-game editor: an <see cref="EditorFrame"/> over the game's UI, an Outline and Toolbox dock on the left, a
    /// Properties dock on the right and a command bar on top, shown and hidden with one key.
    /// </summary>
    /// <remarks>
    /// <para>Using it in your game:</para>
    /// <code>
    /// #if DEBUG
    /// DesignSession design = DesignSession.Attach(configuration);           // before loading any screen
    /// if (DesignSession.FindSourceRoot("MyGame.csproj", "Assets/UI") is { } root)
    ///     design.UseSourceRoot(root);
    /// EditorOverlay overlay = EditorOverlay.Attach(canvas, design);          // F4 toggles it
    /// #endif
    /// </code>
    /// <para>
    /// Reference <c>IcyUI.Design</c> from the game only in Debug builds, so release builds don't carry it:
    /// <c>&lt;ProjectReference Include="..\IcyUI.Design\IcyUI.Design.csproj" Condition="'$(Configuration)' == 'Debug'" /&gt;</c>.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// The toggle key is registered without handling the gesture, so a game's own command for the same key still runs.
    /// </description></item>
    /// <item><description>
    /// Hiding detaches the frame, the docks and the bar. The <see cref="DesignSession"/> keeps tracking, and every
    /// document keeps its edits and undo history.
    /// </description></item>
    /// <item><description>
    /// The docks sit above the frame's capture layer, so clicking them never selects the game's elements. Each dock
    /// collapses to a thin strip so the game stays visible.
    /// </description></item>
    /// <item><description>
    /// A canvas takes one editor at a time: while another one is attached (an <see cref="EditorWorkspace"/>, say),
    /// <see cref="Show"/> stays hidden and <see cref="LastShowError"/> says why.
    /// </description></item>
    /// </list>
    /// </remarks>
    public sealed class EditorOverlay : IDisposable
    {
        private const int BarHeight = 32;
        private const float CollapsedWidth = 24;
        private static readonly Color PanelBackground = Color.FromArgb(235, 25, 25, 30);

        private readonly Canvas canvas;
        private readonly DesignSession design;
        private readonly EditorOverlayOptions options;
        private readonly EditorCommand toggle;
        private readonly List<UIElement> docks = [];
        private readonly List<Control> panels = [];
        private EditorFrame? frame;
        private Border? barHost;
        private Border? leftDock;
        private Border? rightDock;
        private Rectangle region;
        private bool disposed;

        private EditorOverlay(Canvas canvas, DesignSession design, EditorOverlayOptions options)
        {
            this.canvas = canvas;
            this.design = design;
            this.options = options;
            toggle = new EditorCommand(() => !disposed, Toggle);
        }

        /// <summary>
        /// Occurs when <see cref="IsShown"/> changed.
        /// </summary>
        public event EventHandler? IsShownChanged;

        /// <summary>
        /// Gets a value indicating whether the editor is shown.
        /// </summary>
        public bool IsShown => frame != null;

        /// <summary>
        /// Gets the editor session while the overlay is shown, or <see langword="null"/>.
        /// </summary>
        public EditorSession? Session => frame?.Session;

        /// <summary>
        /// Gets why the last <see cref="Show"/> left the overlay hidden, or <see langword="null"/>.
        /// </summary>
        public string? LastShowError { get; private set; }

        internal IReadOnlyList<UIElement> Docks => docks;

        internal IReadOnlyList<Control> Panels => panels;

        internal UIElement? FrameToolbarForTest => frame?.Toolbar;

        /// <summary>
        /// Prepares an editor overlay for <paramref name="canvas"/>, hidden until <see cref="Show"/> or the toggle key.
        /// </summary>
        /// <param name="canvas">The game's canvas.</param>
        /// <param name="design">The session tracking the game's screens.</param>
        /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
        /// <returns>The overlay; dispose it to hide it and release the toggle key.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="canvas"/> or <paramref name="design"/> is <see langword="null"/>.</exception>
        public static EditorOverlay Attach(Canvas canvas, DesignSession design, EditorOverlayOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(design);
            options ??= new EditorOverlayOptions();
            if (options.SourceRoot is { Length: > 0 } root)
                design.UseSourceRoot(root);

            var overlay = new EditorOverlay(canvas, design, options);
            if (options.ToggleKey is { } key)
                design.Configuration.Input.Events.RegisterCommand(overlay.toggle, key, null, handlesGesture: false);
            return overlay;
        }

        /// <summary>
        /// Shows the editor. Does nothing when it's already shown; stays hidden, with <see cref="LastShowError"/> set,
        /// when another editor owns the canvas or the scope isn't on it.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The overlay was disposed.</exception>
        public void Show()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (frame != null)
                return;

            if (EditorSession.FindAttached(canvas) != null)
            {
                LastShowError = "Another editor is attached to this canvas.";
                return;
            }

            try
            {
                frame = EditorFrame.Attach(canvas, design, options.Scope);
            }
            catch (ArgumentException ex)
            {
                LastShowError = ex.Message;
                return;
            }

            frame.ShowsToolbar = false;
            EditorSession session = frame.Session;

            var bar = new EditorCommandBar { Session = session, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
            barHost = new Border
            {
                Background = new SolidColorBrush(PanelBackground),
                Height = BarHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = bar,
            };

            var outline = new OutlinePanel { Session = session };
            var toolbox = new ToolboxPanel { Session = session };
            var properties = new PropertiesPanel { Session = session };
            var tabs = new TabControl
            {
                ItemsSource = new List<object>
                {
                    new TabItem { Header = "Outline", Content = outline },
                    new TabItem { Header = "Toolbox", Content = toolbox },
                },
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            leftDock = CreateDock(tabs, HorizontalAlignment.Left);
            rightDock = CreateDock(properties, HorizontalAlignment.Right);
            docks.Add(barHost);
            docks.Add(leftDock);
            docks.Add(rightDock);
            panels.AddRange([bar, outline, toolbox, properties]);
            foreach (UIElement dock in docks)
            {
                canvas.AddOverlay(dock);
                frame.CompanionLayers.Add(dock);
            }

            frame.RegionChanged += OnRegionChanged;
            OnRegionChanged(session.Region());

            LastShowError = null;
            IsShownChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Hides the editor: detaches the frame, the docks and the bar. Edits and undo history stay with the documents.
        /// </summary>
        public void Hide()
        {
            if (frame == null)
                return;

            foreach (UIElement dock in docks)
                canvas.RemoveOverlay(dock);
            foreach (Control panel in panels)
            {
                switch (panel)
                {
                    case EditorCommandBar bar:
                        bar.Session = null;
                        break;
                    case OutlinePanel outline:
                        outline.Session = null;
                        outline.Document = null;
                        break;
                    case ToolboxPanel toolbox:
                        toolbox.Session = null;
                        break;
                    case PropertiesPanel properties:
                        properties.Session = null;
                        break;
                }
            }

            docks.Clear();
            panels.Clear();
            barHost = leftDock = rightDock = null;
            frame.RegionChanged -= OnRegionChanged;
            frame.Dispose();
            frame = null;
            IsShownChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Shows the editor when it's hidden, and hides it when it's shown.
        /// </summary>
        public void Toggle()
        {
            if (IsShown)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// Hides the editor and releases the toggle key.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            Hide();
            if (options.ToggleKey is { } key)
                design.Configuration.Input.Events.UnregisterCommand(toggle, key);
            disposed = true;
        }

        /// <summary>
        /// Places the bar along the top of the editor's region and the docks down its sides, below the bar. The docks
        /// are sized explicitly, so nothing depends on overflow shrinking.
        /// </summary>
        /// <param name="value">The region, in surface units; empty hides the bar and the docks.</param>
        private void OnRegionChanged(Rectangle value)
        {
            region = value;
            Place();
        }

        private void Place()
        {
            if (barHost == null || leftDock == null || rightDock == null)
                return;

            bool visible = !region.IsEmpty;
            foreach (UIElement dock in docks)
                dock.IsVisible = visible;
            if (!visible)
                return;

            int dockHeight = Math.Max(0, region.Height - BarHeight);
            barHost.Margin = new Thickness(region.X, region.Y, 0, 0);
            barHost.Width = region.Width;
            leftDock.Margin = new Thickness(region.X, region.Y + BarHeight, 0, 0);
            leftDock.Height = dockHeight;
            rightDock.Margin = new Thickness(region.Right - (int)rightDock.Width, region.Y + BarHeight, 0, 0);
            rightDock.Height = dockHeight;
        }

        private Border CreateDock(UIElement content, HorizontalAlignment side)
        {
            var dock = new Border
            {
                Background = new SolidColorBrush(PanelBackground),
                Width = options.DockWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            var collapse = new TextBlock { Text = side == HorizontalAlignment.Left ? "<" : ">" };
            var layout = new UI.Controls.Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            var button = new Button
            {
                Content = collapse,
                Padding = new Thickness(6, 1),
                HorizontalAlignment = side == HorizontalAlignment.Left ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            };
            button.Command = new EditorCommand(() => true, () =>
            {
                bool collapsed = content.IsVisible;
                content.IsVisible = !collapsed;
                dock.Width = collapsed ? CollapsedWidth : options.DockWidth;
                collapse.Text = collapsed == (side == HorizontalAlignment.Left) ? ">" : "<";
                Place();
            });
            UI.Controls.Grid.SetRow(content, 1);
            layout.Children.Add(button);
            layout.Children.Add(content);
            dock.Child = layout;
            return dock;
        }
    }
}
