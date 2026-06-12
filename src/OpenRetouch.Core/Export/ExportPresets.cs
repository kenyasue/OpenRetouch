namespace OpenRetouch.Core.Export;

/// <summary>組み込みの書き出しプリセット。OutputFolderはUIで上書きされる前提のプレースホルダ。</summary>
public static class ExportPresets
{
    public static IReadOnlyList<(string Name, ExportSettings Settings)> BuiltIn { get; } =
    [
        ("Original Size JPEG", new ExportSettings
        {
            OutputFolder = "",
            Format = ExportFormat.Jpeg,
            JpegQuality = 92,
            ResizeMode = ResizeMode.None,
        }),
        ("Web Optimized JPEG", new ExportSettings
        {
            OutputFolder = "",
            Format = ExportFormat.Jpeg,
            JpegQuality = 80,
            ResizeMode = ResizeMode.LongEdge,
            ResizeValue = 2048,
        }),
        ("Instagram Square", new ExportSettings
        {
            OutputFolder = "",
            Format = ExportFormat.Jpeg,
            JpegQuality = 90,
            ResizeMode = ResizeMode.LongEdge,
            ResizeValue = 1080,
        }),
        ("Instagram 4:5", new ExportSettings
        {
            OutputFolder = "",
            Format = ExportFormat.Jpeg,
            JpegQuality = 90,
            ResizeMode = ResizeMode.LongEdge,
            ResizeValue = 1350,
        }),
        ("YouTube Thumbnail", new ExportSettings
        {
            OutputFolder = "",
            Format = ExportFormat.Jpeg,
            JpegQuality = 90,
            ResizeMode = ResizeMode.LongEdge,
            ResizeValue = 1280,
        }),
        ("EC Product Image", new ExportSettings
        {
            OutputFolder = "",
            Format = ExportFormat.Jpeg,
            JpegQuality = 85,
            ResizeMode = ResizeMode.LongEdge,
            ResizeValue = 2000,
        }),
        ("Print TIFF", new ExportSettings
        {
            OutputFolder = "",
            Format = ExportFormat.Tiff,
            ResizeMode = ResizeMode.None,
            Metadata = new MetadataPolicy { KeepExif = true, RemoveGps = true },
        }),
    ];
}
