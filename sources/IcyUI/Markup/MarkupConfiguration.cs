// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Markup.Extensions;
using Icy.UI;

namespace Icy.Markup
{
    /// <summary>
    /// The markup facet of <see cref="Configuration.ReflectionConfiguration"/>: what the loader may construct, under
    /// what names, and how bare text becomes objects.
    /// </summary>
    /// <remarks>
    /// Reached as <see cref="Configuration.ReflectionConfiguration.Markup"/>, and configured fluently through
    /// <see cref="Configuration.BuildingExtensions.ConfigureMarkup"/>. It lives alongside the assembly resolver,
    /// type converter, and property registry because markup resolution is built entirely out of those three.
    /// </remarks>
    public class MarkupConfiguration
    {
        private readonly Dictionary<string, Type> shortNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> extensions = new(StringComparer.Ordinal)
        {
            ["Binding"] = typeof(BindingExtension),
            ["StaticResource"] = typeof(StaticResourceExtension),
        };

        /// <summary>
        /// Gets or sets the service that constructs the objects a markup document declares.
        /// </summary>
        /// <remarks>
        /// Replace this to take over construction wholesale - see <see cref="IMarkupActivator"/> for what that is
        /// for.
        /// </remarks>
        public IMarkupActivator Activator { get; set; } = new DefaultMarkupActivator();

        /// <summary>
        /// Gets the adapters consulted, in order, to turn bare element text into a value its content property can
        /// hold.
        /// </summary>
        /// <remarks>
        /// Starts with <see cref="UIElementTextAdapter"/>. Insert ahead of it to take precedence for a type it also
        /// claims; append to handle types it doesn't.
        /// </remarks>
        public IList<IMarkupTextAdapter> TextAdapters { get; } = [new UIElementTextAdapter()];

        /// <summary>
        /// Gets the CLR namespaces searched, in order, for an unprefixed element name.
        /// </summary>
        /// <remarks>
        /// Only types in <see cref="UIElement"/>'s own assembly are searched here - this is the built-in control
        /// vocabulary, not a general search path. Custom types come in through a <c>clr-namespace</c> XML namespace
        /// or through <see cref="RegisterShortName{T}"/>.
        /// </remarks>
        public IList<string> BuiltInNamespaces { get; } = ["Icy.UI", "Icy.UI.Controls", "Icy.UI.Styles", "Icy.Animations"];

        /// <summary>
        /// Gets the custom types that may be written unprefixed, keyed by the name markup writes.
        /// </summary>
        public IReadOnlyDictionary<string, Type> ShortNames => shortNames;

        /// <summary>
        /// Gets the markup extension types markup may write as <c>{Name ...}</c>, keyed by the name written inside
        /// the braces.
        /// </summary>
        /// <remarks>
        /// Seeded with <c>"Binding"</c> mapping to <see cref="BindingExtension"/> and <c>"StaticResource"</c>
        /// mapping to <see cref="StaticResourceExtension"/>.
        /// </remarks>
        public IReadOnlyDictionary<string, Type> Extensions => extensions;

        /// <summary>
        /// Registers <typeparamref name="T"/> so markup can write it without a namespace prefix.
        /// </summary>
        /// <typeparam name="T">The type to register.</typeparam>
        /// <param name="name">The name markup writes, or <see langword="null"/> to use the type's own name.</param>
        /// <returns>This configuration, for chaining.</returns>
        /// <exception cref="MarkupException">
        /// <paramref name="name"/> collides with a built-in control, or with a different type already registered
        /// under it.
        /// </exception>
        public MarkupConfiguration RegisterShortName<T>(string? name = null) => RegisterShortName(typeof(T), name);

