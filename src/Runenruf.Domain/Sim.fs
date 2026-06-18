namespace Runenruf.Domain

open System

type Ressource =
    | Holz
    | Stein
    | Eisen
    | Nahrung
    | Aether

module Rng =
    /// splitmix64 — deterministisch und plattformunabhaengig (premise-determinismus).
    let next (state: uint64) : struct (uint64 * uint64) =
        let s = state + 0x9E3779B97F4A7C15UL
        let z = (s ^^^ (s >>> 30)) * 0xBF58476D1CE4E5B9UL
        let z = (z ^^^ (z >>> 27)) * 0x94D049BB133111EBUL
        struct (z ^^^ (z >>> 31), s)

type Auftrag =
    | Leerlauf
    | Sammeln of Ressource

type Einheit =
    { Typ     : EinheitTyp
      Pos     : Pos
      Leben   : int
      Auftrag : Auftrag }

type Befehl =
    | RufeRune of EinheitTyp
    | BefehleSammeln of einheit: int * Ressource
    /// Direkter Lagerverbrauch (Ritual, Bau, Handel): nimmt Menge der Ressource aus dem Lager.
    | Verbrauch of Ressource * int

/// Der komplette Spielzustand eines Weltsplitters — pur, deterministisch (adr-004).
type Welt =
    { Tick      : int
      RngState  : uint64
      Volk      : Volk
      Monument  : Pos
      Lager     : Map<Ressource, int>
      Einheiten : Einheit list
      Avatar    : Runengerufener }

