namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the template to apply control styles.
    /// </summary>
    public class ControlTemplate : DataTemplate
    {
        /// <summary>
        /// Gets the base style for the template. It's applied before the current style.
        /// </summary>
        public ControlTemplate? BaseStyle { get; set; }

        /// <summary>
        /// Gets the name of the base style, needed for the lately initialization.
        /// </summary>
        public string? BaseStyleName { get; set; }

        /// <summary>
        /// Applies the style to the control with optional context.
        /// </summary>
        /// <param name="control">The control to apply the style for.</param>
        /// <param name="dataContext">The data context for the control.</param>
        public void Apply(UIElement control, object? dataContext = null)
        {
            BaseStyle?.Apply(control, dataContext);
            LayoutSerializer.Default.ApplyLayout(control, TemplateContent, dataContext);
        }

        internal void Setup(Stylesheet templates)
        {
            if (!string.IsNullOrEmpty(BaseStyleName) && templates.TryGetValue(BaseStyleName, out var style))
            {
                BaseStyle = style;
            }
            else if (templates.TryGetValue("ControlStyle", out var defaultStyle) && this != defaultStyle)
            {
                BaseStyle = defaultStyle;
            }
        }
    }
}