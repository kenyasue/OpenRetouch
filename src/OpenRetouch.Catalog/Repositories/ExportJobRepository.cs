using Dapper;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Records;
using OpenRetouch.Core.Abstractions.Repositories;

namespace OpenRetouch.Catalog.Repositories;

/// <inheritdoc cref="IExportJobRepository"/>
public sealed class ExportJobRepository : IExportJobRepository
{
    private readonly ConnectionFactory _connectionFactory;

    public ExportJobRepository(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task CreateJobAsync(
        string jobId,
        string settingsJson,
        IReadOnlyList<(string ItemId, string PhotoId)> items,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var now = PhotoRow.FormatTimestamp(DateTimeOffset.UtcNow);
            using var connection = _connectionFactory.Open();
            using var transaction = connection.BeginTransaction();

            connection.Execute(
                """
                INSERT INTO export_jobs (id, settings_json, status, created_at)
                VALUES (@Id, @SettingsJson, 'pending', @Now)
                """,
                new { Id = jobId, SettingsJson = settingsJson, Now = now },
                transaction);

            foreach (var (itemId, photoId) in items)
            {
                connection.Execute(
                    """
                    INSERT INTO export_job_items (id, job_id, photo_id, status)
                    VALUES (@ItemId, @JobId, @PhotoId, 'pending')
                    """,
                    new { ItemId = itemId, JobId = jobId, PhotoId = photoId },
                    transaction);
            }

            transaction.Commit();
        }, ct);
    }

    public Task UpdateJobStatusAsync(string jobId, string status, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            var completedAt = status is "completed" or "failed" or "cancelled"
                ? PhotoRow.FormatTimestamp(DateTimeOffset.UtcNow)
                : null;
            connection.Execute(
                "UPDATE export_jobs SET status = @Status, completed_at = @CompletedAt WHERE id = @JobId",
                new { JobId = jobId, Status = status, CompletedAt = completedAt });
        }, ct);
    }

    public Task UpdateItemAsync(
        string itemId, string status, string? outputPath, string? errorMessage, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            connection.Execute(
                """
                UPDATE export_job_items
                SET status = @Status, output_path = @OutputPath, error_message = @ErrorMessage
                WHERE id = @ItemId
                """,
                new { ItemId = itemId, Status = status, OutputPath = outputPath, ErrorMessage = errorMessage });
        }, ct);
    }

    public Task<string?> GetJobSettingsJsonAsync(string jobId, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            return connection.QuerySingleOrDefault<string>(
                "SELECT settings_json FROM export_jobs WHERE id = @JobId", new { JobId = jobId });
        }, ct);
    }

    public Task<IReadOnlyList<ExportJobItem>> GetItemsAsync(string jobId, CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<ExportJobItem>>(() =>
        {
            using var connection = _connectionFactory.Open();
            var rows = connection.Query(
                """
                SELECT id, photo_id, status, output_path, error_message
                FROM export_job_items WHERE job_id = @JobId
                """,
                new { JobId = jobId });
            return rows.Select(r => new ExportJobItem(
                (string)r.id, (string)r.photo_id, (string)r.status,
                (string?)r.output_path, (string?)r.error_message)).ToList();
        }, ct);
    }
}
