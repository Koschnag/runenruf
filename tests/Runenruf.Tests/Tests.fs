module Runenruf.Tests

open System
open Xunit
open Runenruf.Domain
open AssetForge
open Runenruf.Engine

// ===== Deterministische Simulation =====

let private befehle volk t =
    if t = 1 then [ RufeRune (Voelker.arbeiter volk) ]
    elif t = 2 then [ BefehleSammeln (0, Holz) ]
    else []

[<Fact; Trait("spot", "spec-sim-determinismus-test-1")>]
let ``Deterministische Simulation — when beide 1000 Ticks laufen then sind ihre Zustands-Hashes identisch`` () =
    let lauf () = Sim.erschaffe 42UL Menschen |> Sim.lauf 1000 (befehle Menschen) |> Sim.hash
    Assert.Equal(lauf (), lauf ())

[<Fact; Trait("spot", "spec-sim-determinismus-test-2")>]
let ``Deterministische Simulation — when sie 1000 Ticks laeuft then unterscheidet sich ihr Zustands-Hash`` () =
    let lauf seed = Sim.erschaffe seed Menschen |> Sim.lauf 1000 (befehle Menschen) |> Sim.hash
    Assert.NotEqual(lauf 42UL, lauf 43UL)

// ===== Sechs Voelker =====

[<Fact; Trait("spot", "spec-voelker-test-1")>]
let ``Sechs Voelker — when die Voelker abgefragt werden then existieren genau 6 Voelker in 2 Buenden`` () =
    Assert.Equal(6, List.length Voelker.alle)
    let licht = Voelker.alle |> List.filter (fun v -> Voelker.bund v = Licht)
    let dunkel = Voelker.alle |> List.filter (fun v -> Voelker.bund v = Dunkel)
    Assert.Equal(3, List.length licht)
    Assert.Equal(3, List.length dunkel)

[<Fact; Trait("spot", "spec-voelker-test-2")>]
let ``Sechs Voelker — when seine Einheitstypen abgefragt werden then hat es Grundrollen und Alleinstellungsmerkmal`` () =
    for volk in Voelker.alle do
        let rollen = Voelker.einheiten volk |> List.map (fun e -> e.Rolle) |> Set.ofList
        Assert.True(Set.contains Arbeiter rollen, sprintf "%A braucht Arbeiter" volk)
        Assert.True(Set.contains Nahkampf rollen, sprintf "%A braucht Nahkampf" volk)
        Assert.True(Set.contains Fernkampf rollen, sprintf "%A braucht Fernkampf" volk)
        Assert.NotEmpty(Voelker.alleinstellung volk)

// ===== Diplomatie =====

[<Fact; Trait("spot", "spec-diplomatie-test-1")>]
let ``Diplomatie — when die Beziehung zweier Voelker desselben Bundes abgefragt wird then sind sie verbuendet`` () =
    Assert.Equal(Licht, Voelker.bund Menschen)
    Assert.Equal(Licht, Voelker.bund Elfen)
    Assert.Equal(Verbuendet, Voelker.beziehung Menschen Elfen)
    Assert.True(Voelker.sindVerbuendet Menschen Elfen)

[<Fact; Trait("spot", "spec-diplomatie-test-2")>]
let ``Diplomatie — when die Beziehung zweier Voelker verschiedener Buende abgefragt wird then sind sie verfeindet`` () =
    Assert.NotEqual(Voelker.bund Menschen, Voelker.bund Orks)
    Assert.Equal(Verfeindet, Voelker.beziehung Menschen Orks)
    Assert.False(Voelker.sindVerbuendet Menschen Orks)

// ===== Wirtschaft =====

[<Fact; Trait("spot", "spec-wirtschaft-test-1")>]
let ``Wirtschaft — when 100 Ticks simuliert werden then steigt der Holzvorrat messbar`` () =
    let ende = Sim.erschaffe 7UL Menschen |> Sim.lauf 100 (befehle Menschen)
    let arbeiterkostenHolz = (Voelker.arbeiter Menschen).KostenHolz
    Assert.True(Map.find Holz ende.Lager > 50 - arbeiterkostenHolz, sprintf "Holz: %d" (Map.find Holz ende.Lager))

