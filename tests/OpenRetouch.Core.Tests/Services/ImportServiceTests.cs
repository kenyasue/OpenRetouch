using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using Xunit;

namespace OpenRetouch.Core.Tests.Services;

public sealed class ImportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    private readonly IPhotoRepository _photoRepository = Substitute.For<IPhotoRepository>();
    private readonly IFolderRepository _folderRepository = Substitute.For<IFolderRepository>();
    private readonly IEditRepository _editRepository = Substitute.For<IEditRepository>();
    private readonly IPhotoMetadataReader _metadataReader = Substitute.For<IPhotoMetadataReader>();
    private readonly IXmpSidecarService _xmpSidecarService = Substitute.For<IXmpSidecarService>();
    private readonly IJobQueue _jobQueue = Substitute.For<IJobQueue>();
    private readonly ICatalogService _catalogService = Substitute.For<ICatalogService>();
    private readonly ImportService _service;

    public ImportServiceTests()
    {
        Directory.CreateDirectory(_root);
        _photoRepository.GetExistingFilePathsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _folderRepository.GetByPathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Folder?)null);
        _metadataReader.Read(Arg.Any<string>()).Returns(PhotoMetadata.Empty);
        _xmpSidecarService.TryReadAsync(Arg.Any<Photo>(), Arg.Any<CancellationToken>())
            .Returns((OpenRetouch.Core.Editing.XmpSidecarData?)null);

        _service = new ImportService(
            _photoRepository, _folderRepository, _editRepository, _metadataReader,
            _xmpSidecarService, _jobQueue, _catalogService, NullLogger<ImportService>.Instance);
    }

    [Fact]
    public async Task RunImportAsync_RawWithSidecar_AppliesRatingAndSavesEdit()
    {
        var rawPath = WriteFile("photo.dng");
        var sidecarSettings = new OpenRetouch.Core.Editing.EditSettings();
        sidecarSettings.Basic.Exposure = 1.5;
        _xmpSidecarService.TryReadAsync(
                Arg.Is<Photo>(p => p.FilePath == rawPath), Arg.Any<CancellationToken>())
            .Returns(new OpenRetouch.Core.Editing.XmpSidecarData(
                sidecarSettings, Rating: 4, ColorLabel.Green));

        await _service.RunImportAsync(_root, recursive: false, new NoopProgress(), CancellationToken.None);

        await _photoRepository.Received(1).InsertBatchAsync(
            Arg.Is<IReadOnlyList<Photo>>(b =>
                b.Count == 1 && b[0].Rating == 4 && b[0].ColorLabel == ColorLabel.Green),
            Arg.Any<CancellationToken>());
        await _editRepository.Received(1).UpsertCurrentAsync(
            Arg.Any<string>(),
            Arg.Is<OpenRetouch.Core.Editing.EditSettings>(s => s.Basic.Exposure == 1.5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ScanFiles_FiltersSupportedExtensions()
    {
        WriteFile("a.jpg");
        WriteFile("b.PNG");
        WriteFile("c.tiff");
        WriteFile("d.txt");
        WriteFile("e.raw");

        var files = ImportService.ScanFiles(_root, recursive: false);

        files.Select(Path.GetFileName).Should().BeEquivalentTo(["a.jpg", "b.PNG", "c.tiff"]);
    }

    [Fact]
    public void ScanFiles_Recursive_IncludesSubfolders()
    {
        WriteFile("top.jpg");
        WriteFile(Path.Combine("sub", "nested.png"));

        var recursive = ImportService.ScanFiles(_root, recursive: true);
        var flat = ImportService.ScanFiles(_root, recursive: false);

        recursive.Should().HaveCount(2);
        flat.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunImportAsync_RegistersNewPhotosAndSkipsExisting()
    {
        var existingPath = WriteFile("existing.jpg");
        var newPath = WriteFile("new.jpg");
        _photoRepository.GetExistingFilePathsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { existingPath });

        ImportCompletedEventArgs? result = null;
        _service.ImportCompleted += (_, e) => result = e;

        await _service.RunImportAsync(_root, recursive: false, new NoopProgress(), CancellationToken.None);

        await _photoRepository.Received(1).InsertBatchAsync(
            Arg.Is<IReadOnlyList<Photo>>(b => b.Count == 1 && b[0].FilePath == newPath),
            Arg.Any<CancellationToken>());
        result.Should().NotBeNull();
        result!.Imported.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.FailedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task RunImportAsync_EnqueuesThumbnailGeneration()
    {
        WriteFile("photo.jpg");

        await _service.RunImportAsync(_root, recursive: false, new NoopProgress(), CancellationToken.None);

        _catalogService.Received(1).EnqueueThumbnailGeneration();
    }

    [Fact]
    public async Task RunImportAsync_ReusesExistingFolder()
    {
        WriteFile("photo.jpg");
        var folder = new Folder
        {
            Id = "existing-folder",
            Path = _root,
            Name = "x",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _folderRepository.GetByPathAsync(_root, Arg.Any<CancellationToken>()).Returns(folder);

        await _service.RunImportAsync(_root, recursive: false, new NoopProgress(), CancellationToken.None);

        await _folderRepository.DidNotReceive().InsertAsync(Arg.Any<Folder>(), Arg.Any<CancellationToken>());
        await _photoRepository.Received(1).InsertBatchAsync(
            Arg.Is<IReadOnlyList<Photo>>(b => b[0].FolderId == "existing-folder"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunImportAsync_UsesMetadataFromReader()
    {
        var path = WriteFile("photo.jpg");
        var captured = DateTimeOffset.Parse("2026-03-01T09:00:00Z");
        _metadataReader.Read(path).Returns(new PhotoMetadata
        {
            CapturedAt = captured,
            Width = 6000,
            Height = 4000,
            Orientation = 6,
            Exif = new ExifInfo { CameraMake = "Nikon" },
        });

        await _service.RunImportAsync(_root, recursive: false, new NoopProgress(), CancellationToken.None);

        await _photoRepository.Received(1).InsertBatchAsync(
            Arg.Is<IReadOnlyList<Photo>>(b =>
                b[0].CapturedAt == captured
                && b[0].Width == 6000
                && b[0].Orientation == 6
                && b[0].Exif.CameraMake == "Nikon"),
            Arg.Any<CancellationToken>());
    }

    private string WriteFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "dummy");
        return path;
    }

    private sealed class NoopProgress : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value)
        {
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
