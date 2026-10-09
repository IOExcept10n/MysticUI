using Icy.Data;
using Icy.Design.Editor.Panels;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Icy.Tests.Design.Editor
{
    // Every browsable property of the common controls gets hostile values, broken expressions and a Reset through the
    // Properties panel's adapter, with a frame rendered after each: nothing may escape to the game loop.
    public class PropertyEditRobustnessTests(ITestOutputHelper output)
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="e0" Width="40" Height="20"/>
              <Slider x:Name="e1" Minimum="0" Maximum="10" Value="5"/>
              <TextBlock x:Name="e2" Text="x"/>
              <TextBox x:Name="e3" Text="x"/>
              <Border x:Name="e4" Width="10" Height="10"/>
              <ProgressBar x:Name="e5"/>
              <ScrollViewer x:Name="e6"/>
              <ComboBox x:Name="e7"/>
              <CheckBox x:Name="e8"/>
              <Grid x:Name="e9"/>
              <SplitPane x:Name="e10"/>
              <Expander x:Name="e11"/>
              <ListBox x:Name="e12"/>
              <TabControl x:Name="e13"/>
              <WrapGrid x:Name="e14"/>
              <Image x:Name="e15"/>
              <ColorPickerButton x:Name="e16"/>
              <TreeView x:Name="e17"/>
            </StackPanel>
            """;

        // Button, Slider, TextBox, ScrollViewer, ComboBox, Grid, ListBox and ColorPickerButton: plain and content
        // controls, a range, text input, a scroller, popups and items controls. The page declares more for ad-hoc runs.
        private static readonly int[] Sample = [0, 1, 3, 6, 7, 9, 12, 16];

        private static IEnumerable<object?> Hostile(Type t)
        {
            if (t == typeof(float)) return [-1f, 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1e30f, -1e30f];
            if (t == typeof(double)) return [-1d, 0d, double.NaN, double.PositiveInfinity, 1e300];
            if (t == typeof(int)) return [-1, 0, int.MaxValue, int.MinValue];
            if (t == typeof(string)) return ["", "{", "}", "<", "\"", "{Binding", new string('x', 200)];
            if (t == typeof(bool)) return [true, false];
            if (t.IsEnum) return [Enum.ToObject(t, 999), Enum.ToObject(t, -1)];
            if (t == typeof(Thickness)) return [new Thickness(-5), new Thickness(int.MaxValue)];
            if (t == typeof(System.Drawing.Color)) return [System.Drawing.Color.Transparent];
            return [];
        }

        [Fact]
        public void AMinimumAboveTheMaximum_LeavesTheNextFrameWorking()
        {
            using var host = new EditorTestHost(Page);
            var panel = new PropertiesPanel { Session = host.Session, Width = 300, Height = 600 };
            host.Canvas.AddOverlay(panel);
            var border = host.Named<Border>("e4");
            host.Session.Select(border);
            host.Render();
            var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
            var entries = PropertyGridEntry.EnumerateFor(border);

            Assert.True(adapter.TrySetValue(entries.Single(x => x.Name == "MaxWidth"), border, 10f));
            Assert.True(adapter.TrySetValue(entries.Single(x => x.Name == "MinWidth"), border, 50f));
            host.Render();

            Assert.Equal(50, border.ActualBounds.Width);
        }

        private void RenderGuarded(EditorTestHost host, string after, ref int escaped)
        {
            try
            {
                host.Render();
            }
            catch (Exception ex)
            {
                escaped++;
                output.WriteLine($"ESCAPE render after {after}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        [Fact]
        public void HostilePropertyEdits_NeverEscape_NorBreakLaterFrames()
        {
            using var host = new EditorTestHost(Page);
            var panel = new PropertiesPanel { Session = host.Session, Width = 300, Height = 600 };
            host.Canvas.AddOverlay(panel);
            host.Render();
            int escaped = 0, tried = 0, refused = 0;
            foreach (int i in Sample)
            {
                var element = host.Named<UIElement>("e" + i);
                try { host.Session.Select(element); host.Render(); }
                catch (Exception ex) { output.WriteLine($"ESCAPE select {element.GetType().Name}: {ex.GetType().Name}: {ex.Message}"); escaped++; continue; }
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                foreach (PropertyGridEntry entry in PropertyGridEntry.EnumerateFor(element))
                {
                    RenderGuarded(host, $"{element.GetType().Name}.{entry.Name}", ref escaped);
                    foreach (object? value in Hostile(entry.PropertyType))
                    {
                        tried++;
                        try
                        {
                            if (!adapter.TrySetValue(entry, element, value)) refused++;
                        }
                        catch (Exception ex)
                        {
                            escaped++;
                            output.WriteLine($"ESCAPE {element.GetType().Name}.{entry.Name} = {value}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
                            output.WriteLine("   at " + string.Join(" <- ", (ex.StackTrace ?? "").Split('\n').Take(4).Select(s => s.Trim())));
                        }
                    }
                }
            }
            string[] exprs = ["{Binding", "{StaticResource Missing}", "{Bogus a=b}"];
            foreach (int i in Sample)
            {
                var element = host.Named<UIElement>("e" + i);
                RenderGuarded(host, "expressions", ref escaped);
                host.Session.Select(element);
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                foreach (PropertyGridEntry entry in PropertyGridEntry.EnumerateFor(element))
                {
                    foreach (string text in exprs)
                    {
                        tried++;
                        try { adapter.TrySetExpression(entry, element, text); if (adapter.CanReset(entry, element)) adapter.Reset(entry, element); }
                        catch (Exception ex) { escaped++; output.WriteLine($"ESCAPE expr {element.GetType().Name}.{entry.Name} = {text}: {ex.GetType().Name}: {ex.Message.Split((char)10)[0]}"); }
                    }
                }
            }

            RenderGuarded(host, "last edit", ref escaped);
            output.WriteLine($"Tried {tried} edits, {refused} refused, {escaped} escaped.");
            Assert.Equal(0, escaped);
        }
    }
}
