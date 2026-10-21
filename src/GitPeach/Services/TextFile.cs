using System.Text;

namespace GitPeach.Services;

/// <summary>A text file read from disk, remembering its encoding (and BOM) so saving writes it back the same way.</summary>
public sealed record TextFile(string Text, Encoding Encoding)
{
    /// <summary>Larger files are not opened in the built-in viewer.</summary>
    public const long MaxBytes = 20 * 1024 * 1024;

    /// <summary>Reads <paramref name="path"/>; null when it is binary (a NUL byte in the first 8 KB) or too large.</summary>
    public static async Task<TextFile?> ReadAsync(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxBytes) return null;
        var bytes = await File.ReadAllBytesAsync(path);
        if (IsBinary(bytes)) return null;
        using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        return new TextFile(text, reader.CurrentEncoding);
    }

    public static bool IsBinary(ReadOnlySpan<byte> bytes)
    {
        // UTF-16 files legitimately contain NULs; they announce themselves with a BOM.
        if (bytes.Length >= 2 && (bytes[0] == 0xFF && bytes[1] == 0xFE || bytes[0] == 0xFE && bytes[1] == 0xFF)) return false;
        return bytes[..Math.Min(bytes.Length, 8192)].IndexOf((byte)0) >= 0;
    }

    public Task WriteAsync(string path, string text) => File.WriteAllTextAsync(path, text, Encoding);
}
