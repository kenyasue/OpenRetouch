namespace OpenRetouch.Core.Jobs;

/// <summary>バックグラウンドジョブの実行単位。</summary>
public interface IJob
{
    string Id { get; }

    string DisplayName { get; }

    /// <summary>ジョブ本体。進捗は(done, total)で報告する。</summary>
    Task ExecuteAsync(IProgress<(int Done, int Total)> progress, CancellationToken ct);
}
