using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Export;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using OpenRetouch.Imaging.Export;
using Xunit;

namespace OpenRetouch.Imaging.Tests;

/// <summary>
/// 書き出しのE2E統合テスト。
/// 実画像+実SQLite+実WICパイプラインで「一括書き出し→部分失敗→失敗のみ再実行」を検証する。
/// </summary>
public sealed class ExportEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _photosDir;
    private readonly string _outDir;
    private readonly ConnectionFactory _connectionFactory;
    private readonly PhotoRepository _photoRepository;
    private readonly FolderRepository _folderRepository;
    private readonly EditRepository _editRepository;
    private readonly ExportJobRepository _jobRepository;
    private readonly ExportService _service;

    public ExportEndToEndTests()
    {
        _photosDir = Path.Combine(_root, "photos");
        _outDir = Path.Combine(_root, "out");
        Directory.CreateDirectory(_photosDir);
        Directory.CreateDirectory(_outDir);

        var environment = new AppEnvironment(Path.Combine(_root, "appdata"));
        environment.EnsureDirectories();
        _connectionFactory = new ConnectionFactory(environment.CatalogDatabasePath);
        using (var connection = _connectionFactory.Open())
        {
            new MigrationRunner(NullLogger<MigrationRunner>.Instance).Apply(connection);
        }

        _photoRepository = new PhotoRepository(_connectionFactory);
        _folderRepository = new FolderRepository(_connectionFactory);
        _editRepository = new EditRepository(_connectionFactory);
        _jobRepository = new ExportJobRepository(_connectionFactory);

        _service = new ExportService(
            _jobRepository,
            _photoRepository,
            _editRepository,
            new WicExportPipeline(
                new OpenRetouch.Imaging.Raw.LibRawDecoder(
                    NullLogger<OpenRetouch.Imaging.Raw.LibRawDecoder>.Instance),
                NullLogger<WicExportPipeline>.Instance),
            Substitute.For<IJobQueue>(),
            NullLogger<ExportService>.Instance);
    }

    [Fact]
    public async Task BatchExport_PartialFailure_ThenRetryFailedOnly_Works()
    {
        // 準備: 正常画像2枚+破損画像1枚をカタログ登録、1枚に編集を保存
        var folder = new Folder
        {
            Id = Guid.NewGuid().ToString(),
            Path = _photosDir,
            Name = "photos",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _folderRepository.InsertAsync(folder);

        var good1 = await CreatePhotoAsync(folder.Id, "good1.jpg", 600, 400, valid: true);
        var good2 = await CreatePhotoAsync(folder.Id, "good2.png", 300, 300, valid: true);
        var broken = await CreatePhotoAsync(folder.Id, "broken.jpg", 0, 0, valid: false);
        await _photoRepository.InsertBatchAsync([good1, good2, broken]);

        var edit = new EditSettings();
        edit.Crop.Width = 0.5;
        edit.Crop.Height = 0.5;
        await _editRepository.UpsertCurrentAsync(good1.Id, edit);

        var settings = new ExportSettings
        {
            OutputFolder = _outDir,
            Format = ExportFormat.Jpeg,
            FileNameTemplate = "{filename}_export",
        };

        // 1回目: 一括書き出し(brokenは失敗するがジョブは完走)
        var jobId = await _service.EnqueueExportAsync([good1.Id, good2.Id, broken.Id], settings);
        await _service.RunExportJobAsync(
            jobId,
            (await _jobRepository.GetItemsAsync(jobId)).Select(i => (i.ItemId, i.PhotoId)).ToList(),
            settings,
            new NoopProgress(),
            CancellationToken.None);

        File.Exists(Path.Combine(_outDir, "good1_export.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(_outDir, "good2_export.jpg")).Should().BeTrue();

        // 編集(50%クロップ)が適用されている
        var (w, h) = await TestImageFactory.GetSizeAsync(Path.Combine(_outDir, "good1_export.jpg"));
        w.Should().Be(300);
        h.Should().Be(200);

        var items = await _service.GetJobItemsAsync(jobId);
        items.Should().HaveCount(3);
        items.Count(i => i.Status == "completed").Should().Be(2);
        var failedItem = items.Single(i => i.Status == "failed");
        failedItem.PhotoId.Should().Be(broken.Id);
        failedItem.ErrorMessage.Should().NotBeNullOrEmpty("失敗原因が記録される");

        // 2回目: 失敗のみ再実行(対象は破損画像1枚だけ→再び失敗として記録される)
        var retryJobId = await _service.RetryFailedItemsAsync(jobId);
        retryJobId.Should().NotBeNull();
        var retryItems = await _jobRepository.GetItemsAsync(retryJobId!);
        retryItems.Should().ContainSingle().Which.PhotoId.Should().Be(broken.Id);
    }

    private async Task<Photo> CreatePhotoAsync(string folderId, string fileName, int width, int height, bool valid)
    {
        var path = Path.Combine(_photosDir, fileName);
        if (valid)
        {
            await TestImageFactory.CreateAsync(path, width, height);
        }
        else
        {
            await File.WriteAllTextAsync(path, "not an image");
        }

        return new Photo
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = folderId,
            FilePath = path,
            FileName = fileName,
            FileExtension = Path.GetExtension(fileName).ToLowerInvariant(),
            ImportedAt = DateTimeOffset.UtcNow,
        };
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
