using Stride.Core.Mathematics;

namespace MysticUI.Controls
{
    /// <summary>
    /// Gets the special draggable UI element for other controls.
    /// </summary>
    public class Thumb : Image
    {
        /// <summary>
        /// Thickness of the thumb.
        /// </summary>
        public int Thickness { get; set; }

        /// <summary>
        /// Orientation of the thumb.
        /// </summary>
        public Orientation Orientation { get; set; }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            var result = Point.Zero;
            if (Orientation == Orientation.Horizontal)
            {
                result.Y = Thickness;
            }
            else
            {
                result.X = Thickness;
            }
            return result;
        }
    }
}