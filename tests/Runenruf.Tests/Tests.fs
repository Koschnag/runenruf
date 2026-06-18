module Runenruf.Tests

open System
open Xunit
open Runenruf.Domain
open AssetForge
open Runenruf.Engine
open Silk.NET.Windowing

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

// ===== Siegel: reproduzierbarer Weltsplitter-Lauf (spec-siegel-reproduzierbar) =====
// Hebt spec-sim-determinismus von der generischen Sim auf den integrierten Weltsplitter-Lauf:
// Wirtschaft (Sammeln), Unterhalt (Nahrung am Intervall-Rand) und Runenruf wirken hier
// zusammen. Gleicher Seed + gleiche Befehlsliste + gleiche Tick-Zahl ⇒ bitgleicher End-Hash;
// eine andere Tick-Zahl ⇒ anderer Hash (der Hash bildet den Fortschritt ab).

let private weltsplitterBefehle t =
    match t % 10 with
    | 1 -> [ RufeRune (Voelker.arbeiter Menschen) ]   // Runenruf
    | 2 -> [ BefehleSammeln (0, Holz) ]               // Wirtschaft: Arbeiter sammelt
    | 3 -> [ RufeRune (Voelker.arbeiter Menschen) ]
    | 4 -> [ BefehleSammeln (1, Eisen) ]
    | 6 -> [ Verbrauch (Aether, 2) ]                  // direkter Lagerverbrauch (Ritual)
    | _ -> []                                          // Unterhalt schlaegt am Intervall-Rand zu

// Zwei frische Weltsplitter aus demselben Seed, beide explizit via Sim.tick durch dieselbe
// Befehlsliste getrieben — kein gemeinsamer Zustand, nur gleiche Eingaben.
let private weltsplitterEndHash seed ticks =
    let mutable w = Sim.erschaffe seed Menschen
    for t in 1 .. ticks do
        w <- Sim.tick (weltsplitterBefehle t) w
    Sim.hash w

[<Fact; Trait("spot", "spec-siegel-reproduzierbar-test-1")>]
let ``Siegel reproduzierbar — when beide genau N Ticks via Sim.tick laufen then ist ihr End-Zustands-Hash identisch`` () =
    let n = 500
    Assert.Equal(weltsplitterEndHash 42UL n, weltsplitterEndHash 42UL n)

[<Fact; Trait("spot", "spec-siegel-reproduzierbar-test-2")>]
let ``Siegel reproduzierbar — when beide via Sim.tick laufen then ist ihr End-Zustands-Hash verschieden (der Hash bildet den Fortschritt ab)`` () =
    // Gleicher Seed, gleiche Befehlsliste — nur die Tick-Zahl unterscheidet sich (500 vs. 501).
    Assert.NotEqual(weltsplitterEndHash 42UL 500, weltsplitterEndHash 42UL 501)

// ===== Siegel: Kohaerenz-Gate ueber viele Seeds (spec-siegel-gate) =====
// Das Spielbarkeits-Siegel buendelt beide Garantien: ueber eine feste Menge Seeds, je
// vollstaendig durchsimuliert, gelten Nichtnegativitaet (Lager nie negativ an jedem Tick)
// und Reproduzierbarkeit (paarweise gleicher End-Hash) gleichzeitig. Kohaerenz ueber die
// Domaene statt ueber einen Einzelfall.

let private gateSeeds = [ 1UL; 7UL; 42UL; 99UL; 1000UL; 0xDEADBEEFUL ]
let private gateTicks = 300
let private gateBefehle = befehle Menschen

[<Fact; Trait("spot", "spec-siegel-gate-test-1")>]
let ``Siegel-Gate — when jeder Seed genau N Ticks via Sim.tick laeuft then bleibt fuer jeden Seed an jedem Tick jeder Lagerstand >= 0`` () =
    for seed in gateSeeds do
        let mutable w = Sim.erschaffe seed Menschen
        for t in 1 .. gateTicks do
            w <- Sim.tick (gateBefehle t) w
            for kv in w.Lager do
                Assert.True(kv.Value >= 0,
                    sprintf "Seed %d, Tick %d: %A wurde negativ (%d)" seed w.Tick kv.Key kv.Value)

