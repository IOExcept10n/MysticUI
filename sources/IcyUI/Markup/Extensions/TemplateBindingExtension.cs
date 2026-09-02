// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Bindings;
using Icy.UI;

namespace Icy.Markup.Extensions
{
    /// <summary>
    /// <c>{TemplateBinding Path}</c>: inside a <see cref="Icy.UI.Styles.ControlTemplate"/>'s content, binds a
    /// property to a path evaluated against the control the template is being applied to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is how a template's own elements reach the control they decorate - <see cref="Icy.UI.Styles.Style"/>/
    /// <see cref="Icy.UI.Styles.VisualState"/> setters still only ever target the control's own top-level
    /// properties (e.g. <c>Background</c>), exactly as without a template; a <c>{TemplateBinding Background}</c>
    /// on some element inside the template then reflects whatever value that property currently has.
    /// </para>
    /// <para>
    /// Deliberately narrower than <c>{Binding}</c>: always one-way, from the templated control, refreshed
    /// automatically whenever the control raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>
    /// (the default <see cref="UpdateTargetTrigger.Reactive"/> behavior every <see cref="Binding"/> already has) -
    /// no <c>ElementName</c>/<c>Source</c>/<c>Mode</c>/<c>UpdateTargetTrigger</c> surface, since none of those make
    /// sense for the control this template belongs to.
    /// </para>
    /// </remarks>
    [MarkupExtensionDefaultProperty(nameof(Path))]
    public sealed class TemplateBindingExtension : IMarkupExtension
    {
        /// <summary>
        /// Gets or sets the path, relative to the templated control, to bind to.
        /// </summary>
        public string? Path { get; set; }

        /// <inheritdoc/>
        /// <exception cref="MarkupException">
        /// This extension was used outside a <see cref="Icy.UI.Styles.ControlTemplate"/>'s content, the target
        /// doesn't support bindings, the property isn't a registered one, or <see cref="Path"/> is unset.
        /// </exception>
        public object? ProvideValue(MarkupExtensionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.TemplatedControl is not { } templatedControl)
            {
                throw MarkupException.At(
                    "'{TemplateBinding}' can only be used inside a ControlTemplate's content.",
                    context.Node,
                    context.SourcePath);
            }

            if (context.Target is not IBindingTarget bindingTarget)
                throw MarkupException.At($"'{context.Target.GetType().Name}' doesn't support bindings.", context.Node, context.SourcePath);

            if (context.Member.Reference is not { } targetProperty)
            {
                throw MarkupException.At(
                    $"'{context.Member.Name}' isn't a registered property, so it can't be a binding target.",
                    context.Node,
                    context.SourcePath);
            }

            if (string.IsNullOrEmpty(Path))
                throw MarkupException.At("'{TemplateBinding}' needs a Path - unlike {Binding}, there's no sensible 'bind the whole control' default.", context.Node, context.SourcePath);

            var binding = new Binding(new DynamicPropertyPath(Path));
            bindingTarget.Bind(binding);

            try
            {
                binding.TargetProperty = targetProperty;
            }
            catch (Exception ex)
            {
                bindingTarget.Unbind(binding);
                throw MarkupException.At(ex.Message, context.Node, context.SourcePath, ex);
            }

            binding.Mode = BindingMode.OneWay;
            binding.Source = templatedControl;
            binding.IsEnabled = true;
            binding.UpdateTarget();

            return MarkupValue.Unset;
        }
    }
}
