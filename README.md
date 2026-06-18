# Runenruf

Ein Echtzeit-Strategie-Rollenspiel als Hommage an SpellForce 1: RTS-Basisbau und
Völker-Armeen treffen auf einen levelnden Avatar — den **Runengerufenen**.
Sechs Völker in zwei Bünden, schwebende Weltsplitter, Monumente, Runen und Ornamente.

**Technik:** .NET 9, F# (Modelle/Simulation) + C# (Technik) — keine fertige Engine,
Silk.NET + OpenGL 3.3/ES 3.0, cross-platform (Apple Silicon, Windows, Linux).
**Min-Spec:** Raspberry Pi 5 (8 GB), Full HD, niedrigste Settings, 30 FPS.
**Assets:** entstehen reproduzierbar aus Rezepten via eigenem Tool **AssetForge**
(Beschreibung → Rezept → Textur/Terrain/Musik).

Spec-driven entwickelt mit [CDD](https://github.com/Koschnag/cong-driven-development) —
das Modell liegt in `.spot/`, Konvergenz wird gemessen, nicht behauptet.

Lizenz: MPL-2.0. Hommage, kein Klon: eigene Namen, Lore und Assets.

## Reproduzieren / Konvergenz prüfen

```
git clone https://github.com/Koschnag/runenruf
cd runenruf
dotnet test tests/Runenruf.Tests    # 46/46 grün
```

Das Orakel (`SetzeSpecAligned`) setzt einen Spec-Knoten nur auf `Aligned`, wenn ein echter
`dotnet test`-Lauf grün ist — Exit-0 reicht nicht. Teil der Suite: Determinismus (gleicher Seed ⇒
bitgleicher Zustands-Hash) und Lager-Nichtnegativität als FsCheck-Property
`spec-siegel-lager-nichtnegativ` über *jeden* Seed und *jede* Befehlsfolge. Review = Konvergenz
gegen die Spec, nicht der gelesene Diff.
