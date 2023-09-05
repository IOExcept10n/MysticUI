using System.Runtime.InteropServices;

namespace MysticUI.Extensions.Text
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