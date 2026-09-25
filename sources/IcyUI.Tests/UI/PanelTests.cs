// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    /// <summary>
    /// Covers <see cref="Panel.Children"/>'s <c>Clear()</c> path - regression coverage for the
    /// <see cref="Panel.OnChildrenResetting"/>/<see cref="Panel.OnChildrenUpdated"/> fix added alongside
    /// <see cref="Icy.UI.Controls.ColorPicker.RefreshSwatches"/> (the first caller in this codebase to ever call
    /// <see cref="Panel.Children"/>'s <c>Clear()</c>). <see cref="System.Collections.ObjectModel.ObservableCollection{T}.Clear"/>
    /// raises its <c>CollectionChanged</c> event with a <see langword="null"/> <c>OldItems</c> - detaching children
    /// from that event (as the old, buggy code tried to) is impossible, so detachment now happens from
    /// <see cref="Panel.ChildrenResetting"/> instead, before the underlying collection is actually cleared.
    /// </summary>
    public class PanelTests
    {
        private static Canvas CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return new Canvas(config);
        }

        [Fact]
        public void ChildrenClear_DetachesEveryChild()
        {
            var canvas = CreateCanvas();
            var panel = new Panel();
            canvas.Add(panel);
            var a = new UIElement();
            var b = new UIElement();
            panel.Children.Add(a);
            panel.Children.Add(b);

            // Sanity: both children are actually attached before Clear() runs.
            Assert.Same(panel, a.Parent);
            Assert.Same(canvas, a.Canvas);
            Assert.Same(panel, b.Parent);
            Assert.Same(canvas, b.Canvas);

            panel.Children.Clear();

            Assert.Null(a.Parent);
            Assert.Null(a.Canvas);
            Assert.Null(b.Parent);
            Assert.Null(b.Canvas);
        }

        [Fact]
        public void ChildrenClear_OnAlreadyEmptyCollection_DoesNotThrow()
        {
            var panel = new Panel();

            panel.Children.Clear();

            Assert.Empty(panel.Children);
        }

        [Fact]
        public void ChildrenClear_CancelledViaChildrenResetting_LeavesChildrenAttached()
        {
            var panel = new Panel();
            var child = new UIElement();
            panel.Children.Add(child);
            panel.ChildrenResetting += (_, e) => e.Cancel = true;

            panel.Children.Clear();

            Assert.Single(panel.Children);
            Assert.Same(panel, child.Parent);
        }

        [Fact]
        public void ChildrenClear_ThenRepopulate_DoesNotAccumulate_OldGenerationDetached_NewGenerationAttached()
        {
            // Mirrors ColorPicker.RefreshSwatches' own pattern: Clear() the whole strip, then rebuild it from
            // scratch - the case that first exposed the underlying bug.
            var panel = new Panel();
            var oldA = new UIElement();
            var oldB = new UIElement();
            panel.Children.Add(oldA);
            panel.Children.Add(oldB);

            panel.Children.Clear();

            var newC = new UIElement();
            var newD = new UIElement();
            panel.Children.Add(newC);
            panel.Children.Add(newD);

            Assert.Equal(2, panel.Children.Count);
            Assert.Contains(newC, panel.Children);
            Assert.Contains(newD, panel.Children);
            Assert.Null(oldA.Parent);
            Assert.Null(oldB.Parent);
            Assert.Same(panel, newC.Parent);
            Assert.Same(panel, newD.Parent);
        }
    }
}
