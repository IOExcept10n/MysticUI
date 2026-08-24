// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;

namespace Icy.Markup
{
    /// <summary>
    /// The XML namespaces the markup language defines.
    /// </summary>
    public static class MarkupNamespaces
    {
        /// <summary>
        /// The namespace of the built-in IcyUI controls: <c>https://icyui.dev/markup</c>.
        /// </summary>
        /// <remarks>
        /// Implicit - a document that declares no default namespace at all is already in this one, so
        /// <c>&lt;StackPanel/&gt;</c> works with no <c>xmlns</c> anywhere in the file. Stating it explicitly is
        /// supported and equivalent.
        /// </remarks>
        public const string Default = "https://icyui.dev/markup";

        /// <summary>
        /// The namespace of the <c>x:</c> directives: <c>https://icyui.dev/markup/x</c>.
        /// </summary>
        /// <remarks>
        /// Implicitly bound to the <c>x</c> prefix, so <c>x:Name</c> works without an <c>xmlns:x</c> declaration.
        /// Declaring it explicitly, or binding it to a different prefix, both work.
        /// </remarks>
        public const string Directives = "https://icyui.dev/markup/x";

        /// <summary>
        /// The prefix that marks an XML namespace as naming a CLR namespace: <c>clr-namespace:</c>.
        /// </summary>
        /// <remarks>
        /// Written as <c>clr-namespace:My.Game.UI;assembly=MyGame</c>, mirroring WPF. The <c>;assembly=</c> part is
        /// optional and falls back to <see cref="Data.IAssemblyResolver.DefaultAssembly"/>.
        /// </remarks>
        public const string ClrNamespacePrefix = "clr-namespace:";

        /// <summary>
        /// The assignment that names the assembly inside a <see cref="ClrNamespacePrefix"/> namespace: <c>assembly=</c>.
        /// </summary>
        public const string AssemblyAssignment = "assembly=";

        /// <summary>
        /// Gets <see cref="Default"/> as an <see cref="XNamespace"/>.
        /// </summary>
        public static XNamespace DefaultNamespace { get; } = Default;

        /// <summary>
        /// Gets <see cref="Directives"/> as an <see cref="XNamespace"/>.
        /// </summary>
        public static XNamespace DirectivesNamespace { get; } = Directives;

        /// <summary>
        /// Determines whether <paramref name="ns"/> addresses the built-in controls.
        /// </summary>
        /// <param name="ns">The namespace to test.</param>
        /// <returns>
        /// <see langword="true"/> for both <see cref="XNamespace.None"/> (an undeclared default namespace) and
        /// <see cref="Default"/>.
        /// </returns>
        public static bool IsBuiltIn(XNamespace ns) => ns == XNamespace.None || ns == DefaultNamespace;

        /// <summary>
        /// Determines whether <paramref name="ns"/> is the directive namespace.
        /// </summary>
        /// <param name="ns">The namespace to test.</param>
        /// <returns><see langword="true"/> when <paramref name="ns"/> is <see cref="Directives"/>.</returns>
        public static bool IsDirective(XNamespace ns) => ns == DirectivesNamespace;
    }
}
