using System.Text.Json;

namespace AssetForge;

/// <summary>
/// AssetForge-CLI (adr-003): Beschreibung rein, Asset + Rezept raus.
///   assetforge textur  --prompt "verwitterter Stein mit leuchtender Rune" [--seed 7] [--out pfad.png]
///   assetforge terrain --prompt "sanfte Huegel"                            [--seed 7] [--out pfad.obj]
///   assetforge musik   --prompt "heroisch, weite Streicher"               [--seed 7] [--out pfad.wav]
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0) return Hilfe();
        string befehl = args[0];
        string prompt = Wert(args, "--prompt") ?? "";
        ulong seed = ulong.TryParse(Wert(args, "--seed"), out var s) ? s : 7UL;

        try
        {
            switch (befehl)
            {
                case "textur":
                {
                    var rezept = PromptDeuter.Textur(prompt, seed);
                    string pfad = Wert(args, "--out") ?? Standardpfad("textur", seed, "png");
                    Schreibe(pfad, TexturGenerator.ErzeugePng(rezept));
                    SchreibeRezept(pfad, JsonSerializer.Serialize(rezept, RezeptJson.Default.TexturRezept));
                    return 0;
                }
                case "terrain":
                {
                    var rezept = PromptDeuter.Terrain(prompt, seed);
                    string pfad = Wert(args, "--out") ?? Standardpfad("terrain", seed, "obj");
                    var netz = TerrainGenerator.Erzeuge(rezept);
                    Schreibe(pfad, System.Text.Encoding.UTF8.GetBytes(TerrainGenerator.AlsObj(netz)));
                    SchreibeRezept(pfad, JsonSerializer.Serialize(rezept, RezeptJson.Default.TerrainRezept));
                    Console.WriteLine($"Dreiecke: {netz.Dreiecke} (Budget niedrig: {StilBibel.BudgetDreieckeNiedrig})");
                    return 0;
                }
                case "musik":
                {
                    var rezept = PromptDeuter.Musik(prompt, seed);
                    string pfad = Wert(args, "--out") ?? Standardpfad("musik", seed, "wav");
                    Schreibe(pfad, MusikGenerator.ErzeugeWav(rezept));
                    SchreibeRezept(pfad, JsonSerializer.Serialize(rezept, RezeptJson.Default.MusikRezept));
                    return 0;
                }
                default:
                    return Hilfe();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fehler: {ex.Message}");
            return 1;
        }
    }

    private static string? Wert(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    private static string Standardpfad(string art, ulong seed, string endung)
    {
        Directory.CreateDirectory(Path.Combine("assets", "generiert"));
        return Path.Combine("assets", "generiert", $"{art}-{seed}.{endung}");
    }

    private static void Schreibe(string pfad, byte[] daten)
    {
        File.WriteAllBytes(pfad, daten);
        Console.WriteLine($"geschrieben: {pfad} ({daten.Length / 1024} KiB)");
    }

    private static void SchreibeRezept(string assetPfad, string json) =>
        File.WriteAllText(assetPfad + ".rezept.json", json);

    private static int Hilfe()
    {
        Console.WriteLine("AssetForge — Beschreibung rein, Asset raus (deterministisch aus Rezepten)");
        Console.WriteLine("  assetforge textur|terrain|musik --prompt \"...\" [--seed n] [--out pfad]");
        return 2;
    }
}
