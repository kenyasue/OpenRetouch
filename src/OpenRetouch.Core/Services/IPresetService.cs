using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Services;

/// <summary>プリセットの管理・適用・JSON入出力。</summary>
public interface IPresetService
{
    Task<IReadOnlyList<Preset>> GetPresetsAsync(CancellationToken ct = default);

    /// <summary>現在の編集の基本調整からプリセットを作成する(クロップは含めない)。</summary>
    Task<Preset> CreateFromSettingsAsync(
        string name, string? category, EditSettings source, CancellationToken ct = default);

    Task DeleteAsync(string presetId, CancellationToken ct = default);

    /// <summary>
    /// プリセットを複数写真へ適用する(各写真の現行編集にマージして保存)。
    /// 戻り値は適用に成功した枚数。
    /// </summary>
    Task<int> ApplyToPhotosAsync(string presetId, IReadOnlyList<string> photoIds, CancellationToken ct = default);

    /// <summary>プリセットをJSONファイルへエクスポートする。</summary>
    Task ExportAsync(string presetId, string filePath, CancellationToken ct = default);

    /// <summary>JSONファイルからプリセットを取り込む(値域外はクランプ)。</summary>
    Task<Preset> ImportAsync(string filePath, CancellationToken ct = default);
}
