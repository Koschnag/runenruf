namespace AssetForge;

/// <summary>
/// spec-assetforge-musik: prozedural komponierte Stücke in 2003er-Fantasy-Stimmung —
/// Bordun, weite Streicherflächen (verstimmte Sägezähne), Flöten-Melodie in Dorisch.
/// Deterministisch aus dem Rezept, loopbar durch Ein-/Ausblenden.
/// </summary>
public static class MusikGenerator
{
    private const double GrundTon = 146.83; // D3

    // Dorische Skala als Halbtonschritte
    private static readonly int[] Skala = [0, 2, 3, 5, 7, 9, 10];

    private static double Frequenz(int halbtoene) => GrundTon * Math.Pow(2.0, halbtoene / 12.0);

    private static int[][] Akkordfolge(string stimmung) => stimmung switch
    {
        // (Halbtöne relativ zu D) i — VII — i — v  bzw. Varianten
        "heroisch" => [[0, 3, 7], [-2, 2, 5], [0, 3, 7], [5, 8, 12]],
        "duester"  => [[0, 3, 7], [1, 5, 8], [0, 3, 7], [-4, 0, 3]],
        _          => [[0, 3, 7], [-2, 2, 5], [3, 7, 10], [0, 3, 7]],
    };

    public static byte[] ErzeugeWav(MusikRezept r)
    {
        int sr = WavSchreiber.Abtastrate;
        int n = sr * r.Sekunden;
        var puffer = new double[n];
        var rng = new Saat(r.Seed);
        var folge = Akkordfolge(r.Stimmung);
        double taktSek = 240.0 / r.Bpm;             // ein Akkord = 4 Schläge
        double achtelSek = 30.0 / r.Bpm;

        // 1) Bordun: tiefe Quinte, das Fundament
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)sr;
            puffer[i] += 0.10 * Math.Sin(2 * Math.PI * Frequenz(-12) * t)
                       + 0.05 * Math.Sin(2 * Math.PI * Frequenz(-5) * t);
        }

        // 2) Streicherfläche: je Akkordton drei verstimmte Sägezähne, weicher Einsatz
        for (int akkord = 0; ; akkord++)
        {
            double start = akkord * taktSek;
            if (start >= r.Sekunden) break;
            foreach (int ton in folge[akkord % folge.Length])
            {
                double f = Frequenz(ton);
                foreach (double verstimmung in (double[])[0.997, 1.0, 1.004])
                {
                    double fv = f * verstimmung;
                    int von = (int)(start * sr), bis = Math.Min((int)((start + taktSek) * sr), n);
                    for (int i = von; i < bis; i++)
                    {
                        double t = i / (double)sr, lokal = t - start;
                        double huelle = Math.Min(lokal / 0.9, 1.0) * Math.Min((taktSek - lokal) / 0.6, 1.0);
                        double phase = t * fv % 1.0;
                        puffer[i] += 0.030 * (2.0 * phase - 1.0) * Math.Max(huelle, 0.0);
                    }
                }
            }
        }

        // 3) Flöten-Melodie: gewichteter Zufallsgang über der Skala, Vibrato, Pausen
        int stufe = 4;
        for (double start = 0; start < r.Sekunden - achtelSek; start += achtelSek * rng.Bereich(1, 4))
        {
            if (rng.Anteil() < 0.25) continue;   // Atempause
            stufe = Math.Clamp(stufe + rng.Bereich(-2, 3), 0, Skala.Length * 2 - 1);
            double f = Frequenz(Skala[stufe % Skala.Length] + 12 * (1 + stufe / Skala.Length));
            double dauer = achtelSek * rng.Bereich(1, 4);
            int von = (int)(start * sr), bis = Math.Min((int)((start + dauer) * sr), n);
            for (int i = von; i < bis; i++)
            {
                double t = i / (double)sr, lokal = t - start;
                double vibrato = 1.0 + 0.006 * Math.Sin(2 * Math.PI * 5.2 * lokal);
                double huelle = Math.Min(lokal / 0.05, 1.0) * Math.Exp(-lokal * 1.8);
                puffer[i] += 0.16 * huelle * Math.Sin(2 * Math.PI * f * vibrato * t);
            }
        }

        // Normalisieren + Loop-Blenden
        double max = 1e-9;
        foreach (double p in puffer) max = Math.Max(max, Math.Abs(p));
        int blende = sr;
        for (int i = 0; i < n; i++)
        {
            double gain = 0.85 / max;
            if (i < blende) gain *= i / (double)blende;
            if (i >= n - blende) gain *= (n - 1 - i) / (double)blende;
            puffer[i] *= gain;
        }
        return WavSchreiber.Schreibe(puffer);
    }
}
