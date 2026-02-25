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
    /// Computes the SHA256 hash of the specified byte array.
    /// </summary>
    public static byte[] ComputeSha256(byte[] data)
    {
#if NET5_0_OR_GREATER
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
#if NET5_0_OR_GREATER
        return SHA256.HashData(data.AsSpan(offset, count));
#else
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data, offset, count);
#endif
    }
}
