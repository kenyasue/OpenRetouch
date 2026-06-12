namespace OpenRetouch.Core.Models;

/// <summary>RAWファイル形式の判定。</summary>
public static class RawFileTypes
{
    /// <summary>対応するRAW拡張子(小文字、ドット付き)。</summary>
    public static readonly IReadOnlySet<string> Extensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cr2", ".cr3", ".nef", ".arw", ".raf", ".orf", ".rw2", ".dng",
        };

    public static bool IsRaw(string extension) => Extensions.Contains(extension);
}
