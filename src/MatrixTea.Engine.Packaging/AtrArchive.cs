using System.Buffers.Binary;
using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace MatrixTea.Engine.Packaging;

/// <summary>ATR1: bounded, authenticated frames with adaptive Brotli/Deflate compression.
/// The format key is distributed with the engine: this is packaging, not DRM or publisher authentication.</summary>
public static class AtrArchive
{
    const int BlockSize = 1024 * 1024;
    const long MaxExpanded = 4L * 1024 * 1024 * 1024;
    static readonly byte[] Magic = Encoding.ASCII.GetBytes("ATR1MATRIXTEA\0\0\0");
    static readonly byte[] FormatKey = SHA256.HashData(Encoding.UTF8.GetBytes("MatrixTea.Engine/ATR1/Artelu/format-key/v1"));

    public static void Pack(string directory, string destination, CancellationToken cancellation = default)
    {
        string source = Path.GetFullPath(directory);
        string archivePath = Path.GetFullPath(destination);
        if (archivePath.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("封裝輸出必須放在來源資料夾之外。");
        var collected = new List<string>(); var pending = new Stack<DirectoryInfo>(); pending.Push(new DirectoryInfo(source)); int directories = 0;
        while (pending.TryPop(out var folder))
        {
            cancellation.ThrowIfCancellationRequested();
            if (++directories > 20000 || folder.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("ATR 來源含有連結或過多資料夾。");
            foreach (var node in folder.EnumerateFileSystemInfos())
            {
                if (node.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("ATR 不接受檔案連結。");
                if (node is DirectoryInfo child) pending.Push(child);
                else { collected.Add(node.FullName); if (collected.Count > 20000) throw new InvalidDataException("封裝檔案數量異常。"); }
            }
        }
        var files = collected.Order(StringComparer.Ordinal).ToArray();
        if (files.Length is 0 or > 20000) throw new InvalidDataException("封裝檔案數量異常。");
        long total = 0;
        var relativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            SafePath(source, relative);
            if (!relativePaths.Add(relative)) throw new InvalidDataException("ATR 不接受大小寫衝突的路徑。");
            for (var node = new FileInfo(file) as FileSystemInfo; node != null && node.FullName != source; node = new DirectoryInfo(Path.GetDirectoryName(node.FullName)!))
                if (node.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("ATR 不接受檔案連結。");
            total = checked(total + new FileInfo(file).Length);
            if (total > MaxExpanded - 32 * 1024 * 1024) throw new InvalidDataException("封裝超過大小限制。");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        string temporary = archivePath + "." + Guid.NewGuid().ToString("N") + ".payload";
        string partial = archivePath + "." + Guid.NewGuid().ToString("N") + ".partial";
        byte[]? key = null;
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
                foreach (string file in files)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var entry = zip.CreateEntry(Path.GetRelativePath(source, file).Replace('\\', '/'), CompressionLevel.NoCompression);
                    using var input = File.OpenRead(file); using var output = entry.Open();
                    CopyBounded(input, output, new FileInfo(file).Length, cancellation);
                }
            using var payload = File.OpenRead(temporary);
            if (payload.Length > MaxExpanded) throw new InvalidDataException("封裝超過大小限制。");
            byte[] header = new byte[40]; Magic.CopyTo(header, 0); RandomNumberGenerator.Fill(header.AsSpan(16, 16));
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(32), payload.Length);
            key = HMACSHA256.HashData(FormatKey, header.AsSpan(16, 16));
            using var aes = new AesGcm(key, 16);
            using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                target.Write(header);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(BlockSize), brotli = ArrayPool<byte>.Shared.Rent(BlockSize), encrypted = ArrayPool<byte>.Shared.Rent(BlockSize);
                using var deflated = new MemoryStream(BlockSize + 1024);
                try
                {
                    int index = 0, read;
                    while ((read = ReadBlock(payload, buffer)) > 0)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        ReadOnlySpan<byte> best = buffer.AsSpan(0, read); byte codec = 0;
                        if (BrotliEncoder.TryCompress(best, brotli.AsSpan(0, BlockSize), out int compressedSize, quality: 4, window: 22) && compressedSize < best.Length)
                        { best = brotli.AsSpan(0, compressedSize); codec = 1; }
                        deflated.SetLength(0); deflated.Position = 0;
                        using (var compressor = new DeflateStream(deflated, CompressionLevel.Optimal, true)) compressor.Write(buffer.AsSpan(0, read));
                        if (deflated.Length < best.Length) { best = deflated.GetBuffer().AsSpan(0, (int)deflated.Length); codec = 2; }
                        WriteFrame(target, aes, header, index++, codec, read, best, encrypted);
                    }
                    WriteFrame(target, aes, header, index, 255, 0, ReadOnlySpan<byte>.Empty, encrypted);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer, true); ArrayPool<byte>.Shared.Return(brotli, true); ArrayPool<byte>.Shared.Return(encrypted, true);
                }
                target.Flush(true);
            }
            File.Move(partial, archivePath, true);
        }
        finally { if (key != null) CryptographicOperations.ZeroMemory(key); if (File.Exists(temporary)) File.Delete(temporary); if (File.Exists(partial)) File.Delete(partial); }
    }

    public static void Extract(string archivePath, string directory, Action<double>? progress = null, CancellationToken cancellation = default)
    {
        string root = Path.GetFullPath(directory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("ATR 解封裝需要空資料夾。");
        for (var dir = new DirectoryInfo(root); dir != null; dir = dir.Parent)
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("ATR 解封裝目錄不可使用連結。");
        Directory.CreateDirectory(root);
        string temporary = Path.Combine(root, ".atr-payload-" + Guid.NewGuid().ToString("N"));
        byte[]? key = null;
        byte[] encryptedBuffer = ArrayPool<byte>.Shared.Rent(BlockSize), decodedBuffer = ArrayPool<byte>.Shared.Rent(BlockSize), expandedBuffer = ArrayPool<byte>.Shared.Rent(BlockSize);
        try
        {
            using (var input = File.OpenRead(archivePath))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] header = new byte[40]; input.ReadExactly(header);
                if (!header.AsSpan(0, 16).SequenceEqual(Magic)) throw new InvalidDataException("不是相容的 MatrixTea ATR1 封裝。");
                long total = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(32));
                if (total <= 0 || total > MaxExpanded) throw new InvalidDataException("ATR 封裝大小異常。");
                key = HMACSHA256.HashData(FormatKey, header.AsSpan(16, 16));
                using var aes = new AesGcm(key, 16); long expanded = 0; int index = 0;
                byte[] frame = new byte[13], tag = new byte[16];
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    input.ReadExactly(frame);
                    byte codec = frame[0]; int rawSize = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1));
                    int encodedSize = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5));
                    if (BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(9)) != index || rawSize < 0 || rawSize > BlockSize || encodedSize < 0 || encodedSize > BlockSize || (codec != 255 && (rawSize == 0 || codec > 2)))
                        throw new InvalidDataException("ATR 區塊格式異常。");
                    input.ReadExactly(tag);
                    input.ReadExactly(encryptedBuffer.AsSpan(0, encodedSize));
                    aes.Decrypt(Nonce(index), encryptedBuffer.AsSpan(0, encodedSize), tag, decodedBuffer.AsSpan(0, encodedSize), Associated(header, frame));
                    index++;
                    if (codec == 255)
                    {
                        if (rawSize != 0 || encodedSize != 0 || expanded != total || input.Position != input.Length) throw new InvalidDataException("ATR 封裝未完整結束。");
                        break;
                    }
                    expanded = checked(expanded + rawSize);
                    if (expanded > total) throw new InvalidDataException("ATR 解壓縮超過預期大小。");
                    if (codec == 0)
                    {
                        if (encodedSize != rawSize) throw new InvalidDataException("ATR 原始區塊長度不符。");
                        output.Write(decodedBuffer.AsSpan(0, rawSize));
                    }
                    else if (codec == 1)
                    {
                        if (!BrotliDecoder.TryDecompress(decodedBuffer.AsSpan(0, encodedSize), expandedBuffer.AsSpan(0, rawSize), out int written) || written != rawSize) throw new InvalidDataException("ATR Brotli 區塊長度不符。");
                        output.Write(expandedBuffer.AsSpan(0, written));
                    }
                    else
                    {
                        using var compressed = new MemoryStream(decodedBuffer, 0, encodedSize, false);
                        using var raw = new DeflateStream(compressed, CompressionMode.Decompress);
                        CopyBounded(raw, output, rawSize, cancellation);
                    }
                    progress?.Invoke(expanded * 0.65 / total);
                }
            }
            using var zip = ZipFile.OpenRead(temporary);
            if (zip.Entries.Count > 20000) throw new InvalidDataException("ATR 檔案數量異常。");
            long filesSize = 0; var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); int done = 0;
            foreach (var entry in zip.Entries)
            {
                cancellation.ThrowIfCancellationRequested();
                string path = SafePath(root, entry.FullName);
                if (!paths.Add(path) || entry.FullName.StartsWith(".atr-payload-", StringComparison.OrdinalIgnoreCase) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("ATR 含有重複路徑或連結。");
                filesSize = checked(filesSize + entry.Length); if (filesSize > MaxExpanded) throw new InvalidDataException("ATR 檔案總大小異常。");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var source = entry.Open(); using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                CopyBounded(source, target, entry.Length, cancellation);
                progress?.Invoke(0.65 + (++done) * 0.35 / Math.Max(1, zip.Entries.Count));
            }
        }
        catch (CryptographicException ex) { throw new InvalidDataException("ATR 驗證失敗：封裝遭修改或與引擎不相容。", ex); }
        catch (EndOfStreamException ex) { throw new InvalidDataException("ATR 下載不完整。", ex); }
        finally
        {
            if (key != null) CryptographicOperations.ZeroMemory(key);
            ArrayPool<byte>.Shared.Return(encryptedBuffer, true); ArrayPool<byte>.Shared.Return(decodedBuffer, true); ArrayPool<byte>.Shared.Return(expandedBuffer, true);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    static string SafePath(string root, string relative)
    {
        string[] parts = relative.Replace('\\', '/').Split('/');
        if (parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ') || p.Any(c => c < 32 || "<>:\"|?*".Contains(c)) || IsReserved(p))) throw new InvalidDataException("ATR 含有不安全路徑。");
        string path = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ATR 路徑超出安裝資料夾。");
        return path;
    }
    static bool IsReserved(string name)
    {
        string stem = name.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9');
    }
    static int ReadBlock(Stream input, byte[] buffer) { int total = 0, read; while (total < BlockSize && (read = input.Read(buffer, total, BlockSize - total)) > 0) total += read; return total; }
    static byte[] Nonce(int index) { byte[] nonce = new byte[12]; BinaryPrimitives.WriteInt32LittleEndian(nonce.AsSpan(8), index); return nonce; }
    static byte[] Associated(byte[] header, byte[] frame) { byte[] data = new byte[header.Length + frame.Length]; header.CopyTo(data, 0); frame.CopyTo(data, header.Length); return data; }
    static void WriteFrame(Stream output, AesGcm aes, byte[] header, int index, byte codec, int rawSize, ReadOnlySpan<byte> data, byte[] encrypted)
    {
        byte[] frame = new byte[13]; frame[0] = codec; BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), rawSize); BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(5), data.Length); BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(9), index);
        byte[] tag = new byte[16]; aes.Encrypt(Nonce(index), data, encrypted.AsSpan(0, data.Length), tag, Associated(header, frame)); output.Write(frame); output.Write(tag); output.Write(encrypted.AsSpan(0, data.Length));
    }
    static void CopyBounded(Stream input, Stream output, long expected, CancellationToken cancellation)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(131072);
        try
        {
            long total = 0; int read;
            while ((read = input.Read(buffer, 0, 131072)) > 0) { cancellation.ThrowIfCancellationRequested(); total += read; if (total > expected) throw new InvalidDataException("ATR 解壓縮長度超過限制。"); output.Write(buffer, 0, read); }
            if (total != expected) throw new InvalidDataException("ATR 檔案長度不符。");
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, true); }
    }
}