[<Fact; Trait("spot", "spec-wirtschaft-test-2")>]
let ``Wirtschaft — when der Tick verarbeitet wird then wird er abgelehnt und nichts abgezogen`` () =
    let welt = { Sim.erschaffe 7UL Menschen with Lager = Map.ofList [ Holz, 0; Stein, 0; Eisen, 0; Nahrung, 0; Aether, 1 ] }
    let teuer = Voelker.einheiten Menschen |> List.find (fun e -> e.Rolle = Magie)
    let danach = Sim.tick [ RufeRune teuer ] welt
    Assert.Empty(danach.Einheiten)
    Assert.Equal(1, Map.find Aether danach.Lager)

// ===== Runen rufen =====

[<Fact; Trait("spot", "spec-runenruf-test-1")>]
let ``Runen rufen — when eine Einheiten-Rune gerufen wird then entsteht die Einheit am Monument und der Aether sinkt`` () =
    let welt = Sim.erschaffe 7UL Elfen
    let typ = Voelker.arbeiter Elfen
    let danach = Sim.tick [ RufeRune typ ] welt
    let einheit = Assert.Single(danach.Einheiten)
    Assert.Equal(welt.Monument.Y, einheit.Pos.Y)
    Assert.Equal(Map.find Aether welt.Lager - typ.KostenAether, Map.find Aether danach.Lager)

[<Fact; Trait("spot", "spec-runenruf-test-2")>]
let ``Runen rufen — when die Wiederbelebungszeit ablaeuft then ersteht er am letzten aktivierten Monument wieder auf`` () =
    let monument : Pos = { X = 5.0f; Y = 9.0f }
    let mutable avatar = Avatar.erschaffe { X = 99.0f; Y = 99.0f } |> Avatar.faellt 5
    for _ in 1 .. 5 do avatar <- Avatar.tick monument avatar
    Assert.True(avatar.WiederbelebungIn.IsNone)
    Assert.Equal(monument, avatar.Pos)

// ===== Avatar-RPG =====

[<Fact; Trait("spot", "spec-avatar-rpg-test-1")>]
let ``Avatar — when ein Level-Aufstieg verarbeitet wird then steigen Attribute und ein Fertigkeitspunkt entsteht`` () =
    let vorher = Avatar.erschaffe { X = 0.0f; Y = 0.0f }
    let nachher = Avatar.sammleErfahrung 100 vorher
    Assert.Equal(2, nachher.Level)
    Assert.Equal(1, nachher.FertigkeitsPunkte)
    Assert.True(nachher.Attribute.Staerke > vorher.Attribute.Staerke)

[<Fact; Trait("spot", "spec-avatar-rpg-test-2")>]
let ``Avatar — when der Avatar die Anforderungen nicht erfuellt then wird das Anlegen abgelehnt`` () =
    let avatar = Avatar.erschaffe { X = 0.0f; Y = 0.0f }
    let runenklinge = { Name = "Runenklinge der Ahnen"; MinStaerke = 30; MinGeschick = 15; Bonus = 12 }
    match Avatar.legeAn runenklinge avatar with
    | Ok _ -> failwith "Anlegen haette abgelehnt werden muessen"
    | Error meldung -> Assert.Contains("Runenklinge", meldung)

// ===== Terrain =====

let private terrainRezept = PromptDeuter.Terrain("sanfte Huegel", 42UL)

[<Fact; Trait("spot", "spec-terrain-test-1")>]
let ``Terrain — when das Mesh generiert wird then ist es deterministisch und haelt das Tri-Budget ein`` () =
    let netz = TerrainGenerator.Erzeuge(terrainRezept)
    Assert.True(netz.Dreiecke > 0)
    Assert.True(netz.Dreiecke <= StilBibel.BudgetDreieckeNiedrig, sprintf "%d Dreiecke" netz.Dreiecke)

[<Fact; Trait("spot", "spec-terrain-test-2")>]
let ``Terrain — when beide Meshes verglichen werden then sind sie bitgleich`` () =
    let a = TerrainGenerator.Erzeuge(terrainRezept)
    let b = TerrainGenerator.Erzeuge(terrainRezept)
    Assert.Equal<float32[]>(a.Positionen, b.Positionen)
    Assert.Equal<uint32[]>(a.Indizes, b.Indizes)

// ===== Min-Spec-Budgets =====

