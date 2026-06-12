using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Raw;
using OpenRetouch.Imaging.Rendering;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Rendering;

public sealed class AutoToneServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    public AutoToneServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void BuildLuminanceHistogram_CountsAllPixels()
    {
        // 2ピクセル: 黒(0,0,0)と白(255,255,255)
        byte[] pixels = [0, 0, 0, 255, 255, 255, 255, 255];

        var histogram = AutoToneService.BuildLuminanceHistogram(pixels);

        histogram.Sum().Should().Be(2);
        histogram[0].Should().Be(1, "黒は輝度0");
        histogram[255].Should().Be(1, "白は輝度255");
    }

    [Fact]
    public void BuildLuminanceHistogram_GreenWeighsMostInLuminance()
    {
        byte[] greenPixel = [0, 255, 0, 255];  // BGRA
        byte[] bluePixel = [255, 0, 0, 255];

        var greenLuma = Array.FindIndex(
            AutoToneService.BuildLuminanceHistogram(greenPixel), c => c > 0);
        var blueLuma = Array.FindIndex(
            AutoToneService.BuildLuminanceHistogram(bluePixel), c => c > 0);

        greenLuma.Should().BeGreaterThan(blueLuma, "Rec.601では緑の寄与が最大");
    }

    [Fact]
    public async Task ComputeAsync_DarkJpeg_ReturnsPositiveExposure()
    {
        var path = Path.Combine(_root, "dark.jpg");
        await CreateGrayJpegAsync(path, 400, 300, gray: 0x28); // 暗いグレー(ノイズ付き)
        var service = CreateService();

        var result = await service.ComputeAsync(CreatePhoto(path));

        result.Exposure.Should().BeGreaterThan(0.3, "暗い画像は露出をプラス補正する");
    }

    [Fact]
    public async Task ComputeAsync_BrightJpeg_ReturnsNegativeExposure()
    {
        var path = Path.Combine(_root, "bright.jpg");
        await CreateGrayJpegAsync(path, 400, 300, gray: 0xDC);
        var service = CreateService();

        var result = await service.ComputeAsync(CreatePhoto(path));

        result.Exposure.Should().BeLessThan(-0.3, "明るい画像は露出をマイナス補正する");
    }

    [Fact]
    public async Task ComputeAsync_UndecodableFile_Throws()
    {
        var path = Path.Combine(_root, "broken.jpg");
        await File.WriteAllTextAsync(path, "not an image");
        var service = CreateService();

        var act = () => service.ComputeAsync(CreatePhoto(path));

        await act.Should().ThrowAsync<Exception>("解析失敗は呼び出し側で処理する");
    }

    [Fact]
    public async Task ComputeAsync_DoesNotTouchColorParameters()
    {
        var path = Path.Combine(_root, "any.jpg");
        await CreateGrayJpegAsync(path, 200, 200, gray: 0x40);
        var service = CreateService();

        var result = await service.ComputeAsync(CreatePhoto(path));

        result.Temperature.Should().Be(0);
        result.Saturation.Should().Be(0);
        result.Sharpening.Should().Be(0);
    }

    private static AutoToneService CreateService() => new(
        new PreviewRenderer(
            new LibRawDecoder(NullLogger<LibRawDecoder>.Instance),
            NullLogger<PreviewRenderer>.Instance),
        NullLogger<AutoToneService>.Instance);

    /// <summary>指定グレー基調+輝度バリエーション(縞)を持つJPEGを生成する。</summary>
    private static async Task CreateGrayJpegAsync(string path, int width, int height, byte gray)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // 単色だと補正なし扱いになるため、行ごとに±20の縞を付ける
                var value = (byte)Math.Clamp(gray + (y % 3 - 1) * 20, 0, 255);
                var offset = (y * width + x) * 4;
                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 0xFF;
            }
        }

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();

        await using var fileStream = File.Create(path);
        await stream.AsStreamForRead().CopyToAsync(fileStream);
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
