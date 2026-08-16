// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Declares when a <see cref="IBinding"/> refreshes its target from its source, i.e. the opposite
    /// direction from <see cref="Markup.UpdateSourceTrigger"/>.
    /// </summary>
    public enum UpdateTargetTrigger : byte
    {
        /// <summary>
        /// Refreshes the target only when the source raises <see cref="INotifyPropertyChanged.PropertyChanged"/>.
        /// </summary>
        /// <remarks>
        /// This is the default and matches how most view-model-driven UI works. It does nothing for sources that
        /// don't implement <see cref="INotifyPropertyChanged"/> — such a source's value is read once, at bind time,
        /// and never refreshed again.
        /// </remarks>
        Reactive,

        /// <summary>
        /// Refreshes the target once per frame, regardless of whether the source raises change notifications.
        /// </summary>
        /// <remarks>
        /// Intended for game-loop-driven state that mutates every tick without going through
        /// <see cref="INotifyPropertyChanged"/> (e.g. plain fields on a simulation/ECS component). The refresh is
        /// driven by <see cref="Markup.Dispatcher.UpdateFrameBindings"/>, called once per frame from
        /// <see cref="UI.Canvas.Render"/> — no per-property subscription is set up, so this mode has no reactive
        /// overhead beyond the one read-and-compare per frame.
        /// </remarks>
        EveryFrame,
    }
}
