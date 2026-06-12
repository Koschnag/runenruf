using AssetForge;
using Runenruf.Domain;
using Runenruf.Engine;

// Runenruf-Host: erzeugt den Weltsplitter aus seinem Rezept (adr-003 — Assets
// entstehen beim Start, git kennt nur Rezepte) und startet Simulation + Renderer.

bool kopflos = args.Contains("--headless");
ulong seed = 7;

var terrainRezept = PromptDeuter.Terrain("sanfte Huegel, schwebender Weltsplitter", seed);
var terrain = TerrainGenerator.Erzeuge(terrainRezept);
var netz = new Netz(terrain.Positionen, terrain.Normalen, terrain.Indizes);

using IRenderBackend backend = kopflos ? new HeadlessBackend() : new GlBackend();
backend.Initialisiere();
backend.LadeTerrain(netz);

if (kopflos)
{
    // spec-fenster: ohne Display initialisieren, kurz laufen, sauber beenden.
    for (int i = 0; i < 30; i++) backend.Frame(1 / 30.0);
    ulong hash = Spiel.kopfloserLauf(seed, 100);
    var budget = BudgetSchaetzer.Schaetze(netz.Dreiecke, einheiten: 200, sichtbareVoelker: 2);
    Console.WriteLine($"Runenruf headless ok — Sim-Hash nach 100 Ticks: {hash:x16}");
    Console.WriteLine($"Frame-Budget niedrig: {budget.Dreiecke} Dreiecke, {budget.Drawcalls} Drawcalls");
    return 0;
}

// Fenster-Pfad: 20-Hz-Simulation (adr-004), Renderer so schnell er kann.
var welt = Spiel.standardWelt(seed);
double simAkku = 0;
var uhr = System.Diagnostics.Stopwatch.StartNew();
double zuletzt = 0;
while (true)
{
    double jetzt = uhr.Elapsed.TotalSeconds;
    double delta = jetzt - zuletzt;
    zuletzt = jetzt;
    simAkku += delta;
    while (simAkku >= 0.05)
    {
        welt = Sim.tick(Microsoft.FSharp.Collections.FSharpList<Befehl>.Empty, welt);
        simAkku -= 0.05;
    }
    if (!backend.Frame(delta)) break;
}
return 0;
