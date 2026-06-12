using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class PhotoRepositoryTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly PhotoRepository _repository;
    private readonly FolderRepository _folderRepository;

    public PhotoRepositoryTests()
    {
        _repository = new PhotoRepository(_db.ConnectionFactory);
        _folderRepository = new FolderRepository(_db.ConnectionFactory);
    }

    [Fact]
    public async Task InsertBatchAsync_ThenGetAllAsync_RoundTripsAllFields()
    {
        var folder = await InsertFolderAsync();
        var photo = CreatePhoto(folder.Id, @"C:\Photos\IMG_0001.jpg", capturedAt: DateTimeOffset.Parse("2026-01-15T10:30:00+09:00"));
        photo.Exif.CameraMake = "Canon";
        photo.Exif.CameraModel = "EOS R5";
        photo.Exif.Iso = 400;
        photo.Exif.Aperture = 2.8;
        photo.Rating = 3;
        photo.Flag = PhotoFlag.Pick;
        photo.ColorLabel = ColorLabel.Blue;

        await _repository.InsertBatchAsync([photo]);
        var all = await _repository.QueryAsync(new PhotoQuery());

        all.Should().HaveCount(1);
        var loaded = all[0];
        loaded.Id.Should().Be(photo.Id);
        loaded.FilePath.Should().Be(photo.FilePath);
        loaded.FileExtension.Should().Be(".jpg");
        loaded.CapturedAt.Should().Be(photo.CapturedAt);
        loaded.Exif.CameraMake.Should().Be("Canon");
        loaded.Exif.CameraModel.Should().Be("EOS R5");
        loaded.Exif.Iso.Should().Be(400);
        loaded.Exif.Aperture.Should().Be(2.8);
        loaded.Rating.Should().Be(3);
        loaded.Flag.Should().Be(PhotoFlag.Pick);
        loaded.ColorLabel.Should().Be(ColorLabel.Blue);
        loaded.IsMissing.Should().BeFalse();
    }

    [Fact]
    public async Task InsertBatchAsync_DuplicateFilePath_IsIgnored()
    {
        var folder = await InsertFolderAsync();
        var photo1 = CreatePhoto(folder.Id, @"C:\Photos\IMG_0001.jpg");
        var photo2 = CreatePhoto(folder.Id, @"C:\Photos\IMG_0001.jpg");

        await _repository.InsertBatchAsync([photo1]);
        await _repository.InsertBatchAsync([photo2]);

        var all = await _repository.QueryAsync(new PhotoQuery());
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(photo1.Id);
    }

    [Fact]
    public async Task GetExistingFilePathsAsync_ReturnsInsertedPaths()
    {
        var folder = await InsertFolderAsync();
        await _repository.InsertBatchAsync(
        [
            CreatePhoto(folder.Id, @"C:\Photos\a.jpg"),
            CreatePhoto(folder.Id, @"C:\Photos\b.png"),
        ]);

        var paths = await _repository.GetExistingFilePathsAsync();

        paths.Should().BeEquivalentTo([@"C:\Photos\a.jpg", @"C:\Photos\b.png"]);
        paths.Contains(@"C:\PHOTOS\A.JPG").Should().BeTrue("パス比較は大文字小文字を無視する");
    }

    [Fact]
    public async Task QueryAsync_OrdersByCapturedAtDescending()
    {
        var folder = await InsertFolderAsync();
        var older = CreatePhoto(folder.Id, @"C:\Photos\older.jpg", DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        var newer = CreatePhoto(folder.Id, @"C:\Photos\newer.jpg", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        await _repository.InsertBatchAsync([older, newer]);
        var all = await _repository.QueryAsync(new PhotoQuery());

        all.Select(p => p.FileName).Should().ContainInOrder("newer.jpg", "older.jpg");
    }

    private async Task<Folder> InsertFolderAsync()
    {
        var folder = new Folder
        {
            Id = Guid.NewGuid().ToString(),
            Path = @"C:\Photos",
            Name = "Photos",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _folderRepository.InsertAsync(folder);
        return folder;
    }

    private static Photo CreatePhoto(string folderId, string filePath, DateTimeOffset? capturedAt = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        FolderId = folderId,
        FilePath = filePath,
        FileName = Path.GetFileName(filePath),
        FileExtension = Path.GetExtension(filePath).ToLowerInvariant(),
        FileSize = 12345,
        ImportedAt = DateTimeOffset.UtcNow,
        CapturedAt = capturedAt,
        Width = 6000,
        Height = 4000,
    };

    public void Dispose() => _db.Dispose();
}
