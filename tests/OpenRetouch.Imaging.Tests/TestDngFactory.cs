namespace OpenRetouch.Imaging.Tests;

/// <summary>
/// テスト用の最小リニアDNG(TIFFベース、PhotometricInterpretation=LinearRaw、16bit RGB)を生成する。
/// 実カメラのRAWはリポジトリに含められないため、LibRawが読めるデモザイク済みDNGを合成する。
/// </summary>
public static class TestDngFactory
{
    private const ushort TypeByte = 1;
    private const ushort TypeAscii = 2;
    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;
    private const ushort TypeRational = 5;
    private const ushort TypeSRational = 10;

    /// <summary>単色グラデーションのリニアDNGを生成する。</summary>
    public static void Create(string path, int width, int height)
    {
        // 16bit RGBピクセル(横方向グラデーション)
        var pixelBytes = new byte[width * height * 3 * 2];
        var index = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (ushort)(x * 65535L / Math.Max(1, width - 1));
                WriteUInt16(pixelBytes, ref index, value);            // R
                WriteUInt16(pixelBytes, ref index, (ushort)(value / 2)); // G
                WriteUInt16(pixelBytes, ref index, (ushort)(65535 - value)); // B
            }
        }

        var entries = new List<(ushort Tag, ushort Type, uint Count, byte[] Value)>
        {
            (254, TypeLong, 1, Bytes((uint)0)),                       // NewSubfileType
            (256, TypeLong, 1, Bytes((uint)width)),                   // ImageWidth
            (257, TypeLong, 1, Bytes((uint)height)),                  // ImageLength
            (258, TypeShort, 3, Shorts(16, 16, 16)),                  // BitsPerSample
            (259, TypeShort, 1, Shorts(1)),                           // Compression=none
            (262, TypeShort, 1, Shorts(34892)),                       // Photometric=LinearRaw
            (273, TypeLong, 1, Bytes((uint)0)),                       // StripOffsets(後で確定)
            (274, TypeShort, 1, Shorts(1)),                           // Orientation
            (277, TypeShort, 1, Shorts(3)),                           // SamplesPerPixel
            (278, TypeLong, 1, Bytes((uint)height)),                  // RowsPerStrip
            (279, TypeLong, 1, Bytes((uint)pixelBytes.Length)),       // StripByteCounts
            (284, TypeShort, 1, Shorts(1)),                           // PlanarConfiguration
            (50706, TypeByte, 4, [1, 4, 0, 0]),                       // DNGVersion 1.4
            (50707, TypeByte, 4, [1, 1, 0, 0]),                       // DNGBackwardVersion 1.1
            (50708, TypeAscii, 8, "TestCam\0"u8.ToArray()),           // UniqueCameraModel
            (50721, TypeSRational, 9, IdentityMatrixSRational()),     // ColorMatrix1
            (50728, TypeRational, 3, NeutralRational()),              // AsShotNeutral
            (50778, TypeShort, 1, Shorts(21)),                        // CalibrationIlluminant1=D65
        };

        // レイアウト計算: header(8) + IFD(2 + n*12 + 4) + 外部データ + ピクセル
        var ifdSize = 2 + entries.Count * 12 + 4;
        var externalOffset = 8 + ifdSize;

        // 外部データ(4バイト超の値)を配置
        var external = new MemoryStream();
        var valueFields = new Dictionary<int, uint>();   // entryIndex → 書き込むoffset/値
        for (var i = 0; i < entries.Count; i++)
        {
            var (_, _, _, value) = entries[i];
            if (value.Length <= 4)
            {
                var inline = new byte[4];
                value.CopyTo(inline, 0);
                valueFields[i] = BitConverter.ToUInt32(inline, 0);
            }
            else
            {
                valueFields[i] = (uint)(externalOffset + external.Position);
                external.Write(value);
            }
        }

        var dataOffset = (uint)(externalOffset + external.Length);

        // StripOffsetsを確定
        var stripEntryIndex = entries.FindIndex(e => e.Tag == 273);
        valueFields[stripEntryIndex] = dataOffset;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write((uint)8);

        writer.Write((ushort)entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var (tag, type, count, _) = entries[i];
            writer.Write(tag);
            writer.Write(type);
            writer.Write(count);
            writer.Write(valueFields[i]);
        }

        writer.Write((uint)0);   // next IFD
        writer.Write(external.ToArray());
        writer.Write(pixelBytes);
    }

    private static byte[] Bytes(uint value) => BitConverter.GetBytes(value);

    private static byte[] Shorts(params ushort[] values)
    {
        var bytes = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(bytes, i * 2);
        }

        return bytes;
    }

    private static byte[] IdentityMatrixSRational()
    {
        var bytes = new byte[9 * 8];
        var offset = 0;
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                BitConverter.GetBytes(row == col ? 1 : 0).CopyTo(bytes, offset);
                BitConverter.GetBytes(1).CopyTo(bytes, offset + 4);
                offset += 8;
            }
        }

        return bytes;
    }

    private static byte[] NeutralRational()
    {
        var bytes = new byte[3 * 8];
        for (var i = 0; i < 3; i++)
        {
            BitConverter.GetBytes((uint)1).CopyTo(bytes, i * 8);
            BitConverter.GetBytes((uint)1).CopyTo(bytes, i * 8 + 4);
        }

        return bytes;
    }

    private static void WriteUInt16(byte[] buffer, ref int index, ushort value)
    {
        buffer[index] = (byte)(value & 0xFF);
        buffer[index + 1] = (byte)(value >> 8);
        index += 2;
    }
}
