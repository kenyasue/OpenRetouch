namespace OpenRetouch.Core.Jobs;

/// <summary>
/// バックグラウンドジョブのキュー。並列度を制御してジョブを実行し、進捗をイベントで通知する。
/// </summary>
public interface IJobQueue
{
    /// <summary>ジョブを投入し、ジョブIDを返す。</summary>
    string Enqueue(IJob job);

    /// <summary>ジョブをキャンセルする(未実行ならPendingから除去、実行中ならトークンで停止)。</summary>
    void Cancel(string jobId);

    /// <summary>
    /// 進捗通知。ワーカースレッドから発火されるため、UIスレッドへのマーシャリングは購読側の責務。
    /// </summary>
    event EventHandler<JobProgress>? ProgressChanged;
}
