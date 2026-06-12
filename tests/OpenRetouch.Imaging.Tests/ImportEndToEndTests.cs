using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using OpenRetouch.Imaging.Metadata;
using OpenRetouch.Imaging.Thumbnails;
using Xunit;

namespace OpenRetouch.Imaging.Tests;

/// <summary>
/// インポートのE2E統合テスト。
/// 実画像+実SQLite+実サムネイル生成で「スキャン→登録→サムネイル→再インポート」を検証する。
/// </summary>
public sealed class ImportEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _photosDir;
    private readonly AppEnvironment _environment;
    private readonly ConnectionFactory _connectionFactory;
    private readonly PhotoRepository _photoRepository;
    private readonly ThumbnailCacheRepository _thumbnailCacheRepository;
    private readonly ImportService _importService;
    private readonly CatalogService _catalogService;

    public ImportEndToEndTests()
    {
        _photosDir = Path.Combine(_root, "photos");
        Directory.CreateDirectory(_photosDir);

        _environment = new AppEnvironment(Path.Combine(_root, "appdata"));
        _environment.EnsureDirectories();

        _connectionFactory = new ConnectionFactory(_environment.CatalogDatabasePath);
        using (var connection = _connectionFactory.Open())
        {
            new MigrationRunner(NullLogger<MigrationRunner>.Instance).Apply(connection);
        }

        _photoRepository = new PhotoRepository(_connectionFactory);
        _thumbnailCacheRepository = new ThumbnailCacheRepository(_connectionFactory);

        var jobQueue = Substitute.For<IJobQueue>();
        var folderRepository = new FolderRepository(_connectionFactory);
        var editRepository = new EditRepository(_connectionFactory);
        _catalogService = new CatalogService(
            _photoRepository,
            folderRepository,
            new AlbumRepository(_connectionFactory),
            _thumbnailCacheRepository,
            editRepository,
            new WicThumbnailGenerator(_environment, new OpenRetouch.Imaging.Raw.LibRawDecoder(NullLogger<OpenRetouch.Imaging.Raw.LibRawDecoder>.Instance), NullLogger<WicThumbnailGenerator>.Instance),
            jobQueue,
            NullLogger<CatalogService>.Instance);

        _importService = new ImportService(
            _photoRepository,
            folderRepository,
            editRepository,
            new PhotoMetadataReader(NullLogger<PhotoMetadataReader>.Instance),
            new XmpSidecarService(NullLogger<XmpSidecarService>.Instance),
            jobQueue,
            _catalogService,
            NullLogger<ImportService>.Instance);
    }

    [Fact]
    public async Task ImportThenThumbnails_FullPipeline_Works()
    {
        // 準備: 実画像3枚+破損ファイル1つ+非対応ファイル1つ
        await TestImageFactory.CreateAsync(Path.Combine(_photosDir, "a.jpg"), 800, 600);
        await TestImageFactory.CreateAsync(Path.Combine(_photosDir, "b.png"), 400, 400);
        Directory.CreateDirectory(Path.Combine(_photosDir, "sub"));
        await TestImageFactory.CreateAsync(Path.Combine(_photosDir, "sub", "c.tif"), 600, 300);
        await File.WriteAllTextAsync(Path.Combine(_photosDir, "broken.jpg"), "not an image");
        await File.WriteAllTextAsync(Path.Combine(_photosDir, "notes.txt"), "ignore me");

        ImportCompletedEventArgs? result = null;
        _importService.ImportCompleted += (_, e) => result = e;

        // 1回目のインポート
        await _importService.RunImportAsync(_photosDir, recursive: true, new NoopProgress(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Imported.Should().Be(4, "破損JPEGもメタデータ空で登録される(サムネイル段階でスキップ)");
        result.Skipped.Should().Be(0);

        var photos = await _photoRepository.QueryAsync(new PhotoQuery());
        photos.Should().HaveCount(4);
        photos.Select(p => p.FileName).Should().BeEquivalentTo(["a.jpg", "b.png", "c.tif", "broken.jpg"]);
        photos.Single(p => p.FileName == "a.jpg").Width.Should().Be(800);

        // サムネイル生成
        var readyEvents = new List<ThumbnailReadyEventArgs>();
        _catalogService.ThumbnailReady += (_, e) => readyEvents.Add(e);
        await _catalogService.GenerateMissingThumbnailsAsync(new NoopProgress(), CancellationToken.None);

        readyEvents.Should().HaveCount(3, "破損JPEGはサムネイル生成に失敗してスキップされる");
        var thumbs = await _thumbnailCacheRepository.GetAllThumbPathsAsync();
        thumbs.Should().HaveCount(3);
        thumbs.Values.Should().OnlyContain(p => File.Exists(p));

        // 再インポート: 全件スキップ
        await _importService.RunImportAsync(_photosDir, recursive: true, new NoopProgress(), CancellationToken.None);
        result!.Imported.Should().Be(0);
        result.Skipped.Should().Be(4);
        (await _photoRepository.QueryAsync(new PhotoQuery())).Should().HaveCount(4);

        // サムネイル再実行: 生成対象は破損JPEGのみ(失敗してスキップ)
        readyEvents.Clear();
        await _catalogService.GenerateMissingThumbnailsAsync(new NoopProgress(), CancellationToken.None);
        readyEvents.Should().BeEmpty();
    }

    private sealed class NoopProgress : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value)
        {
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
