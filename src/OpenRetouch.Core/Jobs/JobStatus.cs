namespace OpenRetouch.Core.Jobs;

/// <summary>ジョブの状態。</summary>
public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
}