        /// <summary>
        /// Registers <paramref name="type"/> so markup can write it without a namespace prefix.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="name">The name markup writes, or <see langword="null"/> to use the type's own name.</param>
        /// <returns>This configuration, for chaining.</returns>
        /// <exception cref="MarkupException">
        /// <paramref name="name"/> collides with a built-in control, or with a different type already registered
        /// under it.
        /// </exception>
        /// <remarks>
        /// Shadowing a built-in is rejected here rather than silently allowed, because a document that reads
        /// <c>&lt;Button/&gt;</c> should mean the built-in <see cref="UI.Controls.Button"/> wherever it appears. A
        /// replacement is still perfectly possible - declare a <c>clr-namespace</c> prefix and write
        /// <c>&lt;my:Button/&gt;</c>, where the substitution is visible in the file that depends on it.
        /// </remarks>
        public MarkupConfiguration RegisterShortName(Type type, string? name = null)
        {
            ArgumentNullException.ThrowIfNull(type);

            name ??= type.Name;
            if (FindBuiltIn(name) is { } builtIn)
            {
                throw new MarkupException(
                    $"Can't register '{type.FullName}' as '{name}' because it collides with the built-in '{builtIn.FullName}'. " +
                    $"Use a clr-namespace prefix to write this type instead.");
            }

            if (shortNames.TryGetValue(name, out Type? existing) && existing != type)
                throw new MarkupException($"Can't register '{type.FullName}' as '{name}' because '{existing.FullName}' is already registered under that name.");

            shortNames[name] = type;
            return this;
        }

        /// <summary>
        /// Registers <typeparamref name="T"/> so markup can invoke it as <c>{Name ...}</c>.
        /// </summary>
        /// <typeparam name="T">The extension type to register.</typeparam>
        /// <param name="name">
        /// The name markup writes inside the braces, or <see langword="null"/> to derive one from the type's name
        /// - <c>MyExtension</c> becomes <c>My</c>, following the same <c>Binding</c>/<see cref="BindingExtension"/>
        /// convention as the built-in one.
        /// </param>
        /// <returns>This configuration, for chaining.</returns>
        public MarkupConfiguration RegisterExtension<T>(string? name = null)
            where T : IMarkupExtension, new() => RegisterExtension(typeof(T), name);

        /// <summary>
        /// Registers <paramref name="type"/> so markup can invoke it as <c>{Name ...}</c>.
        /// </summary>
        /// <param name="type">The extension type to register. Must implement <see cref="IMarkupExtension"/>.</param>
        /// <param name="name">
        /// The name markup writes inside the braces, or <see langword="null"/> to derive one from the type's name.
        /// </param>
        /// <returns>This configuration, for chaining.</returns>
        /// <exception cref="MarkupException"><paramref name="type"/> doesn't implement <see cref="IMarkupExtension"/>.</exception>
        public MarkupConfiguration RegisterExtension(Type type, string? name = null)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (!typeof(IMarkupExtension).IsAssignableFrom(type))
                throw new MarkupException($"'{type.FullName}' can't be registered as a markup extension because it doesn't implement {nameof(IMarkupExtension)}.");

            extensions[name ?? DeriveExtensionName(type)] = type;
            return this;
        }

        /// <summary>
        /// Finds the built-in control named <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The unqualified type name to look up.</param>
        /// <returns>The built-in type, or <see langword="null"/> when there is none.</returns>
        public Type? FindBuiltIn(string name)
        {
            foreach (string ns in BuiltInNamespaces)
            {
                if (typeof(UIElement).Assembly.GetType($"{ns}.{name}") is { } type)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// Enumerates every name that can be written unprefixed, for error messages that suggest a near match.
        /// </summary>
        /// <returns>The built-in control names followed by the registered short names.</returns>
        public IEnumerable<string> EnumerateUnprefixedNames()
        {
            foreach (Type type in typeof(UIElement).Assembly.GetTypes())
            {
                if (type.IsPublic && type.Namespace != null && BuiltInNamespaces.Contains(type.Namespace))
                    yield return type.Name;
            }

            foreach (string name in shortNames.Keys)
            {
                yield return name;
            }
        }

        /// <summary>
        /// Derives a default registration name from an extension type: <c>BindingExtension</c> becomes
        /// <c>Binding</c>, and a type not named with the <c>Extension</c> suffix keeps its own name.
        /// </summary>
        private static string DeriveExtensionName(Type type)
        {
            const string suffix = "Extension";
            return type.Name.EndsWith(suffix, StringComparison.Ordinal)
                ? type.Name[..^suffix.Length]
                : type.Name;
        }
    }
}
