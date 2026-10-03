using System.ComponentModel;
using System.Globalization;
using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Data.Bindings
{
    public class BindingTextConversionTests
    {
        private sealed class Model : INotifyPropertyChanged
        {
            private int positive = 1;

            public event PropertyChangedEventHandler? PropertyChanged;

            public int Count { get; set; }

            public int? Maybe { get; set; } = 3;

            public float Ratio { get; set; }

            public DayOfWeek Day { get; set; }

            public int Positive
            {
                get => positive;
                set
                {
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                    positive = value;
                }
            }

            public void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static Binding Bind(TextBox box, Model model, string property)
        {
            IPropertyReference text = PropertyRegistry.Default.GetPropertyStore(typeof(TextBox)).GetProperty(nameof(TextBox.Text));
            var binding = new Binding(box, text, new PropertyPath(property, typeof(Model)))
            {
                Source = model,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                IsEnabled = true,
            };

            // Enabling doesn't push the source value; real wiring (markup, BindableObject) calls UpdateTarget once.
            binding.UpdateTarget();
            return binding;
        }

        [Fact]
        public void Text_ParsesIntoAnIntSource()
        {
            var model = new Model();
            var box = new TextBox();
            Bind(box, model, nameof(Model.Count));

            box.Text = "12";

            Assert.Equal(12, model.Count);
        }

        [Fact]
        public void InvalidText_KeepsTheSource_AndSetsTheErrorState()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "12";
            int changes = 0;
            binding.ErrorChanged += (_, _) => changes++;

            box.Text = "12a";

            Assert.Equal(12, model.Count);
            Assert.True(binding.HasError);
            Assert.NotNull(binding.Error);
            Assert.Equal(1, changes);
        }

        [Fact]
        public void CorrectingTheText_ClearsTheError()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "12a";

            box.Text = "13";

            Assert.Equal(13, model.Count);
            Assert.False(binding.HasError);
            Assert.Null(binding.Error);
        }

        [Fact]
        public void AfterAFailure_LaterValidInputStillReachesTheSource()
        {
            // Regression: a failed write left IsEnabled = false, so the binding ignored every later change.
            var model = new Model();
            var box = new TextBox();
            Bind(box, model, nameof(Model.Count));

            box.Text = "x";
            box.Text = "7";

            Assert.Equal(7, model.Count);
        }

        [Theory]
        [InlineData("-")]
        [InlineData("")]
        [InlineData("1e")]
        public void IntermediateStates_NeverThrow(string text)
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));

            Exception? thrown = Record.Exception(() => box.Text = text);

            Assert.Null(thrown);
            Assert.True(binding.HasError);
        }

        [Fact]
        public void Enum_ParsesByName()
        {
            var model = new Model();
            var box = new TextBox();
            Bind(box, model, nameof(Model.Day));

            box.Text = "Friday";

            Assert.Equal(DayOfWeek.Friday, model.Day);
        }

        [Fact]
        public void Float_IsParsedWithTheCurrentCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var model = new Model();
                var box = new TextBox();
                Bind(box, model, nameof(Model.Ratio));

                box.Text = "1,5";

                Assert.Equal(1.5f, model.Ratio);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void EmptyText_SetsANullableSourceToNull()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Maybe));

            box.Text = string.Empty;

            Assert.Null(model.Maybe);
            Assert.False(binding.HasError);
        }

        [Fact]
        public void ThrowingSourceSetter_EntersTheErrorState_WithoutThrowing()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Positive));

            Exception? thrown = Record.Exception(() => box.Text = "0");

            Assert.Null(thrown);
            Assert.Equal(1, model.Positive);
            Assert.IsType<ArgumentOutOfRangeException>(binding.Error);
        }

        [Fact]
        public void SourceToTargetUpdate_ClearsTheError()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "oops";

            model.Count = 5;
            model.Raise(nameof(Model.Count));

            Assert.Equal("5", box.Text);
            Assert.False(binding.HasError);
        }

        [Fact]
        public void NumbersStillFormatAsText_SourceToTarget()
        {
            var model = new Model { Count = 42 };
            var box = new TextBox();
            Bind(box, model, nameof(Model.Count));

            Assert.Equal("42", box.Text);
        }
    }
}
