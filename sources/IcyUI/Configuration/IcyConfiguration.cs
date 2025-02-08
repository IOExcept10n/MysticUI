using Icy.Input;
using Icy.Rendering;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents a service that aggregates library services and provides fluent configuration for all of these.
    /// </summary>
    public class IcyConfiguration
    {
        public IInputSystem InputSystem { get; }

        public IAssetConfiguration AssetConfiguration { get; }

        public IRenderContext RenderContext { get; }
    }
}
