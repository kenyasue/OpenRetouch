using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Services;
using Xunit;

namespace OpenRetouch.Core.Tests.Services;

public sealed class EditServiceTests
{
    private readonly IEditRepository _repository = Substitute.For<IEditRepository>();
    private readonly IPhotoRepository _photoRepository = Substitute.For<IPhotoRepository>();
    private readonly IXmpSidecarService _xmpSidecarService = Substitute.For<IXmpSidecarService>();
    private readonly EditService _service;

    public EditServiceTests()
    {
        _photoRepository.GetByIdsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OpenRetouch.Core.Models.Photo>());
        _service = new EditService(
            _repository, _photoRepository, _xmpSidecarService, NullLogger<EditService>.Instance);
    }

    [Fact]
    public async Task SaveEditAsync_RawPhoto_WritesXmpSidecar()
    {
        var rawPhoto = new OpenRetouch.Core.Models.Photo
        {
            Id = "raw1",
            FolderId = "f1",
            FilePath = @"C:\Photos\IMG_0001.CR3",
            FileName = "IMG_0001.CR3",
            FileExtension = ".cr3",
            ImportedAt = DateTimeOffset.UtcNow,
        };
        _photoRepository.GetByIdsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OpenRetouch.Core.Models.Photo> { rawPhoto });
        var settings = new EditSettings();
        settings.Basic.Exposure = 1.0;

        await _service.SaveEditAsync("raw1", settings);

        await _xmpSidecarService.Received(1).WriteAsync(rawPhoto, settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveEditAsync_NonRawPhoto_DoesNotWriteSidecar()
    {
        var jpegPhoto = new OpenRetouch.Core.Models.Photo
        {
            Id = "jpg1",
            FolderId = "f1",
            FilePath = @"C:\Photos\photo.jpg",
            FileName = "photo.jpg",
            FileExtension = ".jpg",
            ImportedAt = DateTimeOffset.UtcNow,
        };
        _photoRepository.GetByIdsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OpenRetouch.Core.Models.Photo> { jpegPhoto });

        await _service.SaveEditAsync("jpg1", new EditSettings());

        await _xmpSidecarService.DidNotReceive().WriteAsync(
            Arg.Any<OpenRetouch.Core.Models.Photo>(), Arg.Any<EditSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEditAsync_NoEdit_ReturnsDefault()
    {
        _repository.GetCurrentAsync("p1", Arg.Any<CancellationToken>())
            .Returns((EditSettings?)null);

        var settings = await _service.GetEditAsync("p1");

        settings.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetEditAsync_ExistingEdit_ReturnsIt()
    {
        var stored = new EditSettings();
        stored.Basic.Contrast = 25;
        _repository.GetCurrentAsync("p1", Arg.Any<CancellationToken>()).Returns(stored);

        var settings = await _service.GetEditAsync("p1");

        settings.Basic.Contrast.Should().Be(25);
    }

    [Fact]
    public async Task GetEditAsync_CorruptedJson_FallsBackToDefault()
    {
        _repository.GetCurrentAsync("p1", Arg.Any<CancellationToken>())
            .Returns<Task<EditSettings?>>(_ => throw new System.Text.Json.JsonException("broken"));

        var settings = await _service.GetEditAsync("p1");

        settings.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SaveEditAsync_DelegatesToRepository()
    {
        var settings = new EditSettings();
        settings.Basic.Exposure = 0.5;

        await _service.SaveEditAsync("p1", settings);

        await _repository.Received(1).UpsertCurrentAsync("p1", settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetAsync_SavesDefaultSettings()
    {
        await _service.ResetAsync("p1");

        await _repository.Received(1).UpsertCurrentAsync(
            "p1", Arg.Is<EditSettings>(s => s.IsDefault), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveEditAsync_EmptyPhotoId_Throws()
    {
        var act = () => _service.SaveEditAsync("", new EditSettings());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ApplyToPhotosAsync_SavesCloneToEachPhoto()
    {
        var settings = new EditSettings();
        settings.Basic.Contrast = 33;
        settings.Crop.X = 0.2;

        var applied = await _service.ApplyToPhotosAsync(settings, ["a", "b"]);

        applied.Should().Be(2);
        await _repository.Received(1).UpsertCurrentAsync(
            "a", Arg.Is<EditSettings>(s => s.Basic.Contrast == 33 && s.Crop.X == 0.2), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpsertCurrentAsync(
            "b", Arg.Any<EditSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyToPhotosAsync_OneFails_ContinuesAndCounts()
    {
        _repository.UpsertCurrentAsync("bad", Arg.Any<EditSettings>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("db error"));

        var applied = await _service.ApplyToPhotosAsync(new EditSettings(), ["bad", "good"]);

        applied.Should().Be(1);
    }

    [Fact]
    public void CopyBuffer_RoundTrips()
    {
        var settings = new EditSettings();
        settings.Basic.Exposure = 1.0;

        _service.CopyBuffer = settings;

        _service.CopyBuffer.Should().BeSameAs(settings);
    }
}
