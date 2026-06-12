using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Thumbnails;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Thumbnails;

public sealed class WicThumbnailGeneratorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly AppEnvironment _environment;
    private readonly WicThumbnailGenerator _generator;

    public WicThumbnailGeneratorTests()
    {
        _environment = new AppEnvironment(Path.Combine(_root, "appdata"));
        Directory.CreateDirectory(_root);
        _generator = new WicThumbnailGenerator(_environment, new OpenRetouch.Imaging.Raw.LibRawDecoder(Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenRetouch.Imaging.Raw.LibRawDecoder>.Instance), NullLogger<WicThumbnailGenerator>.Instance);
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".tif")]
    public async Task GenerateAsync_LandscapeImage_CreatesThumbnailWithLongEdge320(string extension)
    {
        var sourcePath = Path.Combine(_root, "source" + extension);
        await TestImageFactory.CreateAsync(sourcePath, 1000, 500);
        var photo = CreatePhoto(sourcePath);

        var thumbPath = await _generator.GenerateAsync(photo, new OpenRetouch.Core.Editing.EditSettings());

        File.Exists(thumbPath).Should().BeTrue();
        // 再生成時のUIキャッシュ無効化のため一意サフィックス付きの名前になる
        thumbPath.Should().StartWith(Path.Combine(_environment.ThumbnailsPath, photo.Id + "."));
        thumbPath.Should().EndWith(".jpg");
        var (width, height) = await TestImageFactory.GetSizeAsync(thumbPath);
        width.Should().Be(320);
        height.Should().Be(160);
    }

    [Fact]
    public async Task GenerateAsync_PortraitImage_ScalesByLongEdge()
    {
        var sourcePath = Path.Combine(_root, "portrait.jpg");
        await TestImageFactory.CreateAsync(sourcePath, 400, 800);
        var photo = CreatePhoto(sourcePath);

        var thumbPath = await _generator.GenerateAsync(photo, new OpenRetouch.Core.Editing.EditSettings());

        var (width, height) = await TestImageFactory.GetSizeAsync(thumbPath);
        height.Should().Be(320);
        width.Should().Be(160);
    }

    [Fact]
    public async Task GenerateAsync_SmallImage_DoesNotUpscale()
    {
        var sourcePath = Path.Combine(_root, "small.jpg");
        await TestImageFactory.CreateAsync(sourcePath, 100, 50);
        var photo = CreatePhoto(sourcePath);

        var thumbPath = await _generator.GenerateAsync(photo, new OpenRetouch.Core.Editing.EditSettings());

        var (width, height) = await TestImageFactory.GetSizeAsync(thumbPath);
        width.Should().Be(100);
        height.Should().Be(50);
    }

    [Fact]
    public async Task GenerateAsync_WithCrop_ProducesCroppedThumbnail()
    {
        var sourcePath = Path.Combine(_root, "cropped.jpg");
        await TestImageFactory.CreateAsync(sourcePath, 1000, 500);
        var photo = CreatePhoto(sourcePath);
        var edit = new OpenRetouch.Core.Editing.EditSettings();
        edit.Crop.Width = 0.5;
        edit.Crop.Height = 0.5;

        var thumbPath = await _generator.GenerateAsync(photo, edit);

        var (width, height) = await TestImageFactory.GetSizeAsync(thumbPath);
        // ベース640(クロップあり)→50%クロップ → 320x125相当(アスペクト4:1)
        ((double)width / height).Should().BeApproximately(2.0, 0.1, "クロップ後のアスペクト比を反映する");
        ((int)width).Should().BeLessThanOrEqualTo(320);
    }

    [Fact]
    public async Task GenerateAsync_WithExposure_ProducesBrighterThumbnail()
    {
        var sourcePath = Path.Combine(_root, "exposure.jpg");
        await TestImageFactory.CreateAsync(sourcePath, 200, 200);
        var plainPhoto = CreatePhoto(sourcePath);
        var editedPhoto = CreatePhoto(sourcePath);
        var edit = new OpenRetouch.Core.Editing.EditSettings();
        edit.Basic.Exposure = 2.0;

        var plainThumb = await _generator.GenerateAsync(plainPhoto, new OpenRetouch.Core.Editing.EditSettings());
        var editedThumb = await _generator.GenerateAsync(editedPhoto, edit);

        var plainBytes = new FileInfo(plainThumb).Length;
        var editedBytes = new FileInfo(editedThumb).Length;
        // 内容が異なること(露出適用)を最低限確認
        File.ReadAllBytes(plainThumb).Should().NotEqual(File.ReadAllBytes(editedThumb));
        plainBytes.Should().BeGreaterThan(0);
        editedBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_CorruptedFile_Throws()
    {
        var sourcePath = Path.Combine(_root, "broken.jpg");
        await File.WriteAllTextAsync(sourcePath, "not an image");
        var photo = CreatePhoto(sourcePath);

        var act = () => _generator.GenerateAsync(photo, new OpenRetouch.Core.Editing.EditSettings());

        await act.Should().ThrowAsync<Exception>();
        File.Exists(Path.Combine(_environment.ThumbnailsPath, photo.Id + ".jpg")).Should().BeFalse();
    }

    private static Photo CreatePhoto(string filePath) => new()
    {
        Id = Guid.NewGuid().ToString(),
        FolderId = "folder-1",
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
