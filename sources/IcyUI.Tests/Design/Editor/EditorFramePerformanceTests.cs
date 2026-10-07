// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Text;
using Icy.Design.Editor;
using Icy.Input.Devices;
using Xunit;
using Xunit.Abstractions;

namespace Icy.Tests.Design.Editor
{
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
