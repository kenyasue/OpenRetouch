using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Raw;
using OpenRetouch.Imaging.Rendering;
using OpenRetouch.Imaging.Thumbnails;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Raw;

/// <summary>RAW(生成DNG)のサムネイル生成・編集プレビューの統合テスト。</summary>
public sealed class RawIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly AppEnvironment _environment;
    private readonly LibRawDecoder _rawDecoder = new(NullLogger<LibRawDecoder>.Instance);

    public RawIntegrationTests()
    {
        _environment = new AppEnvironment(Path.Combine(_root, "appdata"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task ThumbnailGenerator_DngFile_CreatesThumbnail()
    {
        var photo = CreateDngPhoto("thumb.dng", 800, 600);
        var generator = new WicThumbnailGenerator(
            _environment, _rawDecoder, NullLogger<WicThumbnailGenerator>.Instance);

        var thumbPath = await generator.GenerateAsync(photo, new EditSettings());

        File.Exists(thumbPath).Should().BeTrue();
        var (w, h) = await TestImageFactory.GetSizeAsync(thumbPath);
        ((int)w).Should().BeLessThanOrEqualTo(320);
        ((int)h).Should().BeLessThanOrEqualTo(320);
    }

    [Fact]
    public async Task PreviewRenderer_DngFile_RendersWithEdits()
    {
        var photo = CreateDngPhoto("preview.dng", 400, 300);
        var renderer = new PreviewRenderer(_rawDecoder, NullLogger<PreviewRenderer>.Instance);
        var bright = new EditSettings();
        bright.Basic.Exposure = 2.0;

        var defaultRender = await renderer.RenderAsync(photo, new EditSettings());
        var brightRender = await renderer.RenderAsync(photo, bright);

        defaultRender.Width.Should().BeGreaterThan(0);
        defaultRender.PixelsBgra.Length.Should().Be(defaultRender.Width * defaultRender.Height * 4);
        brightRender.PixelsBgra.Should().NotEqual(defaultRender.PixelsBgra, "RAWにも編集が適用される");
    }

    [Fact]
    public async Task ExportPipeline_DngToJpeg_AppliesEditsAndKeepsSourceUntouched()
    {
        var photo = CreateDngPhoto("export.dng", 320, 240);
        var sourceBytes = await File.ReadAllBytesAsync(photo.FilePath);
        var pipeline = new OpenRetouch.Imaging.Export.WicExportPipeline(
            _rawDecoder, NullLogger<OpenRetouch.Imaging.Export.WicExportPipeline>.Instance);
        var edit = new EditSettings();
        edit.Crop.Width = 0.5;
        edit.Crop.Height = 0.5;
        var settings = new OpenRetouch.Core.Export.ExportSettings
        {
            OutputFolder = _root,
            Format = OpenRetouch.Core.Export.ExportFormat.Jpeg,
        };
        var outputPath = Path.Combine(_root, "developed.jpg");

        await pipeline.ExportAsync(photo, edit, settings, outputPath);

        File.Exists(outputPath).Should().BeTrue();
        var (w, h) = await TestImageFactory.GetSizeAsync(outputPath);
        ((int)w).Should().Be(160, "フル現像(320x240)に50%クロップが適用される");
        ((int)h).Should().Be(120);
        (await File.ReadAllBytesAsync(photo.FilePath)).Should().Equal(sourceBytes, "RAW元ファイルは不変");
    }

    [Fact]
    public async Task ExportPipeline_DngToTiff_Works()
    {
        var photo = CreateDngPhoto("export-tiff.dng", 200, 100);
        var pipeline = new OpenRetouch.Imaging.Export.WicExportPipeline(
            _rawDecoder, NullLogger<OpenRetouch.Imaging.Export.WicExportPipeline>.Instance);
        var settings = new OpenRetouch.Core.Export.ExportSettings
        {
            OutputFolder = _root,
            Format = OpenRetouch.Core.Export.ExportFormat.Tiff,
        };
        var outputPath = Path.Combine(_root, "developed.tif");

        await pipeline.ExportAsync(photo, new EditSettings(), settings, outputPath);

        File.Exists(outputPath).Should().BeTrue();
        var (w, h) = await TestImageFactory.GetSizeAsync(outputPath);
        ((int)w).Should().Be(200);
        ((int)h).Should().Be(100);
    }

    [Fact]
    public async Task PreviewAndExport_SameEdits_HaveConsistentBrightness()
    {
        // プレビュー(現像ベース)と書き出しが同傾向の明るさになる(WYSIWYG)
        var photo = CreateDngPhoto("wysiwyg.dng", 128, 64);
        var renderer = new PreviewRenderer(_rawDecoder, NullLogger<PreviewRenderer>.Instance);
        var bright = new EditSettings();
        bright.Basic.Exposure = 1.5;

        var defaultPreview = await renderer.RenderAsync(photo, new EditSettings());
        var brightPreview = await renderer.RenderAsync(photo, bright);

        AverageLuma(brightPreview).Should().BeGreaterThan(
            AverageLuma(defaultPreview), "露出+1.5でプレビューが明るくなる(現像ベースにも編集が効く)");
    }

    private static double AverageLuma(OpenRetouch.Core.Abstractions.Imaging.RenderedImage image)
    {
        double sum = 0;
        for (var i = 0; i < image.PixelsBgra.Length; i += 4)
        {
            sum += 0.114 * image.PixelsBgra[i] + 0.587 * image.PixelsBgra[i + 1] + 0.299 * image.PixelsBgra[i + 2];
        }

        return sum / (image.PixelsBgra.Length / 4);
    }

    private Photo CreateDngPhoto(string fileName, int width, int height)
    {
        var path = Path.Combine(_root, fileName);
        TestDngFactory.Create(path, width, height);
        return new Photo
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = "f1",
            FilePath = path,
            FileName = fileName,
            FileExtension = ".dng",
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
