using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Jobs;
using Xunit;

namespace OpenRetouch.Core.Tests.Jobs;

public sealed class JobQueueTests : IDisposable
{
    private readonly JobQueue _queue = new(NullLogger<JobQueue>.Instance, maxConcurrency: 2);

    [Fact]
    public async Task Enqueue_SuccessfulJob_ReportsCompletedStatus()
    {
        var statuses = new List<JobStatus>();
        var completed = new TaskCompletionSource();
        _queue.ProgressChanged += (_, p) =>
        {
            lock (statuses)
            {
                statuses.Add(p.Status);
            }

            if (p.Status is JobStatus.Completed or JobStatus.Failed)
            {
                completed.TrySetResult();
            }
        };

        _queue.Enqueue(new DelegateJob("test", (progress, _) =>
        {
            progress.Report((1, 1));
            return Task.CompletedTask;
        }));

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        statuses.Should().Contain(JobStatus.Running);
        statuses.Last().Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task Enqueue_ThrowingJob_ReportsFailedStatus()
    {
        var failed = new TaskCompletionSource<JobStatus>();
        _queue.ProgressChanged += (_, p) =>
        {
            if (p.Status is JobStatus.Completed or JobStatus.Failed)
            {
                failed.TrySetResult(p.Status);
            }
        };

        _queue.Enqueue(new DelegateJob("failing", (_, _) => throw new InvalidOperationException("boom")));

        var status = await failed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task Cancel_RunningJob_ReportsCancelledStatus()
    {
        var started = new TaskCompletionSource();
        var finished = new TaskCompletionSource<JobStatus>();
        _queue.ProgressChanged += (_, p) =>
        {
            if (p.Status is JobStatus.Cancelled or JobStatus.Completed or JobStatus.Failed)
            {
                finished.TrySetResult(p.Status);
            }
        };

        var jobId = _queue.Enqueue(new DelegateJob("cancellable", async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }));

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _queue.Cancel(jobId);

        var status = await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public async Task Enqueue_ManyJobs_RespectsMaxConcurrency()
    {
        var current = 0;
        var maxObserved = 0;
        var gate = new object();
        var allDone = new CountdownEvent(5);

        for (var i = 0; i < 5; i++)
        {
            _queue.Enqueue(new DelegateJob($"job{i}", async (_, _) =>
            {
                lock (gate)
                {
                    current++;
                    maxObserved = Math.Max(maxObserved, current);
                }

                await Task.Delay(100);

                lock (gate)
                {
                    current--;
                }

                allDone.Signal();
            }));
        }

        await Task.Run(() => allDone.Wait(TimeSpan.FromSeconds(30)));
        maxObserved.Should().BeLessThanOrEqualTo(2);
    }

    public void Dispose() => _queue.Dispose();
}
