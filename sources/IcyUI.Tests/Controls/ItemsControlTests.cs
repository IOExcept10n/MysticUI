using System.Collections.Generic;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ItemsControlTests
    {
        [Fact]
        public void PoolingEnabled_DefaultsToTrue()
        {
            var control = new ItemsControl();

            Assert.True(control.PoolingEnabled);
        }

        [Fact]
        public void DefaultEstimatedItemHeight_DefaultsTo40()
        {
            var control = new ItemsControl();

            Assert.Equal(40f, control.DefaultEstimatedItemHeight);
        }

        [Fact]
        public void ItemsSource_NullByDefault_ExtentHeightIsZero()
        {
            var control = new ItemsControl();

            Assert.Equal(0, control.ExtentHeight);
        }

        [Fact]
        public void ItemsSource_PlainEnumerable_ExtentHeightUsesDefaultEstimateTimesCount()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3, 4, 5 } };

            Assert.Equal(5 * 40f, control.ExtentHeight);
        }

        [Fact]
        public void ItemsSource_Reassigned_ResetsExtentHeight()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };

            control.ItemsSource = new List<object> { 1, 2 };

            Assert.Equal(2 * 40f, control.ExtentHeight);
        }

        [Fact]
        public void ExtentWidth_EqualsCurrentViewportWidth()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };

            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 0, 250, 100);

            Assert.Equal(250, control.ExtentWidth);
        }

        [Fact]
        public void RecordHeight_FirstMeasurement_UpdatesSumAndCount()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };

            InvokeRecordHeight(control, 0, 60f);

            // Once ANY item is known, unknown items are estimated at the running average of known items (not
            // the flat DefaultEstimatedItemHeight, which only applies while knownCount is zero - see
            // AverageHeight) - 1 known (60) + 2 unknown (60 each, the only known average so far) = 180.
            Assert.Equal(180f, control.ExtentHeight);
        }

        [Fact]
        public void RecordHeight_ReMeasurementGrows_AdjustsSumByDeltaOnly()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };
            InvokeRecordHeight(control, 0, 60f);

            InvokeRecordHeight(control, 0, 90f);

            // The known value grew by 30 (60 -> 90); the 2 unknown items re-estimate at the new average (90):
            // 90 + 90 + 90 = 270.
            Assert.Equal(270f, control.ExtentHeight);
        }

        [Fact]
        public void RecordHeight_ReMeasurementShrinks_AdjustsSumByDeltaOnly()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };
            InvokeRecordHeight(control, 0, 60f);

            InvokeRecordHeight(control, 0, 20f);

            // 20 known + 2 unknown re-estimated at the new average (20 each) = 60.
            Assert.Equal(60f, control.ExtentHeight);
        }

        [Fact]
        public void RecordHeight_ChangeAboveAnchor_RaisesCorrectionWithExactDelta()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3, 4, 5 } };
            InvokeRecordHeight(control, 0, 60f); // index 0 known, below anchorIndex(0) is false yet
            SetAnchorIndex(control, 3); // simulate having since scrolled so item 3 is now top-visible

            float? raisedDelta = null;
            ((IVirtualizingScrollInfo)control).VerticalOffsetCorrectionRequested += (_, delta) => raisedDelta = delta;

            InvokeRecordHeight(control, 0, 90f); // index 0 < anchorIndex(3): above the viewport

            Assert.Equal(30f, raisedDelta);
        }

        [Fact]
        public void RecordHeight_ChangeAtOrBelowAnchor_DoesNotRaiseCorrection()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3, 4, 5 } };
            InvokeRecordHeight(control, 3, 60f);
            SetAnchorIndex(control, 3);

            bool raised = false;
            ((IVirtualizingScrollInfo)control).VerticalOffsetCorrectionRequested += (_, _) => raised = true;

            InvokeRecordHeight(control, 3, 90f); // index 3 is not < anchorIndex(3)

            Assert.False(raised);
        }

        private static void InvokeRecordHeight(ItemsControl control, int index, float height)
        {
            var method = typeof(ItemsControl).GetMethod("RecordHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            method.Invoke(control, [index, height]);
        }

        private static void SetAnchorIndex(ItemsControl control, int index)
        {
            var field = typeof(ItemsControl).GetField("anchorIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(control, index);
        }

        [Fact]
        public void EnsureRealized_BuildsAContainerAndMeasuresIt()
        {
            var control = new ItemsControl
            {
                ItemsSource = new List<object> { "a", "b" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>"""),
            };

            InvokeEnsureRealized(control, 0);

            var container = GetRealizedContainers(control)[0];
            Assert.IsType<ItemContainer>(container);
            Assert.Same(control, container.Parent);
        }

        [Fact]
        public void EnsureRealized_CalledTwiceForSameIndex_BuildsOnlyOnce()
        {
            var template = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = new List<object> { "a" }, ItemTemplate = template };

            InvokeEnsureRealized(control, 0);
            ItemContainer first = GetRealizedContainers(control)[0];
            InvokeEnsureRealized(control, 0);
            ItemContainer second = GetRealizedContainers(control)[0];

            Assert.Same(first, second);
        }

        [Fact]
        public void Derealize_ThenEnsureRealizedAgain_ReusesThePooledContainer()
        {
            var template = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = new List<object> { "a", "b" }, ItemTemplate = template };
            InvokeEnsureRealized(control, 0);
            ItemContainer original = GetRealizedContainers(control)[0];

            InvokeDerealize(control, 0);
            Assert.Null(original.Parent); // detached once pooled, before anything reuses it

            InvokeEnsureRealized(control, 1);

            Assert.Same(original, GetRealizedContainers(control)[1]);
        }

        [Fact]
        public void Derealize_PooledContainer_IsDetachedThenReattachedOnReuse()
        {
            var template = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = new List<object> { "a", "b" }, ItemTemplate = template };
            InvokeEnsureRealized(control, 0);
            ItemContainer original = GetRealizedContainers(control)[0];
            InvokeDerealize(control, 0);

            InvokeEnsureRealized(control, 1);

            Assert.Same(control, original.Parent);
        }

        [Fact]
        public void Derealize_PoolingDisabled_ContainerIsNotPooled()
        {
            var template = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = new List<object> { "a", "b" }, ItemTemplate = template, PoolingEnabled = false };
            InvokeEnsureRealized(control, 0);
            ItemContainer original = GetRealizedContainers(control)[0];
            InvokeDerealize(control, 0);

            InvokeEnsureRealized(control, 1);

            Assert.NotSame(original, GetRealizedContainers(control)[1]);
        }

        [Fact]
        public void EnsureRealized_NoTemplate_Throws()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { "a" } };

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => InvokeEnsureRealized(control, 0));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new Tests.Input.FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        private static void InvokeEnsureRealized(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("EnsureRealized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static void InvokeDerealize(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("Derealize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;
    }
}