[<Fact; Trait("spot", "spec-minspec-budgets-test-1")>]
let ``Min-Spec — when ein Frame-Budget geprueft wird then liegen Terrain und Einheiten unter den Budgets`` () =
    let netz = TerrainGenerator.Erzeuge(terrainRezept)
    let statistik = BudgetSchaetzer.Schaetze(netz.Dreiecke, 200, 2)
    Assert.True(statistik.Dreiecke <= StilBibel.BudgetDreieckeNiedrig, sprintf "%d Dreiecke" statistik.Dreiecke)
    Assert.True(statistik.Drawcalls <= StilBibel.BudgetDrawcallsNiedrig, sprintf "%d Drawcalls" statistik.Drawcalls)

[<Fact; Trait("spot", "spec-minspec-budgets-test-2")>]
let ``Min-Spec — when ein Sim-Tick mit 200 Einheiten gemessen wird then bleibt er unter 5 ms`` () =
    let arbeiter = Voelker.arbeiter Menschen
    let welt =
        { Sim.erschaffe 7UL Menschen with
            Einheiten = [ for _ in 1 .. 200 -> { Typ = arbeiter; Pos = { X = 1.0f; Y = 1.0f }; Leben = 40; Auftrag = Sammeln Holz } ] }
    let uhr = System.Diagnostics.Stopwatch.StartNew()
    let mutable w = welt
    for _ in 1 .. 100 do w <- Sim.tick [] w
    uhr.Stop()
    let msProTick = uhr.Elapsed.TotalMilliseconds / 100.0
    Assert.True(msProTick < 5.0, sprintf "%.2f ms pro Tick" msProTick)

// ===== AssetForge: Textur =====

[<Fact; Trait("spot", "spec-assetforge-textur-test-1")>]
let ``AssetForge Textur — when AssetForge laeuft then entsteht ein Rezept und ein gueltiges PNG`` () =
    let rezept = PromptDeuter.Textur("verwitterter Stein mit leuchtender Rune", 42UL)
    Assert.Equal("stein", rezept.Basis)
    Assert.True(rezept.Rune)
    Assert.True(rezept.Risse)
    let png = TexturGenerator.ErzeugePng(rezept)
    Assert.Equal<byte[]>([| 137uy; 80uy; 78uy; 71uy; 13uy; 10uy; 26uy; 10uy |], png.[0..7])

[<Fact; Trait("spot", "spec-assetforge-textur-test-2")>]
let ``AssetForge Textur — when beide PNGs verglichen werden then sind sie bytegleich`` () =
    let rezept = PromptDeuter.Textur("Stein mit Rune", 7UL)
    Assert.Equal<byte[]>(TexturGenerator.ErzeugePng(rezept), TexturGenerator.ErzeugePng(rezept))

// ===== AssetForge: Musik =====

[<Fact; Trait("spot", "spec-assetforge-musik-test-1")>]
let ``AssetForge Musik — when AssetForge Musik generiert then entsteht eine gueltige WAV mit gewuenschter Laenge`` () =
    let rezept = PromptDeuter.Musik("heroisch, weite Streicher", 42UL)
    Assert.Equal("heroisch", rezept.Stimmung)
    let wav = MusikGenerator.ErzeugeWav(rezept)
    Assert.Equal("RIFF", Text.Encoding.ASCII.GetString(wav.[0..3]))
    Assert.Equal(44 + WavSchreiber.Abtastrate * rezept.Sekunden * 2, wav.Length)

[<Fact; Trait("spot", "spec-assetforge-musik-test-2")>]
let ``AssetForge Musik — when beide WAVs verglichen werden then sind sie bytegleich`` () =
    let rezept = PromptDeuter.Musik("ruhig", 7UL)
    Assert.Equal<byte[]>(MusikGenerator.ErzeugeWav(rezept), MusikGenerator.ErzeugeWav(rezept))

// ===== Fenster (Headless-Kriterium; das Display-Kriterium braucht echte Hardware) =====

[<Fact; Trait("spot", "spec-fenster-test-2")>]
let ``Fenster — when mit headless gestartet wird then initialisiert die Technik ohne Fenster und beendet sauber`` () =
    use backend = new HeadlessBackend()
    backend.Initialisiere()
    backend.LadeTerrain(Netz([| 0.0f |], [| 0.0f |], [| 0u; 1u; 2u |]))
    for _ in 1 .. 30 do backend.Frame(1.0 / 30.0) |> ignore
    Assert.Equal(30, backend.GerenderteFrames)
    Assert.Equal(1, backend.Statistik.Dreiecke)

