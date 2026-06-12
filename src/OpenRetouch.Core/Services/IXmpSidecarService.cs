using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Services;

/// <summary>
/// Lightroom互換XMPサイドカー(.xmp)の読み書き。RAWファイルのみ対象とし、元のRAWには一切書き込まない。
/// </summary>
public interface IXmpSidecarService
{
    /// <summary>サイドカーのパス(拡張子を.xmpに置換。Lightroom規約)。</summary>
    string GetSidecarPath(string rawFilePath);

    /// <summary>
    /// サイドカーを読み込む。RAW以外・ファイルなし・破損時はnull(破損はWarningログ)。
    /// </summary>
    Task<XmpSidecarData?> TryReadAsync(Photo photo, CancellationToken ct = default);

    /// <summary>
    /// 編集設定+現在の評価/色ラベルをサイドカーへ書き出す(既存XMPはマージ)。
    /// RAW以外はno-op。書き込みはアトミック。
    /// </summary>
    Task WriteAsync(Photo photo, EditSettings settings, CancellationToken ct = default);
}
