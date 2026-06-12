using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class EditRepositoryTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly EditRepository _repository;
    private readonly PhotoRepository _photoRepository;
    private readonly FolderRepository _folderRepository;

    public EditRepositoryTests()
    {
        _repository = new EditRepository(_db.ConnectionFactory);
        _photoRepository = new PhotoRepository(_db.ConnectionFactory);
        _folderRepository = new FolderRepository(_db.ConnectionFactory);
    }

    [Fact]
    public async Task GetCurrentAsync_NoEdit_ReturnsNull()
    {
        var result = await _repository.GetCurrentAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertThenGet_RoundTrips()
    {
        var photoId = await InsertPhotoAsync();
        var settings = new EditSettings();
        settings.Basic.Exposure = 1.25;
        settings.Basic.Temperature = -40;

        await _repository.UpsertCurrentAsync(photoId, settings);
        var restored = await _repository.GetCurrentAsync(photoId);

        restored.Should().NotBeNull();
        restored!.Basic.Exposure.Should().Be(1.25);
        restored.Basic.Temperature.Should().Be(-40);
    }

    [Fact]
    public async Task UpsertTwice_UpdatesExistingRecord()
    {
        var photoId = await InsertPhotoAsync();
        var first = new EditSettings();
        first.Basic.Contrast = 10;
        var second = new EditSettings();
        second.Basic.Contrast = 50;

        await _repository.UpsertCurrentAsync(photoId, first);
        await _repository.UpsertCurrentAsync(photoId, second);

        var restored = await _repository.GetCurrentAsync(photoId);
        restored!.Basic.Contrast.Should().Be(50);
    }

    [Fact]
    public async Task GetEditedPhotoIdsAsync_ReturnsEditedOnly()
    {
        var edited = await InsertPhotoAsync();
        await InsertPhotoAsync();
        await _repository.UpsertCurrentAsync(edited, new EditSettings());

        var ids = await _repository.GetEditedPhotoIdsAsync();

        ids.Should().BeEquivalentTo([edited]);
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
