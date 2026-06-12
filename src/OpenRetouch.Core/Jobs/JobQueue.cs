using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OpenRetouch.Core.Jobs;

/// <inheritdoc cref="IJobQueue"/>
public sealed class JobQueue : IJobQueue, IDisposable
{
    private static readonly TimeSpan ProgressThrottleInterval = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();
    private readonly ILogger<JobQueue> _logger;

    public JobQueue(ILogger<JobQueue> logger, int maxConcurrency = 2)
    {
        _logger = logger;
        _concurrencyGate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public event EventHandler<JobProgress>? ProgressChanged;

    public string Enqueue(IJob job)
    {
        var cts = new CancellationTokenSource();
        _cancellations[job.Id] = cts;

        RaiseProgress(new JobProgress(job.Id, job.DisplayName, JobStatus.Pending, 0, 0));

        _ = Task.Run(() => RunJobAsync(job, cts.Token));
        return job.Id;
    }

    public void Cancel(string jobId)
    {
        if (_cancellations.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
        }
    }

    private async Task RunJobAsync(IJob job, CancellationToken ct)
    {
        try
        {
            await _concurrencyGate.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            FinishJob(job, JobStatus.Cancelled, 0, 0);
            return;
        }

        var lastReport = DateTimeOffset.MinValue;
        var lastDone = 0;
        var lastTotal = 0;

        try
        {
            RaiseProgress(new JobProgress(job.Id, job.DisplayName, JobStatus.Running, 0, 0));

            // Progress<T>はSynchronizationContextに非同期ポストするため、
            // 報告順序を保証する同期実装を使う(発火元はワーカースレッド)
            var progress = new SynchronousProgress<(int Done, int Total)>(p =>
            {
                lastDone = p.Done;
                lastTotal = p.Total;
                var now = DateTimeOffset.UtcNow;
                if (now - lastReport >= ProgressThrottleInterval || p.Done == p.Total)
                {
                    lastReport = now;
                    RaiseProgress(new JobProgress(job.Id, job.DisplayName, JobStatus.Running, p.Done, p.Total));
                }
            });

            await job.ExecuteAsync(progress, ct);
            FinishJob(job, JobStatus.Completed, lastDone, lastTotal);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job cancelled: {Name} ({Id})", job.DisplayName, job.Id);
            FinishJob(job, JobStatus.Cancelled, lastDone, lastTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job failed: {Name} ({Id})", job.DisplayName, job.Id);
            FinishJob(job, JobStatus.Failed, lastDone, lastTotal);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    private void FinishJob(IJob job, JobStatus status, int done, int total)
    {
        if (_cancellations.TryRemove(job.Id, out var cts))
        {
            cts.Dispose();
        }

        RaiseProgress(new JobProgress(job.Id, job.DisplayName, status, done, total));
    }

    private void RaiseProgress(JobProgress progress) => ProgressChanged?.Invoke(this, progress);

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }

    public void Dispose()
    {
        foreach (var cts in _cancellations.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _cancellations.Clear();
        _concurrencyGate.Dispose();
    }
}
