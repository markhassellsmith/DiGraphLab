#Application Objectives

DiGraphLab provides an open-source platform for researchers, educators, students, and music enthusiasts to explore the intersection of graph theory and music theory.

## Directed Graph Analysis
DiGraphLab provides a way to create, import, edit, analyze and save graphs in multiple formats. 
DiGraphLab provides editing in both a node-edge view and a matrix view.
DigraphLab provides analyses and transformations of directed graphs.
Analyses include characteristics of the graph, such as degree distribution, connectivity, and centrality measures. 
Transformations include operations like subgraph extraction, graph simplification, and edge reweighting.

## Chord Progression Analysis and Music Theory
DiGraphLab also supports chord progression analysis using directed graphs to represent musical structures.
Some music theory concepts, such as chord functions and voice leading, can be modeled using directed graphs, allowing for the analysis of chord progressions and their relationships.

## Quick Start

1. Build and run the DiGraphLab.Harmony.Viewer project from Visual Studio.
2. Click "Load Demo" on the toolbar to populate a sample graph. The canvas will show nodes (chords) and directed edges (progressions).
3. Click a node on the canvas to populate the inline inspector on the right (Traditional, Nashville, Quality, Pitch Classes).
4. Edit values in the inline inspector and click "Apply" to update the selected node's representative chord (visuals update immediately).
5. Shift+Click two nodes (source then target) and click "Create Edge" to add an edge; click "Remove Edge" to remove an existing edge.
6. Build a sequence using Shift+Click on nodes, then click "Load Selection" to move them into the Sequence list; use "Play" to playback the sequence.

## UI Map (common elements)

- Toolbar
  - Load Demo: populate a small example graph for exploration and testing.
  - Import Graph / Import Graph JSON: open a .json graph file and replace the current graph.
  - Import MIDI: parse a MIDI file and add nodes derived from grouped note-on events.
  - Create Node: open a dialog to add a new chord node (id, label, root PC, quality, pitch classes, style).
  - Create Edge / Remove Edge: add or remove directed edges between two selected nodes.
  - Transpose: enter semitone amount and transpose the current graph; a transposed copy replaces the current view.
  - Playback controls: Play / Stop, Tempo slider, Loop toggle and Loop count — control sequence playback.

- Canvas (Graph view)
  - Click a node: select and populate inline inspector.
  - Shift+Click: multi-select in ordered fashion (useful for source→target and ordered sequences).
  - Drag nodes: reposition; edges remain attached to node boundaries.

- Inline Inspector
  - Inspector_Traditional: human-friendly chord label.
  - Inspector_Nashville: Nashville-number style label.
  - Inspector_Quality: chord quality string (e.g., "maj7", "m").
  - Inspector_PitchClasses: comma-separated integers 0..11 describing pitch-class set.
  - Apply: persist inline edits to the selected node's representative chord.

- Edit Node (dialog)
  - Opens a full editor for a node's attributes including Styles and an optional Count badge and Merge Styles checkbox.

- Sequence area
  - Sequence list: ordered items for playback.
  - Load Selection: push selected canvas nodes into the sequence buffer.
  - Move Up / Move Down / Remove: adjust sequence order.

## Glossary

- Node: a vertex representing a chord; shown as a labeled box on the canvas.
- Representative chord: the chord data used to identify and format a node (label, root PC, quality, pitch classes).
- Root PC / tonic: integer 0..11 representing pitch class root.
- Style: free-form tag(s) used to group or color nodes.
- Count: optional integer displayed as a badge on a node used for frequency or weighting.

## Example Workflows

- Load and inspect demo
  1. Click Load Demo.
 2. Click nodes to inspect their attributes in the inline inspector.

- Create a node and connect it
  1. Click Create Node and fill required fields (Id/Label, Root PC, Pitch Classes, Style).
 2. Shift+Click an existing node (source) then the new node (target).
 3. Click Create Edge and confirm the visual arrow appears.

- Edit node attributes
  1. Select a node and click Edit Node.
  2. Change Quality, Styles or Count and click Save.
  3. Verify the canvas visual and badge update (badge pulses on Count change).

- Playback a sequence
  1. Shift+Click nodes in desired order.
  2. Click Load Selection, then Play.
  3. Use Tempo slider and Loop settings to control playback.

## Manual Test Checklist

- [ ] Build and run the viewer without errors.
- [ ] Load Demo populates canvas and analysis numbers update.
- [ ] Clicking a node populates the inline inspector.
- [ ] Inline Apply updates the node representative and canvas visuals.
- [ ] Create Node adds a new node (appears on canvas).
- [ ] Create Edge / Remove Edge operate using Shift+Click selection and sequence buffer paths.
- [ ] Edit Node dialog updates Styles and Count; badge animation occurs on Count change.
- [ ] Import Graph JSON loads a graph file and Update Reload state becomes enabled.
- [ ] Import MIDI adds nodes from a MIDI file (basic grouping).
- [ ] Transpose applies semitone shift and reloads the transposed graph.

## Troubleshooting

- If a button does nothing, check the Output window in Visual Studio for exceptions.
- If Import Graph JSON fails, verify the JSON file is in the HarmonyGraph export format (.graph.json) or compatible schema.
- MIDI import is basic: complex MIDI files may not produce meaningful chord grouping; use simple single-track example files for testing.

## Samples and Help

- Place sample .graph.json files in a /samples folder at the repo root for quick loading.
- Consider adding tooltips and a Help menu that opens this file from the running app for future usability improvements.

---

If you want, I can now:
- A) Commit this updated DiGraphLab_User_Guide.md and push to origin/master.
- B) Also add a Help menu item and tooltips to the viewer that open this guide at runtime.
- C) Create a /samples folder with one or two small sample graphs and add a menu item to load them.

Tell me which of A/B/C to perform next.

