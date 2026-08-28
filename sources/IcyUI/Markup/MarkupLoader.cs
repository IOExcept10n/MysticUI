// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Icy.Configuration;
using Icy.Data;
using Icy.Data.Markup;
using Icy.UI;

namespace Icy.Markup
{
    /// <summary>
    /// Builds a <see cref="UIElement"/> tree from a markup document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The loader walks the XML once, and for each element resolves its type, constructs an instance through
    /// <see cref="MarkupConfiguration.Activator"/>, applies its attributes, then applies its children - in that
    /// order, so that a container's own properties are set before anything is added to it.
    /// </para>
    /// <para>
    /// Every object it creates is constructed inside a
    /// <see cref="PropertyRegistry.UseScope(PropertyRegistry)"/> for the configuration's own registry, so the tree
    /// and the loader that resolves its properties always agree on which registry they are using - see the remarks
    /// on <see cref="PropertyRegistry"/> for why mixing two would compute wrong values.
    /// </para>
    /// <para>
    /// Loading is all-or-nothing: the first error raises a <see cref="MarkupException"/> carrying the file position,
    /// and no partially-built tree is returned.
    /// </para>
    /// </remarks>
    /// <param name="configuration">The configuration supplying type resolution, conversion, and the property registry.</param>
    public class MarkupLoader(IcyConfiguration configuration)
    {
        private readonly MarkupConfiguration markup = configuration.Types.Markup;
        private readonly ITypeConverter converter = configuration.Types.TypeConverter;
        private readonly PropertyRegistry registry = configuration.Types.PropertyRegistry;
        private readonly MarkupTypeResolver types = new(configuration.Types.Markup, configuration.Types.AssemblyResolver);

        /// <summary>
        /// Loads a markup document from a string.
        /// </summary>
        /// <param name="text">The markup document.</param>
        /// <param name="sourcePath">The document's path, used only to make error messages locatable.</param>
        /// <returns>The root element the document declares.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the language.</exception>
        public UIElement Load(string text, string? sourcePath = null)
        {
            ArgumentNullException.ThrowIfNull(text);
            using var reader = new StringReader(text);
            return Load(reader, sourcePath);
        }

        /// <summary>
        /// Loads a markup document from a stream.
        /// </summary>
        /// <param name="stream">The stream to read the document from.</param>
        /// <param name="sourcePath">The document's path, used only to make error messages locatable.</param>
        /// <returns>The root element the document declares.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the language.</exception>
        public UIElement Load(Stream stream, string? sourcePath = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            using var reader = new StreamReader(stream, leaveOpen: true);
            return Load(reader, sourcePath);
        }

        /// <summary>
        /// Loads a markup document and requires its root to be a <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type the document's root element is expected to have.</typeparam>
        /// <param name="text">The markup document.</param>
        /// <param name="sourcePath">The document's path, used only to make error messages locatable.</param>
        /// <returns>The root element the document declares.</returns>
        /// <exception cref="MarkupException">
        /// The document is malformed, breaks a rule of the language, or its root is not a <typeparamref name="T"/>.
        /// </exception>
        public T Load<T>(string text, string? sourcePath = null)
            where T : UIElement
        {
            UIElement root = Load(text, sourcePath);
            return root as T
                ?? throw new MarkupException($"Expected a '{typeof(T).Name}' root, but the document declares a '{root.GetType().Name}'.", sourcePath);
        }

        /// <summary>
        /// Loads a markup document from an already-parsed <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader">The reader positioned at the start of the document.</param>
        /// <param name="sourcePath">The document's path, used only to make error messages locatable.</param>
        /// <returns>The root element the document declares.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the language.</exception>
        public UIElement Load(TextReader reader, string? sourcePath = null)
        {
            ArgumentNullException.ThrowIfNull(reader);

            XDocument document = ParseDocument(reader, sourcePath);
            XElement root = document.Root
                ?? throw new MarkupException("The document is empty.", sourcePath);

            var context = new MarkupLoadContext(sourcePath, new MarkupNameScope());

            // Construct the whole tree against the configuration's registry, so every element captures the same one
            // the loader resolves properties through.
            using (PropertyRegistry.UseScope(registry))
            {
                object instance = CreateObject(root, context);
                if (instance is not UIElement element)
                    throw MarkupException.At($"The root element must be a '{nameof(UIElement)}', but '{instance.GetType().Name}' isn't one.", root, sourcePath);

                MarkupNameScope.SetScope(element, context.Names);
                return element;
            }
        }

