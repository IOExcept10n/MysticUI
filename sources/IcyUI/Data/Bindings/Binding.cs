// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup;
using Icy.UI;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Represents a class for property binding. It provides automatic property update for supported objects.
    /// </summary>
    /// <remarks>
    /// This binding implementation can dynamically change source objects and binding modes.
    /// </remarks>
    public sealed class Binding : IBinding
    {
        private readonly object syncLock = new();

        private bool disposedValue;
        private Exception? error;
        private bool hasError;
        private bool isEnabled;
        private object? source;
        private IBindingTarget target;
        private IPropertyReference targetProperty;
        private UpdateSourceTrigger trigger;
        private UpdateTargetTrigger targetTrigger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Binding"/> class.
        /// </summary>
        /// <param name="target">Target object to bind.</param>
        /// <param name="targetProperty">Property of the target object to bind to.</param>
        /// <param name="sourceProperty">Property path of the source object to access.</param>
        public Binding(IBindingTarget target, IPropertyReference targetProperty, IPropertyPath sourceProperty)
        {
            Guard.IsNotNull(target);
            Guard.IsNotNull(targetProperty);
            Guard.IsNotNull(sourceProperty);
            Target = target;
            TargetProperty = targetProperty;
            Path = sourceProperty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Binding"/> class.
        /// </summary>
        /// <remarks>
        /// Note that this initializer makes incomplete binding that is not ready for work.
        /// You'll need to call <see cref="BindableObject.Bind(IBinding)"/>
        /// with the created instance to complete the binding.
        /// </remarks>
        /// <param name="sourceProperty">Property path of the source object to access.</param>
        public Binding(IPropertyPath sourceProperty)
        {
            Path = sourceProperty;

            // Left genuinely unset, not routed through the TargetProperty setter - it dereferences its argument's
            // Metadata unconditionally, which a real "not assigned yet" value can't provide.
            target = null!;
            targetProperty = null!;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Binding"/> class.
        /// </summary>
        ~Binding()
        {
            Dispose(disposing: false);
        }

        /// <summary>
        /// Occurs when <see cref="HasError"/> changes, or when <see cref="Error"/> changes while <see cref="HasError"/>
        /// stays <see langword="true"/>.
        /// </summary>
        public event EventHandler? ErrorChanged;

        /// <summary>
        /// Gets or sets the parameters for the value conversion.
        /// </summary>
        public BindingConverterParameters? ConverterParameters { get; set; }

        /// <summary>
        /// Gets or sets the name of the UI element that should be used as the binding source.
        /// </summary>
        public string? ElementName { get; set; }

        /// <summary>
        /// Gets the failure that put this binding into its error state, or <see langword="null"/> when there is none.
        /// </summary>
        /// <remarks>
        /// For example the type converter's exception for text that doesn't parse, or the exception the source property's own
        /// setter threw. Exceptions wrapped in <see cref="TargetInvocationException"/> are unwrapped.
        /// </remarks>
        public Exception? Error => error;

        /// <summary>
        /// Gets or sets the value that is set when the binding cannot return the value.
        /// </summary>
        public object? FallbackValue { get; set; }

        /// <summary>
        /// Gets a value indicating whether the last write to the source failed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Writing to the source never throws for a bad value: the source keeps its previous value and this flag is set
        /// instead (see <see cref="Error"/>). It clears on the next successful write in either direction.
        /// </para>
        /// <para>
        /// When the target is a <see cref="UIElement"/>, the element carries <see cref="UI.Styles.ControlState.Invalid"/> while
        /// any of its bindings has an error, so styles can show it.
        /// </para>
        /// </remarks>
        public bool HasError => hasError;

        /// <summary>
        /// Gets or sets a value indicating whether the binding is enabled and listening to updates.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                lock (syncLock)
                {
                    return isEnabled;
                }
            }

            set
            {
                lock (syncLock)
                {
                    isEnabled = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value defining the direction of the binding.
        /// </summary>
        public BindingMode Mode { get; set; }

        /// <summary>
        /// Gets the property path to the source object to get values from.
        /// </summary>
        public IPropertyPath Path { get; }

        /// <summary>
        /// Gets or sets the source object to access.
        /// </summary>
        public object? Source
        {
            get
            {
                lock (syncLock)
                {
                    return source;
                }
            }

            set
            {
                lock (syncLock)
                {
                    UnsubscribeSource();
                    source = value;
                    SubscribeSource();
                }
            }
        }

        /// <summary>
        /// Gets the target object to set values to.
        /// </summary>
        [MemberNotNull(nameof(target))]
        public IBindingTarget Target
        {
            get => target ?? ThrowHelper.ThrowArgumentNullException<IBindingTarget>(nameof(target));
            internal set
            {
                Guard.IsNotNull(value);
                UnsubscribeTarget();
                target = value;
                SubscribeTarget();
            }
        }

        /// <summary>
        /// Gets or sets the value of the target property that is set when the source returns <see langword="null"/>.
        /// </summary>
        public object? TargetNullValue { get; set; }

        /// <summary>
        /// Gets or sets the property of the target object to access.
        /// </summary>
        public IPropertyReference TargetProperty
        {
            get => targetProperty;
            [MemberNotNull(nameof(targetProperty))]
            set
            {
                UnsubscribeTarget();
                targetProperty = value;
                if (value.Metadata is UIPropertyMetadata metadata)
                {
                    if (!metadata.IsBindable)
                        ThrowHelper.ThrowArgumentException(nameof(value), "Cannot bind to non-bindable properties.");
                    UpdateSourceTrigger = metadata.DefaultUpdateSourceTrigger;
                    if (metadata.DefaultTwoWayBinding)
                    {
                        Mode = BindingMode.TwoWay;
                    }
                    else
                    {
                        Mode = BindingMode.OneWay;
                    }
                }

                ResetTargetNotification();
            }
        }

        /// <summary>
        /// Gets or sets a value defining target triggers to update source property.
        /// </summary>
        public UpdateSourceTrigger UpdateSourceTrigger
        {
            get => trigger;
            set
            {
                trigger = value;
                ResetTargetNotification();
            }
        }

        /// <inheritdoc/>
        public UpdateTargetTrigger UpdateTargetTrigger
        {
            get => targetTrigger;
            set
            {
                if (targetTrigger == value)
                    return;
                targetTrigger = value;
                Dispatcher dispatcher = Dispatcher.GetCurrentThreadDispatcher();
                if (value == UpdateTargetTrigger.EveryFrame)
                    dispatcher.RegisterFrameBinding(this);
                else
                    dispatcher.UnregisterFrameBinding(this);
            }
        }

        /// <inheritdoc/>
        public void DestroyBinding()
        {
            if (disposedValue) return;
            Target.Unbind(this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public void UpdateSource()
        {
            if (disposedValue || !IsEnabled)
                return;
            if (Source == null)
                throw new BindingException("Source is null.");

            try
            {
                SetValueToSource();
                ClearError();
            }
            catch (Exception ex)
            {
                if (FallbackValue != null)
                {
                    try
                    {
                        Path.SetValue(Source, FallbackValue);
                    }
                    catch (Exception)
                    {
                        // The error is recorded below; a failing fallback must not break the never-throw rule.
                    }
                }

                SetError(ex);
            }

            if (Mode == BindingMode.OneTime)
                DestroyBinding();
        }

        /// <inheritdoc/>
        public void UpdateTarget()
        {
            try
            {
                if (disposedValue || !IsEnabled)
                    return;
                IsEnabled = false;
                SetValueToTarget();
                ClearError();
                IsEnabled = true;
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                if (FallbackValue != null)
                {
                    TargetProperty.SetRawValue(Target, FallbackValue);
                }
                else
                {
                    throw new BindingException("Cannot set value to the target.", ex);
                }
            }

            if (Mode == BindingMode.OneTime)
                DestroyBinding();
        }

        /// <inheritdoc/>
        void IBinding.SetSource(object source)
        {
            Source = source;
        }

        /// <inheritdoc/>
        void IBinding.SetTarget(IBindingTarget target)
        {
            Target = target;
        }

        private object? ConvertValueToSource(object? value)
        {
            if (ConverterParameters != null)
                return ConverterParameters.Converter.ConvertTo(null, ConverterParameters.Culture, value, Path.PropertyType);
            return value is string text ? ConvertTextToSourceType(text) : value;
        }

        /// <summary>
        /// Converts typed text to the source property's type with its <see cref="TypeConverter"/> and
        /// <see cref="CultureInfo.CurrentCulture"/>. Empty text becomes <see langword="null"/> for types that accept it.
        /// </summary>
        private object? ConvertTextToSourceType(string text)
        {
            Type type = Path.PropertyType;
            if (type == typeof(string) || type == typeof(object))
                return text;

            Type? underlying = Nullable.GetUnderlyingType(type);
            if (text.Length == 0)
            {
                if (underlying != null || !type.IsValueType)
                    return null;
                throw new FormatException($"An empty value can't be converted to '{type.Name}'.");
            }

            return TypeDescriptor.GetConverter(underlying ?? type).ConvertFromString(null, CultureInfo.CurrentCulture, text);
        }

        /// <summary>
        /// Formats a non-<see langword="string"/> value as text when the target property is <see langword="string"/>-typed
        /// (e.g. an <see langword="enum"/> or a number bound into <see cref="UI.Controls.TextBlock.Text"/>), using the
        /// converter culture when one is set, otherwise <see cref="CultureInfo.CurrentCulture"/>. Any other value passes through unchanged.
        /// </summary>
        private object? CoerceToTargetType(object? value)
        {
            if (value is null or string || TargetProperty.PropertyType != typeof(string))
                return value;

            return Convert.ToString(value, ConverterParameters?.Culture ?? CultureInfo.CurrentCulture);
        }

        private object? ConvertValueToTarget(object? value)
        {
            object? result = value;
            if (ConverterParameters != null)
            {
                result = ConverterParameters.Converter.ConvertFrom(null, ConverterParameters.Culture, value!);
            }

            return result;
        }

        private void SetError(Exception exception)
        {
            Exception cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
            bool changed = !hasError || !ReferenceEquals(error, cause);
            hasError = true;
            error = cause;
            ReportErrorToTarget(true);
            if (changed)
                ErrorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearError()
        {
            if (!hasError)
                return;
            hasError = false;
            error = null;
            ReportErrorToTarget(false);
            ErrorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ReportErrorToTarget(bool inError)
        {
            if (Target is UIElement element)
                element.SetBindingError(this, inError);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    IsEnabled = false;

                    // Only on an explicit Dispose: from the finalizer this would touch the target element on the finalizer
                    // thread, where the dispatcher's thread check throws and takes the process down.
                    ClearError();
                }

                UnsubscribeSource();
                UnsubscribeTarget();
                if (targetTrigger == UpdateTargetTrigger.EveryFrame)
                    Dispatcher.GetCurrentThreadDispatcher().UnregisterFrameBinding(this);
                Source = null!;
                disposedValue = true;
            }
        }

        private void ResetTargetNotification()
        {
            UnsubscribeTarget();
            SubscribeTarget();
        }

        private void SetValueToSource()
        {
            IsEnabled = false;
            try
            {
                object? value = TargetProperty.GetRawValue(Target);
                Path.SetValue(Source!, ConvertValueToSource(value));
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void SetValueToTarget()
        {
            var value = Source == null ? null : Path.GetValue(Source);
            object? result = ConvertValueToTarget(value);
            result ??= TargetNullValue;
            result = CoerceToTargetType(result);
            var validation = TargetProperty.ValidationCallback?.Invoke(result!);
            if (validation == ValidationResult.Success)
            {
                TargetProperty.SetRawValue(target, result);
            }
            else if (FallbackValue != null)
            {
                TargetProperty.SetRawValue(Target, FallbackValue);
            }
            else
            {
                throw new ValidationException(validation!, null, result);
            }
        }

        private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Mode != BindingMode.OneWayToSource)
                UpdateTarget();
        }

        private void SubscribeSource()
        {
            if (Source is INotifyPropertyChanged notify)
                notify.PropertyChanged += Source_PropertyChanged;
        }

        private void SubscribeTarget()
        {
            if (UpdateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
                Target.PropertyChanged += Target_PropertyChanged;
            if (UpdateSourceTrigger == UpdateSourceTrigger.LostFocus && Target is INotifyFocusChanged focus)
                focus.FocusChanged += Target_FocusChanged;
        }

        private void Target_FocusChanged(object? sender, EventArgs e)
        {
            if (Mode != BindingMode.OneWay)
                UpdateSource();
        }

        private void Target_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Mode != BindingMode.OneWay && e.PropertyName == TargetProperty.Name)
                UpdateSource();
        }

        private void UnsubscribeSource()
        {
            if (Source is INotifyPropertyChanged notify)
                notify.PropertyChanged -= Source_PropertyChanged;
        }

        private void UnsubscribeTarget()
        {
            // Use the backing field directly, not the Target property - it throws when there's no target yet,
            // which is exactly the case the first time this runs (from the Target/constructor setter).
            if (target == null)
                return;
            target.PropertyChanged -= Target_PropertyChanged;
            if (target is INotifyFocusChanged focus)
                focus.FocusChanged -= Target_FocusChanged;
        }
    }
}