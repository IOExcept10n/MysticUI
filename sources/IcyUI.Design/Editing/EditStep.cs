// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// One atomic edit: a text change, and the actions that mirror it onto the live pages.
    /// </summary>
    internal sealed class EditStep(TextChangeSet change, IReadOnlyList<MirrorAction> actions, string description)
    {
        public TextChangeSet Change { get; } = change;

        public IReadOnlyList<MirrorAction> Actions { get; } = actions;

        public string Description { get; } = description;
    }
}
