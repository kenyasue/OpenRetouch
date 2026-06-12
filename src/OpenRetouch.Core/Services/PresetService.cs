using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Services;

/// <inheritdoc cref="IPresetService"/>
public sealed class PresetService : IPresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly IPresetRepository _presetRepository;
    private readonly IEditRepository _editRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IXmpSidecarService _xmpSidecarService;
    private readonly ILogger<PresetService> _logger;

    public PresetService(
        IPresetRepository presetRepository,
        IEditRepository editRepository,
        IPhotoRepository photoRepository,
        IXmpSidecarService xmpSidecarService,
        ILogger<PresetService> logger)
    {
        _presetRepository = presetRepository;
        _editRepository = editRepository;
        _photoRepository = photoRepository;
        _xmpSidecarService = xmpSidecarService;
        _logger = logger;
    }

    public Task<IReadOnlyList<Preset>> GetPresetsAsync(CancellationToken ct = default) =>
        _presetRepository.GetAllAsync(ct);

    public async Task<Preset> CreateFromSettingsAsync(
        string name, string? category, EditSettings source, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);

        var now = DateTimeOffset.UtcNow;
        var preset = new Preset
        {
            Id = Guid.NewGuid().ToString(),
            Name = name.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            Settings = source.Basic.Clone(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _presetRepository.InsertAsync(preset, ct);
        return preset;
    }

    public Task DeleteAsync(string presetId, CancellationToken ct = default) =>
        _presetRepository.DeleteAsync(presetId, ct);

    public async Task<int> ApplyToPhotosAsync(
        string presetId, IReadOnlyList<string> photoIds, CancellationToken ct = default)
    {
        var presets = await _presetRepository.GetAllAsync(ct);
        var preset = presets.FirstOrDefault(p => p.Id == presetId)
            ?? throw new InvalidOperationException($"Preset not found: {presetId}");

        var photosById = (await _photoRepository.GetByIdsAsync(photoIds, ct)).ToDictionary(p => p.Id);

        var applied = 0;
        foreach (var photoId in photoIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var current = await _editRepository.GetCurrentAsync(photoId, ct) ?? new EditSettings();
                var merged = PresetMerger.Merge(current, preset);
                await _editRepository.UpsertCurrentAsync(photoId, merged, ct);

                // RAWはXMPサイドカーも更新(Lightroom互換)
                if (photosById.TryGetValue(photoId, out var photo)
                    && RawFileTypes.IsRaw(photo.FileExtension))
                {
                    await _xmpSidecarService.WriteAsync(photo, merged, ct);
                }

                applied++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 1枚の失敗で一括適用全体を止めない
                _logger.LogError(ex, "Failed to apply preset {PresetId} to photo {PhotoId}", presetId, photoId);
            }
        }

        return applied;
    }

    public async Task ExportAsync(string presetId, string filePath, CancellationToken ct = default)
    {
        var presets = await _presetRepository.GetAllAsync(ct);
        var preset = presets.FirstOrDefault(p => p.Id == presetId)
            ?? throw new InvalidOperationException($"Preset not found: {presetId}");

        var dto = new PresetFileDto
        {
            Name = preset.Name,
            Category = preset.Category,
            Settings = preset.Settings,
        };
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    public async Task<Preset> ImportAsync(string filePath, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(filePath, ct);
        var dto = JsonSerializer.Deserialize<PresetFileDto>(json, JsonOptions)
            ?? throw new JsonException("Preset file deserialized to null");

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            dto.Name = Path.GetFileNameWithoutExtension(filePath);
        }

        var settings = dto.Settings ?? new BasicAdjustments();
        settings.ClampToValidRange();

        var now = DateTimeOffset.UtcNow;
        var preset = new Preset
        {
            Id = Guid.NewGuid().ToString(),
            Name = dto.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim(),
            Settings = settings,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _presetRepository.InsertAsync(preset, ct);
        return preset;
    }

    /// <summary>プリセットJSONファイルのフォーマット。</summary>
    private sealed class PresetFileDto
    {
        public string Name { get; set; } = "";

        public string? Category { get; set; }

        public BasicAdjustments? Settings { get; set; }
    }
}
