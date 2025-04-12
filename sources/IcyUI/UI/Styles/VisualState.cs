using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icy.UI.Styles
{
    [Flags]
    public enum ControlState
    {
        Normal = 0,
        Hovered = 1 << 0,
        Pressed = 1 << 1,
        Disabled = 1 << 2,
        Focused = 1 << 3,
        Touching = 1 << 4
    }

    public class VisualState
    {
        public string Name { get; }
        public ControlState State { get; }
        public Dictionary<string, object> Setters { get; }
    }

    public class VisualStateGroup
    {
        public string Name { get; }
        public List<VisualState> States { get; }
    }
}
