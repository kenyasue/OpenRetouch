using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Abstractions.Imaging;

/// <summary>画像ファイルからメタデータ(EXIF+サイズ)を読み取る。実装はImagingレイヤー。</summary>
public interface IPhotoMetadataReader
{
    /// <summary>
    /// メタデータを読み取る。読み取れない項目はデフォルト値のままにし、例外を投げない。
    /// </summary>
    PhotoMetadata Read(string filePath);
}
