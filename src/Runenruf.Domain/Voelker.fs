namespace Runenruf.Domain

/// Zwei Bünde, sechs Völker — die Fraktions-Identität des Spiels (term-volk).
type Bund =
    | Licht
    | Dunkel

type Volk =
    | Menschen
    | Elfen
    | Zwerge
    | Orks
    | Trolle
    | Dunkelelfen

type Rolle =
    | Arbeiter
    | Nahkampf
    | Fernkampf
    | Magie
    | Belagerung

/// Bauplan einer rufbaren Einheit (term-rune): Kosten, Kampfwerte.
type EinheitTyp =
    { Volk         : Volk
      Name         : string
      Rolle        : Rolle
      KostenHolz   : int
      KostenEisen  : int
      KostenAether : int
      Leben        : int
      Schaden      : int
      Tempo        : float32 }

module Voelker =

    let alle = [ Menschen; Elfen; Zwerge; Orks; Trolle; Dunkelelfen ]

    let bund = function
        | Menschen | Elfen | Zwerge -> Licht
        | Orks | Trolle | Dunkelelfen -> Dunkel

    let private e volk name rolle holz eisen aether leben schaden tempo =
        { Volk = volk; Name = name; Rolle = rolle
          KostenHolz = holz; KostenEisen = eisen; KostenAether = aether
          Leben = leben; Schaden = schaden; Tempo = tempo }

    /// Einheitenlinien je Volk: Arbeiter, Nahkampf, Fernkampf + Alleinstellungsmerkmal.
    let einheiten volk =
        match volk with
        | Menschen ->
            [ e Menschen "Leibeigener"      Arbeiter   10 0  5  40  2 1.0f
              e Menschen "Schwertkaempfer"  Nahkampf   10 20 10 90 12 1.0f
              e Menschen "Armbrustschuetze" Fernkampf  20 15 10 60 14 1.0f
              e Menschen "Feldscher"        Magie       5  0 30 50  4 1.0f ]
        | Elfen ->
            [ e Elfen "Sammlerin"        Arbeiter  10 0  5  35  2 1.1f
              e Elfen "Klingentaenzer"   Nahkampf  15 15 15 70 14 1.2f
              e Elfen "Langbogenschuetzin" Fernkampf 25 10 10 55 18 1.1f
              e Elfen "Druide"           Magie      5  0 35 45  6 1.0f ]
        | Zwerge ->
            [ e Zwerge "Steinhauer"   Arbeiter   10 0  5  50  2 0.9f
              e Zwerge "Axttraeger"   Nahkampf   10 25 10 110 13 0.9f
              e Zwerge "Bolzenwerfer" Fernkampf  20 20 10 70 15 0.9f
              e Zwerge "Rammbock"     Belagerung 60 40 20 200 30 0.6f ]
        | Orks ->
            [ e Orks "Knecht"      Arbeiter  10 0  5  45  3 1.0f
              e Orks "Schlaechter" Nahkampf  10 15 10 100 14 1.0f
              e Orks "Speerwerfer" Fernkampf 15 15 10 65 13 1.0f
              e Orks "Kriegstrommler" Magie   10  0 25 55  3 1.0f ]
        | Trolle ->
            [ e Trolle "Schlepper"          Arbeiter   10 0  5  70  4 0.8f
              e Trolle "Felsfaust"          Nahkampf   10 20 15 160 18 0.8f
              e Trolle "Brockenschleuderer" Fernkampf  25 20 15 90 20 0.7f
              e Trolle "Stampfer"           Belagerung 50 50 25 250 35 0.5f ]
        | Dunkelelfen ->
            [ e Dunkelelfen "Dienerin"       Arbeiter  10 0  5  35  2 1.1f
              e Dunkelelfen "Klingenmeister" Nahkampf  15 15 15 75 15 1.2f
              e Dunkelelfen "Schattenschuetzin" Fernkampf 20 10 15 55 17 1.1f
              e Dunkelelfen "Blutmagierin"   Magie      5  0 40 45  8 1.0f ]

    /// Alleinstellungsmerkmal: eine Einheit jenseits der drei Grundrollen.
    let alleinstellung volk =
        einheiten volk |> List.filter (fun t -> t.Rolle = Magie || t.Rolle = Belagerung)

    let arbeiter volk = einheiten volk |> List.find (fun t -> t.Rolle = Arbeiter)
