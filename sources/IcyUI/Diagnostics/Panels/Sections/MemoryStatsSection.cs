// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Diagnostics.Panels.Sections
{
    /// <summary>
    /// Shows managed heap size and total allocated memory, via <see cref="GC"/> - deliberately engine-agnostic,
    /// unlike the MonoGame-only <c>Hebron.Runtime.MemoryStats</c> reflection the samples used before this existed.
    /// </summary>
    public class MemoryStatsSection : IDebugHudSection
    {
        private const double BytesPerMebibyte = 1024 * 1024;

        /// <inheritdoc/>
        public string Name => "Memory";

        /// <inheritdoc/>
        public UIElement Build() => new TextBlock { Foreground = Color.WhiteSmoke, FontSize = 13 };

        /// <inheritdoc/>
        public void Refresh(UIElement built, in DebugFrameContext frame)
        {
            var text = (TextBlock)built;
            double heapMebibytes = GC.GetGCMemoryInfo().HeapSizeBytes / BytesPerMebibyte;
            double allocatedMebibytes = GC.GetTotalMemory(false) / BytesPerMebibyte;
            text.Text = $"Heap {heapMebibytes:F1} MiB, allocated {allocatedMebibytes:F1} MiB";
        }
    }
}
