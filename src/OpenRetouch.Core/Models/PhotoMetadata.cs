namespace OpenRetouch.Core.Models;

/// <summary>画像ファイルから読み取ったメタデータ(EXIF+画像サイズ)。</summary>
public sealed class PhotoMetadata
{
    public static PhotoMetadata Empty { get; } = new();

    public DateTimeOffset? CapturedAt { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int Orientation { get; init; } = 1;

    public ExifInfo Exif { get; init; } = new();
}
