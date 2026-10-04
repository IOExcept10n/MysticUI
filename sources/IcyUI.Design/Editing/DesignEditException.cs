// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Raised by a mirror action for an edit that can't be made. The pipeline turns it into a failed
    /// <see cref="EditResult"/>.
    /// </summary>
    internal sealed class DesignEditException(string message) : Exception(message);
}
