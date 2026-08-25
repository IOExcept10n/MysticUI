// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Bindings;
using Icy.UI;

namespace Icy.Markup.Extensions
{
    /// <summary>
    /// <c>{Binding ...}</c>: binds a property to a path evaluated against an explicit source, a named element, or
    /// - the common case - the target's <see cref="UIElement.DataContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>{Binding Path=Name}</c> can be shortened to <c>{Binding Name}</c> - <c>Path</c> is this extension's
    /// <see cref="MarkupExtensionDefaultPropertyAttribute">default property</see>. <c>{Binding}</c> with no path at
    /// all binds the whole source object.
    /// </para>
    /// <para>
    /// Binding to a property the property-registry marks non-bindable
    /// (<see cref="Data.Bindings.Attributes.NonBindableAttribute.AsTarget"/>, or
    /// <see cref="Data.Markup.UIPropertyMetadata.IsBindable"/>) is an error, not a silent no-op - raised by
    /// <see cref="Binding.TargetProperty"/> itself, which this extension assigns through unchanged.
    /// </para>
    /// <para>
    /// Always resolves to <see cref="MarkupValue.Unset"/>: the binding is applied to the target directly (and, for
    /// the implicit-<see cref="UIElement.DataContext"/> case, re-applied whenever
    /// <see cref="UIElement.DataContextChanged"/> fires), so there is never a single value for the loader to
    /// assign afterward.
    /// </para>
    /// </remarks>
    [MarkupExtensionDefaultProperty(nameof(Path))]
    public sealed class BindingExtension : IMarkupExtension
    {
        /// <summary>
        /// Gets or sets the path, relative to the source, to bind to. Unset (or empty) binds the source itself.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets the <c>x:Name</c> of the element to bind to, instead of the target's
        /// <see cref="UIElement.DataContext"/>.
        /// </summary>
        public string? ElementName { get; set; }

        /// <summary>
        /// Gets or sets the object to bind to directly, instead of the target's <see cref="UIElement.DataContext"/>.
        /// </summary>
        public object? Source { get; set; }

        /// <summary>
        /// Gets or sets the binding's direction. Unset keeps the target property's own default (see
        /// <see cref="Data.Markup.UIPropertyMetadata.DefaultTwoWayBinding"/>).
        /// </summary>
        public BindingMode? Mode { get; set; }

        /// <summary>
        /// Gets or sets when the binding refreshes the target from the source. See
        /// <see cref="Data.Bindings.UpdateTargetTrigger"/>.
        /// </summary>
        public UpdateTargetTrigger UpdateTargetTrigger { get; set; }

        /// <inheritdoc/>
        /// <exception cref="MarkupException">
        /// The target doesn't support bindings, the property isn't a registered one, <see cref="ElementName"/>
        /// names nothing in scope, or the property is marked non-bindable.
        /// </exception>
        public object? ProvideValue(MarkupExtensionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Target is not IBindingTarget bindingTarget)
                throw MarkupException.At($"'{context.Target.GetType().Name}' doesn't support bindings.", context.Node, context.SourcePath);

            if (context.Member.Reference is not { } targetProperty)
            {
                throw MarkupException.At(
                    $"'{context.Member.Name}' isn't a registered property, so it can't be a binding target.",
                    context.Node,
                    context.SourcePath);
            }

            var binding = new Binding(new DynamicPropertyPath(string.IsNullOrEmpty(Path) ? "this" : Path));
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

            if (Mode.HasValue)
                binding.Mode = Mode.Value;
            binding.UpdateTargetTrigger = UpdateTargetTrigger;

            if (Source != null)
            {
                binding.Source = Source;
            }
            else if (ElementName != null)
            {
                if (!context.Names.TryFind(ElementName, out UIElement? element))
                    throw MarkupException.At($"No element named '{ElementName}' is in scope.", context.Node, context.SourcePath);
                binding.Source = element;
            }
            else if (context.Target is UIElement owner)
            {
                binding.Source = owner.DataContext;

                // No explicit source - the binding tracks the target's inherited DataContext for its whole
                // lifetime, not just its value at bind time. See UIElement.DataContextChanged's remarks for why
                // this can't be resolved once and forgotten: the target is commonly still detached from its parent
                // when the extension runs, so there is often no effective DataContext yet to read. Assigning
                // Source alone only rewires the change-notification subscription - it doesn't itself re-pull a
                // value - so the refresh needs the same explicit UpdateTarget() the initial bind uses below.
                owner.DataContextChanged += (_, _) =>
                {
                    binding.Source = owner.DataContext;
                    if (binding.Mode != BindingMode.OneWayToSource)
                        binding.UpdateTarget();
                };
            }

            binding.IsEnabled = true;
            if (binding.Mode != BindingMode.OneWayToSource)
                binding.UpdateTarget();

            return MarkupValue.Unset;
        }
    }
}
