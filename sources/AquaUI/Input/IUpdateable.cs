using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.Input
{
    /// <summary>
    /// Represents an interface for the updateable input system component.
    /// </summary>
    internal interface IUpdateable
    {
        /// <summary>
        /// Performs the update for the component.
        /// </summary>
        /// <param name="elapsedTime">Time since the last frame.</param>
        void Update(TimeSpan elapsedTime);
    }
}
