using MysticUI.Brushes;
using MysticUI.Controls;
using MysticUI.Extensions;
using MysticUI.Extensions.Content;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace MysticUI
{
    /// <summary>
    /// A tool for serializing layout components into XML files and read from them.
    /// </summary>
    public class LayoutSerializer
    {
        internal const string KeyAttribute = "Key";

        private static readonly Lazy<LayoutSerializer> instance = new();
        private readonly List<IStringSerializer> availableSerializers = new();
        private readonly ExpressionParser expressionParser = new();
        private readonly HashSet<string> ignoredNodes = new();

        /// <summary>
        /// Default serializer instance.
        /// </summary>
        public static LayoutSerializer Default => instance.Value;

        /// <summary>
        /// Presented available serializers.
        /// </summary>
        public IEnumerable<IStringSerializer> AvailableSerializers => availableSerializers;

        /// <summary>
        /// Ignored XML tags names.
        /// </summary>
        public IEnumerable<string> IgnoredNodes => ignoredNodes;

        /// <summary>
        /// Instance of expression parser used in this serializer.
        /// </summary>
        public ExpressionParser ExpressionParser => expressionParser;

        /// <summary>
        /// A function that creates a object of given type.
        /// </summary>
        public Func<Type, object?> ActivationFactory { get; set; } = x =>
        {
            var result = Activator.CreateInstance(x);
            if (result is Control c)
            {
                if (Stylesheet.Default.TryGetValue($"{x.Name}Style", out var style))
                {
                    c.ApplyStyle(style);
                }
            }
            return result;
        };

        /// <summary>
        /// Creates a new instance of the <see cref="LayoutSerializer"/> class.
        /// </summary>
        public LayoutSerializer()
        {
            ResetSerializers();
        }

        /// <summary>
        /// Loads layout element as given type instance.
        /// </summary>
        /// <typeparam name="T">Type for correct loading.</typeparam>
        /// <param name="layoutDocument">XML node with layout for loaded element.</param>
        /// <param name="dataContext">Optional data context to handle events and expressions in created object.</param>
        /// <returns>Instance of given type.</returns>
        /// <exception cref="TypeLoadException">Thrown when created object type isn't assignable to target type.</exception>
        public virtual T LoadLayout<T>(XElement layoutDocument, object? dataContext = null) where T : class
        {
            var result = LoadLayout(layoutDocument, dataContext, typeof(T));
            return result as T ?? throw new TypeLoadException($"Type {result?.GetType().FullName} can't be assigned to target type {typeof(T).FullName}");
        }

        /// <summary>
        /// Loads layout element from XML node.
        /// </summary>
        /// <param name="layoutDocument">XML node with layout for loaded element.</param>
        /// <param name="dataContext">Optional data context to handle events and expressions in created object.</param>
        /// <param name="type">Optional preset type to create object. If type is not <see langword="null"/>,
        /// parser will ignore name of the node and create object of target type.</param>
        /// <returns>Instance of an object from the node or <see langword="null"/> if node type couldn't be resolved.</returns>
        public virtual object? LoadLayout(XElement layoutDocument, object? dataContext = null, Type? type = null)
        {
            // Interfaces are needed only for outer creation algorithms.
            if (type?.IsInterface == true) type = null;
            // Resolve original node type
            Type? nodeType = CoreUtils.SearchType(layoutDocument.Name.LocalName);
            type ??= nodeType;
            if (nodeType?.IsAssignableTo(type) == true) type = nodeType;
            // Try to detect instance type if it's supported by the target.
            var typeProperty = type?.GetProperties()?.FirstOrDefault(x => x.GetCustomAttribute<InstanceTypeAttribute>() != null);
            if (typeProperty != null)
            {
                string? newTypeName = layoutDocument.Attributes().FirstOrDefault(x => x.Name.LocalName == typeProperty.Name)?.Value;
                if (newTypeName != null)
                    type = CoreUtils.SearchType(newTypeName) ?? type;
            }
            if (type == null)
                return null;
            // Create an instance of a target type and apply the layout to it.
            var createdObject = ActivationFactory(type);
            if (createdObject != null)
                ApplyLayout(createdObject, layoutDocument, dataContext);
            return createdObject;
        }

        /// <summary>
        /// Applies the layout to the target object.
        /// </summary>
        /// <param name="target">Target to apply the layout.</param>
        /// <param name="layout">Layout node to apply.</param>
        /// <param name="dataContext">Optional data context to handle events and expressions in created object.</param>
        /// <exception cref="ArgumentException">Thrown when some of properties values are incorrect.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when some keys or properties names are not exist.</exception>
        public virtual void ApplyLayout(object target, XElement layout, object? dataContext = null)
        {
            Type targetType = target.GetType();
            Type? contextType = dataContext?.GetType();
            var uiElement = target as UIElement;
            if (uiElement != null && dataContext != null)
            {
                uiElement.DataContext = dataContext;
            }
            ResolveProperties(targetType, out var parsableProperties, out var complexProperties, out var namesToReplace);
            foreach (var attribute in layout.Attributes())
            {
                string name = attribute.Name.LocalName;
                // Replace obsolete property name by new.
                if (namesToReplace.Contains(name))
                    name = (string)namesToReplace[name]!;

                string propertyString = attribute.Value;
                // Stage 1: Try to detect an expression inside the property.
                if (expressionParser.ResolveExpression(propertyString, out var expressionResolver))
                {
                    propertyString = expressionResolver.HandleExpression(target, ExpressionParser.GetExpressionContent(propertyString), targetType.GetProperty(name), dataContext)!;
                    if (propertyString == null) continue;
                }

                // Cache some fields here
                EventInfo? targetEvent = null;
                // Stage 2: Try to load property as parsable from string.
                PropertyInfo? parsable = parsableProperties.FirstOrDefault(x => x.Name == name), complex = complexProperties.FirstOrDefault(x => x.Name == name);
                if (parsable != null)
                {
                    Type? propertyType = parsable.PropertyType;
                    object? value = ParseProperty(propertyString, propertyType);
                    parsable.SetValue(target, value);
                }
                // 2.1. Some of complex properties also can have an attribute form. For example, a ContentControl.Content is object, which actually can keep strings.
                else if (complex != null)
                {
                    // Also there are brushes which can be interpreted as solid colors which are also available in short attribute form.
                    if (complex.PropertyType == typeof(IBrush) || complex.PropertyType == typeof(SolidColorBrush))
                    {
                        SolidColorBrush brush = ColorTools.ParseColor(propertyString);
                        complex.SetValue(target, brush);
                    }
                    else
                    {
                        complex.SetValue(target, propertyString);
                    }
                }
                // 2.2: events can be parsed if either control or data context contains handler for them.
                else if ((targetEvent = targetType.GetEvent(name)) != null)
                {
                    var method = contextType?.GetMethod(propertyString, BindingFlags.Public | BindingFlags.Instance) ??
                        targetType.GetMethod(propertyString, BindingFlags.Public | BindingFlags.Instance);
                    object methodHandle = dataContext ?? targetType;
                    if (method != null)
                    {
                        var eventHandler = method.CreateDelegate(targetEvent.EventHandlerType!, methodHandle);
                        targetEvent.AddEventHandler(target, eventHandler);
                    }
                    else
                    {
                        throw new ArgumentException("Can't find a method to bind to an event.");
                    }
                }
                // 2.3: All missed attributes are present as custom attributes.
                else
                {
                    uiElement?.Attributes.Add(name, propertyString);
                }
            }

            // If value is data template, all following content should be as its template content and shouldn't be processed.
            if (target is DataTemplate template && layout.Elements().Count() == 1)
            {
                template.TemplateContent = layout.Elements().First();
                return;
            }

            // Stage 3: try to load the property as it is complex property. If there are more than one content properties, it throws an exception.
            var contentProperty = complexProperties.SingleOrDefault(x => x.GetCustomAttribute<ContentAttribute>() != null);
            foreach (var child in layout.Elements())
            {
                string childName = child.Name.LocalName;
                if (ignoredNodes.Contains(childName)) continue;
                bool isProperty = childName.Contains('.');
                if (isProperty)
                    childName = childName.Split('.')[1];
                if (namesToReplace.Contains(childName))
                    childName = (string)namesToReplace[childName]!;

                var property = complexProperties.FirstOrDefault(x => x.Name == childName);
                if (property != null)
                {
                    var value = property.GetValue(target);
                    if (value is IList list)
                    {
                        foreach (var element in child.Elements())
                        {
                            var item = ActivationFactory(property.PropertyType.GenericTypeArguments[0]);
                            ApplyLayout(item!, element, dataContext);
                            list.Add(item);
                        }
                    }
                    else if (value is IDictionary dictionary)
                    {
                        foreach (var element in child.Elements())
                        {
                            var item = ActivationFactory(property.PropertyType.GenericTypeArguments[1]);
                            ApplyLayout(item!, element, dataContext);

                            var key = string.Empty;
                            var keyAttribute = child.Attributes().FirstOrDefault(x => x.Name.LocalName == KeyAttribute);
                            if (keyAttribute != null)
                            {
                                key = keyAttribute.Value;
                            }
                            else
                            {
                                throw new KeyNotFoundException($"Dictionary element should contain a key.");
                            }

                            if (dictionary.Contains(key))
                                dictionary[key] = item;
                            else
                                dictionary.Add(key, item);
                        }
                    }
                    else
                    {
                        var newValue = ActivationFactory(property.PropertyType);
                        ApplyLayout(newValue!, child.Elements().First(), dataContext);
                        property.SetValue(target, newValue);
                    }
                }
                else
                {
                    if (isProperty)
                        throw new ArgumentException($"Class {targetType.FullName} doesn't have a property {childName}");
                    var newControl = LoadLayout(child, dataContext);
                    if (newControl != null)
                    {
                        if (contentProperty == null)
                            throw new ArgumentException($"Class {targetType.FullName} requires a property with ContentAttribute to store other controls inside.");
                        var containerValue = contentProperty.GetValue(target);
                        if (containerValue is IList list)
                        {
                            list.Add(newControl);
                        }
                        else
                        {
                            contentProperty.SetValue(target, newControl);
                        }
                    }
                    else
                        throw new KeyNotFoundException($"Couldn't resolve tag {childName}");
                }
            }

            // Stage 4 (optional): if the control has a content property with parsable type, try to load value:
            if (contentProperty == null)
            {
                contentProperty = parsableProperties.FirstOrDefault(x => x.GetCustomAttribute<ContentAttribute>() != null);
                if (contentProperty != null)
                {
                    string value = layout.Value.Trim(' ', '\t', '\n', '\r');
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        if (ExpressionParser.ResolveExpression(value, out var resolver))
                        {
                            value = resolver.HandleExpression(target, value, contentProperty, dataContext)!;
                            if (value == null) return;
                        }
                        contentProperty.SetValue(target, ParseProperty(value, contentProperty.PropertyType));
                    }
                }
            }
        }

        /// <summary>
        /// Parses the property value from string.
        /// </summary>
        /// <remarks>
        /// Note that the property type should be <see cref="PropertyState.Parsable"/> to be parsed.
        /// </remarks>
        /// <param name="propertyString">String with packed property value.</param>
        /// <param name="propertyType">Type of the property to parse.</param>
        /// <returns>Value of the property.</returns>
        public object? ParseProperty(string propertyString, Type propertyType)
        {
            if (propertyType == typeof(string)) return propertyString;
            if (propertyString.Equals("null", StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrEmpty(propertyString))
            {
                return null;
            }
            object? value;
            var serializer = ResolveSerializer(propertyType);
            if (serializer != null)
            {
                value = serializer.Parse(propertyType, propertyString);
            }
            else if (propertyType.IsEnum)
            {
                value = Enum.Parse(propertyType, propertyString);
            }
            else
            {
                var customConverter = TypeDescriptor.GetConverter(propertyType);
                if (customConverter != null)
                {
                    value = customConverter.ConvertFromString(null, CultureInfo.InvariantCulture, propertyString);
                }
                else
                {
                    if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && propertyType.GenericTypeArguments[0].IsPrimitive)
                    {
                        propertyType = propertyType.GenericTypeArguments[0];
                    }
                    value = Convert.ChangeType(propertyString, propertyType, CultureInfo.InvariantCulture);
                }
            }

            return value;
        }

        /// <summary>
        /// Saves an object into XML layout element.
        /// </summary>
        /// <param name="target">Object to save.</param>
        /// <param name="skipComplex">Indicates to not to save complex properties.</param>
        /// <param name="customTagName">Optional custom name of the tag to save. Note that tag names are often used to search type of loaded object.</param>
        /// <returns></returns>
        public XElement Save(object target, bool skipComplex = false, string? customTagName = null)
        {
            Type type = target.GetType();
            var uiElement = target as UIElement;
            ResolveProperties(type, out var parsableProperties, out var complexProperties, out var obsoleteTable);
            var layout = new XElement(customTagName ?? type.Name);
            foreach (var property in parsableProperties)
            {
                object? value = property.GetValue(target);
                if (property.IsDefaultValue(value)) continue;
                if (property.GetCustomAttribute<ObsoleteAttribute>() != null) continue;
                if (value != null)
                {
                    string? result = null;
                    Type propertyType = property.PropertyType;
                    var serializer = ResolveSerializer(propertyType);
                    TypeConverter converter = TypeDescriptor.GetConverter(value);
                    if (serializer != null)
                    {
                        result = serializer.Serialize(value);
                    }
                    else if (converter != null && converter.CanConvertTo(typeof(string)))
                    {
                        result = converter.ConvertToInvariantString(value);
                    }
                    else
                    {
                        result = Convert.ToString(value, CultureInfo.InvariantCulture);
                    }
                    if (result != null)
                    {
                        layout.Add(new XAttribute(property.Name, result));
                    }
                }
            }

            if (!skipComplex)
            {
                var contentProperty = complexProperties.SingleOrDefault(x => x.GetCustomAttribute<ContentAttribute>() != null);
                foreach (var property in complexProperties)
                {
                    object? value = property.GetValue(target);
                    if (property.IsDefaultValue(value)) continue;
                    if (property.GetCustomAttribute<ObsoleteAttribute>() != null) continue;
                    if (value != null)
                    {
                        string propertyName = $"{type.Name}.{property.Name}";
                        bool isContent = property == contentProperty;
                        if (value is IList list)
                        {
                            var collectionRoot = layout;

                            if (property.GetCustomAttribute<ContentAttribute>() == null && list.Count > 0)
                            {
                                collectionRoot = new XElement(propertyName);
                                layout.Add(collectionRoot);
                            }

                            foreach (var item in list)
                            {
                                collectionRoot.Add(Save(item));
                            }
                        }
                        else if (value is IDictionary dictionary)
                        {
                            var collectionRoot = layout;

                            if (property.GetCustomAttribute<ContentAttribute>() == null && dictionary.Count > 0)
                            {
                                collectionRoot = new XElement(propertyName);
                                layout.Add(collectionRoot);
                            }

                            foreach (var key in dictionary.Keys)
                            {
                                var item = dictionary[key];
                                if (item == null) continue;
                                var contentElement = Save(item);
                                contentElement.Add(new XAttribute(KeyAttribute, key));
                                collectionRoot.Add(contentElement);
                            }
                        }
                        else
                        {
                            layout.Add(isContent ? Save(value) : Save(value, false, propertyName));
                        }
                    }
                }
            }
            return layout;
        }

        /// <summary>
        /// Adds a type serializer for current <see cref="LayoutSerializer"/> instance.
        /// </summary>
        /// <param name="serializer">Serializer for given type.</param>
        public void AddSerializer(IStringSerializer serializer) => availableSerializers.Add(serializer);

        /// <summary>
        /// Adds a ignored tag to the blacklist.
        /// </summary>
        /// <param name="tag">Tag to ignore.</param>
        public void AddIgnoreTag(string tag) => ignoredNodes.Add(tag);

        /// <summary>
        /// Resets all type serializer to their defaults.
        /// </summary>
        public void ResetSerializers()
        {
            availableSerializers.Clear();
            availableSerializers.Add(new FontLoader());
        }

        /// <summary>
        /// Clears ignored types blacklist.
        /// </summary>
        public void ClearIgnoreTags()
        {
            ignoredNodes.Clear();
        }

        /// <summary>
        /// Groups properties of given type.
        /// </summary>
        /// <param name="type">Type to scan for serializable properties.</param>
        /// <param name="parsable">List of properties that can be parsed from string.</param>
        /// <param name="complex">List of properties that can't be parsed from string.</param>
        /// <param name="obsoleteNamesTable">Obsolete properties names to rename them while parsing.</param>
        protected void ResolveProperties(Type type, out List<PropertyInfo> parsable, out List<PropertyInfo> complex, out ListDictionary obsoleteNamesTable)
        {
            parsable = new();
            complex = new();
            obsoleteNamesTable = new();

            foreach (var property in type.GetProperties())
            {
                switch (GetPropertyState(property))
                {
                    case PropertyState.Parsable:
                        parsable.Add(property); break;
                    case PropertyState.Complex:
                        complex.Add(property); break;
                }
                var formerlySerialize = property.GetCustomAttribute<FormerlySerializeAsAttribute>();
                if (formerlySerialize != null)
                    obsoleteNamesTable.Add(formerlySerialize.OldName, property.Name);
            }
        }

        /// <summary>
        /// Gets serialization state for the property.
        /// </summary>
        /// <param name="property">Property to check.</param>
        /// <param name="readXmlIgnorable">Determines whether to ignore properties with the <see cref="XmlIgnoreAttribute"/>.</param>
        /// <returns>Serialization state for the property.</returns>
        protected internal PropertyState GetPropertyState(PropertyInfo property, bool readXmlIgnorable = true)
        {
            // Don't serialize private and static properties
            if (property.GetMethod == null || property.GetMethod.IsStatic || !property.GetMethod.IsPublic || property.SetMethod?.IsPublic == false || (readXmlIgnorable && property.GetCustomAttribute<XmlIgnoreAttribute>() != null))
                return PropertyState.DontSerialize;
            // Parse properties that can be parsed.
            if (IsParsable(property.PropertyType))
                return PropertyState.Parsable;
            // All other properties should be deserialized with their complex attributes
            return PropertyState.Complex;
        }

        protected internal bool IsParsable(Type type)
        {
            return type.IsPrimitive ||
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && type.GenericTypeArguments[0].IsPrimitive) ||
                type.IsEnum ||
                type == typeof(string) ||
                availableSerializers.Any(x => x.CanParse(type)) ||
                TypeDescriptor.GetConverter(type)?.CanConvertFrom(typeof(string)) == true;
        }

        private IStringSerializer? ResolveSerializer(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && type.GenericTypeArguments[0].IsPrimitive)
                type = type.GenericTypeArguments[0];
            return availableSerializers.FirstOrDefault(x => x.CanParse(type));
        }

        /// <summary>
        /// Represents serialization state of the property.
        /// </summary>
        public enum PropertyState
        {
            /// <summary>
            /// Indicates that the property shouldn't be serialized at all.
            /// </summary>
            DontSerialize,

            /// <summary>
            /// Indicates that the property can be serialized from string.
            /// </summary>
            Parsable,

            /// <summary>
            /// Indicates that the property should be serialized using its properties.
            /// </summary>
            Complex
        }
    }
}