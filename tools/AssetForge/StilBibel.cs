namespace AssetForge;

/// <summary>
/// Die Stil-Bibel (risk-stilbruch): zentrale Defaults für die SpellForce-1-Stimmung —
/// warme, erdige Paletten, verwitterter Stein, kühles Runenleuchten, weite Streicher.
/// Jedes Rezept erbt von hier; Stil wird an genau einer Stelle gesteuert.
/// </summary>
public static class StilBibel
{
    public static readonly (byte R, byte G, byte B) SteinDunkel = (64, 58, 50);
    public static readonly (byte R, byte G, byte B) SteinHell   = (158, 148, 130);
    public static readonly (byte R, byte G, byte B) Erde        = (107, 79, 42);
    public static readonly (byte R, byte G, byte B) ErdeHell    = (148, 116, 72);
    public static readonly (byte R, byte G, byte B) Gras        = (86, 110, 58);
    public static readonly (byte R, byte G, byte B) GrasHell    = (128, 148, 84);
    public static readonly (byte R, byte G, byte B) Gold        = (201, 162, 39);
    public static readonly (byte R, byte G, byte B) RunenGlut   = (122, 215, 255);

    public const int TexturGroesse = 512;

    // Min-Spec-Budgets (premise-minspec, spec-minspec-budgets) — niedrigste Stufe:
    public const int TerrainAufloesungNiedrig = 128;
    public const int BudgetDreieckeNiedrig    = 100_000;
    public const int BudgetDrawcallsNiedrig   = 150;

    public const int MusikSekundenStandard = 32;
    public const int MusikBpmStandard      = 84;
}