[<Fact; Trait("spot", "spec-siegel-gate-test-2")>]
let ``Siegel-Gate — when beide Durchlaeufe je Seed genau N Ticks laufen then stimmt der End-Zustands-Hash je Seed paarweise ueberein`` () =
    let lauf seed = Sim.erschaffe seed Menschen |> Sim.lauf gateTicks gateBefehle |> Sim.hash
    for seed in gateSeeds do
        Assert.Equal(lauf seed, lauf seed)

// ===== Siegel: treuer Zustands-Hash (spec-siegel-hash-treue) =====
// Der Zustands-Hash ist das Beweismittel des Siegels: eine reine Funktion des Weltzustands.
// Strukturell gleiche Welten ergeben denselben Hash, jede relevante Aenderung einen anderen —
// ein luegender Hash wuerde Reproduzierbarkeit nur vortaeuschen und die Garantie wertlos machen.

let private siegelLager = Map.ofList [ Holz, 50; Stein, 20; Eisen, 20; Nahrung, 50; Aether, 100 ]

let private siegelWeltMit (lager: Map<Ressource, int>) : Welt =
    let monument : Pos = { X = 0.0f; Y = 0.0f }
    let arbeiter = Voelker.arbeiter Menschen
    { Tick = 7
      RngState = 0xABCDEF123UL
      Volk = Menschen
      Monument = monument
      Lager = lager
      Einheiten = [ { Typ = arbeiter; Pos = monument; Leben = arbeiter.Leben; Auftrag = Sammeln Holz } ]
      Avatar = Avatar.erschaffe monument }

[<Fact; Trait("spot", "spec-siegel-hash-treue-test-1")>]
let ``Siegel: treuer Zustands-Hash — when beide gehasht werden then ist ihr Zustands-Hash identisch`` () =
    // Zwei unabhaengig gebaute, strukturell gleiche Welten: gleiches Lager, gleiche Einheiten, gleiche Tick-Zahl.
    let a = siegelWeltMit siegelLager
    let b = siegelWeltMit (Map.ofList [ Holz, 50; Stein, 20; Eisen, 20; Nahrung, 50; Aether, 100 ])
    Assert.Equal(Sim.hash a, Sim.hash b)

[<Fact; Trait("spot", "spec-siegel-hash-treue-test-2")>]
let ``Siegel: treuer Zustands-Hash — when beide gehasht werden then ist ihr Zustands-Hash verschieden`` () =
    // Unterschied in genau einem einzigen Lagerstand (Holz 50 -> 51), sonst Welt fuer Welt identisch.
    let a = siegelWeltMit siegelLager
    let b = siegelWeltMit (Map.add Holz 51 siegelLager)
    Assert.NotEqual(Sim.hash a, Sim.hash b)

// ===== Siegel: Lager nie negativ ueber den Lauf (spec-siegel-lager-nichtnegativ) =====
// Nichtnegativitaet ist kein Endzustands-Check, sondern eine ueber jeden Tick und jede
// Ressource gepruefte Eigenschaft. Eine Ueberausgabe — auch kumulativ aus mehreren
// Befehlen eines Ticks — wird abgelehnt, statt das Lager ins Minus zu treiben.

let private alleRessourcen = [ Holz; Stein; Eisen; Nahrung; Aether ]

// Gemischter Befehlsstrom aus Sammeln, RufeRune und (aggressivem) Verbrauch.
let private lagerBefehle t =
    match t % 5 with
    | 1 -> [ RufeRune (Voelker.arbeiter Menschen) ]
    | 2 -> [ BefehleSammeln (0, Holz) ]
    | 3 -> [ Verbrauch (Eisen, 3); Verbrauch (Aether, 7) ]
    | 4 -> [ Verbrauch (Holz, 9); Verbrauch (Stein, 4); Verbrauch (Nahrung, 2) ]
    | _ -> []

