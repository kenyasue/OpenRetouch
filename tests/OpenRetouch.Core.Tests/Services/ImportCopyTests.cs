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

/// <summary>コピーインポート(SDカード取り込み)のテスト。リポジトリはモック、ファイル操作は実FS。</summary>
public sealed class ImportCopyTests : IDisposable
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 5, 14, 10, 30, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    private readonly string _source;
    private readonly string _destination;

    private readonly IPhotoRepository _photoRepository = Substitute.For<IPhotoRepository>();
    private readonly IFolderRepository _folderRepository = Substitute.For<IFolderRepository>();
    private readonly IPhotoMetadataReader _metadataReader = Substitute.For<IPhotoMetadataReader>();
    private readonly ImportService _service;

    public ImportCopyTests()
    {
        _source = Path.Combine(_root, "sdcard");
        _destination = Path.Combine(_root, "library");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_destination);

        _photoRepository.GetExistingFilePathsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _folderRepository.GetByPathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Folder?)null);
        _metadataReader.Read(Arg.Any<string>()).Returns(new PhotoMetadata { CapturedAt = CapturedAt });

        var xmpSidecarService = Substitute.For<IXmpSidecarService>();
        xmpSidecarService.TryReadAsync(Arg.Any<Photo>(), Arg.Any<CancellationToken>())
            .Returns((OpenRetouch.Core.Editing.XmpSidecarData?)null);

        _service = new ImportService(
            _photoRepository, _folderRepository, Substitute.For<IEditRepository>(),
            _metadataReader, xmpSidecarService, Substitute.For<IJobQueue>(),
            Substitute.For<ICatalogService>(), NullLogger<ImportService>.Instance);
    }

    private static string DateFolder()
    {
        var local = CapturedAt.ToLocalTime();
        return Path.Combine(
            local.Year.ToString("D4"), local.Month.ToString("D2"), local.Day.ToString("D2"));
    }

    [Fact]
    public async Task CopyImport_WithDateFolders_CopiesIntoDateHierarchyAndRegistersDestination()
    {
        var sourceFile = WriteFile(Path.Combine(_source, "IMG_0001.jpg"), "image-data");

        await RunAsync(new ImportOptions
        {
            SourceFolder = _source,
            Mode = ImportMode.CopyToCustomFolder,
            DestinationFolder = _destination,
            UseDateFolders = true,
        });

        var expectedDest = Path.Combine(_destination, DateFolder(), "IMG_0001.jpg");
        File.Exists(expectedDest).Should().BeTrue("日付フォルダ階層へコピーされる");
        File.Exists(sourceFile).Should().BeTrue("コピー元は変更されない");
        File.ReadAllText(sourceFile).Should().Be("image-data");

        await _photoRepository.Received(1).InsertBatchAsync(
            Arg.Is<IReadOnlyList<Photo>>(b => b.Count == 1 && b[0].FilePath == expectedDest),
            Arg.Any<CancellationToken>());
        await _folderRepository.Received(1).InsertAsync(
            Arg.Is<Folder>(f => f.Path == _destination), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CopyImport_WithoutDateFolders_CopiesDirectlyUnderRoot()
    {
        WriteFile(Path.Combine(_source, "IMG_0001.jpg"), "image-data");

        await RunAsync(new ImportOptions
        {
            SourceFolder = _source,
            Mode = ImportMode.CopyToDefaultFolder,
            DestinationFolder = _destination,
            UseDateFolders = false,
        });

        File.Exists(Path.Combine(_destination, "IMG_0001.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task CopyImport_RawWithSidecar_CopiesSidecarTogether()
    {
        WriteFile(Path.Combine(_source, "IMG_0002.dng"), "raw-data");
        WriteFile(Path.Combine(_source, "IMG_0002.xmp"), "<xmp/>");

        await RunAsync(new ImportOptions
        {
            SourceFolder = _source,
            Mode = ImportMode.CopyToCustomFolder,
            DestinationFolder = _destination,
            UseDateFolders = true,
        });

        var destDir = Path.Combine(_destination, DateFolder());
        File.Exists(Path.Combine(destDir, "IMG_0002.dng")).Should().BeTrue();
        File.Exists(Path.Combine(destDir, "IMG_0002.xmp")).Should().BeTrue("XMPサイドカーも一緒にコピーされる");
    }

    [Fact]
    public async Task CopyImport_SidecarCopyFails_StillImportsMainFile()
    {
        WriteFile(Path.Combine(_source, "IMG_0002.dng"), "raw-data");
        var sidecarPath = WriteFile(Path.Combine(_source, "IMG_0002.xmp"), "<xmp/>");

        // サイドカーを排他ロックしてコピー失敗を再現する
        using (File.Open(sidecarPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await RunAsync(new ImportOptions
            {
                SourceFolder = _source,
                Mode = ImportMode.CopyToCustomFolder,
                DestinationFolder = _destination,
                UseDateFolders = true,
            });
        }

        var destDir = Path.Combine(_destination, DateFolder());
        File.Exists(Path.Combine(destDir, "IMG_0002.dng")).Should().BeTrue();
        await _photoRepository.Received(1).InsertBatchAsync(
            Arg.Is<IReadOnlyList<Photo>>(b => b.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CopyImport_DestinationAlreadyExists_DoesNotOverwriteAndImportsExisting()
    {
        WriteFile(Path.Combine(_source, "IMG_0001.jpg"), "new-content");
        var existingDest = WriteFile(
            Path.Combine(_destination, DateFolder(), "IMG_0001.jpg"), "already-copied");

        await RunAsync(new ImportOptions
        {
            SourceFolder = _source,
            Mode = ImportMode.CopyToCustomFolder,
            DestinationFolder = _destination,
            UseDateFolders = true,
        });

        File.ReadAllText(existingDest).Should().Be("already-copied", "既存ファイルは上書きされない");
        await _photoRepository.Received(1).InsertBatchAsync(
            Arg.Is<IReadOnlyList<Photo>>(b => b.Count == 1 && b[0].FilePath == existingDest),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CopyImport_DestinationEqualsSource_RegistersInPlaceWithoutCopy()
    {
        var sourceFile = WriteFile(Path.Combine(_source, "IMG_0001.jpg"), "image-data");

        await RunAsync(new ImportOptions
        {
            SourceFolder = _source,
            Mode = ImportMode.CopyToCustomFolder,
            DestinationFolder = _source,
            UseDateFolders = true,
        });

        Directory.EnumerateDirectories(_source).Should().BeEmpty("日付フォルダは作られない");
        await _photoRepository.Received(1).InsertBatchAsync(
            Arg.Is<IReadOnlyList<Photo>>(b => b.Count == 1 && b[0].FilePath == sourceFile),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Import_CopyModeWithoutDestination_Throws()
    {
        var act = () => _service.Import(new ImportOptions
        {
            SourceFolder = _source,
            Mode = ImportMode.CopyToDefaultFolder,
            DestinationFolder = null,
        });

        act.Should().Throw<ArgumentException>();
    }

    private Task RunAsync(ImportOptions options) =>
        _service.RunImportAsync(options, new NoopProgress(), CancellationToken.None);

    private static string WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
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
