using MysticUI.Controls;
using MysticUI.Extensions;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace MysticUI
{
    /// <summary>
    /// Represents a tool for property binding. It provides automatic property update for supported objects.
    /// </summary>
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class Binding : IDisposable
    {
        private object? source;
        private PropertyInfo? sourceProperty;
        private bool toTargetByPass;
        private bool toSourceByPass;
        private string? path;
        private bool computedFirstTime;

        private bool disposedValue;

        /// <summary>
        /// Indicates if the binding is enabled.
        /// </summary>
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
                SourceChanged(this, source);
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
        /// Name of a named control in desktop if available.
        /// </summary>
        /// <remarks>
        /// If set, source object is ignored and this property is used for source detection.
        /// </remarks>
        internal string? XPath { get; set; }

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
        /// Occurs when any source property updates.
        /// </summary>
        public event EventHandler<GenericEventArgs<object?>> SourceUpdated;

        /// <summary>
        /// Occurs when any target property updates.
        /// </summary>
        public event EventHandler<GenericEventArgs<object?>> TargetUpdated;

        /// <summary>
        /// Occurs when source object changes.
        /// </summary>
        public event EventHandler<GenericEventArgs<object?>> SourceChanged;

        /// <summary>
        /// Creates a new binding to target.
        /// </summary>
        /// <param name="target">Target object to bind.</param>
        /// <param name="targetProperty">Property of the target object to bind to.</param>
        public Binding(object target, PropertyInfo targetProperty)
        {
            SourceUpdated = OnSourceUpdated!;
            TargetUpdated = OnTargetUpdated!;
            SourceChanged = OnSourceChanged!;
            Target = target;
            TargetProperty = targetProperty;
            if (!Target.GetType().IsAssignableTo(targetProperty.DeclaringType))
                throw new ArgumentException($"Target and property class should represent the same type.");
            if (target is INotifyPropertyChanged notifyProperty)
            {
                notifyProperty.PropertyChanged += TargetUpdatedHandler!;
            }
            if (target is INotifyFocusChanged notifyFocus)
            {
                notifyFocus.FocusChanged += TargetLostFocusHandler!;
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
            if (target is IBindingHandler bindingHandler)
            {
                bindingHandler.SetBinding(binding);
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
            if (Mode == BindingMode.OneTime && computedFirstTime) return;
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
            computedFirstTime = true;
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
                SourceUpdated(this, SourceProperty?.GetValue(source));
            }
        }

        private void TargetUpdatedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (UpdateOnAnyProperty || e.PropertyName == TargetProperty?.Name)
            {
                TargetUpdated(this, TargetProperty?.GetValue(Target));
            }
        }

        private void TargetLostFocusHandler(object sender, EventArgs e)
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
        protected virtual void Dispose(bool disposing)
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
                        notifyProperty.PropertyChanged -= TargetUpdatedHandler!;
                    }
                    if (Target is INotifyFocusChanged notifyFocus)
                    {
                        notifyFocus.FocusChanged -= TargetLostFocusHandler!;
                    }
                    if (Target is IBindingHandler bindingHandler)
                    {
                        bindingHandler.RemoveBinding(this);
                    }
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
            return $"Binding from {(Source == null ? "" : Source.GetType() + ".")}{Path} to {Target.GetType()}.{TargetProperty.Name}: {Mode} ({UpdateSourceTrigger})";
        }
    }

    /// <summary>
    /// Declares type of update source trigger.
    /// </summary>
    public enum UpdateSourceTrigger
    {
        /// <summary>
        /// Trigger on property value change.
        /// </summary>
        PropertyChanged,

        /// <summary>
        /// Trigger on target focus losing.
        /// </summary>
        LostFocus,

        /// <summary>
        /// Trigger only on calling <see cref="Binding.UpdateSource()"/> method.
        /// </summary>
        Explicit
    }

    /// <summary>
    /// Declares type of property binding.
    /// </summary>
    public enum BindingMode
    {
        /// <summary>
        /// Updates value of one property when other property changes regardless source or target update.
        /// </summary>
        TwoWay,

        /// <summary>
        /// Updates target value when source value changes.
        /// </summary>
        OneWay,

        /// <summary>
        /// Only sets source value one time when <see cref="Binding.Source"/> is set.
        /// </summary>
        OneTime,

        /// <summary>
        /// updates source value when target value changes.
        /// </summary>
        OneWayToSource
    }

    /// <summary>
    /// Indicates that the property can override binding parameters according to its behavior.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class BindingOverrideAttribute : Attribute
    {
        /// <summary>
        /// Get the binding mode that should be used instead of custom when handle this property.
        /// </summary>
        public BindingMode ModeOverride { get; }

        /// <summary>
        /// Indicates that the property can override binding parameters according to its behavior.
        /// </summary>
        /// <param name="mode">The binding mode that should be used instead of custom when handle this property.</param>
        public BindingOverrideAttribute(BindingMode mode)
        {
            ModeOverride = mode;
        }
    }

    /// <summary>
    /// Indicates that the property can't use bindings API.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class NonBindableAttribute : Attribute
    {
        /// <summary>
        /// Determines whether the property shouldn't be used as the source for binding.
        /// </summary>
        public bool AsSource { get; set; } = true;

        /// <summary>
        /// Determines whether the property shouldn't be used as the target for binding.
        /// </summary>
        public bool AsTarget { get; set; } = true;
    }
}