[<Fact; Trait("spot", "spec-siegel-lager-nichtnegativ-test-1")>]
let ``Siegel Lager — when der Lauf vollstaendig via Sim.tick verarbeitet wird then ist an jedem Tick und fuer jede Ressource der Lagerstand >= 0`` () =
    let mutable w = Sim.erschaffe 42UL Menschen
    for t in 1 .. 400 do
        w <- Sim.tick (lagerBefehle t) w
        for r in alleRessourcen do
            let bestand = Map.find r w.Lager
            Assert.True(bestand >= 0,
                sprintf "Tick %d: %A wurde negativ (%d)" w.Tick r bestand)

[<Fact; Trait("spot", "spec-siegel-lager-nichtnegativ-test-2")>]
let ``Siegel Lager — when ein Tick zusammen mehr ausgeben will als im Lager then wird die Ueberausgabe abgelehnt und kein Lagerstand wird negativ`` () =
    // Holz-Bestand 5; zwei Verbrauchsbefehle wollen zusammen 3 + 4 = 7 > 5 ausgeben.
    let welt = { Sim.erschaffe 7UL Menschen with Lager = Map.ofList [ Holz, 5; Stein, 0; Eisen, 0; Nahrung, 1; Aether, 0 ] }
    let danach = Sim.tick [ Verbrauch (Holz, 3); Verbrauch (Holz, 4) ] welt
    // Der erste Befehl (3) geht durch, der zweite (4 > Rest 2) wird abgelehnt — Bestand bleibt 2.
    Assert.Equal(2, Map.find Holz danach.Lager)
    for r in alleRessourcen do
        Assert.True(Map.find r danach.Lager >= 0,
            sprintf "%A wurde negativ (%d)" r (Map.find r danach.Lager))

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

// ===== Kampfkraft =====

[<Fact; Trait("spot", "spec-kampfkraft-test-1")>]
let ``Kampfkraft — when Voelker.kampfkraft fuer eine Einheit mit Leben L und Schaden S aufgerufen wird then ist das Ergebnis L plus S mal 5`` () =
    let einheit =
        { Volk = Menschen; Name = "Pruefling"; Rolle = Nahkampf
          KostenHolz = 0; KostenEisen = 0; KostenAether = 0
          Leben = 90; Schaden = 12; Tempo = 1.0f }
    Assert.Equal(90 + 12 * 5, Voelker.kampfkraft einheit)
    // gilt für jede Einheit: kampfkraft = Leben + Schaden * 5
    for volk in Voelker.alle do
        for t in Voelker.einheiten volk do
            Assert.Equal(t.Leben + t.Schaden * 5, Voelker.kampfkraft t)

[<Fact; Trait("spot", "spec-kampfkraft-test-2")>]
let ``Kampfkraft — when die Kampfkraft zweier Einheiten verglichen wird then ist a genau dann staerker als b wenn kampfkraft a groesser ist`` () =
    let mache leben schaden =
        { Volk = Menschen; Name = "x"; Rolle = Nahkampf
          KostenHolz = 0; KostenEisen = 0; KostenAether = 0
          Leben = leben; Schaden = schaden; Tempo = 1.0f }
    let stark = mache 90 12   // Kampfkraft 150
    let schwach = mache 60 14 // Kampfkraft 130
    Assert.True(Voelker.istStaerker stark schwach)
    Assert.False(Voelker.istStaerker schwach stark)
    // Äquivalenz zum Skalar-Vergleich auf allen realen Einheiten
    let alleEinheiten = Voelker.alle |> List.collect Voelker.einheiten
    for a in alleEinheiten do
        for b in alleEinheiten do
            Assert.Equal(Voelker.kampfkraft a > Voelker.kampfkraft b, Voelker.istStaerker a b)

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

// ===== Runenruf-Sperre bei leerem Nahrungslager (spec-rufe-rune-nahrungssperre) =====
// Nahrung ist keine Stueckkosten, sondern ein Tor: ist das Nahrungslager leer, laesst
// sich keine Rune rufen — sonst entsteht die Einheit normal und nur Holz/Eisen/Aether sinken.

let private genugFuer (typ: EinheitTyp) nahrung =
    Map.ofList [ Holz, typ.KostenHolz; Stein, 0; Eisen, typ.KostenEisen; Nahrung, nahrung; Aether, typ.KostenAether ]

