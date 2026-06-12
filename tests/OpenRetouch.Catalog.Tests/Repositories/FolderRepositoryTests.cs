using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class FolderRepositoryTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly FolderRepository _repository;

    public FolderRepositoryTests()
    {
        _repository = new FolderRepository(_db.ConnectionFactory);
    }

    [Fact]
    public async Task GetByPathAsync_NotInserted_ReturnsNull()
    {
        var result = await _repository.GetByPathAsync(@"C:\Nowhere");
        result.Should().BeNull();
    }

    [Fact]
    public async Task InsertAsync_ThenGetByPathAsync_RoundTrips()
    {
        var folder = new Folder
        {
            Id = Guid.NewGuid().ToString(),
            Path = @"C:\Photos\Trip",
            Name = "Trip",
            CreatedAt = DateTimeOffset.Parse("2026-06-10T12:00:00+00:00"),
        };

        await _repository.InsertAsync(folder);
        var loaded = await _repository.GetByPathAsync(@"C:\Photos\Trip");

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(folder.Id);
        loaded.Name.Should().Be("Trip");
        loaded.ParentId.Should().BeNull();
        loaded.CreatedAt.Should().Be(folder.CreatedAt);
    }

    [Fact]
    public async Task InsertAsync_SamePathTwice_IsIgnored()
    {
        var first = new Folder
        {
            Id = "id-1",
            Path = @"C:\Photos",
            Name = "Photos",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var second = new Folder
        {
            Id = "id-2",
            Path = @"C:\Photos",
            Name = "Photos2",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _repository.InsertAsync(first);
        await _repository.InsertAsync(second);

        var loaded = await _repository.GetByPathAsync(@"C:\Photos");
        loaded!.Id.Should().Be("id-1");
    }

    public void Dispose() => _db.Dispose();
}
