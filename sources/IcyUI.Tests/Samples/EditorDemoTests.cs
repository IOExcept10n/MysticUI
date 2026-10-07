// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.Design.Editor;
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
        public void RemovingTheDemo_DetachesTheEditor()
        {
            (Canvas canvas, UIElement root) = Build();
            FindButton(root, "Edit this page").Command!.Execute(null);

            canvas.Remove(root);

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

        [Fact]
        public void TheEditor_IsScopedToThePage()
        {
            (Canvas canvas, UIElement root, _) = BuildWithInput();

            FindButton(root, "Edit this page").Command!.Execute(null);

            Assert.Same(((StackPanel)root).Children[0], EditorSession.FindAttached(canvas)!.Scope);
        }

        [Fact]
        public void TheDemosButtons_StillWork_InEditMode()
        {
            (Canvas canvas, UIElement root, FakeInputSystem input) = BuildWithInput();
            FindButton(root, "Edit this page").Command!.Execute(null);
            for (int i = 0; i < 3; i++)
                canvas.Render();

            Tap(input, FindButton(root, "Stop editing"));

            Assert.Null(EditorSession.FindAttached(canvas));
            Assert.Empty(canvas.Overlays);
        }

        [Fact]
        public void TheEditButton_Refuses_WhileAnotherEditorHoldsTheCanvas()
        {
            (Canvas canvas, UIElement root, _) = BuildWithInput();
            using EditorSession other = EditorSession.Attach(DesignDemo.SessionFor(canvas.Configuration), canvas);

            FindButton(root, "Edit this page").Command!.Execute(null);

            Assert.Same(other, EditorSession.FindAttached(canvas));
            Assert.Empty(canvas.Overlays);
            Assert.Contains(root.EnumerateVisualSubtree().OfType<TextBlock>(), t => t.Text == "The shell's editor is on; press F4 to stop it.");
        }

        private static (Canvas Canvas, UIElement Root, FakeInputSystem Input) BuildWithInput()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            UIElement root = EditorDemo.Build(configuration, "Airfool");
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(root);
            canvas.Render();
            return (canvas, root, input);
        }

        private static void Tap(FakeInputSystem input, UIElement element) =>
            input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(element.PointToScreen(new System.Numerics.Vector2(5, 5)), 1));

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
