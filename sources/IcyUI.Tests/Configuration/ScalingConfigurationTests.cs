using System.ComponentModel;
using System.Drawing;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Configuration
{
    public class ScalingConfigurationTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var scaling = new ScalingConfiguration();

            Assert.Equal(UIScaleMode.Dpi, scaling.Mode);
            Assert.Equal(new Size(1920, 1080), scaling.ReferenceSize);
            Assert.Equal(ReferenceFit.Fit, scaling.ReferenceFit);
            Assert.Equal(1f, scaling.UserScale);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-0.5f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void UserScale_RejectsInvalidValues(float value) =>
            Assert.ThrowsAny<ArgumentException>(() => new ScalingConfiguration().UserScale = value);

        [Fact]
        public void ReferenceSize_RejectsEmpty() =>
            Assert.ThrowsAny<ArgumentException>(() => new ScalingConfiguration().ReferenceSize = new Size(0, 1080));

        [Fact]
        public void UserScale_RaisesPropertyChanged()
        {
            var scaling = new ScalingConfiguration();
            string? changed = null;
            ((INotifyPropertyChanged)scaling).PropertyChanged += (_, e) => changed = e.PropertyName;

            scaling.UserScale = 1.5f;

            Assert.Equal(nameof(ScalingConfiguration.UserScale), changed);
        }

        [Fact]
        public void Builder_ProvidesNonNullScalingFacet()
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext()).ConfigureInput(new FakeInputSystem());

            IcyConfiguration configuration = builder.Build();

            Assert.NotNull(configuration.Scaling);
        }

        [Fact]
        public void ConfigureScaling_Action_AppliesToBuiltConfiguration()
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext()).ConfigureInput(new FakeInputSystem());
            builder.ConfigureScaling().ConfigureScaling(s => s.Mode = UIScaleMode.None);

            Assert.Equal(UIScaleMode.None, builder.Build().Scaling.Mode);
        }
    }
}