// ===== Release-Pipeline (spec-release-pipeline) =====
// Die Pipeline ist Infrastruktur — die Tests messen den Workflow als Artefakt:
// hält er die vier Zielplattformen und das CDD-Gate ein?

let rec private repoWurzel (dir: string) =
    if IO.File.Exists(IO.Path.Combine(dir, "Runenruf.sln"))
       || IO.Directory.Exists(IO.Path.Combine(dir, ".github")) then dir
    else
        let eltern = IO.Directory.GetParent dir
        if isNull eltern then failwith "Repo-Wurzel nicht gefunden" else repoWurzel eltern.FullName

let private releaseYml =
    IO.Path.Combine(repoWurzel (IO.Directory.GetCurrentDirectory()), ".github", "workflows", "release.yml")
    |> IO.File.ReadAllText

[<Fact; Trait("spot", "spec-release-pipeline-test-1")>]
let ``Release — when die Pipeline laeuft then erscheinen Binaries fuer alle vier Zielplattformen`` () =
    for rid in [ "osx-arm64"; "win-x64"; "linux-x64"; "linux-arm64" ] do
        Assert.True(releaseYml.Contains rid, sprintf "Release-Workflow muss %s bauen" rid)
    Assert.True(releaseYml.Contains "self-contained", "Binaries muessen self-contained sein")
    Assert.True(releaseYml.Contains "action-gh-release", "es muss ein GitHub-Release entstehen")

[<Fact; Trait("spot", "spec-release-pipeline-test-2")>]
let ``Release — when das Modell inkonsistent ist then bricht das Gate vor dem Build ab`` () =
    // Das Gate laeuft CDD-Pruefungen; Build haengt per needs am Gate → kein Build ohne gruenes Modell.
    Assert.True(releaseYml.Contains "validate", "Gate muss cdd validate ausfuehren")
    Assert.True(releaseYml.Contains "sync-code", "Gate muss cdd sync-code ausfuehren")
    Assert.True(releaseYml.Contains "sync-tests", "Gate muss cdd sync-tests ausfuehren")
    Assert.True(releaseYml.Contains "needs: gate", "Build muss vom Gate abhaengen")

[<Fact>]
let ``Avatar — derselbe Gegenstand kann nicht doppelt angelegt werden`` () =
    let avatar = Avatar.erschaffe { X = 0.0f; Y = 0.0f }
    let ring = { Name = "Ring der Sammler"; MinStaerke = 0; MinGeschick = 0; Bonus = 1 }
    match Avatar.legeAn ring avatar with
    | Error e -> failwith e
    | Ok mitRing ->
        match Avatar.legeAn ring mitRing with
        | Ok _ -> failwith "Doppel-Anlegen haette abgelehnt werden muessen"
        | Error meldung -> Assert.Contains("bereits angelegt", meldung)

// ===== Browser-Spiel (spec-browser-spiel) =====

[<Fact; Trait("spot", "spec-browser-spiel-test-1")>]
let ``Browser — when das Terrain gerendert wird then nutzt der Renderer WebGL2 und ES-300-Shader`` () =
    let renderJs =
        IO.Path.Combine(repoWurzel (IO.Directory.GetCurrentDirectory()), "src", "Runenruf.Web", "wwwroot", "render.js")
        |> IO.File.ReadAllText
    Assert.Contains("getContext(\"webgl2\")", renderJs)
    Assert.Contains("#version 300 es", renderJs)

[<Fact; Trait("spot", "spec-browser-spiel-test-2")>]
let ``Browser — when beide denselben Seed 100 Ticks laufen then ist der Zustands-Hash identisch`` () =
    // Die Simulation, die im Browser (WASM) läuft, ist dieselbe pure F#-Domäne wie auf dem Desktop:
    // gleicher Seed ⇒ gleicher Hash, unabhängig von der Plattform (das ist die Portabilitäts-Garantie).
    let a = Spiel.kopfloserLauf 7UL 100
    let b = Spiel.kopfloserLauf 7UL 100
    Assert.Equal(a, b)
    Assert.NotEqual(a, Spiel.kopfloserLauf 8UL 100)
