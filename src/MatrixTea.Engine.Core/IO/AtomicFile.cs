// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Text;

namespace MatrixTea.Engine.Core.IO;

/// <summary>Same-directory atomic replacement with durable flush and a previous-version backup.</summary>
public static class AtomicFile
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    public static void WriteText(string path, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        Write(path, stream =>
        {
            using var writer = new StreamWriter(stream, Utf8, 4096, leaveOpen: true);
            writer.Write(contents); writer.Flush();
        });
    }
    public static Task WriteTextAsync(string path, string contents, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(contents);
        return WriteAsync(path, async (stream, token) =>
        {
            await using var writer = new StreamWriter(stream, Utf8, 4096, leaveOpen: true);
            await writer.WriteAsync(contents.AsMemory(), token);
            await writer.FlushAsync();
        }, cancellation);
    }
    public static void Write(string path, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        path = Prepare(path); string temporary = Temporary(path);
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { write(stream); stream.Flush(true); }
            Commit(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static async Task WriteAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(write); cancellation.ThrowIfCancellationRequested();
        path = Prepare(path); string temporary = Temporary(path);
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            { await write(stream, cancellation); await stream.FlushAsync(cancellation); stream.Flush(true); }
            cancellation.ThrowIfCancellationRequested();
            Commit(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static string Prepare(string path)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }
    private static string Temporary(string path) => path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    private static void Commit(string temporary, string path)
    {
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }
}
