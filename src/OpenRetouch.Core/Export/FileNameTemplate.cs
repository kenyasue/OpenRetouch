using System.Globalization;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Export;

/// <summary>書き出しファイル名テンプレートの展開と衝突解決。</summary>
public static class FileNameTemplate
{
    private static readonly char[] InvalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>テンプレートを展開する(拡張子は含まない)。</summary>
    public static string Expand(string template, Photo photo, int sequence)
    {
        var baseName = Path.GetFileNameWithoutExtension(photo.FileName);
        var date = (photo.CapturedAt ?? photo.ImportedAt)
            .ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var expanded = template
            .Replace("{filename}", baseName, StringComparison.OrdinalIgnoreCase)
            .Replace("{seq}", sequence.ToString("D3", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", date, StringComparison.OrdinalIgnoreCase);

        var sanitized = Sanitize(expanded);
        return sanitized.Length == 0 ? baseName : sanitized;
    }

    /// <summary>ファイル名に使えない文字を'_'へ置換する。</summary>
    public static string Sanitize(string fileName)
    {
        var chars = fileName.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidChars, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars).Trim();
    }

    /// <summary>
    /// 衝突解決済みの出力パスを返す。Skipはnull(書き出さない)、
    /// Renameは "name (2).ext" 形式で空き連番を探す。
    /// </summary>
    public static string? ResolveConflict(string path, ConflictPolicy policy)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        switch (policy)
        {
            case ConflictPolicy.Overwrite:
                return path;
            case ConflictPolicy.Skip:
                return null;
            default:
                var directory = Path.GetDirectoryName(path) ?? "";
                var name = Path.GetFileNameWithoutExtension(path);
                var extension = Path.GetExtension(path);
                for (var i = 2; i < 10000; i++)
                {
                    var candidate = Path.Combine(directory, $"{name} ({i}){extension}");
                    if (!File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                throw new IOException($"Could not resolve file name conflict for: {path}");
        }
    }
}