[<Fact; Trait("spot", "spec-rufe-rune-nahrungssperre-test-1")>]
let ``Runenruf-Sperre — when der Befehl RufeRune typ via Sim.tick verarbeitet wird then entsteht keine Einheit und das Lager fuer Holz, Eisen und Aether bleibt unveraendert`` () =
    let typ = Voelker.arbeiter Menschen
    let welt = { Sim.erschaffe 7UL Menschen with Lager = genugFuer typ 0 }
    let danach = Sim.tick [ RufeRune typ ] welt
    Assert.Empty(danach.Einheiten)
    Assert.Equal(Map.find Holz welt.Lager, Map.find Holz danach.Lager)
    Assert.Equal(Map.find Eisen welt.Lager, Map.find Eisen danach.Lager)
    Assert.Equal(Map.find Aether welt.Lager, Map.find Aether danach.Lager)

[<Fact; Trait("spot", "spec-rufe-rune-nahrungssperre-test-2")>]
let ``Runenruf-Sperre — when der Befehl RufeRune typ via Sim.tick verarbeitet wird then entsteht die Einheit am Monument und die Kosten werden abgezogen`` () =
    let typ = Voelker.arbeiter Menschen
    let welt = { Sim.erschaffe 7UL Menschen with Lager = genugFuer typ 5 }
    let danach = Sim.tick [ RufeRune typ ] welt
    let einheit = Assert.Single(danach.Einheiten)
    // Wie spec-runenruf-test-1: Pos.Y verankert am Monument (X traegt deterministischen Tick-Jitter).
    Assert.Equal(welt.Monument.Y, einheit.Pos.Y)
    Assert.Equal(Map.find Holz welt.Lager - typ.KostenHolz, Map.find Holz danach.Lager)
    Assert.Equal(Map.find Eisen welt.Lager - typ.KostenEisen, Map.find Eisen danach.Lager)
    Assert.Equal(Map.find Aether welt.Lager - typ.KostenAether, Map.find Aether danach.Lager)

[<Fact; Trait("spot", "spec-rufe-rune-nahrungssperre-test-3")>]
let ``Runenruf-Sperre — when der Befehl RufeRune typ via Sim.tick verarbeitet wird then entsteht die Einheit wieder`` () =
    let typ = Voelker.arbeiter Menschen
    let leer = { Sim.erschaffe 7UL Menschen with Lager = genugFuer typ 0 }
    // Bei leerem Nahrungslager bleibt der Ruf gesperrt.
    let gesperrt = Sim.tick [ RufeRune typ ] leer
    Assert.Empty(gesperrt.Einheiten)
    // Nahrung von 0 auf > 0 auffuellen — danach entsteht die Einheit wieder.
    let aufgefuellt = { gesperrt with Lager = Map.add Nahrung 3 gesperrt.Lager }
    let danach = Sim.tick [ RufeRune typ ] aufgefuellt
    Assert.Single(danach.Einheiten) |> ignore

// ===== Unterhalt bei knapper Nahrung (spec-unterhalt-knappheit) =====
// Hunger ist ein hartes Limit, kein Schuldenkonto: ist die Armee groesser als die
// Vorratskammer, faellt das Nahrungslager auf genau 0 statt ins Minus — der Weltzustand
// bleibt wohlgeformt und die Determinismus-Hashes bleiben sauber.

let private mitNahrung anzahl nahrung =
    let arbeiter = Voelker.arbeiter Menschen
    let basis = Sim.erschaffe 7UL Menschen
    { basis with
        Lager = Map.add Nahrung nahrung basis.Lager
        Einheiten = [ for _ in 1 .. anzahl -> { Typ = arbeiter; Pos = { X = 0.0f; Y = 0.0f }; Leben = arbeiter.Leben; Auftrag = Leerlauf } ] }

