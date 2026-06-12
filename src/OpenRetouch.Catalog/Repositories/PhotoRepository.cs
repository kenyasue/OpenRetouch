using Dapper;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Records;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Catalog.Repositories;

/// <inheritdoc cref="IPhotoRepository"/>
public sealed class PhotoRepository : IPhotoRepository
{
    private const string SelectColumns = """
        SELECT id, folder_id, file_path, file_name, file_extension, file_size,
               imported_at, captured_at, width, height, orientation,
               camera_make, camera_model, lens_model, iso, aperture, shutter_speed, focal_length,
               rating, flag, color_label, is_missing
        FROM photos
        """;

    private readonly ConnectionFactory _connectionFactory;

    public PhotoRepository(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<Photo>> QueryAsync(PhotoQuery query, CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<Photo>>(() =>
        {
            var (sql, parameters) = BuildQuerySql(query);
            using var connection = _connectionFactory.Open();
            var rows = connection.Query<PhotoRow>(sql, parameters);
            var photos = rows.Select(r => r.ToModel()).ToList();

            if (query.ExcludeJpegWithRawPair)
            {
                // RAW+JPG同時記録ペアのJPGを除外する。
                // SQLの相関サブクエリは大規模カタログでO(n^2)になるため、
                // RAWのキー集合(フォルダ+ベース名)をC#側で構築してO(n)でフィルターする
                var rawSql = "SELECT folder_id, file_name, file_extension FROM photos WHERE file_extension IN @RawExtensions";
                if (query.FolderId is not null)
                {
                    rawSql += " AND folder_id = @FolderId";
                }

                var rawKeys = connection
                    .Query<(string FolderId, string FileName, string FileExtension)>(
                        rawSql,
                        new { RawExtensions = RawFileTypes.Extensions.ToList(), query.FolderId })
                    .Select(r => RawPairKey(r.FolderId, r.FileName, r.FileExtension))
                    .ToHashSet();

                if (rawKeys.Count > 0)
                {
                    photos = photos
                        .Where(p => !IsJpeg(p.FileExtension)
                            || !rawKeys.Contains(RawPairKey(p.FolderId, p.FileName, p.FileExtension)))
                        .ToList();
                }
            }

            return photos;
        }, ct);
    }

    private static bool IsJpeg(string extension) =>
        extension is ".jpg" or ".jpeg";

    /// <summary>
    /// RAW+JPGペア判定キー(フォルダID+拡張子を除いたファイル名)。
    /// ベース名は大文字小文字を区別しない。タプルにすることで区切り文字の衝突を避ける。
    /// </summary>
    private static (string FolderId, string Stem) RawPairKey(string folderId, string fileName, string fileExtension)
    {
        var stemLength = fileName.Length - fileExtension.Length;
        var stem = stemLength > 0 ? fileName[..stemLength] : fileName;
        return (folderId, stem.ToLowerInvariant());
    }

    internal static (string Sql, Dapper.DynamicParameters Parameters) BuildQuerySql(PhotoQuery query)
    {
        var parameters = new Dapper.DynamicParameters();
        var conditions = new List<string>();
        var join = "";

        if (query.AlbumId is not null)
        {
            join = " INNER JOIN photo_album_map pam ON pam.photo_id = photos.id";
            conditions.Add("pam.album_id = @AlbumId");
            parameters.Add("AlbumId", query.AlbumId);
        }

        if (query.FolderId is not null)
        {
            conditions.Add("photos.folder_id = @FolderId");
            parameters.Add("FolderId", query.FolderId);
        }

        if (query.MinRating > 0)
        {
            conditions.Add("photos.rating >= @MinRating");
            parameters.Add("MinRating", query.MinRating);
        }

        if (query.Flag is { } flag)
        {
            if (flag == PhotoFlag.None)
            {
                conditions.Add("photos.flag IS NULL");
            }
            else
            {
                conditions.Add("photos.flag = @Flag");
                parameters.Add("Flag", PhotoRow.FormatFlag(flag));
            }
        }

        if (query.ColorLabel is { } label && label != ColorLabel.None)
        {
            conditions.Add("photos.color_label = @ColorLabel");
            parameters.Add("ColorLabel", PhotoRow.FormatColorLabel(label));
        }

        if (query.Extensions is { Count: > 0 })
        {
            conditions.Add("photos.file_extension IN @Extensions");
            parameters.Add("Extensions", query.Extensions);
        }

        // ソートはホワイトリスト方式(ユーザー入力をSQLに連結しない)
        var orderColumn = query.SortBy switch
        {
            PhotoSortField.ImportedAt => "photos.imported_at",
            PhotoSortField.FileName => "photos.file_name COLLATE NOCASE",
            _ => "COALESCE(photos.captured_at, photos.imported_at)",
        };
        var direction = query.SortDescending ? "DESC" : "ASC";

        var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
        var sql = SelectColumns.Replace("FROM photos", "FROM photos" + join)
            + where
            + $" ORDER BY {orderColumn} {direction}, photos.file_name ASC";

        return (sql, parameters);
    }

    public Task UpdateRatingAsync(IReadOnlyList<string> photoIds, int rating, CancellationToken ct = default) =>
        ExecuteBulkUpdateAsync("UPDATE photos SET rating = @Value WHERE id IN @Ids", photoIds, rating, ct);

    public Task UpdateFlagAsync(IReadOnlyList<string> photoIds, PhotoFlag flag, CancellationToken ct = default) =>
        ExecuteBulkUpdateAsync("UPDATE photos SET flag = @Value WHERE id IN @Ids", photoIds, PhotoRow.FormatFlag(flag), ct);

    public Task UpdateColorLabelAsync(IReadOnlyList<string> photoIds, ColorLabel label, CancellationToken ct = default) =>
        ExecuteBulkUpdateAsync("UPDATE photos SET color_label = @Value WHERE id IN @Ids", photoIds, PhotoRow.FormatColorLabel(label), ct);

    private Task ExecuteBulkUpdateAsync(string sql, IReadOnlyList<string> photoIds, object? value, CancellationToken ct)
    {
        if (photoIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            using var transaction = connection.BeginTransaction();
            connection.Execute(sql, new { Value = value, Ids = photoIds }, transaction);
            transaction.Commit();
        }, ct);
    }

