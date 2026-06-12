using Dapper;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Records;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Editing;

namespace OpenRetouch.Catalog.Repositories;

/// <summary>
/// editsテーブルへのアクセス。
/// M3では写真ごとに現行1レコード(version=1, is_current=1)で運用する(履歴はM4以降)。
/// </summary>
public sealed class EditRepository : IEditRepository
{
    private const int CurrentVersion = 1;

    private readonly ConnectionFactory _connectionFactory;

    public EditRepository(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<EditSettings?> GetCurrentAsync(string photoId, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            var json = connection.QuerySingleOrDefault<string>(
                "SELECT edit_json FROM edits WHERE photo_id = @PhotoId AND is_current = 1",
                new { PhotoId = photoId });
            return json is null ? null : EditSettingsSerializer.Deserialize(json);
        }, ct);
    }

    public Task UpsertCurrentAsync(string photoId, EditSettings settings, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var now = PhotoRow.FormatTimestamp(DateTimeOffset.UtcNow);
            using var connection = _connectionFactory.Open();
            connection.Execute(
                """
                INSERT INTO edits (id, photo_id, version, edit_json, is_current, created_at, updated_at)
                VALUES (@Id, @PhotoId, @Version, @EditJson, 1, @Now, @Now)
                ON CONFLICT(photo_id, version) DO UPDATE SET
                    edit_json = excluded.edit_json,
                    updated_at = excluded.updated_at
                """,
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    PhotoId = photoId,
                    Version = CurrentVersion,
                    EditJson = EditSettingsSerializer.Serialize(settings),
                    Now = now,
                });
        }, ct);
    }

    public Task<IReadOnlyCollection<string>> GetEditedPhotoIdsAsync(CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyCollection<string>>(() =>
        {
            using var connection = _connectionFactory.Open();
            var ids = connection.Query<string>("SELECT DISTINCT photo_id FROM edits WHERE is_current = 1");
            return ids.ToHashSet();
        }, ct);
    }
}
