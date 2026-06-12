using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Export;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using Xunit;

namespace OpenRetouch.Core.Tests.Services;

public sealed class ExportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    private readonly IExportJobRepository _jobRepository = Substitute.For<IExportJobRepository>();
    private readonly IPhotoRepository _photoRepository = Substitute.For<IPhotoRepository>();
    private readonly IEditRepository _editRepository = Substitute.For<IEditRepository>();
    private readonly IExportPipeline _pipeline = Substitute.For<IExportPipeline>();
    private readonly IJobQueue _jobQueue = Substitute.For<IJobQueue>();
    private readonly ExportService _service;

    public ExportServiceTests()
    {
        Directory.CreateDirectory(_root);
        _editRepository.GetCurrentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((EditSettings?)null);
        _service = new ExportService(
            _jobRepository, _photoRepository, _editRepository, _pipeline, _jobQueue,
            NullLogger<ExportService>.Instance);
    }

    [Fact]
    public async Task EnqueueExportAsync_CreatesJobAndEnqueues()
    {
        var jobId = await _service.EnqueueExportAsync(["p1", "p2"], CreateSettings());

        jobId.Should().NotBeNullOrEmpty();
        await _jobRepository.Received(1).CreateJobAsync(
            jobId,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<(string, string)>>(items => items.Count == 2),
            Arg.Any<CancellationToken>());
        _jobQueue.Received(1).Enqueue(Arg.Any<IJob>());
    }

    [Fact]
    public async Task EnqueueExportAsync_EmptyPhotoIds_Throws()
    {
        var act = () => _service.EnqueueExportAsync([], CreateSettings());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunExportJob_AllSucceed_ReportsSummary()
    {
        SetupPhotos("p1", "p2");
        ExportJobSummary? summary = null;
        _service.ExportCompleted += (_, s) => summary = s;

        await _service.RunExportJobAsync(
            "job1", [("i1", "p1"), ("i2", "p2")], CreateSettings(), new NoopProgress(), CancellationToken.None);

        summary.Should().NotBeNull();
        summary!.Succeeded.Should().Be(2);
        summary.Failed.Should().Be(0);
        await _jobRepository.Received(1).UpdateJobStatusAsync("job1", "completed", Arg.Any<CancellationToken>());
        await _jobRepository.Received(2).UpdateItemAsync(
            Arg.Any<string>(),
            Arg.Is("completed"),
            Arg.Any<string?>(),
            Arg.Is((string?)null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunExportJob_OneFails_ContinuesAndRecordsError()
    {
        SetupPhotos("p1", "p2");
        _pipeline.ExportAsync(
                Arg.Is<Photo>(p => p.Id == "p1"), Arg.Any<EditSettings>(), Arg.Any<ExportSettings>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("デコード失敗"));
        ExportJobSummary? summary = null;
        _service.ExportCompleted += (_, s) => summary = s;

        await _service.RunExportJobAsync(
            "job1", [("i1", "p1"), ("i2", "p2")], CreateSettings(), new NoopProgress(), CancellationToken.None);

        summary!.Succeeded.Should().Be(1);
        summary.Failed.Should().Be(1);
        await _jobRepository.Received(1).UpdateItemAsync(
            "i1", "failed", null, "デコード失敗", Arg.Any<CancellationToken>());
        await _jobRepository.Received(1).UpdateJobStatusAsync("job1", "failed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunExportJob_PhotoMissingFromCatalog_FailsItem()
    {
        SetupPhotos("p1");
        ExportJobSummary? summary = null;
        _service.ExportCompleted += (_, s) => summary = s;

        await _service.RunExportJobAsync(
            "job1", [("i1", "p1"), ("i2", "missing")], CreateSettings(), new NoopProgress(), CancellationToken.None);

        summary!.Succeeded.Should().Be(1);
        summary.Failed.Should().Be(1);
    }

    [Fact]
    public async Task RunExportJob_Cancelled_UpdatesStatusToCancelled()
    {
        SetupPhotos("p1", "p2");
        using var cts = new CancellationTokenSource();
        // 1枚目の書き出し中にキャンセルを発火させる
        _pipeline.ExportAsync(
                Arg.Any<Photo>(), Arg.Any<EditSettings>(), Arg.Any<ExportSettings>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                cts.Cancel();
                await Task.Yield();
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
            });

        var act = () => _service.RunExportJobAsync(
            "job1", [("i1", "p1"), ("i2", "p2")], CreateSettings(), new NoopProgress(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _jobRepository.Received(1).UpdateJobStatusAsync(
            "job1", "cancelled", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryFailedItemsAsync_NoFailedItems_ReturnsNull()
    {
        _jobRepository.GetJobSettingsJsonAsync("job1", Arg.Any<CancellationToken>())
            .Returns("""{"outputFolder":"C:\\out"}""");
        _jobRepository.GetItemsAsync("job1", Arg.Any<CancellationToken>())
            .Returns(new List<ExportJobItem> { new("i1", "p1", "completed", @"C:\out\a.jpg", null) });

        var result = await _service.RetryFailedItemsAsync("job1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RetryFailedItemsAsync_WithFailedItems_EnqueuesNewJob()
    {
        _jobRepository.GetJobSettingsJsonAsync("job1", Arg.Any<CancellationToken>())
            .Returns("""{"outputFolder":"C:\\out","format":"jpeg"}""");
        _jobRepository.GetItemsAsync("job1", Arg.Any<CancellationToken>())
            .Returns(new List<ExportJobItem>
            {
                new("i1", "p1", "failed", null, "error"),
                new("i2", "p2", "completed", @"C:\out\b.jpg", null),
            });

        var newJobId = await _service.RetryFailedItemsAsync("job1");

        newJobId.Should().NotBeNull();
        await _jobRepository.Received(1).CreateJobAsync(
            newJobId!,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<(string, string)>>(items =>
                items.Count == 1 && items[0].Item2 == "p1"),
            Arg.Any<CancellationToken>());
    }

    private void SetupPhotos(params string[] photoIds)
    {
        var photos = photoIds.Select(id => new Photo
        {
            Id = id,
            FolderId = "f1",
            FilePath = Path.Combine(_root, id + "_src.jpg"),
            FileName = id + "_src.jpg",
            FileExtension = ".jpg",
            ImportedAt = DateTimeOffset.UtcNow,
        }).ToList();
        _photoRepository.GetByIdsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ids = callInfo.Arg<IReadOnlyList<string>>();
                return (IReadOnlyList<Photo>)photos.Where(p => ids.Contains(p.Id)).ToList();
            });
    }

    private ExportSettings CreateSettings() => new()
    {
        OutputFolder = Path.Combine(_root, "out"),
    };

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
