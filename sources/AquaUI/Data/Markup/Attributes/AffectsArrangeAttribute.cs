using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.Data.Markup.Attributes
{
    /// <summary>
    /// Represents the attribute for the properties that affect element arrange when updated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class AffectsArrangeAttribute : Attribute
    {
    }
}
