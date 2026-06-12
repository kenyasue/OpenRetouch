using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Export;
using OpenRetouch.Core.Jobs;

namespace OpenRetouch.Core.Services;

/// <inheritdoc cref="IExportService"/>
public sealed class ExportService : IExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IExportJobRepository _jobRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IEditRepository _editRepository;
    private readonly IExportPipeline _pipeline;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<ExportService> _logger;

    /// <summary>書き出しジョブID→キュージョブID(キャンセル用)。</summary>
    private readonly ConcurrentDictionary<string, string> _queueJobIds = new();

    public ExportService(
        IExportJobRepository jobRepository,
        IPhotoRepository photoRepository,
        IEditRepository editRepository,
        IExportPipeline pipeline,
        IJobQueue jobQueue,
        ILogger<ExportService> logger)
    {
        _jobRepository = jobRepository;
        _photoRepository = photoRepository;
        _editRepository = editRepository;
        _pipeline = pipeline;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public event EventHandler<ExportJobSummary>? ExportCompleted;

    public async Task<string> EnqueueExportAsync(
        IReadOnlyList<string> photoIds, ExportSettings settings, CancellationToken ct = default)
    {
        if (photoIds.Count == 0)
        {
            throw new ArgumentException("There are no photos to export.", nameof(photoIds));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(settings.OutputFolder);

        var jobId = Guid.NewGuid().ToString();
        var items = photoIds
            .Select(photoId => (ItemId: Guid.NewGuid().ToString(), PhotoId: photoId))
            .ToList();

        await _jobRepository.CreateJobAsync(
            jobId, JsonSerializer.Serialize(settings, JsonOptions), items, ct);

        var job = new DelegateJob(
            $"Export ({items.Count} photos)",
            (progress, jobCt) => RunExportJobAsync(jobId, items, settings.Clone(), progress, jobCt));
        var queueJobId = _jobQueue.Enqueue(job);
        _queueJobIds[jobId] = queueJobId;

        return jobId;
    }

    public async Task<string?> RetryFailedItemsAsync(string jobId, CancellationToken ct = default)
    {
        var settingsJson = await _jobRepository.GetJobSettingsJsonAsync(jobId, ct)
            ?? throw new InvalidOperationException($"Export job not found: {jobId}");
        var items = await _jobRepository.GetItemsAsync(jobId, ct);
        var failedPhotoIds = items.Where(i => i.Status == ExportItemStatus.Failed).Select(i => i.PhotoId).ToList();
        if (failedPhotoIds.Count == 0)
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<ExportSettings>(settingsJson, JsonOptions)
            ?? throw new JsonException("Export settings deserialized to null");
        return await EnqueueExportAsync(failedPhotoIds, settings, ct);
    }

    public void Cancel(string jobId)
    {
        if (_queueJobIds.TryGetValue(jobId, out var queueJobId))
        {
            _jobQueue.Cancel(queueJobId);
        }
    }

    public Task<IReadOnlyList<ExportJobItem>> GetJobItemsAsync(string jobId, CancellationToken ct = default) =>
        _jobRepository.GetItemsAsync(jobId, ct);

    internal async Task RunExportJobAsync(
        string jobId,
        IReadOnlyList<(string ItemId, string PhotoId)> items,
        ExportSettings settings,
        IProgress<(int Done, int Total)> progress,
        CancellationToken ct)
    {
        await _jobRepository.UpdateJobStatusAsync(jobId, ExportJobStatus.Running, CancellationToken.None);

        var photos = (await _photoRepository.GetByIdsAsync(items.Select(i => i.PhotoId).ToList(), ct))
            .ToDictionary(p => p.Id);

        var succeeded = 0;
        var failed = 0;
        var skipped = 0;
        var done = 0;

        try
        {
            var sequence = 0;
            foreach (var (itemId, photoId) in items)
            {
                ct.ThrowIfCancellationRequested();
                sequence++;

                try
                {
                    if (!photos.TryGetValue(photoId, out var photo))
                    {
                        throw new InvalidOperationException("Photo not found in the catalog.");
                    }

                    var edit = await _editRepository.GetCurrentAsync(photoId, ct) ?? new EditSettings();
                    var fileName = FileNameTemplate.Expand(settings.FileNameTemplate, photo, sequence)
                        + settings.FileExtension;
                    var outputPath = Path.Combine(settings.OutputFolder, fileName);
                    var resolvedPath = FileNameTemplate.ResolveConflict(outputPath, settings.Conflict);

                    if (resolvedPath is null)
                    {
                        skipped++;
                        await _jobRepository.UpdateItemAsync(
                            itemId, ExportItemStatus.Skipped, null, "Skipped because a file with the same name already exists", CancellationToken.None);
                    }
                    else
                    {
                        await _pipeline.ExportAsync(photo, edit, settings, resolvedPath, ct);
                        succeeded++;
                        await _jobRepository.UpdateItemAsync(
                            itemId, ExportItemStatus.Completed, resolvedPath, null, CancellationToken.None);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // アイテム単位の失敗はジョブを止めない
                    failed++;
                    _logger.LogError(ex, "Export item failed: photo={PhotoId}", photoId);
                    // COMException等はMessageが空のことがあるため型名でフォールバック
                    var message = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
                    await _jobRepository.UpdateItemAsync(
                        itemId, ExportItemStatus.Failed, null, message, CancellationToken.None);
                }

                done++;
                progress.Report((done, items.Count));
            }

            await _jobRepository.UpdateJobStatusAsync(
                jobId, failed > 0 ? ExportJobStatus.Failed : ExportJobStatus.Completed, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await _jobRepository.UpdateJobStatusAsync(jobId, ExportJobStatus.Cancelled, CancellationToken.None);
            throw;
        }
        finally
        {
            _queueJobIds.TryRemove(jobId, out _);
            ExportCompleted?.Invoke(
                this, new ExportJobSummary(jobId, items.Count, succeeded, failed, skipped));
        }
    }
}
