using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetForge;

/// <summary>Asset-Rezepte (term-rezept, adr-003): deterministische Bauanleitungen statt Binärdaten.</summary>
public sealed record TexturRezept(string Beschreibung, ulong Seed, string Basis, bool Rune, bool Risse, int Groesse);
public sealed record TerrainRezept(string Beschreibung, ulong Seed, int Aufloesung, float HoehenSkala, float InselRadius);
public sealed record MusikRezept(string Beschreibung, ulong Seed, int Sekunden, string Stimmung, int Bpm);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TexturRezept))]
[JsonSerializable(typeof(TerrainRezept))]
[JsonSerializable(typeof(MusikRezept))]
public partial class RezeptJson : JsonSerializerContext;

/// <summary>
/// Übersetzt Prosa-Beschreibungen in Rezepte. Offline-Heuristik — ein LLM kann
/// dieselben Rezepte liefern (adr-003), ist aber nie Voraussetzung.
/// </summary>
public static class PromptDeuter
{
    private static bool Hat(string prompt, params string[] worte)
    {
        foreach (var w in worte)
            if (prompt.Contains(w, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static TexturRezept Textur(string prompt, ulong seed)
    {
        string basis = Hat(prompt, "gras", "wiese") ? "gras"
                     : Hat(prompt, "erde", "boden", "lehm") ? "erde"
                     : "stein";
        bool rune  = Hat(prompt, "rune", "leucht", "glut", "magisch");
        bool risse = Hat(prompt, "verwittert", "riss", "alt", "ruine");
        return new TexturRezept(prompt, seed, basis, rune, risse, StilBibel.TexturGroesse);
    }

    public static TerrainRezept Terrain(string prompt, ulong seed)
    {
        float hoehe = Hat(prompt, "gebirge", "schroff", "berg") ? 18f
                    : Hat(prompt, "huegel", "sanft") ? 8f
                    : 12f;
        return new TerrainRezept(prompt, seed, StilBibel.TerrainAufloesungNiedrig, hoehe, 0.82f);
    }

    public static MusikRezept Musik(string prompt, ulong seed)
    {
        string stimmung = Hat(prompt, "heroisch", "schlacht", "episch") ? "heroisch"
                        : Hat(prompt, "duester", "dunkel", "bedrohlich") ? "duester"
                        : "ruhig";
        return new MusikRezept(prompt, seed, StilBibel.MusikSekundenStandard, stimmung, StilBibel.MusikBpmStandard);
    }
}
