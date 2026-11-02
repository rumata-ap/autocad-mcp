using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nodal.AcadMcp.Contracts;

/// <summary>
/// The BCL members this codebase uses that .NET Framework 4.8 does not ship.
/// Everything here exists on both target frameworks with one behaviour, so a call site
/// reads the same whichever half of the product it is compiled into. The pieces that only
/// stand in for a missing overload live behind NET48 so they cannot shadow the real one
/// on net10.
/// </summary>
public static class NetCompat
{
    public static int ProcessId { get; } = Process.GetCurrentProcess().Id;

    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    public static float Clamp(float value, float min, float max) =>
        value < min ? min : value > max ? max : value;

    public static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;

    public static long Clamp(long value, long min, long max) =>
        value < min ? min : value > max ? max : value;

    public static string ToHexString(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    public static byte[] Sha256(byte[] data)
    {
        using (var sha = SHA256.Create()) return sha.ComputeHash(data);
    }

    public static byte[] Sha256(Stream stream)
    {
        using (var sha = SHA256.Create()) return sha.ComputeHash(stream);
    }

    /// <summary>RandomNumberGenerator.GetBytes(int), which .NET Framework 4.8 lacks.</summary>
    public static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return bytes;
    }

    /// <summary>Length-independent comparison, so a token check leaks nothing through timing.</summary>
    public static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;

        var diff = 0;
        for (var i = 0; i < left.Length; i++) diff |= left[i] ^ right[i];
        return diff == 0;
    }

    /// <summary>A path that is rooted and already fully resolved, as Path.IsPathFullyQualified means it.</summary>
    public static bool IsPathFullyQualified(string path)
    {
        if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path)) return false;

        // "C:foo" is rooted but drive-relative; .NET treats only "C:\..." as fully qualified.
        // Forward slashes are legal separators and must not be rejected.
        if (path.Length >= 2 && path[1] == ':')
            return path.Length > 2 && (path[2] == '\\' || path[2] == '/');

        return true;
    }

    public static Task<byte[]> ReadAllBytesAsync(string path) => Task.Run(() => File.ReadAllBytes(path));

    /// <summary>Marshal.QueryInterface, whose IID parameter is `ref` on .NET Framework and `in` later.</summary>
    public static int QueryInterface(IntPtr unknown, Guid iid, out IntPtr result)
    {
#if NET48
        return System.Runtime.InteropServices.Marshal.QueryInterface(unknown, ref iid, out result);
#else
        return System.Runtime.InteropServices.Marshal.QueryInterface(unknown, in iid, out result);
#endif
    }

#if NET48
    /// <summary>string.ReplaceLineEndings, which .NET Framework 4.8 lacks.</summary>
    public static string ReplaceLineEndings(this string text, string replacement)
    {
        if (text.IndexOfAny(new[] { '\r', '\n' }) < 0) return text;
        return text.Replace("\r\n", replacement).Replace("\r", replacement).Replace("\n", replacement);
    }

    /// <summary>KeyValuePair deconstruction, which .NET Framework 4.8 lacks.</summary>
    public static void Deconstruct<TKey, TValue>(
        this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }

    /// <summary>Dictionary.GetValueOrDefault, which .NET Framework 4.8 lacks on SortedDictionary.</summary>
    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) =>
        dictionary.TryGetValue(key, out var value) ? value : default;

    /// <summary>Task.WaitAsync, which .NET Framework 4.8 lacks.</summary>
    public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout)
    {
        var finished = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (!ReferenceEquals(finished, task)) throw new TimeoutException($"The operation did not finish within {timeout}.");

        return await task.ConfigureAwait(false);
    }

    /// <summary>Stream overloads taking a Memory, which .NET Framework 4.8 lacks.</summary>
    public static Task<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray<byte>(buffer, out var segment)
            && segment.Array is not null)
            return stream.ReadAsync(segment.Array, segment.Offset, segment.Count, cancellationToken);

        return ReadViaBufferAsync(stream, buffer, cancellationToken);
    }

    private static async Task<int> ReadViaBufferAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var temp = new byte[buffer.Length];
        var read = await stream.ReadAsync(temp, 0, temp.Length, cancellationToken).ConfigureAwait(false);
        temp.AsSpan(0, read).CopyTo(buffer.Span);
        return read;
    }

    public static Task WriteAsync(this Stream stream, byte[] buffer, CancellationToken cancellationToken) =>
        stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);

    public static Task WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(buffer, out var segment)
            && segment.Array is not null)
            return stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken);

        return stream.WriteAsync(buffer.ToArray(), 0, buffer.Length, cancellationToken);
    }

    /// <summary>TcpListener.AcceptTcpClientAsync(CancellationToken), which .NET Framework 4.8 lacks.</summary>
    public static async Task<TcpClient> AcceptTcpClientAsync(this TcpListener listener, CancellationToken cancellationToken)
    {
        using (cancellationToken.Register(() => { try { listener.Stop(); } catch (Exception) { } }))
        {
            try
            {
                return await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
    }
#endif
}
