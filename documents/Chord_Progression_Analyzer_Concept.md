# Chord Progression Analyzer (Concept Notes)

## Recommended name
Use **Chord Progression Analyzer** as the project/feature name.

Reason:
- It describes the actual goal (learning and predicting progression quality).
- It does not lock the model to one geometry.
- It leaves room for multiple views (Circle of Fifths, grid/lattice, force-directed graph, style-specific maps).

Use **Circle of Fifths** as one important visualization mode, not the overall name.

---

## Core goal
Learn to predict musically pleasing next chords by modeling progression behavior as a directed graph and exploring paths.

---

## Graph model
- **Vertex (node):** a chord state, e.g. `Cmaj`, `Dm7`, `G7`, `Cm7`.
- **Directed edge:** an allowed/observed transition from one chord to the next.
- **Edge weight:** transition strength (frequency, confidence, or preference).
- **Path:** a full chord progression.

This supports prediction directly: “Given current chord, which outgoing edge is strongest and best continues the path?”

---

## Practical starter scope
- 48-node chord-state set (12 roots × 4 types):
  - Major
  - minor
  - dominant 7
  - minor 7
- Keep each node’s outgoing transitions small at first (about 2–5).
- Start with a curated transition set; expand over time.

This avoids visual overload while preserving musical usefulness.

---

## Enharmonic normalization (recommended)
Use a deterministic canonical internal spelling to prevent duplicate nodes:

Canonical pitch classes:
- `C, C#, D, D#, E, F, F#, G, G#, A, A#, B`

Notes:
- This is for internal identity stability.
- Display aliases (e.g., showing `Bb` instead of `A#`) can be a later UI layer.

---

## Why not force a fifths-only layout?
Circle-of-fifths layout is excellent for tonal intuition, but not always best for every analysis task.

Different tasks may prefer different layouts:
- **Circle of Fifths view:** tonal gravity and dominant movement intuition.
- **Force-directed graph:** discover clusters/regions and heavily used pathways.
- **Lattice/grid variants:** compare transformations/voice-leading neighborhoods.

Conclusion: keep the analysis model independent from the visual layout.

---

## Learning workflow
1. Pick a current chord node.
2. Inspect outgoing edges.
3. Choose by strongest edge weight (or by style filter).
4. Walk 4–8 steps and audition.
5. Increase weights for good-sounding moves; decrease weak ones.
6. Repeat with 2-step lookahead for better continuity.

This creates a feedback loop from listening into predictive structure.

---

## Near-term implementation ideas
- Add preset graph: **Core Tonal Progressions (48-node sparse)**.
- Add edge metadata tags: style, cadence role, confidence.
- Add threshold/filter controls to hide weak transitions.
- Add simple next-chord suggestion panel based on outgoing weights.

## Playback note (staged delivery)
- Add chord-sequence playback in phases to control scope and risk.
- Phase 1: **MIDI export** of selected/generated progression (fastest value).
- Phase 2: **In-app MIDI playback** (tempo + play/stop).
- Phase 3: optional **built-in synth/audio rendering** and richer voicing options.

This keeps implementation practical while delivering useful musical feedback early.

---

## Naming summary
- **Project/feature name:** Chord Progression Analyzer
- **Visualization mode name:** Circle of Fifths (one of several)
