using Icy.Data;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class PropertyGridAdapterTests
    {
        private sealed class Target
        {
            public string Nickname { get; set; } = "Alpha";

            public int Score { get; set; } = 10;
        }

        private sealed class RecordingAdapter : PropertyGridValueAdapter
        {
            public List<(string Name, object? Value)> Writes { get; } = [];

            public Dictionary<string, string> Expressions { get; } = [];

            public HashSet<string> Resettable { get; } = [];

            public List<string> Resets { get; } = [];

            public bool AcceptExpressions { get; set; } = true;

            public override bool TrySetValue(PropertyGridEntry entry, object target, object? value)
            {
                Writes.Add((entry.Name, value));
                return base.TrySetValue(entry, target, value);
            }

            public override string? GetExpression(PropertyGridEntry entry, object target) => Expressions.GetValueOrDefault(entry.Name);

            public override bool TrySetExpression(PropertyGridEntry entry, object target, string text)
            {
                if (!AcceptExpressions)
                    return false;
                Expressions[entry.Name] = text;
                return true;
            }

            public override bool CanReset(PropertyGridEntry entry, object target) => Resettable.Contains(entry.Name);

            public override void Reset(PropertyGridEntry entry, object target) => Resets.Add(entry.Name);
        }

        [Fact]
        public void TheDefaultAdapter_WritesThroughReflection()
        {
            var target = new Target();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            ((TextBox)FindEditor(grid, "Nickname")!).Text = "Beta";

            Assert.Same(PropertyGridValueAdapter.Default, grid.ValueAdapter);
            Assert.Equal("Beta", target.Nickname);
        }

        [Fact]
        public void ACustomAdapter_ReceivesEveryWrite()
        {
            var adapter = new RecordingAdapter();
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            ((TextBox)FindEditor(grid, "Score")!).Text = "42";

            Assert.Contains(("Score", (object?)42), adapter.Writes);
        }

        [Fact]
        public void AnExpressionRow_ShowsTheTextAndCommitsOnlyOnFocusLoss()
        {
            var adapter = new RecordingAdapter();
            adapter.Expressions["Nickname"] = "{Binding Name}";
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            var box = (TextBox)FindEditor(grid, "Nickname")!;
            Assert.Equal("{Binding Name}", box.Text);

            box.Text = "{Binding Title}";
            Assert.Equal("{Binding Name}", adapter.Expressions["Nickname"]);
            Assert.Empty(adapter.Writes);

            PropertyGrid.CommitExpressionForTest(box);
            Assert.Equal("{Binding Title}", adapter.Expressions["Nickname"]);
        }

        [Fact]
        public void ARejectedExpression_MarksTheBoxInvalid()
        {
            var adapter = new RecordingAdapter { AcceptExpressions = false };
            adapter.Expressions["Nickname"] = "{Binding Name}";
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            var box = (TextBox)FindEditor(grid, "Nickname")!;
            box.Text = "{Broken";
            PropertyGrid.CommitExpressionForTest(box);

            Assert.True(box.ControlState.HasFlag(Icy.UI.Styles.ControlState.Invalid));
        }

        [Fact]
        public void AResettableRow_HasAResetButtonThatCallsTheAdapter()
        {
            var adapter = new RecordingAdapter();
            adapter.Resettable.Add("Score");
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            Grid row = FindRow(grid, "Score")!;
            var reset = Assert.IsType<Button>(row.Children[2]);
            reset.Command!.Execute(null);

            Assert.Equal(["Score"], adapter.Resets);
            Assert.Equal(2, FindRow(grid, "Nickname")!.Children.Count);
        }

        [Fact]
        public void Refresh_RereadsValuesWithoutReplacingRows()
        {
            var target = new Target();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();
            Grid rowBefore = FindRow(grid, "Score")!;

            target.Score = 99;
            grid.Refresh();

            Assert.Same(rowBefore, FindRow(grid, "Score"));
            Assert.Equal("99", ((TextBox)FindEditor(grid, "Score")!).Text);
        }

        [Fact]
        public void Refresh_KeepsTheEditorsOfUnchangedRows()
        {
            var target = new Target();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();
            UIElement nicknameBefore = FindEditor(grid, "Nickname")!;
            UIElement scoreBefore = FindEditor(grid, "Score")!;

            target.Score = 99;
            grid.Refresh();

            Assert.Same(nicknameBefore, FindEditor(grid, "Nickname"));
            Assert.NotSame(scoreBefore, FindEditor(grid, "Score"));
        }

        [Fact]
        public void Refresh_PicksUpANewlyResettableRow()
        {
            var adapter = new RecordingAdapter();
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            adapter.Resettable.Add("Score");
            grid.Refresh();

            Assert.IsType<Button>(FindRow(grid, "Score")!.Children[2]);
        }

        [Fact]
        public void Refresh_SkipsTheRowBeingWritten()
        {
            // The adapter's write raises a document change that calls Refresh synchronously; rebuilding the editor
            // that is mid-write would drop the user's widget (an open color popup, a slider drag).
            var target = new Target();
            var grid = new PropertyGrid();
            var adapter = new RefreshingAdapter(grid);
            grid.ValueAdapter = adapter;
            grid.Target = target;
            grid.Measure();

            var box = (TextBox)FindEditor(grid, "Score")!;
            box.Text = "5";

            Assert.Same(box, FindEditor(grid, "Score"));
            Assert.Equal(5, target.Score);
        }

        [Fact]
        public void ChangingTheAdapter_RebuildsTheRows()
        {
            var adapter = new RecordingAdapter();
            adapter.Expressions["Nickname"] = "{Binding Name}";
            var grid = new PropertyGrid { Target = new Target() };
            grid.Measure();

            grid.ValueAdapter = adapter;
            grid.Measure();

            Assert.Equal("{Binding Name}", ((TextBox)FindEditor(grid, "Nickname")!).Text);
        }

        private sealed class DefaultedTarget
        {
            [System.ComponentModel.DefaultValue(3)]
            public int Lives { get; set; } = 3;

            public int Plain { get; set; } = 1;
        }

        [Fact]
        public void TheDefaultAdapter_OffersReset_OnlyForAChangedDefaultedValue()
        {
            var target = new DefaultedTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            Assert.Equal(2, FindRow(grid, "Lives")!.Children.Count);
            Assert.Equal(2, FindRow(grid, "Plain")!.Children.Count);

            ((TextBox)FindEditor(grid, "Lives")!).Text = "7";
            grid.Refresh();

            Assert.IsType<Button>(FindRow(grid, "Lives")!.Children[2]);
            Assert.Equal(2, FindRow(grid, "Plain")!.Children.Count);
        }

        [Fact]
        public void Reset_WritesTheDefault_AndRemovesItsButton()
        {
            var target = new DefaultedTarget { Lives = 9 };
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var reset = Assert.IsType<Button>(FindRow(grid, "Lives")!.Children[2]);
            reset.Command!.Execute(null);

            Assert.Equal(3, target.Lives);
            Assert.Equal("3", ((TextBox)FindEditor(grid, "Lives")!).Text);
            Assert.Equal(2, FindRow(grid, "Lives")!.Children.Count);
        }

        private sealed class RefreshingAdapter(PropertyGrid grid) : PropertyGridValueAdapter
        {
            public override bool TrySetValue(PropertyGridEntry entry, object target, object? value)
            {
                bool written = base.TrySetValue(entry, target, value);
                grid.Refresh();
                return written;
            }
        }

        private static Grid? FindRow(PropertyGrid grid, string name)
        {
            foreach (ItemContainer container in GetRealizedContainers(grid).Values)
            {
                if (container.Content is Grid row && row.Children.ElementAtOrDefault(0) is TextBlock label && label.Text == name)
                    return row;
            }

            return null;
        }

        private static UIElement? FindEditor(PropertyGrid grid, string name) => FindRow(grid, name)?.Children.ElementAtOrDefault(1);

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(control)!;
    }
}
