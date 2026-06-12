using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using Xunit;

namespace OpenRetouch.Core.Tests.Services;

public sealed class PresetServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly IPresetRepository _presetRepository = Substitute.For<IPresetRepository>();
    private readonly IEditRepository _editRepository = Substitute.For<IEditRepository>();
    private readonly IPhotoRepository _photoRepository = Substitute.For<IPhotoRepository>();
    private readonly IXmpSidecarService _xmpSidecarService = Substitute.For<IXmpSidecarService>();
    private readonly PresetService _service;

    public PresetServiceTests()
    {
        Directory.CreateDirectory(_root);
        _photoRepository.GetByIdsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Photo>());
        _service = new PresetService(
            _presetRepository, _editRepository, _photoRepository, _xmpSidecarService,
            NullLogger<PresetService>.Instance);
    }

    [Fact]
    public async Task CreateFromSettings_StoresBasicOnly()
    {
        var source = new EditSettings();
        source.Basic.Exposure = 1.5;
        source.Crop.X = 0.2;

        var preset = await _service.CreateFromSettingsAsync("My Preset", "風景", source);

        preset.Name.Should().Be("My Preset");
        preset.Category.Should().Be("風景");
        preset.Settings.Exposure.Should().Be(1.5);
        await _presetRepository.Received(1).InsertAsync(Arg.Any<Preset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFromSettings_IsIndependentOfSource()
    {
        var source = new EditSettings();
        source.Basic.Contrast = 10;

        var preset = await _service.CreateFromSettingsAsync("P", null, source);
        source.Basic.Contrast = 99;

        preset.Settings.Contrast.Should().Be(10);
    }

    [Fact]
    public async Task ApplyToPhotos_MergesAndSavesPerPhoto()
    {
        var preset = new Preset
        {
            Id = "p1",
            Name = "P",
            Settings = new BasicAdjustments { Vibrance = 30 },
        };
        _presetRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Preset> { preset });
        var existingEdit = new EditSettings();
        existingEdit.Crop.X = 0.3;
        _editRepository.GetCurrentAsync("photo1", Arg.Any<CancellationToken>()).Returns(existingEdit);
        _editRepository.GetCurrentAsync("photo2", Arg.Any<CancellationToken>()).Returns((EditSettings?)null);

        var applied = await _service.ApplyToPhotosAsync("p1", ["photo1", "photo2"]);

        applied.Should().Be(2);
        await _editRepository.Received(1).UpsertCurrentAsync(
            "photo1",
            Arg.Is<EditSettings>(s => s.Basic.Vibrance == 30 && s.Crop.X == 0.3),
            Arg.Any<CancellationToken>());
        await _editRepository.Received(1).UpsertCurrentAsync(
            "photo2",
            Arg.Is<EditSettings>(s => s.Basic.Vibrance == 30 && s.Crop.IsDefault),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyToPhotos_OneFails_ContinuesAndReportsCount()
    {
        var preset = new Preset { Id = "p1", Name = "P", Settings = new BasicAdjustments() };
        _presetRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Preset> { preset });
        _editRepository.GetCurrentAsync("bad", Arg.Any<CancellationToken>())
            .Returns<Task<EditSettings?>>(_ => throw new InvalidOperationException("db error"));
        _editRepository.GetCurrentAsync("good", Arg.Any<CancellationToken>()).Returns((EditSettings?)null);

        var applied = await _service.ApplyToPhotosAsync("p1", ["bad", "good"]);

        applied.Should().Be(1);
    }

    [Fact]
    public async Task ExportThenImport_RoundTrips()
    {
        var preset = new Preset
        {
            Id = "p1",
            Name = "Export Me",
            Category = "Test",
            Settings = new BasicAdjustments { Exposure = 0.8, Clarity = 25 },
        };
        _presetRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Preset> { preset });
        var filePath = Path.Combine(_root, "preset.json");

        await _service.ExportAsync("p1", filePath);
        var imported = await _service.ImportAsync(filePath);

        imported.Name.Should().Be("Export Me");
        imported.Category.Should().Be("Test");
        imported.Settings.Exposure.Should().Be(0.8);
        imported.Settings.Clarity.Should().Be(25);
        imported.Id.Should().NotBe("p1", "インポートは新しいIDで作成される");
    }

    [Fact]
    public async Task Import_OutOfRangeValues_AreClamped()
    {
        var filePath = Path.Combine(_root, "extreme.json");
        await File.WriteAllTextAsync(filePath, """
            { "name": "Extreme", "settings": { "exposure": 99, "contrast": -500, "sharpening": 9999 } }
            """);

        var imported = await _service.ImportAsync(filePath);

        imported.Settings.Exposure.Should().Be(5.0);
        imported.Settings.Contrast.Should().Be(-100);
        imported.Settings.Sharpening.Should().Be(150);
    }

    [Fact]
    public async Task Import_CorruptedJson_Throws()
    {
        var filePath = Path.Combine(_root, "broken.json");
        await File.WriteAllTextAsync(filePath, "{ not json !!");

        var act = () => _service.ImportAsync(filePath);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
