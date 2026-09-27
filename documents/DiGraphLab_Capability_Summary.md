# DiGraphLab — Capability Summary

## Overview
This repository contains a Harmony-focused directed-graph prototype with a small, well-separated core: chord model, harmony graph, analysis utilities, a WPF viewer, simple synth playback, and MIDI export. Core concepts use Nashville tokens + traditional labels and voice-leading weights on directed edges.

## Key files / components
- Core chord model
  - DiGraphLab.Harmony/Chord.cs
  - DiGraphLab.Harmony/ChordFormatter.cs
  - DiGraphLab.Harmony/ChordLabeler.cs
- Graph model & service
  - DiGraphLab.Harmony/HarmonyGraph.cs
  - DiGraphLab.Harmony/HarmonyService.cs
  - DiGraphLab.Harmony/GraphAdapter.cs
- Analysis
  - DiGraphLab.Harmony/Analytics.cs
  - DiGraphLab.Harmony/VoiceLeading.cs
- Viewer (WPF)
  - DiGraphLab.Harmony.Viewer/MainWindow.xaml(.cs)
  - DiGraphLab.Harmony.Viewer/Controls/GraphCanvas.xaml(.cs)
- Playback & export
  - DiGraphLab.Harmony.Viewer/Playback/SimpleSynth.cs (NAudio)
  - DiGraphLab.Harmony.Viewer/Playback/MidiExporter.cs
- Demo + exports
  - DiGraphLab.Harmony/Demo.cs

## Feature mapping — current status

- Digraphs — Create
  - Programmatic creation: implemented (HarmonyService.AddChord, AddProgression, Demo).
  - UI creation: no dedicated “create node” dialog/button yet.

- Digraphs — Edit (layout, nodes, directions, labeling, attributes)
  - Layout: force-directed + manual dragging implemented (GraphCanvas).
  - Node attributes: displayed in inspector; model contains root, quality, inversion, pitch-classes.
  - Edge direction: model supports directed edges; no UI for creating/modifying edges interactively.
  - Labeling: Traditional / Nashville formatting present and toggleable (ChordFormatter, GraphCanvas.ApplyOptions).
  - Direct attribute editing in UI: not implemented.

- Transform (path simplification, optimized layout)
  - Voice-leading weight present (VoiceLeading) and Transpose utility (HarmonyService.TransposeGraph).
  - Automated path simplification / optimized graph transforms (A*, Dijkstra, pruning) are not implemented.

- Analyze digraphs
  - Implemented analyses:
	- Node/edge statistics (Analytics.ComputeStatistics).
	- Transition matrix (Analytics.ComputeTransitionMatrix) + matrix window.
	- Motif enumeration (Analytics.EnumeratePaths).
	- Sequence similarity (LCS-based).
	- Voice-leading costs per edge.
  - Not implemented: shortest-path/rewiring algorithms using voice-leading weights (but feasible).

- Import / Export / Print to PDF
  - Export: JSON and CSV (HarmonyGraph.ExportJson / ExportCsv); sequence .seq.json save/load; MIDI export via MidiExporter.WriteMidiFile from viewer.
  - Import: sequence JSON import implemented; graph JSON can be produced but no UI import button exists.
  - Print to PDF: not implemented (WPF printing / XPS pipeline could be added).

- Specialized musical features
  - Assign nodes to named chords / Nashville numbers: implemented (GraphAdapter, ChordFormatter).
  - Assign chord qualities: model supports Quality and uses it for labeling/analysis.
  - Analyze progressions: transition probabilities, motifs, voice-leading cost available. Higher-level template/substitution matching not implemented.
  - Play progressions: SimpleSynth provides polyphonic synth; sequence playback, tempo, beats, arpeggiation and MIDI export implemented.
  - Edit rhythm/time durations: tempo slider and per-item beats present; per-chord rhythmic grid editor is not.
  - Repeating loops: not present as a UI control (looping could be added by repeating PlayChordSequenceAsync).
  - Reading sheet music (OMR) to infer chords: feasible but not implemented. Recommended path: support MIDI / MusicXML import + chord inference (much simpler and reliable) before attempting OMR.

