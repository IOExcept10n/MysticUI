using CommunityToolkit.Diagnostics;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a base for controls that allows presenting a numeric value in provided range.
    /// </summary>
    public abstract class RangeBase : Control
    {
        private double value;

        /// <summary>
        /// Gets or sets the minimal value of the control.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(double.NaN)]
        public virtual double Minimum { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the maximal value of the control.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(double.NaN)]
        public virtual double Maximum { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the current value of the control.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(0)]
        public virtual double Value
        {
            get => value;
            set
            {
                if (value == this.value)
                    return;
                if (ThrowOnOutOfRange)
                {
                    if (!double.IsNaN(Maximum)) Guard.IsLessThanOrEqualTo(value, Maximum);
                    if (!double.IsNaN(Minimum)) Guard.IsGreaterThanOrEqualTo(value, Minimum);
                }
                else
                    value = Math.Clamp(value, Minimum, Maximum);
                this.value = value;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(Value));
            }
        }

        /// <summary>
        /// Gets or sets the value of a small change e.g. when user wants to accurately set a value in the range.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(1)]
        public virtual double SmallChange { get; set; } = 1;

        /// <summary>
        /// Gets or sets the value of large change e.g. when user wants to set a value with keyboard or additional buttons.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(5)]
        public virtual double LargeChange { get; set; } = 5;

        /// <summary>
        /// Determines whether the control should throw an exception when the value is out of presentation range.
        /// </summary>
        /// <remarks>
        /// If the value is set to <see langword="true"/>, the <see cref="ArgumentOutOfRangeException"/> will be thrown.
        /// Otherwise value will be fixed in the provided range.
        /// </remarks>
        [Category("Miscellaneous")]
        [DefaultValue(true)]
        public bool ThrowOnOutOfRange { get; set; } = true;

        /// <summary>
        /// Occurs when the value of the <see cref="Value"/> property changes.
        /// </summary>
        public event EventHandler? ValueChanged;

        /// <summary>
        /// Raises the <see cref="ValueChanged"/> event.
        /// </summary>
        protected internal void OnValueChanged()
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}