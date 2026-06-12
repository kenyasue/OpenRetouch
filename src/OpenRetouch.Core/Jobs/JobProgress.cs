namespace OpenRetouch.Core.Jobs;

/// <summary>ジョブの進捗スナップショット(UIへの通知用)。</summary>
public sealed record JobProgress(
    string JobId,
    string DisplayName,
    JobStatus Status,
    int Done,
    int Total);
