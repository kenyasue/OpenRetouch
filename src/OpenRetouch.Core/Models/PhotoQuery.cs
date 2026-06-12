namespace OpenRetouch.Core.Models;

/// <summary>写真一覧のソートキー。</summary>
public enum PhotoSortField
{
    CapturedAt,
    ImportedAt,
    FileName,
}

/// <summary>写真一覧のフィルター・ソート条件。デフォルトは「全写真を撮影日時降順」。</summary>
public sealed class PhotoQuery
{
    /// <summary>フォルダ絞り込み(null=全フォルダ)。</summary>
    public string? FolderId { get; set; }

    /// <summary>アルバム絞り込み(null=なし)。</summary>
    public string? AlbumId { get; set; }

    /// <summary>星評価の下限(0=フィルターなし)。</summary>
    public int MinRating { get; set; }

    /// <summary>フラグフィルター(null=なし。Noneを指定すると未設定のみ)。</summary>
    public PhotoFlag? Flag { get; set; }

    /// <summary>色ラベルフィルター(null=なし)。</summary>
    public ColorLabel? ColorLabel { get; set; }

    /// <summary>拡張子フィルター(小文字 ".jpg" 等。null/空=全形式)。</summary>
    public IReadOnlyList<string>? Extensions { get; set; }

    /// <summary>
    /// 同一フォルダに同じベース名のRAWが存在するJPEGを除外する
    /// (RAW+JPG同時記録のペアはRAWのみ表示する)。
    /// </summary>
    public bool ExcludeJpegWithRawPair { get; set; }

    public PhotoSortField SortBy { get; set; } = PhotoSortField.CapturedAt;

    public bool SortDescending { get; set; } = true;
}
