using System.Buffers.Binary;
using System.Text;

namespace TXT2IMG;

// Invisible PNG metadata only — never draws text onto image pixels.
public static class PngTextChunk
{
    public static byte[] Embed(byte[] pngBytes, string keyword, string text)
    {
        // Find IEND and insert a tEXt chunk just before it.
        var iend = FindChunk(pngBytes, "IEND");
        if (iend < 0) return pngBytes;

        var payload = Encoding.Latin1.GetBytes(keyword + "\0" + text);
        var chunk = BuildChunk("tEXt", payload);

        var result = new byte[pngBytes.Length + chunk.Length];
        Buffer.BlockCopy(pngBytes, 0, result, 0, iend);
        Buffer.BlockCopy(chunk, 0, result, iend, chunk.Length);
        Buffer.BlockCopy(pngBytes, iend, result, iend + chunk.Length, pngBytes.Length - iend);
        return result;
    }

    private static int FindChunk(byte[] png, string type)
    {
        // PNG signature is 8 bytes; chunks follow.
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var i = 8;
        while (i + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(i));
            if (length < 0 || i + 12 + length > png.Length) return -1;
            if (png[i + 4] == typeBytes[0] && png[i + 5] == typeBytes[1]
                && png[i + 6] == typeBytes[2] && png[i + 7] == typeBytes[3])
                return i;
            i += 12 + length;
        }
        return -1;
    }

    private static byte[] BuildChunk(string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(0), data.Length);
        Buffer.BlockCopy(typeBytes, 0, chunk, 4, 4);
        Buffer.BlockCopy(data, 0, chunk, 8, data.Length);
        var crc = Crc32(typeBytes, data);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length), crc);
        return chunk;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in type) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
