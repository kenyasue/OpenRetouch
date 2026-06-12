using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Imaging.Raw;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Raw;

public sealed class LibRawDecoderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly LibRawDecoder _decoder = new(NullLogger<LibRawDecoder>.Instance);

    public LibRawDecoderTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task DecodeDevelopedAsync_LinearDng_ReturnsBgraImage()
    {
        var path = Path.Combine(_root, "test.dng");
        TestDngFactory.Create(path, 64, 48);

        var result = await _decoder.DecodeDevelopedAsync(path, halfSize: false);

        result.Width.Should().Be(64);
        result.Height.Should().Be(48);
        result.PixelsBgra.Length.Should().Be(64 * 48 * 4);
        // グラデーション: 左端はRが暗く、右端はRが明るい
        var leftR = result.PixelsBgra[2];
        var rightR = result.PixelsBgra[(63 * 4) + 2];
        rightR.Should().BeGreaterThan(leftR);
    }

    [Fact]
    public async Task DecodeDevelopedAsync_IsDeterministic()
    {
        var path = Path.Combine(_root, "deterministic.dng");
        TestDngFactory.Create(path, 32, 32);

        var first = await _decoder.DecodeDevelopedAsync(path, halfSize: false);
        var second = await _decoder.DecodeDevelopedAsync(path, halfSize: false);

        second.PixelsBgra.Should().Equal(first.PixelsBgra);
    }

    [Fact]
    public async Task DecodePreviewAsync_DngWithoutEmbeddedPreview_FallsBackToDevelop()
    {
        var path = Path.Combine(_root, "nopreview.dng");
        TestDngFactory.Create(path, 256, 128);

        var result = await _decoder.DecodePreviewAsync(path, longEdge: 64);

        result.Width.Should().BeLessThanOrEqualTo(64);
        result.Height.Should().BeLessThanOrEqualTo(64);
        result.PixelsBgra.Length.Should().Be(result.Width * result.Height * 4);
    }

    [Fact]
    public async Task DecodeDevelopedAsync_DoesNotModifySourceFile()
    {
        var path = Path.Combine(_root, "untouched.dng");
        TestDngFactory.Create(path, 32, 32);
        var before = await File.ReadAllBytesAsync(path);

        await _decoder.DecodeDevelopedAsync(path, halfSize: false);

        var after = await File.ReadAllBytesAsync(path);
        after.Should().Equal(before, "RAWファイルは読み取り専用で扱う");
    }

    [Fact]
    public async Task DecodeDevelopedAsync_NotARawFile_Throws()
    {
        var path = Path.Combine(_root, "fake.dng");
        await File.WriteAllTextAsync(path, "not a raw file");

        var act = () => _decoder.DecodeDevelopedAsync(path, halfSize: false);

        await act.Should().ThrowAsync<Exception>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
