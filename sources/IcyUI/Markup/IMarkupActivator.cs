// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// Constructs the objects a markup document declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The loader never calls <see cref="Activator"/> directly; every object it produces comes through here. That
    /// makes construction a single replaceable seam, which is what lets later work slot in without reshaping the
    /// loader: a source generator can supply compiled construction for the types it knows, a container can inject
    /// dependencies, and a designer can substitute stand-ins for types that misbehave outside a running game.
    /// </para>
    /// <para>
    /// Replace the configured one through <see cref="MarkupConfiguration.Activator"/>.
    /// </para>
    /// </remarks>
    public interface IMarkupActivator
    {
        /// <summary>
        /// Creates an instance of <paramref name="type"/>.
        /// </summary>
        /// <param name="type">The type to construct, already resolved from the markup tag.</param>
        /// <returns>The new instance. Never <see langword="null"/>.</returns>
        /// <exception cref="MarkupException">The type could not be constructed.</exception>
        object CreateInstance(Type type);

        /// <summary>
        /// Creates an instance of <paramref name="type"/> using a constructor whose parameters are all satisfied by
        /// <paramref name="constructorArguments"/>, for a type that has no parameterless constructor.
        /// </summary>
        /// <param name="type">The type to construct, already resolved from the markup tag.</param>
        /// <param name="constructorArguments">
        /// The already-converted argument values, keyed by constructor parameter name (case-insensitive).
        /// </param>
        /// <returns>The new instance. Never <see langword="null"/>.</returns>
        /// <exception cref="MarkupException">
        /// <paramref name="type"/> has no single public constructor whose parameters are all covered by
        /// <paramref name="constructorArguments"/>, or construction threw.
        /// </exception>
        /// <remarks>
        /// The default implementation throws, so an existing custom <see cref="IMarkupActivator"/> that only overrides
        /// <see cref="CreateInstance(Type)"/> keeps compiling and fails clearly for a type it can't construct, rather
        /// than silently misbehaving.
        /// </remarks>
        object CreateInstance(Type type, IReadOnlyDictionary<string, object?> constructorArguments) =>
            throw new MarkupException($"'{type.FullName}' has no parameterless constructor, and this {nameof(IMarkupActivator)} doesn't support constructor-argument binding.");

        /// <summary>
        /// Resolves the constructor <paramref name="type"/> should be built through, given the attribute names
        /// available to bind as arguments, for a type with no parameterless constructor.
        /// </summary>
        /// <param name="type">The type to resolve a constructor for.</param>
        /// <param name="availableAttributeNames">The attribute names available on the markup element (case-insensitive).</param>
        /// <returns>The constructor to use.</returns>
        /// <exception cref="MarkupException">No constructor - or more than one - has its parameters fully covered by <paramref name="availableAttributeNames"/>.</exception>
        /// <remarks>
        /// The default implementation throws, matching <see cref="CreateInstance(Type, IReadOnlyDictionary{string, object?})"/>'s
        /// pattern, so an existing custom <see cref="IMarkupActivator"/> that predates this method keeps compiling.
        /// </remarks>
        System.Reflection.ConstructorInfo ResolveConstructor(Type type, IReadOnlyCollection<string> availableAttributeNames) =>
            throw new MarkupException($"'{type.FullName}' has no parameterless constructor, and this {nameof(IMarkupActivator)} doesn't support constructor resolution.");
    }

    /// <summary>
    /// The default <see cref="IMarkupActivator"/>: constructs types through their parameterless constructor.
    /// </summary>
    /// <remarks>
    /// Every built-in control has one, and markup has no syntax for constructor arguments, so this covers the whole
    /// language as it stands.
    /// </remarks>
    public sealed class DefaultMarkupActivator : IMarkupActivator
    {
        /// <inheritdoc/>
        public object CreateInstance(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (type.IsAbstract || type.IsInterface)
                throw new MarkupException($"'{type.FullName}' can't be created because it is {(type.IsInterface ? "an interface" : "abstract")}.");

            if (type.GetConstructor(Type.EmptyTypes) == null)
                throw new MarkupException($"'{type.FullName}' can't be created because it has no parameterless constructor.");

            try
            {
                return System.Activator.CreateInstance(type)!;
            }
            catch (Exception ex)
            {
                throw new MarkupException($"'{type.FullName}' threw while being constructed: {ex.Message}", innerException: ex);
            }
        }

        /// <inheritdoc/>
        public object CreateInstance(Type type, IReadOnlyDictionary<string, object?> constructorArguments)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(constructorArguments);

            System.Reflection.ConstructorInfo constructor = ResolveConstructor(type, (IReadOnlyCollection<string>)constructorArguments.Keys);
            object?[] arguments = [.. constructor.GetParameters().Select(p => constructorArguments[p.Name!])];

            try
            {
                return constructor.Invoke(arguments);
            }
            catch (Exception ex)
            {
                throw new MarkupException($"'{type.FullName}' threw while being constructed: {ex.Message}", innerException: ex);
            }
        }

        /// <inheritdoc/>
        public System.Reflection.ConstructorInfo ResolveConstructor(Type type, IReadOnlyCollection<string> availableAttributeNames)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(availableAttributeNames);

            var available = new HashSet<string>(availableAttributeNames, StringComparer.OrdinalIgnoreCase);
            var candidates = type.GetConstructors()
                .Where(c => c.GetParameters().Length > 0 && c.GetParameters().All(p => available.Contains(p.Name!)))
                .ToList();

            if (candidates.Count == 1)
                return candidates[0];

            throw new MarkupException(candidates.Count == 0
                ? $"'{type.FullName}' has no parameterized constructor whose parameters are all covered by its markup attributes ({string.Join(", ", available)})."
                : $"'{type.FullName}' has {candidates.Count} constructors whose parameters are all covered by its markup attributes - markup construction requires exactly one match.");
        }
    }
}
