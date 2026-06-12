namespace Runenruf.Engine;

/// <summary>Ein Dreiecksnetz, wie der Renderer es braucht.</summary>
public sealed record Netz(float[] Positionen, float[] Normalen, uint[] Indizes)
{
    public int Dreiecke => Indizes.Length / 3;
}

/// <summary>
/// Render-Abstraktion (risk-gl-macos): GL heute, Vulkan/MoltenVK als spaeterer
/// zweiter Implementierer — die Spiellogik kennt nur dieses Interface.
/// </summary>
public interface IRenderBackend : IDisposable
{
    void Initialisiere();
    void LadeTerrain(Netz netz);
    /// <summary>Rendert einen Frame; false = Fenster wurde geschlossen.</summary>
    bool Frame(double deltaSekunden);
    FrameStatistik Statistik { get; }
}

/// <summary>Messwerte eines Frames — Grundlage der Min-Spec-Budgets.</summary>
public readonly record struct FrameStatistik(int Dreiecke, int Drawcalls);
