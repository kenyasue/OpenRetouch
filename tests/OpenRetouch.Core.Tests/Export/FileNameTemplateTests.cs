using FluentAssertions;
using OpenRetouch.Core.Export;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Core.Tests.Export;

public sealed class FileNameTemplateTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    public FileNameTemplateTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Expand_AllTokens_AreReplaced()
    {
        var photo = CreatePhoto("IMG_0042.jpg", DateTimeOffset.Parse("2026-03-15T10:00:00Z"));

        var result = FileNameTemplate.Expand("{filename}_{date}_{seq}", photo, 7);

        result.Should().StartWith("IMG_0042_2026");
        result.Should().EndWith("_007");
    }

    [Fact]
    public void Expand_NoTokens_ReturnsTemplateAsIs()
    {
        var photo = CreatePhoto("a.jpg", null);

        FileNameTemplate.Expand("output", photo, 1).Should().Be("output");
    }

    [Fact]
    public void Expand_InvalidCharacters_AreSanitized()
    {
        var photo = CreatePhoto("a.jpg", null);

        var result = FileNameTemplate.Expand("ab:c/d*e", photo, 1);

        result.Should().Be("ab_c_d_e");
    }

    [Fact]
    public void Expand_EmptyResult_FallsBackToFileName()
    {
        var photo = CreatePhoto("fallback.jpg", null);

        FileNameTemplate.Expand("   ", photo, 1).Should().Be("fallback");
    }

    [Fact]
    public void ResolveConflict_NoConflict_ReturnsOriginalPath()
    {
        var path = Path.Combine(_root, "new.jpg");

        FileNameTemplate.ResolveConflict(path, ConflictPolicy.Rename).Should().Be(path);
    }

    [Fact]
    public void ResolveConflict_Rename_FindsNextAvailableName()
    {
        var path = Path.Combine(_root, "photo.jpg");
        File.WriteAllText(path, "x");
        File.WriteAllText(Path.Combine(_root, "photo (2).jpg"), "x");

        var resolved = FileNameTemplate.ResolveConflict(path, ConflictPolicy.Rename);

        resolved.Should().Be(Path.Combine(_root, "photo (3).jpg"));
    }

    [Fact]
    public void ResolveConflict_Overwrite_ReturnsSamePath()
    {
        var path = Path.Combine(_root, "photo.jpg");
        File.WriteAllText(path, "x");

        FileNameTemplate.ResolveConflict(path, ConflictPolicy.Overwrite).Should().Be(path);
    }

    [Fact]
    public void ResolveConflict_Skip_ReturnsNull()
    {
        var path = Path.Combine(_root, "photo.jpg");
        File.WriteAllText(path, "x");

        FileNameTemplate.ResolveConflict(path, ConflictPolicy.Skip).Should().BeNull();
    }

    private static Photo CreatePhoto(string fileName, DateTimeOffset? capturedAt) => new()
    {
        Id = Guid.NewGuid().ToString(),
        FolderId = "f1",
        FilePath = @"C:\Photos\" + fileName,
        FileName = fileName,
        FileExtension = Path.GetExtension(fileName).ToLowerInvariant(),
        ImportedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        CapturedAt = capturedAt,
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
