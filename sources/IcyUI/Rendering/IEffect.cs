// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

namespace Icy.Rendering
{
    /// <summary>
    /// Defines most used UI effect types.
    /// </summary>
    public enum EffectCode
    {
        /// <summary>
        /// Gradient coloring effect that applies linear color gradient to the given texture.
        /// </summary>
        GradientEffect,

        /// <summary>
        /// Gaussian blur effect that applies blurring to the given texture.
        /// </summary>
        BlurEffect,
    }

    /// <summary>
    /// Represents an interface for the shader effects used in rendering process.
    /// </summary>
    public interface IEffect
    {
        /// <summary>
        /// Gets a value of the parameter with the specified name.
        /// </summary>
        /// <remarks>
        /// Note that because of engine-dependent effects implementations
        /// the wrapper for the <see cref="GetParameter{T}(string)"/> and <see cref="SetParameter{T}(string, T)"/>
        /// methods should be responsible to convert only to compatible types,
        /// all the other types should be either binary compatible with target type
        /// or should be able to be implicitly converted to it.
        /// </remarks>
        /// <typeparam name="T">Expected type of the parameter.</typeparam>
        /// <param name="name">Name of the parameter to get value from.</param>
        /// <returns>Value of the parameter by the specified name.</returns>
        /// <exception cref="InvalidCastException">Occurs when the target type is incompatible with the parameter type.</exception>
        /// <exception cref="KeyNotFoundException">Occurs when the parameter with the specified name not found.</exception>
        public T GetParameter<T>(string name);

        /// <summary>
        /// Sets a value to the parameter with the specified name.
        /// </summary>
        /// <remarks>
        /// Note that because of engine-dependent effects implementations
        /// the wrapper for the <see cref="GetParameter{T}(string)"/> and <see cref="SetParameter{T}(string, T)"/>
        /// methods should be responsible to convert only to compatible types,
        /// all the other types should be either binary compatible with target type
        /// or should be able to be implicitly converted to it.
        /// </remarks>
        /// <typeparam name="T">Expected type of the parameter.</typeparam>
        /// <param name="name">Name of the parameter to set value to.</param>
        /// <param name="value">Value to set to the parameter.</param>
        /// <exception cref="InvalidCastException">Occurs when the target type is incompatible with the parameter type.</exception>
        /// <exception cref="KeyNotFoundException">Occurs when the parameter with the specified name not found.</exception>
        public void SetParameter<T>(string name, T value);
    }
}
