namespace Runenruf.Domain

open System

[<Struct>]
type Pos = { X: float32; Y: float32 }

type Attribute =
    { Staerke  : int
      Geschick : int
      Weisheit : int }

type Gegenstand =
    { Name        : string
      MinStaerke  : int
      MinGeschick : int
      Bonus       : int }

/// Der Runengerufene (term-runengerufener): levelt, traegt Ausruestung,
/// ersteht nach dem Fall am Monument wieder auf.
type Runengerufener =
    { Level             : int
      Erfahrung         : int
      FertigkeitsPunkte : int
      Attribute         : Attribute
      Ausruestung       : Gegenstand list
      Pos               : Pos
      WiederbelebungIn  : int option }

module Avatar =

    let erfahrungFuerLevel level = level * 100

    let erschaffe (start: Pos) =
        { Level = 1; Erfahrung = 0; FertigkeitsPunkte = 0
          Attribute = { Staerke = 10; Geschick = 10; Weisheit = 10 }
          Ausruestung = []; Pos = start; WiederbelebungIn = None }

    /// spec-avatar-rpg: Levelaufstieg hebt Attribute und gibt einen Fertigkeitspunkt.
    let rec private levelAuf (a: Runengerufener) =
        if a.Erfahrung >= erfahrungFuerLevel a.Level then
            levelAuf
                { a with
                    Level = a.Level + 1
                    Erfahrung = a.Erfahrung - erfahrungFuerLevel a.Level
                    FertigkeitsPunkte = a.FertigkeitsPunkte + 1
                    Attribute =
                        { Staerke  = a.Attribute.Staerke + 2
                          Geschick = a.Attribute.Geschick + 2
                          Weisheit = a.Attribute.Weisheit + 1 } }
        else a

    let sammleErfahrung xp (a: Runengerufener) =
        levelAuf { a with Erfahrung = a.Erfahrung + xp }

    /// spec-avatar-rpg: Anlegen nur, wenn die Anforderungen erfuellt sind.
    let legeAn (g: Gegenstand) (a: Runengerufener) =
        if a.Attribute.Staerke >= g.MinStaerke && a.Attribute.Geschick >= g.MinGeschick then
            Ok { a with Ausruestung = g :: a.Ausruestung }
        else
            Error (sprintf "Anforderungen nicht erfuellt: %s braucht Staerke %d, Geschick %d" g.Name g.MinStaerke g.MinGeschick)

    let faellt wiederbelebungsTicks (a: Runengerufener) =
        { a with WiederbelebungIn = Some wiederbelebungsTicks }

    /// spec-runenruf: nach Ablauf der Wiederbelebungszeit ersteht der Avatar am Monument.
    let tick (monument: Pos) (a: Runengerufener) =
        match a.WiederbelebungIn with
        | Some n when n > 1 -> { a with WiederbelebungIn = Some (n - 1) }
        | Some _            -> { a with WiederbelebungIn = None; Pos = monument }
        | None              -> a
