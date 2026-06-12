using Dapper;
using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class FolderDeleteCascadeTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly FolderRepository _repository;

    public FolderDeleteCascadeTests()
    {
        _repository = new FolderRepository(_db.ConnectionFactory);
    }

    [Fact]
    public async Task DeleteCascadeAsync_RemovesAllRelatedRows_AndReturnsThumbPaths()
    {
        SeedFolderWithPhoto(folderId: "folder-a", photoId: "photo-a", thumbPath: @"C:\thumbs\a.jpg");
        SeedFolderWithPhoto(folderId: "folder-b", photoId: "photo-b", thumbPath: @"C:\thumbs\b.jpg");

        var thumbPaths = await _repository.DeleteCascadeAsync("folder-a");

        thumbPaths.Should().Equal(@"C:\thumbs\a.jpg");

        using var connection = _db.ConnectionFactory.Open();
        Count(connection, "folders", "id = 'folder-a'").Should().Be(0);
        Count(connection, "photos", "folder_id = 'folder-a'").Should().Be(0);
        Count(connection, "edits", "photo_id = 'photo-a'").Should().Be(0);
        Count(connection, "thumbnail_cache", "photo_id = 'photo-a'").Should().Be(0);
        Count(connection, "photo_album_map", "photo_id = 'photo-a'").Should().Be(0);
        Count(connection, "export_job_items", "photo_id = 'photo-a'").Should().Be(0);
        Count(connection, "export_jobs", "id = 'job-photo-a'").Should().Be(0, "アイテムが全て消えたジョブは削除される");

        // 他フォルダは無傷
        Count(connection, "folders", "id = 'folder-b'").Should().Be(1);
        Count(connection, "photos", "id = 'photo-b'").Should().Be(1);
        Count(connection, "edits", "photo_id = 'photo-b'").Should().Be(1);
        Count(connection, "thumbnail_cache", "photo_id = 'photo-b'").Should().Be(1);
        Count(connection, "photo_album_map", "photo_id = 'photo-b'").Should().Be(1);
        Count(connection, "export_job_items", "photo_id = 'photo-b'").Should().Be(1);
        Count(connection, "export_jobs", "id = 'job-photo-b'").Should().Be(1);
    }

    [Fact]
    public async Task DeleteCascadeAsync_FolderWithoutThumbnails_ReturnsEmpty()
    {
        using (var connection = _db.ConnectionFactory.Open())
        {
            connection.Execute(
                """
                INSERT INTO folders (id, path, name, created_at)
                VALUES ('folder-empty', 'C:\Empty', 'Empty', '2026-06-10T00:00:00Z')
                """);
        }

        var thumbPaths = await _repository.DeleteCascadeAsync("folder-empty");

        thumbPaths.Should().BeEmpty();
        var loaded = await _repository.GetByPathAsync(@"C:\Empty");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCascadeAsync_UnknownFolder_DoesNothing()
    {
        SeedFolderWithPhoto(folderId: "folder-a", photoId: "photo-a", thumbPath: @"C:\thumbs\a.jpg");

        var thumbPaths = await _repository.DeleteCascadeAsync("no-such-folder");

        thumbPaths.Should().BeEmpty();
        using var connection = _db.ConnectionFactory.Open();
        Count(connection, "photos", "id = 'photo-a'").Should().Be(1);
    }

    /// <summary>フォルダ1つ+写真1枚+編集・サムネイル・アルバム所属・書き出し履歴をシードする。</summary>
    private void SeedFolderWithPhoto(string folderId, string photoId, string thumbPath)
    {
        using var connection = _db.ConnectionFactory.Open();
        var now = "2026-06-10T00:00:00Z";

        connection.Execute(
            "INSERT INTO folders (id, path, name, created_at) VALUES (@Id, @Path, @Id, @Now)",
            new { Id = folderId, Path = @"C:\" + folderId, Now = now });
        connection.Execute(
            """
            INSERT INTO photos (id, folder_id, file_path, file_name, file_extension, imported_at)
            VALUES (@Id, @FolderId, @Path, @Id, '.jpg', @Now)
            """,
            new { Id = photoId, FolderId = folderId, Path = @"C:\" + folderId + @"\" + photoId + ".jpg", Now = now });
        connection.Execute(
            """
            INSERT INTO edits (id, photo_id, version, edit_json, is_current, created_at, updated_at)
            VALUES (@Id, @PhotoId, 1, '{}', 1, @Now, @Now)
            """,
            new { Id = "edit-" + photoId, PhotoId = photoId, Now = now });
        connection.Execute(
            """
            INSERT INTO thumbnail_cache (photo_id, thumb_path, source_modified_at, generated_at)
            VALUES (@PhotoId, @ThumbPath, @Now, @Now)
            """,
            new { PhotoId = photoId, ThumbPath = thumbPath, Now = now });
        connection.Execute(
            """
            INSERT INTO albums (id, name, created_at, updated_at)
            VALUES (@Id, @Id, @Now, @Now)
            """,
            new { Id = "album-" + photoId, Now = now });
        connection.Execute(
            """
            INSERT INTO photo_album_map (photo_id, album_id, added_at)
            VALUES (@PhotoId, @AlbumId, @Now)
            """,
            new { PhotoId = photoId, AlbumId = "album-" + photoId, Now = now });
        connection.Execute(
            """
            INSERT INTO export_jobs (id, settings_json, status, created_at)
            VALUES (@Id, '{}', 'completed', @Now)
            """,
            new { Id = "job-" + photoId, Now = now });
        connection.Execute(
            """
            INSERT INTO export_job_items (id, job_id, photo_id, status)
            VALUES (@Id, @JobId, @PhotoId, 'completed')
            """,
            new { Id = "item-" + photoId, JobId = "job-" + photoId, PhotoId = photoId });
    }

    private static long Count(System.Data.IDbConnection connection, string table, string where) =>
        connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    public void Dispose() => _db.Dispose();
}
