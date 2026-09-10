using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

/// <summary>Lossless cell storage: S1:bits:count:CRC32:Base64(raw Deflate).</summary>
public static class LevelCellCodec
{
    // Also bounds allocations when reading damaged assets. Tiled levels can exceed 64x64.
    public const int MaxCellCount = 16 * 1024 * 1024;

    public static string Encode(byte[] cells)
    {
        if (cells == null || cells.Length == 0) return string.Empty;
        if (cells.Length > MaxCellCount) throw new ArgumentException("Level cell array is too large.", nameof(cells));

        int maximum = 0;
        foreach (byte cell in cells) maximum = Math.Max(maximum, cell);
        int bits = 1;
        while ((maximum >> bits) != 0) bits++;
        var packed = new byte[(cells.Length * bits + 7) / 8];
        for (int i = 0; i < cells.Length; i++)
        {
            int offset = i * bits;
            int value = cells[i] << (offset & 7);
            packed[offset >> 3] |= (byte)value;
            if ((offset & 7) + bits > 8) packed[(offset >> 3) + 1] |= (byte)(value >> 8);
        }

        using (var output = new MemoryStream())
        {
            using (var compressor = new DeflateStream(output, CompressionLevel.Optimal, true))
                compressor.Write(packed, 0, packed.Length);
            return "S1:" + bits.ToString(CultureInfo.InvariantCulture) + ":"
                + cells.Length.ToString(CultureInfo.InvariantCulture) + ":"
                + Checksum(cells).ToString("X8", CultureInfo.InvariantCulture) + ":"
                + Convert.ToBase64String(output.ToArray());
        }
    }

    public static byte[] Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return Array.Empty<byte>();
        string[] fields = encoded.Split(':');
        if (fields.Length != 5 || fields[0] != "S1"
            || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int bits) || bits < 1 || bits > 8
            || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 1 || count > MaxCellCount
            || fields[3].Length != 8
            || !uint.TryParse(fields[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint checksum))
            throw new InvalidDataException("Invalid or unsupported level cell encoding.");

        int packedLength = (count * bits + 7) / 8;
        if (fields[4].Length > (packedLength + 1024L + packedLength / 100) * 4 / 3 + 4)
            throw new InvalidDataException("Compressed level data exceeds its declared size.");
        byte[] compressed;
        try { compressed = Convert.FromBase64String(fields[4]); }
        catch (FormatException error) { throw new InvalidDataException("Invalid level Base64 data.", error); }
        var packed = new byte[packedLength];
        using (var input = new MemoryStream(compressed, false))
        using (var decompressor = new DeflateStream(input, CompressionMode.Decompress))
        {
            int read = 0;
            while (read < packed.Length)
            {
                int length = decompressor.Read(packed, read, packed.Length - read);
                if (length == 0) throw new InvalidDataException("Truncated level cell data.");
                read += length;
            }
            if (decompressor.ReadByte() != -1) throw new InvalidDataException("Level cell data is longer than declared.");
        }

        var cells = new byte[count];
        int mask = (1 << bits) - 1;
        for (int i = 0; i < count; i++)
        {
            int offset = i * bits;
            int value = packed[offset >> 3];
            if ((offset & 7) + bits > 8) value |= packed[(offset >> 3) + 1] << 8;
            cells[i] = (byte)((value >> (offset & 7)) & mask);
        }
        if (Checksum(cells) != checksum) throw new InvalidDataException("Level cell checksum mismatch.");
        return cells;
    }

    private static uint Checksum(byte[] cells)
    {
        uint crc = uint.MaxValue;
        foreach (byte cell in cells)
        {
            crc ^= cell;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
        }
        return ~crc;
    }
}
