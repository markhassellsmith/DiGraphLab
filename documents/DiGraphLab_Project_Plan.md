# DiGraphLab — Project Plan

## 1. Purpose

**DiGraphLab** will be a C#/.NET educational workbench for studying **directed graphs**.

The goal is not merely to draw graphs. The application should let the user create, inspect, manipulate, analyze, save, and reload directed graphs while making graph-theory concepts visible and interactive.

The project will be developed as **one Visual Studio solution**, rather than assembling several unrelated graph-editor applications.

## 2. Implemented Architecture

- **Solution:** `DiGraphLab.slnx`
- **Target Framework:** .NET 10
- **WinForms application:** `DiGraphLab`
- **Core graph library:** `DiGraphLab.Core`
- **Visualization/layout:** Microsoft Automatic Graph Layout (**MSAGL**)
- **Algorithms:** **QuickGraphCore** for graph algorithms and structures

```text
DiGraphLab.sln
│
├── DiGraphLab
│   ├── WinForms user interface
│   ├── Graph editor interaction
│   ├── Menus / toolbars
│   ├── Property panels
│   ├── Visualization controls
│   └── Educational demonstrations
│
└── DiGraphLab.Core
    ├── DirectedGraph
    ├── Vertex
    ├── Edge
    ├── Graph properties
    ├── Graph algorithms
    └── Import / export models
```

This keeps the solution simple while separating the **graph-theory model** from the **user interface**.

## 3. Installed NuGet Packages

### Microsoft.Msagl.GraphViewerGDI (v1.1.7)

**Installed in:** DiGraphLab (WinForms project)

**Primary purpose:** graph visualization and automatic layout.

MSAGL provides:

- Directed graph visualization
- Automatic graph layout
- Node and edge rendering
- Pan and zoom
- Graph navigation
- Highlighting
- Layout algorithms
- Rendering the graph after changes

**Important:** MSAGL should **not** be treated as the graph editor itself. Its visualization/layout capabilities are the reason for using it; the interactive editing behavior will belong to DiGraphLab.

### QuickGraphCore (v1.0.0)

**Installed in:** DiGraphLab.Core

**Primary purpose:** graph algorithms and related graph structures.

QuickGraphCore is a modern .NET port (fully compatible with .NET 10) that provides usable, battle-tested implementations for:

- Breadth-first search
- Depth-first search
- Shortest paths
- Graph traversal
- Connectivity algorithms
- Strongly connected components
- Topological sorting
- Various graph data structures

**Note:** We're using existing algorithm implementations from QuickGraphCore rather than building from scratch, allowing us to focus on the educational visualization and interaction aspects of the workbench.

## 4. Deliberately Excluded Alternatives

The following were considered but **not** included in the architecture:

- A separate third-party graph-editor application merely to obtain basic editing operations.
- **NodeEditorWinforms** — useful as a general node editor, but not necessary for this project.
- **SimpleStateMachineNodeEditor** — interesting interaction model, but aimed at state-machine editing rather than our general directed-graph laboratory.
- JavaScript/browser graph editors — unnecessary technology and architectural complexity for a C#/.NET desktop application.
- **Original QuickGraph package (v3.6.x)** — replaced with QuickGraphCore for better .NET Core/.NET 10 compatibility.

## 5. Interactive Graph Editing

DiGraphLab itself should implement the basic editing behavior:

| Operation | Implementation |
|---|---|
| Click empty space to create a node | DiGraphLab |
| Drag between nodes to create an edge | DiGraphLab |
| Move nodes | DiGraphLab |
| Select nodes | DiGraphLab |
| Select edges | DiGraphLab |
| Delete nodes | DiGraphLab |
| Delete edges | DiGraphLab |
| Reverse/change edge direction | DiGraphLab |
| Create reflexive/self-loop edges | DiGraphLab |

MSAGL handles visualization/layout; **DiGraphLab owns the editing semantics**.

## 6. Core Graph Model

The graph model should initially remain simple.