module Sim =

    /// Alle 10 Ticks (0,5 s bei 20 Hz) liefert ein sammelnder Arbeiter eine Einheit.
    let sammelIntervall = 10

    let erschaffe (seed: uint64) volk =
        let monument = { X = 0.0f; Y = 0.0f }
        { Tick = 0; RngState = seed; Volk = volk; Monument = monument
          Lager = Map.ofList [ Holz, 50; Stein, 20; Eisen, 20; Nahrung, 50; Aether, 100 ]
          Einheiten = []
          Avatar = Avatar.erschaffe monument }

    let private kosten (t: EinheitTyp) =
        [ Holz, t.KostenHolz; Eisen, t.KostenEisen; Aether, t.KostenAether ]

    let private bezahlbar (lager: Map<Ressource, int>) k =
        k |> List.forall (fun (r, c) -> Map.find r lager >= c)

    let private zahle (lager: Map<Ressource, int>) k =
        k |> List.fold (fun l (r, c) -> Map.add r (Map.find r l - c) l) lager

    let private wendeBefehlAn (welt: Welt) befehl =
        match befehl with
        | RufeRune typ ->
            // spec-runenruf: Einheit entsteht am Monument, Kosten werden abgezogen —
            // spec-wirtschaft: ohne volle Deckung wird abgelehnt und nichts abgezogen.
            // spec-rufe-rune-nahrungssperre: leerer Nahrungsvorrat sperrt den Runenruf —
            // ohne Nahrung im Lager entsteht keine Einheit (Nahrung selbst ist keine Kosten).
            let k = kosten typ
            let nahrungVorhanden = (Map.tryFind Nahrung welt.Lager |> Option.defaultValue 0) > 0
            if nahrungVorhanden && bezahlbar welt.Lager k then
                let e = { Typ = typ; Pos = welt.Monument; Leben = typ.Leben; Auftrag = Leerlauf }
                { welt with Lager = zahle welt.Lager k; Einheiten = welt.Einheiten @ [ e ] }
            else
                welt
        | BefehleSammeln (i, r) ->
            { welt with
                Einheiten =
                    welt.Einheiten
                    |> List.mapi (fun j e -> if j = i then { e with Auftrag = Sammeln r } else e) }
        | Verbrauch (r, menge) ->
            // spec-siegel-lager-nichtnegativ: Lager nie negativ. Eine Ausgabe ueber den
            // vorhandenen Bestand hinaus wird abgelehnt; nichts wird abgezogen. Da die
            // Befehle eines Ticks nacheinander auf dasselbe Lager wirken, faengt diese
            // Pruefung auch die kumulative Ueberausgabe mehrerer Befehle innerhalb eines Ticks.
            let bestand = Map.tryFind r welt.Lager |> Option.defaultValue 0
            if menge > 0 && bestand >= menge then
                { welt with Lager = Map.add r (bestand - menge) welt.Lager }
            else
                welt

    /// Ein deterministischer Simulationsschritt: Befehle anwenden, Wirtschaft, Avatar, RNG.
    let tick (befehle: Befehl list) (welt: Welt) =
        let welt = befehle |> List.fold wendeBefehlAn welt
        let neuerTick = welt.Tick + 1
        let lager =
            if neuerTick % sammelIntervall = 0 then
                // spec-wirtschaft: sammelnde Arbeiter liefern je Intervall eine Ressource.
                let gesammelt =
                    welt.Einheiten
                    |> List.fold (fun l e ->
                        match e.Auftrag with
                        | Sammeln r when e.Typ.Rolle = Arbeiter -> Map.add r (Map.find r l + 1) l
                        | _ -> l) welt.Lager
                // spec-unterhalt: jede lebende Einheit isst je Intervall eine Nahrung —
                // spec-unterhalt-knappheit: Hunger ist ein hartes Limit, kein Schuldenkonto;
                // das Nahrungslager faellt auf genau 0 statt ins Minus.
                let lebende = welt.Einheiten |> List.filter (fun e -> e.Leben > 0) |> List.length
                let nahrung = max 0 (Map.find Nahrung gesammelt - lebende)
                Map.add Nahrung nahrung gesammelt
            else
                welt.Lager
        let struct (z, rng) = Rng.next welt.RngState
        let jitter = float32 (z % 7UL) * 0.0001f
        let einheiten =
            welt.Einheiten
            |> List.map (fun e -> { e with Pos = { X = e.Pos.X + jitter; Y = e.Pos.Y } })
        { welt with
            Tick = neuerTick
            RngState = rng
            Lager = lager
            Einheiten = einheiten
            Avatar = Avatar.tick welt.Monument welt.Avatar }

    let lauf (ticks: int) (befehleFuerTick: int -> Befehl list) (welt: Welt) =
        let mutable w = welt
        for t in 1 .. ticks do
            w <- tick (befehleFuerTick t) w
        w

    /// FNV-1a ueber den kanonischen Zustand — bitgleiche Welten haben gleiche Hashes.
    let hash (welt: Welt) : uint64 =
        let mutable h = 14695981039346656037UL
        let mix (b: uint64) = h <- (h ^^^ b) * 1099511628211UL
        mix (uint64 welt.Tick)
        mix welt.RngState
        for kv in welt.Lager do
            mix (uint64 kv.Value)
        for e in welt.Einheiten do
            mix (uint64 (uint32 (BitConverter.SingleToInt32Bits e.Pos.X)))
            mix (uint64 (uint32 (BitConverter.SingleToInt32Bits e.Pos.Y)))
            mix (uint64 e.Leben)
        mix (uint64 welt.Avatar.Level)
        mix (uint64 welt.Avatar.Erfahrung)
        mix (uint64 welt.Avatar.FertigkeitsPunkte)
        mix (match welt.Avatar.WiederbelebungIn with Some n -> uint64 n + 1UL | None -> 0UL)
        mix (uint64 (uint32 (BitConverter.SingleToInt32Bits welt.Avatar.Pos.X)))
        mix (uint64 (uint32 (BitConverter.SingleToInt32Bits welt.Avatar.Pos.Y)))
        h

/// C#-freundliche Fassade fuer Host und Tests.
module Spiel =

    let standardWelt (seed: uint64) = Sim.erschaffe seed Menschen

    /// Kopfloser Lauf ohne Befehle — fuer CI-Smoke und Determinismus-Checks von aussen.
    let kopfloserLauf (seed: uint64) (ticks: int) : uint64 =
        Sim.erschaffe seed Menschen
        |> Sim.lauf ticks (fun _ -> [])
        |> Sim.hash
