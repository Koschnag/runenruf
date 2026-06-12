using System.Text;

namespace AssetForge;

/// <summary>Dreiecksnetz eines Weltsplitters (term-insel).</summary>
public sealed record TerrainNetz(float[] Positionen, float[] Normalen, uint[] Indizes)
{
    public int Dreiecke => Indizes.Length / 3;
}

/// <summary>
/// spec-terrain: Heightmap-fBm mit radialem Insel-Falloff → schwebender Weltsplitter.
/// Deterministisch; die niedrigste Stufe hält das Budget aus der Stil-Bibel ein.
/// </summary>
public static class TerrainGenerator
{
    public static float[,] Hoehenfeld(TerrainRezept r)
    {
        int n = r.Aufloesung;
        var h = new float[n, n];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            float u = x / (float)(n - 1) * 2f - 1f, v = z / (float)(n - 1) * 2f - 1f;
            float radial = MathF.Sqrt(u * u + v * v) / r.InselRadius;
            float falloff = Math.Clamp(1f - radial * radial, 0f, 1f);
            h[z, x] = Rauschen.Fbm(r.Seed, (u + 1f) * 4f, (v + 1f) * 4f, 6) * r.HoehenSkala * falloff;
        }
        return h;
    }

    public static TerrainNetz Erzeuge(TerrainRezept r)
    {
        int n = r.Aufloesung;
        var h = Hoehenfeld(r);
        float schritt = 100f / (n - 1);   // Weltsplitter: 100 m Kante

        var pos = new float[n * n * 3];
        var nor = new float[n * n * 3];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int i = (z * n + x) * 3;
            pos[i] = x * schritt; pos[i + 1] = h[z, x]; pos[i + 2] = z * schritt;

            float hl = h[z, Math.Max(x - 1, 0)],     hr = h[z, Math.Min(x + 1, n - 1)];
            float hu = h[Math.Max(z - 1, 0), x],     ho = h[Math.Min(z + 1, n - 1), x];
            float nx = (hl - hr) / (2f * schritt), nz = (hu - ho) / (2f * schritt);
            float l = MathF.Sqrt(nx * nx + 1f + nz * nz);
            nor[i] = nx / l; nor[i + 1] = 1f / l; nor[i + 2] = nz / l;
        }

        var idx = new uint[(n - 1) * (n - 1) * 6];
        int k = 0;
        for (int z = 0; z < n - 1; z++)
        for (int x = 0; x < n - 1; x++)
        {
            uint a = (uint)(z * n + x), b = a + 1, c = a + (uint)n, d = c + 1;
            idx[k++] = a; idx[k++] = c; idx[k++] = b;
            idx[k++] = b; idx[k++] = c; idx[k++] = d;
        }
        return new TerrainNetz(pos, nor, idx);
    }

    public static string AlsObj(TerrainNetz netz)
    {
        var sb = new StringBuilder("# Runenruf-Weltsplitter — generiert von AssetForge\n");
        var p = netz.Positionen;
        for (int i = 0; i < p.Length; i += 3)
            sb.Append("v ").Append(p[i].ToString("F4")).Append(' ')
              .Append(p[i + 1].ToString("F4")).Append(' ').Append(p[i + 2].ToString("F4")).Append('\n');
        var ix = netz.Indizes;
        for (int i = 0; i < ix.Length; i += 3)
            sb.Append("f ").Append(ix[i] + 1).Append(' ').Append(ix[i + 1] + 1).Append(' ').Append(ix[i + 2] + 1).Append('\n');
        return sb.ToString();
    }
}
