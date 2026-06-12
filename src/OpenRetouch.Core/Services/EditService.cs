using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Editing;

namespace OpenRetouch.Core.Services;

/// <inheritdoc cref="IEditService"/>
public sealed class EditService : IEditService
{
    private readonly IEditRepository _editRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IXmpSidecarService _xmpSidecarService;
    private readonly ILogger<EditService> _logger;

    public EditService(
        IEditRepository editRepository,
        IPhotoRepository photoRepository,
        IXmpSidecarService xmpSidecarService,
        ILogger<EditService> logger)
    {
        _editRepository = editRepository;
        _photoRepository = photoRepository;
        _xmpSidecarService = xmpSidecarService;
        _logger = logger;
    }

    public async Task<EditSettings> GetEditAsync(string photoId, CancellationToken ct = default)
    {
        try
        {
            return await _editRepository.GetCurrentAsync(photoId, ct) ?? new EditSettings();
        }
        catch (System.Text.Json.JsonException ex)
        {
            // 破損したedit_jsonはデフォルトで継続(編集は失われるがアプリは動作する)
            _logger.LogWarning(ex, "Corrupted edit settings for photo {PhotoId}. Using defaults.", photoId);
            return new EditSettings();
        }
    }

    public async Task SaveEditAsync(string photoId, EditSettings settings, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(photoId);
        ArgumentNullException.ThrowIfNull(settings);
        await _editRepository.UpsertCurrentAsync(photoId, settings, ct);
        await WriteSidecarIfRawAsync(photoId, settings, ct);
    }

    public Task ResetAsync(string photoId, CancellationToken ct = default) =>
        SaveEditAsync(photoId, new EditSettings(), ct);

    public EditSettings? CopyBuffer { get; set; }

    public async Task<int> ApplyToPhotosAsync(
        EditSettings settings, IReadOnlyList<string> photoIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var applied = 0;
        foreach (var photoId in photoIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _editRepository.UpsertCurrentAsync(photoId, settings.Clone(), ct);
                await WriteSidecarIfRawAsync(photoId, settings, ct);
                applied++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 1枚の失敗で一括適用全体を止めない
                _logger.LogError(ex, "Failed to paste edit settings to photo {PhotoId}", photoId);
            }
        }

        return applied;
    }

    /// <summary>RAW写真ならXMPサイドカーを書き出す(失敗してもDB保存は成功扱い)。</summary>
    private async Task WriteSidecarIfRawAsync(string photoId, EditSettings settings, CancellationToken ct)
    {
        try
        {
            var photos = await _photoRepository.GetByIdsAsync([photoId], ct);
            if (photos.FirstOrDefault() is { } photo && Models.RawFileTypes.IsRaw(photo.FileExtension))
            {
                await _xmpSidecarService.WriteAsync(photo, settings, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to write XMP sidecar for photo {PhotoId}", photoId);
        }
    }
}
