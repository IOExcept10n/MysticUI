// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    public class EditorDemoTests
    {
        [Fact]
        public void TheEditButton_AttachesAndDetachesTheFrame()
        {
            (Canvas canvas, UIElement root) = Build();
            Button edit = FindButton(root, "Edit this page");

            edit.Command!.Execute(null);
            int withEditor = canvas.Overlays.Count;
            FindButton(root, "Stop editing").Command!.Execute(null);

            Assert.Equal(2, withEditor);
            Assert.Empty(canvas.Overlays);
        }

        [Fact]
        public void HidingTheDemo_DetachesTheEditor()
        {
            (Canvas canvas, UIElement root) = Build();
            FindButton(root, "Edit this page").Command!.Execute(null);

            root.IsVisible = false;

            Assert.Empty(canvas.Overlays);
            Assert.True(canvas.IsKeyboardNavigationEnabled);
        }

        [Fact]
        public void ThePage_IsTrackedByTheSharedSession()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var root = (StackPanel)EditorDemo.Build(configuration, "Airfool");

            Assert.NotNull(DesignDemo.SessionFor(configuration).FindDocument(root.Children[0], out _));
        }

        private static (Canvas Canvas, UIElement Root) Build()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            UIElement root = EditorDemo.Build(configuration, "Airfool");
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(root);
            canvas.Render();
            return (canvas, root);
        }

        private static Button FindButton(UIElement root, string text) =>
            root.EnumerateVisualSubtree().OfType<Button>().First(x => x.Content is TextBlock { Text: var label } && label == text);
    }
}