    public Task<IReadOnlyList<Photo>> GetByIdsAsync(IReadOnlyList<string> photoIds, CancellationToken ct = default)
    {
        if (photoIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<Photo>>([]);
        }

        return Task.Run<IReadOnlyList<Photo>>(() =>
        {
            using var connection = _connectionFactory.Open();
            var rows = connection.Query<PhotoRow>(
                SelectColumns + " WHERE id IN @Ids", new { Ids = photoIds });
            return rows.Select(r => r.ToModel()).ToList();
        }, ct);
    }

    public Task<HashSet<string>> GetExistingFilePathsAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            var paths = connection.Query<string>("SELECT file_path FROM photos");
            return paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }, ct);
    }

    public Task InsertBatchAsync(IReadOnlyList<Photo> photos, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            using var transaction = connection.BeginTransaction();

            const string sql = """
                INSERT OR IGNORE INTO photos
                    (id, folder_id, file_path, file_name, file_extension, file_size,
                     imported_at, captured_at, width, height, orientation,
                     camera_make, camera_model, lens_model, iso, aperture, shutter_speed, focal_length,
                     rating, flag, color_label, is_missing)
                VALUES
                    (@Id, @FolderId, @FilePath, @FileName, @FileExtension, @FileSize,
                     @ImportedAt, @CapturedAt, @Width, @Height, @Orientation,
                     @CameraMake, @CameraModel, @LensModel, @Iso, @Aperture, @ShutterSpeed, @FocalLength,
                     @Rating, @Flag, @ColorLabel, @IsMissing)
                """;

            foreach (var photo in photos)
            {
                ct.ThrowIfCancellationRequested();
                connection.Execute(sql, PhotoRow.ToParameters(photo), transaction);
            }

            transaction.Commit();
        }, ct);
    }
}
