using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Repositories;

/// <summary>presetsテーブルへのアクセス。実装はCatalogレイヤー。</summary>
public interface IPresetRepository
{
    Task<IReadOnlyList<Preset>> GetAllAsync(CancellationToken ct = default);

    Task InsertAsync(Preset preset, CancellationToken ct = default);

    Task DeleteAsync(string presetId, CancellationToken ct = default);
}
