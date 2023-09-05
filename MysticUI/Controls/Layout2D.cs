using System.Data;
using System.Text.RegularExpressions;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a layout expression for <see cref="Control"/>.
    /// </summary>
    public struct Layout2D
    {
        private static readonly Regex controlDetector = new(@"\[.+\]", RegexOptions.Compiled);
        private string? yExpression;
        private string? xExpression;
        private string? widthExpression;
        private string? heightExpression;

        /// <summary>
        /// Maximal depth of calculation during expression computing.
        /// </summary>
        public static int CalculationDepth { get; set; } = 10;

        /// <summary>
        /// Default expression, shouldn't be computed.
        /// </summary>
        public static Layout2D Default => new();

        /// <summary>
        /// An expression for <see cref="Control.Y"/> property.
        /// </summary>
        public string? YExpression
        {
            readonly get => yExpression;
            set
            {
                yExpression = value;
                if (value != null) ContainsExpression = true;
                Calculated = false;
            }
        }

        /// <summary>
        /// An expression for <see cref="Control.X"/> property.
        /// </summary>
        public string? XExpression
        {
            readonly get => xExpression;
            set
            {
                xExpression = value;
                if (value != null) ContainsExpression = true;
                Calculated = false;
            }
        }

        /// <summary>
        /// An expression for <see cref="Control.Width"/> property.
        /// </summary>
        public string? WidthExpression
        {
            readonly get => widthExpression;
            set
            {
                widthExpression = value;
                if (value != null) ContainsExpression = true;
                Calculated = false;
            }
        }

        /// <summary>
        /// An expression for <see cref="Control.Height"/> property.
        /// </summary>
        public string? HeightExpression
        {
            readonly get => heightExpression;
            set
            {
                heightExpression = value;
                if (value != null) ContainsExpression = true;
                Calculated = false;
            }
        }

        /// <summary>
        /// Full expression string for all direct layout properties.
        /// </summary>
        public string? Expression
        {
            readonly get => ToString();
            set => ParseInternal(value);
        }

        /// <summary>
        /// Determines whether layout contains any computable expression.
        /// </summary>
        public bool ContainsExpression { readonly get; private set; }

        /// <summary>
        /// Determines whether expression value is calculated after initialization.
        /// </summary>
        public bool Calculated { readonly get; set; }

        /// <summary>
        /// Creates new <see cref="Layout2D"/> expression.
        /// </summary>
        /// <param name="expression">Expression string to apply.</param>
        public Layout2D(string? expression = null) : this()
        {
            ParseInternal(expression);
        }

        /// <summary>
        /// Computes the target values using given expression.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="controls"></param>
        /// <param name="level"></param>
        public readonly void Compute(Control target, IEnumerable<Control> controls, int level = 0)
        {
            if (level > CalculationDepth)
                return;

            if (target.Parent?.LayoutExpression.ContainsExpression == true)
            {
                target.Parent.LayoutExpression.Compute(target.Parent, controls, ++level);
            }

            if (XExpression != null)
                target.X = ParseAndCompute(XExpression, target, controls);
            if (YExpression != null)
                target.Y = ParseAndCompute(YExpression, target, controls);
            if (WidthExpression != null)
                target.Width = ParseAndCompute(WidthExpression, target, controls);
            if (HeightExpression != null)
                target.Height = ParseAndCompute(HeightExpression, target, controls);
            target.LayoutExpression.Calculated = true;
        }

        private static int ParseAndCompute(string expression, Control target, IEnumerable<Control> controls)
        {
            if (string.IsNullOrWhiteSpace(expression)) return 0;
            // Replace self values.
            expression = expression.Replace("this.X", target.X.ToString(), StringComparison.InvariantCultureIgnoreCase)
                .Replace("this.Y", target.Y.ToString(), StringComparison.InvariantCultureIgnoreCase)
                .Replace("this.Width", target.Width.ToString(), StringComparison.InvariantCultureIgnoreCase)
                .Replace("this.Height", target.Height.ToString(), StringComparison.InvariantCultureIgnoreCase);
            // Replace parent values.
            if (target.Parent != null)
            {
                Control parent = target.Parent;
                expression = expression.Replace("parent", "&")
                    .Replace("&.X", parent.X.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("&.Y", parent.Y.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("&.Width", parent.Width.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("&.Height", parent.Height.ToString(), StringComparison.InvariantCultureIgnoreCase);
            }
            // Replace viewport values.
            if (target.Desktop != null)
            {
                var desktop = target.Desktop;
                expression = expression.Replace("desktop", "@").Replace("window", "@")
                    .Replace("@.X", desktop.LayoutBounds.X.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("@.Y", desktop.LayoutBounds.Y.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("@.Width", desktop.LayoutBounds.Width.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    .Replace("@.Height", desktop.LayoutBounds.Height.ToString(), StringComparison.InvariantCultureIgnoreCase);
            }
            // Replace named target values.
            var matchResults = controlDetector.Matches(expression);
            if (matchResults.Any())
            {
                foreach (Match match in matchResults.Cast<Match>())
                {
                    string matchString = match.Value;
                    string toReplace = "0";
                    string name = match.Value;
                    var control = controls.FirstOrDefault(c => c.Name == name);
                    if (control != null)
                    {
                        string propertyName = "";
                        for (int i = match.Index + 2; i < expression.Length; i++)
                        {
                            propertyName += expression[i];
                        }
                        switch (propertyName.ToLowerInvariant())
                        {
                            case "x":
                                toReplace = control.X.ToString(); break;
                            case "y":
                                toReplace = control.Y.ToString(); break;
                            case "width":
                                toReplace = control.Width.ToString(); break;
                            case "height":
                                toReplace = control.Height.ToString(); break;
                        }
                    }
                    expression = expression.Replace(matchString, toReplace);
                }
            }
            return (int)new DataTable().Compute(expression, null);
        }

        private void ParseInternal(string? expression)
        {
            if (expression != null)
            {
                foreach (var part in expression.Trim('{', '}').Replace(" ", "").Split(';'))
                {
                    string? expr = part.Split('=')[1];
                    if (expr.Equals("null", StringComparison.InvariantCultureIgnoreCase)) expr = null;
                    if (part.Contains("this.x", StringComparison.InvariantCultureIgnoreCase))
                    {
                        XExpression = expr;
                    }
                    else if (part.Contains("this.y", StringComparison.InvariantCultureIgnoreCase))
                    {
                        YExpression = expr;
                    }
                    else if (part.Contains("this.height", StringComparison.InvariantCultureIgnoreCase))
                    {
                        HeightExpression = expr;
                    }
                    else if (part.Contains("this.width", StringComparison.InvariantCultureIgnoreCase))
                    {
                        WidthExpression = expr;
                    }
                };
            }
        }

        /// <inheritdoc/>
        public override readonly string? ToString()
        {
            return ContainsExpression ? $"{{this.x={XExpression};this.y={YExpression};this.width={WidthExpression};this.height={HeightExpression}}}" : null;
        }
    }
}