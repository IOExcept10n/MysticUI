// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Icy.Data;

namespace Icy.Markup
{
    /// <summary>
    /// Resolves markup names - element tags, attached-property owners, and type-valued directives - to CLR types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two forms exist. A <em>prefixed</em> name is resolved through the XML namespace its prefix is bound to,
    /// which must be a <c>clr-namespace</c> namespace naming a CLR namespace and, optionally, an assembly. An
    /// <em>unprefixed</em> name is looked for among the built-in controls first, then among the short names
    /// registered on <see cref="MarkupConfiguration"/>.
    /// </para>
    /// <para>
    /// Built-ins deliberately win over short names, and registering a short name over a built-in is rejected at
    /// registration time - see <see cref="MarkupConfiguration.RegisterShortName(Type, string?)"/>.
    /// </para>
    /// </remarks>
    /// <param name="markup">The markup configuration supplying built-in namespaces and short names.</param>
    /// <param name="assemblies">The resolver used to find assemblies and fully-qualified types.</param>
    public sealed class MarkupTypeResolver(MarkupConfiguration markup, IAssemblyResolver assemblies)
    {
        /// <summary>
        /// Resolves an element or attached-property-owner name to a type.
        /// </summary>
        /// <param name="name">The XML name to resolve.</param>
        /// <param name="node">The node the name appears on, for error positions.</param>
        /// <param name="sourcePath">The markup file's path, for error messages.</param>
        /// <returns>The resolved type.</returns>
        /// <exception cref="MarkupException">The name doesn't resolve to any type.</exception>
        public Type Resolve(XName name, IXmlLineInfo? node, string? sourcePath)
        {
            ArgumentNullException.ThrowIfNull(name);

            if (TryResolve(name, out Type? type, out string? failure))
                return type;

            throw MarkupException.At(failure, node, sourcePath);
        }

        /// <summary>
        /// Attempts to resolve an element or attached-property-owner name to a type.
        /// </summary>
        /// <param name="name">The XML name to resolve.</param>
        /// <param name="type">The resolved type, when this returns <see langword="true"/>.</param>
        /// <param name="failure">A message describing why resolution failed, when this returns <see langword="false"/>.</param>
        /// <returns><see langword="true"/> when the name resolved.</returns>
        public bool TryResolve(XName name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? type, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? failure)
        {
            ArgumentNullException.ThrowIfNull(name);

            XNamespace ns = name.Namespace;
            if (MarkupNamespaces.IsBuiltIn(ns))
            {
                type = markup.FindBuiltIn(name.LocalName);
                if (type == null && markup.ShortNames.TryGetValue(name.LocalName, out Type? shortName))
                    type = shortName;

                failure = type != null
                    ? null
                    : $"Unknown type '{name.LocalName}'. It is not a built-in control and no short name is registered for it.{NameSuggestion.Clause(name.LocalName, markup.EnumerateUnprefixedNames())}";
                return type != null;
            }

            return TryResolveClrNamespace(ns.NamespaceName, name.LocalName, out type, out failure);
        }

