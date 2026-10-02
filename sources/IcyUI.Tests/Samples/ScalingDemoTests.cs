using Icy.Assets;
using Icy.Configuration;
using Icy.Data.Markup;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    /// <summary>
    /// Pins ScalingDemo's apply/confirm/auto-revert flow: display settings must never apply live, and an applied
    /// change must revert on its own unless the user confirms it - otherwise a bad scale can leave the UI unusable.
    /// </summary>
    public class ScalingDemoTests
    {
        private static readonly TimeSpan PastCountdown = TimeSpan.FromSeconds(11);

        private static (UIElement Root, ScalingConfiguration Scaling) Build()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            return (ScalingDemo.Build(configuration, "Airfool"), configuration.Scaling);
        }

        private static void Click(UIElement root, string name) => root.FindRequiredControl<Button>(name).OnTap();

        private static Slider UserScale(UIElement root) => root.FindRequiredControl<Slider>("UserScaleSlider");

        private static void AdvanceAnimations(TimeSpan delta) => Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(delta);

        [Fact]
        public void EditingControls_DoesNotApplyAnything()
        {
            var (root, scaling) = Build();

            UserScale(root).Value = 1.5f;
            root.FindRequiredControl<ComboBox>("ScaleModeBox").SelectedItem = UIScaleMode.None;

            Assert.Equal(1f, scaling.UserScale);
            Assert.Equal(UIScaleMode.Dpi, scaling.Mode);
        }

        [Fact]
        public void Apply_CommitsTheDraft_AndAsksForConfirmation()
        {
            var (root, scaling) = Build();
            UserScale(root).Value = 1.5f;

            Click(root, "ApplyButton");

            Assert.Equal(1.5f, scaling.UserScale);
            Assert.True(root.FindRequiredControl<UIElement>("ConfirmPanel").IsVisible);
        }

        [Fact]
        public void UnconfirmedApply_RevertsAutomaticallyAfterTheCountdown()
        {
            var (root, scaling) = Build();
            UserScale(root).Value = 1.5f;
            root.FindRequiredControl<ComboBox>("ScaleModeBox").SelectedItem = UIScaleMode.None;
            Click(root, "ApplyButton");

            AdvanceAnimations(PastCountdown);

            Assert.Equal(1f, scaling.UserScale);
            Assert.Equal(UIScaleMode.Dpi, scaling.Mode);
            Assert.Equal(1f, UserScale(root).Value);
            Assert.False(root.FindRequiredControl<UIElement>("ConfirmPanel").IsVisible);
        }

        [Fact]
        public void Keep_KeepsTheAppliedSettings_PastTheCountdown()
        {
            var (root, scaling) = Build();
            UserScale(root).Value = 1.5f;
            Click(root, "ApplyButton");

            Click(root, "KeepButton");
            AdvanceAnimations(PastCountdown);

            Assert.Equal(1.5f, scaling.UserScale);
            Assert.False(root.FindRequiredControl<UIElement>("ConfirmPanel").IsVisible);
        }

        [Fact]
        public void Revert_RestoresThePreviousSettingsImmediately()
        {
            var (root, scaling) = Build();
            UserScale(root).Value = 1.5f;
            Click(root, "ApplyButton");

            Click(root, "RevertButton");

            Assert.Equal(1f, scaling.UserScale);
            Assert.Equal(1f, UserScale(root).Value);
        }

        [Fact]
        public void Discard_ResetsTheDraftToTheAppliedSettings()
        {
            var (root, scaling) = Build();
            UserScale(root).Value = 1.5f;

            Click(root, "DiscardButton");

            Assert.Equal(1f, UserScale(root).Value);
            Assert.Equal(1f, scaling.UserScale);
        }
    }
}
