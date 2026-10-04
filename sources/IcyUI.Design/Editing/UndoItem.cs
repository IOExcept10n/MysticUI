// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// One thing to replay when an <see cref="UndoEntry"/> is undone or redone.
    /// </summary>
    internal sealed class UndoItem
    {
        private readonly EditStep step;

        private UndoItem(EditStep step)
        {
            this.step = step;
        }

        public static UndoItem ForStep(EditStep step) => new(step);

        public EditStep CreateStep(DesignDocument document) => step;
    }
}
