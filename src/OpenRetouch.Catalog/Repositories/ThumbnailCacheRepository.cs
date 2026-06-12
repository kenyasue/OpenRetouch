using Dapper;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Records;
using OpenRetouch.Core.Abstractions.Repositories;

namespace OpenRetouch.Catalog.Repositories;

/// <inheritdoc cref="IThumbnailCacheRepository"/>
public sealed class ThumbnailCacheRepository : IThumbnailCacheRepository
{
    private readonly ConnectionFactory _connectionFactory;

    public ThumbnailCacheRepository(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyDictionary<string, string>> GetAllThumbPathsAsync(CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyDictionary<string, string>>(() =>
        {
            using var connection = _connectionFactory.Open();
            var rows = connection.Query<(string PhotoId, string ThumbPath)>(
                "SELECT photo_id, thumb_path FROM thumbnail_cache");
            return rows.ToDictionary(r => r.PhotoId, r => r.ThumbPath);
        }, ct);
    }

    public Task RemoveAsync(IReadOnlyList<string> photoIds, CancellationToken ct = default)
    {
        if (photoIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            connection.Execute(
                "DELETE FROM thumbnail_cache WHERE photo_id IN @Ids", new { Ids = photoIds });
        }, ct);
    }

    public Task<long> CountPhotosWithoutThumbnailAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            return connection.ExecuteScalar<long>(
                """
                SELECT COUNT(*) FROM photos p
                WHERE NOT EXISTS (SELECT 1 FROM thumbnail_cache tc WHERE tc.photo_id = p.id)
                """);
        }, ct);
    }

    public Task RemoveAllAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            connection.Execute("DELETE FROM thumbnail_cache");
        }, ct);
    }

    public Task UpsertAsync(
        string photoId,
        string thumbPath,
        DateTimeOffset sourceModifiedAt,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            connection.Execute(
                """
                INSERT INTO thumbnail_cache (photo_id, thumb_path, source_modified_at, generated_at)
                VALUES (@PhotoId, @ThumbPath, @SourceModifiedAt, @GeneratedAt)
                ON CONFLICT(photo_id) DO UPDATE SET
                    thumb_path = excluded.thumb_path,
                    source_modified_at = excluded.source_modified_at,
                    generated_at = excluded.generated_at
                """,
                new
                {
                    PhotoId = photoId,
                    ThumbPath = thumbPath,
                    SourceModifiedAt = PhotoRow.FormatTimestamp(sourceModifiedAt),
                    GeneratedAt = PhotoRow.FormatTimestamp(DateTimeOffset.UtcNow),
                });
        }, ct);
    }
}
