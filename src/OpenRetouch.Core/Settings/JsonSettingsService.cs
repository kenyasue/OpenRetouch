using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Environment;

namespace OpenRetouch.Core.Settings;

/// <summary>
/// settings.json への設定永続化。
/// 保存はテンポラリファイル経由のアトミック書き込みで、クラッシュ時の破損を防ぐ。
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IAppEnvironment _environment;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsService(IAppEnvironment environment, ILogger<JsonSettingsService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public AppSettings Current { get; private set; } = new();

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = _environment.SettingsFilePath;
            if (!File.Exists(path))
            {
                _logger.LogInformation("Settings file not found. Using defaults: {Path}", path);
                Current = new AppSettings();
                return Current;
            }

            try
            {
                await using var stream = File.OpenRead(path);
                var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, ct);
                Current = loaded ?? new AppSettings();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Settings file is corrupted. Falling back to defaults: {Path}", path);
                Current = new AppSettings();
            }

            return Current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(ct);
        try
        {
            var path = _environment.SettingsFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var tempPath = path + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, ct);
            }

            File.Move(tempPath, path, overwrite: true);
            Current = settings;
        }
        finally
        {
            _gate.Release();
        }
    }
}
