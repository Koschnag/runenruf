# SPOT-Kontext

Generiert aus 66 Knoten (`cdd export-context`). Der SPOT-Graph ist die Quelle — dieses Dokument ist Derivat und ersetzt handgepflegte Doku.

**Konvergenz:** Aligned 55 · Pending 11 · Diverged 0 · Orphaned 0

## Ubiquitäre Sprache (Ontologie)

Diese Begriffe sind verbindlich — in Code, Antworten und allen Artefakten:

- **Äther** *(auch: Mana)* — Magie-Ressource, die aus Ätherquellen gewonnen wird — treibt Zauber und Beschwörungen
  - ist ein `term-ressource`
- **Weltsplitter** *(auch: Insel)* — Schwebende Insel-Welt, über Portale verbunden — eine Karte/Mission
  - bezieht sich auf `term-portal`
- **Monument** *(auch: Steinkreis)* — Verwitterter Steinkreis, an dem Runen gerufen werden — Spawn- und Kontrollpunkt
  - bezieht sich auf `term-rune`
- **Portal** — Übergang zwischen Weltsplittern
- **Ressource** — Sammelbares Wirtschaftsgut: Holz, Stein, Eisen, Nahrung, Äther
- **Asset-Rezept** *(auch: Recipe)* — Deterministische JSON-Beschreibung, aus der AssetForge ein Asset erzeugt — git speichert Rezepte, nicht Binärdaten
- **Rune** — Gebundene Seele als beschworene Einheit oder Avatar — das zentrale Beschwörungs- und Schmuckmotiv der Welt
- **Runengerufener** *(auch: Avatar)* — Der Avatar des Spielers — eine an eine Rune gebundene Seele, an Monumenten rufbar; levelt, trägt Ausrüstung, lernt Fertigkeiten
  - bezieht sich auf `term-rune`
  - bezieht sich auf `term-monument`
- **Volk** *(auch: Fraktion, Rasse)* — Spielbare Fraktion mit eigenen Einheiten, Gebäuden und Ornamentik: Menschen, Elfen, Zwerge, Orks, Trolle, Dunkelelfen

## Invarianten (Governance — werden bei jeder Validierung erzwungen)

- **Jeder Begriff der ubiquitären Sprache ist definiert** — jeder Begriff braucht eine Definition
- **Kritische Risiken brauchen eine Mitigation** — kritische Risiken brauchen eine Mitigation
- **Jede Spec hat mindestens einen Test** — jede Spec braucht mindestens einen Test

## Prämissen (nicht verhandelbar)

- **Plan (CDD-Modell) und Auslieferung (Release) sind synchron: kein Binary ohne konvergiertes Modell, Release-Notes kommen aus dem Modell** — So wird der Anforderungsstand bei jeder Auslieferung mit-evaluiert; das Spiel ist zugleich Dauertest für CDD
- **Die Simulation ist deterministisch: Fixed-Tick, eigener RNG, gleiche Eingaben ⇒ gleicher Zustand** — Replays, Tests und späterer Multiplayer werden trivial statt unmöglich
- **Hommage, kein Klon: Mechanik und Stimmung von SpellForce 1, aber eigene Namen, Lore und Assets** — Spielmechanik ist nicht schützbar — Assets, Namen und Texte sind es
- **Kein Python — nie.** — Ein Stack: .NET. Vorgabe des Maintainers
- **Keine fertige Engine — Eigenbau auf FOSS-Bindings (Silk.NET)** — Volle Kontrolle über Stil, Performance und Min-Spec; Bindings sind keine Engine
- **Min-Spec: Raspberry Pi 5 (8 GB) — 1080p, niedrigste Settings, 30 FPS; Primärgerät: M1 Mac** — Harte Budgets erzwingen Disziplin; was auf dem Pi läuft, läuft überall
- **F# für Modelle und Simulation, C# für Technik (Rendering, IO, Audio, Tools)** — Typsichere, pure Domäne; imperative Technik dort, wo Interop und Performance zählen