        /// <summary>
        /// Parses the document text into an <see cref="XDocument"/> with position information.
        /// </summary>
        /// <param name="reader">The reader positioned at the start of the document.</param>
        /// <param name="sourcePath">The document's path, used only to make error messages locatable.</param>
        /// <returns>The parsed document.</returns>
        /// <exception cref="MarkupException">The document is not well-formed XML.</exception>
        /// <remarks>
        /// The <c>x</c> prefix is predeclared on the reader, which is what lets a document use <c>x:Name</c> without
        /// an <c>xmlns:x</c> line of its own. A document that declares the prefix itself is unaffected - its own
        /// declaration simply shadows this one with the same value.
        /// </remarks>
        protected static XDocument ParseDocument(TextReader reader, string? sourcePath)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var nameTable = new NameTable();
            var namespaces = new XmlNamespaceManager(nameTable);
            namespaces.AddNamespace("x", MarkupNamespaces.Directives);

            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            try
            {
                using XmlReader xml = XmlReader.Create(reader, settings, new XmlParserContext(nameTable, namespaces, null, XmlSpace.None));
                return XDocument.Load(xml, LoadOptions.SetLineInfo);
            }
            catch (XmlException ex)
            {
                throw new MarkupException(ex.Message, sourcePath, ex.LineNumber, ex.LinePosition, ex);
            }
        }

        /// <summary>
        /// Reads an element's own text, ignoring the whitespace that indentation puts between child elements.
        /// </summary>
        private static string ReadText(XElement element)
        {
            string text = string.Concat(element.Nodes().OfType<XText>().Select(x => x.Value));
            return text.Trim();
        }

