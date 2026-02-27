using System.Security.Cryptography;
using System.Text;

namespace FirmwareKit.Lp;

/// <summary>
/// Provides extension methods for cross-framework compatibility.
/// </summary>
internal static class CompatibilityExtensions
{
    /// <summary>
    /// Decodes a read-only span of bytes into a string.
    /// </summary>
    /// <param name="encoding">The character encoding to use.</param>
    /// <param name="bytes">Input byte buffer.</param>
    /// <returns>The decoded string.</returns>
    public static string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return encoding.GetString(bytes);
#else
        unsafe
        {
            fixed (byte* p = bytes)
            {
                return encoding.GetString(p, bytes.Length);
            }
        }
#endif
    }

    /// <summary>
    /// Encodes a string into a span of bytes.
    /// </summary>
    /// <param name="encoding">The encoding to use.</param>
    /// <param name="s">The input string.</param>
    /// <param name="bytes">The destination byte span.</param>
    /// <returns>The number of bytes written.</returns>
    public static int GetBytes(this Encoding encoding, string s, Span<byte> bytes)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return encoding.GetBytes(s, bytes);
#else
        var arr = encoding.GetBytes(s);
        var len = Math.Min(arr.Length, bytes.Length);
        arr.AsSpan(0, len).CopyTo(bytes);
        return len;
#endif
    }

    /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    /// </summary>
    public static int Read(this Stream stream, Span<byte> buffer)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return stream.Read(buffer);
#else
        var arr = new byte[buffer.Length];
        var read = stream.Read(arr, 0, arr.Length);
        arr.AsSpan(0, read).CopyTo(buffer);
        return read;
#endif
    }

    /// <summary>
    /// Computes the SHA256 hash of the specified byte array.
    /// </summary>
    public static byte[] ComputeSha256(byte[] data)
    {
#if NET6_0_OR_GREATER
        return SHA256.HashData(data);
#else
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
#endif
    }

    /// <summary>
    /// Computes the SHA256 hash of the specified byte array segment.
    /// </summary>
    public static byte[] ComputeSha256(byte[] data, int offset, int count)
    {
#if NET6_0_OR_GREATER
        return SHA256.HashData(data.AsSpan(offset, count));
#else
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data, offset, count);
#endif
    }

    /// <summary>
    /// Computes the SHA256 hash of the specified read-only span of bytes.
    /// </summary>
    /// <param name="source">The source bytes to hash.</param>
    /// <param name="destination">The destination span for the hash (must be 32 bytes).</param>
    /// <returns>True if the hash was computed successfully.</returns>
    public static bool TryComputeSha256(ReadOnlySpan<byte> source, Span<byte> destination)
    {
#if NET6_0_OR_GREATER
        return SHA256.TryHashData(source, destination, out _);
#else
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(source.ToArray());
        hash.AsSpan().CopyTo(destination);
        return true;
#endif
    }
}
