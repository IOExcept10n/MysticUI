using AquaUI.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace AquaUI.Data
{
    /// <summary>
    /// Represents a class for property binding. It provides automatic property update for supported objects.
    /// </summary>
    /// <remarks>
    /// This binding implementation can dynamically change source objects and binding modes.
    /// </remarks>
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public sealed class Binding : IBinding
    {
        private bool disposedValue;

        private object? source;
        private PropertyInfo? sourceProperty;

        private string? path;
        /// <summary>
        /// Determines whether the value can be assigned from source to target without conversion.
        /// </summary>
        private bool toTargetByPass;

        /// <summary>
        /// Determines whether the value can be assigned from target to source without conversion.
        /// </summary>
        private bool toSourceByPass;

        /// <summary>
        /// Indicates if the binding is enabled. 
        /// </summary>
        /// <remarks>
        /// It is also used to solve cyclic two-sided bindings: 
        /// if the binding updates an object that implements <see cref="INotifyPropertyChanged"/>,
        /// binding can receive recursive callback. This will lead to stack overflow, so this property disables binding while it is processed.
        /// Note that this is still not thread-safe so it can have unexpected behavior with concurrency.
        /// </remarks>
        public bool Enabled = true;

        /// <summary>
        /// Target object for the binding.
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// Source object for the binding.
        /// </summary>
        public object? Source
        {
            get => source;
            set
            {
                if (source == value)
                    return;
                UnbindSource(source);
                source = value;
                BindSource(source);
                OnSourceChanged(this, source);
            }
        }

        /// <summary>
        /// Target property for the binding.
        /// </summary>
        public PropertyInfo TargetProperty { get; }

        /// <summary>
        /// Source property of the binding.
        /// </summary>
        public PropertyInfo? SourceProperty => sourceProperty;

        /// <summary>
        /// Source property path for binding.
        /// </summary>
        public string? Path
        {
            get => path;
            set
            {
                path = value;
                if (Source != null) UpdateSourceProperty(Source);
            }
        }

        /// <summary>
        /// Property update mode for the binding.
        /// </summary>
        public BindingMode Mode { get; set; }

        /// <summary>
        /// Defines how should binding update source value according to target change.
        /// </summary>
        public UpdateSourceTrigger UpdateSourceTrigger { get; set; }

        /// <summary>
        /// Determines whether binding should update value on any property change without property name check.
        /// </summary>
        /// <remarks>
        /// This can be used if property change isn't implemented correctly in source object and you can't fix this in source class.
        /// </remarks>
        public bool UpdateOnAnyProperty { get; set; }
        
        /// <summary>
        /// Name of a named control in desktop if available.
        /// </summary>
        /// <remarks>
        /// If set, source object is ignored and this property is used for source detection.
        /// It is used to store layout control references in bindings without passing any other context before bindings initialization.
        /// </remarks>
        internal string? SourcePath { get; set; }

        /// <summary>
        /// Creates a new binding to target.
        /// </summary>
        /// <param name="target">Target object to bind.</param>
        /// <param name="targetProperty">Property of the target object to bind to.</param>
        public Binding(object target, PropertyInfo targetProperty)
        {
            Target = target;
            TargetProperty = targetProperty;
            if (!Target.GetType().IsAssignableTo(targetProperty.DeclaringType))
                throw new ArgumentException($"Target and property class should represent the same type.");
            if (target is INotifyPropertyChanged notifyProperty)
            {
                notifyProperty.PropertyChanged += OnTargetPropertyChanged!;
            }
            if (target is INotifyFocusChanged notifyFocus)
            {
                notifyFocus.FocusChanged += OnTargetLostFocus!;
            }
        }

        /// <summary>
        /// Creates new property binding and adds it to a target if it's available.
        /// </summary>
        /// <param name="target">Target to add binding to.</param>
        /// <param name="targetProperty">Target property to bind.</param>
        /// <returns>Created binding object.</returns>
        public static Binding CreateBinding(object target, PropertyInfo targetProperty)
        {
            Binding binding = new(target, targetProperty);
            var overrideAttribute = targetProperty.GetCustomAttribute<BindingOverrideAttribute>();
            if (overrideAttribute != null)
            {
                binding.Mode = overrideAttribute.ModeOverride;
            }
            if (target is IBindingTarget bindingHandler)
            {
                bindingHandler.Bind(binding);
            }
            return binding;
        }

        /// <summary>
        /// Updates source property value with current target property value.
        /// </summary>
        /// <exception cref="FormatException">Occurs when target value can't be converted to source property type.</exception>
        public void UpdateSource()
        {
            if (SourceProperty == null || !SourceProperty.CanWrite || !Enabled) return;
            Enabled = false;
            var value = TargetProperty.GetValue(Target);
            if (toSourceByPass || TargetProperty.PropertyType.IsAssignableTo(SourceProperty.PropertyType) || SourceProperty.PropertyType.IsByRef && value == null)
            {
                SourceProperty.SetValue(Source, value);
            }
            else if (SourceProperty.PropertyType == typeof(string))
            {
                SourceProperty.SetValue(Source, Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            else if (value is string propertyString && LayoutSerializer.Default.GetPropertyState(SourceProperty) == LayoutSerializer.PropertyState.Parsable)
            {
                SourceProperty.SetValue(Source, LayoutSerializer.Default.ParseProperty(propertyString, SourceProperty.PropertyType));
            }
            else if (TargetProperty.PropertyType.IsGenericType && TargetProperty.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                Type type = TargetProperty.PropertyType.GenericTypeArguments[0];
                if (type == SourceProperty.PropertyType || type.IsAssignableTo(SourceProperty.PropertyType))
                {
                    if (value != null)
                    {
                        SourceProperty.SetValue(Target, value);
                    }
                }
            }
            else if (value != null)
            {
                var converter = TypeDescriptor.GetConverter(value);
                if (converter.CanConvertTo(SourceProperty.PropertyType))
                {
                    SourceProperty.SetValue(Source, converter.ConvertTo(value, SourceProperty.PropertyType));
                }
                else
                {
                    throw new FormatException($"Type of the value provided in a property ({value.GetType().FullName}) is not valid for target type ({SourceProperty.PropertyType.FullName}).");
                }
            }
            Enabled = true;
        }

        /// <summary>
        /// Updates target property value with current source property value.
        /// </summary>
        /// <exception cref="FormatException">Occurs when source value can't be converted to target property type.</exception>
        public void UpdateTarget()
        {
            if (SourceProperty == null || !Enabled) return;
            Enabled = false;
            var value = SourceProperty.GetValue(source);
            if (toTargetByPass || SourceProperty.PropertyType.IsAssignableTo(TargetProperty.PropertyType) || TargetProperty.PropertyType.IsByRef && value == null)
            {
                TargetProperty.SetValue(Target, value);
            }
            else if (TargetProperty.PropertyType == typeof(string))
            {
                TargetProperty.SetValue(Target, Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            else if (value is string propertyString && LayoutSerializer.Default.GetPropertyState(TargetProperty) == LayoutSerializer.PropertyState.Parsable)
            {
                TargetProperty.SetValue(Target, LayoutSerializer.Default.ParseProperty(propertyString, TargetProperty.PropertyType));
            }
            else if (SourceProperty.PropertyType.IsGenericType && SourceProperty.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                Type type = SourceProperty.PropertyType.GenericTypeArguments[0];
                if (type == TargetProperty.PropertyType || type.IsAssignableTo(TargetProperty.PropertyType))
                {
                    if (value != null)
                    {
                        TargetProperty.SetValue(Target, value);
                    }
                }
            }
            else if (value != null)
            {
                var converter = TypeDescriptor.GetConverter(value);
                if (converter.CanConvertTo(TargetProperty.PropertyType))
                {
                    TargetProperty.SetValue(Target, converter.ConvertTo(value, TargetProperty.PropertyType));
                }
                else
                {
                    throw new FormatException($"Type of the value provided in a property ({value.GetType().FullName}) is not valid for target type ({TargetProperty.PropertyType.FullName}).");
                }
            }
            Enabled = true;
            if (Mode == BindingMode.OneTime) DestroyBinding();
        }

        /// <inheritdoc/>
        public void DestroyBinding()
        {
            if (Target is IBindingTarget bindings)
                bindings.Unbind(this);
            else Dispose();
        }

        private void UnbindSource(object? source)
        {
            if (source == null) return;
            if (source is INotifyPropertyChanged notifyProperty)
            {
                notifyProperty.PropertyChanged -= SourceUpdatedHandler!;
            }
        }

        private void BindSource(object? source)
        {
            if (source == null) return;
            UpdateSourceProperty(source);
            if (source is INotifyPropertyChanged notifyProperty)
            {
                notifyProperty.PropertyChanged += SourceUpdatedHandler!;
            }
        }

        private void SourceUpdatedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (UpdateOnAnyProperty || e.PropertyName == SourceProperty?.Name)
            {
                OnSourceUpdated(this, SourceProperty?.GetValue(source));
            }
        }

        private void OnTargetPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (UpdateOnAnyProperty || e.PropertyName == TargetProperty?.Name)
            {
                OnTargetUpdated(this, TargetProperty?.GetValue(Target) ?? new GenericEventArgs<object?>(null));
            }
        }

        private void OnTargetLostFocus(object sender, EventArgs e)
        {
            if (sender is INotifyFocusChanged { HasFocus: false } && UpdateSourceTrigger == UpdateSourceTrigger.LostFocus)
            {
                UpdateSource();
            }
        }

        private void UpdateSourceProperty(object source)
        {
            if (Path == null) return;
            sourceProperty = source.GetType().GetProperty(Path);
            if (sourceProperty != null)
            {
                toTargetByPass = sourceProperty.PropertyType == TargetProperty.PropertyType || sourceProperty.PropertyType.IsAssignableTo(TargetProperty.PropertyType);
                toSourceByPass = sourceProperty.PropertyType == TargetProperty.PropertyType || sourceProperty.PropertyType.IsAssignableFrom(TargetProperty.PropertyType);
            }
        }

        private void OnSourceChanged(object sender, GenericEventArgs<object?> e)
        {
            UpdateTarget();
        }

        private void OnSourceUpdated(object sender, GenericEventArgs<object?> e)
        {
            if (Mode == BindingMode.OneWay || Mode == BindingMode.TwoWay)
            {
                UpdateTarget();
            }
        }

        private void OnTargetUpdated(object sender, GenericEventArgs<object> e)
        {
            if (UpdateSourceTrigger == UpdateSourceTrigger.PropertyChanged && (Mode == BindingMode.OneWayToSource || Mode == BindingMode.TwoWay))
            {
                UpdateSource();
            }
        }

        /// <summary>
        /// Remove binding between properties.
        /// </summary>
        /// <param name="disposing">Set it to <see langword="true"/> if <see cref="Dispose()"/> method called.</param>
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Enabled = false;
                    Source = null;
                    path = null;
                    if (Target is INotifyPropertyChanged notifyProperty)
                    {
                        notifyProperty.PropertyChanged -= OnTargetPropertyChanged!;
                    }
                    if (Target is INotifyFocusChanged notifyFocus)
                    {
                        notifyFocus.FocusChanged -= OnTargetLostFocus!;
                    }
                    // This line is unnecessary because disposed bindings have already got to be deleted
                    //if (Target is IBindingHandler bindingHandler)
                    //{
                    //    bindingHandler.RemoveBinding(this);
                    //}
                    if (Source != null)
                    {
                        if (Source is INotifyPropertyChanged notifyPropertyChanged)
                        {
                            notifyPropertyChanged.PropertyChanged -= SourceUpdatedHandler!;
                        }
                    }
                }

                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private string GetDebuggerDisplay()
        {
            return $"Binding ({(Source == null ? "" : Source.GetType() + ".")}{Path} -> {Target.GetType()}.{TargetProperty.Name}: {Mode} ({UpdateSourceTrigger}))";
        }
    }
}