## Entscheidungen (ADRs)

### Silk.NET + OpenGL 3.3 Core / ES 3.0 (`adr-001-silknet-gl`)
- **Kontext:** Cross-Platform ohne Engine: M1-Mac kann GL 4.1, Pi 5 kann GL 3.1/ES 3.1, Windows alles; Vulkan+MoltenVK wäre mächtiger, aber massiv mehr Code
- **Entscheidung:** Fenster/Input/GL über Silk.NET-Bindings; Shader auf GLSL-330/ES-300-Schnittmenge; Vulkan-Backend bleibt als spätere Option
- **Konsequenzen:** macOS markiert GL als deprecated (funktioniert aber); dafür ein Renderer-Code für alle drei Plattformen

### Eigene Struct-of-Arrays-Simulation statt ECS-Framework (`adr-002-eigenes-ecs`)
- **Kontext:** RTS mit hunderten Einheiten auf einem Pi 5 braucht datenorientierte Updates ohne Framework-Overhead
- **Entscheidung:** Einheiten als Arrays primitiver Felder in F#; Systeme sind pure Funktionen über Spans
- **Konsequenzen:** Kein Framework-Lock-in; Cache-freundlich; etwas mehr Handarbeit

### AssetForge: Prompt → JSON-Rezept → deterministischer Generator (`adr-003-assetforge`)
- **Kontext:** KI-generierte Assets müssen reproduzierbar, lizenzrein und stilkonsistent sein; Binär-Blobs in git sind Gift
- **Entscheidung:** Eigenes C#-Tool: Beschreibung wird (per LLM oder Heuristik offline) zu einem Rezept; Generatoren erzeugen Texturen, Terrain, Meshes, Musik deterministisch aus Seed+Parametern
- **Konsequenzen:** Assets jederzeit neu generierbar; Stil zentral steuerbar (Stil-Bibel als Rezept-Defaults); LLM optional, nie Pflicht

### Simulation mit festem Tick (20 Hz), Rendering entkoppelt mit Interpolation (`adr-004-fixed-tick`)
- **Kontext:** Determinismus (premise-determinismus) und Pi-5-Budget verlangen planbare Update-Kosten
- **Entscheidung:** Sim-Tick 50 ms in F#, Renderer interpoliert Positionen; Eingaben werden als Befehls-Queue an Ticks gebunden
- **Konsequenzen:** RTS-typisch robust; Replays = Befehlsliste + Seed

### NativeAOT für Game-Host und AssetForge-CLI (`adr-005-nativeaot`)
- **Kontext:** Schneller Start, kleiner Speicher — wichtig auf Pi 5; F#-Domäne ist AOT-tauglich (keine Reflection)
- **Entscheidung:** PublishAot für Runenruf.Game und AssetForge; Reflection-freie Serialisierung (Source-Generator)
- **Konsequenzen:** Trim-Warnungen werden Fehler; ein paar Bibliotheks-Einschränkungen

### CDD als Release-Gate und Notes-Quelle, beide Repos eigenständig (`adr-006-release-gate`)
- **Kontext:** Releases sollen mit der CDD-Planung synchron sein; ein einziger Repo-übergreifender Workflow wäre fragil
- **Entscheidung:** Runenruf-Release checkt CDD aus, lässt validate/sync-code/sync-tests als Gate laufen und generiert die Notes aus export-context; CDD bleibt eigenes Repo mit eigenen Releases
- **Konsequenzen:** Lose Kopplung statt Monorepo; CDD-Lücken fallen bei jedem Release auf (Co-Evolution); CDD-Version im Release-Workflow gepinnt für Reproduzierbarkeit

## Spezifikationen

### AssetForge: Stimmung → Musik (`spec-assetforge-musik`, Pending)
**Intent:** Chor-/Streicher-Stimmung à la 2003 als prozedural komponiertes, loopbares Stück