```text
DirectedGraph
    ├── Vertices
    └── Edges

Vertex
    ├── ID
    ├── Label
    └── optional user data

Edge
    ├── ID
    ├── Source
    ├── Target
    ├── Label
    └── optional user data
```

An edge is explicitly directional:

```text
A → B
```

where `A` is the source and `B` is the target.

A reflexive edge is allowed:

```text
A → A
```

The graph model should be independent of MSAGL so that graph-theory operations do not depend on the visualization technology.

## 7. Graph Information to Display

Selecting a vertex should eventually provide useful graph-theory information, for example:

```text
Vertex A

In-degree:       3
Out-degree:      2
Total degree:    5

Predecessors:
    C
    E
    F

Successors:
    B
    D
```

Selecting an edge could show:

```text
Edge A → B

Source:       A
Target:       B
Self-loop:    No
```

This makes the application an educational tool rather than simply a drawing program.

## 8. Planned Graph-Theory Features

The exact list can grow over time, but the workbench should eventually support demonstrations of:

- In-degree and out-degree
- Predecessors and successors
- Paths
- Walks
- Trails
- Cycles
- Reachability
- Connectivity
- Strongly connected components
- Weak connectivity
- Directed acyclic graphs
- Topological sorting
- Transitive closure
- Breadth-first search
- Depth-first search
- Shortest paths
- Adjacency representations
- Incidence relationships
- Sources and sinks

Algorithm results should ideally be **visualized on the graph**. For example, a BFS could highlight traversal order, while a shortest-path operation could highlight the resulting path.

## 9. Development Phases

### Phase 0 — Solution Setup ✅ COMPLETED

**Completed steps:**
- Created `DiGraphLab.slnx` solution targeting .NET 10
- Created `DiGraphLab.Core` class library project
- Created `DiGraphLab` WinForms application project
- Added project reference from DiGraphLab to DiGraphLab.Core
- Installed Microsoft.Msagl.GraphViewerGDI (v1.1.7) in DiGraphLab project
- Installed QuickGraphCore (v1.0.0) in DiGraphLab.Core project
- Verified solution builds successfully

**Status:** Infrastructure complete and ready for development.

### Phase 1 — Basic graph display

- Create basic graph model classes in DiGraphLab.Core
- Construct a graph programmatically
- Set up MSAGL viewer in WinForms
- Display the graph
- Experiment with layout

**Goal:** prove the visualization architecture.

### Interactive editing: current status & implementation notes

- Current small-proof implementation (UI): sample graph rendering and a right-click-on-empty-space handler that creates a new vertex (auto-labeled). This lives in the WinForms project and keeps MSAGL code out of DiGraphLab.Core.
- Short-term goal (Phase 2): implement point-and-click selection for vertices and edges. Selection will be implemented in the UI layer using MSAGL hit-testing, resolving MSAGL node/edge id -> Vertex.Id / Edge.Id in the core model.
- Drag-and-drop and edge-creation via mouse-drag are planned in Phase 2/Phase 3 once reliable selection is in place.
- Freezing node positions (per-node and global) will be added when manual placement is introduced:
  - Provide a per-node "Freeze position / Unfreeze" context action and a global "Freeze layout" toggle.
  - Implementation approach: store a mapping between Vertex.Id and the MSAGL node position; when a node is frozen set its position and mark it fixed so MSAGL preserves it while laying out unfrozen nodes.
- Design rule: keep graph model (DiGraphLab.Core) independent of MSAGL. All hit-testing, position storage for visualization, and UI actions should live in the WinForms project; the model should only expose stable ids, labels, and data.

These notes are intended to mark the most appropriate development points for implementing interactive editing features and to record the decisions already made.

### Phase 2 — Vertex interaction

Implement:

- Select vertex
- Move vertex
- Delete vertex
- Create vertex

**Goal:** establish the basic editing interaction.

### Phase 3 — Edges

Implement:

- Select edge
- Create directed edge
- Delete edge
- Reverse edge
- Self-loop

