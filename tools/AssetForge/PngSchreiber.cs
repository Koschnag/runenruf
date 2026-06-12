using System.IO.Compression;

namespace AssetForge;

/// <summary>Minimaler PNG-Encoder (RGBA, 8 Bit) — null Abhängigkeiten, deterministisch.</summary>
public static class PngSchreiber
{
    public static byte[] Schreibe(int breite, int hoehe, byte[] rgba)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var ihdr = new byte[13];
        IntBe(ihdr, 0, breite);
        IntBe(ihdr, 4, hoehe);
        ihdr[8] = 8;  // Bittiefe
        ihdr[9] = 6;  // RGBA
        Chunk(ms, "IHDR", ihdr);

        var zeilen = new byte[(breite * 4 + 1) * hoehe];
        for (int y = 0; y < hoehe; y++)
            Array.Copy(rgba, y * breite * 4, zeilen, y * (breite * 4 + 1) + 1, breite * 4);
        Chunk(ms, "IDAT", Zlib(zeilen));
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static byte[] Zlib(byte[] daten)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x9C);
        using (var d = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            d.Write(daten);
        uint a = Adler32(daten);
        ms.WriteByte((byte)(a >> 24)); ms.WriteByte((byte)(a >> 16));
        ms.WriteByte((byte)(a >> 8)); ms.WriteByte((byte)a);
        return ms.ToArray();
    }

    private static uint Adler32(byte[] daten)
    {
        uint a = 1, b = 0;
        foreach (byte t in daten) { a = (a + t) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }

    private static readonly uint[] CrcTabelle = ErzeugeCrcTabelle();

    private static uint[] ErzeugeCrcTabelle()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    private static void Chunk(Stream s, string typ, byte[] daten)
    {
        var laenge = new byte[4];
        IntBe(laenge, 0, daten.Length);
        s.Write(laenge);
        var typBytes = System.Text.Encoding.ASCII.GetBytes(typ);
        s.Write(typBytes);
        s.Write(daten);
        uint crc = 0xFFFFFFFF;
        foreach (byte x in typBytes) crc = CrcTabelle[(crc ^ x) & 0xFF] ^ (crc >> 8);
        foreach (byte x in daten) crc = CrcTabelle[(crc ^ x) & 0xFF] ^ (crc >> 8);
        crc ^= 0xFFFFFFFF;
        var crcBytes = new byte[4];
        IntBe(crcBytes, 0, unchecked((int)crc));
        s.Write(crcBytes);
    }

    private static void IntBe(byte[] ziel, int offset, int wert)
    {
        ziel[offset] = (byte)(wert >> 24);
        ziel[offset + 1] = (byte)(wert >> 16);
        ziel[offset + 2] = (byte)(wert >> 8);
        ziel[offset + 3] = (byte)wert;
    }
}
