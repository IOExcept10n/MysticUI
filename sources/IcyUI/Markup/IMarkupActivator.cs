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
    }
}
