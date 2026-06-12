using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Export;

namespace OpenRetouch.Core.Services;

/// <summary>書き出しジョブの完了サマリ。</summary>
public sealed record ExportJobSummary(string JobId, int Total, int Succeeded, int Failed, int Skipped);

/// <summary>一括書き出しの管理(キュー投入・キャンセル・失敗再実行)。</summary>
public interface IExportService
{
    /// <summary>一括書き出しジョブを投入する。戻り値は書き出しジョブID。</summary>
    Task<string> EnqueueExportAsync(
        IReadOnlyList<string> photoIds, ExportSettings settings, CancellationToken ct = default);

    /// <summary>
    /// 失敗アイテムのみを同じ設定で再実行する新規ジョブを投入する。
    /// 失敗アイテムがない場合はnull。
    /// </summary>
    Task<string?> RetryFailedItemsAsync(string jobId, CancellationToken ct = default);

    /// <summary>実行中の書き出しジョブをキャンセルする。</summary>
    void Cancel(string jobId);

    /// <summary>ジョブのアイテム一覧(結果表示用)。</summary>
    Task<IReadOnlyList<ExportJobItem>> GetJobItemsAsync(string jobId, CancellationToken ct = default);

    /// <summary>ジョブ完了時に発火(ワーカースレッドから)。</summary>
    event EventHandler<ExportJobSummary>? ExportCompleted;
}
