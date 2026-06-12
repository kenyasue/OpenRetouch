using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Services;

/// <inheritdoc cref="ICatalogService"/>
public sealed class CatalogService : ICatalogService, IDisposable
{
    private readonly IPhotoRepository _photoRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IAlbumRepository _albumRepository;
    private readonly IThumbnailCacheRepository _thumbnailCacheRepository;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<CatalogService> _logger;

    /// <summary>サムネイル生成ジョブの直列化(連続インポート時に同一写真を並行生成しない)。</summary>
    private readonly SemaphoreSlim _thumbnailGate = new(1, 1);

    private readonly IEditRepository _editRepository;

    public CatalogService(
        IPhotoRepository photoRepository,
        IFolderRepository folderRepository,
        IAlbumRepository albumRepository,
        IThumbnailCacheRepository thumbnailCacheRepository,
        IEditRepository editRepository,
        IThumbnailGenerator thumbnailGenerator,
        IJobQueue jobQueue,
        ILogger<CatalogService> logger)
    {
        _photoRepository = photoRepository;
        _folderRepository = folderRepository;
        _albumRepository = albumRepository;
        _thumbnailCacheRepository = thumbnailCacheRepository;
        _editRepository = editRepository;
        _thumbnailGenerator = thumbnailGenerator;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public event EventHandler<ThumbnailReadyEventArgs>? ThumbnailReady;

    public Task<IReadOnlyList<Photo>> QueryPhotosAsync(PhotoQuery query, CancellationToken ct = default) =>
        _photoRepository.QueryAsync(query, ct);

    public Task<IReadOnlyDictionary<string, string>> GetThumbnailPathsAsync(CancellationToken ct = default) =>
        _thumbnailCacheRepository.GetAllThumbPathsAsync(ct);

    public Task SetRatingAsync(IReadOnlyList<string> photoIds, int rating, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rating, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rating, 5);
        return _photoRepository.UpdateRatingAsync(photoIds, rating, ct);
    }

    public Task SetFlagAsync(IReadOnlyList<string> photoIds, PhotoFlag flag, CancellationToken ct = default) =>
        _photoRepository.UpdateFlagAsync(photoIds, flag, ct);

    public Task SetColorLabelAsync(IReadOnlyList<string> photoIds, ColorLabel label, CancellationToken ct = default) =>
        _photoRepository.UpdateColorLabelAsync(photoIds, label, ct);

    public Task<IReadOnlyList<Folder>> GetFoldersAsync(CancellationToken ct = default) =>
        _folderRepository.GetAllAsync(ct);

    public async Task RemoveFolderFromCatalogAsync(string folderId, CancellationToken ct = default)
    {
        // DB行は1トランザクションで削除し、サムネイルファイルは残りパスを使って後追い削除する
        var thumbPaths = await _folderRepository.DeleteCascadeAsync(folderId, ct);
        DeleteThumbnailFiles(thumbPaths);
        _logger.LogInformation(
            "Folder removed from catalog: {FolderId} (thumbnails: {Count})", folderId, thumbPaths.Count);
    }

    public async Task ClearThumbnailCacheAsync(CancellationToken ct = default)
    {
        // 実行中の生成ジョブと競合すると、生成直後のファイルを誤削除しうるためゲートで直列化する
        await _thumbnailGate.WaitAsync(ct);
        try
        {
            var paths = await _thumbnailCacheRepository.GetAllThumbPathsAsync(ct);
            await _thumbnailCacheRepository.RemoveAllAsync(ct);
            DeleteThumbnailFiles(paths.Values);
            _logger.LogInformation("Thumbnail cache cleared: {Count} entries", paths.Count);
        }
        finally
        {
            _thumbnailGate.Release();
        }

        EnqueueThumbnailGeneration();
    }

