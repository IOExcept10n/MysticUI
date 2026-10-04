// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Mirrors one part of a text edit onto the live pages.
    /// </summary>
    /// <remarks>
    /// <see cref="Execute"/> runs after the document already holds the new text and tree. When a later action of the
    /// same step fails, the document is restored to the old text and tree first, then <see cref="Revert"/> runs, so
    /// an action must be able to undo whatever part of <see cref="Execute"/> got done, including when
    /// <see cref="Execute"/> itself threw halfway. <see cref="CreateInverse"/> runs only after the whole step
    /// succeeded.
    /// </remarks>
    internal abstract class MirrorAction
    {
        /// <summary>
        /// Gets the element this action moves and where it lands, so its id survives the re-parse.
        /// </summary>
        public virtual MoveHint? Hint => null;

        /// <summary>
        /// Gets the removed element this action brings back and its old ids, so they survive the re-parse.
        /// </summary>
        public virtual RestoreHint? Restore => null;

        public abstract void Execute(MirrorContext context);

        public abstract void Revert(MirrorContext context);

        public abstract MirrorAction CreateInverse(MirrorContext context);
    }
}
