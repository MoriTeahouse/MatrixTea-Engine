using System.Buffers.Binary;
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
        foreach (var file in files)
        {
            SafePath(source, Path.GetRelativePath(source, file).Replace('\\', '/'));
            for (var node = new FileInfo(file) as FileSystemInfo; node != null && node.FullName != source; node = new DirectoryInfo(Path.GetDirectoryName(node.FullName)!))
                if (node.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("ATR 不接受檔案連結。");
            total = checked(total + new FileInfo(file).Length);
            if (total > MaxExpanded - 32 * 1024 * 1024) throw new InvalidDataException("封裝超過大小限制。");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        string temporary = archivePath + "." + Guid.NewGuid().ToString("N") + ".payload";
        string partial = archivePath + "." + Guid.NewGuid().ToString("N") + ".partial";
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
            byte[] key = HMACSHA256.HashData(FormatKey, header.AsSpan(16, 16));
            using var aes = new AesGcm(key, 16);
            using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                target.Write(header);
                byte[] buffer = new byte[BlockSize]; int index = 0; int read;
                while ((read = ReadBlock(payload, buffer)) > 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    byte[] raw = buffer.AsSpan(0, read).ToArray(); byte codec = 0;
                    byte[] best = raw;
                    foreach (byte candidate in new byte[] { 1, 2 })
                    {
                        byte[] compressed = Compress(raw, candidate);
                        if (compressed.Length < best.Length) { best = compressed; codec = candidate; }
                    }
                    WriteFrame(target, aes, header, index++, codec, read, best);
                }
                WriteFrame(target, aes, header, index, 255, 0, Array.Empty<byte>());
                target.Flush(true);
            }
            File.Move(partial, archivePath, true);
            CryptographicOperations.ZeroMemory(key);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); if (File.Exists(partial)) File.Delete(partial); }
    }

    public static void Extract(string archivePath, string directory, Action<double>? progress = null, CancellationToken cancellation = default)
    {
        string root = Path.GetFullPath(directory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("ATR 解封裝需要空資料夾。");
        for (var dir = new DirectoryInfo(root); dir != null; dir = dir.Parent)
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("ATR 解封裝目錄不可使用連結。");
        Directory.CreateDirectory(root);
        string temporary = Path.Combine(root, ".atr-payload-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var input = File.OpenRead(archivePath))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] header = new byte[40]; input.ReadExactly(header);
                if (!header.AsSpan(0, 16).SequenceEqual(Magic)) throw new InvalidDataException("不是相容的 MatrixTea ATR1 封裝。");
                long total = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(32));
                if (total <= 0 || total > MaxExpanded) throw new InvalidDataException("ATR 封裝大小異常。");
                byte[] key = HMACSHA256.HashData(FormatKey, header.AsSpan(16, 16));
                using var aes = new AesGcm(key, 16); long expanded = 0; int index = 0;
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    byte[] frame = new byte[13]; input.ReadExactly(frame);
                    byte codec = frame[0]; int rawSize = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1));
                    int encodedSize = BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(5));
                    if (BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(9)) != index || rawSize < 0 || rawSize > BlockSize || encodedSize < 0 || encodedSize > BlockSize || (codec != 255 && (rawSize == 0 || codec > 2)))
                        throw new InvalidDataException("ATR 區塊格式異常。");
                    byte[] tag = new byte[16]; input.ReadExactly(tag);
                    byte[] encrypted = new byte[encodedSize]; input.ReadExactly(encrypted); byte[] decoded = new byte[encodedSize];
                    aes.Decrypt(Nonce(index), encrypted, tag, decoded, Associated(header, frame));
                    index++;
                    if (codec == 255)
                    {
                        if (rawSize != 0 || encodedSize != 0 || expanded != total || input.Position != input.Length) throw new InvalidDataException("ATR 封裝未完整結束。");
                        break;
                    }
                    expanded = checked(expanded + rawSize);
                    if (expanded > total) throw new InvalidDataException("ATR 解壓縮超過預期大小。");
                    using var compressed = new MemoryStream(decoded);
                    using Stream raw = codec switch { 1 => new BrotliStream(compressed, CompressionMode.Decompress), 2 => new DeflateStream(compressed, CompressionMode.Decompress), _ => compressed };
                    CopyBounded(raw, output, rawSize, cancellation);
                    progress?.Invoke(expanded * 0.65 / total);
                }
                CryptographicOperations.ZeroMemory(key);
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
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
    static int ReadBlock(Stream input, byte[] buffer) { int total = 0, read; while (total < buffer.Length && (read = input.Read(buffer, total, buffer.Length - total)) > 0) total += read; return total; }
    static byte[] Compress(byte[] raw, byte codec)
    {
        using var target = new MemoryStream();
        using (Stream compressor = codec == 1 ? new BrotliStream(target, CompressionLevel.Optimal, true) : new DeflateStream(target, CompressionLevel.Optimal, true)) compressor.Write(raw);
        return target.ToArray();
    }
    static byte[] Nonce(int index) { byte[] nonce = new byte[12]; BinaryPrimitives.WriteInt32LittleEndian(nonce.AsSpan(8), index); return nonce; }
    static byte[] Associated(byte[] header, byte[] frame) { byte[] data = new byte[header.Length + frame.Length]; header.CopyTo(data, 0); frame.CopyTo(data, header.Length); return data; }
    static void WriteFrame(Stream output, AesGcm aes, byte[] header, int index, byte codec, int rawSize, byte[] data)
    {
        byte[] frame = new byte[13]; frame[0] = codec; BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1), rawSize); BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(5), data.Length); BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(9), index);
        byte[] encrypted = new byte[data.Length], tag = new byte[16]; aes.Encrypt(Nonce(index), data, encrypted, tag, Associated(header, frame)); output.Write(frame); output.Write(tag); output.Write(encrypted);
    }
    static void CopyBounded(Stream input, Stream output, long expected, CancellationToken cancellation)
    {
        byte[] buffer = new byte[131072]; long total = 0; int read;
        while ((read = input.Read(buffer)) > 0) { cancellation.ThrowIfCancellationRequested(); total += read; if (total > expected) throw new InvalidDataException("ATR 解壓縮長度超過限制。"); output.Write(buffer, 0, read); }
        if (total != expected) throw new InvalidDataException("ATR 檔案長度不符。");
    }
}
