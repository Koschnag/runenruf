namespace AssetForge;

/// <summary>Wert-Rauschen + fBm — Grundlage für Stein, Erde, Terrain.</summary>
public static class Rauschen
{
    private static float Gitterwert(ulong seed, int x, int y)
    {
        ulong h = seed + (ulong)(uint)x * 0x9E3779B97F4A7C15UL + (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
        h ^= h >> 31;
        return (h >> 40) * (1.0f / (1UL << 24));
    }

    private static float Glatt(float t) => t * t * (3f - 2f * t);

    public static float Wert(ulong seed, float x, float y)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = Glatt(x - x0), fy = Glatt(y - y0);
        float a = Gitterwert(seed, x0, y0), b = Gitterwert(seed, x0 + 1, y0);
        float c = Gitterwert(seed, x0, y0 + 1), d = Gitterwert(seed, x0 + 1, y0 + 1);
        return float.Lerp(float.Lerp(a, b, fx), float.Lerp(c, d, fx), fy);
    }

    /// <summary>Fraktales Rauschen in [0, 1].</summary>
    public static float Fbm(ulong seed, float x, float y, int oktaven)
    {
        float summe = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int o = 0; o < oktaven; o++)
        {
            summe += amp * Wert(seed + (ulong)o * 7919UL, x * freq, y * freq);
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return summe / norm;
    }
}
