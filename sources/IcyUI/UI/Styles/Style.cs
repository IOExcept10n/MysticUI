using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icy.UI.Styles
{
    public class Style
    {
        public Type TargetType { get; }
        public Dictionary<string, object> Setters { get; }
        public List<VisualStateGroup> StateGroups { get; }
        public Style? BasedOn { get; set; }

        public void Apply(UIElement control)
        {
            // Apply base setters
            foreach (var setter in Setters)
            {
                var property = control.GetType().GetProperty(setter.Key);
                if (property != null)
                {
                    property.SetValue(control, setter.Value);
                }
            }

            // Apply state groups
            //foreach (var group in StateGroups)
            //{
            //    control.RegisterStateGroup(group);
            //}

            BasedOn?.Apply(control);
        }
    }
}
