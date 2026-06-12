namespace OpenRetouch.Core.Export;

public enum ExportFormat
{
    Jpeg,
    Png,
    Tiff,
}

public enum ResizeMode
{
    None,
    LongEdge,
    ShortEdge,
}

/// <summary>出力ファイル名衝突時の動作。</summary>
public enum ConflictPolicy
{
    /// <summary>"name (2).ext" 形式で連番リネーム。</summary>
    Rename,
    Overwrite,
    Skip,
}

/// <summary>書き出し時のメタデータポリシー。</summary>
public sealed class MetadataPolicy
{
    /// <summary>基本EXIF(撮影日時・カメラ・露出情報)を書き込む。</summary>
    public bool KeepExif { get; set; } = true;

    /// <summary>KeepExif時もGPS情報は書き込まない(プライバシー既定ON)。</summary>
    public bool RemoveGps { get; set; } = true;
}

/// <summary>書き出し設定。カラープロファイルはMVPではsRGB固定。</summary>
public sealed class ExportSettings
{
    public required string OutputFolder { get; set; }

    /// <summary>トークン: {filename}=元名 {seq}=連番(001..) {date}=撮影日yyyyMMdd。</summary>
    public string FileNameTemplate { get; set; } = "{filename}";

    public ExportFormat Format { get; set; } = ExportFormat.Jpeg;

    public int JpegQuality { get; set; } = 90;

    public ResizeMode ResizeMode { get; set; }

    /// <summary>リサイズ基準サイズ(px)。ResizeMode=Noneのとき無視。</summary>
    public int? ResizeValue { get; set; }

    public MetadataPolicy Metadata { get; set; } = new();

    public ConflictPolicy Conflict { get; set; } = ConflictPolicy.Rename;

    public ExportSettings Clone() => new()
    {
        OutputFolder = OutputFolder,
        FileNameTemplate = FileNameTemplate,
        Format = Format,
        JpegQuality = JpegQuality,
        ResizeMode = ResizeMode,
        ResizeValue = ResizeValue,
        Metadata = new MetadataPolicy
        {
            KeepExif = Metadata.KeepExif,
            RemoveGps = Metadata.RemoveGps,
        },
        Conflict = Conflict,
    };

    /// <summary>出力ファイルの拡張子(小文字、ドット付き)。</summary>
    public string FileExtension => Format switch
    {
        ExportFormat.Png => ".png",
        ExportFormat.Tiff => ".tif",
        _ => ".jpg",
    };
}
