using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using Icy.Assets;
using Icy.Configuration;
using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.Rendering.Brushes;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
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
            => Bind(box, model, new PropertyPath(property, typeof(Model)));

        // Markup ({Binding Count}) builds a DynamicPropertyPath, which knows nothing about the source type up front.
        private static Binding BindDynamic(TextBox box, Model model, string property)
            => Bind(box, model, new DynamicPropertyPath(property));

        private static Binding Bind(TextBox box, Model model, IPropertyPath path)
        {
            IPropertyReference text = PropertyRegistry.Default.GetPropertyStore(typeof(TextBox)).GetProperty(nameof(TextBox.Text));
            var binding = new Binding(box, text, path)
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

        [Fact]
        public void ErroringBinding_MarksItsTargetInvalid_UntilCorrected()
        {
            var model = new Model();
            var box = new TextBox();
            Bind(box, model, nameof(Model.Count));

            box.Text = "12a";
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));

            box.Text = "12";
            Assert.False(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void TwoBindings_OneInError_KeepsTheElementInvalid()
        {
            var model = new Model();
            var box = new TextBox();
            Binding count = Bind(box, model, nameof(Model.Count));
            Binding day = Bind(box, model, nameof(Model.Day));

            box.Text = "Monday"; // not an int, but a valid DayOfWeek

            Assert.True(count.HasError);
            Assert.False(day.HasError);
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void DisposingAnErroringBinding_ClearsInvalid()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "nope";

            binding.Dispose();

            Assert.False(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void FinalizingAnErroringBinding_DoesNotTouchItsTarget()
        {
            // Regression: Dispose(false) from the finalizer cleared the error and set the target's ControlState on the
            // finalizer thread; the dispatcher's thread check threw there and crashed the process.
            WeakReference binding = MakeErroringBindingAndDropIt();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Reaching this line means the finalizer ran without crashing the process.
            Assert.False(binding.IsAlive);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static WeakReference MakeErroringBindingAndDropIt()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "not a number";
            return new WeakReference(binding);
        }

        [Fact]
        public void DynamicPath_ParsesIntoAnIntSource()
        {
            var model = new Model();
            var box = new TextBox();
            BindDynamic(box, model, nameof(Model.Count));

            box.Text = "12";

            Assert.Equal(12, model.Count);
        }

        [Fact]
        public void DynamicPath_InvalidText_SetsTheErrorState()
        {
            var model = new Model { Count = 12 };
            var box = new TextBox();
            Binding binding = BindDynamic(box, model, nameof(Model.Count));

            box.Text = "12a";

            Assert.Equal(12, model.Count);
            Assert.True(binding.HasError);
            Assert.NotNull(binding.Error);
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void DynamicPath_ThrowingSetter_SetsTheErrorState()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = BindDynamic(box, model, nameof(Model.Positive));

            box.Text = "0";

            Assert.Equal(1, model.Positive);
            Assert.True(binding.HasError);
            Assert.IsType<ArgumentOutOfRangeException>(binding.Error);
        }

        [Fact]
        public void DynamicPath_MissingIntermediate_IsStillSilent()
        {
            var holder = new Holder();
            var box = new TextBox();
            IPropertyReference text = PropertyRegistry.Default.GetPropertyStore(typeof(TextBox)).GetProperty(nameof(TextBox.Text));
            var binding = new Binding(box, text, new DynamicPropertyPath("Inner.Count"))
            {
                Source = holder,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                IsEnabled = true,
            };

            box.Text = "5";

            Assert.False(binding.HasError);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnrelatedSourceChange_KeepsTheTypedTextAndTheError(bool dynamic)
        {
            var model = new Model { Count = 12 };
            var box = new TextBox();
            Binding binding = dynamic ? BindDynamic(box, model, nameof(Model.Count)) : Bind(box, model, nameof(Model.Count));
            box.Text = "12a";

            model.Raise(nameof(Model.Ratio));

            Assert.Equal("12a", box.Text);
            Assert.True(binding.HasError);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(nameof(Model.Count))]
        public void RelevantSourceChange_StillRefreshesTheTarget(string? name)
        {
            var model = new Model { Count = 12 };
            var box = new TextBox();
            Binding binding = BindDynamic(box, model, nameof(Model.Count));
            box.Text = "12a";

            model.Raise(name!);

            Assert.Equal("12", box.Text);
            Assert.False(binding.HasError);
        }

        private sealed class Holder
        {
            public Model? Inner { get; set; }
        }

        [Fact]
        public void DefaultTheme_ShowsFocusedInvalidOverFocusedAndInvalid()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration()).UseDefaultTheme();
            var canvas = new Canvas(configuration);
            var box = new TextBox();
            canvas.Add(box);
            canvas.Render();

            box.ControlState |= ControlState.Focused | ControlState.Invalid;

            var brush = Assert.IsType<SolidColorBrush>(box.BorderBrush);
            Assert.Equal(Color.FromArgb(255, 255, 106, 106), brush.Color);
        }
    }
}
