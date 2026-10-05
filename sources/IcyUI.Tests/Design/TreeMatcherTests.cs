// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editing;
using Icy.Design.Syntax;
using Xunit;

namespace Icy.Tests.Design
{
    public class TreeMatcherTests
    {
        [Fact]
        public void IdenticalTrees_AreNoChange()
        {
            const string page = "<StackPanel><Border x:Name=\"b\" Width=\"10\"/><TextBlock>Hi</TextBlock></StackPanel>";

            TreeDiff diff = Diff(page, page);

            Assert.True(diff.IsEmpty);
            Assert.Equal(3, diff.Pairs.Count);
        }

        [Fact]
        public void FormattingQuotesAndEntitiesOnly_AreNoChange()
        {
            TreeDiff diff = Diff(
                "<StackPanel><TextBlock Text=\"a &amp; b\" Width=\"10\"/></StackPanel>",
                "<StackPanel>\n  <TextBlock\n    Width='10'\n    Text='a &#38; b' />\n</StackPanel>");

            Assert.True(diff.IsEmpty);
        }

        [Fact]
        public void AttributeChanges_AreListedPerAttribute()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border Width=\"10\" Margin=\"1\"/></StackPanel>",
                "<StackPanel><Border Width=\"20\" Height=\"5\"/></StackPanel>");

            Assert.Equal(["Height", "Margin", "Width"], diff.ChangedAttributes.Select(x => x.Name).Order());
            Assert.All(diff.ChangedAttributes, x => Assert.Equal("Border", x.Element.Name));
        }

        [Fact]
        public void AChangeInTheMiddle_IsOneRemovalAndOneInsertion()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border/><Button/><TextBlock/></StackPanel>",
                "<StackPanel><Border/><CheckBox/><TextBlock/></StackPanel>");

            Assert.Equal("Button", Assert.Single(diff.Removed).Name);
            Assert.Equal("CheckBox", Assert.Single(diff.Inserted).Name);
            Assert.Equal(3, diff.Pairs.Count);
        }

        [Fact]
        public void ANamedElementInAnotherParent_IsAMove()
        {
            TreeDiff diff = Diff(
                "<StackPanel><StackPanel x:Name=\"l\"><Border x:Name=\"box\"/></StackPanel><StackPanel x:Name=\"r\"/></StackPanel>",
                "<StackPanel><StackPanel x:Name=\"l\"/><StackPanel x:Name=\"r\"><Border x:Name=\"box\"/></StackPanel></StackPanel>");

            (ElementSyntax old, ElementSyntax moved) = Assert.Single(diff.Moved);
            Assert.Equal("Border", old.Name);
            Assert.Equal("r", NameOf(moved.Parent!));
            Assert.Empty(diff.Removed);
            Assert.Empty(diff.Inserted);
        }

        [Fact]
        public void ANamedElementThatChangedType_IsReplacedInPlace()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border x:Name=\"b\"/></StackPanel>",
                "<StackPanel><Button x:Name=\"b\"/></StackPanel>");

            Assert.Equal("Button", Assert.Single(diff.Replaced).New.Name);
            Assert.Empty(diff.Removed);
            Assert.Empty(diff.Inserted);
        }

        [Fact]
        public void UnnamedElementsReordered_AreARemovalAndAnInsertion()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border/><Button/></StackPanel>",
                "<StackPanel><Button/><Border/></StackPanel>");

            Assert.Single(diff.Removed);
            Assert.Single(diff.Inserted);
            Assert.Empty(diff.Moved);
        }

        [Fact]
        public void NamedElementsReordered_AreAMoveWithinTheParent()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border x:Name=\"a\"/><Button x:Name=\"b\"/></StackPanel>",
                "<StackPanel><Button x:Name=\"b\"/><Border x:Name=\"a\"/></StackPanel>");

            Assert.Single(diff.Moved);
            Assert.Empty(diff.Removed);
            Assert.Empty(diff.Inserted);
        }

        [Fact]
        public void ChangedElementText_IsAReplacement()
        {
            TreeDiff diff = Diff(
                "<StackPanel><TextBlock>Hi</TextBlock></StackPanel>",
                "<StackPanel><TextBlock>Bye</TextBlock></StackPanel>");

            Assert.Equal("TextBlock", Assert.Single(diff.Replaced).New.Name);
        }

        [Fact]
        public void AChangeInsideAPropertyElement_IsOpaque()
        {
            TreeDiff diff = Diff(
                "<Grid><Grid.RowDefinitions><RowDefinition Height=\"Auto\"/></Grid.RowDefinitions></Grid>",
                "<Grid><Grid.RowDefinitions><RowDefinition Height=\"10\"/></Grid.RowDefinitions></Grid>");

            Assert.True(diff.OpaqueChanged);
            Assert.Empty(diff.ChangedAttributes);
            Assert.Equal(3, diff.Pairs.Count);
        }

        [Fact]
        public void ARootThatChangedType_IsARootProblem()
        {
            TreeDiff diff = Diff("<StackPanel/>", "<Border/>");

            Assert.NotNull(diff.RootProblem);
            Assert.Empty(diff.Pairs);
        }

        [Fact]
        public void AChangedNamespaceDeclaration_IsAReplacement()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border/></StackPanel>",
                "<StackPanel><Border xmlns:z=\"u\"/></StackPanel>");

            Assert.Equal("Border", Assert.Single(diff.Replaced).New.Name);
        }

        [Fact]
        public void ANamedElementMovedIntoANewContainer_IsRemovedAndComesWithTheInsertion()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border x:Name=\"box\"/></StackPanel>",
                "<StackPanel><StackPanel><Border x:Name=\"box\"/></StackPanel></StackPanel>");

            Assert.Equal("StackPanel", Assert.Single(diff.Inserted).Name);
            Assert.Equal("Border", Assert.Single(diff.Removed).Name);
            Assert.Empty(diff.Moved);
        }

        private static TreeDiff Diff(string before, string after) =>
            TreeMatcher.Match(DocumentSyntax.Parse(before), DocumentSyntax.Parse(after));

        private static string? NameOf(ElementSyntax element) => element.FindAttribute("x:Name")?.Value;
    }
}