        /// <summary>
        /// Resolves a type named in an attribute <em>value</em>, such as <c>TargetType</c>, <c>x:Class</c>, or
        /// <c>x:DataType</c>.
        /// </summary>
        /// <param name="typeName">
        /// Either a prefix-qualified name (<c>local:PlayerViewModel</c>), resolved through
        /// <paramref name="scope"/>'s namespace declarations; an unprefixed name that resolves against
        /// <paramref name="scope"/>'s default namespace the same way an element tag would (<c>Button</c>, when the
        /// default namespace is <see cref="MarkupNamespaces.IsBuiltIn(XNamespace)">built-in</see>); or, failing
        /// that, a plain CLR name (<c>MyGame.UI.PlayerViewModel</c>), resolved through
        /// <see cref="IAssemblyResolver.FindType"/>.
        /// </param>
        /// <param name="scope">The element the attribute appears on, whose prefixes are in scope.</param>
        /// <param name="node">The node the name appears on, for error positions.</param>
        /// <param name="sourcePath">The markup file's path, for error messages.</param>
        /// <returns>The resolved type.</returns>
        /// <exception cref="MarkupException">The name doesn't resolve to any type.</exception>
        /// <remarks>
        /// The built-in/short-name attempt exists so <c>TargetType="Button"</c> resolves the same
        /// <see cref="Icy.UI.Controls.Button"/> a <c>&lt;Button&gt;</c> tag would, instead of falling straight to
        /// <see cref="IAssemblyResolver.FindType"/>'s unqualified, load-order-dependent scan of every type in every
        /// loaded assembly - which a short, common control name can resolve incorrectly once a host (a WinForms-
        /// backed game window, say) has loaded an unrelated same-named type first.
        /// </remarks>
        public Type ResolveTypeName(string typeName, XElement scope, IXmlLineInfo? node, string? sourcePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
            ArgumentNullException.ThrowIfNull(scope);

            int separator = typeName.IndexOf(':');
            if (separator >= 0)
            {
                string prefix = typeName[..separator];
                string localName = typeName[(separator + 1)..];
                XNamespace? ns = scope.GetNamespaceOfPrefix(prefix)
                    ?? throw MarkupException.At($"Undeclared namespace prefix '{prefix}' in '{typeName}'.", node, sourcePath);
                return Resolve(ns + localName, node, sourcePath);
            }

            XNamespace defaultNamespace = scope.GetDefaultNamespace();
            if (MarkupNamespaces.IsBuiltIn(defaultNamespace) && TryResolve(defaultNamespace + typeName, out Type? builtInType, out _))
                return builtInType;

            return assemblies.FindType(typeName)
                ?? throw MarkupException.At($"Unknown type '{typeName}'.", node, sourcePath);
        }

        private static IEnumerable<string> EnumerateNames(Assembly assembly, string clrNamespace)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.OfType<Type>()];
            }

            return types.Where(x => x.IsPublic && x.Namespace == clrNamespace).Select(x => x.Name);
        }

        private bool TryResolveClrNamespace(string namespaceName, string localName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? type, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? failure)
        {
            type = null;
            if (!namespaceName.StartsWith(MarkupNamespaces.ClrNamespacePrefix, StringComparison.Ordinal))
            {
                failure =
                    $"XML namespace '{namespaceName}' doesn't address any types. Use '{MarkupNamespaces.Default}' for built-in controls, " +
                    $"or '{MarkupNamespaces.ClrNamespacePrefix}My.Namespace;assembly=MyAssembly' for your own.";
                return false;
            }

            string declaration = namespaceName[MarkupNamespaces.ClrNamespacePrefix.Length..];
            string clrNamespace = declaration;
            Assembly? assembly = assemblies.DefaultAssembly;

            int assemblySeparator = declaration.IndexOf(';');
            if (assemblySeparator >= 0)
            {
                clrNamespace = declaration[..assemblySeparator].Trim();
                string assemblyPart = declaration[(assemblySeparator + 1)..].Trim();
                if (!assemblyPart.StartsWith(MarkupNamespaces.AssemblyAssignment, StringComparison.Ordinal))
                {
                    failure = $"Malformed namespace '{namespaceName}'. Expected '{MarkupNamespaces.ClrNamespacePrefix}My.Namespace;{MarkupNamespaces.AssemblyAssignment}MyAssembly'.";
                    return false;
                }

                string assemblyName = assemblyPart[MarkupNamespaces.AssemblyAssignment.Length..].Trim();
                assembly = FindAssembly(assemblyName);
                if (assembly == null)
                {
                    failure = $"Assembly '{assemblyName}' (from namespace '{namespaceName}') isn't loaded.";
                    return false;
                }
            }

            type = assembly.GetType($"{clrNamespace}.{localName}");
            failure = type != null
                ? null
                : $"Unknown type '{clrNamespace}.{localName}' in assembly '{assembly.GetName().Name}'.{NameSuggestion.Clause(localName, EnumerateNames(assembly, clrNamespace))}";
            return type != null;
        }

        private Assembly? FindAssembly(string name)
        {
            // IAssemblyResolver.FindAssembly matches on the full name, but markup names assemblies the way a project
            // reference does - by simple name - so try that first and fall back to the resolver for a full one.
            return assemblies.GetAssemblies().FirstOrDefault(x => string.Equals(x.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                ?? assemblies.FindAssembly(name);
        }
    }
}
