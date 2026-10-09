using Icy.Design.Editor.Panels;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class PropertiesPanelTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <TextBlock x:Name="bound" Text="{Binding Missing}"/>
              <Button x:Name="b" Width="40" Height="20"/>
            </StackPanel>
            """;

        private static (EditorTestHost Host, PropertiesPanel Panel) Create()
        {
            var host = new EditorTestHost(Page);
            var panel = new PropertiesPanel { Session = host.Session };
            host.Canvas.AddOverlay(panel);
            host.Render();
            return (host, panel);
        }

        [Fact]
        public void SelectingAnElement_ShowsItsProperties()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<Button>("b"));

                Assert.Same(host.Named<Button>("b"), panel.Grid.Target);
                Assert.IsType<MarkupPropertyAdapter>(panel.Grid.ValueAdapter);
            }
        }

        [Fact]
        public void ATypedEdit_WritesTheAttribute_AsOneUndoStep()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<Button>("b"));
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var entry = Icy.Data.PropertyGridEntry.EnumerateFor(host.Named<Button>("b")).Single(x => x.Name == "Width");

                Assert.True(adapter.TrySetValue(entry, host.Named<Button>("b"), 5f));
                Assert.True(adapter.TrySetValue(entry, host.Named<Button>("b"), 55f));

                Assert.Contains("Width=\"55\"", host.Document.Text);
                host.Document.Editor.UndoStack.Undo();
                Assert.Contains("Width=\"40\"", host.Document.Text);
            }
        }

        [Fact]
        public void AnExpressionAttribute_IsShownAsText_AndTypedEditsDontReplaceIt()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var bound = host.Named<TextBlock>("bound");
                host.Session.Select(bound);
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var text = Icy.Data.PropertyGridEntry.EnumerateFor(bound).Single(x => x.Name == "Text");

                Assert.Equal("{Binding Missing}", adapter.GetExpression(text, bound));
                Assert.True(adapter.TrySetExpression(text, bound, "{Binding Other}"));
                Assert.Contains("Text=\"{Binding Other}\"", host.Document.Text);
            }
        }

        [Fact]
        public void Enter_CommitsAnExpressionRow()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<TextBlock>("bound"));
                host.Render();
                var box = (TextBox)FindEditor(panel.Grid, "Text")!;
                host.Canvas.Focus(box);
                box.Text = "{Binding Other}";
                Assert.DoesNotContain("{Binding Other}", host.Document.Text);

                host.Input.Keyboard.RaiseKeyDown(Icy.Input.Devices.Keys.Enter);

                Assert.Contains("Text=\"{Binding Other}\"", host.Document.Text);
            }
        }

        [Fact]
        public void Reset_RemovesTheAttribute()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var button = host.Named<Button>("b");
                host.Session.Select(button);
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var height = Icy.Data.PropertyGridEntry.EnumerateFor(button).Single(x => x.Name == "Height");

                Assert.True(adapter.CanReset(height, button));
                adapter.Reset(height, button);

                Assert.DoesNotContain("Height=", host.Document.Text);
                Assert.False(adapter.CanReset(height, button));
            }
        }

        [Fact]
        public void AnUnsetProperty_GainsALocalAttributeOnFirstEdit()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var button = host.Named<Button>("b");
                host.Session.Select(button);
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var opacity = Icy.Data.PropertyGridEntry.EnumerateFor(button).Single(x => x.Name == "Opacity");

                Assert.False(adapter.CanReset(opacity, button));
                Assert.True(adapter.TrySetValue(opacity, button, 0.5f));

                Assert.Contains("Opacity=\"0.5\"", host.Document.Text);
                Assert.True(adapter.CanReset(opacity, button));
            }
        }

        [Fact]
        public void AnUndo_RefreshesTheGrid()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var button = host.Named<Button>("b");
                host.Session.Select(button);
                host.Render();
                host.Document.Editor.SetAttribute(host.IdOf(button), "Width", "70");
                Assert.Equal("70", FindEditorText(panel.Grid, "Width"));
                host.Document.Editor.UndoStack.Undo();

                Assert.Equal(40, button.Width);
                Assert.Equal("40", FindEditorText(panel.Grid, "Width"));
            }
        }

        [Fact]
        public void ClearingTheSelection_EmptiesTheGrid()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<Button>("b"));
                host.Session.Clear();

                Assert.Null(panel.Grid.Target);
            }
        }

        [Fact]
        public void ARefusedValue_ShowsTheReasonInTheStatusLine()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var button = host.Named<Button>("b");
                host.Session.Select(button);
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var content = Icy.Data.PropertyGridEntry.EnumerateFor(button).Single(x => x.Name == "Content");

                Assert.False(adapter.TrySetValue(content, button, new TextBlock()));

                Assert.Contains("no markup form", panel.StatusText);
            }
        }

        [Fact]
        public void ADetachedPanel_StopsFollowingTheSelection()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Canvas.RemoveOverlay(panel);
                host.Session.Select(host.Named<Button>("b"));

                Assert.Null(panel.Grid.Target);

                host.Canvas.AddOverlay(panel);
                Assert.Same(host.Named<Button>("b"), panel.Grid.Target);
            }
        }

        [Fact]
        public void TypingAnInvalidWidth_ShowsTheRule_AndThrowsNothing()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<Button>("b"));
                host.Render();
                var box = (TextBox)FindEditor(panel.Grid, "Width")!;
                host.Canvas.Focus(box);

                // Count only this thread's exceptions: the suite runs other test classes in parallel.
                int thread = Environment.CurrentManagedThreadId;
                int thrown = 0;
                void Count(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
                {
                    if (Environment.CurrentManagedThreadId == thread)
                        thrown++;
                }

                AppDomain.CurrentDomain.FirstChanceException += Count;
                try
                {
                    foreach (string text in new[] { "-", "-5", "1e40", "abc" })
                        box.Text = text;
                }
                finally
                {
                    AppDomain.CurrentDomain.FirstChanceException -= Count;
                }

                Assert.Equal(0, thrown);
                Assert.Equal("Enter a number.", panel.StatusText);
                Assert.Contains("Width=\"40\"", host.Document.Text);

                box.Text = "-5";
                Assert.Equal("Must be at least 0.", panel.StatusText);
            }
        }

        private static Icy.UI.UIElement? FindEditor(PropertyGrid grid, string name)
        {
            var containers = (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(grid)!;
            foreach (ItemContainer container in containers.Values)
            {
                if (container.Content is Grid row && row.Children[0] is TextBlock label && label.Text == name)
                    return row.Children[1];
            }

            return null;
        }

        private static string? FindEditorText(PropertyGrid grid, string name)
        {
            var containers = (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(grid)!;
            foreach (ItemContainer container in containers.Values)
            {
                if (container.Content is Grid row && row.Children[0] is TextBlock label && label.Text == name)
                    return (row.Children[1] as TextBox)?.Text;
            }

            return null;
        }
    }
}