- GIVEN eine Stimmungsbeschreibung wie 'heroisch, weite Streicher' WHEN AssetForge Musik generiert THEN entsteht eine gültige WAV-Datei mit der gewünschten Länge
- GIVEN dasselbe Musik-Rezept zweimal WHEN beide WAVs verglichen werden THEN sind sie bytegleich

### AssetForge: Beschreibung → Textur (`spec-assetforge-textur`, Pending)
**Intent:** Stein, Ornament, Rune: stilkonsistente Texturen aus Prosa, reproduzierbar

- GIVEN eine Beschreibung wie 'verwitterter Stein mit leuchtender Rune' WHEN AssetForge läuft THEN entsteht ein Rezept und daraus ein gültiges PNG in der Stil-Palette
- GIVEN dasselbe Rezept zweimal WHEN beide PNGs verglichen werden THEN sind sie bytegleich

### Avatar mit RPG-Kern (`spec-avatar-rpg`, Pending)
**Intent:** Der Runengerufene levelt, lernt Fertigkeiten und trägt Ausrüstung

- GIVEN ein Avatar mit genug Erfahrung WHEN ein Level-Aufstieg verarbeitet wird THEN steigen seine Attribute gemäß Fertigkeitsbaum und ein Fertigkeitspunkt entsteht
- GIVEN ein Ausrüstungsgegenstand mit Anforderungen WHEN der Avatar sie nicht erfüllt THEN wird das Anlegen abgelehnt

### Cross-Platform-Fenster mit GL-Kontext (`spec-fenster`, Pending)
**Intent:** Ein Fenster mit Frame-Loop auf Mac/Windows/Linux — das technische Fundament

- GIVEN ein Start auf einer der drei Plattformen WHEN das Spiel startet THEN öffnet sich ein Fenster mit GL-3.3/ES-3.0-Kontext und laufender Frame-Schleife
- GIVEN die Umgebung hat kein Display (CI) WHEN mit --headless gestartet wird THEN initialisiert die Technik ohne Fenster und beendet sich sauber

### Min-Spec-Budgets als Tests (`spec-minspec-budgets`, Pending)
**Intent:** Pi-5-Tauglichkeit wird gemessen, nicht gehofft: Budgets sind testbare Zahlen

- GIVEN die niedrigste Grafikstufe WHEN ein Frame-Budget geprüft wird THEN liegen Terrain+Einheiten unter 100k Dreiecken und 150 Drawcalls
- GIVEN ein Sim-Tick mit 200 Einheiten WHEN auf Referenz-Hardware gemessen wird THEN bleibt er unter 5 ms

### Release-Pipeline mit CDD-Gate (`spec-release-pipeline`, Aligned)
**Intent:** Ein Tag erzeugt fertig compilierte Releases für alle Zielplattformen — aber nur, wenn das CDD-Modell konvergiert ist; die Release-Notes entstehen aus dem Modell

- GIVEN ein Versions-Tag v* und ein konsistentes CDD-Modell WHEN die Release-Pipeline läuft THEN erscheinen self-contained Binaries für osx-arm64 (M1), win-x64, linux-x64 und linux-arm64 (Pi 5) als GitHub-Release
- GIVEN ein Tag, aber das CDD-Modell ist inkonsistent oder Tests sind rot WHEN die Release-Pipeline läuft THEN bricht sie vor dem Build ab und es entsteht kein Release

### Runen rufen (`spec-runenruf`, Pending)
**Intent:** Der Kern-Fantasy-Moment: Einheiten entstehen als gerufene Runen am Monument

- GIVEN ein Monument und genug Äther WHEN eine Einheiten-Rune gerufen wird THEN entsteht die Einheit am Monument und der Äther sinkt um die Kosten
- GIVEN der Runengerufene fällt im Kampf WHEN die Wiederbelebungszeit abläuft THEN ersteht er am letzten aktivierten Monument wieder auf

### Deterministische Simulation (`spec-sim-determinismus`, Pending)
**Intent:** Gleiche Eingaben ergeben bitgleiche Zustände — Fundament für Tests, Replays, Multiplayer

