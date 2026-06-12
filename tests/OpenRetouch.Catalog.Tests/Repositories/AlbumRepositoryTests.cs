using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class AlbumRepositoryTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly AlbumRepository _repository;
    private readonly PhotoRepository _photoRepository;
    private readonly FolderRepository _folderRepository;

    public AlbumRepositoryTests()
    {
        _repository = new AlbumRepository(_db.ConnectionFactory);
        _photoRepository = new PhotoRepository(_db.ConnectionFactory);
        _folderRepository = new FolderRepository(_db.ConnectionFactory);
    }

    [Fact]
    public async Task InsertAsync_ThenGetAll_ReturnsAlbum()
    {
        await _repository.InsertAsync("Trip 2026");

        var albums = await _repository.GetAllAsync();

        albums.Should().ContainSingle().Which.Name.Should().Be("Trip 2026");
    }

    [Fact]
    public async Task InsertAsync_EmptyName_Throws()
    {
        var act = () => _repository.InsertAsync("  ");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddPhotos_ThenQueryByAlbum_ReturnsMembers()
    {
        var photoId = await InsertPhotoAsync("a.jpg");
        var album = await _repository.InsertAsync("Album");

        await _repository.AddPhotosAsync(album.Id, [photoId]);
        var photos = await _photoRepository.QueryAsync(new PhotoQuery { AlbumId = album.Id });

        photos.Should().ContainSingle().Which.Id.Should().Be(photoId);
    }

    [Fact]
    public async Task AddPhotos_SamePhotoTwice_IsIgnored()
    {
        var photoId = await InsertPhotoAsync("a.jpg");
        var album = await _repository.InsertAsync("Album");

        await _repository.AddPhotosAsync(album.Id, [photoId]);
        await _repository.AddPhotosAsync(album.Id, [photoId]);

        var photos = await _photoRepository.QueryAsync(new PhotoQuery { AlbumId = album.Id });
        photos.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemovePhotos_RemovesMembershipOnly()
    {
        var photoId = await InsertPhotoAsync("a.jpg");
        var album = await _repository.InsertAsync("Album");
        await _repository.AddPhotosAsync(album.Id, [photoId]);

        await _repository.RemovePhotosAsync(album.Id, [photoId]);

        (await _photoRepository.QueryAsync(new PhotoQuery { AlbumId = album.Id })).Should().BeEmpty();
        (await _photoRepository.QueryAsync(new PhotoQuery())).Should().HaveCount(1, "写真自体は削除されない");
    }

    [Fact]
    public async Task DeleteAsync_RemovesAlbumAndMembership()
    {
        var photoId = await InsertPhotoAsync("a.jpg");
        var album = await _repository.InsertAsync("Album");
        await _repository.AddPhotosAsync(album.Id, [photoId]);

        await _repository.DeleteAsync(album.Id);

        (await _repository.GetAllAsync()).Should().BeEmpty();
        (await _photoRepository.QueryAsync(new PhotoQuery())).Should().HaveCount(1, "写真自体は削除されない");
    }

    private async Task<string> InsertPhotoAsync(string fileName)
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
            FilePath = @"C:\Photos\" + Guid.NewGuid().ToString("N") + "_" + fileName,
            FileName = fileName,
            FileExtension = Path.GetExtension(fileName).ToLowerInvariant(),
            ImportedAt = DateTimeOffset.UtcNow,
        };
        await _photoRepository.InsertBatchAsync([photo]);
        return photo.Id;
    }

    public void Dispose() => _db.Dispose();
}
