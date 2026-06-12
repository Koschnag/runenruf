namespace Runenruf.Engine;

/// <summary>
/// spec-fenster: Backend ohne Display — initialisiert die Technik, zaehlt Frames
/// und beendet sich sauber. Fuer CI, Tests und dedizierte Server.
/// </summary>
public sealed class HeadlessBackend : IRenderBackend
{
    private Netz? _terrain;

    public bool Initialisiert { get; private set; }
    public int GerenderteFrames { get; private set; }
    public FrameStatistik Statistik { get; private set; }

    public void Initialisiere() => Initialisiert = true;

    public void LadeTerrain(Netz netz) => _terrain = netz;

    public bool Frame(double deltaSekunden)
    {
        if (!Initialisiert) throw new InvalidOperationException("Initialisiere() zuerst.");
        GerenderteFrames++;
        Statistik = new FrameStatistik(_terrain?.Dreiecke ?? 0, _terrain is null ? 0 : 1);
        return true;
    }

    public void Dispose() => Initialisiert = false;
}