- GIVEN zwei Simulationen mit gleichem Seed und gleicher Befehlsliste WHEN beide 1000 Ticks laufen THEN sind ihre Zustands-Hashes identisch
- GIVEN eine Simulation mit anderem Seed WHEN sie 1000 Ticks läuft THEN unterscheidet sich ihr Zustands-Hash

### Prozedurales Terrain (`spec-terrain`, Pending)
**Intent:** Ein Weltsplitter aus einem Rezept: Heightmap → Mesh → gerendert mit warmem Licht

- GIVEN ein Terrain-Rezept mit Seed WHEN das Mesh generiert wird THEN ist es deterministisch und hält das Tri-Budget der niedrigsten Stufe ein
- GIVEN dasselbe Rezept zweimal WHEN beide Meshes verglichen werden THEN sind sie bitgleich

### Sechs Völker mit Einheiten (`spec-voelker`, Pending)
**Intent:** Die Völker-Identität von SpellForce: zwei Bünde, je eigene Einheitenlinien

- GIVEN das Domänenmodell WHEN die Völker abgefragt werden THEN existieren genau 6 Völker in 2 Bünden (Licht/Dunkel)
- GIVEN ein beliebiges Volk WHEN seine Einheitstypen abgefragt werden THEN hat es mindestens Arbeiter, Nahkämpfer, Fernkämpfer und ein Alleinstellungsmerkmal

### Ressourcen-Wirtschaft (`spec-wirtschaft`, Pending)
**Intent:** Arbeiter sammeln, Lager füllen sich, Produktion verbraucht — der RTS-Kreislauf

- GIVEN ein Arbeiter neben einer Holzquelle und ein Lager WHEN 100 Ticks simuliert werden THEN steigt der Holzvorrat messbar
- GIVEN ein Beschwörungs-Befehl ohne genug Äther WHEN der Tick verarbeitet wird THEN wird er abgelehnt und nichts abgezogen

## Risiken

- **Apple entfernt OpenGL in einer künftigen macOS-Version** (Likelihood Low, Impact High) — Mitigation: Renderer hinter Interface (IRenderBackend); Vulkan/MoltenVK-Backend als geplanter zweiter Implementierer
- **Pi 5 schafft die 30 FPS in Full HD nicht** (Likelihood Medium, Impact High) — Mitigation: Harte Render-Budgets als testbare Invarianten (Tris, Drawcalls); Renderscale-Option; LOD von Anfang an
- **Scope-Explosion: ein SpellForce-Umfang ist jahrelange Arbeit** (Likelihood High, Impact Critical) — Mitigation: Vertical Slice zuerst: eine Insel, ein Volk spielbar, ein Gegner-Volk — alles Weitere nur als Spec
- **Generierte Assets wirken beliebig statt nach SpellForce-Stimmung** (Likelihood Medium, Impact High) — Mitigation: Stil-Bibel als zentrale Rezept-Defaults (Palette, Ornament-Grammatik, Instrumentierung); jedes Asset-Rezept erbt davon

## Komponenten

- **AssetForge** (`comp-assetforge`)
- **Runenruf.Domain** (`comp-domain`)
- **Runenruf.Engine** (`comp-engine`) → hängt ab von `comp-domain`
- **Runenruf.Game** (`comp-game`) → hängt ab von `comp-domain`, `comp-engine`, `comp-assetforge`

## Offene Arbeit (nicht Aligned)

- `spec-assetforge-musik` (spec, Pending)
- `spec-assetforge-textur` (spec, Pending)
- `spec-avatar-rpg` (spec, Pending)
- `spec-fenster-test-1` (test, Pending)
- `spec-fenster` (spec, Pending)
- `spec-minspec-budgets` (spec, Pending)
- `spec-runenruf` (spec, Pending)
- `spec-sim-determinismus` (spec, Pending)
- `spec-terrain` (spec, Pending)
- `spec-voelker` (spec, Pending)
- `spec-wirtschaft` (spec, Pending)

