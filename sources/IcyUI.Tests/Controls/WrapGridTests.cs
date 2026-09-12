using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class WrapGridTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var grid = new WrapGrid();

            Assert.Equal(-1, grid.SelectedIndex);
            Assert.Null(grid.SelectedItem);
            Assert.Equal(64f, grid.ItemWidth);
            Assert.Equal(64f, grid.ItemHeight);
        }

        [Fact]
        public void ItemWidth_ItemHeight_RejectNonPositiveValues()
        {
            var grid = new WrapGrid();

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.ItemWidth = 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.ItemWidth = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.ItemHeight = 0f);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }
    }
}
