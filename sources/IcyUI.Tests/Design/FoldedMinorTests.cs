// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    /// <summary>
    /// 10.1 minors folded into 10.2 because "the text always wins" depends on them.
    /// </summary>
    public class FoldedMinorTests
    {
        // M4: a game setter throwing a non-markup exception must be a failed edit, not a crash, and the fast path must
        // put back every copy it already changed.
        [Fact]
        public void SetterException_OnTheFastPath_RollsBackEveryCopy()
        {
            using var host = new DesignTestHost();
            string page = "<StackPanel x:Name=\"root\"><ThrowingBox x:Name=\"t\" Count=\"5\"/></StackPanel>";
            (UIElement first, DesignDocument document) = host.Load(page, "same.xml");
            (UIElement second, _) = host.Load(page, "same.xml");

            EditResult result = document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<ThrowingBox>(first, "t")), "Count", "-1");

            Assert.False(result.Succeeded);
            Assert.Equal(5, DesignTestHost.Named<ThrowingBox>(first, "t").Count);
            Assert.Equal(5, DesignTestHost.Named<ThrowingBox>(second, "t").Count);
            Assert.Equal(page, document.Text);
        }

        [Fact]
        public void SetterException_OnTheNormalPath_IsAFailedEdit()
        {
            using var host = new DesignTestHost();
            string page = "<StackPanel x:Name=\"root\"><ThrowingBox x:Name=\"t\" Count=\"5\"/></StackPanel>";
            (UIElement root, DesignDocument document) = host.Load(page);

            // "{}" escapes the value, which keeps the edit off the fast path.
            EditResult result = document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<ThrowingBox>(root, "t")), "Count", "{}-1");

            Assert.False(result.Succeeded);
            Assert.Equal(5, DesignTestHost.Named<ThrowingBox>(root, "t").Count);
            Assert.Equal(page, document.Text);
        }

        // M1: a page loaded while fast-path values are pending must correlate against the current text.
        [Fact]
        public void ALoadWhileValuesArePending_RecordsTheLaterAttributes()
        {
            using var host = new DesignTestHost();
            (UIElement first, DesignDocument document) = host.Load("<StackPanel><Border x:Name=\"b\" Width=\"10\" Height=\"5\"/></StackPanel>", "same.xml");
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(first, "b")), "Width", "12345");

            (UIElement second, DesignDocument again) = host.Load(document.Text, "same.xml");

            Assert.Same(document, again);
            Assert.True(document.Map.TryGetEntry(DesignTestHost.Named<Border>(second, "b"), out var entry));
            Assert.True(entry!.Members.ContainsKey("Height"));
        }

        // M3: when adding a built element to its live parent fails, the names it registered must not stay behind.
        [Fact]
        public void AFailedLiveInsert_LeavesNoNamesBehind()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<PickyPanel x:Name=\"root\"/>");

            EditResult result = document.Editor.InsertElement(host.IdOf(root), 0, "<TextBlock x:Name=\"orphan\"/>");

            Assert.False(result.Succeeded);
            Assert.Null(MarkupNameScope.GetScope(root)!.Find("orphan"));
        }
    }
}
