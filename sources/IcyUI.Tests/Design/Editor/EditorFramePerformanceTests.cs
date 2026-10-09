// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Text;
using Icy.Design.Editor;
using Icy.Design.Editor.Panels;
using Icy.Input.Devices;
using Xunit;
using Xunit.Abstractions;

namespace Icy.Tests.Design.Editor
{
    /// <summary>
    /// Runs timing-sensitive tests alone, after the parallel ones, so other test classes don't skew their measurements.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class TimingCollection
    {
        public const string Name = "Timing";
    }

    [Collection(TimingCollection.Name)]
    public class EditorFramePerformanceTests(ITestOutputHelper output)
    {
        [Fact]
        public void EditModeOverhead_OnA500ElementPage_IsMeasured()
        {
            var markup = new StringBuilder("<StackPanel x:Name=\"root\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\">");
            for (int i = 0; i < 500; i++)
                markup.Append("<Border Width=\"4\" Height=\"1\"/>");
            markup.Append("</StackPanel>");
            using var host = new EditorTestHost(markup.ToString(), attachSession: false);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Root, 2, 2));

            // Warm up long enough for tiered JIT to settle, and measure the detached canvas on both sides of the editor
            // run, so whichever configuration runs first isn't penalized.
            for (int i = 0; i < 500; i++)
                host.Render();
            double before = Measure(host);
            EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            frame.Session.Select(host.Root);
            double editing = Measure(host);
            frame.Dispose();
            double after = Measure(host);
            double baseline = (before + after) / 2;

            output.WriteLine($"Canvas.Render on 500 elements: {baseline:0.000} ms detached ({before:0.000}/{after:0.000}), {editing:0.000} ms in Edit mode, overhead {editing - baseline:0.000} ms (budget 0.2 ms).");
        }

        [Fact]
        public void ScopedEditModeOverhead_OnA500ElementPage_IsMeasured()
        {
            var markup = new StringBuilder("<StackPanel x:Name=\"root\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\">");
            for (int i = 0; i < 500; i++)
                markup.Append("<Border Width=\"4\" Height=\"1\"/>");
            markup.Append("</StackPanel>");
            using var host = new EditorTestHost(markup.ToString(), attachSession: false);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Root, 2, 2));

            for (int i = 0; i < 500; i++)
                host.Render();
            double before = Measure(host);
            EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design, host.Root);
            frame.Session.Select(host.Root);
            double editing = Measure(host);
            frame.Dispose();
            double after = Measure(host);
            double baseline = (before + after) / 2;

            output.WriteLine($"Scoped: Canvas.Render on 500 elements: {baseline:0.000} ms detached ({before:0.000}/{after:0.000}), {editing:0.000} ms in Edit mode, overhead {editing - baseline:0.000} ms (budget 0.2 ms).");
        }

        [Fact]
        public void AnAttributeEdit_WithAllPanelsAttached_StaysUnderTwoMilliseconds()
        {
            var markup = new StringBuilder("<StackPanel x:Name=\"root\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\">");
            for (int i = 0; i < 500; i++)
                markup.Append("<Border Width=\"4\" Height=\"1\"/>");
            markup.Append("</StackPanel>");
            using var host = new EditorTestHost(markup.ToString(), attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var outline = new OutlinePanel { Session = frame.Session, Width = 280, Height = 600 };
            var properties = new PropertiesPanel { Session = frame.Session, Width = 280, Height = 600 };
            var bar = new EditorCommandBar { Session = frame.Session };
            host.Canvas.AddOverlay(outline);
            host.Canvas.AddOverlay(properties);
            host.Canvas.AddOverlay(bar);
            var target = (Icy.UI.UIElement)((Icy.UI.Controls.StackPanel)host.Root).Children[250];
            frame.Session.Select(target);
            host.Render();
            Icy.Design.NodeId node = host.IdOf(target);

            // Warm up long enough for tiered JIT to settle; a short warm-up measures the JIT, not the panels.
            for (int i = 0; i < 200; i++)
            {
                host.Document.Editor.SetAttribute(node, "Width", (5 + i).ToString(System.Globalization.CultureInfo.InvariantCulture));
                _ = outline.Roots;
            }

            const int Edits = 200;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < Edits; i++)
            {
                host.Document.Editor.SetAttribute(node, "Width", (10 + (i % 7)).ToString(System.Globalization.CultureInfo.InvariantCulture));

                // The outline coalesces edits until the next layout pass; reading it here charges its rebuild to every
                // edit, the worst case of one edit per frame.
                _ = outline.Roots;
            }
            double mean = watch.Elapsed.TotalMilliseconds / Edits;

            output.WriteLine($"SetAttribute on a 500-element page with Outline, Properties and the command bar attached, outline rebuilt every edit: {mean:0.000} ms per edit (budget 2 ms).");
            Assert.True(mean < 2, $"{mean:0.000} ms per edit");
            bar.Session = null;
        }

        private static double Measure(EditorTestHost host)
        {
            for (int i = 0; i < 60; i++)
                host.Render();

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 60; i++)
                host.Render();
            return watch.Elapsed.TotalMilliseconds / 60;
        }
    }
}
