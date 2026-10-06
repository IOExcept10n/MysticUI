// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Editor;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorSessionTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Border x:Name="box" Width="100" Height="40"/>
              <Button x:Name="ok" Width="100" Height="30">OK</Button>
              <Border x:Name="frame" Width="120" Height="50">
                <TextBlock x:Name="inner" Text="Inside"/>
              </Border>
            </StackPanel>
            """;

        [Fact]
        public void Attach_StartsInEditModeWithNoSelection()
        {
            using var host = new EditorTestHost(Page);

            Assert.Equal(EditorMode.Edit, host.Session.Mode);
            Assert.Null(host.Session.Selection);
            Assert.False(host.Canvas.IsKeyboardNavigationEnabled);
        }

        [Fact]
        public void ResolveSelectable_PicksTheOwnerOfTemplateContent()
        {
            using var host = new EditorTestHost(Page);
            var ok = host.Named<Button>("ok");
            UIElement? hit = host.Canvas.HitTest(host.At(ok, 50, 15));

            Assert.NotNull(hit);
            Assert.Same(ok, host.Session.ResolveSelectable(hit));
        }

        [Fact]
        public void ResolveSelectable_PicksTheDeepestTrackedElement()
        {
            using var host = new EditorTestHost(Page);
            var inner = host.Named<TextBlock>("inner");

            Assert.Same(inner, host.Session.ResolveSelectable(host.Canvas.HitTest(host.At(inner))));
        }

        [Fact]
        public void HitTest_SkipsTheEditorsOwnLayersButNotPagePopups()
        {
            using var host = new EditorTestHost(Page);
            var layer = new UIElement { Width = 800, Height = 600 };
            host.Canvas.AddOverlay(layer);
            host.Session.OwnLayers.Add(layer);
            var popup = new UIElement { Width = 50, Height = 50, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(600, 400, 0, 0) };
            host.Canvas.AddOverlay(popup);
            host.Render();

            Assert.Same(host.Named<Border>("box"), host.Session.HitTest(host.At(host.Named<Border>("box"))));
            Assert.Same(popup, host.Session.HitTest(new System.Drawing.Point(610, 410)));
        }

        [Fact]
        public void Select_SetsTheSelectionAndRaisesSelectionChanged()
        {
            using var host = new EditorTestHost(Page);
            var box = host.Named<Border>("box");
            int changed = 0;
            host.Session.SelectionChanged += (_, _) => changed++;

            Assert.True(host.Session.Select(box));

            Assert.Equal(new EditorSelection(host.Document, host.IdOf(box), box), host.Session.Selection);
            Assert.Equal(1, changed);
        }

        [Fact]
        public void Select_RefusesUntrackedElements()
        {
            using var host = new EditorTestHost(Page);

            Assert.False(host.Session.Select(new Border()));
            Assert.Null(host.Session.Selection);
        }

        [Fact]
        public void TheSelection_FollowsARebuild()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("box"));

            // A changed element type rebuilds it in place (10.2), keeping its id.
            host.Document.ApplyText(host.Document.Text.Replace("<Border x:Name=\"box\" Width=\"100\" Height=\"40\"/>", "<Button x:Name=\"box\" Width=\"100\" Height=\"40\"/>", StringComparison.Ordinal));

            Assert.IsType<Button>(host.Session.Selection!.Instance);
            Assert.Same(host.Named<Button>("box"), host.Session.Selection.Instance);
        }

        [Fact]
        public void TheSelection_SurvivesAppliedText()
        {
            using var host = new EditorTestHost(Page);
            var box = host.Named<Border>("box");
            host.Session.Select(box);

            host.Document.ApplyText(host.Document.Text.Replace("Height=\"40\"", "Height=\"45\"", StringComparison.Ordinal));

            Assert.Same(box, host.Session.Selection!.Instance);
        }

        [Fact]
        public void TheSelection_ClearsWhenItsElementIsRemoved()
        {
            using var host = new EditorTestHost(Page);
            var box = host.Named<Border>("box");
            host.Session.Select(box);

            host.Document.Editor.RemoveElement(host.IdOf(box));

            Assert.Null(host.Session.Selection);
        }

        [Fact]
        public void EditMode_ClearsFocus_AndInteractRestoresIt()
        {
            using var host = new EditorTestHost(Page);
            var ok = host.Named<Button>("ok");
            host.Session.Mode = EditorMode.Interact;
            host.Canvas.Focus(ok);

            host.Session.Mode = EditorMode.Edit;
            Assert.Null(host.Canvas.FocusedElement);
            Assert.False(host.Canvas.IsKeyboardNavigationEnabled);

            host.Session.Mode = EditorMode.Interact;
            Assert.Same(ok, host.Canvas.FocusedElement);
            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);
        }

        [Fact]
        public void Dispose_RestoresNavigationAndFocus()
        {
            var host = new EditorTestHost(Page);
            host.Session.Mode = EditorMode.Interact;
            host.Canvas.Focus(host.Named<Button>("ok"));
            host.Session.Mode = EditorMode.Edit;

            host.Dispose();

            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);
            Assert.Same(host.Named<Button>("ok"), host.Canvas.FocusedElement);
        }

        [Fact]
        public void IsBlocked_FollowsTheSelectedDocumentsSyncState()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("box"));
            int changed = 0;
            host.Session.BlockedChanged += (_, _) => changed++;

            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);
            Assert.True(host.Session.IsBlocked);
            Assert.NotNull(host.Session.BlockedReason);

            host.Document.ApplyText(host.Document.Text + "</StackPanel>");
            Assert.False(host.Session.IsBlocked);
            Assert.Equal(2, changed);
        }
    }
}
