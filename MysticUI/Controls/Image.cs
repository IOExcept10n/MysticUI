using MysticUI.Extensions;
using Stride.Core.Mathematics;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a control that displays an image.
    /// </summary>
    public class Image : Control
    {
        /// <summary>
        /// Provides the access to an <see cref="Image"/> foreground without necessary to raise any events.
        /// </summary>
        protected IImage? foreground;

        /// <summary>
        /// Foreground image to draw.
        /// </summary>
        [Category("Appearance")]
        public IImage? Foreground
        {
            get => foreground;
            set
            {
                if (value == foreground) return;
                foreground = value;
                OnForegroundChanged();
                NotifyPropertyChanged(nameof(Foreground));
            }
        }

        /// <summary>
        /// Image source to load an image from it.
        /// </summary>
        [Category("Appearance")]
        public string? Source
        {
            get => Foreground?.Source;
            set
            {
                if (Desktop != null && value != null)
                {
                    Foreground = EnvironmentSettings.DefaultAssetsResolver.LoadAsset<IImage>(Desktop.AssetContext, value);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value that describes image stretch way to fill target rectangle.
        /// </summary>
        [DefaultValue(Stretch.Uniform)]
        [Category("Appearance")]
        public Stretch Stretch { get; set; } = Stretch.Uniform;

        /// <summary>
        /// Occurs when the value of the <see cref="Foreground"/> property changes.
        /// </summary>
        public event EventHandler? ForegroundChanged;

        /// <summary>
        /// Creates a new instance of the <see cref="Image"/> class.
        /// </summary>
        public Image()
        {
            // Default foreground for other controls is black but th image should use this color as a filter so color should be white as default.
            ForegroundColor = ActiveForegroundColor = DisabledForegroundColor = PressedForegroundColor = Color.White;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Image"/> class with setting foreground image.
        /// </summary>
        /// <param name="image"></param>
        public Image(IImage image) : this()
        {
            Foreground = image;
        }

        /// <summary>
        /// Measures the size of an image inside.
        /// </summary>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            return Foreground?.Size ?? Point.Zero;
        }

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            if (foreground != null)
            {
                var bounds = ActualBounds;
                bounds.Width = bounds.Width.OptionalClamp(MinWidth, MaxWidth);
                bounds.Height = bounds.Height.OptionalClamp(MinHeight, MaxHeight);
                switch (Stretch)
                {
                    case Stretch.UniformToFill:
                        {
                            var aspect = (float)foreground.Size.X / foreground.Size.Y;
                            if (bounds.Height <= bounds.Width)
                            {
                                bounds.Height = (int)(bounds.Width * aspect);
                            }
                            else
                            {
                                bounds.Width = (int)(bounds.Height * (1 / aspect));
                            }
                            break;
                        }

                    case Stretch.Uniform:
                        {
                            var aspect = (float)foreground.Size.X / foreground.Size.Y;
                            if (bounds.Height <= bounds.Width)
                            {
                                bounds.Width = (int)(bounds.Height * aspect);
                            }
                            else
                            {
                                bounds.Height = (int)(bounds.Width * (1 / aspect));
                            }
                        }
                        break;

                    case Stretch.None:
                        bounds = new(bounds.X, bounds.Y, foreground.Size.X, foreground.Size.Y);
                        break;
                }
                //bounds.Width = bounds.Width.OptionalClamp(MinWidth, MaxWidth);
                //bounds.Height = bounds.Height.OptionalClamp(MinHeight, MaxHeight);
                foreground.Draw(context, bounds, GetCurrentForegroundColor());
            }
        }

        /// <summary>
        /// Invokes the <see cref="ForegroundChanged"/> event.
        /// </summary>
        protected internal virtual void OnForegroundChanged()
        {
            ForegroundChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Describes a way how to stretch an image to fill target rectangle.
    /// </summary>
    public enum Stretch
    {
        /// <summary>
        /// Draws an image without stretching using its original size.
        /// </summary>
        None,

        /// <summary>
        /// Stretches an image without saving proportions.
        /// </summary>
        Stretch,

        /// <summary>
        /// Resizes target image size with keeping proportions to fill all rectangle space.
        /// </summary>
        UniformToFill,

        /// <summary>
        /// Resizes target image size with keeping proportions to render whole image inside the rectangle.
        /// </summary>
        Uniform,
    }
}