// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    public class NavigationRoutingTests
    {
        [Fact]
        public void AClaimingFocusedElement_HandlesThePress()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var probe = new Probe { Claim = true };
            canvas.Add(probe);
            canvas.Render();
            canvas.Focus(probe);

            Assert.True(input.Events.Navigation.RaiseFocusChanging(new Vector2(0.2f, 0.9f)).Handled);
            Assert.Equal(Vector2.UnitY, probe.LastDirection);
        }

        [Fact]
        public void AnAncestor_GetsThePressTheFocusedElementLetsGo()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var child = new Probe();
            var parent = new ProbePanel { Claim = true };
            parent.Children.Add(child);
            canvas.Add(parent);
            canvas.Render();
            canvas.Focus(child);

            Assert.True(input.Events.Navigation.RaiseFocusChanging(-Vector2.UnitX).Handled);
            Assert.Equal(-Vector2.UnitX, child.LastDirection);
            Assert.Equal(-Vector2.UnitX, parent.LastDirection);
        }

        [Fact]
        public void Activation_ClicksAFocusedButton_AndTogglesACheckBox()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var button = new Button();
            var checkBox = new CheckBox();
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            canvas.Add(button);
            canvas.Add(checkBox);
            canvas.Render();

            canvas.Focus(button);
            input.Events.Navigation.RaiseSelectElement();
            canvas.Focus(checkBox);
            input.Events.Navigation.RaiseSelectElement();

            Assert.Equal(1, clicks);
            Assert.True(checkBox.IsChecked == true);
        }

        [Fact]
        public void Activation_OfADisabledButton_DoesNothing()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var button = new Button();
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            canvas.Add(button);
            canvas.Render();
            canvas.Focus(button);
            button.IsEnabled = false;

            input.Events.Navigation.RaiseSelectElement();

            Assert.Equal(0, clicks);
        }

        [Fact]
        public void AnAppSubscriber_StillSeesEveryPress()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var probe = new Probe { Claim = true };
            canvas.Add(probe);
            canvas.Render();
            canvas.Focus(probe);
            int seen = 0;
            input.Events.Navigation.FocusChanging += (_, _) => seen++;

            input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY);

            Assert.Equal(1, seen);
        }

        [Fact]
        public void WithKeyboardNavigationDisabled_NothingIsRouted()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var probe = new Probe { Claim = true };
            canvas.Add(probe);
            canvas.Render();
            canvas.Focus(probe);
            canvas.IsKeyboardNavigationEnabled = false;

            Assert.False(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY).Handled);
            Assert.Null(probe.LastDirection);
        }

        private sealed class Probe : UIElement
        {
            public Probe()
            {
                IsFocusable = true;
                Width = 20;
                Height = 20;
            }

            public bool Claim { get; set; }

            public Vector2? LastDirection { get; private set; }

            protected internal override bool OnNavigate(Vector2 direction)
            {
                LastDirection = direction;
                return Claim;
            }
        }

        private sealed class ProbePanel : StackPanel
        {
            public bool Claim { get; set; }

            public Vector2? LastDirection { get; private set; }

            protected internal override bool OnNavigate(Vector2 direction)
            {
                LastDirection = direction;
                return Claim;
            }
        }
    }
}