**Goal:** have a usable directed-graph editor.

### Phase 4 — Graph properties

Add:

- In-degree
- Out-degree
- Predecessors
- Successors
- Sources
- Sinks
- Cycle detection
- Reachability

**Goal:** begin turning the editor into a graph-theory laboratory.

### Phase 5 — Algorithms

Integrate QuickGraphCore algorithms and create UI for visualizing results:

1. BFS (Breadth-first search)
2. DFS (Depth-first search)
3. Path finding
4. Shortest path (Dijkstra's algorithm)
5. Topological sort
6. Strongly connected components
7. Transitive closure

Display the algorithm's result directly on the graph with highlighting, animation, or step-by-step visualization.

### Phase 6 — Import / Export

Support one or more useful graph representations:

- DiGraphLab's own simple format
- Adjacency-list representation
- Adjacency-matrix representation
- GraphML, if useful
- JSON, if useful

Import/export should preserve graph structure rather than merely storing a screenshot of the graph.

### Phase 7 — Educational enhancements

Add features such as:

- Graph statistics
- Algorithm animation
- Step-by-step traversal
- Highlighting of predecessors/successors
- Path highlighting
- SCC highlighting
- Topological-order display
- Adjacency matrix display
- Adjacency-list display
- Graph property panels

## 10. Architectural Principle

Keep these three concerns separate:

```text
             DiGraphLab
                  │
       ┌──────────┴──────────┐
       │                     │
       ▼                     ▼
Graph Model              User Interface
DiGraphLab.Core             WinForms
       │                     │
       └──────────┬──────────┘
                  │
                  ▼
               MSAGL
        Layout + Visualization
```

The **graph model knows graph theory**.

The **WinForms application knows user interaction**.

**MSAGL knows graph layout and rendering.**

This makes it possible to change the visualization technology later without rewriting the graph-theory model.

## 11. Why Implement the Editor Ourselves?

The basic editing operations are manageable:

1. Click empty space → create vertex
2. Drag from vertex to vertex → create edge
3. Drag vertex → move vertex
4. Click → select
5. Delete → remove selected object
6. Reverse edge → swap source and target
7. Drag a vertex back to itself → create self-loop

The complexity is primarily in handling mouse interaction and maintaining synchronization between the application's graph model and the visual representation.

That is manageable and, importantly, gives the project educational value. There is no need to introduce another graph-editor framework simply to obtain these operations.

## 12. Initial Scope

The first version should **not** attempt to be a full commercial graph editor.

The initial objective is a reliable educational workbench that can:

- Create a directed graph
- Display it clearly
- Move and edit vertices
- Create and edit directed edges
- Select graph objects
- Delete graph objects
- Display basic graph properties
- Save/load graphs
- Run a few fundamental graph algorithms
- Visually demonstrate their results

The application can become considerably more sophisticated later.

## 13. Project Names

- **Solution:** `DiGraphLab.slnx`
- **Target Framework:** .NET 10
- **Application:** `DiGraphLab` (WinForms)
- **Core library:** `DiGraphLab.Core` (class library)

The name emphasizes the primary subject: a laboratory/workbench for **directed graphs**.

## 14. Implementation Summary

The implemented approach is **one coherent Visual Studio solution** targeting .NET 10.

**Architecture:**

- **WinForms** for the desktop user interface
- **Microsoft.Msagl.GraphViewerGDI (v1.1.7)** for graph visualization and automatic layout
- **QuickGraphCore (v1.0.0)** for ready-to-use graph algorithms
- **DiGraphLab.Core** for the graph model/data structures
- **DiGraphLab** for custom editing behavior and educational UI

**Key design decisions:**

1. DiGraphLab owns the graph model and editing behavior
2. MSAGL handles only visualization/rendering
3. QuickGraphCore provides battle-tested algorithm implementations
4. Focus on educational visualization and interaction rather than reinventing algorithms

This gives us a practical, modern application platform for exploring and learning graph theory.
