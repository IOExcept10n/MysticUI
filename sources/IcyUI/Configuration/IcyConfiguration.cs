// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Input;
using Icy.Rendering;

namespace Icy.Configuration
{
    /// <summary>
    /// Aggregates library services and provides fluent configuration for all of these.
    /// </summary>
    /// <param name="Input"> Gets an input service that is used by library in this application. </param>
    /// <param name="Assets"> Gets configuration of the assets system used by library in this application. </param>
    /// <param name="RenderContext"> Gets the rendering service instance used by library in this application. </param>
    /// <param name="Types"> Gets an instance of the reflection-related services used in library. </param>
    public record IcyConfiguration(IInputSystem Input, IAssetConfiguration Assets, IRenderContext RenderContext, IReflectionConfiguration Types);
}