    private void DeleteThumbnailFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to delete thumbnail file: {Path}", path);
            }
        }
    }

    public Task<IReadOnlyList<Album>> GetAlbumsAsync(CancellationToken ct = default) =>
        _albumRepository.GetAllAsync(ct);

    public Task<Album> CreateAlbumAsync(string name, CancellationToken ct = default) =>
        _albumRepository.InsertAsync(name, ct);

    public Task DeleteAlbumAsync(string albumId, CancellationToken ct = default) =>
        _albumRepository.DeleteAsync(albumId, ct);

    public Task AddToAlbumAsync(string albumId, IReadOnlyList<string> photoIds, CancellationToken ct = default) =>
        _albumRepository.AddPhotosAsync(albumId, photoIds, ct);

    public Task RemoveFromAlbumAsync(string albumId, IReadOnlyList<string> photoIds, CancellationToken ct = default) =>
        _albumRepository.RemovePhotosAsync(albumId, photoIds, ct);

    public void EnqueueThumbnailGeneration()
    {
        var job = new DelegateJob("Thumbnail Generation", GenerateMissingThumbnailsAsync);
        _jobQueue.Enqueue(job);
    }

    public async Task EnqueueThumbnailGenerationIfMissingAsync(CancellationToken ct = default)
    {
        // 全Photoオブジェクトの展開は避け、まずSQLカウントで判定する(数万枚カタログ対策)
        var missingRows = await _thumbnailCacheRepository.CountPhotosWithoutThumbnailAsync(ct);
        if (missingRows > 0)
        {
            _logger.LogInformation(
                "Thumbnail check found {Missing} photos without thumbnails. Enqueueing generation.",
                missingRows);
            EnqueueThumbnailGeneration();
            return;
        }

        // 全行存在する場合のみ、ファイル消失(キャッシュ手動削除等)を確認する
        var cached = await _thumbnailCacheRepository.GetAllThumbPathsAsync(ct);
        if (cached.Values.Any(path => !File.Exists(path)))
        {
            _logger.LogInformation("Thumbnail cache files are missing on disk. Enqueueing generation.");
            EnqueueThumbnailGeneration();
        }
    }

    public async Task RefreshThumbnailsAsync(IReadOnlyList<string> photoIds, CancellationToken ct = default)
    {
        if (photoIds.Count == 0)
        {
            return;
        }

        // 旧サムネイルファイルを削除(生成は毎回一意名のため、残すとリークする)
        var paths = await _thumbnailCacheRepository.GetAllThumbPathsAsync(ct);
        foreach (var photoId in photoIds)
        {
            if (paths.TryGetValue(photoId, out var oldPath))
            {
                try
                {
                    File.Delete(oldPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to delete old thumbnail: {Path}", oldPath);
                }
            }
        }

        // キャッシュ行を消すことで「未生成」扱いになり、生成ジョブが編集反映済みで再生成する
        await _thumbnailCacheRepository.RemoveAsync(photoIds, ct);
        EnqueueThumbnailGeneration();
    }

    internal async Task GenerateMissingThumbnailsAsync(
        IProgress<(int Done, int Total)> progress,
        CancellationToken ct)
    {
        await _thumbnailGate.WaitAsync(ct);
        try
        {
            await GenerateMissingThumbnailsCoreAsync(progress, ct);
        }
        finally
        {
            _thumbnailGate.Release();
        }
    }

    private async Task GenerateMissingThumbnailsCoreAsync(
        IProgress<(int Done, int Total)> progress,
        CancellationToken ct)
    {
        var photos = await _photoRepository.QueryAsync(new PhotoQuery(), ct);
        var cached = await _thumbnailCacheRepository.GetAllThumbPathsAsync(ct);

        // キャッシュにない、またはキャッシュファイルが消えている写真が対象
        var pending = photos
            .Where(p => !cached.TryGetValue(p.Id, out var path) || !File.Exists(path))
            .ToList();

        if (pending.Count == 0)
        {
            progress.Report((0, 0));
            return;
        }

        var done = 0;
        foreach (var photo in pending)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // 現行編集を適用したサムネイルを生成する(XMP由来・アプリ内編集とも反映)
                var edit = await _editRepository.GetCurrentAsync(photo.Id, ct) ?? new Editing.EditSettings();
                var thumbPath = await _thumbnailGenerator.GenerateAsync(photo, edit, ct);
                var sourceModifiedAt = File.GetLastWriteTimeUtc(photo.FilePath);
                await _thumbnailCacheRepository.UpsertAsync(photo.Id, thumbPath, sourceModifiedAt, ct);
                ThumbnailReady?.Invoke(this, new ThumbnailReadyEventArgs(photo.Id, thumbPath));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 1件の失敗(破損画像等)でジョブ全体を止めない
                _logger.LogError(ex, "Thumbnail generation failed: {Path}", photo.FilePath);
            }

            done++;
            progress.Report((done, pending.Count));
        }
    }

    public void Dispose() => _thumbnailGate.Dispose();
}
