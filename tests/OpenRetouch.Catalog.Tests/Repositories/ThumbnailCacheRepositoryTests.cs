using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class ThumbnailCacheRepositoryTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly ThumbnailCacheRepository _repository;
    private readonly PhotoRepository _photoRepository;
    private readonly FolderRepository _folderRepository;

    public ThumbnailCacheRepositoryTests()
    {
        _repository = new ThumbnailCacheRepository(_db.ConnectionFactory);
        _photoRepository = new PhotoRepository(_db.ConnectionFactory);
        _folderRepository = new FolderRepository(_db.ConnectionFactory);
    }

    [Fact]
    public async Task GetAllThumbPathsAsync_Empty_ReturnsEmpty()
    {
        var result = await _repository.GetAllThumbPathsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_ThenGetAll_ReturnsMapping()
    {
        var photoId = await InsertPhotoAsync();

        await _repository.UpsertAsync(photoId, @"C:\cache\thumb1.jpg", DateTimeOffset.UtcNow);
        var result = await _repository.GetAllThumbPathsAsync();

        result.Should().ContainKey(photoId).WhoseValue.Should().Be(@"C:\cache\thumb1.jpg");
    }

    [Fact]
    public async Task UpsertAsync_SamePhotoTwice_UpdatesPath()
    {
        var photoId = await InsertPhotoAsync();

        await _repository.UpsertAsync(photoId, @"C:\cache\old.jpg", DateTimeOffset.UtcNow);
        await _repository.UpsertAsync(photoId, @"C:\cache\new.jpg", DateTimeOffset.UtcNow);

        var result = await _repository.GetAllThumbPathsAsync();
        result.Should().HaveCount(1);
        result[photoId].Should().Be(@"C:\cache\new.jpg");
    }

    [Fact]
    public async Task CountPhotosWithoutThumbnail_CountsOnlyMissing()
    {
        var withThumb = await InsertPhotoAsync();
        await InsertPhotoAsync(); // サムネイルなし
        await InsertPhotoAsync(); // サムネイルなし
        await _repository.UpsertAsync(withThumb, @"C:\cache\t1.jpg", DateTimeOffset.UtcNow);

        var count = await _repository.CountPhotosWithoutThumbnailAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task CountPhotosWithoutThumbnail_AllCached_ReturnsZero()
    {
        var photoId = await InsertPhotoAsync();
        await _repository.UpsertAsync(photoId, @"C:\cache\t1.jpg", DateTimeOffset.UtcNow);

        var count = await _repository.CountPhotosWithoutThumbnailAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task RemoveAsync_DeletesCacheRows()
    {
        var photoId1 = await InsertPhotoAsync();
        var photoId2 = await InsertPhotoAsync();
        await _repository.UpsertAsync(photoId1, @"C:\cache\t1.jpg", DateTimeOffset.UtcNow);
        await _repository.UpsertAsync(photoId2, @"C:\cache\t2.jpg", DateTimeOffset.UtcNow);

        await _repository.RemoveAsync([photoId1]);

        var remaining = await _repository.GetAllThumbPathsAsync();
        remaining.Should().HaveCount(1);
        remaining.Should().ContainKey(photoId2);
    }

    [Fact]
    public async Task RemoveAsync_EmptyList_DoesNothing()
    {
        var act = () => _repository.RemoveAsync([]);
        await act.Should().NotThrowAsync();
    }

    private async Task<string> InsertPhotoAsync()
    {
        var folder = new Folder
        {
            Id = Guid.NewGuid().ToString(),
            Path = @"C:\Photos\" + Guid.NewGuid().ToString("N"),
            Name = "Photos",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _folderRepository.InsertAsync(folder);

        var photo = new Photo
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = folder.Id,
            FilePath = @"C:\Photos\" + Guid.NewGuid().ToString("N") + ".jpg",
            FileName = "x.jpg",
            FileExtension = ".jpg",
            ImportedAt = DateTimeOffset.UtcNow,
        };
        await _photoRepository.InsertBatchAsync([photo]);
        return photo.Id;
    }

    public void Dispose() => _db.Dispose();
}
