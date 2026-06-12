using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Rendering;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Rendering;

public sealed class PreviewRendererTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly PreviewRenderer _renderer = new(new OpenRetouch.Imaging.Raw.LibRawDecoder(Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenRetouch.Imaging.Raw.LibRawDecoder>.Instance), NullLogger<PreviewRenderer>.Instance);

    public PreviewRendererTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task RenderAsync_LargeImage_ScalesToDraftLongEdge()
    {
        var photo = await CreatePhotoAsync("large.jpg", 2560, 1280);

        var result = await _renderer.RenderAsync(photo, new EditSettings());

        result.Width.Should().Be(1280);
        result.Height.Should().Be(640);
        result.PixelsBgra.Length.Should().Be(1280 * 640 * 4);
    }

    [Fact]
    public async Task RenderAsync_SmallImage_DoesNotUpscale()
    {
        var photo = await CreatePhotoAsync("small.png", 200, 100);

        var result = await _renderer.RenderAsync(photo, new EditSettings());

        result.Width.Should().Be(200);
        result.Height.Should().Be(100);
    }

    [Fact]
    public async Task RenderAsync_WithExposure_DiffersFromDefault()
    {
        var photo = await CreatePhotoAsync("photo.jpg", 400, 300);
        var bright = new EditSettings();
        bright.Basic.Exposure = 2.0;

        var defaultRender = await _renderer.RenderAsync(photo, new EditSettings());
        var brightRender = await _renderer.RenderAsync(photo, bright);

        brightRender.PixelsBgra.Should().NotEqual(defaultRender.PixelsBgra);
    }

    [Fact]
    public async Task RenderAsync_SamePhotoTwice_UsesCachedBase()
    {
        var photo = await CreatePhotoAsync("cached.jpg", 800, 600);

        var first = await _renderer.RenderAsync(photo, new EditSettings());

        // 元ファイルを削除してもキャッシュからレンダリングできる=2回目はデコードしていない
        File.Delete(photo.FilePath);
        var second = await _renderer.RenderAsync(photo, new EditSettings());

        second.PixelsBgra.Should().Equal(first.PixelsBgra);
    }

    [Fact]
    public async Task RenderAsync_WithCropApplied_ChangesDimensions()
    {
        var photo = await CreatePhotoAsync("crop.jpg", 400, 200);
        var settings = new EditSettings();
        settings.Crop.Width = 0.5;
        settings.Crop.Height = 0.5;

        var cropped = await _renderer.RenderAsync(photo, settings, applyCrop: true);
        var uncropped = await _renderer.RenderAsync(photo, settings, applyCrop: false);

        cropped.Width.Should().Be(200);
        cropped.Height.Should().Be(100);
        uncropped.Width.Should().Be(400, "applyCrop=false ではクロップを適用しない(クロップ編集UI用)");
        uncropped.Height.Should().Be(200);
    }

    [Fact]
    public async Task RenderAsync_DoesNotMutateCachedBase()
    {
        var photo = await CreatePhotoAsync("mutate.jpg", 400, 300);
        var edited = new EditSettings();
        edited.Basic.Exposure = 3.0;

        var before = await _renderer.RenderAsync(photo, new EditSettings());
        await _renderer.RenderAsync(photo, edited);
        var after = await _renderer.RenderAsync(photo, new EditSettings());

        after.PixelsBgra.Should().Equal(before.PixelsBgra, "編集適用がキャッシュ済みベースを破壊しない");
    }

    private async Task<Photo> CreatePhotoAsync(string fileName, int width, int height)
    {
        var path = Path.Combine(_root, fileName);
        await TestImageFactory.CreateAsync(path, width, height);
        return new Photo
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = "f1",
            FilePath = path,
            FileName = fileName,
            FileExtension = Path.GetExtension(fileName).ToLowerInvariant(),
            ImportedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