## Feasibility note: reading sheet music to determine chord progressions
- MIDI / MusicXML import → chord inference is the recommended and practical approach. Use an existing parser (DryWetMIDI, MusicXML parser) and implement a chord-inference stage (onset-aligned chroma clustering or sliding-window pitch-class detection).
- Optical Music Recognition (OMR) from scanned sheet/PDF requires an OMR engine (e.g., Audiveris or a commercial service) and robust post-processing; this is high-effort and error-prone.

## Suggested short roadmap (implementation priorities)
1. Surface existing model capabilities in UI (create/load/save graph, node editor, edge editor).
2. Improve sequence playback UX (loop controls, per-item durations, repeat count).
3. Add import features (load HarmonyGraph JSON; add MIDI/MusicXML import + simple chord inference).
4. Implement graph search/optimization (Dijkstra/A* using VoiceLeading weight).
5. Improve export (multi-track MIDI, PDF/XPS print of canvas).
6. Consider OMR only after robust MusicXML/MIDI ingestion and evaluation.

---

## Prioritized checklist (main priority: implement UI for existing functions)

1. Add "Import Graph JSON" UI (MainWindow)
   - Button in toolbar/menu to open HarmonyGraph JSON and call HarmonyService/GraphAdapter to load and display.
   - Validate JSON vs. HarmonyGraph export schema.

2. Add "Create Node" UI
   - Dialog to enter chord root (pc/name), quality, inversion, pitch-classes, label, style.
   - Hook to HarmonyService.AddChord and refresh GraphCanvas.

3. Add "Create / Remove Edge" UI
   - Allow selecting source/target nodes and adding/removing edges via HarmonyService.AddEdge / internal edge removal API (implement edge removal if missing).

4. Add node attribute editor in inspector
   - Make selected-node inspector editable: modify root/quality/inversion/label and persist to HarmonyGraph (update representative or replace node).

5. Add "Import MIDI / MusicXML" (basic)
   - Add menu/button to import MIDI or MusicXML.
   - Implement minimal MIDI parser using a library or simple event grouping to infer chord pitch-classes per beat.
   - Convert inferred chords to HarmonyGraph nodes via HarmonyService.AddChord and build progression edges.

6. Add sequence playback loop controls
   - UI elements: loop toggle, loop count (numeric), and "loop forever" option.
   - Wire PlaySequence logic to repeat sequence N times or indefinitely.

7. Expose "Transpose Graph" UI
   - Add UI control to transpose entire graph by N semitones using HarmonyService.TransposeGraph and reload viewer.

8. Add "Export Graph JSON" UI (save)
   - Button to export current HarmonyGraph to JSON (HarmonyService.ExportJson) and CSV (ExportCsv).

9. Add "Flip / Edit Edge Direction" UI
   - Allow flipping a selected edge direction via viewer and update HarmonyGraph accordingly.

10. Add Dijkstra/A* search (reharmonization)
	- Implement path search using VoiceLeading cost and expose a simple dialog to find low-cost paths between two nodes.

Checkmarks: completed items are marked with ✅.

1. ✅ Add "Import Graph JSON" UI (MainWindow)
   - Button in toolbar/menu to open HarmonyGraph JSON and call HarmonyService/GraphAdapter to load and display.
   - Validate JSON vs. HarmonyGraph export schema.

2. ✅ Add "Create Node" UI
   - Dialog to enter chord root (pc/name), quality, inversion, pitch-classes, label, style.
   - Hook to HarmonyService.AddChord and refresh GraphCanvas.

3. ✅ Add "Create / Remove Edge" UI
   - Allow selecting source/target nodes and adding/removing edges via HarmonyService.AddEdge / internal edge removal API (implement edge removal if missing).

4. Add node attribute editor in inspector
   - Make selected-node inspector editable: modify root/quality/inversion/label and persist to HarmonyGraph (update representative or replace node).

5. Add "Import MIDI / MusicXML" (basic)
