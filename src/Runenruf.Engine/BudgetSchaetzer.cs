namespace Runenruf.Engine;

/// <summary>
/// spec-minspec-budgets: Pi-5-Tauglichkeit wird gerechnet, nicht gehofft.
/// Niedrigste Stufe: Terrain 1 Drawcall, Einheiten instanziert je Volk,
/// UI als feste Obergrenze.
/// </summary>
public static class BudgetSchaetzer
{
    public const int DreieckeProEinheitNiedrig = 220;  // Low-Poly-Figur inkl. Waffe
    public const int UiDrawcallsMax = 24;

    public static FrameStatistik Schaetze(int terrainDreiecke, int einheiten, int sichtbareVoelker)
    {
        int dreiecke = terrainDreiecke + einheiten * DreieckeProEinheitNiedrig;
        int drawcalls = 1 + sichtbareVoelker + UiDrawcallsMax;  // Terrain + Instanz-Batches + UI
        return new FrameStatistik(dreiecke, drawcalls);
    }
}
