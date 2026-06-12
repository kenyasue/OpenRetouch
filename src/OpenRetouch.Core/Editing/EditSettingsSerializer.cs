using System.Text.Json;

namespace OpenRetouch.Core.Editing;

/// <summary>EditSettingsのJSON永続化(camelCase・前方互換)。</summary>
public static class EditSettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(EditSettings settings) =>
        JsonSerializer.Serialize(settings, Options);

    /// <summary>
    /// JSONからEditSettingsを復元する。未知フィールドは無視、欠落フィールドはデフォルト値。
    /// 破損JSONは <see cref="JsonException"/> を投げる(呼び出し側がデフォルトへフォールバック)。
    /// </summary>
    public static EditSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<EditSettings>(json, Options)
            ?? throw new JsonException("edit_json deserialized to null");

        // 旧スキーマ(セクション欠落)や明示nullに対する防御
        settings.Basic ??= new BasicAdjustments();
        settings.Crop ??= new CropSettings();
        return settings;
    }
}
