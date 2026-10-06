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
    /// <c>Grid.Row</c>, <c>Grid.Column</c> and the spans are written only when they differ from their defaults (0 and 1),
    /// and removed when they return to them. An axis the element stretches on changes only its span; an axis with an
    /// explicit size also writes <c>Width</c>/<c>Height</c>.
    /// </remarks>
    public sealed class GridPlacement : MarginPlacement
    {
        private static readonly string[] Owned = ["Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan"];

        /// <summary>
        /// Gets or sets how far, as a fraction of a track, the dragged edge must reach into the next track before the span
        /// grows to include it. Defaults to <c>0.25</c>.
        /// </summary>
        public float SpanThreshold { get; set; } = 0.25f;

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
            AttributeEdit[] edits = [Index("Grid.Column", column), Index("Grid.Row", row)];
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

                // Explicit sizes resize as in a stack: the cell, not the element, anchors it.
                ResizeHandle sized = handle;
                if (stretchedX)
                    sized &= ~(ResizeHandle.Left | ResizeHandle.Right);
                if (stretchedY)
                    sized &= ~(ResizeHandle.Top | ResizeHandle.Bottom);
                sizes = new Resize(context, sized, anchorHorizontal: true, anchorVertical: true);
            }

            public IReadOnlyList<AttributeEdit> Update(Vector2 delta)
            {
                var edits = new List<AttributeEdit>(sizes.Update(delta));
                if ((handle & ResizeHandle.Right) != 0)
                    edits.Add(Span("Grid.ColumnSpan", EndSpan(columns, column, columnSpan, delta.X)));
                if ((handle & ResizeHandle.Bottom) != 0)
                    edits.Add(Span("Grid.RowSpan", EndSpan(rows, row, rowSpan, delta.Y)));
                if ((handle & ResizeHandle.Left) != 0)
                {
                    (int start, int span) = StartSpan(columns, column, columnSpan, delta.X);
                    edits.Add(Index("Grid.Column", start));
                    edits.Add(Span("Grid.ColumnSpan", span));
                }

                if ((handle & ResizeHandle.Top) != 0)
                {
                    (int start, int span) = StartSpan(rows, row, rowSpan, delta.Y);
                    edits.Add(Index("Grid.Row", start));
                    edits.Add(Span("Grid.RowSpan", span));
                }

                return edits;
            }

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
