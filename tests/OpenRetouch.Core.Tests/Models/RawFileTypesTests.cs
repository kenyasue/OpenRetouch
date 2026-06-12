using FluentAssertions;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using Xunit;

namespace OpenRetouch.Core.Tests.Models;

public sealed class RawFileTypesTests
{
    [Theory]
    [InlineData(".cr2")]
    [InlineData(".CR3")]
    [InlineData(".nef")]
    [InlineData(".arw")]
    [InlineData(".raf")]
    [InlineData(".orf")]
    [InlineData(".rw2")]
    [InlineData(".dng")]
    public void IsRaw_RawExtensions_ReturnsTrue(string extension)
    {
        RawFileTypes.IsRaw(extension).Should().BeTrue();
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".tiff")]
    [InlineData(".txt")]
    public void IsRaw_NonRawExtensions_ReturnsFalse(string extension)
    {
        RawFileTypes.IsRaw(extension).Should().BeFalse();
    }

    [Fact]
    public void ImportService_SupportedExtensions_IncludeRawFormats()
    {
        foreach (var extension in RawFileTypes.Extensions)
        {
            ImportService.SupportedExtensions.Contains(extension).Should().BeTrue($"{extension} はインポート対象");
        }

        ImportService.SupportedExtensions.Contains(".jpg").Should().BeTrue();
    }
}
