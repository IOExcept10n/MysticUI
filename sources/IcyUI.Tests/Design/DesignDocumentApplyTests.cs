// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Editing;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Design
{
    public class DesignDocumentApplyTests
    {
        private static readonly string Page =
            """
            <StackPanel>
              <Border Width="10"/>
              <TextBlock/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void Apply_UpdatesTextAndVersionAndRaisesChanged()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            var raised = new List<DocumentChangedEventArgs>();
            document.Changed += (_, e) => raised.Add(e);
            TextChangeSet change = ReplaceWidth(document, "20");

            EditResult result = document.Apply(new EditStep(change, [], "test"), out _);

            Assert.True(result.Succeeded);
            Assert.Contains("Width=\"20\"", document.Text);
            Assert.Equal(1, document.Version);
            DocumentChangedEventArgs args = Assert.Single(raised);
            Assert.Same(change, args.Changes);
            Assert.Equal(1, args.Version);
        }

        [Fact]
        public void Apply_KeepsTheIdsOfElementsTheChangeDidNotReplace()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            NodeId[] before = [.. document.Syntax.Elements.Select(x => document.GetNodeId(x)!.Value)];

            document.Apply(new EditStep(ReplaceWidth(document, "123456"), [], "test"), out _);

            Assert.Equal(before, document.Syntax.Elements.Select(x => document.GetNodeId(x)!.Value));
        }

        [Fact]
        public void Apply_AReplacedElementGetsANewIdAndTheOldOneVanishes()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            ElementSyntax block = document.Syntax.Elements.Single(x => x.Name == "TextBlock");
            NodeId oldId = document.GetNodeId(block)!.Value;

            document.Apply(new EditStep(new TextChangeSet([new TextChange(block.Span, "<Button/>")]), [], "test"), out _);

            ElementSyntax button = document.Syntax.Elements.Single(x => x.Name == "Button");
            Assert.NotEqual(oldId, document.GetNodeId(button));
            Assert.Null(document.GetNode(oldId));
            Assert.Empty(document.GetObjects(oldId));
        }

        [Fact]
        public void Apply_ReturnsAnInverseThatRestoresTheTextExactly()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);

            document.Apply(new EditStep(ReplaceWidth(document, "20"), [], "test"), out EditStep? inverse);
            document.Apply(inverse!, out _);

            Assert.Equal(Page, document.Text);
        }

        [Fact]
        public void Apply_AFailingAction_RollsBackTheTextAndRevertsInReverseOrder()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            var log = new List<string>();
            int changes = 0;
            document.Changed += (_, _) => changes++;

            EditResult result = document.Apply(
                new EditStep(ReplaceWidth(document, "20"), [new RecordingAction(log, "A"), new RecordingAction(log, "B", fail: true)], "test"),
                out EditStep? inverse);

            Assert.False(result.Succeeded);
            Assert.Equal("boom", result.Error!.Value.Message);
            Assert.Null(inverse);
            Assert.Equal(Page, document.Text);
            Assert.Equal(0, document.Version);
            Assert.Equal(0, changes);
            Assert.Equal(["A.Execute", "B.Execute", "B.Revert", "A.Revert"], log);
        }

        [Fact]
        public void Apply_ThatWouldMakeTheMarkupMalformed_Fails()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            TextSpan endTag = document.Syntax.Root!.EndTagSpan!.Value;

            EditResult result = document.Apply(new EditStep(new TextChangeSet([new TextChange(endTag, string.Empty)]), [], "test"), out _);

            Assert.False(result.Succeeded);
            Assert.Equal(Page, document.Text);
        }

        [Fact]
        public async Task Apply_FromAnotherThread_Throws()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            var step = new EditStep(ReplaceWidth(document, "20"), [], "test");

            await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => { document.Apply(step, out _); }));
        }

        private static TextChangeSet ReplaceWidth(DesignDocument document, string value)
        {
            AttributeSyntax width = document.Syntax.Elements.Single(x => x.Name == "Border").FindAttribute("Width")!;
            return new TextChangeSet([new TextChange(width.ValueSpan, value)]);
        }

        private sealed class RecordingAction(List<string> log, string name, bool fail = false) : MirrorAction
        {
            public override void Execute(MirrorContext context)
            {
                log.Add($"{name}.Execute");
                if (fail)
                    throw new DesignEditException("boom");
            }

            public override void Revert(MirrorContext context) => log.Add($"{name}.Revert");

            public override MirrorAction CreateInverse(MirrorContext context) => new RecordingAction(log, name + "'");
        }
    }
}
