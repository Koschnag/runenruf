namespace AssetForge;

/// <summary>Minimaler WAV-Schreiber: 16 Bit PCM, mono.</summary>
public static class WavSchreiber
{
    public const int Abtastrate = 44100;

    public static byte[] Schreibe(double[] proben)
    {
        int n = proben.Length;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + n * 2);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);          // PCM
        w.Write((short)1);          // mono
        w.Write(Abtastrate);
        w.Write(Abtastrate * 2);    // Bytes/s
        w.Write((short)2);          // Blockgröße
        w.Write((short)16);         // Bits
        w.Write("data"u8);
        w.Write(n * 2);
        foreach (double p in proben)
            w.Write((short)(Math.Clamp(p, -1.0, 1.0) * short.MaxValue));
        return ms.ToArray();
    }
}
