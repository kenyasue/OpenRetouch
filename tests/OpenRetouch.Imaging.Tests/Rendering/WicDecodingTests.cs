using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Raw;
using OpenRetouch.Imaging.Rendering;
using OpenRetouch.Imaging.Thumbnails;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Rendering;

/// <summary>
/// EXIF Orientation付き画像のデコード回帰テスト。
/// WICのBitmapTransformはOrientation適用前の座標系でスケールを指定する仕様のため、
/// 90/270度回転画像で縦横を入れ替えないと画像が崩壊する(縦位置RAW/JPEG崩壊バグの回帰防止)。
/// </summary>
public sealed class WicDecodingTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly AppEnvironment _environment;

    public WicDecodingTests()
    {
        _environment = new AppEnvironment(Path.Combine(_root, "appdata"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Thumbnail_Orientation6Jpeg_ProducesUprightUncorruptedImage()
    {
        // 物理400x200(上=赤/下=青)、Orientation=6(表示時90度CW回転)→ 表示は200x400縦
        var sourcePath = Path.Combine(_root, "rotated.jpg");
        await TestImageFactory.CreateOrientedJpegAsync(sourcePath, 400, 200, orientation: 6);
        var generator = new WicThumbnailGenerator(
            _environment,
            new LibRawDecoder(NullLogger<LibRawDecoder>.Instance),
            NullLogger<WicThumbnailGenerator>.Instance);
        var photo = CreatePhoto(sourcePath);

        var thumbPath = await generator.GenerateAsync(photo, new EditSettings());

        var (width, height) = await TestImageFactory.GetSizeAsync(thumbPath);
        // 縦長(回転適用済み)で長辺320以下
        ((int)height).Should().BeGreaterThan((int)width, "Orientation=6は縦長として出力される");
        ((int)height).Should().BeLessThanOrEqualTo(320);

        // ピクセル検証: 90度CW回転後は「左=青、右=赤」になる(崩壊していれば縞模様になり一致しない)
        var pixels = await DecodePixelsAsync(thumbPath);
        var (w, h) = ((int)width, (int)height);
        GetRed(pixels, w, x: w - 2, y: h / 2).Should().BeGreaterThan(180, "右側は赤");
        GetBlue(pixels, w, x: w - 2, y: h / 2).Should().BeLessThan(80);
        GetBlue(pixels, w, x: 1, y: h / 2).Should().BeGreaterThan(180, "左側は青");
        GetRed(pixels, w, x: 1, y: h / 2).Should().BeLessThan(80);
    }

    [Fact]
    public async Task PreviewRenderer_Orientation6Jpeg_KeepsPortraitAspect()
    {
        var sourcePath = Path.Combine(_root, "rotated-large.jpg");
        await TestImageFactory.CreateOrientedJpegAsync(sourcePath, 2000, 1000, orientation: 6);
        var renderer = new PreviewRenderer(
            new LibRawDecoder(NullLogger<LibRawDecoder>.Instance),
            NullLogger<PreviewRenderer>.Instance);
        var photo = CreatePhoto(sourcePath);

        var result = await renderer.RenderAsync(photo, new EditSettings());

        // 表示寸法1000x2000 → 長辺1280に縮小 → 640x1280
        result.Width.Should().Be(640);
        result.Height.Should().Be(1280);
    }

    private static byte GetRed(byte[] bgra, int width, int x, int y) => bgra[(y * width + x) * 4 + 2];

    private static byte GetBlue(byte[] bgra, int width, int x, int y) => bgra[(y * width + x) * 4];

    private static async Task<byte[]> DecodePixelsAsync(string path)
    {
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        return (await decoder.GetPixelDataAsync()).DetachPixelData();
    }

    private static Photo CreatePhoto(string filePath) => new()
    {
        Id = Guid.NewGuid().ToString(),
        FolderId = "f1",
        FilePath = filePath,
        FileName = Path.GetFileName(filePath),
        FileExtension = Path.GetExtension(filePath).ToLowerInvariant(),
        ImportedAt = DateTimeOffset.UtcNow,
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
