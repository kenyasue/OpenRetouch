using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class ExportJobRepositoryTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly ExportJobRepository _repository;
    private readonly PhotoRepository _photoRepository;
    private readonly FolderRepository _folderRepository;

    public ExportJobRepositoryTests()
    {
        _repository = new ExportJobRepository(_db.ConnectionFactory);
        _photoRepository = new PhotoRepository(_db.ConnectionFactory);
        _folderRepository = new FolderRepository(_db.ConnectionFactory);
    }

    [Fact]
    public async Task CreateJob_ThenGetItems_ReturnsPendingItems()
    {
        var photoId = await InsertPhotoAsync();
        var jobId = Guid.NewGuid().ToString();

        await _repository.CreateJobAsync(jobId, """{"format":"jpeg"}""", [("item1", photoId)]);

        var items = await _repository.GetItemsAsync(jobId);
        items.Should().ContainSingle();
        items[0].ItemId.Should().Be("item1");
        items[0].PhotoId.Should().Be(photoId);
        items[0].Status.Should().Be("pending");

        var settings = await _repository.GetJobSettingsJsonAsync(jobId);
        settings.Should().Be("""{"format":"jpeg"}""");
    }

    [Fact]
    public async Task UpdateItem_RecordsStatusAndError()
    {
        var photoId = await InsertPhotoAsync();
        var jobId = Guid.NewGuid().ToString();
        await _repository.CreateJobAsync(jobId, "{}", [("item1", photoId)]);

        await _repository.UpdateItemAsync("item1", "failed", null, "デコードに失敗しました");

        var items = await _repository.GetItemsAsync(jobId);
        items[0].Status.Should().Be("failed");
        items[0].ErrorMessage.Should().Be("デコードに失敗しました");
    }

    [Fact]
    public async Task UpdateJobStatus_Completed_DoesNotThrow()
    {
        var photoId = await InsertPhotoAsync();
        var jobId = Guid.NewGuid().ToString();
        await _repository.CreateJobAsync(jobId, "{}", [("item1", photoId)]);

        var act = () => _repository.UpdateJobStatusAsync(jobId, "completed");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetJobSettingsJson_UnknownJob_ReturnsNull()
    {
        (await _repository.GetJobSettingsJsonAsync("nope")).Should().BeNull();
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
