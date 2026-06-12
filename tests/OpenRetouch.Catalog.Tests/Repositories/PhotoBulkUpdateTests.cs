using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class PhotoBulkUpdateTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly PhotoRepository _repository;
    private readonly string _folderId = Guid.NewGuid().ToString();

    public PhotoBulkUpdateTests()
    {
        _repository = new PhotoRepository(_db.ConnectionFactory);
        new FolderRepository(_db.ConnectionFactory).InsertAsync(new Folder
        {
            Id = _folderId,
            Path = @"C:\Photos",
            Name = "Photos",
            CreatedAt = DateTimeOffset.UtcNow,
        }).Wait();
    }

    [Fact]
    public async Task UpdateRatingAsync_MultiplePhotos_UpdatesAll()
    {
        var ids = await InsertPhotosAsync(3);

        await _repository.UpdateRatingAsync([ids[0], ids[1]], 4);

        var photos = await _repository.QueryAsync(new PhotoQuery());
        photos.Where(p => p.Rating == 4).Should().HaveCount(2);
        photos.Single(p => p.Id == ids[2]).Rating.Should().Be(0);
    }

    [Fact]
    public async Task UpdateFlagAsync_SetAndClear_RoundTrips()
    {
        var ids = await InsertPhotosAsync(1);

        await _repository.UpdateFlagAsync(ids, PhotoFlag.Pick);
        var afterPick = await _repository.QueryAsync(new PhotoQuery());
        afterPick[0].Flag.Should().Be(PhotoFlag.Pick);

        await _repository.UpdateFlagAsync(ids, PhotoFlag.None);
        var afterClear = await _repository.QueryAsync(new PhotoQuery());
        afterClear[0].Flag.Should().Be(PhotoFlag.None);
    }

    [Fact]
    public async Task UpdateColorLabelAsync_UpdatesLabel()
    {
        var ids = await InsertPhotosAsync(1);

        await _repository.UpdateColorLabelAsync(ids, ColorLabel.Green);

        var photos = await _repository.QueryAsync(new PhotoQuery());
        photos[0].ColorLabel.Should().Be(ColorLabel.Green);
    }

    [Fact]
    public async Task UpdateRatingAsync_EmptyIdList_DoesNothing()
    {
        var act = () => _repository.UpdateRatingAsync([], 3);
        await act.Should().NotThrowAsync();
    }

    private async Task<List<string>> InsertPhotosAsync(int count)
    {
        var photos = Enumerable.Range(0, count).Select(i => new Photo
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = _folderId,
            FilePath = @"C:\Photos\photo" + Guid.NewGuid().ToString("N") + ".jpg",
            FileName = $"photo{i}.jpg",
            FileExtension = ".jpg",
            ImportedAt = DateTimeOffset.UtcNow,
        }).ToList();
        await _repository.InsertBatchAsync(photos);
        return photos.Select(p => p.Id).ToList();
    }

    public void Dispose() => _db.Dispose();
}
