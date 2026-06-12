using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class PresetRepositoryTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly PresetRepository _repository;

    public PresetRepositoryTests()
    {
        _repository = new PresetRepository(_db.ConnectionFactory);
    }

    [Fact]
    public async Task InsertThenGetAll_RoundTripsSettings()
    {
        var preset = CreatePreset("Warm Portrait", "Portrait");
        preset.Settings.Temperature = 30;
        preset.Settings.Vibrance = 15;
        preset.Settings.Sharpening = 40;

        await _repository.InsertAsync(preset);
        var all = await _repository.GetAllAsync();

        all.Should().ContainSingle();
        var loaded = all[0];
        loaded.Name.Should().Be("Warm Portrait");
        loaded.Category.Should().Be("Portrait");
        loaded.Settings.Temperature.Should().Be(30);
        loaded.Settings.Vibrance.Should().Be(15);
        loaded.Settings.Sharpening.Should().Be(40);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPreset()
    {
        var preset = CreatePreset("Temp", null);
        await _repository.InsertAsync(preset);

        await _repository.DeleteAsync(preset.Id);

        (await _repository.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_OrdersByCategoryThenName()
    {
        await _repository.InsertAsync(CreatePreset("Zebra", "B"));
        await _repository.InsertAsync(CreatePreset("alpha", "B"));
        await _repository.InsertAsync(CreatePreset("Mono", "A"));

        var all = await _repository.GetAllAsync();

        all.Select(p => p.Name).Should().ContainInOrder("Mono", "alpha", "Zebra");
    }

    private static Preset CreatePreset(string name, string? category) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        Category = category,
        Settings = new BasicAdjustments(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    public void Dispose() => _db.Dispose();
}
