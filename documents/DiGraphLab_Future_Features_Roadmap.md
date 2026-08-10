# DiGraphLab — Future Features Roadmap

## Why this document exists

This complements `DiGraphLab_Project_Plan.md` with a practical roadmap focused on:

1. **Pedagogy** (what concept the learner sees),
2. **Application fit** (how it appears in Graph/Matrix UI),
3. **Development sequence** (what to build first for compounding value).

---

## Guiding principles

- Keep **DiGraphLab.Core** algorithm-first and UI-independent.
- Keep **DiGraphLab** (WinForms) focused on interaction, explanation, and visualization.
- Prefer features that produce both a **computed result** and a **visual explanation**.
- Build in small increments, each with a demo scenario and test coverage.

---

## Phase A — Foundational study tools (highest instructional value)

### A1. Reachability query (u → v?)
- **Concept:** Directed path existence.
- **Pedagogy:** First core question in digraphs.
- **UI fit:** Select two vertices, show reachable/not reachable and (optionally) one witness path.
- **Core work:** BFS/DFS methods in `DiGraphLab.Core`.
- **Why early:** Enables many later tools and immediate learner feedback.

### A2. Strongly Connected Components (SCC)
- **Concept:** Mutual reachability classes.
- **Pedagogy:** Foundation for reducibility and structure.
- **UI fit:** Color vertices by SCC; show component list.
- **Core work:** Tarjan/Kosaraju implementation.
- **Why early:** Direct bridge to condensation and reducibility.

### A3. Cycle detection + DAG check + topological order
- **Concept:** Cyclic vs acyclic behavior.
- **Pedagogy:** Clarifies when partial orders exist.
- **UI fit:** Analysis panel result; if DAG, show topo sequence.
- **Core work:** DFS coloring or Kahn algorithm.
- **Why early:** Common educational milestone after SCC/reachability.

### A4. Vertex/edge local invariants
- **Concept:** In-degree, out-degree, predecessors, successors.
- **Pedagogy:** Local properties support global reasoning.
- **UI fit:** Property panel details on selection.
- **Core work:** Efficient helper methods on graph model.

---

## Phase B — Reducibility and structural analysis (research-oriented)

### B1. Reducible/irreducible classification
- **Concept:** Whether graph is strongly connected as a whole.
- **Pedagogy:** Directly tied to classical reducibility discussions.
- **UI fit:** One-click result card with rationale and SCC witness.
- **Core work:** Based on SCC results.

### B2. Condensation graph (SCC DAG)
- **Concept:** Contract SCCs into super-nodes.
- **Pedagogy:** Makes global structure transparent.
- **UI fit:** Alternate view mode: Original vs Condensation.
- **Core work:** SCC mapping + projected inter-component edges.

### B3. Source/sink SCC identification
- **Concept:** Entry/exit blocks in component DAG.
- **Pedagogy:** Supports decomposition and flow interpretation.
- **UI fit:** Highlight SCC nodes with zero in-degree or zero out-degree.

---

## Phase C — Matrix-centric computation features

### C1. Transitive closure matrix
- **Concept:** Reachability matrix.
- **Pedagogy:** Connects graph traversal to boolean matrix reasoning.
- **UI fit:** Matrix mode toggle: Adjacency / Closure.
- **Core work:** Warshall or repeated BFS.

### C2. Matrix powers (A^k)
- **Concept:** Length-k path existence/count behavior.
- **Pedagogy:** Visualizes dynamic path growth.
- **UI fit:** `k` selector in matrix view.
- **Core work:** Boolean multiply (existence) and optional integer multiply (counts).

### C3. Matrix diagnostics
- **Concept:** Sparsity, density, diagonal loops.
- **Pedagogy:** Teaches structural signatures quickly.
- **UI fit:** Summary strip in matrix tab.

---

## Phase D — Learning UX and explainability

### D1. Guided analysis workflows
- **Concept:** “Run analysis” with interpretation text.
- **Pedagogy:** Makes outputs meaningful, not just numeric.
- **UI fit:** Right panel with compact explanations and examples.

### D2. Stepwise algorithm playback
- **Concept:** BFS/DFS/SCC progression.
- **Pedagogy:** Shows algorithm behavior over time.
- **UI fit:** Play/Pause/Next controls and highlighted nodes/edges.

### D3. Example library
- **Concept:** Curated sample digraphs by concept.
- **Pedagogy:** Immediate hands-on cases for teaching.
- **UI fit:** File/New From Template (DAG, SCC-heavy, reducible, etc.).

---

## Phase E — Reliability, reproducibility, and scale

### E1. Automated tests (Core)
- **Scope:** Reachability, SCC, topo, closure, reducibility.
- **Value:** Protects correctness as features grow.

### E2. Import/export for analyses
- **Scope:** Save analysis results (JSON/CSV) with graph snapshots.
- **Value:** Repeatable experiments and sharing.

### E3. Performance checkpoints
- **Scope:** Baseline timings at 10, 50, 100, 500 vertices.
- **Value:** Predictable behavior as graph size increases.

---

## Proposed implementation sequence (practical)

1. Reachability query + witness path
2. SCC computation + graph coloring
3. Reducibility classification
4. Condensation graph view
5. Cycle detection + topological sort
6. Transitive closure matrix mode
7. Matrix powers mode
8. Guided explanations + examples
9. Test expansion + export/reporting

This order maximizes pedagogical impact early while building reusable computational primitives.

---

## Mapping to current codebase

- **Core algorithms and data contracts:** `DiGraphLab.Core`
  - `DirectedGraph.cs` and adjacent algorithm/helper classes
- **Interaction and presentation:** `DiGraphLab/MainForm.cs`
  - Toolbar/menu commands, highlighting, dialogs, matrix modes
- **Project-level planning docs:** `documents/`

---

## “Dream” horizon (longer-term)

- Condensation + original graph synchronized side-by-side views
- Interactive theorem checks (e.g., SCC-related properties)
- Session notebooks: sequence of graph edits + analyses + commentary
- Plugin architecture for custom algorithm modules

These are aspirational and should follow only after stable Phase A–C delivery.

---

## Suggested next concrete increment

For the next coding session, the most strategic single increment is:

1. **Implement SCC in `DiGraphLab.Core`**
2. **Add “Highlight SCCs” action in the UI**
3. **Show component summary (count, members, reducible/irreducible hint)**

That gives immediate educational value and unlocks the reducibility roadmap.