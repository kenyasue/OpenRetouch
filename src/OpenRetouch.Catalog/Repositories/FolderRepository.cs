using Dapper;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Records;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Catalog.Repositories;

/// <inheritdoc cref="IFolderRepository"/>
public sealed class FolderRepository : IFolderRepository
{
    private readonly ConnectionFactory _connectionFactory;

    public FolderRepository(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<Folder>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<Folder>>(() =>
        {
            using var connection = _connectionFactory.Open();
            var rows = connection.Query(
                "SELECT id, path, name, parent_id, created_at FROM folders ORDER BY name COLLATE NOCASE");
            return rows.Select(r => (Folder)MapFolder(r)).ToList();
        }, ct);
    }

    public Task<Folder?> GetByPathAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            // Windowsのパスは大文字小文字を区別しないため、照合もNOCASEで行う
            var row = connection.QuerySingleOrDefault(
                "SELECT id, path, name, parent_id, created_at FROM folders WHERE path = @Path COLLATE NOCASE",
                new { Path = path });
            return row is null ? (Folder?)null : (Folder)MapFolder(row);
        }, ct);
    }

    private static Folder MapFolder(dynamic row) => new()
    {
        Id = (string)row.id,
        Path = (string)row.path,
        Name = (string)row.name,
        ParentId = (string?)row.parent_id,
        CreatedAt = PhotoRow.ParseTimestamp((string)row.created_at) ?? DateTimeOffset.MinValue,
    };

    public Task<IReadOnlyList<string>> DeleteCascadeAsync(string folderId, CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<string>>(() =>
        {
            using var connection = _connectionFactory.Open();
            using var transaction = connection.BeginTransaction();

            var folderParam = new { FolderId = folderId };
            var thumbPaths = connection.Query<string>(
                """
                SELECT thumb_path FROM thumbnail_cache
                WHERE photo_id IN (SELECT id FROM photos WHERE folder_id = @FolderId)
                """,
                folderParam, transaction).ToList();

            connection.Execute(
                """
                DELETE FROM thumbnail_cache
                WHERE photo_id IN (SELECT id FROM photos WHERE folder_id = @FolderId)
                """,
                folderParam, transaction);
            connection.Execute(
                """
                DELETE FROM edits
                WHERE photo_id IN (SELECT id FROM photos WHERE folder_id = @FolderId)
                """,
                folderParam, transaction);
            connection.Execute(
                """
                DELETE FROM photo_album_map
                WHERE photo_id IN (SELECT id FROM photos WHERE folder_id = @FolderId)
                """,
                folderParam, transaction);
            connection.Execute(
                """
                DELETE FROM export_job_items
                WHERE photo_id IN (SELECT id FROM photos WHERE folder_id = @FolderId)
                """,
                folderParam, transaction);

            // 全アイテムが消えた書き出しジョブは履歴として意味を持たないため一緒に削除する
            connection.Execute(
                """
                DELETE FROM export_jobs
                WHERE id NOT IN (SELECT DISTINCT job_id FROM export_job_items)
                """,
                transaction: transaction);

            connection.Execute(
                "DELETE FROM photos WHERE folder_id = @FolderId", folderParam, transaction);
            connection.Execute(
                "DELETE FROM folders WHERE id = @FolderId", folderParam, transaction);

            transaction.Commit();
            return thumbPaths;
        }, ct);
    }

    public Task InsertAsync(Folder folder, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            connection.Execute(
                """
                INSERT OR IGNORE INTO folders (id, path, name, parent_id, created_at)
                VALUES (@Id, @Path, @Name, @ParentId, @CreatedAt)
                """,
                new
                {
                    folder.Id,
                    folder.Path,
                    folder.Name,
                    folder.ParentId,
                    CreatedAt = PhotoRow.FormatTimestamp(folder.CreatedAt),
                });
        }, ct);
    }
}
