using FontStashSharp;
using FontStashSharp.RichText;
using MysticUI.Extensions;
using Stride.Core.Mathematics;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a text-only control that supports text display and formatting.
    /// </summary>
    public class TextBlock : Control
    {
        private static readonly RichTextLayout errorText = new()
        {
            SupportsCommands = false
        };

        private readonly RichTextLayout text = new()
        {
            CommandPrefix = '\\',
            SupportsCommands = true,
        };

        private RichTextLayout autoEllipsisText = new();
        private bool textWrapping;
        private TextEllipsis textEllipsis;
        private string autoEllipsisString = "...";
        private TextHorizontalAlignment textAlignment;
        private int verticalSpacing;

        /// <summary>
        /// Original text string presented in the control.
        /// </summary>
        [Category("Appearance")]
        [Content]
        public string? Text
        {
            get => text.Text;
            set
            {
                text.Text = value;
                TextChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(Text));
            }
        }

        /// <summary>
        /// Determines whether to use text wrapping.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(false)]
        public bool TextWrapping
        {
            get => textWrapping;
            set
            {
                if (value == textWrapping)
                    return;
                textWrapping = value;
                NotifyPropertyChanged(nameof(TextWrapping));
            }
        }

        /// <summary>
        /// Gets or sets text ellipsis method.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(TextEllipsis.None)]
        public TextEllipsis TextEllipsis
        {
            get => textEllipsis;
            set
            {
                if (value == textEllipsis)
                    return;
                textEllipsis = value;
                NotifyPropertyChanged(nameof(TextEllipsis));
            }
        }

        /// <summary>
        /// Gets or sets ellipsis string.
        /// </summary>
        [Category("Miscellaneous")]
        [DefaultValue("...")]
        public string AutoEllipsisString
        {
            get => autoEllipsisString;
            set
            {
                if (value == autoEllipsisString)
                    return;
                autoEllipsisString = value;
                NotifyPropertyChanged(nameof(AutoEllipsisString));
            }
        }

        /// <summary>
        /// Gets or sets text alignment inside the control.
        /// </summary>
        [Category("Appearance")]
        public TextHorizontalAlignment TextAlignment
        {
            get => textAlignment;
            set
            {
                if (value == textAlignment)
                    return;
                textAlignment = value;
                NotifyPropertyChanged(nameof(TextAlignment));
            }
        }

        /// <summary>
        /// Gets or sets text line spacing.
        /// </summary>
        [Category("Appearance")]
        public int VerticalSpacing
        {
            get => verticalSpacing;
            set
            {
                if (value == verticalSpacing)
                    return;
                verticalSpacing = value;
                NotifyPropertyChanged(nameof(VerticalSpacing));
            }
        }

        /// <summary>
        /// Determines whether the <see cref="TextBlock"/> supports rich text layout commands.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool SupportLayoutCommands { get => text.SupportsCommands; set => text.SupportsCommands = value; }

        /// <summary>
        /// Determines whether the <see cref="TextBlock"/> should show rich text layout errors if they appear.
        /// </summary>
        [Category("Miscellaneous")]
        public bool ShowRichLayoutErrors { get; set; }

        /// <summary>
        /// Gets or sets the value indicating if the text block should set default color when mouse is over.
        /// </summary>
        public bool IndicateInteractionColor { get; set; }

        /// <summary>
        /// Occurs when the value of the <see cref="Text"/> property changes.
        /// </summary>
        public event EventHandler? TextChanged;

        /// <summary>
        /// Creates new instance of the <see cref="TextBlock"/> class.
        /// </summary>
        public TextBlock()
        {
        }

        /// <summary>
        /// Creates new instance of the <see cref="TextBlock"/> class with given text and font.
        /// </summary>
        public TextBlock(string? text, SpriteFontBase? font)
        {
            Text = text;
            Font = font;
        }

        /// <inheritdoc/>
        protected override void OnFontChanged()
        {
            base.OnFontChanged();
            text.Font = Font;
        }

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            if (text.Font == null) return;

            Color color = GetCurrentForegroundColor();
            bool ignoreColorChange = IndicateInteractionColor && (InteractionState & (ControlInteractionState.Default | ControlInteractionState.Focused)) != 0;

            var textToDraw = textEllipsis == TextEllipsis.None ? text : autoEllipsisText;
            textToDraw.IgnoreColorCommand = ignoreColorChange;
            var bounds = ZeroBounds;
            var x = bounds.X;
            if (TextAlignment == TextHorizontalAlignment.Center)
            {
                x += bounds.Width / 2;
            }
            else if (TextAlignment == TextHorizontalAlignment.Right)
            {
                x += bounds.Width;
            }

            if (TextWrapping) textToDraw.Width = ActualBounds.Width;

            try
            {
                context.DrawRichText(textToDraw, new Vector2(x, bounds.Y), color, horizontalAlignment: TextAlignment);
            }
            catch (Exception ex)
            {
                x = bounds.X;
                errorText.Text = ShowRichLayoutErrors ? $"RTL Error: {ex.Message}" : Text;
                context.DrawRichText(errorText, new Vector2(x, bounds.Y), Color.Red);
            }
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            if (Font == null)
            {
                return Point.Zero;
            }

            int width = availableSize.X;
            int height = availableSize.Y;
            Point result;
            try
            {
                RichTextLayout resultMeasureProvider;
                if (TextEllipsis != TextEllipsis.None)
                {
                    resultMeasureProvider = autoEllipsisText = ApplyAutoEllipsis(width, height);
                }
                else
                {
                    resultMeasureProvider = text;
                }

                result = resultMeasureProvider.Measure(TextWrapping ? width : null);
            }
            catch (Exception ex)
            {
                errorText.Font = Font;
                errorText.Text = ShowRichLayoutErrors ? $"RTL Error: {ex.Message}" : Text;
                result = errorText.Measure(TextWrapping ? width : null);
            }

            if (result.Y < Font.LineHeight)
            {
                result.Y = Font.LineHeight;
            }
            return result;
        }

        private RichTextLayout ApplyAutoEllipsis(int width, int height)
        {
            var unchangedMeasure = text.Measure(TextWrapping ? width : null);
            if (unchangedMeasure.X <= width && unchangedMeasure.Y <= height)
            {
                return text;
            }

            string? originalText = Text;
            if (originalText == null)
            {
                return text;
            }
            var measureText = new RichTextLayout()
            {
                Text = Text,
                Font = Font,
                VerticalSpacing = VerticalSpacing,
                Width = text.Width,
                CalculateGlyphs = text.CalculateGlyphs,
                SupportsCommands = SupportLayoutCommands
            };
            string result;

            // find longest possible string using binary search
            int left = 0;
            int right = originalText.Length;
            int center = 0;

            while (left <= right)
            {
                center = left + ((right - left) / 2);
                measureText.Text = $"{originalText[..center]}{AutoEllipsisString}";

                var measure = measureText.Measure(TextWrapping ? width : null);
                if (measure.X == width && measure.Y <= height)
                {
                    break;
                }
                else if (measure.X > width || measure.Y > height)
                {
                    right = center - 1;
                }
                else
                {
                    left = center + 1;
                }
            }

            result = originalText[..center];

            if (TextEllipsis == TextEllipsis.Word)
            {
                // cut on spaces rather than in the middle of a word.
                // preserve a space character before the ellipsis if there is
                // enough room for it.
                try
                {
                    var closestSpace = originalText.LastIndexOf(' ', center);
                    if (closestSpace > 0)
                    {
                        int subStrLength = closestSpace;
                        measureText.Text = originalText[..(closestSpace + 1)] + AutoEllipsisString;
                        if (measureText.Measure(TextWrapping ? width : null).X < width)
                        {
                            subStrLength++;
                        }
                        result = originalText[..subStrLength];
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    // do nothing
                }
            }

            measureText.Text = result + AutoEllipsisString;

            return measureText;
        }
    }

    /// <summary>
    /// Represents an auto ellipsis method.
    /// </summary>
    public enum TextEllipsis
    {
        /// <summary>
        /// Disable text ellipsis.
        /// </summary>
        None,

        /// <summary>
        /// Cut text after any character.
        /// </summary>
        Character,

        /// <summary>
        /// Cut only at spaces.
        /// </summary>
        Word
    }
}