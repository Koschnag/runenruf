namespace AssetForge;

/// <summary>
/// spec-assetforge-textur: Stein/Erde/Gras-Texturen mit Verwitterung und
/// leuchtenden Runen-Ornamenten — deterministisch aus dem Rezept.
/// </summary>
public static class TexturGenerator
{
    public static byte[] ErzeugePng(TexturRezept r)
    {
        int n = r.Groesse;
        var px = new byte[n * n * 4];
        var (dunkel, hell) = r.Basis switch
        {
            "gras" => (StilBibel.Gras, StilBibel.GrasHell),
            "erde" => (StilBibel.Erde, StilBibel.ErdeHell),
            _      => (StilBibel.SteinDunkel, StilBibel.SteinHell),
        };

        var striche = r.Rune ? RunenStriche(r.Seed) : [];

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float u = x / (float)n, v = y / (float)n;
            float t = Rauschen.Fbm(r.Seed, u * 8f, v * 8f, 5);

            if (r.Risse)
            {
                // Risse: schmale dunkle Täler im Rauschen
                float riss = MathF.Abs(Rauschen.Fbm(r.Seed + 101, u * 5f, v * 5f, 4) - 0.5f);
                if (riss < 0.018f) t *= 0.45f;
            }

            float rr = float.Lerp(dunkel.R, hell.R, t);
            float gg = float.Lerp(dunkel.G, hell.G, t);
            float bb = float.Lerp(dunkel.B, hell.B, t);

            if (striche.Length > 0)
            {
                // Runen-Glyphe im Zentrum mit Glut-Falloff (term-rune)
                float d = AbstandZuGlyphe(striche, u, v);
                float glut = MathF.Exp(-d * d * 2600f);
                rr = float.Lerp(rr, StilBibel.RunenGlut.R, glut);
                gg = float.Lerp(gg, StilBibel.RunenGlut.G, glut);
                bb = float.Lerp(bb, StilBibel.RunenGlut.B, glut);
            }

            int i = (y * n + x) * 4;
            px[i] = (byte)Math.Clamp(rr, 0f, 255f); px[i + 1] = (byte)Math.Clamp(gg, 0f, 255f); px[i + 2] = (byte)Math.Clamp(bb, 0f, 255f); px[i + 3] = 255;
        }
        return PngSchreiber.Schreibe(n, n, px);
    }

    /// <summary>Prozedurale Glyphe: Strichzüge auf einem 4×4-Gitter im Texturzentrum.</summary>
    private static (float X1, float Y1, float X2, float Y2)[] RunenStriche(ulong seed)
    {
        var rng = new Saat(seed ^ 0xABCDEF);
        int anzahl = rng.Bereich(4, 8);
        var striche = new (float, float, float, float)[anzahl];
        float gx = 0.35f, gw = 0.30f / 3f;
        int px = rng.Bereich(0, 4), py = rng.Bereich(0, 4);
        for (int i = 0; i < anzahl; i++)
        {
            int nx = Math.Clamp(px + rng.Bereich(-1, 2), 0, 3);
            int ny = Math.Clamp(py + rng.Bereich(-1, 2), 0, 3);
            if (nx == px && ny == py) ny = (ny + 1) % 4;
            striche[i] = (gx + px * gw, gx + py * gw, gx + nx * gw, gx + ny * gw);
            px = nx; py = ny;
        }
        return striche;
    }

    private static float AbstandZuGlyphe((float X1, float Y1, float X2, float Y2)[] striche, float u, float v)
    {
        float min = float.MaxValue;
        foreach (var (x1, y1, x2, y2) in striche)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float laenge2 = dx * dx + dy * dy;
            float t = laenge2 > 0 ? Math.Clamp(((u - x1) * dx + (v - y1) * dy) / laenge2, 0f, 1f) : 0f;
            float px = x1 + t * dx - u, py = y1 + t * dy - v;
            min = MathF.Min(min, MathF.Sqrt(px * px + py * py));
        }
        return min;
    }
}
