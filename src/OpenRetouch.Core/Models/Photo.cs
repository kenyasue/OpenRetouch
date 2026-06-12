namespace OpenRetouch.Core.Models;

/// <summary>カタログ内の写真1枚を表す中心エンティティ(photosテーブルに対応)。</summary>
public sealed class Photo
{
    public required string Id { get; init; }

    public required string FolderId { get; init; }

    /// <summary>元画像の絶対パス。重複検出キー(UNIQUE)。</summary>
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    /// <summary>小文字正規化済みの拡張子(例: ".jpg")。</summary>
    public required string FileExtension { get; init; }

    public long FileSize { get; init; }

    public DateTimeOffset ImportedAt { get; init; }

    public DateTimeOffset? CapturedAt { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>EXIF Orientation(1-8、既定1)。</summary>
    public int Orientation { get; set; } = 1;

    public ExifInfo Exif { get; set; } = new();

    /// <summary>星評価(0-5)。</summary>
    public int Rating { get; set; }

    public PhotoFlag Flag { get; set; }

    public ColorLabel ColorLabel { get; set; }

    /// <summary>元ファイルが見つからない状態。</summary>
    public bool IsMissing { get; set; }
}