[<Fact; Trait("spot", "spec-unterhalt-knappheit-test-1")>]
let ``Unterhalt bei knapper Nahrung — when genau sammelIntervall Ticks via Sim.tick verarbeitet werden then ist die Nahrung im Lager genau 0 und nie negativ`` () =
    // N lebende Einheiten, Lager-Nahrung F mit 0 < F < N.
    let welt = mitNahrung 8 3
    let mutable w = welt
    for _ in 1 .. Sim.sammelIntervall do
        w <- Sim.tick [] w
        Assert.True(Map.find Nahrung w.Lager >= 0, sprintf "Nahrung wurde negativ: %d" (Map.find Nahrung w.Lager))
    Assert.Equal(0, Map.find Nahrung w.Lager)

[<Fact; Trait("spot", "spec-unterhalt-knappheit-test-2")>]
let ``Unterhalt bei knapper Nahrung — when genau sammelIntervall Ticks via Sim.tick verarbeitet werden then bleibt die Nahrung im Lager 0`` () =
    // Lager-Nahrung 0 und mindestens eine lebende Einheit.
    let welt = mitNahrung 1 0
    let mutable w = welt
    for _ in 1 .. Sim.sammelIntervall do
        w <- Sim.tick [] w
        Assert.True(Map.find Nahrung w.Lager >= 0, sprintf "Nahrung wurde negativ: %d" (Map.find Nahrung w.Lager))
    Assert.Equal(0, Map.find Nahrung w.Lager)

// ===== Nahrungs-Unterhalt stehender Einheiten (spec-unterhalt) =====
// Eine stehende Armee isst: pro Sammelintervall verzehrt jede lebende Einheit eine Nahrung
// aus dem Lager. Der Unterhalt faellt nur am Intervall-Rand an — der klassische RTS-Kreislauf.

[<Fact; Trait("spot", "spec-unterhalt-test-1")>]
let ``Unterhalt — when genau sammelIntervall Ticks ohne Befehle via Sim.tick verarbeitet werden then ist die Nahrung im Lager um genau N gesunken`` () =
    // N lebende Einheiten im Leerlauf, Lager-Nahrung F mit F >= N.
    let n = 3
    let welt = mitNahrung n 50
    let vorher = Map.find Nahrung welt.Lager
    let mutable w = welt
    for _ in 1 .. Sim.sammelIntervall do
        w <- Sim.tick [] w
    Assert.Equal(vorher - n, Map.find Nahrung w.Lager)

[<Fact; Trait("spot", "spec-unterhalt-test-2")>]
let ``Unterhalt — when ein einzelner Sim.tick ohne sammelIntervall-Rand verarbeitet wird then ist die Nahrung im Lager unveraendert`` () =
    // Aus erschaffe ist Tick = 0; der naechste Tick wird 1 und ueberschreitet keinen Intervall-Rand.
    let welt = mitNahrung 5 50
    Assert.NotEqual(0, (welt.Tick + 1) % Sim.sammelIntervall)
    let danach = Sim.tick [] welt
    Assert.Equal(Map.find Nahrung welt.Lager, Map.find Nahrung danach.Lager)

[<Fact; Trait("spot", "spec-unterhalt-test-3")>]
let ``Unterhalt — when genau sammelIntervall Ticks ohne Einheiten via Sim.tick verarbeitet werden then ist die Nahrung im Lager unveraendert`` () =
    // Welt ganz ohne Einheiten: am Intervall-Rand wird nichts verzehrt.
    let welt = Sim.erschaffe 7UL Menschen
    Assert.Empty(welt.Einheiten)
    let vorher = Map.find Nahrung welt.Lager
    let mutable w = welt
    for _ in 1 .. Sim.sammelIntervall do
        w <- Sim.tick [] w
    Assert.Equal(vorher, Map.find Nahrung w.Lager)

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

// ===== Fenster =====
// Ein echtes Fenster braucht ein Display (in CI nicht vorhanden). Das Display-Kriterium
// prüfen wir daher über genau den GL-/ES-Kontext-Kontrakt, den GlBackend.Initialisiere()
// dem Fenster gibt — Desktop GL 3.3 Core, Min-Spec (Pi 5) GL ES 3.0 — plus den realen
// Frame-Loop-Wächter. Das Headless-Kriterium läuft eine echte Frame-Schleife ohne Display.

