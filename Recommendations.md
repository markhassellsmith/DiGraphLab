# DiGraphLab — Feature Recommendations & Roadmap

Purpose
- Provide a concise, prioritized feature set and implementation notes focused on: learning harmony, composing chord progressions, flexible notation (traditional, Nashville, Roman), and practical tooling for analysis and generation.

Core ideas (overview)
- Keep a small, stable core data model: immutable Chord (rootPc, pitchClasses, quality, inversion, label), Scale, and a HarmonyGraph (nodes/edges).
- Use Nashville-number tokens as a key-independent canonical identifier for nodes. Preserve absolute pitch data for playback and voicing.
- Provide flexible notation toggles: Traditional chord-symbols, Nashville numbers, and Roman numeral analysis.
- Support diatonic forms for multiple minor variants (natural, harmonic, melodic) and modal/scale factories.
- Include voicing and inversion helpers plus voice-leading cost to enable composition/reharmonization features.

Recommended features (detailed)
1) Core data model
- Chord: immutable, normalized pitch-classes (0..11), optional PreserveDoubling, automatic quality inference (triads/sevenths/extended).
- Scale: tonicPc + mode/scale type.
- HarmonyGraph: nodes store canonical Nashville id, example Chord(s), metadata (count/style tags); edges store voice-leading weight and frequency.

2) Key-independent representation (Nashville)
- Store canonical node id using Nashville label (degree + quality + optional inversion) so the graph is transposable across keys.
- Keep representative absolute chords per node for playback/voicing.

3) Flexible notation & labelers
- Quick formatter: traditional chord symbols and Nashville options (preferSharps, includeBass, showExtensions).
- Roman-numeral labeler: requires tonic + scaleType; outputs I/ii/V, accidentals (#/b) for altered roots and suffixes for sevenths/ø/°.
- Keep labeling/formatting out of Chord (separate ChordFormatter/ChordLabeler classes).

4) Chord factories & scale forms
- Factories for major, natural minor, harmonic minor, melodic minor, and mode-based chord generation.
- Allow addSeventh flag and variant selection so dominant-major/minor sevenths exist where appropriate.

5) Voicings & inversions
- Preserve doubling option when required.
- Helpers: GetIntervalsFromRoot(), GetInversionPitchClasses(), GetVoicedSemitones() to produce voiced semitones suitable for voice-leading calculations and playback.

6) Voice-leading metrics and weighted digraph
- Edge weight = voice-leading cost between two voiced representations (e.g., sum absolute semitone moves, optionally squared/L2).
- Store multiple example voicings per Nashville node and choose the minimal-cost pairing when computing edge weight.

7) Progression templates, substitutions and pattern matching
- Store common templates (ii–V–I, I–vi–IV–V); use fuzzy matching on HarmonyGraph to find occurrences and propose substitutions (tritone sub, secondary dominants).

8) Style packs & voicing libraries
- Pluggable style packs (Jazz, Pop, Classical) mapping Nashville tokens to prioritized voicings and common extensions.

9) Export & notation toggles
- Export CSV/JSON including: Nashville token, traditional symbol, rootPc, pitch-classes, voiced semitones, inversion.
- Runtime UI toggle: switch rendering mode between Traditional/Nashville/Roman; switch tonic to transpose entire graph.

10) Interactive learning tools
- Chord decomposition view: show root, intervals, scale-degree explanation.
- Step-through cadences with voice-leading visualization and playback.
- Exercises: identify Nashville degree or Roman numeral, choose optimal voicing.

11) Generation & ML
- Simple Markov/transition model on Nashville nodes for style-conditioned progression generation.
- Later: graph embeddings, sequence models trained on Nashville tokens.

12) Reharmonization and search
- Dijkstra/A* search on HarmonyGraph using voice-leading cost to find low-motion reharmonizations.

13) Functional analysis
- Detect secondary dominants (V/V), tonicizations, and common functional relationships; attach labels (e.g., "V/V") in Roman mode.

14) Corpus ingestion
- Parsers for chord CSVs, simple chordchart formats, and MIDI-based chord extraction. Canonicalize to Nashville tokens during ingestion.

Implementation notes & trade-offs
- Automatic quality inference is convenient but best-effort only — allow override and provide a configurable strictness mode.
- Enharmonic spelling (accurate accidentals, double-sharps/flats) requires full note-name logic and key-signature awareness; treat as an advanced feature.
- Keep Chord simple; place labeling, formatting, and UI/analysis in separate modules to avoid responsibility creep.
- Use Nashville tokens as canonical keys to collapse equivalent functional nodes across keys; store absolute chords as examples for sound and voicing.

Prioritization (MVP roadmap)
- MVP (short list to implement first):
  1. Stabilize Chord model + inference (done).
  2. ChordFormatter (traditional + Nashville) and ChordLabeler (Roman numerals) (done).
  3. HarmonyGraph class (node/edge APIs) storing Nashville canonicalId and representative Chord objects.
  4. Voice-leading cost function and attach weights on edges.
  5. Exporter: CSV/JSON with both notations and voiced semitones.

- Near-term (next iterations):
  6. Style packs & voicing libraries.
  7. Progression templates & pattern search.
  8. Simple UI features: key switcher, notation toggle, playback hook.
  9. Unit tests for core features.

- Advanced (later):
 10. Reharmonization search and secondary-dominant detection.
 11. Probabilistic generation / ML workflows.
 12. Full enharmonic spelling and advanced alteration naming.

Concrete starter tasks (pick one)
- Add HarmonyGraph class and wire ChordFormatter for canonical node ids.
- Implement voice-leading cost and example function to compute edge weight between two chords.
- Create exporter that writes nodes and edges to JSON/CSV with Nashville + traditional labels for offline study.

Next steps
- Tell me which starter task you want implemented first and I will scaffold the code and tests.

--
DiGraphLab recommendations generated by GitHub Copilot
