using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Imaging.Metadata;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Metadata;

public sealed class PhotoMetadataReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly PhotoMetadataReader _reader = new(NullLogger<PhotoMetadataReader>.Instance);

    public PhotoMetadataReaderTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData(".jpg", 640, 480)]
    [InlineData(".png", 320, 240)]
    public async Task Read_GeneratedImage_ReturnsDimensions(string extension, int width, int height)
    {
        var path = Path.Combine(_root, "image" + extension);
        await TestImageFactory.CreateAsync(path, width, height);

        var metadata = _reader.Read(path);

        metadata.Width.Should().Be(width);
        metadata.Height.Should().Be(height);
    }

    [Fact]
    public async Task Read_ImageWithoutExif_ReturnsEmptyExifAndDefaultOrientation()
    {
        var path = Path.Combine(_root, "noexif.png");
        await TestImageFactory.CreateAsync(path, 100, 100);

        var metadata = _reader.Read(path);

        metadata.CapturedAt.Should().BeNull();
        metadata.Orientation.Should().Be(1);
        metadata.Exif.CameraMake.Should().BeNull();
        metadata.Exif.Iso.Should().BeNull();
    }

    [Fact]
    public async Task Read_CorruptedFile_ReturnsEmptyMetadata()
    {
        var path = Path.Combine(_root, "broken.jpg");
        await File.WriteAllTextAsync(path, "not an image at all");

        var metadata = _reader.Read(path);

        metadata.Width.Should().Be(0);
        metadata.Height.Should().Be(0);
        metadata.CapturedAt.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
