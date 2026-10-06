// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The editor's full-surface overlay: in Edit mode it takes every pointer gesture over the canvas and hands it to its
    /// <see cref="EditorFrame"/>, and it draws the adorners.
    /// </summary>
    internal sealed class EditorCaptureLayer(EditorFrame frame) : UIElement
    {
        // The drag hooks are protected internal in core; from another assembly they're overridden as protected.
        protected override DragAxes GetDragAxes(in DragClaimContext context) =>
            frame.Session.Mode == EditorMode.Edit ? DragAxes.Both : DragAxes.None;

        protected override void OnDragStarted(Point screenPoint) => frame.BeginDrag(screenPoint);

        protected override void OnDragPerforming(Point screenPoint) => frame.UpdateDrag(screenPoint);

        protected override void OnDragEnded(Point screenPoint) => frame.EndDrag(screenPoint);

        protected override void OnDragCanceled(Point screenPoint) => frame.CancelDrag();

        protected override void OnRender(IRenderContext context) => frame.Render(context);
    }
}
