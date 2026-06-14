namespace AssetForge;

/// <summary>splitmix64 — deterministischer RNG, plattformunabhängig (premise-determinismus).</summary>
public struct Saat(ulong zustand)
{
    private ulong _zustand = zustand;

    public ulong Naechste()
    {
        _zustand += 0x9E3779B97F4A7C15UL;
        ulong z = _zustand;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Gleichverteilt in [0, 1).</summary>
    public double Anteil() => (Naechste() >> 11) * (1.0 / (1UL << 53));

    public int Bereich(int minInkl, int maxExkl)
    {
        if (maxExkl <= minInkl) return minInkl; // leerer/ungültiger Bereich → untere Grenze statt Division durch Null
        return minInkl + (int)(Naechste() % (ulong)(maxExkl - minInkl));
    }
}
