// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Places elements in a <see cref="Grid"/>: a drop lands in the cell under the pointer, and a resize grows or shrinks
    /// <c>Grid.RowSpan</c>/<c>Grid.ColumnSpan</c> as the dragged edge crosses into the next track.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Grid.Row</c>, <c>Grid.Column</c> and the spans are written only when they differ from their defaults (0 and 1),
    /// and removed when they return to them.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// On an axis the element stretches on, the dragged edge follows the pointer: the span ends in the track the edge is
    /// in, and the rest of that track becomes the margin on the dragged side. Within <see cref="SnapDistance"/> of a track
    /// line the edge snaps to the line, so whole-cell spans stay easy to hit.
    /// </description></item>
    /// <item><description>
    /// On an axis with an explicit size, the size follows the pointer, and the span grows once the edge reaches
    /// <see cref="SpanThreshold"/> into the next track.
    /// </description></item>
    /// </list>
    /// </remarks>
    public sealed class GridPlacement : MarginPlacement
    {
        private static readonly string[] Owned = ["Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan"];

        private float snapDistance = 8;

        /// <summary>
        /// Gets or sets how far, as a fraction of a track, the dragged edge of an element with an explicit size must reach
        /// into the next track before the span grows to include it. Defaults to <c>0.25</c>.
        /// </summary>
        public float SpanThreshold { get; set; } = 0.25f;

        /// <summary>
        /// Gets or sets how close, in layout units, the dragged edge of a stretched element must come to a track line to
        /// snap to it. Defaults to <c>8</c>; <c>0</c> turns snapping off.
        /// </summary>
        /// <remarks>
        /// Layout units are scaled with the rest of the UI (<see cref="Canvas.EffectiveScale"/>), so the distance feels the
        /// same at any display scale. The editor skips snapping while <see langword="Alt"/> is held.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or not a number.</exception>
        public float SnapDistance
        {
            get => snapDistance;
            set
            {
                if (!(value >= 0))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The snap distance must be a non-negative number.");
                snapDistance = value;
            }
        }

        /// <inheritdoc/>
        public override IReadOnlyList<string> OwnedAttributes => Owned;

        /// <inheritdoc/>
        public override PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point)
        {
            ArgumentNullException.ThrowIfNull(context);
            var grid = (Grid)context.Container;
            (float[] columns, float[] rows) = Tracks(grid, context);

            int columnSpan = Math.Min(Grid.GetColumnSpan(context.Element), columns.Length - 1);
            int rowSpan = Math.Min(Grid.GetRowSpan(context.Element), rows.Length - 1);
            int column = Math.Min(TrackAt(columns, point.X), columns.Length - 1 - columnSpan);
            int row = Math.Min(TrackAt(rows, point.Y), rows.Length - 1 - rowSpan);

            var area = RectangleF.FromLTRB(columns[column], rows[row], columns[column + columnSpan], rows[row + rowSpan]);
            AttributeEdit[] edits = [Index("Grid.Column", column), Index("Grid.Row", row), Span("Grid.ColumnSpan", columnSpan), Span("Grid.RowSpan", rowSpan)];
            int index = context.IsCurrentContainer ? CurrentIndex(context) : context.ContentCount;
            return new PlacementTarget(index, edits, area, IndicatorIsLine: false);
        }

        /// <inheritdoc/>
        public override IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new SpanResize(this, context, handle);
        }

        private static AttributeEdit Index(string name, int value) => new(name, value == 0 ? null : MarkupValues.Format(value));

        private static AttributeEdit Span(string name, int value) => new(name, value == 1 ? null : MarkupValues.Format(value));

        /// <summary>
        /// Gets the track edges in the grid's local space: <c>n + 1</c> offsets for <c>n</c> tracks.
        /// </summary>
        private static (float[] Columns, float[] Rows) Tracks(Grid grid, PlacementContext context)
        {
            Rectangle content = context.ContainerContent;
            float[] columns = Edges(content.Left, grid.ColumnDefinitions.Select(x => x.ActualWidth), content.Width);
            float[] rows = Edges(content.Top, grid.RowDefinitions.Select(x => x.ActualHeight), content.Height);
            return (columns, rows);
        }

        private static float[] Edges(float start, IEnumerable<float> sizes, float whole)
        {
            List<float> edges = [start];
            foreach (float size in sizes)
                edges.Add(edges[^1] + size);
            if (edges.Count == 1)
                edges.Add(start + whole);
            return [.. edges];
        }

        private static int TrackAt(float[] edges, float position)
        {
            for (int i = 0; i < edges.Length - 1; i++)
            {
                if (position < edges[i + 1])
                    return i;
            }

            return edges.Length - 2;
        }

        private sealed class SpanResize : IResizeOperation
        {
            private readonly GridPlacement owner;
            private readonly ResizeHandle handle;
            private readonly IResizeOperation sizes;
            private readonly float[] columns;
            private readonly float[] rows;
            private readonly int column;
            private readonly int row;
            private readonly int columnSpan;
            private readonly int rowSpan;
            private readonly bool stretchedX;
            private readonly bool stretchedY;
            private readonly Rectangle bounds;
            private readonly Thickness margin;

            public SpanResize(GridPlacement owner, PlacementContext context, ResizeHandle handle)
            {
                this.owner = owner;
                this.handle = handle;
                var grid = (Grid)context.Container;
                (columns, rows) = Tracks(grid, context);
                column = Math.Clamp(Grid.GetColumn(context.Element), 0, columns.Length - 2);
                row = Math.Clamp(Grid.GetRow(context.Element), 0, rows.Length - 2);
                columnSpan = Math.Clamp(Grid.GetColumnSpan(context.Element), 1, columns.Length - 1 - column);
                rowSpan = Math.Clamp(Grid.GetRowSpan(context.Element), 1, rows.Length - 1 - row);
                stretchedX = LayoutMath.Kind(context.Element.HorizontalAlignment, context.Element.Width) == AxisKind.Stretched;
                stretchedY = LayoutMath.Kind(context.Element.VerticalAlignment, context.Element.Height) == AxisKind.Stretched;
                bounds = context.ElementBounds;
                margin = context.Element.Margin;

                // Explicit sizes resize as in a stack: the cell, not the element, anchors it. Stretched axes are handled here.
                ResizeHandle sized = handle;
                if (stretchedX)
                    sized &= ~(ResizeHandle.Left | ResizeHandle.Right);
                if (stretchedY)
                    sized &= ~(ResizeHandle.Top | ResizeHandle.Bottom);
                sizes = new Resize(context, sized, anchorHorizontal: true, anchorVertical: true);
                Indicator = Area(column, columnSpan, row, rowSpan);
            }

            public RectangleF? Indicator { get; private set; }

            public IReadOnlyList<AttributeEdit> Update(Vector2 delta) => Update(delta, snap: true);

            public IReadOnlyList<AttributeEdit> Update(Vector2 delta, bool snap)
            {
                var edits = new List<AttributeEdit>(sizes.Update(delta));
                float snapDistance = snap ? owner.SnapDistance : 0;
                int firstColumn = column, columnCount = columnSpan, firstRow = row, rowCount = rowSpan;
                int left = margin.Left, top = margin.Top, right = margin.Right, bottom = margin.Bottom;
                if ((handle & ResizeHandle.Right) != 0)
                {
                    if (stretchedX)
                        (columnCount, right) = StretchedEnd(columns, column, bounds.Left, bounds.Right + delta.X, snapDistance);
                    else
                        columnCount = EndSpan(columns, column, columnSpan, delta.X);
                    edits.Add(Span("Grid.ColumnSpan", columnCount));
                }

                if ((handle & ResizeHandle.Bottom) != 0)
                {
                    if (stretchedY)
                        (rowCount, bottom) = StretchedEnd(rows, row, bounds.Top, bounds.Bottom + delta.Y, snapDistance);
                    else
                        rowCount = EndSpan(rows, row, rowSpan, delta.Y);
                    edits.Add(Span("Grid.RowSpan", rowCount));
                }

                if ((handle & ResizeHandle.Left) != 0)
                {
                    int last = column + columnSpan - 1;
                    if (stretchedX)
                        (firstColumn, left) = StretchedStart(columns, last, bounds.Left + delta.X, bounds.Right, snapDistance);
                    else
                        firstColumn = StartSpan(columns, column, columnSpan, delta.X).Start;
                    columnCount = last - firstColumn + 1;
                    edits.Add(Index("Grid.Column", firstColumn));
                    edits.Add(Span("Grid.ColumnSpan", columnCount));
                }

                if ((handle & ResizeHandle.Top) != 0)
                {
                    int last = row + rowSpan - 1;
                    if (stretchedY)
                        (firstRow, top) = StretchedStart(rows, last, bounds.Top + delta.Y, bounds.Bottom, snapDistance);
                    else
                        firstRow = StartSpan(rows, row, rowSpan, delta.Y).Start;
                    rowCount = last - firstRow + 1;
                    edits.Add(Index("Grid.Row", firstRow));
                    edits.Add(Span("Grid.RowSpan", rowCount));
                }

                var next = new Thickness(left, top, right, bottom);
                if (next != margin)
                    edits.Add(new AttributeEdit("Margin", MarkupValues.Format(next)));
                Indicator = Area(firstColumn, columnCount, firstRow, rowCount);
                return edits;
            }

            /// <summary>
            /// Puts a stretched element's dragged end edge at <paramref name="edge"/>: the span ends in the track the edge is
            /// in, and the rest of that track becomes the end margin.
            /// </summary>
            private static (int Span, int Margin) StretchedEnd(float[] edges, int start, float elementStart, float edge, float snapDistance)
            {
                float min = elementStart + 1;
                edge = Math.Clamp(edge, min, Math.Max(min, edges[^1]));
                edge = Snap(edges, edge, start + 1, edges.Length - 1, snapDistance, min, float.PositiveInfinity);
                int last = start;
                while (last < edges.Length - 2 && edge > edges[last + 1])
                    last++;

                return (last - start + 1, Math.Max(0, (int)MathF.Round(edges[last + 1] - edge)));
            }

            /// <summary>
            /// Puts a stretched element's dragged start edge at <paramref name="edge"/>: the span starts in the track the edge
            /// is in, and the part of that track before the edge becomes the start margin.
            /// </summary>
            private static (int First, int Margin) StretchedStart(float[] edges, int end, float edge, float elementEnd, float snapDistance)
            {
                float max = elementEnd - 1;
                edge = Math.Clamp(edge, Math.Min(edges[0], max), max);
                edge = Snap(edges, edge, 0, end, snapDistance, float.NegativeInfinity, max);
                int first = end;
                while (first > 0 && edge < edges[first])
                    first--;

                return (first, Math.Max(0, (int)MathF.Round(edge - edges[first])));
            }

            /// <summary>
            /// Moves <paramref name="edge"/> onto the nearest track line, among edges <paramref name="from"/> to
            /// <paramref name="to"/>, that is within <paramref name="snapDistance"/> and inside [<paramref name="min"/>,
            /// <paramref name="max"/>]; leaves it where it is when there is none.
            /// </summary>
            private static float Snap(float[] edges, float edge, int from, int to, float snapDistance, float min, float max)
            {
                float snapped = edge;
                float nearest = snapDistance;
                for (int i = from; i <= to; i++)
                {
                    float distance = MathF.Abs(edges[i] - edge);
                    if (distance <= nearest && edges[i] >= min && edges[i] <= max)
                    {
                        snapped = edges[i];
                        nearest = distance;
                    }
                }

                return snapped;
            }

            private RectangleF Area(int firstColumn, int columnCount, int firstRow, int rowCount) =>
                RectangleF.FromLTRB(columns[firstColumn], rows[firstRow], columns[firstColumn + columnCount], rows[firstRow + rowCount]);

            /// <summary>
            /// The span whose last track the dragged end edge reaches past the threshold.
            /// </summary>
            private int EndSpan(float[] edges, int start, int span, float delta)
            {
                float edge = edges[start + span] + delta;
                int last = start;
                for (int i = start + 1; i < edges.Length - 1; i++)
                {
                    float size = edges[i + 1] - edges[i];
                    if (edge > edges[i] + (owner.SpanThreshold * size))
                        last = i;
                }

                return last - start + 1;
            }

            /// <summary>
            /// The first track the dragged start edge reaches past the threshold, and the span that keeps the end track.
            /// </summary>
            private (int Start, int Span) StartSpan(float[] edges, int start, int span, float delta)
            {
                int end = start + span - 1;
                float edge = edges[start] + delta;
                int first = end;
                for (int i = end - 1; i >= 0; i--)
                {
                    float size = edges[i + 1] - edges[i];
                    if (edge < edges[i + 1] - (owner.SpanThreshold * size))
                        first = i;
                }

                return (first, end - first + 1);
            }
        }
    }
}
