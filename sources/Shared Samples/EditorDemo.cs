// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.IO;
using Icy.Configuration;
using Icy.Design;
using Icy.Design.Editor;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A page to edit with <see cref="EditorFrame"/>: a stack, a grid with spans, a free panel for margin moves, a text
    /// box and a combo box. The manual check for the editor overlay (Phase 10.3).
    /// </summary>
    public static class EditorDemo
    {
        /// <summary>
        /// The page the demo edits.
        /// </summary>
        public const string Markup =
            """
            <StackPanel x:Name="page" Orientation="Vertical" HorizontalAlignment="Left" VerticalAlignment="Top">
              <TextBlock x:Name="title" FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,8">Edit me: select, drag, resize</TextBlock>
              <StackPanel x:Name="stack" Orientation="Horizontal" Margin="0,0,0,8">
                <Button Padding="12,6" Margin="0,0,8,0">One</Button>
                <Button Padding="12,6" Margin="0,0,8,0">Two</Button>
                <Button Padding="12,6">Three</Button>
              </StackPanel>
              <Grid x:Name="grid" Width="360" Height="140" Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="120"/>
                  <ColumnDefinition Width="120"/>
                  <ColumnDefinition Width="120"/>
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                  <RowDefinition Height="70"/>
                  <RowDefinition Height="70"/>
                </Grid.RowDefinitions>
                <Border Background="LightCoral" Grid.ColumnSpan="2"/>
                <Border Background="LightSkyBlue" Grid.Column="2" Grid.RowSpan="2"/>
                <Border Background="LightGreen" Grid.Row="1"/>
              </Grid>
              <Panel x:Name="free" Width="360" Height="100" Margin="0,0,0,8">
                <Border Background="Gold" Width="60" Height="30" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="10,10,0,0"/>
                <Border Background="Plum" Width="60" Height="30" HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,10,10"/>
              </Panel>
              <TextBox x:Name="text" Width="200" Margin="0,0,0,8"/>
              <ComboBox x:Name="combo" Width="160"/>
            </StackPanel>
            """;

        /// <summary>
        /// Builds the demo: the tracked page, an "Edit this page" toggle that attaches an editor scoped to the page, a Save
        /// button and a status line.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The demo's root element.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;

            DesignSession session = DesignDemo.SessionFor(configuration);
            UIElement page = new MarkupLoader(configuration).Load(Markup, nameof(EditorDemo));
            DesignDocument document = session.FindDocument(page, out _)!;
            ((ComboBox)MarkupNameScope.GetScope(page)!.Find("combo")!).ItemsSource = new[] { "One", "Two", "Three" };

            var status = new TextBlock { Text = "Press \"Edit this page\", then click, drag and resize. Ctrl+Shift+E toggles Interact mode.", Margin = new Thickness(0, 6, 0, 0) };
            var editLabel = new TextBlock { Text = "Edit this page" };
            var edit = new Button { Content = editLabel, Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button { Content = new TextBlock { Text = "Save" }, Padding = new Thickness(12, 6) };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            toolbar.Children.Add(edit);
            toolbar.Children.Add(save);

            var root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20),
            };
            root.Children.Add(page);
            root.Children.Add(toolbar);
            root.Children.Add(status);

            EditorFrame? frame = null;
            void Detach()
            {
                frame?.Dispose();
                frame = null;
                editLabel.Text = "Edit this page";
            }

            edit.Command = new DemoCommand(() =>
            {
                if (frame != null)
                {
                    Detach();
                    return;
                }

                if (root.Canvas is not { } canvas)
                    return;

                // A canvas takes one editor; in the samples shell, that may be the shell's own.
                if (EditorSession.FindAttached(canvas) != null)
                {
                    status.Text = "The shell's editor is on; press F4 to stop it.";
                    return;
                }

                // Scoped to the page, so this demo's own buttons stay clickable while editing.
                frame = EditorFrame.Attach(canvas, session, page);
                editLabel.Text = "Stop editing";
            });
            save.Command = new DemoCommand(() =>
            {
                string path = Path.Combine(Path.GetTempPath(), "IcyEditorDemo.xml");
                document.SaveAs(path);
                status.Text = $"Saved to {path}";
            });

            // The samples shell detaches a demo when you switch away: an editor left in Edit mode would keep the input.
            root.Detached += (_, _) => Detach();

            return root;
        }

        private sealed class DemoCommand(Action execute) : System.Windows.Input.ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute();
        }
    }
}
