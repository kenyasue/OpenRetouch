using System.Text.Json;
using Dapper;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Records;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Catalog.Repositories;

/// <inheritdoc cref="IPresetRepository"/>
public sealed class PresetRepository : IPresetRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConnectionFactory _connectionFactory;

    public PresetRepository(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<Preset>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<Preset>>(() =>
        {
            using var connection = _connectionFactory.Open();
            var rows = connection.Query(
                "SELECT id, name, category, preset_json, created_at, updated_at FROM presets ORDER BY category, name COLLATE NOCASE");
            var presets = new List<Preset>();
            foreach (var row in rows)
            {
                presets.Add(new Preset
                {
                    Id = (string)row.id,
                    Name = (string)row.name,
                    Category = (string?)row.category,
                    Settings = JsonSerializer.Deserialize<BasicAdjustments>(
                        (string)row.preset_json, JsonOptions) ?? new BasicAdjustments(),
                    CreatedAt = PhotoRow.ParseTimestamp((string)row.created_at) ?? DateTimeOffset.MinValue,
                    UpdatedAt = PhotoRow.ParseTimestamp((string)row.updated_at) ?? DateTimeOffset.MinValue,
                });
            }

            return presets;
        }, ct);
    }

    public Task InsertAsync(Preset preset, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            connection.Execute(
                """
                INSERT INTO presets (id, name, category, preset_json, sort_order, created_at, updated_at)
                VALUES (@Id, @Name, @Category, @PresetJson, 0, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    preset.Id,
                    preset.Name,
                    preset.Category,
                    PresetJson = JsonSerializer.Serialize(preset.Settings, JsonOptions),
                    CreatedAt = PhotoRow.FormatTimestamp(preset.CreatedAt),
                    UpdatedAt = PhotoRow.FormatTimestamp(preset.UpdatedAt),
                });
        }, ct);
    }

    public Task DeleteAsync(string presetId, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var connection = _connectionFactory.Open();
            connection.Execute("DELETE FROM presets WHERE id = @Id", new { Id = presetId });
        }, ct);
    }
}
