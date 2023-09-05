using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MysticUI
{
    /// <summary>
    /// Represents an object that can resolve a layout integrated expression, parse it and transform its value according to expression content.
    /// </summary>
    public interface IExpressionResolver
    {
        /// <summary>
        /// Tries to detect the expression of this resolver in the string.
        /// </summary>
        /// <param name="expression">A string to check for an expression.</param>
        /// <returns><see langword="true"/> if the string passes an expression, <see langword="false"/> otherwise.</returns>
        public bool Match(string expression);

        /// <summary>
        /// Handles the expression and performs actions to the passes object.
        /// </summary>
        /// <param name="target">A target object to use an expression to.</param>
        /// <param name="expressionContent">Content of the expression string (without curly brackets and prefixes)</param>
        /// <param name="targetProperty">A property in which will be passed a value from the transformed string.</param>
        /// <param name="dataContext">Optional preset of data context for an object.</param>
        /// <returns>Transformed string to pass into property or <see langword="null"/> if the property was handled successfully and doesn't have to be set manually.</returns>
        public string? HandleExpression(object target, string expressionContent, PropertyInfo? targetProperty, object? dataContext = null);
    }

    /// <summary>
    /// Handles layout integration expressions to handle functionality for content.
    /// </summary>
    public class ExpressionParser
    {
        private static readonly Regex expressionRegex = new(@"^.*\{.+\}$", RegexOptions.Compiled);
        private readonly List<IExpressionResolver> resolvers;

        /// <summary>
        /// Creates new instance of <see cref="ExpressionParser"/> class.
        /// </summary>
        public ExpressionParser()
        {
            resolvers = new()
            {
                new BindingResolver(),
                new LocalizationResolver(),
                new LayoutExpressionResolver(),
                new ResourceResolver(),
            };
        }

        /// <summary>
        /// Tries to find an expression in the following string and finds the resolver to handle the expression.
        /// </summary>
        /// <param name="expression">An expression to handle.</param>
        /// <param name="resolver">A resolver that can handle the expression.</param>
        /// <returns><see langword="true"/> if the resolver was found, <see langword="false"/> otherwise.</returns>
        public bool ResolveExpression(string expression, [NotNullWhen(true)] out IExpressionResolver? resolver)
        {
            resolver = null;
            if (expressionRegex.IsMatch(expression))
            {
                resolver = resolvers.FirstOrDefault(x => x.Match(expression));
            }
            return resolver != null;
        }

        /// <summary>
        /// Parses the expression and tries to convert it using all available converters.
        /// </summary>
        /// <param name="target">Target object to apply expression to.</param>
        /// <param name="expression">Expression to parse.</param>
        /// <param name="targetProperty">Property that contains string with expression.</param>
        /// <param name="dataContext">Optional data context to apply.</param>
        /// <returns>Parsed and converted expression if it can be parsed.</returns>
        public string? Parse(object target, string expression, PropertyInfo targetProperty, object? dataContext = null)
        {
            IExpressionResolver? resolver = null;
            var match = expressionRegex.Match(expression);
            if (match.Success)
            {
                string content = expression[(expression.IndexOf('{') + 1)..expression.LastIndexOf('}')];
                resolver = resolvers.FirstOrDefault(x => x.Match(expression));
                if (resolver != null)
                    return resolver.HandleExpression(target, content, targetProperty, dataContext);
                return content;
            }
            return expression;
        }

        /// <summary>
        /// Gets the content of an expression if it's available.
        /// </summary>
        /// <param name="expression">Expression to parse.</param>
        /// <returns>Content of the expression or expression itself if it can't be parsed.</returns>
        public static string GetExpressionContent(string expression)
        {
            var match = expressionRegex.Match(expression);
            if (match.Success) return expression[(expression.IndexOf('{') + 1)..(expression.LastIndexOf('}'))];
            return expression;
        }

        /// <summary>
        /// Adds an expression resolver to this expression parser.
        /// </summary>
        /// <param name="resolver">Expression resolver instance.</param>
        public void AddResolver(IExpressionResolver resolver) => resolvers.Add(resolver);
    }
}