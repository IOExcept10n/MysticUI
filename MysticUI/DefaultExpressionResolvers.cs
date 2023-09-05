using CommunityToolkit.Diagnostics;
using MysticUI.Controls;
using System.Globalization;
using System.Reflection;

namespace MysticUI
{
    internal class BindingResolver : IExpressionResolver
    {
        public string? HandleExpression(object target, string expressionContent, PropertyInfo? targetProperty, object? dataContext = null)
        {
            Guard.IsNotNull(target);
            Guard.IsNotNull(expressionContent);
            Guard.IsNotNull(targetProperty);
            if (expressionContent.StartsWith("Binding "))
            {
                expressionContent = expressionContent[8..].Replace(" ", "");
                string[] bindingParts = expressionContent.Replace(',', ';').Split(';');
                // Don't handle properties that are not bindable.
                if (targetProperty.GetCustomAttribute<NonBindableAttribute>()?.AsTarget == true) return expressionContent;
                var binding = Binding.CreateBinding(target, targetProperty);
                if (bindingParts.Length == 1)
                {
                    string path = bindingParts[0].Replace("Path=", "");
                    binding.Path = path;
                }
                else
                {
                    foreach (string part in bindingParts)
                    {
                        var temp = part.Split('=');
                        string propertyName = temp[0];
                        string propertyValue = temp[1];
                        switch (propertyName.ToLowerInvariant())
                        {
                            case "path":
                                binding.Path = propertyValue; break;
                            case "mode":
                                binding.Mode = Enum.Parse<BindingMode>(propertyValue); break;
                            case "trigger":
                                binding.UpdateSourceTrigger = Enum.Parse<UpdateSourceTrigger>(propertyValue); break;
                            case "source":
                                binding.XPath = propertyValue; break;
                        }
                    }
                }
                if (dataContext != null)
                {
                    binding.Source = dataContext;
                }
                binding.UpdateTarget();
                return null;
            }
            else throw new ArgumentException($"Incorrect binding expression: {expressionContent}");
        }

        public bool Match(string expression)
        {
            return expression.StartsWith("{Binding ", StringComparison.InvariantCultureIgnoreCase);
        }
    }

    internal class LocalizationResolver : IExpressionResolver
    {
        public string? HandleExpression(object target, string expressionContent, PropertyInfo? targetProperty, object? dataContext = null)
        {
            return EnvironmentSettingsProvider.EnvironmentSettings.LocalizationProvider?.Localize(expressionContent, CultureInfo.CurrentUICulture) ?? expressionContent;
        }

        public bool Match(string expression)
        {
            return expression.StartsWith('@');
        }
    }

    internal class LayoutExpressionResolver : IExpressionResolver
    {
        public string? HandleExpression(object target, string expressionContent, PropertyInfo? targetProperty, object? dataContext = null)
        {
            if (target is Control control)
            {
                switch (targetProperty?.Name)
                {
                    case nameof(Control.X):
                        control.LayoutExpression.XExpression = expressionContent;
                        break;

                    case nameof(Control.Y):
                        control.LayoutExpression.YExpression = expressionContent;
                        break;

                    case nameof(Control.Width):
                        control.LayoutExpression.WidthExpression = expressionContent;
                        break;

                    case nameof(Control.Height):
                        control.LayoutExpression.HeightExpression = expressionContent;
                        break;
                }
            }
            return null;
        }

        public bool Match(string expression)
        {
            return expression.StartsWith('$');
        }
    }

    internal class ResourceResolver : IExpressionResolver
    {
        public string? HandleExpression(object target, string expressionContent, PropertyInfo? targetProperty, object? dataContext = null)
        {
            Guard.IsNotNull(target);
            Guard.IsNotNull(expressionContent);
            Guard.IsNotNull(targetProperty);
            var assets = EnvironmentSettingsProvider.EnvironmentSettings.DefaultAssets;
            if (expressionContent.StartsWith("Resource "))
            {
                expressionContent = expressionContent[9..].Replace(" ", "");
                var result = assets.GetStaticResource(expressionContent, targetProperty.PropertyType, false);
                if (result != null)
                {
                    targetProperty.SetValue(target, result);
                    return null;
                }
            }
            return expressionContent;
        }

        public bool Match(string expression)
        {
            return expression.StartsWith("{Resource ");
        }
    }
}