        /// <summary>
        /// Finds a public instance <c>Add</c> method on <paramref name="collection"/>'s runtime type that accepts a
        /// single argument - the same duck-typed rule C#'s own collection-initializer syntax uses, so a property
        /// declared as a read-only interface (<see cref="IReadOnlyList{T}"/>, say) can still be populated as long as
        /// what it actually returns has an <c>Add</c>.
        /// </summary>
        /// <param name="collection">The collection object to inspect.</param>
        /// <returns>A tuple containing the Add method and the type of its parameter, or <see langword="null"/> if no suitable Add method was found.</returns>
        private static (MethodInfo Add, Type ItemType)? FindAddMethod(object collection)
        {
            foreach (MethodInfo method in collection.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != "Add")
                    continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1)
                    return (method, parameters[0].ParameterType);
            }

            return null;
        }

        /// <summary>
        /// Invokes a duck-typed <c>Add</c> method found by <see cref="FindAddMethod"/>, translating a failure
        /// inside the target method into a <see cref="MarkupException"/> carrying file position instead of letting
        /// reflection's <see cref="TargetInvocationException"/> wrapper escape.
        /// </summary>
        /// <param name="addable">The <c>Add</c> method and its parameter type, as returned by <see cref="FindAddMethod"/>.</param>
        /// <param name="collection">The collection instance to add <paramref name="value"/> to.</param>
        /// <param name="value">The already-converted item to add.</param>
        /// <param name="memberLabel">The owning type and property name, for the error message (e.g. <c>"Panel.Children"</c>).</param>
        /// <param name="node">The item's position in the source document, for error reporting.</param>
        /// <param name="context">The load's mutable state.</param>
        private static void InvokeAdd((MethodInfo Add, Type ItemType) addable, object collection, object? value, string memberLabel, IXmlLineInfo? node, MarkupLoadContext context)
        {
            try
            {
                addable.Add.Invoke(collection, [value]);
            }
            catch (TargetInvocationException ex)
            {
                Exception cause = ex.InnerException ?? ex;
                throw MarkupException.At($"'{memberLabel}' failed to add an item: {cause.Message}", node, context.SourcePath, cause);
            }
        }

        private object CreateObject(XElement element, MarkupLoadContext context)
        {
            Type type = ResolveInstanceType(element, context);
            HashSet<string>? consumedByConstructor = null;
            object instance = type.GetConstructor(Type.EmptyTypes) != null
                ? markup.Activator.CreateInstance(type)
                : CreateObjectFromConstructorAttributes(type, element, context, out consumedByConstructor);

            ApplyAttributes(element, instance, context, consumedByConstructor);
            ApplyChildren(element, instance, context);
            return instance;
        }

        /// <summary>
        /// Constructs <paramref name="type"/> through its parameterized public constructor, binding each
        /// parameter to an attribute of the same name (case-insensitive). Constructor selection is delegated to
        /// the activator: the loader gathers candidate attributes, the activator picks the matching constructor.
        /// </summary>
        private object CreateObjectFromConstructorAttributes(Type type, XElement element, MarkupLoadContext context, out HashSet<string> consumed)
        {
            // Gather all candidate attributes (non-directive, non-namespaced) by name
            var candidateAttributes = new Dictionary<string, XAttribute>(StringComparer.OrdinalIgnoreCase);
            foreach (XAttribute attr in element.Attributes())
            {
                if (attr.IsNamespaceDeclaration)
                    continue;
                if (MarkupNamespaces.IsDirective(attr.Name.Namespace))
                    continue;

                candidateAttributes[attr.Name.LocalName] = attr;
            }

            // Let the activator's ResolveConstructor determine which constructor matches the available attributes.
            // This is the single source of truth for constructor selection, and goes through the pluggable activator.
            System.Reflection.ConstructorInfo constructor = markup.Activator.ResolveConstructor(type, (IReadOnlyCollection<string>)candidateAttributes.Keys);
            System.Reflection.ParameterInfo[] parameters = constructor.GetParameters();
            var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (System.Reflection.ParameterInfo parameter in parameters)
            {
                if (!candidateAttributes.TryGetValue(parameter.Name!, out XAttribute? attribute))
                {
                    throw MarkupException.At(
                        $"'{type.Name}' requires an attribute '{parameter.Name}' - it has no parameterless constructor.",
                        element,
                        context.SourcePath);
                }

                object? value = parameter.ParameterType == typeof(Type)
                    ? types.ResolveTypeName(attribute.Value, element, attribute, context.SourcePath)
                    : ConvertValue(attribute.Value, parameter.ParameterType, attribute, context);

                arguments[parameter.Name!] = value;
                consumed.Add(attribute.Name.LocalName);
            }

            return markup.Activator.CreateInstance(type, arguments);
        }

        /// <summary>
        /// Resolves the type to actually instantiate for an element: its <c>x:Class</c> when it declares one, and
        /// its tag's type otherwise.
        /// </summary>
        private Type ResolveInstanceType(XElement element, MarkupLoadContext context)
        {
            Type tagType = types.Resolve(element.Name, element, context.SourcePath);

            XAttribute? backingClass = element.Attribute(MarkupNamespaces.DirectivesNamespace + MarkupDirectives.Class);
            if (backingClass == null)
                return tagType;

            if (element.Parent != null)
                throw MarkupException.At($"{MarkupDirectives.Qualified(MarkupDirectives.Class)} is only valid on the root element.", backingClass, context.SourcePath);

            Type backingType = types.ResolveTypeName(backingClass.Value, element, backingClass, context.SourcePath);
            if (!tagType.IsAssignableFrom(backingType))
            {
                throw MarkupException.At(
                    $"{MarkupDirectives.Qualified(MarkupDirectives.Class)} names '{backingType.FullName}', which does not derive from the tag's type '{tagType.FullName}'.",
                    backingClass,
                    context.SourcePath);
            }

            return backingType;
        }

        private void ApplyAttributes(XElement element, object instance, MarkupLoadContext context, HashSet<string>? consumedByConstructor = null)
        {
            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;

                if (MarkupNamespaces.IsDirective(attribute.Name.Namespace))
                {
                    ApplyDirective(element, instance, attribute, context);
                    continue;
                }

                // Only skip consumed attributes that have no namespace. Namespaced attributes are never constructor parameters
                // (constructor parameters must be matched case-insensitively from plain attributes).
                if (consumedByConstructor != null && attribute.Name.Namespace == XNamespace.None && consumedByConstructor.Contains(attribute.Name.LocalName))
                    continue;

                if (attribute.Name.LocalName.Contains('.', StringComparison.Ordinal))
                {
                    ApplyAttachedProperty(instance, attribute, context);
                    continue;
                }

                ApplyProperty(instance, attribute.Name.LocalName, attribute.Value, attribute, context);
            }
        }

        private void ApplyDirective(XElement element, object instance, XAttribute attribute, MarkupLoadContext context)
        {
            switch (attribute.Name.LocalName)
            {
                case MarkupDirectives.Name:
                    if (instance is not UIElement named)
                        throw MarkupException.At($"{MarkupDirectives.Qualified(MarkupDirectives.Name)} is only valid on a '{nameof(UIElement)}'.", attribute, context.SourcePath);
                    named.Name = attribute.Value;
                    try
                    {
                        context.Names.Register(attribute.Value, named);
                    }
                    catch (MarkupException ex)
                    {
                        throw MarkupException.At(ex.Description, attribute, context.SourcePath, ex);
                    }

                    break;

                case MarkupDirectives.Class:
                    // Already consumed by ResolveInstanceType before the instance existed.
                    break;

                case MarkupDirectives.Key:
                    // Read by the dictionary-population path in ApplyPropertyElement; nothing to do on the object.
                    break;

                case MarkupDirectives.DataType:
                    // Retained for tooling and the future generator to validate binding paths against. It has no
                    // runtime effect, but resolving it here means a misspelled type fails at load, not silently.
                    context.DataTypes[element] = types.ResolveTypeName(attribute.Value, element, attribute, context.SourcePath);
                    break;

                default:
                    throw MarkupException.At(
                        $"Unknown directive '{MarkupDirectives.Qualified(attribute.Name.LocalName)}'.{NameSuggestion.Clause(attribute.Name.LocalName, MarkupDirectives.All)}",
                        attribute,
                        context.SourcePath);
            }
        }

        private void ApplyAttachedProperty(object instance, XAttribute attribute, MarkupLoadContext context)
        {
            string qualified = attribute.Name.LocalName;
            int separator = qualified.LastIndexOf('.');
            string ownerName = qualified[..separator];
            string propertyName = qualified[(separator + 1)..];

            Type ownerType = types.Resolve(attribute.Name.Namespace + ownerName, attribute, context.SourcePath);
            if (!registry.GetPropertyStore(ownerType).TryGetProperty(propertyName, searchInherited: true, out IPropertyReference? property))
            {
                throw MarkupException.At(
                    $"'{ownerType.Name}' has no attached property '{propertyName}'.{NameSuggestion.Clause(propertyName, registry.GetPropertyStore(ownerType).EnumerateProperties().Select(x => x.Name))}",
                    attribute,
                    context.SourcePath);
            }

            if (property.Metadata is not UIPropertyMetadata { IsAttached: true })
                throw MarkupException.At($"'{ownerType.Name}.{propertyName}' is an ordinary property, not an attached one, so it can't be set on another element.", attribute, context.SourcePath);

            AssignValue(instance, MarkupMember.FromReference(property), attribute.Value, attribute, context);
        }

        private void ApplyProperty(object instance, string name, string value, XAttribute attribute, MarkupLoadContext context)
        {
            Type type = instance.GetType();
            MarkupMember? member = MarkupMember.Resolve(type, name, registry);
            if (member == null)
            {
                if (type.GetEvent(name, BindingFlags.Public | BindingFlags.Instance) != null)
                    throw MarkupException.At($"'{type.Name}.{name}' is an event. Wiring handlers from markup isn't supported yet.", attribute, context.SourcePath);

                throw MarkupException.At(
                    $"Unknown property '{name}' on '{type.Name}'.{NameSuggestion.Clause(name, MarkupMember.EnumerateNames(type, registry))}",
                    attribute,
                    context.SourcePath);
            }

            if (!member.CanSet)
                throw MarkupException.At($"'{type.Name}.{name}' is read-only and can't be assigned from an attribute.", attribute, context.SourcePath);

            AssignValue(instance, member, value, attribute, context);
        }

        private void ApplyChildren(XElement element, object instance, MarkupLoadContext context)
        {
            List<XElement> content = [];
            foreach (XElement child in element.Elements())
            {
                if (IsPropertyElement(child, instance.GetType(), context, out string? propertyName))
                    ApplyPropertyElement(instance, propertyName, child, context);
                else
                    content.Add(child);
            }

            if (content.Count > 0)
            {
                ApplyContentChildren(element, instance, content, context);
                return;
            }

            string text = ReadText(element);
            if (text.Length > 0)
                ApplyContentText(element, instance, text, context);
        }

        /// <summary>
        /// Determines whether a child element sets a property of its parent (<c>&lt;Grid.ColumnDefinitions&gt;</c>)
        /// rather than being content.
        /// </summary>
        private bool IsPropertyElement(XElement child, Type parentType, MarkupLoadContext context, [NotNullWhen(true)] out string? propertyName)
        {
            propertyName = null;
            string localName = child.Name.LocalName;
            int separator = localName.LastIndexOf('.');
            if (separator < 0)
                return false;

            string ownerName = localName[..separator];
            Type ownerType = types.Resolve(child.Name.Namespace + ownerName, child, context.SourcePath);
            if (!ownerType.IsAssignableFrom(parentType))
            {
                throw MarkupException.At(
                    $"'{localName}' sets a property of '{ownerType.Name}', but its parent is a '{parentType.Name}'. " +
                    $"Property elements can only set properties of the element they appear inside.",
                    child,
                    context.SourcePath);
            }

            propertyName = localName[(separator + 1)..];
            return true;
        }

        private void ApplyPropertyElement(object instance, string propertyName, XElement child, MarkupLoadContext context)
        {
            Type type = instance.GetType();
            MarkupMember member = MarkupMember.Resolve(type, propertyName, registry)
                ?? throw MarkupException.At(
                    $"Unknown property '{propertyName}' on '{type.Name}'.{NameSuggestion.Clause(propertyName, MarkupMember.EnumerateNames(type, registry))}",
                    child,
                    context.SourcePath);

            object? current = member.GetValue(instance);
            List<XElement> children = [.. child.Elements()];

            if (current is IDictionary dictionary)
            {
                foreach (XElement entry in children)
                {
                    XAttribute key = entry.Attribute(MarkupNamespaces.DirectivesNamespace + MarkupDirectives.Key)
                        ?? throw MarkupException.At($"Entries of '{type.Name}.{propertyName}' need an {MarkupDirectives.Qualified(MarkupDirectives.Key)}.", entry, context.SourcePath);
                    dictionary[key.Value] = CreateObject(entry, context);
                }

                return;
            }

            if (current != null && FindAddMethod(current) is { } addable)
            {
                foreach (XElement entry in children)
                {
                    object? item = ConvertValue(CreateObject(entry, context), addable.ItemType, entry, context);
                    InvokeAdd(addable, current, item, $"{type.Name}.{propertyName}", entry, context);
                }

                return;
            }

            if (children.Count > 1)
                throw MarkupException.At($"'{type.Name}.{propertyName}' holds a single value, but {children.Count} elements were given.", child, context.SourcePath);

            if (!member.CanSet)
                throw MarkupException.At($"'{type.Name}.{propertyName}' is read-only, and isn't a collection that could be populated instead.", child, context.SourcePath);

            object? value = children.Count == 1
                ? CreateObject(children[0], context)
                : ReadText(child);

            AssignValue(instance, member, value, child, context);
        }

        private void ApplyContentChildren(XElement element, object instance, List<XElement> children, MarkupLoadContext context)
        {
            (MarkupMember member, object? current) = ResolveContentProperty(element, instance, context);

            if (current != null && FindAddMethod(current) is { } addable)
            {
                foreach (XElement child in children)
                {
                    object? item = ConvertValue(CreateObject(child, context), addable.ItemType, child, context);
                    InvokeAdd(addable, current, item, $"{instance.GetType().Name}.{member.Name}", child, context);
                }

                return;
            }

            if (children.Count > 1)
            {
                throw MarkupException.At(
                    $"'{instance.GetType().Name}' holds a single piece of content in '{member.Name}', but {children.Count} elements were given. Wrap them in a panel.",
                    children[1],
                    context.SourcePath);
            }

            if (!member.CanSet)
                throw MarkupException.At($"'{instance.GetType().Name}.{member.Name}' is read-only, and isn't a collection that could be populated instead.", element, context.SourcePath);

            member.SetValue(instance, ConvertValue(CreateObject(children[0], context), member.PropertyType, children[0], context));
        }

        private void ApplyContentText(XElement element, object instance, string text, MarkupLoadContext context)
        {
            (MarkupMember member, _) = ResolveContentProperty(element, instance, context);

            if (!member.CanSet)
                throw MarkupException.At($"'{instance.GetType().Name}.{member.Name}' is read-only, so it can't hold this element's text.", element, context.SourcePath);

            // A content property that can hold the string outright takes it directly (still through AssignValue, so
            // {Binding}/{} escaping work here too); anything else needs an adapter to decide what object represents
            // the text - a UIElement content property gets a TextBlock, say.
            if (member.PropertyType.IsAssignableFrom(typeof(string)))
            {
                AssignValue(instance, member, text, element, context);
                return;
            }

            foreach (IMarkupTextAdapter adapter in markup.TextAdapters)
            {
                if (!adapter.CanAdapt(member.PropertyType))
                    continue;

                member.SetValue(instance, adapter.Adapt(text, member.PropertyType));
                return;
            }

            // Fall back to ordinary conversion, which covers value-typed content properties with a parser.
            AssignValue(instance, member, text, element, context);
        }

        private (MarkupMember Member, object? Current) ResolveContentProperty(XElement element, object instance, MarkupLoadContext context)
        {
            Type type = instance.GetType();
            string name = ContentPropertyAttribute.GetContentPropertyName(type)
                ?? throw MarkupException.At(
                    $"'{type.Name}' has no content property, so it can't have children or text. Set a property explicitly, or mark the type with [ContentProperty].",
                    element,
                    context.SourcePath);

            MarkupMember member = MarkupMember.Resolve(type, name, registry)
                ?? throw MarkupException.At($"'{type.Name}' declares '{name}' as its content property, but has no such property.", element, context.SourcePath);

            return (member, member.GetValue(instance));
        }

        /// <summary>
        /// Resolves <paramref name="value"/> - a markup extension, an escaped literal, or an ordinary value - and
        /// assigns it to <paramref name="member"/> on <paramref name="instance"/>, unless the extension resolved to
        /// <see cref="MarkupValue.Unset"/>, in which case nothing is assigned.
        /// </summary>
        /// <remarks>
        /// The one entry point every string-sourced property or content value goes through - attributes, property
        /// elements, and content text alike - so <c>{Binding ...}</c> and the <c>{}</c> escape work uniformly
        /// everywhere a value can be written as text.
        /// </remarks>
        private void AssignValue(object instance, MarkupMember member, object? value, IXmlLineInfo? node, MarkupLoadContext context)
        {
            object? resolved = ConvertValue(value, member.PropertyType, node, context, instance, member);
            if (ReferenceEquals(resolved, MarkupValue.Unset))
                return;

            member.SetValue(instance, resolved);
        }

        /// <summary>
        /// Converts <paramref name="value"/> to <paramref name="targetType"/>, resolving a markup extension first
        /// when it is a <c>{...}</c> string.
        /// </summary>
        /// <param name="value">The raw value: text from an attribute or element content, or an already-built object.</param>
        /// <param name="targetType">The type the resolved value must be assignable to.</param>
        /// <param name="node">The value's position in the source document, for error reporting.</param>
        /// <param name="context">The load's mutable state.</param>
        /// <param name="instance">
        /// The object the value is being resolved for, or <see langword="null"/> when there is none - collection
        /// and dictionary items are converted this way, and can't be a markup extension themselves since they never
        /// come from a string.
        /// </param>
        /// <param name="member">The property the value is being resolved for; required together with <paramref name="instance"/> to resolve a markup extension.</param>
        private object? ConvertValue(object? value, Type targetType, IXmlLineInfo? node, MarkupLoadContext context, object? instance = null, MarkupMember? member = null)
        {
            if (value is string text)
            {
                if (text.StartsWith("{}", StringComparison.Ordinal))
                {
                    // The XAML escape for a literal value that genuinely starts with a brace.
                    value = text[2..];
                }
                else if (text.StartsWith('{'))
                {
                    if (instance == null || member == null)
                        throw MarkupException.At($"'{text}' looks like a markup extension, but this position doesn't support them. Escape it as '{{}}{text}' if it's meant literally.", node, context.SourcePath);

                    return ResolveExtension(text, instance, member, node, context);
                }
            }

            if (value == null || targetType.IsInstanceOfType(value))
                return value;

            try
            {
                return converter.Convert(value, targetType);
            }
            catch (Exception ex)
            {
                throw MarkupException.At($"Can't convert '{value}' to '{targetType.Name}': {ex.Message}", node, context.SourcePath, ex);
            }
        }

        /// <summary>
        /// Parses and evaluates a <c>{Name ...}</c> markup extension.
        /// </summary>
        private object? ResolveExtension(string text, object instance, MarkupMember member, IXmlLineInfo? node, MarkupLoadContext context)
        {
            if (!MarkupExtensionSyntax.TryParse(text, out string name, out List<(string? Key, string Value)> arguments))
                throw MarkupException.At($"'{text}' isn't a valid markup extension.", node, context.SourcePath);

            if (!markup.Extensions.TryGetValue(name, out Type? extensionType))
                throw MarkupException.At($"Unknown markup extension '{{{name}}}'.{NameSuggestion.Clause(name, markup.Extensions.Keys)}", node, context.SourcePath);

            var extension = (IMarkupExtension)markup.Activator.CreateInstance(extensionType);
            ApplyExtensionArguments(extension, name, arguments, node, context);

            var extensionContext = new MarkupExtensionContext(instance, member, configuration, context.Names, node, context.SourcePath);
            return extension.ProvideValue(extensionContext);
        }

        /// <summary>
        /// Sets an extension's properties from its parsed <c>Key=Value</c> and positional arguments.
        /// </summary>
        private void ApplyExtensionArguments(IMarkupExtension extension, string extensionName, List<(string? Key, string Value)> arguments, IXmlLineInfo? node, MarkupLoadContext context)
        {
            Type type = extension.GetType();
            string? defaultProperty = MarkupExtensionDefaultPropertyAttribute.GetDefaultPropertyName(type);
            bool usedPositional = false;

            foreach ((string? key, string value) in arguments)
            {
                string propertyName = key ?? defaultProperty
                    ?? throw MarkupException.At($"'{{{extensionName}}}' has no default property, so its argument must be written as 'Name=Value'.", node, context.SourcePath);

                if (key == null)
                {
                    if (usedPositional)
                        throw MarkupException.At($"'{{{extensionName}}}' was given more than one positional argument.", node, context.SourcePath);
                    usedPositional = true;
                }

                PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || !property.CanWrite)
                {
                    throw MarkupException.At(
                        $"'{{{extensionName}}}' has no settable property '{propertyName}'.{NameSuggestion.Clause(propertyName, type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.CanWrite).Select(x => x.Name))}",
                        node,
                        context.SourcePath);
                }

                property.SetValue(extension, ConvertValue(value, property.PropertyType, node, context));
            }
        }

        /// <summary>
        /// The mutable state threaded through one load: where errors come from, and what the document has declared
        /// so far.
        /// </summary>
        /// <param name="sourcePath">The document's path, or <see langword="null"/> when it isn't known.</param>
        /// <param name="names">The name scope the document's <c>x:Name</c>s are registered into.</param>
        private sealed class MarkupLoadContext(string? sourcePath, MarkupNameScope names)
        {
            /// <summary>
            /// Gets the document's path, used to make error messages locatable.
            /// </summary>
            public string? SourcePath { get; } = sourcePath;

            /// <summary>
            /// Gets the name scope the document's <c>x:Name</c>s are registered into.
            /// </summary>
            public MarkupNameScope Names { get; } = names;

            /// <summary>
            /// Gets the <c>x:DataType</c> declared for each element that declares one.
            /// </summary>
            /// <remarks>
            /// Collected but unused: bindings, which are what a data type would validate, arrive with markup
            /// extensions. Resolving the names now means a misspelling is an error at load rather than a surprise
            /// later.
            /// </remarks>
            public Dictionary<XElement, Type> DataTypes { get; } = [];
        }
    }
}
