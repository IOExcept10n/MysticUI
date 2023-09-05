using Stride.Core.Mathematics;

namespace MysticUI.Controls
{
    /// <summary>
    /// An interface for objects that can transform positions.
    /// </summary>
    public interface ITransformable
    {
        /// <summary>
        /// Converts global vector coordinate to the local coordinate system.
        /// </summary>
        /// <param name="position">A vector to convert.</param>
        /// <returns>New vector with local coordinates.</returns>
        public Vector2 ToLocal(Vector2 position);

        /// <summary>
        /// Converts local vector coordinate to the global coordinate system.
        /// </summary>
        /// <param name="position">A vector to convert.</param>
        /// <returns>New vector with global coordinates.</returns>
        public Vector2 ToGlobal(Vector2 position);
    }
}