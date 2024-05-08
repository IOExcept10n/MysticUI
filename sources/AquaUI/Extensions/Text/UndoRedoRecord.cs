using System.Runtime.InteropServices;

namespace AquaUI.Extensions.Text
{
    internal enum TextEditOperationType
    {
        Insertion,
        Deletion,
        Replacement,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct UndoRedoRecord(TextEditOperationType OperationType, string Data, int Position, int Length);
}
