using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Export;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Export;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Export;

public sealed class WicExportPipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _outDir;
    private readonly WicExportPipeline _pipeline = new(
        new OpenRetouch.Imaging.Raw.LibRawDecoder(
            NullLogger<OpenRetouch.Imaging.Raw.LibRawDecoder>.Instance),
        NullLogger<WicExportPipeline>.Instance);

    public WicExportPipelineTests()
    {
        _outDir = Path.Combine(_root, "out");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outDir);
    }

    [Theory]
    [InlineData(ExportFormat.Jpeg, ".jpg")]
    [InlineData(ExportFormat.Png, ".png")]
    [InlineData(ExportFormat.Tiff, ".tif")]
    public async Task ExportAsync_EachFormat_CreatesDecodableFile(ExportFormat format, string extension)
    {
        var photo = await CreatePhotoAsync("source.jpg", 400, 300);
        var outputPath = Path.Combine(_outDir, "result" + extension);

        await _pipeline.ExportAsync(photo, new EditSettings(), CreateSettings(format), outputPath);

        File.Exists(outputPath).Should().BeTrue();
        var (w, h) = await TestImageFactory.GetSizeAsync(outputPath);
        w.Should().Be(400);
        h.Should().Be(300);
    }

    [Fact]
    public async Task ExportAsync_LongEdgeResize_Scales()
    {
        var photo = await CreatePhotoAsync("large.jpg", 1600, 800);
        var settings = CreateSettings(ExportFormat.Jpeg);
        settings.ResizeMode = ResizeMode.LongEdge;
        settings.ResizeValue = 800;
        var outputPath = Path.Combine(_outDir, "resized.jpg");

        await _pipeline.ExportAsync(photo, new EditSettings(), settings, outputPath);

        var (w, h) = await TestImageFactory.GetSizeAsync(outputPath);
        w.Should().Be(800);
        h.Should().Be(400);
    }

    [Fact]
    public async Task ExportAsync_ShortEdgeResize_Scales()
    {
        var photo = await CreatePhotoAsync("large2.jpg", 1600, 800);
        var settings = CreateSettings(ExportFormat.Jpeg);
        settings.ResizeMode = ResizeMode.ShortEdge;
        settings.ResizeValue = 400;
        var outputPath = Path.Combine(_outDir, "resized2.jpg");

        await _pipeline.ExportAsync(photo, new EditSettings(), settings, outputPath);

        var (w, h) = await TestImageFactory.GetSizeAsync(outputPath);
        w.Should().Be(800);
        h.Should().Be(400);
    }

    [Fact]
    public async Task ExportAsync_WithCrop_OutputsCroppedDimensions()
    {
        var photo = await CreatePhotoAsync("crop.jpg", 800, 600);
        var edit = new EditSettings();
        edit.Crop.Width = 0.5;
        edit.Crop.Height = 0.5;
        var outputPath = Path.Combine(_outDir, "cropped.jpg");

        await _pipeline.ExportAsync(photo, edit, CreateSettings(ExportFormat.Jpeg), outputPath);

        var (w, h) = await TestImageFactory.GetSizeAsync(outputPath);
        w.Should().Be(400);
        h.Should().Be(300);
    }

    [Fact]
    public async Task ExportAsync_SamePathAsOriginal_Throws()
    {
        var photo = await CreatePhotoAsync("protect.jpg", 100, 100);

        var act = () => _pipeline.ExportAsync(
            photo, new EditSettings(), CreateSettings(ExportFormat.Jpeg), photo.FilePath);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // 元ファイルが無傷であること
        var (w, h) = await TestImageFactory.GetSizeAsync(photo.FilePath);
        w.Should().Be(100);
    }

    [Fact]
    public void CalculateOutputSize_NoUpscale()
    {
        var settings = CreateSettings(ExportFormat.Jpeg);
        settings.ResizeMode = ResizeMode.LongEdge;
        settings.ResizeValue = 5000;

        WicExportPipeline.CalculateOutputSize(800, 600, settings).Should().Be((800, 600));
    }

    private ExportSettings CreateSettings(ExportFormat format) => new()
    {
        OutputFolder = _outDir,
        Format = format,
    };

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
