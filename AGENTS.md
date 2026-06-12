# Regeln für KI-Agenten in diesem Repo

1. **SPOT zuerst:** `.spot/` ist das Modell des Spiels (CDD). Neue Features entstehen
   zuerst als Pending-Spec mit Given/When/Then, dann `cdd derive-tests --write`, dann Code.
2. **Konvergenz wird gemessen:** `cdd validate`, `cdd sync-code`, `cdd sync-tests`
   müssen grün sein; Convergence-Status niemals von Hand auf Aligned setzen,
   außer die Erfüllung ist nachweisbar (Tests grün, Referenzen real).
3. **Assets nur aus Rezepten:** Kein Binär-Asset ohne `.rezept.json` daneben.
   Generierte Binärdaten bleiben aus git (außer Rezepte). Stil-Änderungen gehören
   in die Stil-Bibel, nicht in einzelne Assets.
4. **Hommage, kein Klon:** Keine Assets, Namen, Texte oder Daten aus SpellForce
   oder anderen Spielen. Mechanik-Inspiration ja, Material nein.
5. **Min-Spec ist Gesetz:** Pi 5 (8 GB), 1080p niedrig, 30 FPS. Budgets stehen in der
   Stil-Bibel und sind testbar — wer sie reißt, baut LOD, nicht Ausnahmen.
6. **Kein Python. Nie.** F# für Modelle/Simulation, C# für Technik.
7. **Release-Tags (`v*`) nur nach explizitem Auftrag des Maintainers.**