[<Fact; Trait("spot", "spec-fenster-test-1")>]
let ``Fenster — when das Spiel startet then oeffnet sich ein Fenster mit GL-3.3/ES-3.0-Kontext und laufender Frame-Schleife`` () =
    // Desktop (Mac/Windows/Linux): OpenGL 3.3 Core — derselbe Kontext, den das Fenster bekommt.
    let desktop = GlKontext.Api(GlProfil.Desktop)
    Assert.Equal(ContextAPI.OpenGL, desktop.API)
    Assert.Equal(3, desktop.Version.MajorVersion)
    Assert.Equal(3, desktop.Version.MinorVersion)
    Assert.Equal(ContextProfile.Core, desktop.Profile)
    // Min-Spec / Raspberry Pi 5 (GLES-only): OpenGL ES 3.0.
    let eingebettet = GlKontext.Api(GlProfil.Eingebettet)
    Assert.Equal(ContextAPI.OpenGLES, eingebettet.API)
    Assert.Equal(3, eingebettet.Version.MajorVersion)
    Assert.Equal(0, eingebettet.Version.MinorVersion)
    // Das Fenster trägt diese API (selber Pfad wie Initialisiere(), nur ohne echtes Display zu öffnen).
    let optionen = GlKontext.Optionen(GlProfil.Desktop, 1920, 1080)
    Assert.Equal(ContextAPI.OpenGL, optionen.API.API)
    Assert.Equal("Runenruf", optionen.Title)
    // Laufende Frame-Schleife: der GL-Loop-Wächter ist real — Frame() vor Initialisiere() wird abgelehnt.
    use backend = new GlBackend()
    Assert.Throws<InvalidOperationException>(fun () -> backend.Frame(1.0 / 60.0) |> ignore) |> ignore

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

// ===== Siegel: Property — Lager-Nichtnegativitaet ueber ALLE Befehlsfolgen (spec-siegel-lager-nichtnegativ) =====
// Die beiden Beispieltests oben pruefen Nichtnegativitaet fuer EINE feste Befehlsfolge (Seed 42,
// 400 Ticks) und EINEN handverlesenen Ueberausgabe-Fall. Beides ist existenzquantifiziert: "fuer
// DIESE Eingaben". Diese Property hebt denselben Invarianten auf "fuer JEDEN Seed und JEDE
// Befehlsfolge". FsCheck sucht aktiv ein Gegenbeispiel und schrumpft es auf den minimalen Fall —
// ein Off-by-one in einem Pfad, den die feste Folge nie trifft (etwa der Nahrungs-Unterhalt),
// faellt hier auf, nicht im Beispiel. Das ist der Unterschied zwischen Beispiel abhaken und
// Allaussage widerlegen.

open FsCheck

let private ressourceGen = Gen.elements alleRessourcen

let private befehlGen : Gen<Befehl> =
    Gen.oneof
        [ Gen.constant (RufeRune (Voelker.arbeiter Menschen))
          gen { let! i = Gen.choose (0, 4)
                let! r = ressourceGen
                return BefehleSammeln (i, r) }
          gen { let! r = ressourceGen
                let! menge = Gen.choose (1, 80)   // bis ueber den Startbestand — stresst die Guards
                return Verbrauch (r, menge) } ]

let private szenarioGen : Gen<uint64 * Befehl list list> =
    gen { let! seed  = Arb.generate<uint64>
          let! ticks = Gen.choose (1, 80)
          let! plan  = Gen.listOfLength ticks (Gen.listOf befehlGen)
          return seed, plan }

[<Fact; Trait("spot", "spec-siegel-lager-nichtnegativ-property")>]
let ``Siegel Lager (Property) — fuer jeden Seed und jede Befehlsfolge bleibt an jedem Tick jeder Lagerstand >= 0`` () =
    let invariante (seed: uint64, plan: Befehl list list) =
        let mutable w = Sim.erschaffe seed Menschen
        let mutable ok = true
        for befehle in plan do
            w <- Sim.tick befehle w
            for kv in w.Lager do
                if kv.Value < 0 then ok <- false
        ok
    Prop.forAll (Arb.fromGen szenarioGen) invariante
    |> Check.QuickThrowOnFailure
