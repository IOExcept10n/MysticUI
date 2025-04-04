using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;

namespace Icy.Data
{
    /// <summary>
    /// Provides extension methods for the <see cref="Stream"/> class.
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Writes a value to the stream in little-endian byte order.
        /// </summary>
        /// <typeparam name="T">The type of the value to write. Must be unmanaged.</typeparam>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="value">The value to write to the stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static void WriteLittleEndian<T>(this Stream stream, T value)
            where T : unmanaged
        {
            Guard.CanWrite(stream);
            Span<byte> bytes = stackalloc byte[Unsafe.SizeOf<T>()];
            MemoryMarshal.Write(bytes, in value);
            if (!BitConverter.IsLittleEndian)
                bytes.Reverse();
            stream.Write(bytes);
        }

        /// <summary>
        /// Writes a value to the stream in big-endian byte order.
        /// </summary>
        /// <typeparam name="T">The type of the value to write. Must be unmanaged.</typeparam>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="value">The value to write to the stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static void WriteBigEndian<T>(this Stream stream, T value)
            where T : unmanaged
        {
            Guard.CanWrite(stream);
            Span<byte> bytes = stackalloc byte[Unsafe.SizeOf<T>()];
            MemoryMarshal.Write(bytes, in value);
            if (BitConverter.IsLittleEndian)
                bytes.Reverse();
            stream.Write(bytes);
        }

        /// <summary>
        /// Reads a value from the stream in little-endian byte order.
        /// </summary>
        /// <typeparam name="T">The type of the value to read. Must be unmanaged.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>The value read from the stream.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static T ReadLittleEndian<T>(this Stream stream)
            where T : unmanaged
        {
            Guard.CanRead(stream);
            Span<byte> bytes = stackalloc byte[Unsafe.SizeOf<T>()];
            stream.ReadExactly(bytes);
            if (!BitConverter.IsLittleEndian)
                bytes.Reverse();
            return MemoryMarshal.Read<T>(bytes);
        }

        /// <summary>
        /// Reads a value from the stream in big-endian byte order.
        /// </summary>
        /// <typeparam name="T">The type of the value to read. Must be unmanaged.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>The value read from the stream.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static T ReadBigEndian<T>(this Stream stream)
            where T : unmanaged
        {
            Guard.CanRead(stream);
            Span<byte> bytes = stackalloc byte[Unsafe.SizeOf<T>()];
            stream.ReadExactly(bytes);
            if (BitConverter.IsLittleEndian)
                bytes.Reverse();
            return MemoryMarshal.Read<T>(bytes);
        }

        /// <summary>
        /// Asynchronously copies a specified number of bytes from the source stream to the destination stream.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="destination">The destination stream to write to.</param>
        /// <param name="offset">The offset in the source stream to start reading from.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <param name="buffer">An optional buffer to use for copying. If null, a temporary buffer will be allocated.</param>
        /// <param name="token">A cancellation token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the source or destination stream is null.</exception>
        public static async Task CopyBytesAsync(this Stream source, Stream destination, long offset, long length, byte[]? buffer = null, CancellationToken token = default)
        {
            bool tempBuffer = buffer == null;
            try
            {
                if (tempBuffer)
                {
                    buffer = ArrayPool<byte>.Shared.Rent(10240);
                }

                int bytesRead;
                long totalBytesRead = 0;

                // Set the initial position of the source stream
                source.Seek(offset, SeekOrigin.Begin);

                while (totalBytesRead < length && (bytesRead = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer!.Length, length - totalBytesRead)), token)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    totalBytesRead += bytesRead;
                }
            }
            finally
            {
                if (tempBuffer)
                {
                    ArrayPool<byte>.Shared.Return(buffer!);
                }
            }
        }

        /// <summary>
        /// Reverses the endianness of a value.
        /// </summary>
        /// <typeparam name="T">The type of the value. Must be unmanaged.</typeparam>
        /// <param name="value">The value whose endianness is to be reversed.</param>
        /// <returns>The value with reversed endianness.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T ReverseEndianness<T>(this T value)
            where T : unmanaged
        {
            value.AsByteSpan().Reverse();
            return value;
        }

        /// <summary>
        /// Converts a reference to an unmanaged structure into a <see cref="Span{T}"/> of bytes,
        /// allowing for direct manipulation of the underlying memory representation.
        /// </summary>
        /// <typeparam name="T">The type of the unmanaged structure.</typeparam>
        /// <param name="value">A reference to the unmanaged structure to be converted.</param>
        /// <returns>A <see cref="Span{T}"/> of bytes representing the memory of the specified structure.</returns>
        /// <remarks>
        /// This method uses <see cref="MemoryMarshal.CreateSpan"/> and <see cref="Unsafe.As{TFrom, TTo}"/>
        /// to create a span that points to the same memory as the original structure.
        /// It is important to ensure that the type <typeparamref name="T"/> is unmanaged,
        /// as using this method with managed types may lead to undefined behavior.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if <typeparamref name="T"/> is not an unmanaged type.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> AsByteSpan<T>(this ref T value)
            where T : unmanaged
        {
            return MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>());
        }

        /// <summary>
        /// Throws a <see cref="FileNotFoundException"/> with the specified message, file name, and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="fileName">The name of the file that could not be found.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or null if no inner exception is specified.</param>
        /// <exception cref="FileNotFoundException">Always thrown.</exception>
        [DoesNotReturn]
        public static void ThrowFileNotFoundException(string? message = null, string? fileName = null, Exception? innerException = null) =>
            throw new FileNotFoundException(message, fileName, innerException);

        /// <summary>
        /// Throws a <see cref="FileNotFoundException"/> with the specified message, file name, and inner exception, returning a default value of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to return if the exception is not thrown.</typeparam>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="fileName">The name of the file that could not be found.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or null if no inner exception is specified.</param>
        /// <returns>Never returns a value, always throws an exception.</returns>
        /// <exception cref="FileNotFoundException">Always thrown.</exception>
        public static T ThrowFileNotFoundException<T>(string? message = null, string? fileName = null, Exception? innerException = null) =>
            throw new FileNotFoundException(message, fileName, innerException);

        /// <summary>
        /// Throws an <see cref="EndOfStreamException"/> with the specified message and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or null if no inner exception is specified.</param>
        /// <exception cref="EndOfStreamException">Always thrown.</exception>
        [DoesNotReturn]
        public static void ThrowEndOfStreamException(string? message = null, Exception? innerException = null) =>
            throw new EndOfStreamException(message, innerException);

        /// <summary>
        /// Throws an <see cref="EndOfStreamException"/> with the specified message and inner exception, returning a default value of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to return if the exception is not thrown.</typeparam>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or null if no inner exception is specified.</param>
        /// <returns>Never returns a value, always throws an exception.</returns>
        /// <exception cref="EndOfStreamException">Always thrown.</exception>
        public static T ThrowEndOfStreamException<T>(string? message = null, Exception? innerException = null) =>
            throw new EndOfStreamException(message, innerException);
    }
}
