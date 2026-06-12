namespace OpenRetouch.Core.Abstractions.Repositories;

/// <summary>書き出しジョブアイテムのスナップショット。</summary>
public sealed record ExportJobItem(string ItemId, string PhotoId, string Status, string? OutputPath, string? ErrorMessage);

/// <summary>export_jobs / export_job_items テーブルへのアクセス。実装はCatalogレイヤー。</summary>
public interface IExportJobRepository
{
    Task CreateJobAsync(
        string jobId,
        string settingsJson,
        IReadOnlyList<(string ItemId, string PhotoId)> items,
        CancellationToken ct = default);

    Task UpdateJobStatusAsync(string jobId, string status, CancellationToken ct = default);

    Task UpdateItemAsync(
        string itemId,
        string status,
        string? outputPath,
        string? errorMessage,
        CancellationToken ct = default);

    Task<string?> GetJobSettingsJsonAsync(string jobId, CancellationToken ct = default);

    Task<IReadOnlyList<ExportJobItem>> GetItemsAsync(string jobId, CancellationToken ct = default);
}
