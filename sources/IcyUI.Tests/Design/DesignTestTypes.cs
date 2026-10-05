// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;

namespace Icy.Tests.Design
{
    /// <summary>
    /// An element with a plain CLR property the property registry knows nothing about, so it has no "unset" state:
    /// clearing it in markup can only be mirrored by rebuilding the element.
    /// </summary>
    internal sealed class ClrBox : UIElement
    {
        public string? Caption { get; set; }

        protected override Size MeasureContent() => Size.Empty;

        protected override void ArrangeContent()
        {
        }
    }

    /// <summary>
    /// An element whose only constructor consumes an attribute, so changing that attribute needs a rebuild.
    /// </summary>
    internal sealed class Labeled : UIElement
    {
        public Labeled(string label)
        {
            Label = label;
        }

        public string Label { get; }

        protected override Size MeasureContent() => Size.Empty;

        protected override void ArrangeContent()
        {
        }
    }

    /// <summary>
    /// An element whose plain CLR setter throws a non-markup exception for bad values, like a game's own validation.
    /// </summary>
    internal sealed class ThrowingBox : UIElement
    {
        private int count;

        public int Count
        {
            get => count;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                count = value;
            }
        }

        protected override Size MeasureContent() => Size.Empty;

        protected override void ArrangeContent()
        {
        }
    }

    /// <summary>
    /// A panel that refuses text blocks, so adding one fails after the child was already built.
    /// </summary>
    internal sealed class PickyPanel : Icy.UI.Controls.StackPanel
    {
        protected override void OnChildAdding(object? sender, Icy.Data.CancellableEventArgs<UIElement> e)
        {
            if (e.Data is Icy.UI.Controls.TextBlock)
                throw new InvalidOperationException("No text blocks here.");
            base.OnChildAdding(sender, e);
        }
    }
}
