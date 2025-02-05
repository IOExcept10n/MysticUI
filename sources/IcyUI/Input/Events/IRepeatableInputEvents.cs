// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Events
{
    /// <summary>
    /// Represents an interface for the event sources that can be repeated over the time if user keeps event trigger active.
    /// </summary>
    public interface IRepeatableInputEvents
    {
        /// <summary>
        /// Gets or sets the value that defines the delay between the events trigger repeat.
        /// </summary>
        TimeSpan RepeatDelay { get; set; }
    }

    /// <summary>
    /// Represents an interface for the event sources that can start repeat after a short delay.
    /// </summary>
    public interface IStartRepeatEvents : IRepeatableInputEvents
    {
        /// <summary>
        /// Gets or sets the value that defines the delay before the beginning of events trigger repeat.
        /// </summary>
        TimeSpan RepeatStartDelay { get; set; }
    }
}
