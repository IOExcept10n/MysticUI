// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The editor's overlay over its scope: in Edit mode it takes the left-button and touch gestures there and hands them
    /// to its <see cref="EditorFrame"/>, lets the wheel and middle-button pans through to the page, and draws the adorners.
    /// </summary>
    internal sealed class EditorCaptureLayer(EditorFrame frame) : UIElement
    {
        // The drag hooks are protected internal in core; from another assembly they're overridden as protected.
        // Only primary pointers edit: core reserves middle-button drags for panning, and the right button for menus.
        protected override DragAxes GetDragAxes(in DragClaimContext context) =>
            frame.Session.Mode == EditorMode.Edit && context.Kind is Input.Gestures.PointerKind.MouseLeft or Input.Gestures.PointerKind.Touch
                ? DragAxes.Both
                : DragAxes.None;

        protected override void OnDragStarted(Point screenPoint) => frame.BeginDrag(screenPoint);

        protected override void OnDragPerforming(Point screenPoint) => frame.UpdateDrag(screenPoint);

        protected override void OnDragEnded(Point screenPoint) => frame.EndDrag(screenPoint);

        protected override void OnDragCanceled(Point screenPoint) => frame.CancelDrag();

        protected override void OnRender(IRenderContext context) => frame.Render(context);
    }
}
