// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Defines a priority for the actions in the dispatcher queue.
    /// </summary>
    public enum DispatcherPriority
    {
        /// <summary>
        /// Actions that should be completed before any operations in dispatcher queue.
        /// </summary>
        Send,

        /// <summary>
        /// Actions that should be completed with the normal priority.
        /// </summary>
        Normal,

        /// <summary>
        /// Operations of data binding update should be sent with this priority, before input actions.
        /// </summary>
        DataBind,

        /// <summary>
        /// Input operations should be sent with the following priority, before background actions.
        /// </summary>
        Input,

        /// <summary>
        /// Any background operations should be completed with this priority.
        /// </summary>
        Background,

        /// <summary>
        /// Operations with the lowest priority are completed after all the previous priorities.
        /// </summary>
        Idle,
    }
}
