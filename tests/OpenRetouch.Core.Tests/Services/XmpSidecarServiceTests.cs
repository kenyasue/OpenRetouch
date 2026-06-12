using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using Xunit;

namespace OpenRetouch.Core.Tests.Services;

public sealed class XmpSidecarServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly XmpSidecarService _service = new(NullLogger<XmpSidecarService>.Instance);

    public XmpSidecarServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void GetSidecarPath_ReplacesExtensionWithXmp()
    {
        _service.GetSidecarPath(@"C:\Photos\IMG_0001.CR3").Should().Be(@"C:\Photos\IMG_0001.xmp");
    }

    [Fact]
    public async Task TryReadAsync_NonRawPhoto_ReturnsNull()
    {
        var photo = CreatePhoto("photo.jpg");
        await File.WriteAllTextAsync(_service.GetSidecarPath(photo.FilePath), "<x/>");

        (await _service.TryReadAsync(photo)).Should().BeNull();
    }

    [Fact]
    public async Task TryReadAsync_NoSidecar_ReturnsNull()
    {
        var photo = CreatePhoto("photo.dng");

        (await _service.TryReadAsync(photo)).Should().BeNull();
    }

    [Fact]
    public async Task TryReadAsync_CorruptedSidecar_ReturnsNull()
    {
        var photo = CreatePhoto("photo.dng");
        await File.WriteAllTextAsync(_service.GetSidecarPath(photo.FilePath), "<broken!!");

        (await _service.TryReadAsync(photo)).Should().BeNull();
    }

    [Fact]
    public async Task WriteThenRead_RoundTrips()
    {
        var photo = CreatePhoto("photo.dng", rating: 4, label: ColorLabel.Green);
        var settings = new EditSettings();
        settings.Basic.Exposure = 1.5;
        settings.Crop.Width = 0.5;
        settings.Crop.Height = 0.5;

        await _service.WriteAsync(photo, settings);
        var data = await _service.TryReadAsync(photo);

        File.Exists(Path.Combine(_root, "photo.xmp")).Should().BeTrue();
        data.Should().NotBeNull();
        data!.Settings.Basic.Exposure.Should().Be(1.5);
        data.Settings.Crop.Width.Should().BeApproximately(0.5, 1e-6);
        data.Rating.Should().Be(4);
        data.ColorLabel.Should().Be(ColorLabel.Green);
    }

    [Fact]
    public async Task WriteAsync_NonRaw_DoesNothing()
    {
        var photo = CreatePhoto("photo.jpg");

        await _service.WriteAsync(photo, new EditSettings());

        File.Exists(Path.Combine(_root, "photo.xmp")).Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_ExistingLightroomSidecar_MergesAndPreservesUnknown()
    {
        var photo = CreatePhoto("photo.dng");
        var sidecarPath = _service.GetSidecarPath(photo.FilePath);
        await File.WriteAllTextAsync(sidecarPath, """
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
             <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description rdf:about=""
                xmlns:crs="http://ns.adobe.com/camera-raw-settings/1.0/"
                crs:Exposure2012="+0.50"
                crs:ToneCurveName2012="Medium Contrast"/>
             </rdf:RDF>
            </x:xmpmeta>
            """);

        var settings = new EditSettings();
        settings.Basic.Exposure = -2.0;
        await _service.WriteAsync(photo, settings);

        var written = await File.ReadAllTextAsync(sidecarPath);
        written.Should().Contain("Medium Contrast", "未知フィールドは保持される");
        written.Should().Contain("-2.00", "管理フィールドは更新される");
    }

    [Fact]
    public async Task WriteAsync_DoesNotLeaveTempFiles()
    {
        var photo = CreatePhoto("photo.dng");

        await _service.WriteAsync(photo, new EditSettings());

        Directory.GetFiles(_root, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task WriteAsync_DoesNotTouchRawFile()
    {
        var photo = CreatePhoto("photo.dng");
        var before = await File.ReadAllBytesAsync(photo.FilePath);

        await _service.WriteAsync(photo, new EditSettings());

        (await File.ReadAllBytesAsync(photo.FilePath)).Should().Equal(before);
    }

    private Photo CreatePhoto(string fileName, int rating = 0, ColorLabel label = ColorLabel.None)
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllText(path, "raw-or-jpeg-content");
        return new Photo
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = "f1",
            FilePath = path,
            FileName = fileName,
            FileExtension = Path.GetExtension(fileName).ToLowerInvariant(),
            ImportedAt = DateTimeOffset.UtcNow,
            Rating = rating,
            ColorLabel = label,
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
