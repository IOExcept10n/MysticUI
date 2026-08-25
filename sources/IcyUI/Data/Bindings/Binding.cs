// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
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
        /// Gets or sets the parameters for the value conversion.
        /// </summary>
        public BindingConverterParameters? ConverterParameters { get; set; }

        /// <summary>
        /// Gets or sets the name of the UI element that should be used as the binding source.
        /// </summary>
        public string? ElementName { get; set; }

        /// <summary>
        /// Gets or sets the value that is set when the binding cannot return the value.
        /// </summary>
        public object? FallbackValue { get; set; }

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
            try
            {
                if (disposedValue || !IsEnabled)
                    return;
                SetValueToSource();
            }
            catch (Exception ex)
            {
                if (FallbackValue != null && Source != null)
                {
                    Path.SetValue(Source, FallbackValue);
                }
                else
                {
                    throw new BindingException("Cannot set value to the source.", ex);
                }
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
            object? result = value;
            if (ConverterParameters != null)
            {
                result = ConverterParameters.Converter.ConvertTo(null, ConverterParameters.Culture, value, Path.PropertyType);
            }

            return result;
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

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    IsEnabled = false;
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
            if (Source == null)
                throw new BindingException("Source is null.");
            IsEnabled = false;
            var value = TargetProperty.GetRawValue(Target);
            object? result = ConvertValueToSource(value);

            Path.SetValue(Source, result);
            IsEnabled = true;
        }

        private void SetValueToTarget()
        {
            var value = Source == null ? null : Path.GetValue(Source);
            object? result = ConvertValueToTarget(value);
            result ??= TargetNullValue;
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