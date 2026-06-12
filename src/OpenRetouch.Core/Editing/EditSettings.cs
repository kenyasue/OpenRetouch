namespace OpenRetouch.Core.Editing;

/// <summary>
/// 1枚の写真に対する編集内容(非破壊編集の中核モデル)。
/// edits.edit_json としてJSON永続化される。未知フィールドは無視し、欠落はデフォルト補完(前方互換)。
/// </summary>
public sealed class EditSettings
{
    /// <summary>編集スキーマバージョン。</summary>
    public int Version { get; init; } = 1;

    public BasicAdjustments Basic { get; set; } = new();

    public CropSettings Crop { get; set; } = new();

    /// <summary>全パラメータがデフォルト(未編集相当)か。</summary>
    public bool IsDefault => Basic.IsDefault && Crop.IsDefault;

    /// <summary>Undo/Redoスナップショット用のディープコピー。</summary>
    public EditSettings Clone() => new()
    {
        Version = Version,
        Basic = Basic.Clone(),
        Crop = Crop.Clone(),
    };
}
