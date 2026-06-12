using Dapper;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Records;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Catalog.Repositories;

/// <inheritdoc cref="IAlbumRepository"/>
public sealed class AlbumRepository : IAlbumRepository
{
    private readonly ConnectionFactory _connectionFactory;

    public AlbumRepository(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<Album>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<Album>>(() =>
        {
            using var connection = _connectionFactory.Open();
            var rows = connection.Query(
                "SELECT id, name, sort_order, created_at, updated_at FROM albums ORDER BY sort_order, name COLLATE NOCASE");
            return rows.Select(r => (Album)MapAlbum(r)).ToList();
        }, ct);
    }

    public Task<Album> InsertAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Task.Run(() =>
        {
            var now = DateTimeOffset.UtcNow;
            var album = new Album
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                SortOrder = 0,
                CreatedAt = now,
                UpdatedAt = now,
            };

            using var connection = _connectionFactory.Open();
            connection.Execute(
                """
                INSERT INTO albums (id, name, sort_order, created_at, updated_at)
                VALUES (@Id, @Name, @SortOrder, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    album.Id,
                    album.Name,
                    album.SortOrder,
                    CreatedAt = PhotoRow.FormatTimestamp(album.CreatedAt),
                    UpdatedAt = PhotoRow.FormatTimestamp(album.UpdatedAt),
                });
            return album;
        }, ct);
    }

    public Task DeleteAsync(string albumId, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            using var transaction = connection.BeginTransaction();
            connection.Execute(
                "DELETE FROM photo_album_map WHERE album_id = @AlbumId", new { AlbumId = albumId }, transaction);
            connection.Execute(
                "DELETE FROM albums WHERE id = @AlbumId", new { AlbumId = albumId }, transaction);
            transaction.Commit();
        }, ct);
    }

    public Task AddPhotosAsync(string albumId, IReadOnlyList<string> photoIds, CancellationToken ct = default)
    {
        if (photoIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            var addedAt = PhotoRow.FormatTimestamp(DateTimeOffset.UtcNow);
            using var connection = _connectionFactory.Open();
            using var transaction = connection.BeginTransaction();
            foreach (var photoId in photoIds)
            {
                connection.Execute(
                    """
                    INSERT OR IGNORE INTO photo_album_map (photo_id, album_id, added_at)
                    VALUES (@PhotoId, @AlbumId, @AddedAt)
                    """,
                    new { PhotoId = photoId, AlbumId = albumId, AddedAt = addedAt },
                    transaction);
            }

            transaction.Commit();
        }, ct);
    }

    public Task RemovePhotosAsync(string albumId, IReadOnlyList<string> photoIds, CancellationToken ct = default)
    {
        if (photoIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            connection.Execute(
                "DELETE FROM photo_album_map WHERE album_id = @AlbumId AND photo_id IN @PhotoIds",
                new { AlbumId = albumId, PhotoIds = photoIds });
        }, ct);
    }

    private static Album MapAlbum(dynamic row) => new()
    {
        Id = (string)row.id,
        Name = (string)row.name,
        SortOrder = (int)(long)row.sort_order,
        CreatedAt = PhotoRow.ParseTimestamp((string)row.created_at) ?? DateTimeOffset.MinValue,
        UpdatedAt = PhotoRow.ParseTimestamp((string)row.updated_at) ?? DateTimeOffset.MinValue,
    };
}
