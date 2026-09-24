using System;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using GraphicsUnit = System.Drawing.GraphicsUnit;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.MDS;
using DiGraphLab.Core;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using CoreVertex = DiGraphLab.Core.Vertex;
using CoreEdge = DiGraphLab.Core.Edge;

namespace DiGraphLab;

public class MainForm : Form
{
    private readonly GViewer _viewer;
    private DirectedGraph? _model;
    private ToolStripButton? _settingsBtn;
    private Settings _settings = new Settings();

    private int _nextAutoLabel = 1;
    private Guid? _selectedVertexId;
    private Guid? _selectedEdgeId;
    private readonly System.Collections.Generic.HashSet<Guid> _selectedVertexIds = new();
    private readonly System.Collections.Generic.HashSet<Guid> _selectedEdgeIds = new();
    private readonly System.Collections.Generic.HashSet<Guid> _frozenNodes = new();

    private readonly ContextMenuStrip _graphContextMenu;
    private readonly System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point> _positions = new();
    private readonly ToolStrip _toolStrip;
    private readonly ToolStripButton _freezeToggleButton;
    private readonly System.Collections.Generic.Dictionary<string, System.Drawing.Bitmap> _toolbarIcons = new();
    private TabControl _mainTab;
    private DataGridView _matrixGrid;
    private int _lastSelectedTabIndex = 0;
    private ToolStripButton? _applyMatrixBtn;
    private ToolStripButton? _discardMatrixBtn;
    private bool _matrixDirty = false;
    private bool _suppressTabChange = false;
    private ToolTip _hoverToolTip;
    private ToolStripButton? _addMatrixVertexBtn;
    private ToolStripButton? _removeMatrixVertexBtn;
    private ToolStripButton? _importMatrixCsvBtn;
    private ToolStripButton? _exportMatrixCsvBtn;
    private ToolStripButton? _exportMatrixPngBtn;
    private ToolStripButton? _importGraphCsvBtn;
    private ToolStripButton? _exportGraphCsvBtn;
    private ToolStripDropDownButton? _presetsDropDown;
    private readonly System.Collections.Generic.List<PresetItem> _presetItems = new();
    private enum GraphLayoutMode
    {
        AutoDefault,
        AutoMds,
        AutoMdsAlignedFifths,
        FixedTraditionalFifths,
        FixedChordState48,
        FixedRectangularLattice,
        FixedNgonalLattice
    }

    private void ConfigureAndShowContextMenu(System.Drawing.Point location)
    {
        if (_graphContextMenu == null) return;

        var addVertexItem = _graphContextMenu.Items["ctxAddVertex"] as ToolStripMenuItem;
        var addEdgePairItem = _graphContextMenu.Items["ctxAddEdgePair"] as ToolStripMenuItem;
        var addSelfEdgeItem = _graphContextMenu.Items["ctxAddSelfEdge"] as ToolStripMenuItem;
        var renameVertexItem = _graphContextMenu.Items["ctxRenameVertex"] as ToolStripMenuItem;
        var renameEdgeItem = _graphContextMenu.Items["ctxRenameEdge"] as ToolStripMenuItem;
        var reverseEdgeItem = _graphContextMenu.Items["ctxReverseEdge"] as ToolStripMenuItem;
        var deleteItem = _graphContextMenu.Items["ctxDelete"] as ToolStripMenuItem;
        var freezeVertexItem = _graphContextMenu.Items["ctxFreezeVertex"] as ToolStripMenuItem;

        var onEmpty = !_contextVertexId.HasValue && !_contextEdgeId.HasValue;
        var onVertex = _contextVertexId.HasValue;
        var onEdge = _contextEdgeId.HasValue;

        if (addVertexItem != null)
        {
            addVertexItem.Visible = onEmpty;
            addVertexItem.Enabled = onEmpty && _model != null;
        }

        if (addEdgePairItem != null)
        {
            // Only offer the explicit add-edge command when exactly two vertices are
            // selected and the right-clicked vertex is the source (the "from" vertex).
            // Prefer the snapshot captured at menu time; fall back to the live selection
            // only if the snapshot is not populated.
            var sourceList = _selectionSnapshot.Count == 2 ? _selectionSnapshot : _selectedVertexIds.ToList();
            var exactlyTwoAndSourceIncluded = _contextVertexId.HasValue && sourceList.Count == 2 && sourceList.Contains(_contextVertexId.Value);

            addEdgePairItem.Visible = onVertex && exactlyTwoAndSourceIncluded;
            addEdgePairItem.Enabled = onVertex && exactlyTwoAndSourceIncluded && _model != null;

            // Clear any previous dynamic submenu entries and handlers
            addEdgePairItem.DropDownItems.Clear();
            addEdgePairItem.Tag = null;
            addEdgePairItem.Click -= GraphContext_AddEdgeBetweenSelected_Click;
            addEdgePairItem.Click -= GraphContext_AddEdgeToTarget_Click;

            if (addEdgePairItem.Visible && _contextVertexId.HasValue && exactlyTwoAndSourceIncluded)
            {
                var fromLabel = GetVertexLabel(_contextVertexId.Value);
                var others = sourceList.Where(id => id != _contextVertexId.Value).ToList();

                if (others.Count == 1)
                {
                    var toLabel = GetVertexLabel(others[0]);
                    addEdgePairItem.Text = $"Add new edge from {fromLabel} to {toLabel}";
                    addEdgePairItem.Tag = others[0];
                    addEdgePairItem.Click += GraphContext_AddEdgeToTarget_Click;
                }
                else
                {
                    // Shouldn't happen under the above guard, but provide a safe fallback
                    addEdgePairItem.Text = "Add new edge";
                    addEdgePairItem.Click += GraphContext_AddEdgeBetweenSelected_Click;
                }
            }
            else
            {
                addEdgePairItem.Text = "Add new edge";
                addEdgePairItem.Click += GraphContext_AddEdgeBetweenSelected_Click;
            }
        }

        if (addSelfEdgeItem != null)
        {
            addSelfEdgeItem.Visible = onVertex;
            addSelfEdgeItem.Enabled = onVertex && _model != null;
            if (_contextVertexId.HasValue)
                addSelfEdgeItem.Text = $"Add self-edge on {GetVertexLabel(_contextVertexId.Value)}";
            else
                addSelfEdgeItem.Text = "Add self-edge";
        }

        if (renameVertexItem != null)
        {
            renameVertexItem.Visible = onVertex;
            renameVertexItem.Enabled = onVertex;
        }

        if (freezeVertexItem != null)
        {
            freezeVertexItem.Visible = onVertex;
            freezeVertexItem.Enabled = onVertex;
        }

        if (renameEdgeItem != null)
        {
            renameEdgeItem.Visible = onEdge;
            renameEdgeItem.Enabled = onEdge;
        }

        if (reverseEdgeItem != null)
        {
            reverseEdgeItem.Visible = onEdge;
            reverseEdgeItem.Enabled = onEdge && _model != null;
        }

        if (deleteItem != null)
        {
            deleteItem.Visible = onVertex || onEdge;
            deleteItem.Enabled = onVertex || onEdge;
            if (onVertex && _contextVertexId.HasValue)
                deleteItem.Text = $"Delete {GetVertexLabel(_contextVertexId.Value)}";
            else if (onEdge && _contextEdgeId.HasValue)
                deleteItem.Text = $"Delete {GetEdgeDisplayName(_contextEdgeId.Value)}";
            else
                deleteItem.Text = "Delete";
        }

        _graphContextMenu.Show(_viewer, location);
    }

    private string GetVertexLabel(Guid id)
    {
        if (_model != null && _model.TryGetVertex(id, out var v) && v != null)
        {
            var label = (v.Label ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(label)) return label;
        }
        return id.ToString("N")[..8];
    }

    private string GetEdgeDisplayName(Guid id)
    {
        if (_model == null) return id.ToString("N")[..8];
        if (_model.TryGetEdge(id, out var edge) && edge != null)
        {
            var src = edge.Source?.Label ?? "?";
            var dst = edge.Target?.Label ?? "?";
            if (!string.IsNullOrWhiteSpace(edge.Label))
                return $"{src}→{dst} ({edge.Label})";
            return $"{src}→{dst}";
        }
        return id.ToString("N")[..8];
    }

    private void GraphContext_AddVertex_Click(object? sender, EventArgs e)
    {
        if (_model == null || !_contextClickGraphPoint.HasValue) return;

        var defaultLabel = "V" + _nextAutoLabel++;
        string label;
        try { label = Microsoft.VisualBasic.Interaction.InputBox("Vertex label:", "Add Vertex", defaultLabel); }
        catch { label = defaultLabel; }
        if (string.IsNullOrWhiteSpace(label)) label = defaultLabel;
        label = NormalizeChordLabelSymbols(label);

        var v = _model.CreateVertex(label);
        _positions[v.Id] = _contextClickGraphPoint.Value;
        PushUndo(new CreateVertexAction(v.Id, v.Label, _contextClickGraphPoint));
        ClearSelection();
        SelectVertex(v.Id);
        RenderGraph(_model);
    }

    private bool HasEdge(Guid sourceId, Guid targetId)
    {
        return _model?.Edges.Any(e => e.Source.Id == sourceId && e.Target.Id == targetId) == true;
    }

    private void AddEdgeIfMissing(Guid sourceId, Guid targetId, bool allowSelf)
    {
        if (_model == null) return;
        if (!allowSelf && sourceId == targetId) return;

        if (HasEdge(sourceId, targetId))
        {
            MessageBox.Show(this, "That edge already exists.", "Add Edge", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string label;
        try { label = Microsoft.VisualBasic.Interaction.InputBox("Edge label (optional):", "Add Edge", ""); }
        catch { label = string.Empty; }

        var edge = _model.CreateEdge(sourceId, targetId, label ?? string.Empty);
        PushUndo(new CreateEdgeAction(edge.Id, edge.Label, edge.Source.Id, edge.Target.Id));
        RenderGraph(_model);
        SelectEdge(edge.Id);
    }

    private void GraphContext_AddEdgeBetweenSelected_Click(object? sender, EventArgs e)
    {
        // Require exactly two selected vertices for this action (unless a specific
        // target was provided via a tagged menu item). This keeps edge creation
        // explicit and avoids ambiguity when 3+ vertices are selected.
        if (_model == null || !_contextVertexId.HasValue) return;
        var source = _contextVertexId.Value;

        // If this menu item was constructed with an explicit target tag, honor it.
        if (sender is ToolStripMenuItem tsi && tsi.Tag is Guid tagged)
        {
            AddEdgeIfMissing(source, tagged, allowSelf: false);
            return;
        }

        // Otherwise require the selection snapshot to contain exactly two ids.
        if (_selectionSnapshot.Count != 2) return;
        var target = _selectionSnapshot.FirstOrDefault(id => id != source);
        if (target == Guid.Empty) return;
        AddEdgeIfMissing(source, target, allowSelf: false);
    }

    private void GraphContext_AddEdgeToTarget_Click(object? sender, EventArgs e)
    {
        if (_model == null || !_contextVertexId.HasValue) return;
        var source = _contextVertexId.Value;
        Guid? target = null;
        if (sender is ToolStripMenuItem tsi && tsi.Tag is Guid tg) target = tg;
        else if (sender is ToolStripMenuItem tsi2 && tsi2.OwnerItem is ToolStripMenuItem parent && parent.Tag is Guid pt) target = pt;
        if (!target.HasValue) return;
        AddEdgeIfMissing(source, target.Value, allowSelf: false);
    }

    private void GraphContext_AddSelfEdge_Click(object? sender, EventArgs e)
    {
        if (_model == null || !_contextVertexId.HasValue) return;
        var source = _contextVertexId.Value;

        var confirm = MessageBox.Show(
            this,
            $"Create a self-edge on {GetVertexLabel(source)}?",
            "Add Self-Edge",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        // Intentionally allow self-edges independent of multi-selection state.
        AddEdgeIfMissing(source, source, allowSelf: true);
    }

    private void GraphContext_Delete_Click(object? sender, EventArgs e)
    {
        if (_contextVertexId.HasValue)
        {
            SelectVertex(_contextVertexId.Value);
            VertexDelete_Click(sender, e);
            return;
        }
        if (_contextEdgeId.HasValue)
        {
            SelectEdge(_contextEdgeId.Value);
            EdgeDelete_Click(sender, e);
        }
    }

    private void GraphContext_ReverseEdge_Click(object? sender, EventArgs e)
    {
        if (_model == null || !_contextEdgeId.HasValue) return;
        if (!_model.TryGetEdge(_contextEdgeId.Value, out var edge) || edge == null) return;

        var srcId = edge.Source.Id;
        var dstId = edge.Target.Id;
        if (HasEdge(dstId, srcId))
        {
            var confirm = MessageBox.Show(
                this,
                $"The reverse edge {GetVertexLabel(dstId)}→{GetVertexLabel(srcId)} already exists. Delete {GetVertexLabel(srcId)}→{GetVertexLabel(dstId)} and keep the existing reverse edge?",
                "Reverse Edge",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            var removedOnly = _model.RemoveEdge(edge.Id);
            if (removedOnly == null) return;
            PushUndo(new DeleteEdgeAction(removedOnly));
            RenderGraph(_model);

            var existingReverse = _model.Edges.FirstOrDefault(e => e.Source.Id == dstId && e.Target.Id == srcId);
            if (existingReverse != null)
                SelectEdge(existingReverse.Id);
            return;
        }

        var removed = _model.RemoveEdge(edge.Id);
        if (removed == null) return;
        PushUndo(new DeleteEdgeAction(removed));

        var created = _model.CreateEdge(dstId, srcId, edge.Label ?? string.Empty, edge.Color);
        PushUndo(new CreateEdgeAction(created.Id, created.Label, created.Source.Id, created.Target.Id));

        RenderGraph(_model);
        SelectEdge(created.Id);
    }

    private GraphLayoutMode _currentLayoutMode = GraphLayoutMode.AutoDefault;
    private ToolStripLabel _statusLabel;
    private Guid? _lastHoverVertexId;
    private Guid? _lastHoverEdgeId;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _bottomStatusLabel;
    private bool _globalFreeze;
    private ToolStripButton? _addVertexBtn;
    private ToolStripButton? _addEdgeBtn;
    private bool _addVertexMode;
    private bool _addEdgeMode;
    private Guid? _pendingEdgeSourceId;
    private Guid? _contextVertexId;
    private Guid? _contextEdgeId;
    private Microsoft.Msagl.Core.Geometry.Point? _contextClickGraphPoint;
    // Snapshot of selection used when building context menus to avoid timing/order issues
    private System.Collections.Generic.List<Guid> _selectionSnapshot = new();

    // simple undo/redo
    private readonly System.Collections.Generic.List<IUndoableAction> _undoStack = new();
    private readonly System.Collections.Generic.List<IUndoableAction> _redoStack = new();
    private const int MaxUndo = 5;

    public MainForm()
    {
        Text = "DiGraphLab";
        Width = 1000;
        Height = 700;

        _viewer = new GViewer
        {
            Dock = DockStyle.Fill
        };

        // main tab control with two screens: Graph view and Matrix view
        _mainTab = new TabControl { Dock = DockStyle.Fill };
        var graphTab = new TabPage("Graph") { Padding = new Padding(0) };
        var matrixTab = new TabPage("Adjacency Matrix") { Padding = new Padding(0) };

        graphTab.Controls.Add(_viewer);
        _matrixGrid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false };
        matrixTab.Controls.Add(_matrixGrid);

        // monitor edits to mark matrix dirty
        _matrixGrid.CellValueChanged += (s, e) => { _matrixDirty = true; ToggleMatrixApplyButtons(); };
        _matrixGrid.CurrentCellDirtyStateChanged += (s, e) => { if (_matrixGrid.IsCurrentCellDirty) _matrixGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        // allow typing '0'/'1' into checkbox cells by parsing string input to bool on commit
        _matrixGrid.CellParsing += (s, e) =>
        {
            try
            {
                if (e.ColumnIndex >= 2 && e.Value is string sVal && int.TryParse(sVal.Trim(), out var xi))
                {
                    e.Value = xi != 0;
                    e.ParsingApplied = true;
                }
            }
            catch { }
        };

        // accept keyboard digits 0/1 to set checkbox cells when focused
        _matrixGrid.KeyDown += (s, e) =>
        {
            try
            {
                var cell = _matrixGrid.CurrentCell;
                if (cell == null) return;
                if (cell.ColumnIndex < 2) return; // only adjacency checkbox columns

                bool? desired = null;
                if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) desired = false;
                else if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) desired = true;

                if (desired.HasValue)
                {
                    // set value and commit
                    cell.Value = desired.Value;
                    _matrixDirty = true;
                    ToggleMatrixApplyButtons();
                    e.Handled = true;
                }
            }
            catch { }
        };

        // suppress DataGridView errors from transient edit states
        _matrixGrid.DataError += (s, e) => { e.ThrowException = false; };

        _mainTab.TabPages.Add(graphTab);
        _mainTab.TabPages.Add(matrixTab);
        Controls.Add(_mainTab);
        _mainTab.SelectedIndexChanged += MainTab_SelectedIndexChanged;

        // toolbar
        _toolStrip = new ToolStrip { Dock = DockStyle.Top };
        _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        _toolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
        _toolStrip.Padding = new Padding(4);
        _toolStrip.RenderMode = ToolStripRenderMode.System;

        var fileDrop = new ToolStripDropDownButton("File");
        var editDrop = new ToolStripDropDownButton("Edit");
        _presetsDropDown = new ToolStripDropDownButton("Presets") { ToolTipText = "Load preset graphs" };

        // File group (alphabetized)
        var exportAdjBtn = new ToolStripButton("Export Adjacency") { ToolTipText = "Export adjacency matrix (dense/sparse)" };
        exportAdjBtn.Click += ExportAdjacency_Click;

        _exportGraphCsvBtn = new ToolStripButton("Export Graph CSV") { ToolTipText = "Export graph to CSV" };
        _exportGraphCsvBtn.Click += ExportGraphCsv_Click;

        _exportMatrixCsvBtn = new ToolStripButton("Export Matrix CSV") { Visible = false, ToolTipText = "Export adjacency matrix to CSV" };
        _exportMatrixCsvBtn.Click += ExportMatrixCsv_Click;

        _exportMatrixPngBtn = new ToolStripButton("Export Matrix PNG") { Visible = false, ToolTipText = "Export matrix view as PNG image" };
        _exportMatrixPngBtn.Click += ExportMatrixPng_Click;

        var exportPngBtn = new ToolStripButton("Export PNG") { ToolTipText = "Export graph image as PNG" };
        exportPngBtn.Click += ExportViewerPng_Click;

        var importAdjBtn = new ToolStripButton("Import Adjacency") { ToolTipText = "Import adjacency matrix (dense/sparse)" };
        importAdjBtn.Click += ImportAdjacency_Click;

        _importGraphCsvBtn = new ToolStripButton("Import Graph CSV") { ToolTipText = "Import graph from CSV" };
        _importGraphCsvBtn.Click += ImportGraphCsv_Click;

        _importMatrixCsvBtn = new ToolStripButton("Import Matrix CSV") { Visible = false, ToolTipText = "Import adjacency matrix from CSV" };
        _importMatrixCsvBtn.Click += ImportMatrixCsv_Click;

        var openItem = new ToolStripButton("Open") { ToolTipText = "Open graph from JSON" };
        openItem.Click += ImportButton_Click;

        var saveItem = new ToolStripButton("Save") { ToolTipText = "Save graph to JSON" };
        saveItem.Click += ExportButton_Click;

        var exitItem = new ToolStripButton("Exit") { ToolTipText = "Exit application" };
        exitItem.Click += (s, e) => Close();

        fileDrop.DropDownItems.Add(exportAdjBtn);
        fileDrop.DropDownItems.Add(_exportGraphCsvBtn);
        fileDrop.DropDownItems.Add(_exportMatrixCsvBtn);
        fileDrop.DropDownItems.Add(_exportMatrixPngBtn);
        fileDrop.DropDownItems.Add(exportPngBtn);
        fileDrop.DropDownItems.Add(importAdjBtn);
        fileDrop.DropDownItems.Add(_importGraphCsvBtn);
        fileDrop.DropDownItems.Add(_importMatrixCsvBtn);
        fileDrop.DropDownItems.Add(openItem);
        fileDrop.DropDownItems.Add(saveItem);
        fileDrop.DropDownItems.Add(new ToolStripSeparator());
        fileDrop.DropDownItems.Add(exitItem);

        // Edit group (alphabetized)
        _addEdgeBtn = new ToolStripButton("Add Edge") { CheckOnClick = true };
        _addEdgeBtn.Click += (s, e) => ToggleAddEdgeMode();

        _addMatrixVertexBtn = new ToolStripButton("Add Matrix Vertex") { Visible = false };
        _addMatrixVertexBtn.Click += (s, e) => { AddMatrixVertex(); };

        _addVertexBtn = new ToolStripButton("Add Vertex") { CheckOnClick = true };
        _addVertexBtn.Click += (s, e) => ToggleAddVertexMode();

        _applyMatrixBtn = new ToolStripButton("Apply Matrix") { Visible = false };
        _applyMatrixBtn.Click += (s, e) => { ApplyMatrixGridToModel(); _matrixDirty = false; ToggleMatrixApplyButtons(); };

        _discardMatrixBtn = new ToolStripButton("Discard Matrix") { Visible = false };
        _discardMatrixBtn.Click += (s, e) => { PopulateMatrixGrid(); _matrixDirty = false; ToggleMatrixApplyButtons(); };

        _freezeToggleButton = new ToolStripButton("Freeze layout") { CheckOnClick = true };
        _freezeToggleButton.CheckedChanged += FreezeToggle_CheckedChanged;

        var optimizeBtn = new ToolStripButton("Optimize Layout") { ToolTipText = "Optimize graph layout" };
        optimizeBtn.Click += (s, e) => { try { OptimizeLayout(); } catch { } };

        var reindexBtn = new ToolStripButton("Reindex Ordinals") { ToolTipText = "Reindex vertex and edge ordinals to be dense (1..N)" };
        reindexBtn.Click += (s, e) => { if (MessageBox.Show(this, "Reindex ordinals to a dense 1..N sequence and rename autogenerated labels?", "Reindex Ordinals", MessageBoxButtons.YesNo) == DialogResult.Yes) ReindexOrdinals(); };

        _removeMatrixVertexBtn = new ToolStripButton("Remove Matrix Vertex") { Visible = false };
        _removeMatrixVertexBtn.Click += (s, e) => { RemoveSelectedMatrixVertex(); };

        _settingsBtn = new ToolStripButton("Settings");
        _settingsBtn.Click += SettingsBtn_Click;

        var themeMenu = new ToolStripMenuItem("Theme") { ToolTipText = "Select theme" };
        var darkItem = new ToolStripMenuItem("Dark");
        darkItem.Click += (s, e) => ApplyDarkTheme();
        var lightItem = new ToolStripMenuItem("Light");
        lightItem.Click += (s, e) => ApplyLightTheme();
        themeMenu.DropDownItems.Add(darkItem);
        themeMenu.DropDownItems.Add(lightItem);

        editDrop.DropDownItems.Add(_addEdgeBtn);
        editDrop.DropDownItems.Add(_addMatrixVertexBtn);
        editDrop.DropDownItems.Add(_addVertexBtn);
        editDrop.DropDownItems.Add(_applyMatrixBtn);
        editDrop.DropDownItems.Add(_discardMatrixBtn);
        editDrop.DropDownItems.Add(_freezeToggleButton);
        editDrop.DropDownItems.Add(optimizeBtn);
        editDrop.DropDownItems.Add(reindexBtn);
        editDrop.DropDownItems.Add(_removeMatrixVertexBtn);
        editDrop.DropDownItems.Add(_settingsBtn);
        editDrop.DropDownItems.Add(themeMenu);

        try
        {
            fileDrop.DropDown.AutoSize = true;
            editDrop.DropDown.AutoSize = true;

            if (fileDrop.DropDown is ToolStripDropDownMenu fileMenu)
            {
                fileMenu.ShowImageMargin = false;
                fileMenu.ShowCheckMargin = false;
            }
            if (editDrop.DropDown is ToolStripDropDownMenu editMenu)
            {
                editMenu.ShowImageMargin = false;
                editMenu.ShowCheckMargin = false;
            }
            if (_presetsDropDown.DropDown is ToolStripDropDownMenu presetsMenu)
            {
                presetsMenu.ShowImageMargin = false;
                presetsMenu.ShowCheckMargin = false;
            }
        }
        catch { }

        _toolStrip.Items.Add(fileDrop);
        _toolStrip.Items.Add(editDrop);
        _toolStrip.Items.Add(_presetsDropDown);

        // give each toolbar item a little breathing room
        foreach (ToolStripItem it in _toolStrip.Items)
        {
            try
            {
                it.Padding = new Padding(8, 2, 8, 2);
                it.Margin = new Padding(3, 0, 3, 0);
            }
            catch { }
        }

        _statusLabel = new ToolStripLabel();
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(_statusLabel);
        Controls.Add(_toolStrip);

        // Standardized context menu
        _graphContextMenu = new ContextMenuStrip();
        var addVertexMenu = new ToolStripMenuItem("Add new vertex") { Name = "ctxAddVertex" };
        addVertexMenu.Click += GraphContext_AddVertex_Click;
        _graphContextMenu.Items.Add(addVertexMenu);

        var addEdgePairMenu = new ToolStripMenuItem("Add new edge") { Name = "ctxAddEdgePair" };
        addEdgePairMenu.Click += GraphContext_AddEdgeBetweenSelected_Click;
        _graphContextMenu.Items.Add(addEdgePairMenu);

        var addSelfEdgeMenu = new ToolStripMenuItem("Add self-edge") { Name = "ctxAddSelfEdge" };
        addSelfEdgeMenu.Click += GraphContext_AddSelfEdge_Click;
        _graphContextMenu.Items.Add(addSelfEdgeMenu);

        _graphContextMenu.Items.Add(new ToolStripSeparator());

        var renameVertexMenu = new ToolStripMenuItem("Rename vertex") { Name = "ctxRenameVertex" };
        renameVertexMenu.Click += VertexProperties_Click;
        _graphContextMenu.Items.Add(renameVertexMenu);

        var renameEdgeMenu = new ToolStripMenuItem("Rename edge") { Name = "ctxRenameEdge" };
        renameEdgeMenu.Click += EdgeProperties_Click;
        _graphContextMenu.Items.Add(renameEdgeMenu);

        var reverseEdgeMenu = new ToolStripMenuItem("Reverse edge direction") { Name = "ctxReverseEdge" };
        reverseEdgeMenu.Click += GraphContext_ReverseEdge_Click;
        _graphContextMenu.Items.Add(reverseEdgeMenu);

        _graphContextMenu.Items.Add(new ToolStripSeparator());

        var deleteMenu = new ToolStripMenuItem("Delete") { Name = "ctxDelete" };
        deleteMenu.Click += GraphContext_Delete_Click;
        _graphContextMenu.Items.Add(deleteMenu);

        var freezeVertexMenu = new ToolStripMenuItem("Freeze position") { Name = "ctxFreezeVertex" };
        freezeVertexMenu.Click += VertexFreeze_Click;
        _graphContextMenu.Items.Add(freezeVertexMenu);

        _viewer.MouseClick += Viewer_MouseClick;
        _viewer.MouseDown += Viewer_MouseDown;
        _viewer.MouseMove += Viewer_MouseMove;
        _viewer.MouseUp += Viewer_MouseUp;
        _viewer.MouseEnter += Viewer_MouseEnter;
        _viewer.MouseLeave += Viewer_MouseLeave;
        _viewer.MouseMove += Viewer_MouseHoverMove;
        _hoverToolTip = new ToolTip();
        // status strip at bottom
        _statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        _bottomStatusLabel = new ToolStripStatusLabel();
        _statusStrip.Items.Add(_bottomStatusLabel);
        Controls.Add(_statusStrip);
        KeyPreview = true;
        KeyDown += MainForm_KeyDown;

        // load and apply saved settings
        _settings = Settings.Load();
        if (_settings != null)
        {
            if (string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase))
                ApplyLightTheme();
            else
                ApplyDarkTheme();
        }

        ReloadPresets();
    }

    private void AddMatrixVertex()
    {
        try
        {
            // append a new vertex with autogenerated label V{n}
            int nextOrd = (_model?.Vertices.Count ?? 0) + 1;
            var v = new CoreVertex($"V{nextOrd}");
            // add to model immediately so PopulateMatrixGrid can read it
            _model?.AddVertex(v);
            PopulateMatrixGrid();
            _matrixDirty = true;
            ToggleMatrixApplyButtons();
        }
        catch (Exception ex) { MessageBox.Show(this, "Failed to add vertex: " + ex.Message); }
    }

    private enum PresetSource
    {
        BuiltIn,
        File
    }

    private sealed class PresetItem
    {
        public required string Name { get; init; }
        public required PresetSource Source { get; init; }
        public string? FilePath { get; init; }
        public Func<DirectedGraph>? Factory { get; init; }
        public GraphLayoutMode LayoutMode { get; init; } = GraphLayoutMode.AutoDefault;
    }

    private static string GetPresetsFolderPath()
    {
        return System.IO.Path.Combine(AppContext.BaseDirectory, "presets");
    }

    private void ReloadPresets()
    {
        _presetItems.Clear();

        // Built-in presets
        _presetItems.Add(new PresetItem
        {
            Name = "Single Vertex",
            Source = PresetSource.BuiltIn,
            Factory = () =>
            {
                var g = new DirectedGraph();
                g.AddVertex(new CoreVertex("A"));
                return g;
            }
        });

        _presetItems.Add(new PresetItem
        {
            Name = "Diatonic Major (Roman Numerals)",
            Source = PresetSource.BuiltIn,
            Factory = BuildDiatonicMajorModelPreset,
            LayoutMode = GraphLayoutMode.AutoMds
        });

        _presetItems.Add(new PresetItem
        {
            Name = "Diatonic Minor (Roman Numerals)",
            Source = PresetSource.BuiltIn,
            Factory = BuildDiatonicMinorModelPreset,
            LayoutMode = GraphLayoutMode.AutoMds
        });

        _presetItems.Add(new PresetItem
        {
            Name = "48 Chord States (maj/min/7/m7) Sparse",
            Source = PresetSource.BuiltIn,
            Factory = BuildChordState48SparsePreset,
            LayoutMode = GraphLayoutMode.FixedChordState48
        });

        _presetItems.Add(new PresetItem
        {
            Name = "Rectangular Lattice (4x3)",
            Source = PresetSource.BuiltIn,
            Factory = () => BuildRectangularLatticePreset(4, 3),
            LayoutMode = GraphLayoutMode.FixedRectangularLattice
        });

        _presetItems.Add(new PresetItem
        {
            Name = "N-Gonal Lattice (12)",
            Source = PresetSource.BuiltIn,
            Factory = () => BuildNgonalLatticePreset(12),
            LayoutMode = GraphLayoutMode.FixedNgonalLattice
        });

        _presetItems.Add(new PresetItem
        {
            Name = "Two Vertices (A→B)",
            Source = PresetSource.BuiltIn,
            Factory = () =>
            {
                var g = new DirectedGraph();
                var a = new CoreVertex("A");
                var b = new CoreVertex("B");
                g.AddVertex(a);
                g.AddVertex(b);
                g.AddEdge(new CoreEdge(a, b));
                return g;
            }
        });

        _presetItems.Add(new PresetItem
        {
            Name = "3-Cycle (A→B→C→A)",
            Source = PresetSource.BuiltIn,
            Factory = () =>
            {
                var g = new DirectedGraph();
                var a = new CoreVertex("A");
                var b = new CoreVertex("B");
                var c = new CoreVertex("C");
                g.AddVertex(a);
                g.AddVertex(b);
                g.AddVertex(c);
                g.AddEdge(new CoreEdge(a, b));
                g.AddEdge(new CoreEdge(b, c));
                g.AddEdge(new CoreEdge(c, a));
                return g;
            }
        });

        _presetItems.Add(new PresetItem
        {
            Name = "12-Node Fifths (Proof of Concept)",
            Source = PresetSource.BuiltIn,
            LayoutMode = GraphLayoutMode.AutoMdsAlignedFifths,
            Factory = () =>
            {
                var g = new DirectedGraph();
                var labels = new[] { "C", "G", "D", "A", "E", "B", "F#", "C#", "G#", "D#", "A#", "F" };
                var verts = new System.Collections.Generic.List<CoreVertex>(labels.Length);

                foreach (var label in labels)
                {
                    var v = new CoreVertex(label);
                    g.AddVertex(v);
                    verts.Add(v);
                }

                // clockwise circle of fifths edges
                for (int i = 0; i < verts.Count; i++)
                {
                    var source = verts[i];
                    var target = verts[(i + 1) % verts.Count];
                    g.AddEdge(new CoreEdge(source, target) { Data = "structural" });
                }

                return g;
            }
        });

        // File-based presets
        var folder = GetPresetsFolderPath();
        try
        {
            if (System.IO.Directory.Exists(folder))
            {
                var files = System.IO.Directory
                    .EnumerateFiles(folder, "*.json", System.IO.SearchOption.TopDirectoryOnly)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

                foreach (var path in files)
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);
                    _presetItems.Add(new PresetItem
                    {
                        Name = name,
                        Source = PresetSource.File,
                        FilePath = path
                    });
                }
            }
        }
        catch
        {
            // ignore folder scan issues and keep built-in presets available
        }

        RebuildPresetsMenu();
    }

    private static DirectedGraph BuildRectangularLatticePreset(int columns, int rows)
    {
        var g = new DirectedGraph();
        if (columns < 1) columns = 1;
        if (rows < 1) rows = 1;

        var verts = new CoreVertex[rows, columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var v = new CoreVertex($"V{r + 1},{c + 1}");
                g.AddVertex(v);
                verts[r, c] = v;
            }
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (c + 1 < columns)
                    g.AddEdge(new CoreEdge(verts[r, c], verts[r, c + 1]) { Data = "structural" });
                if (r + 1 < rows)
                    g.AddEdge(new CoreEdge(verts[r, c], verts[r + 1, c]) { Data = "structural" });
            }
        }

        return g;
    }

    private static DirectedGraph BuildNgonalLatticePreset(int n)
    {
        var g = new DirectedGraph();
        n = Math.Max(3, n);

        var verts = new System.Collections.Generic.List<CoreVertex>(n);
        for (int i = 0; i < n; i++)
        {
            var v = new CoreVertex($"V{i + 1}");
            g.AddVertex(v);
            verts.Add(v);
        }

        for (int i = 0; i < n; i++)
            g.AddEdge(new CoreEdge(verts[i], verts[(i + 1) % n]) { Data = "structural" });

        return g;
    }

    private static DirectedGraph BuildDiatonicMajorModelPreset()
    {
        var g = new DirectedGraph();
        var order = new[] { "I", "ii", "iii", "IV", "V", "vi", "vii°" };
        var byLabel = new System.Collections.Generic.Dictionary<string, CoreVertex>(StringComparer.OrdinalIgnoreCase);

        foreach (var label in order)
        {
            var v = new CoreVertex(label);
            g.AddVertex(v);
            byLabel[label] = v;
        }

        void Add(string src, string dst)
        {
            g.AddEdge(new CoreEdge(byLabel[src], byLabel[dst]) { Data = "progression" });
        }

        Add("I", "ii");
        Add("I", "iii");
        Add("I", "IV");
        Add("I", "V");
        Add("I", "vi");

        Add("ii", "V");
        Add("ii", "vii°");

        Add("iii", "vi");
        Add("iii", "IV");

        Add("IV", "V");
        Add("IV", "vii°");

        Add("V", "I");
        Add("V", "vi");

        Add("vi", "ii");
        Add("vi", "IV");
        Add("vi", "V");

        Add("vii°", "I");
        Add("vii°", "iii");

        return g;
    }

    private static DirectedGraph BuildDiatonicMinorModelPreset()
    {
        var g = new DirectedGraph();
        var order = new[] { "i", "ii°", "III", "iv", "V", "VI", "vii°" };
        var byLabel = new System.Collections.Generic.Dictionary<string, CoreVertex>(StringComparer.OrdinalIgnoreCase);

        foreach (var label in order)
        {
            var v = new CoreVertex(label);
            g.AddVertex(v);
            byLabel[label] = v;
        }

        void Add(string src, string dst)
        {
            g.AddEdge(new CoreEdge(byLabel[src], byLabel[dst]) { Data = "progression" });
        }

        Add("i", "iv");
        Add("i", "ii°");
        Add("i", "VI");
        Add("i", "V");

        Add("ii°", "V");
        Add("ii°", "vii°");

        Add("III", "VI");
        Add("III", "iv");

        Add("iv", "V");
        Add("iv", "vii°");

        Add("V", "i");
        Add("V", "VI");

        Add("VI", "ii°");
        Add("VI", "iv");
        Add("VI", "V");

        Add("vii°", "i");
        Add("vii°", "III");

        return g;
    }

    private static DirectedGraph BuildChordState48SparsePreset()
    {
        var g = new DirectedGraph();
        var roots = new[] { "C", "G", "D", "A", "E", "B", "F#", "C#", "G#", "D#", "A#", "F" };

        var majorByRoot = new System.Collections.Generic.Dictionary<string, CoreVertex>(StringComparer.OrdinalIgnoreCase);
        var minorByRoot = new System.Collections.Generic.Dictionary<string, CoreVertex>(StringComparer.OrdinalIgnoreCase);
        var dom7ByRoot = new System.Collections.Generic.Dictionary<string, CoreVertex>(StringComparer.OrdinalIgnoreCase);
        var min7ByRoot = new System.Collections.Generic.Dictionary<string, CoreVertex>(StringComparer.OrdinalIgnoreCase);

        CoreVertex Add(string label, string color)
        {
            var v = new CoreVertex(label) { Color = color };
            g.AddVertex(v);
            return v;
        }

        const string majColor = "#4C78A8";
        const string minColor = "#59A14F";
        const string dom7Color = "#F28E2B";
        const string min7Color = "#B07AA1";

        foreach (var r in roots)
        {
            majorByRoot[r] = Add(r, majColor);
            minorByRoot[r] = Add(r + "m", minColor);
            dom7ByRoot[r] = Add(r + "7", dom7Color);
            min7ByRoot[r] = Add(r + "m7", min7Color);
        }

        void AddStructuralFifths(System.Collections.Generic.Dictionary<string, CoreVertex> map)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                var src = map[roots[i]];
                var dst = map[roots[(i + 1) % roots.Length]];
                g.AddEdge(new CoreEdge(src, dst) { Data = "structural" });
            }
        }

        AddStructuralFifths(majorByRoot);
        AddStructuralFifths(minorByRoot);

        foreach (var r in roots)
        {
            g.AddEdge(new CoreEdge(majorByRoot[r], dom7ByRoot[r]) { Data = "quality" });
            g.AddEdge(new CoreEdge(minorByRoot[r], min7ByRoot[r]) { Data = "quality" });
        }

        for (int i = 0; i < roots.Length; i++)
        {
            var src = dom7ByRoot[roots[i]];
            var targetRoot = roots[(i + 1) % roots.Length];
            var dst = majorByRoot[targetRoot];
            g.AddEdge(new CoreEdge(src, dst) { Data = "progression" });
        }

        foreach (var r in roots)
        {
            g.AddEdge(new CoreEdge(min7ByRoot[r], dom7ByRoot[r]) { Data = "progression" });
        }

        return g;
    }

    private void RebuildPresetsMenu()
    {
        if (_presetsDropDown == null) return;

        _presetsDropDown.DropDownItems.Clear();

        var builtIns = _presetItems.Where(p => p.Source == PresetSource.BuiltIn).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var files = _presetItems.Where(p => p.Source == PresetSource.File).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (builtIns.Count > 0)
        {
            foreach (var item in builtIns)
            {
                var mi = new ToolStripMenuItem(item.Name);
                mi.Click += (s, e) => ApplyPreset(item);
                _presetsDropDown.DropDownItems.Add(mi);
            }
        }

        if (files.Count > 0)
        {
            if (_presetsDropDown.DropDownItems.Count > 0)
                _presetsDropDown.DropDownItems.Add(new ToolStripSeparator());

            foreach (var item in files)
            {
                var mi = new ToolStripMenuItem($"[File] {item.Name}");
                mi.Click += (s, e) => ApplyPreset(item);
                _presetsDropDown.DropDownItems.Add(mi);
            }
        }

        if (_presetsDropDown.DropDownItems.Count > 0)
            _presetsDropDown.DropDownItems.Add(new ToolStripSeparator());

        var reloadItem = new ToolStripMenuItem("Reload Presets");
        reloadItem.Click += (s, e) => ReloadPresets();
        _presetsDropDown.DropDownItems.Add(reloadItem);

        var folder = GetPresetsFolderPath();
        var openFolderItem = new ToolStripMenuItem("Open Presets Folder");
        openFolderItem.Click += (s, e) =>
        {
            try
            {
                if (!System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open presets folder: " + ex.Message, "Presets", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        _presetsDropDown.DropDownItems.Add(openFolderItem);
    }

    private void ApplyPreset(PresetItem preset)
    {
        if (preset == null) return;

        var confirm = MessageBox.Show(
            this,
            $"Replace current graph with preset '{preset.Name}'?",
            "Load Preset",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            var prevModel = _model;
            var prevPositions = new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>(_positions);
            var prevFrozen = new System.Collections.Generic.HashSet<Guid>(_frozenNodes);

            DirectedGraph newGraph;
            var newPositions = new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>();
            var newFrozen = new System.Collections.Generic.HashSet<Guid>();

            if (preset.Source == PresetSource.File)
            {
                if (string.IsNullOrWhiteSpace(preset.FilePath) || !System.IO.File.Exists(preset.FilePath))
                {
                    MessageBox.Show(this, "Preset file not found.", "Load Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var loaded = DirectedGraph.LoadFromFileWithLayout(preset.FilePath);
                newGraph = loaded.Graph;
                if (loaded.Layout?.Positions != null)
                {
                    foreach (var kv in loaded.Layout.Positions)
                        newPositions[kv.Key] = new Microsoft.Msagl.Core.Geometry.Point(kv.Value.X, kv.Value.Y);
                }
                if (loaded.Layout?.FrozenIds != null)
                {
                    foreach (var id in loaded.Layout.FrozenIds)
                        newFrozen.Add(id);
                }
            }
            else
            {
                if (preset.Factory == null)
                {
                    MessageBox.Show(this, "Preset is not configured correctly.", "Load Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                newGraph = preset.Factory();
            }

            _currentLayoutMode = preset.LayoutMode;

            _model = newGraph;
            _positions.Clear();
            foreach (var kv in newPositions) _positions[kv.Key] = kv.Value;
            _frozenNodes.Clear();
            foreach (var id in newFrozen) _frozenNodes.Add(id);
            _globalFreeze = _frozenNodes.Count > 0;

            PushUndo(new ReplaceGraphAction(prevModel, prevPositions, prevFrozen, newGraph, newPositions, newFrozen));
            ClearSelection();
            RenderGraph(_model);
            if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Loaded preset: {preset.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Failed to load preset: " + ex.Message, "Load Preset", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReindexOrdinals()
    {
        if (_model == null) return;
        try
        {
            var prevModel = _model;
            var prevPositions = new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>(_positions);
            var prevFrozen = new System.Collections.Generic.HashSet<Guid>(_frozenNodes);

            var ordered = _model.Vertices.OrderBy(v => v.Ordinal).ThenBy(v => v.Label).ToList();
            int n = ordered.Count;
            var idToNewOrdinal = new System.Collections.Generic.Dictionary<Guid, int>();
            for (int i = 0; i < n; i++) idToNewOrdinal[ordered[i].Id] = i + 1;

            var autoRegex = new Regex("^V\\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            foreach (var v in ordered)
            {
                v.Ordinal = idToNewOrdinal[v.Id];
                if (!string.IsNullOrEmpty(v.Label) && autoRegex.IsMatch(v.Label))
                    v.Label = $"V{v.Ordinal}";
            }

            var edgesOrdered = _model.Edges.OrderBy(e => e.Ordinal).ThenBy(e => e.Label).ToList();
            for (int i = 0; i < edgesOrdered.Count; i++) edgesOrdered[i].Ordinal = i + 1;

            PushUndo(new ReplaceGraphAction(
                prevModel,
                prevPositions,
                prevFrozen,
                _model,
                new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>(_positions),
                new System.Collections.Generic.HashSet<Guid>(_frozenNodes)));

            RenderGraph(_model);
            if (_bottomStatusLabel != null) _bottomStatusLabel.Text = "Reindexed ordinals";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Reindex failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemoveSelectedMatrixVertex()
    {
        try
        {
            if (_matrixGrid.CurrentRow == null) return;
            var idCell = _matrixGrid.CurrentRow.Cells[0].Value?.ToString();
            if (string.IsNullOrEmpty(idCell) || !Guid.TryParse(idCell, out var gid))
            {
                MessageBox.Show(this, "Cannot determine vertex id to remove.", "Remove vertex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // remove from model
            var (v, removedEdges) = _model?.RemoveVertex(gid) ?? (null, new System.Collections.Generic.List<CoreEdge>());
            PopulateMatrixGrid();
            _matrixDirty = true;
            ToggleMatrixApplyButtons();
        }
        catch (Exception ex) { MessageBox.Show(this, "Failed to remove vertex: " + ex.Message); }
    }

    private Bitmap CreateIcon(Color bg, string text)
    {
        // generate a sharper 24x24 icon with rounded background and centered glyph
        const int size = 24;
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            // rounded rectangle background with subtle gradient
            var rect = new RectangleF(0.5f, 0.5f, size - 1, size - 1);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            float r = 5f;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();

            using var lg = new System.Drawing.Drawing2D.LinearGradientBrush(rect, ControlPaint.Light(bg), ControlPaint.Dark(bg), 90f);
            using var pen = new Pen(ControlPaint.Dark(bg)) { Width = 1f };
            g.FillPath(lg, path);
            g.DrawPath(pen, path);

            // draw glyph centered
            using var f = new Font(SystemFonts.DefaultFont.FontFamily, 11f, FontStyle.Bold, GraphicsUnit.Pixel);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var glyphRect = new RectangleF(0, 0, size, size);
            using var brush = new SolidBrush(Color.FromArgb(230, Color.White));
            g.DrawString(text, f, brush, glyphRect, sf);
        }

        return bmp;
    }

    private void Viewer_MouseEnter(object? sender, EventArgs e)
    {
        UpdateStatusLabel();
    }

    private void PopulateMatrixGrid()
    {
        if (_model == null) return;

        var (matrix, labels) = _model.ToAdjacencyMatrix();
        int n = matrix.GetLength(0);

        _matrixGrid.SuspendLayout();
        _matrixGrid.Columns.Clear();
        _matrixGrid.Rows.Clear();
        _matrixGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _matrixGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _matrixGrid.RowHeadersVisible = false;

        // hidden GUID column
        var idCol = new DataGridViewTextBoxColumn();
        idCol.Name = "Id";
        idCol.Visible = false;
        _matrixGrid.Columns.Add(idCol);

        // label column (editable)
        var textCol = new DataGridViewTextBoxColumn();
        textCol.Name = "Vertex";
        textCol.ReadOnly = false;
        textCol.Width = 120;
        _matrixGrid.Columns.Add(textCol);

        // subsequent columns are checkboxes for adjacency
        for (int j = 0; j < n; j++)
        {
            var cb = new DataGridViewCheckBoxColumn();
            cb.Name = labels[j];
            cb.Width = 40;
            cb.HeaderText = labels[j];
            _matrixGrid.Columns.Add(cb);
        }

        // determine vertices in the same order used by ToAdjacencyMatrix (by Ordinal then label)
        var orderedVerts = _model.Vertices.OrderBy(v => v.Ordinal).ThenBy(v => v.Label).ToList();
        for (int i = 0; i < n; i++)
        {
            var values = new object[n + 2];
            // hidden guid column
            values[0] = orderedVerts[i].Id.ToString();
            values[1] = labels[i];
            for (int j = 0; j < n; j++) values[j + 2] = matrix[i, j];
            _matrixGrid.Rows.Add(values);
        }

        _matrixGrid.ResumeLayout();
        // clear dirty flag after loading
        _matrixDirty = false;
        ToggleMatrixApplyButtons();
    }

    private void ToggleMatrixApplyButtons()
    {
        try
        {
            var isMatrixTab = _mainTab.SelectedTab != null && _mainTab.SelectedTab.Text == "Adjacency Matrix";
            var isGraphTab = _mainTab.SelectedTab != null && _mainTab.SelectedTab.Text == "Graph";
            if (_applyMatrixBtn != null) _applyMatrixBtn.Visible = _matrixDirty && _mainTab.SelectedTab != null && _mainTab.SelectedTab.Text == "Adjacency Matrix";
            if (_discardMatrixBtn != null) _discardMatrixBtn.Visible = _matrixDirty && _mainTab.SelectedTab != null && _mainTab.SelectedTab.Text == "Adjacency Matrix";
            if (_addMatrixVertexBtn != null) _addMatrixVertexBtn.Visible = isMatrixTab;
            if (_removeMatrixVertexBtn != null) _removeMatrixVertexBtn.Visible = isMatrixTab;
            if (_importMatrixCsvBtn != null) _importMatrixCsvBtn.Visible = isMatrixTab;
            if (_exportMatrixCsvBtn != null) _exportMatrixCsvBtn.Visible = isMatrixTab;
            if (_exportMatrixPngBtn != null) _exportMatrixPngBtn.Visible = isMatrixTab;
            if (_importGraphCsvBtn != null) _importGraphCsvBtn.Visible = isGraphTab;
            if (_exportGraphCsvBtn != null) _exportGraphCsvBtn.Visible = isGraphTab;
        }
        catch { }
    }

    private void ApplyMatrixGridToModel()
    {
        if (_model == null) return;
        try
        {
            int n = Math.Max(0, _matrixGrid.RowCount);
            if (n == 0) return;

            // validate square grid (columns include hidden Id and label columns)
            if (_matrixGrid.Columns.Count - 2 != n)
            {
                MessageBox.Show(this, "Matrix must be square: number of rows must equal number of columns.", "Invalid matrix", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // read labels from first column and validate uniqueness
            var labels = new string[n];
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < n; i++)
            {
                // Id is column 0 (hidden), label is column 1
                var lbl = _matrixGrid.Rows[i].Cells[1].Value?.ToString()?.Trim();
                if (string.IsNullOrEmpty(lbl)) lbl = $"V{i + 1}";
                lbl = NormalizeChordLabelSymbols(lbl);
                if (seen.Contains(lbl))
                {
                    MessageBox.Show(this, $"Duplicate vertex label '{lbl}' found. Labels must be unique.", "Duplicate labels", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                seen.Add(lbl);
                labels[i] = lbl!;
            }

            // read boolean matrix
            var matrix = new bool[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    var cell = _matrixGrid.Rows[i].Cells[j + 2].Value; // offset by 2: Id + Label
                    bool val = false;
                    if (cell is bool b) val = b;
                    else if (cell is int ii) val = ii != 0;
                    else if (cell is string s && int.TryParse(s, out var x)) val = x != 0;
                    matrix[i, j] = val;
                }
            }

            // mapping: try to reuse existing vertices by label (case-insensitive)
            var oldGraph = _model;
            var labelToVertex = oldGraph.Vertices.ToDictionary(v => v.Label ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            var mappedVertices = new System.Collections.Generic.List<CoreVertex>(n);
            var reusedIds = new System.Collections.Generic.HashSet<Guid>();
            int addedVertices = 0;
            int removedVertices = 0;

            for (int i = 0; i < n; i++)
            {
                var lbl = labels[i];
                // attempt to map by hidden GUID first
                var idCell = _matrixGrid.Rows[i].Cells[0].Value?.ToString();
                if (!string.IsNullOrEmpty(idCell) && Guid.TryParse(idCell, out var parsedId) && oldGraph.TryGetVertex(parsedId, out var byId))
                {
                    mappedVertices.Add(byId);
                    reusedIds.Add(byId.Id);
                }
                else if (labelToVertex.TryGetValue(lbl, out var existing))
                {
                    mappedVertices.Add(existing);
                    reusedIds.Add(existing.Id);
                }
                else
                {
                    var nv = new CoreVertex(lbl);
                    mappedVertices.Add(nv);
                    addedVertices++;
                }
            }

            // any old vertices not reused are removed
            removedVertices = oldGraph.Vertices.Count - reusedIds.Count;

            // build new graph, reusing vertex instances where available
            var newGraph = new DirectedGraph();
            foreach (var v in mappedVertices)
            {
                // if vertex already exists in oldGraph, reuse that instance to preserve Id/color
                if (oldGraph.TryGetVertex(v.Id, out var _) && labelToVertex.ContainsKey(v.Label))
                {
                    // get the original vertex instance by label
                    var orig = labelToVertex[v.Label];
                    newGraph.AddVertex(orig);
                }
                else
                {
                    newGraph.AddVertex(v);
                }
            }

            // build mapping from old edge pairs to edge for attribute preservation
            var oldEdgeMap = new System.Collections.Generic.Dictionary<(Guid s, Guid t), CoreEdge>();
            foreach (var e in oldGraph.Edges) oldEdgeMap[(e.Source.Id, e.Target.Id)] = e;

            int addedEdges = 0;
            int removedEdges = 0;

            // add edges according to matrix
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (!matrix[i, j]) continue;
                    var src = newGraph.Vertices.ElementAt(i);
                    var tgt = newGraph.Vertices.ElementAt(j);
                    // see if old graph had this edge between the corresponding original ids
                    CoreEdge? oldEdge = null;
                    if (labelToVertex.TryGetValue(labels[i], out var origSrc) && labelToVertex.TryGetValue(labels[j], out var origTgt))
                    {
                        oldEdgeMap.TryGetValue((origSrc.Id, origTgt.Id), out CoreEdge foundEdge);
                        oldEdge = foundEdge;
                    }

                    if (oldEdge != null)
                    {
                        var e = new CoreEdge(oldEdge.Id, src, tgt, oldEdge.Label);
                        e.Color = oldEdge.Color;
                        e.Ordinal = oldEdge.Ordinal;
                        newGraph.AddEdge(e);
                    }
                    else
                    {
                        var e = new CoreEdge(src, tgt, string.Empty);
                        newGraph.AddEdge(e);
                        addedEdges++;
                    }
                }
            }

            // compute removedEdges as old edges not present in new
            var newEdgePairs = new System.Collections.Generic.HashSet<(Guid s, Guid t)>();
            foreach (var e in newGraph.Edges) newEdgePairs.Add((e.Source.Id, e.Target.Id));
            foreach (var e in oldGraph.Edges)
            {
                if (!newEdgePairs.Contains((e.Source.Id, e.Target.Id))) removedEdges++;
            }

            // capture positions and frozen nodes for reused vertices
            var newPositions = new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>();
            var newFrozen = new System.Collections.Generic.HashSet<Guid>();
            foreach (var v in newGraph.Vertices)
            {
                if (_positions.TryGetValue(v.Id, out var p)) newPositions[v.Id] = p;
                if (_frozenNodes.Contains(v.Id)) newFrozen.Add(v.Id);
            }

            // prepare undo action: replace graph
            var prevModel = _model;
            var prevPositions = new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>(_positions);
            var prevFrozen = new System.Collections.Generic.HashSet<Guid>(_frozenNodes);

            // apply
            _model = newGraph;
            _positions.Clear();
            foreach (var kv in newPositions) _positions[kv.Key] = kv.Value;
            _frozenNodes.Clear();
            foreach (var id in newFrozen) _frozenNodes.Add(id);

            // push undo that restores previous model and positions
            PushUndo(new ReplaceGraphAction(prevModel, prevPositions, prevFrozen, newGraph, newPositions, newFrozen));

            RenderGraph(_model);
            if (_bottomStatusLabel != null)
                _bottomStatusLabel.Text = $"Applied matrix edits: +{addedVertices} vertices, -{removedVertices} vertices, +{addedEdges} edges, -{removedEdges} edges";
            UpdateStatusLabel();
            _matrixDirty = false;
            ToggleMatrixApplyButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Failed to apply matrix edits: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportMatrixCsv_Click(object? sender, EventArgs e)
    {
        if (_model == null) return;

        using var dlg = new SaveFileDialog
        {
            Filter = "CSV files|*.csv|All files|*.*",
            DefaultExt = "csv",
            FileName = "matrix.csv"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var (matrix, labels) = _model.ToAdjacencyMatrix();
            int n = matrix.GetLength(0);
            var lines = new System.Collections.Generic.List<string>(n + 1);

            var header = new System.Collections.Generic.List<string>(n + 1) { string.Empty };
            header.AddRange(labels.Select(EscapeCsv));
            lines.Add(string.Join(",", header));

            for (int i = 0; i < n; i++)
            {
                var row = new System.Collections.Generic.List<string>(n + 1) { EscapeCsv(labels[i]) };
                for (int j = 0; j < n; j++) row.Add(matrix[i, j] ? "1" : "0");
                lines.Add(string.Join(",", row));
            }

            System.IO.File.WriteAllLines(dlg.FileName, lines);
            if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Exported matrix CSV: {dlg.FileName}";
            MessageBox.Show(this, "Matrix CSV exported.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Matrix CSV export failed: " + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportMatrixCsv_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "CSV files|*.csv|All files|*.*"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var rawLines = System.IO.File.ReadAllLines(dlg.FileName)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            if (rawLines.Length < 2)
            {
                MessageBox.Show(this, "CSV must include a header row and at least one matrix row.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var headerCells = ParseCsvLine(rawLines[0]);
            if (headerCells.Count < 2)
            {
                MessageBox.Show(this, "CSV header must include row/column labels.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var labels = headerCells.Skip(1).Select(c => c.Trim()).ToArray();
            int n = labels.Length;
            if (n == 0)
            {
                MessageBox.Show(this, "CSV header has no vertex labels.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (rawLines.Length - 1 != n)
            {
                MessageBox.Show(this, "CSV must be square: row count must match column count.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var matrix = new bool[n, n];
            for (int i = 0; i < n; i++)
            {
                var cells = ParseCsvLine(rawLines[i + 1]);
                if (cells.Count != n + 1)
                {
                    MessageBox.Show(this, $"Row {i + 2} has {cells.Count} columns; expected {n + 1}.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                for (int j = 0; j < n; j++)
                {
                    var token = cells[j + 1].Trim();
                    if (token == "1") matrix[i, j] = true;
                    else if (token == "0" || token.Length == 0) matrix[i, j] = false;
                    else
                    {
                        MessageBox.Show(this, $"Invalid matrix value '{token}' at row {i + 2}, column {j + 2}. Use 0 or 1.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            var prevModel = _model;
            var prevPositions = new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>(_positions);
            var prevFrozen = new System.Collections.Generic.HashSet<Guid>(_frozenNodes);

            var newGraph = DirectedGraph.FromAdjacencyMatrix(matrix, labels);
            _model = newGraph;
            _positions.Clear();
            _frozenNodes.Clear();

            PushUndo(new ReplaceGraphAction(
                prevModel,
                prevPositions,
                prevFrozen,
                newGraph,
                new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>(_positions),
                new System.Collections.Generic.HashSet<Guid>(_frozenNodes)));

            PopulateMatrixGrid();
            RenderGraph(_model);
            _matrixDirty = false;
            ToggleMatrixApplyButtons();
            if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Imported matrix CSV: {dlg.FileName}";
            MessageBox.Show(this, "Matrix CSV imported.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Matrix CSV import failed: " + ex.Message, "Import", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string EscapeCsv(string? value)
    {
        var s = value ?? string.Empty;
        if (s.Contains('"')) s = s.Replace("\"", "\"\"");
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r')) return $"\"{s}\"";
        return s;
    }

    private static System.Collections.Generic.List<string> ParseCsvLine(string line)
    {
        var values = new System.Collections.Generic.List<string>();
        if (line == null)
        {
            values.Add(string.Empty);
            return values;
        }

        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private void ExportMatrixPng_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            DefaultExt = "png",
            FileName = "matrix.png"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var originalSize = _matrixGrid.Size;
            var originalScroll = _matrixGrid.ScrollBars;

            try
            {
                _matrixGrid.ScrollBars = ScrollBars.None;

                int width = _matrixGrid.RowHeadersVisible ? _matrixGrid.RowHeadersWidth : 0;
                foreach (DataGridViewColumn col in _matrixGrid.Columns)
                {
                    if (col.Visible) width += col.Width;
                }

                int height = _matrixGrid.ColumnHeadersVisible ? _matrixGrid.ColumnHeadersHeight : 0;
                foreach (DataGridViewRow row in _matrixGrid.Rows)
                {
                    if (row.Visible) height += row.Height;
                }

                width = Math.Max(width + 2, 1);
                height = Math.Max(height + 2, 1);

                _matrixGrid.Size = new Size(width, height);
                _matrixGrid.PerformLayout();

                using var bmp = new Bitmap(width, height);
                _matrixGrid.DrawToBitmap(bmp, new Rectangle(0, 0, width, height));
                bmp.Save(dlg.FileName, ImageFormat.Png);
            }
            finally
            {
                _matrixGrid.Size = originalSize;
                _matrixGrid.ScrollBars = originalScroll;
            }

            if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Exported matrix PNG: {dlg.FileName}";
            MessageBox.Show(this, "Matrix PNG exported.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Matrix PNG export failed: " + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void MainTab_SelectedIndexChanged(object? sender, EventArgs e)
    {
        try
        {
            if (_suppressTabChange) return;

            if (_mainTab.SelectedTab != null && _mainTab.SelectedTab.Text == "Adjacency Matrix")
            {
                // switching to matrix view: populate grid from model
                PopulateMatrixGrid();
                _matrixDirty = false;
                ToggleMatrixApplyButtons();
            }
            else if (_mainTab.SelectedTab != null && _mainTab.SelectedTab.Text == "Graph")
            {
                // switching back to graph view: auto-commit pending cell edits then auto-apply changes
                try
                {
                    // ensure any active cell edit is committed
                    _matrixGrid.EndEdit();
                    _matrixGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
                catch { }

                if (_matrixDirty)
                {
                    // automatically apply changes without prompting for a smoother UX
                    ApplyMatrixGridToModel();
                    _matrixDirty = false;
                    ToggleMatrixApplyButtons();
                }

                RenderGraph(_model!);
            }
        }
        catch { }
    }

    private void UpdateStatusLabel()
    {
        try
        {
            var mode = _addVertexMode ? "Add Vertex" : _addEdgeMode ? "Add Edge" : "Navigate";
            var autos = _settings?.AutoScaleNodeLabels == true ? "Autoscale:On" : "Autoscale:Off";
            if (_statusLabel != null)
                _statusLabel.Text = $"Mode: {mode} | {autos}";
            if (_bottomStatusLabel != null)
                _bottomStatusLabel.Text = $"Mode: {mode} | {autos}";
        }
        catch { }
    }

    private void Viewer_MouseLeave(object? sender, EventArgs e)
    {
        _hoverToolTip?.Hide(_viewer);
        _lastHoverVertexId = null;
        _lastHoverEdgeId = null;
    }

    private void Viewer_MouseHoverMove(object? sender, MouseEventArgs e)
    {
        // show tooltip for truncated labels when hovering
        try
        {
            var obj = _viewer.ObjectUnderMouseCursor;
            if (obj == null)
            {
                _hoverToolTip?.Hide(_viewer);
                _lastHoverVertexId = null;
                _lastHoverEdgeId = null;
                return;
            }

            if (TryResolveModelIdsFromViewerObject(obj, out var vId, out var eId))
            {
                if (vId.HasValue && vId != _lastHoverVertexId && _model != null && _model.TryGetVertex(vId.Value, out var v))
                {
                    _lastHoverVertexId = vId;
                    _lastHoverEdgeId = null;
                    var label = v.Label ?? string.Empty;
                    if (label.Length > (_settings?.MaxLabelChars ?? 30))
                    {
                        _hoverToolTip?.Show(label, _viewer, e.Location.X + 15, e.Location.Y + 15, 3000);
                    }
                    else
                    {
                        _hoverToolTip?.Hide(_viewer);
                    }
                }
                else if (eId.HasValue && eId != _lastHoverEdgeId && _model != null && _model.TryGetEdge(eId.Value, out var edge))
                {
                    _lastHoverEdgeId = eId;
                    _lastHoverVertexId = null;
                    var label = edge.Label ?? string.Empty;
                    if (label.Length > (_settings?.MaxLabelChars ?? 30))
                    {
                        _hoverToolTip?.Show(label, _viewer, e.Location.X + 15, e.Location.Y + 15, 3000);
                    }
                    else
                    {
                        _hoverToolTip?.Hide(_viewer);
                    }
                }
            }
        }
        catch { }
    }

    private void ExportAdjacency_Click(object? sender, EventArgs e)
    {
        if (_model == null) return;
        using var dlg = new SaveFileDialog { Filter = "Adjacency JSON|*.adj.json|All files|*.*", DefaultExt = "adj.json", FileName = "graph.adj.json" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var json = _model.ToAdjacencyJson();
                System.IO.File.WriteAllText(dlg.FileName, json);
                MessageBox.Show(this, "Adjacency exported.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                try { if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Exported adjacency: {dlg.FileName}"; } catch { }
                UpdateStatusLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportViewerPng_Click(object? sender, EventArgs e)
    {
        if (_viewer == null) return;

        using var dlg = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            DefaultExt = "png",
            FileName = "graph.png"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var bmp = new Bitmap(_viewer.Width, _viewer.Height);
            _viewer.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(dlg.FileName, ImageFormat.Png);

            try
            {
                var sidecar = System.IO.Path.ChangeExtension(dlg.FileName, ".json");
                var posDto = _positions.ToDictionary(
                    kv => kv.Key,
                    kv => new DirectedGraph.PositionDto { X = kv.Value.X, Y = kv.Value.Y });
                _model?.SaveToFile(sidecar, posDto, _frozenNodes);
            }
            catch { }

            if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Exported PNG: {dlg.FileName}";
            MessageBox.Show(this, "Export complete", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Export failed: " + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportGraphCsv_Click(object? sender, EventArgs e)
    {
        if (_model == null) return;

        using var dlg = new SaveFileDialog
        {
            Filter = "CSV files|*.csv|All files|*.*",
            DefaultExt = "csv",
            FileName = "graph-edges.csv"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var lines = new System.Collections.Generic.List<string>
            {
                "Source,Target,Label"
            };

            foreach (var edge in _model.Edges.OrderBy(e => e.Ordinal).ThenBy(e => e.Label))
            {
                lines.Add(string.Join(",", EscapeCsv(edge.Source.Label), EscapeCsv(edge.Target.Label), EscapeCsv(edge.Label)));
            }

            System.IO.File.WriteAllLines(dlg.FileName, lines);
            if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Exported graph CSV: {dlg.FileName}";
            MessageBox.Show(this, "Graph CSV exported.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Graph CSV export failed: " + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportGraphCsv_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "CSV files|*.csv|All files|*.*"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var rawLines = System.IO.File.ReadAllLines(dlg.FileName)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            if (rawLines.Length < 1)
            {
                MessageBox.Show(this, "CSV file is empty.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int startIndex = 0;
            var first = ParseCsvLine(rawLines[0]);
            if (first.Count >= 2 &&
                string.Equals(first[0].Trim(), "Source", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(first[1].Trim(), "Target", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
            }

            var prevModel = _model;
            var prevPositions = new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>(_positions);
            var prevFrozen = new System.Collections.Generic.HashSet<Guid>(_frozenNodes);

            var g = new DirectedGraph();
            var byLabel = new System.Collections.Generic.Dictionary<string, CoreVertex>(StringComparer.OrdinalIgnoreCase);

            CoreVertex GetOrCreateVertex(string? label)
            {
                var trimmed = (label ?? string.Empty).Trim();
                if (trimmed.Length == 0) trimmed = $"V{byLabel.Count + 1}";
                trimmed = NormalizeChordLabelSymbols(trimmed);
                if (byLabel.TryGetValue(trimmed, out var existing)) return existing;

                var v = new CoreVertex(trimmed);
                g.AddVertex(v);
                byLabel[trimmed] = v;
                return v;
            }

            for (int i = startIndex; i < rawLines.Length; i++)
            {
                var cells = ParseCsvLine(rawLines[i]);
                if (cells.Count < 2)
                {
                    MessageBox.Show(this, $"Row {i + 1} must contain at least Source and Target columns.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var source = GetOrCreateVertex(cells[0]);
                var target = GetOrCreateVertex(cells[1]);
                var label = cells.Count > 2 ? cells[2] : string.Empty;

                g.AddEdge(new CoreEdge(source, target, label ?? string.Empty));
            }

            _model = g;
            _positions.Clear();
            _frozenNodes.Clear();

            PushUndo(new ReplaceGraphAction(
                prevModel,
                prevPositions,
                prevFrozen,
                g,
                new System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point>(_positions),
                new System.Collections.Generic.HashSet<Guid>(_frozenNodes)));

            RenderGraph(_model);
            if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Imported graph CSV: {dlg.FileName}";
            MessageBox.Show(this, "Graph CSV imported.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Graph CSV import failed: " + ex.Message, "Import", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportAdjacency_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "Adjacency JSON|*.adj.json|JSON|*.json|All files|*.*" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var g = DiGraphLab.Core.DirectedGraph.LoadFromAdjacencyFile(dlg.FileName);
                _model = g;
                // clear selection/positions when loading a fresh graph
                try { ClearSelection(); } catch { }
                RenderGraph(_model);
                MessageBox.Show(this, "Adjacency imported.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                try { if (_bottomStatusLabel != null) _bottomStatusLabel.Text = $"Imported adjacency: {dlg.FileName}"; } catch { }
                UpdateStatusLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Import failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Toolbar mode toggles
    private void ToggleAddVertexMode()
    {
        _addVertexMode = !_addVertexMode;
        if (_addVertexBtn != null) _addVertexBtn.Checked = _addVertexMode;
        if (_addVertexMode)
        {
            _addEdgeMode = false;
            if (_addEdgeBtn != null) _addEdgeBtn.Checked = false;
            _pendingEdgeSourceId = null;
        }
        _viewer.Cursor = _addVertexMode ? Cursors.Cross : Cursors.Default;
        UpdateStatusLabel();
    }

    private void ToggleAddEdgeMode()
    {
        _addEdgeMode = !_addEdgeMode;
        if (_addEdgeBtn != null) _addEdgeBtn.Checked = _addEdgeMode;
        if (_addEdgeMode)
        {
            _addVertexMode = false;
            if (_addVertexBtn != null) _addVertexBtn.Checked = false;
        }
        else
        {
            _pendingEdgeSourceId = null;
        }
        _viewer.Cursor = _addEdgeMode ? Cursors.Cross : Cursors.Default;
        UpdateStatusLabel();
    }

    private void ApplyLightTheme()
    {
        // Light theme: set form background to white and re-render
        this.BackColor = System.Drawing.Color.White;
        if (_model != null)
            RenderGraph(_model);
    }

    private void ApplyDarkTheme()
    {
        // Dark theme: set form background to a dark gray and re-render
        this.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
        if (_model != null)
            RenderGraph(_model);
    }

    private void SettingsBtn_Click(object? sender, EventArgs e)
    {
        var settings = Settings.Load();
        using var dlg = new SettingsForm(settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            // update cached settings and re-apply theme if changed
            _settings = settings;
            if (_model != null)
                ConfigureColorProviders(_model);
            if (string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase))
                ApplyLightTheme();
            else
                ApplyDarkTheme();
        }
    }

    private void ConfigureColorProviders(DirectedGraph graph)
    {
        graph.VertexColorProvider = _ =>
        {
            if (!_settings.AssignDefaultColorToNew) return null;
            var bg = this.BackColor;
            var inv = System.Drawing.Color.FromArgb(255 - bg.R, 255 - bg.G, 255 - bg.B);
            return System.Drawing.ColorTranslator.ToHtml(inv);
        };

        graph.EdgeColorProvider = (_, _) =>
        {
            if (!_settings.AssignDefaultColorToNew) return null;
            var bg = this.BackColor;
            var inv = System.Drawing.Color.FromArgb(255 - bg.R, 255 - bg.G, 255 - bg.B);
            return System.Drawing.ColorTranslator.ToHtml(inv);
        };
    }

    private record MultiMoveVertexAction(System.Collections.Generic.List<(Guid id, Microsoft.Msagl.Core.Geometry.Point? oldPos, Microsoft.Msagl.Core.Geometry.Point newPos)> Moves) : IUndoableAction
    {
        public void Undo(MainForm f)
        {
            foreach (var m in Moves)
            {
                if (m.oldPos.HasValue)
                    f._positions[m.id] = m.oldPos.Value;
                else
                    f._positions.Remove(m.id);
            }
            f.RenderGraph(f._model!);
        }

        public void Redo(MainForm f)
        {
            foreach (var m in Moves)
                f._positions[m.id] = m.newPos;
            f.RenderGraph(f._model!);
        }
    }

    private void FreezeToggle_CheckedChanged(object? sender, EventArgs e)
    {
        if (_freezeToggleButton.Checked)
            FreezeAll();
        else
            UnfreezeAll();
    }

    private void FreezeAll()
    {
        if (_model == null) return;
        // capture current positions for all nodes and mark frozen
        foreach (var v in _model.Vertices)
        {
            var node = _viewer.Graph?.FindNode(v.Id.ToString());
            var pos = node?.GeometryNode?.Center;
            if (pos.HasValue)
                _positions[v.Id] = pos.Value;
            _frozenNodes.Add(v.Id);
        }
        _globalFreeze = true;
        RenderGraph(_model);
    }

    private void UnfreezeAll()
    {
        _frozenNodes.Clear();
        _globalFreeze = false;
        if (_model != null) RenderGraph(_model);
    }

    private Guid? _draggingVertexId;
    private Microsoft.Msagl.Core.Geometry.Point? _dragOriginalPosition;
    private bool _isDragging;
    private bool _draggingMultiple;
    private System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point> _dragOriginalPositions = new();

    // marquee selection
    private bool _isMarquee;
    private System.Drawing.Point _marqueeStart;
    private System.Drawing.Rectangle _marqueePrevScreenRect;

    public void RenderGraph(DirectedGraph graph)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));

        // store reference to model for interactive edits
        _model = graph;
        ConfigureColorProviders(_model);

        if (_currentLayoutMode == GraphLayoutMode.FixedTraditionalFifths)
        {
            ApplyTraditionalFifthsPositions(graph);
        }
        else if (_currentLayoutMode == GraphLayoutMode.FixedChordState48)
        {
            ApplyChordState48StandardPositions(graph);
        }
        else if (_currentLayoutMode == GraphLayoutMode.FixedRectangularLattice)
        {
            ApplyRectangularLatticePositions(graph);
        }
        else if (_currentLayoutMode == GraphLayoutMode.FixedNgonalLattice)
        {
            ApplyNgonalLatticePositions(graph);
        }

        var msagl = new Microsoft.Msagl.Drawing.Graph("graph") { Directed = true };
        if (_currentLayoutMode == GraphLayoutMode.AutoMds || _currentLayoutMode == GraphLayoutMode.AutoMdsAlignedFifths)
        {
            try
            {
                msagl.LayoutAlgorithmSettings = new MdsLayoutSettings();
            }
            catch { }
        }

        // compute default color as inverse of the form background so themes are visible
        var bg = this.BackColor;
        var defaultMsaglColor = bg.ToMsagl();
        defaultMsaglColor = new Microsoft.Msagl.Drawing.Color((byte)(255 - defaultMsaglColor.R), (byte)(255 - defaultMsaglColor.G), (byte)(255 - defaultMsaglColor.B));
        var defaultEdgeSys = GetDefaultContrastingEdgeColor(bg);
        var preferDarkLabel = GetPerceivedLuminance(bg) >= 140;

        // Add nodes
        foreach (var v in graph.Vertices)
        {
            var node = msagl.AddNode(v.Id.ToString());
            node.LabelText = v.Label ?? string.Empty;
            // determine fill/border color from model or use theme default
            Microsoft.Msagl.Drawing.Color nodeColor = defaultMsaglColor;
            if (!string.IsNullOrEmpty(v.Color))
            {
                try
                {
                    nodeColor = ColorExtensions.FromHtmlToMsagl(v.Color);
                }
                catch { }
            }
            var baseNodeSys = System.Drawing.Color.FromArgb(nodeColor.R, nodeColor.G, nodeColor.B);
            var labelSys = preferDarkLabel ? System.Drawing.Color.Black : System.Drawing.Color.White;
            var adjustedFillSys = EnsureFillContrastWithLabel(baseNodeSys, labelSys, 115);
            node.Attr.FillColor = new Microsoft.Msagl.Drawing.Color(adjustedFillSys.R, adjustedFillSys.G, adjustedFillSys.B);
            node.Attr.Color = nodeColor;
            if (node.Label != null)
            {
                node.Label.FontColor = new Microsoft.Msagl.Drawing.Color(labelSys.R, labelSys.G, labelSys.B);
            }
            // visually indicate frozen nodes and mark attribute for future use
            if (_frozenNodes.Contains(v.Id))
            {
                node.Attr.FillColor = Microsoft.Msagl.Drawing.Color.LightGray;
                node.Attr.LineWidth = 2;
            }
            // apply stored position if present
            if (_positions.TryGetValue(v.Id, out var pos))
            {
                try
                {
                    // try to set geometry center if available
                    var geom = node.GeometryNode;
                    if (geom != null)
                        geom.Center = pos;
                }
                catch { }
            }
            }

        // Add edges
        foreach (var e in graph.Edges)
        {
            var me = msagl.AddEdge(e.Source.Id.ToString(), e.Target.Id.ToString());
            if (!string.IsNullOrEmpty(e.Label))
                me.LabelText = e.Label;
            var isStructuralFifthsEdge =
                _currentLayoutMode == GraphLayoutMode.AutoMdsAlignedFifths &&
                string.Equals(e.Data as string, "structural", StringComparison.OrdinalIgnoreCase);
            var isStructuralContextEdge =
                string.Equals(e.Data as string, "structural", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Data as string, "quality", StringComparison.OrdinalIgnoreCase);

            // edge color from model or theme default
            var edgeColorSys = defaultEdgeSys;
            if (isStructuralFifthsEdge || isStructuralContextEdge)
            {
                edgeColorSys = GetMutedEdgeColor(bg);
            }
            else if (!string.IsNullOrEmpty(e.Color))
            {
                try
                {
                    var parsed = ColorExtensions.FromHtmlToMsagl(e.Color);
                    var parsedSys = System.Drawing.Color.FromArgb(parsed.R, parsed.G, parsed.B);
                    edgeColorSys = HasStrongContrast(parsedSys, bg) ? parsedSys : defaultEdgeSys;
                }
                catch { }
            }
            me.Attr.Color = new Microsoft.Msagl.Drawing.Color(edgeColorSys.R, edgeColorSys.G, edgeColorSys.B);
            me.Attr.LineWidth = (isStructuralFifthsEdge || isStructuralContextEdge) ? 1.0 : 1.8;
            try { me.Attr.ArrowheadLength = (isStructuralFifthsEdge || isStructuralContextEdge) ? 8 : 14; } catch { }
        }

        _viewer.Graph = msagl;

        // For the traditional fifths preset, force the fixed circular coordinates onto
        // geometry nodes after graph assignment so layout does not stretch into a long strip.
        if (_currentLayoutMode == GraphLayoutMode.FixedTraditionalFifths
            || _currentLayoutMode == GraphLayoutMode.FixedChordState48
            || _currentLayoutMode == GraphLayoutMode.FixedRectangularLattice
            || _currentLayoutMode == GraphLayoutMode.FixedNgonalLattice)
        {
            ApplyStoredPositionsToViewerGraph();
            _viewer.Invalidate();
            return;
        }

        // Normalize label sizes to avoid overly large labels relative to node boxes
        try
        {
            // Dynamic font sizing: scale label fonts based on viewer area and node count
            if (!_settings.AutoScaleNodeLabels)
            {
                // keep previous simple normalization when autoscale disabled
                foreach (var n in _viewer.Graph?.Nodes ?? System.Linq.Enumerable.Empty<Microsoft.Msagl.Drawing.Node>())
                {
                    try
                    {
                        if (n.Label != null && n.Label.FontSize > 12)
                            n.Label.FontSize = 10;
                    }
                    catch { }
                }

            }
            else
            {
                int nodeCount = _viewer.Graph == null ? 0 : _viewer.Graph.Nodes.Count();
            var client = _viewer.ClientSize;
            var area = Math.Max(1, client.Width) * Math.Max(1, client.Height);
            // occupancy factor: portion of area we want nodes to occupy (tweakable)
            var occupancy = _settings?.OccupancyFactor ?? 0.25; // portion of area reserved for nodes
            var targetAreaPerNode = area * occupancy / Math.Max(1, nodeCount);
            // base constant to convert area -> font scale (empirical)
            var scaleConst = 200.0;
            var rawSize = Math.Sqrt(targetAreaPerNode) / Math.Sqrt(scaleConst);
            var baseFont = 10.0; // baseline font
            double fontSize = Math.Max(_settings?.MinFontSize ?? 6, Math.Min(_settings?.MaxFontSize ?? 14, baseFont * rawSize));
            // adjust further by average label length to avoid huge boxes for long labels
            double avgLen = 0;
            if (nodeCount > 0 && _viewer.Graph != null)
            {
                foreach (var n in _viewer.Graph.Nodes)
                    avgLen += (n.LabelText?.Length ?? 0);
                avgLen = nodeCount > 0 ? avgLen / nodeCount : 0;
                if (avgLen > 20) fontSize = Math.Max(_settings?.MinFontSize ?? 6, fontSize * (20.0 / avgLen));

                foreach (var n in _viewer.Graph.Nodes)
                {
                    try
                    {
                        if (n.Label != null)
                        {
                            var labelText = n.LabelText ?? string.Empty;
                            if (labelText.Length > (_settings?.MaxLabelChars ?? 30))
                                n.LabelText = labelText.Substring(0, (_settings?.MaxLabelChars ?? 30)) + "…";
                            n.Label.FontSize = (float)fontSize;
                        }
                    }
                    catch { }
                }
            }
                // edge labels slightly smaller
                if (_viewer.Graph != null)
                {
                    foreach (var e in _viewer.Graph.Edges)
                    {
                        try { if (e.Label != null) e.Label.FontSize = (float)Math.Max(6.0, fontSize * 0.8); } catch { }
                    }
                }
            }
        }
        catch { }

        // Attempt to zoom/fit the graph to the viewer so the drawing area appears larger
        TryFitViewerToGraph();

        // For the fifths proof-of-concept using MDS, rotate the resulting coordinates so C is at 12:00
        // while preserving the compact circular shape that MDS produced.
        if (_currentLayoutMode == GraphLayoutMode.AutoMdsAlignedFifths)
        {
            AlignFifthsLayoutCAtTop();
        }
    }

    private void ApplyRectangularLatticePositions(DirectedGraph graph)
    {
        try
        {
            var width = Math.Max(500, _viewer.ClientSize.Width);
            var height = Math.Max(500, _viewer.ClientSize.Height);
            var centerX = width / 2.0;
            var centerY = height / 2.0;

            var count = Math.Max(1, graph.Vertices.Count);
            var columns = (int)Math.Ceiling(Math.Sqrt(count));
            var rows = (int)Math.Ceiling((double)count / columns);

            var spacingX = Math.Max(60.0, (width * 0.7) / Math.Max(1, columns - 1));
            var spacingY = Math.Max(60.0, (height * 0.7) / Math.Max(1, rows - 1));

            var startX = centerX - spacingX * (columns - 1) / 2.0;
            var startY = centerY - spacingY * (rows - 1) / 2.0;

            var vertices = graph.Vertices
                .OrderBy(v => (v.Label ?? string.Empty), StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.Id)
                .ToList();

            for (int i = 0; i < vertices.Count; i++)
            {
                var row = i / columns;
                var col = i % columns;
                var x = startX + col * spacingX;
                var y = startY + row * spacingY;
                _positions[vertices[i].Id] = new Microsoft.Msagl.Core.Geometry.Point(x, y);
            }
        }
        catch { }
    }

    private void ApplyNgonalLatticePositions(DirectedGraph graph)
    {
        try
        {
            var width = Math.Max(500, _viewer.ClientSize.Width);
            var height = Math.Max(500, _viewer.ClientSize.Height);
            var centerX = width / 2.0;
            var centerY = height / 2.0;

            var vertices = graph.Vertices
                .OrderBy(v => (v.Label ?? string.Empty), StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.Id)
                .ToList();

            if (vertices.Count == 0) return;

            var radius = Math.Max(120.0, Math.Min(width, height) * 0.32);
            var startAngle = -Math.PI / 2.0;

            for (int i = 0; i < vertices.Count; i++)
            {
                var angle = startAngle + (2.0 * Math.PI * i / vertices.Count);
                var x = centerX + radius * Math.Cos(angle);
                var y = centerY + radius * Math.Sin(angle);
                _positions[vertices[i].Id] = new Microsoft.Msagl.Core.Geometry.Point(x, y);
            }
        }
        catch { }
    }

    private void ApplyStoredPositionsToViewerGraph()
    {
        try
        {
            if (_viewer?.Graph == null) return;
            if (_model == null) return;

            foreach (var v in _model.Vertices)
            {
                if (!_positions.TryGetValue(v.Id, out var pos)) continue;
                var node = _viewer.Graph.FindNode(v.Id.ToString());
                if (node == null) continue;

                if (node.GeometryNode != null)
                {
                    node.GeometryNode.Center = pos;
                    continue;
                }

                try
                {
                    // fallback for cases where geometry nodes are not materialized yet
                    var attr = node.Attr;
                    var posProp = attr?.GetType().GetProperty("Pos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (posProp != null && posProp.CanWrite)
                    {
                        object? value = null;
                        if (posProp.PropertyType == typeof(Microsoft.Msagl.Core.Geometry.Point))
                        {
                            value = pos;
                        }
                        else
                        {
                            var ctor = posProp.PropertyType.GetConstructor(new[] { typeof(double), typeof(double) });
                            if (ctor != null)
                                value = ctor.Invoke(new object[] { pos.X, pos.Y });
                        }

                        if (value != null)
                            posProp.SetValue(attr, value);
                    }
                }
                catch { }
            }

            EnsureGraphGeometryUpdated();
            _viewer.Invalidate();
        }
        catch { }
    }

    private void AlignFifthsLayoutCAtTop()
    {
        try
        {
            if (_viewer?.Graph == null || _model == null) return;

            var cVertex = _model.Vertices.FirstOrDefault(v => string.Equals((v.Label ?? string.Empty).Trim(), "C", StringComparison.OrdinalIgnoreCase));
            if (cVertex == null) return;

            var cNode = _viewer.Graph.FindNode(cVertex.Id.ToString());
            if (cNode?.GeometryNode == null) return;

            var nodes = _viewer.Graph.Nodes.Where(n => n.GeometryNode != null).ToList();
            if (nodes.Count == 0) return;

            double cx = nodes.Average(n => n.GeometryNode.Center.X);
            double cy = nodes.Average(n => n.GeometryNode.Center.Y);

            var cp = cNode.GeometryNode.Center;
            var angleC = Math.Atan2(cp.Y - cy, cp.X - cx);
            // MSAGL Y-axis orientation is opposite screen-space, so +PI/2 maps to visual 12:00.
            var target = Math.PI / 2.0; // 12:00 (visual)
            var delta = target - angleC;

            foreach (var n in nodes)
            {
                var p = n.GeometryNode.Center;
                var dx = p.X - cx;
                var dy = p.Y - cy;
                var rx = dx * Math.Cos(delta) - dy * Math.Sin(delta);
                var ry = dx * Math.Sin(delta) + dy * Math.Cos(delta);
                n.GeometryNode.Center = new Microsoft.Msagl.Core.Geometry.Point(cx + rx, cy + ry);
            }

            // keep backing positions in sync for subsequent interactions/exports
            foreach (var v in _model.Vertices)
            {
                var node = _viewer.Graph.FindNode(v.Id.ToString());
                if (node?.GeometryNode != null)
                    _positions[v.Id] = node.GeometryNode.Center;
            }

            EnsureGraphGeometryUpdated();
            _viewer.Invalidate();
        }
        catch { }
    }

    private static System.Drawing.Color GetDefaultContrastingEdgeColor(System.Drawing.Color bg)
    {
        // tune for readability: darker in light theme, lighter in dark theme
        return GetPerceivedLuminance(bg) >= 140
            ? System.Drawing.Color.FromArgb(35, 35, 35)
            : System.Drawing.Color.FromArgb(225, 225, 225);
    }

    private static System.Drawing.Color GetMutedEdgeColor(System.Drawing.Color bg)
    {
        return GetPerceivedLuminance(bg) >= 140
            ? System.Drawing.Color.FromArgb(170, 170, 170)
            : System.Drawing.Color.FromArgb(120, 120, 120);
    }

    private static bool HasStrongContrast(System.Drawing.Color fg, System.Drawing.Color bg)
    {
        return Math.Abs(GetPerceivedLuminance(fg) - GetPerceivedLuminance(bg)) >= 95;
    }

    private static int GetPerceivedLuminance(System.Drawing.Color c)
    {
        return (int)Math.Round(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
    }

    private static System.Drawing.Color EnsureFillContrastWithLabel(System.Drawing.Color fill, System.Drawing.Color label, int minLumaDelta)
    {
        var fillL = GetPerceivedLuminance(fill);
        var labelL = GetPerceivedLuminance(label);
        if (Math.Abs(fillL - labelL) >= minLumaDelta) return fill;

        // For dark labels, lighten fill. For light labels, darken fill.
        return labelL < 128
            ? System.Drawing.Color.FromArgb(
                (int)Math.Min(255, fill.R + (255 - fill.R) * 0.45),
                (int)Math.Min(255, fill.G + (255 - fill.G) * 0.45),
                (int)Math.Min(255, fill.B + (255 - fill.B) * 0.45))
            : System.Drawing.Color.FromArgb(
                (int)Math.Max(0, fill.R * 0.55),
                (int)Math.Max(0, fill.G * 0.55),
                (int)Math.Max(0, fill.B * 0.55));
    }

    private static string NormalizeChordLabelSymbols(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.Length == 0) return s;

        s = Regex.Replace(s, "\\bdim\\b", "°", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "\\baug\\b", "+", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "\\s+([°+])", "$1");

        return s;
    }

    private void ApplyTraditionalFifthsPositions(DirectedGraph graph)
    {
        try
        {
            var order = new[] { "C", "G", "D", "A", "E", "B", "F#", "C#", "G#", "D#", "A#", "F" };
            var byLabel = graph.Vertices
                .GroupBy(v => (v.Label ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            if (byLabel.Count == 0) return;

            var width = Math.Max(400, _viewer.ClientSize.Width);
            var height = Math.Max(400, _viewer.ClientSize.Height);
            var radius = Math.Max(120.0, Math.Min(width, height) * 0.35);

            var centerX = width / 2.0;
            var centerY = height / 2.0;
            var startAngle = -Math.PI / 2.0; // 12:00

            for (int i = 0; i < order.Length; i++)
            {
                if (!byLabel.TryGetValue(order[i], out var v)) continue;

                var angle = startAngle + (2.0 * Math.PI * i / order.Length);
                var x = centerX + radius * Math.Cos(angle);
                var y = centerY + radius * Math.Sin(angle);
                _positions[v.Id] = new Microsoft.Msagl.Core.Geometry.Point(x, y);
            }
        }
        catch { }
    }

    private void ApplyChordState48StandardPositions(DirectedGraph graph)
    {
        try
        {
            var roots = new[] { "C", "G", "D", "A", "E", "B", "F#", "C#", "G#", "D#", "A#", "F" };
            var width = Math.Max(500, _viewer.ClientSize.Width);
            var height = Math.Max(500, _viewer.ClientSize.Height);
            var centerX = width / 2.0;
            var centerY = height / 2.0;

            // Four concentric rings: maj, min, 7, m7
            var baseRadius = Math.Min(width, height) * 0.12;
            var ringGap = Math.Min(width, height) * 0.10;

            var ringMap = new (Func<string, string> Label, double Radius)[]
            {
                (r => r, baseRadius + ringGap * 3),
                (r => r + "m", baseRadius + ringGap * 2),
                (r => r + "7", baseRadius + ringGap * 1),
                (r => r + "m7", baseRadius + ringGap * 0)
            };

            var byLabel = graph.Vertices
                .GroupBy(v => (v.Label ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var startAngle = -Math.PI / 2.0; // C at 12:00
            for (int i = 0; i < roots.Length; i++)
            {
                var angle = startAngle + (2.0 * Math.PI * i / roots.Length);
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);

                foreach (var ring in ringMap)
                {
                    var label = ring.Label(roots[i]);
                    if (!byLabel.TryGetValue(label, out var v)) continue;

                    // compensate for screen-space Y orientation so circle renders upright
                    var x = centerX + ring.Radius * cos;
                    var y = centerY - ring.Radius * sin;
                    _positions[v.Id] = new Microsoft.Msagl.Core.Geometry.Point(x, y);
                }
            }
        }
        catch { }
    }

    private void TryFitViewerToGraph()
    {
        try
        {
            if (_viewer == null) return;
            var vType = _viewer.GetType();
            var methods = vType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                var name = m.Name.ToLowerInvariant();
                if (!(name.Contains("zoom") || name.Contains("fit") || name.Contains("scale"))) continue;
                if (m.GetParameters().Length != 0) continue;
                try
                {
                    m.Invoke(_viewer, null);
                    return;
                }
                catch { }
            }

            // fallback: reassign graph to force a layout/refresh which often results in a better fit
            var g = _viewer.Graph;
            _viewer.Graph = null;
            _viewer.Graph = g;
        }
        catch { }
    }

    private void OptimizeLayout()
    {
        try
        {
            if (_model == null || _viewer == null) return;
            if (_globalFreeze) return; // respect freeze

            // trigger a layout refresh; reassigning graph often forces MSAGL to recompute layout
            var g = _viewer.Graph;
            _viewer.Graph = null;
            _viewer.Graph = g;

            // After re-layout, try to fit to viewer
            TryFitViewerToGraph();
        }
        catch { }
    }

    private void Viewer_MouseMove(object? sender, MouseEventArgs e)
    {
        // marquee handling
        if (_isMarquee)
        {
            // erase previous
            try
            {
                if (!_marqueePrevScreenRect.IsEmpty)
                    ControlPaint.DrawReversibleFrame(_marqueePrevScreenRect, System.Drawing.Color.Black, System.Windows.Forms.FrameStyle.Dashed);
                var p1 = _viewer.PointToScreen(_marqueeStart);
                var p2 = _viewer.PointToScreen(e.Location);
                var rect = System.Drawing.Rectangle.FromLTRB(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y));
                ControlPaint.DrawReversibleFrame(rect, System.Drawing.Color.Black, System.Windows.Forms.FrameStyle.Dashed);
                _marqueePrevScreenRect = rect;
            }
            catch { }
            return;
        }

        if (!_isDragging || !_draggingVertexId.HasValue) return;

        // convert screen point to graph point
        if (!TryScreenToGraph(e.Location, out var gpt)) return;

        var id = _draggingVertexId.Value;
        if (_draggingMultiple)
        {
            // compute delta relative to original of dragged vertex
            if (!_dragOriginalPositions.TryGetValue(id, out var orig)) orig = _dragOriginalPosition ?? new Microsoft.Msagl.Core.Geometry.Point(0,0);
            var dx = gpt.X - orig.X;
            var dy = gpt.Y - orig.Y;
            foreach (var kv in _dragOriginalPositions.ToList())
            {
                var sid = kv.Key;
                var sOrig = kv.Value;
                var newPos = new Microsoft.Msagl.Core.Geometry.Point(sOrig.X + dx, sOrig.Y + dy);
                var node = _viewer.Graph?.FindNode(sid.ToString());
                if (node?.GeometryNode != null)
                    node.GeometryNode.Center = newPos;
                _positions[sid] = newPos;
            }
            // Ensure edge geometry is refreshed after manual node moves
            EnsureGraphGeometryUpdated();
            _viewer.Invalidate();
        }
        else
        {
            // update visual position immediately
            var node = _viewer.Graph?.FindNode(id.ToString());
            if (node?.GeometryNode != null)
            {
                node.GeometryNode.Center = gpt;
                _positions[id] = gpt;
                // Ensure edge geometry is refreshed after manual node move
                EnsureGraphGeometryUpdated();
                _viewer.Invalidate();
            }
        }
    }

    // MSAGL may cache edge curves; after moving nodes manually we attempt to invoke
    // any internal graph update/layout methods via reflection to force edge geometry
    // to be recalculated. This is defensive and uses no-arg methods that look like
    // "update", "layout", "create", "compute" or "calculate".
    private void EnsureGraphGeometryUpdated()
    {
        try
        {
            var g = _viewer?.Graph;
            if (g == null) return;
            var methods = g.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            foreach (var m in methods)
            {
                var name = m.Name.ToLowerInvariant();
                if (!(name.Contains("update") || name.Contains("create") || name.Contains("layout") || name.Contains("compute") || name.Contains("calculate")))
                    continue;
                if (m.GetParameters().Length != 0) continue;
                try
                {
                    m.Invoke(g, null);
                    // stop after first successful invocation
                    break;
                }
                catch
                {
                    // ignore and try next
                }
            }
        }
        catch
        {
            // swallow any reflection errors
        }
    }

    private void Viewer_MouseUp(object? sender, MouseEventArgs e)
    {
        // finish marquee
        if (_isMarquee)
        {
            try
            {
                if (!_marqueePrevScreenRect.IsEmpty)
                    ControlPaint.DrawReversibleFrame(_marqueePrevScreenRect, System.Drawing.Color.Black, System.Windows.Forms.FrameStyle.Dashed);
            }
            catch { }
            _isMarquee = false;
            // convert marquee corners to graph coords
            if (_model != null)
            {
                var start = _marqueeStart;
                var end = e.Location;
                if (TryScreenToGraph(start, out var g1) && TryScreenToGraph(end, out var g2))
                {
                    var minX = Math.Min(g1.X, g2.X);
                    var maxX = Math.Max(g1.X, g2.X);
                    var minY = Math.Min(g1.Y, g2.Y);
                    var maxY = Math.Max(g1.Y, g2.Y);
                    // select nodes whose center is within rect
                    ClearSelection();
                    foreach (var v in _model.Vertices)
                    {
                        Microsoft.Msagl.Core.Geometry.Point? center = null;
                        if (_positions.TryGetValue(v.Id, out var p)) center = p;
                        else center = _viewer.Graph?.FindNode(v.Id.ToString())?.GeometryNode?.Center;
                        if (center.HasValue)
                        {
                            var cp = center.Value;
                            if (cp.X >= minX && cp.X <= maxX && cp.Y >= minY && cp.Y <= maxY)
                            {
                                _selectedVertexIds.Add(v.Id);
                            }
                        }
                    }
                    // update visuals
                    SelectVertex(_selectedVertexIds.FirstOrDefault());
                }
            }
            _marqueePrevScreenRect = System.Drawing.Rectangle.Empty;
            return;
        }

        if (!_isDragging || !_draggingVertexId.HasValue) return;

        if (e.Button == System.Windows.Forms.MouseButtons.Left)
        {
            var id = _draggingVertexId.Value;
            _isDragging = false;
            _draggingVertexId = null;
            if (_draggingMultiple)
            {
                // commit multi-move
                var moves = new System.Collections.Generic.List<(Guid id, Microsoft.Msagl.Core.Geometry.Point? oldPos, Microsoft.Msagl.Core.Geometry.Point newPos)>();
                foreach (var kv in _dragOriginalPositions)
                {
                    var sid = kv.Key;
                    var oldPos = kv.Value;
                    var newPos = _positions[sid];
                    if (oldPos != newPos)
                        moves.Add((sid, oldPos, newPos));
                }
                if (moves.Count > 0)
                    PushUndo(new MultiMoveVertexAction(moves));
                _dragOriginalPositions.Clear();
                _draggingMultiple = false;
            }
            else
            {
                // determine final position for single drag
                if (_positions.TryGetValue(id, out var newPos))
                {
                    var oldPos = _dragOriginalPosition;
                    _dragOriginalPosition = null;
                    // if changed, push Move action
                    if (!oldPos.HasValue || oldPos.Value != newPos)
                    {
                        PushUndo(new MoveVertexAction(id, oldPos, newPos));
                    }
                }
            }

            // finalize edge geometry after drag release to avoid temporary visual disconnects
            EnsureGraphGeometryUpdated();
            _viewer.Invalidate();
        }
    }

    private bool TryScreenToGraph(System.Drawing.Point screenPt, out Microsoft.Msagl.Core.Geometry.Point graphPt)
    {
        graphPt = new Microsoft.Msagl.Core.Geometry.Point();
        try
        {
            var viewerType = _viewer.GetType();
            var methods = viewerType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                var name = m.Name.ToLower();
                if (!name.Contains("screen") && !name.Contains("transform") && !name.Contains("point")) continue;
                var parameters = m.GetParameters();
                object? ret = null;
                try
                {
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(System.Drawing.Point))
                        ret = m.Invoke(_viewer, new object[] { screenPt });
                    else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(int))
                        ret = m.Invoke(_viewer, new object[] { screenPt.X, screenPt.Y });
                }
                catch { ret = null; }

                if (ret == null) continue;
                if (ret is Microsoft.Msagl.Core.Geometry.Point gp)
                {
                    graphPt = gp;
                    return true;
                }
                if (ret is System.Drawing.PointF pf)
                {
                    graphPt = new Microsoft.Msagl.Core.Geometry.Point(pf.X, pf.Y);
                    return true;
                }
                if (ret is System.Drawing.Point p2)
                {
                    graphPt = new Microsoft.Msagl.Core.Geometry.Point(p2.X, p2.Y);
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    private void Viewer_MouseClick(object? sender, MouseEventArgs e)
    {
        // Add Edge mode: left-click first node to set source, second to set target
        if (_addEdgeMode && e.Button == System.Windows.Forms.MouseButtons.Left)
        {
            var objUnder = _viewer.ObjectUnderMouseCursor;
            if (TryResolveModelIdsFromViewerObject(objUnder, out var vId, out var eId))
            {
                if (vId.HasValue && _model != null)
                {
                    if (!_pendingEdgeSourceId.HasValue)
                    {
                        _pendingEdgeSourceId = vId.Value;
                        // inform user to select target
                        MessageBox.Show(this, "Select target vertex to create edge", "Add Edge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        var source = _pendingEdgeSourceId.Value;
                        var target = vId.Value;
                        var allowCreate = source != target;
                        if (!allowCreate)
                        {
                            var confirm = MessageBox.Show(
                                this,
                                "Create a self-edge on this vertex?",
                                "Add Edge",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning);
                            allowCreate = confirm == DialogResult.Yes;
                        }

                        if (allowCreate)
                        {
                            // prompt for label
                            string label;
                            try { label = Microsoft.VisualBasic.Interaction.InputBox("Edge label (optional):", "Add Edge", ""); }
                            catch { label = string.Empty; }
                            var edge = _model.CreateEdge(source, target, label ?? string.Empty);
                            PushUndo(new CreateEdgeAction(edge.Id, edge.Label, edge.Source.Id, edge.Target.Id));
                            RenderGraph(_model);
                            SelectEdge(edge.Id);
                        }

                        _pendingEdgeSourceId = null;
                    }
                }
            }
            return;
        }
        // If in Add Vertex mode, left-click creates a vertex at the clicked location
        if (_addVertexMode && e.Button == System.Windows.Forms.MouseButtons.Left)
        {
            if (_model == null) return;
            // attempt to capture creation position in graph coordinates
            if (!TryScreenToGraph(e.Location, out var gpt)) return;
            // prompt for label
            var defaultLabel = "V" + _nextAutoLabel++;
            string label;
            try { label = Microsoft.VisualBasic.Interaction.InputBox("Vertex label:", "Add Vertex", defaultLabel); }
            catch { label = defaultLabel; }
            if (string.IsNullOrWhiteSpace(label)) label = defaultLabel;
            label = NormalizeChordLabelSymbols(label);
            var v = _model.CreateVertex(label);
            // set position and record
            _positions[v.Id] = gpt;
            // select and prompt properties (label already set) - allow editing again
            ClearSelection();
            SelectVertex(v.Id);
            RenderGraph(_model);

            // one-shot add vertex mode: return to default interaction after one insert
            _addVertexMode = false;
            if (_addVertexBtn != null) _addVertexBtn.Checked = false;
            _viewer.Cursor = Cursors.Default;
            UpdateStatusLabel();
            return;
        }

        // Right-click behavior: show standardized context menu only
        if (e.Button == System.Windows.Forms.MouseButtons.Right)
        {
            var obj = _viewer.ObjectUnderMouseCursor;
            _contextVertexId = null;
            _contextEdgeId = null;
            _contextClickGraphPoint = null;
            TryScreenToGraph(e.Location, out var contextPoint);
            _contextClickGraphPoint = contextPoint;

            if (obj != null && TryResolveModelIdsFromViewerObject(obj, out var vId, out var eId))
            {
                if (vId.HasValue)
                {
                    _contextVertexId = vId.Value;
                    // If multiple vertices are already selected and the clicked vertex is one of them,
                    // preserve the multi-selection. Only change selection if clicked vertex is not part
                    // of the current selection.
                    if (!_selectedVertexIds.Contains(vId.Value))
                    {
                        SelectVertex(vId.Value);
                    }
                    else
                    {
                        // Make the clicked vertex the primary selection without clearing others
                        _selectedVertexId = vId.Value;
                    }
                }
                else if (eId.HasValue)
                {
                    _contextEdgeId = eId.Value;
                    SelectEdge(eId.Value);
                }
            }

            // snapshot current selection to avoid timing/order issues between MouseDown/MouseClick
            _selectionSnapshot = _selectedVertexIds.ToList();
            ConfigureAndShowContextMenu(e.Location);
        }
    }

    private void Viewer_MouseDown(object? sender, MouseEventArgs e)
    {
        // Left-click selects
        if (e.Button == System.Windows.Forms.MouseButtons.Left)
        {
            var obj = _viewer.ObjectUnderMouseCursor;
            if (obj == null)
            {
                // Left-click on empty background clears any current selection
                ClearSelection();
                // start marquee selection
                _isMarquee = true;
                _marqueeStart = e.Location;
                _marqueePrevScreenRect = System.Drawing.Rectangle.Empty;
                return;
            }

            if (TryResolveModelIdsFromViewerObject(obj, out var vId, out var eId))
            {
                var ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                if (vId.HasValue)
                {
                    var groupDragCandidate = !ctrl && _selectedVertexIds.Count > 1 && _selectedVertexIds.Contains(vId.Value);
                    if (!groupDragCandidate)
                        SelectVertex(vId.Value, ctrl);
                    // begin potential drag only when not toggling selection
                    if (!ctrl && !_frozenNodes.Contains(vId.Value))
                    {
                        _draggingVertexId = vId.Value;
                        _isDragging = true;
                        if (groupDragCandidate)
                        {
                            _draggingMultiple = true;
                            _dragOriginalPosition = null;
                            _dragOriginalPositions.Clear();
                            foreach (var sid in _selectedVertexIds)
                            {
                                if (_positions.TryGetValue(sid, out var p))
                                    _dragOriginalPositions[sid] = p;
                                else
                                {
                                    var node = _viewer.Graph?.FindNode(sid.ToString());
                                    var center = node?.GeometryNode?.Center ?? new Microsoft.Msagl.Core.Geometry.Point(0, 0);
                                    _dragOriginalPositions[sid] = center;
                                }
                            }
                        }
                        else
                        {
                            _draggingMultiple = false;
                            _dragOriginalPositions.Clear();
                            // capture original position if present
                            if (_positions.TryGetValue(vId.Value, out var p))
                                _dragOriginalPosition = p;
                            else
                            {
                                // try to read geometry center
                                var node = _viewer.Graph?.FindNode(vId.Value.ToString());
                                _dragOriginalPosition = node?.GeometryNode?.Center;
                            }
                        }
                    }
                }
                else if (eId.HasValue)
                    SelectEdge(eId.Value, ctrl);
            }
        }
    }

    private void SelectVertex(Guid id, bool toggle = false)
    {
        if (_model == null) return;
        if (toggle)
        {
            // toggle membership
            if (_selectedVertexIds.Contains(id))
                _selectedVertexIds.Remove(id);
            else
            {
                _selectedVertexIds.Add(id);
                _selectedEdgeIds.Clear();
            }
        }
        else
        {
            _selectedVertexIds.Clear();
            _selectedEdgeIds.Clear();
            _selectedVertexIds.Add(id);
        }

        _selectedVertexId = _selectedVertexIds.FirstOrDefault();
        _selectedEdgeId = _selectedEdgeIds.FirstOrDefault();

        // update colors on current graph
        if (_viewer.Graph != null)
        {
            foreach (var n in _viewer.Graph.Nodes)
            {
                var gid = Guid.Empty;
                if (Guid.TryParse(n.Id, out gid) && _selectedVertexIds.Contains(gid))
                    n.Attr.Color = Microsoft.Msagl.Drawing.Color.Red;
                else
                    n.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
            }

            foreach (var de in _viewer.Graph.Edges)
            {
                // default to black; edge selection coloring handled in SelectEdge
                de.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
            }
        }

        _viewer.Invalidate();
    }

    private void SelectEdge(Guid id, bool toggle = false)
    {
        if (_model == null) return;
        if (toggle)
        {
            if (_selectedEdgeIds.Contains(id))
                _selectedEdgeIds.Remove(id);
            else
            {
                _selectedEdgeIds.Add(id);
                _selectedVertexIds.Clear();
            }
        }
        else
        {
            _selectedEdgeIds.Clear();
            _selectedVertexIds.Clear();
            _selectedEdgeIds.Add(id);
        }

        _selectedVertexId = _selectedVertexIds.FirstOrDefault();
        _selectedEdgeId = _selectedEdgeIds.FirstOrDefault();

        if (_viewer.Graph != null)
        {
            foreach (var n in _viewer.Graph.Nodes)
            {
                var gid = Guid.Empty;
                if (Guid.TryParse(n.Id, out gid) && _selectedVertexIds.Contains(gid))
                    n.Attr.Color = Microsoft.Msagl.Drawing.Color.Red;
                else
                    n.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
            }

            foreach (var de in _viewer.Graph.Edges)
            {
                // find corresponding model edge
                var src = de.Source;
                var tgt = de.Target;
                if (Guid.TryParse(src, out var sgid) && Guid.TryParse(tgt, out var tgid))
                {
                    var modelEdge = _model.Edges.FirstOrDefault(e => e.Source.Id == sgid && e.Target.Id == tgid);
                    if (modelEdge != null && _selectedEdgeIds.Contains(modelEdge.Id))
                        de.Attr.Color = Microsoft.Msagl.Drawing.Color.Red;
                    else
                        de.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
                }
                else
                {
                    de.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
                }
            }
        }

        _viewer.Invalidate();
    }

    private void ClearSelection()
    {
        _selectedVertexIds.Clear();
        _selectedEdgeIds.Clear();
        _selectedVertexId = null;
        _selectedEdgeId = null;
        if (_viewer.Graph != null)
        {
            foreach (var n in _viewer.Graph.Nodes)
                n.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
            foreach (var e in _viewer.Graph.Edges)
                e.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
        }
    }

    private async void ImportButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "JSON files|*.json|All files|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _currentLayoutMode = GraphLayoutMode.AutoDefault;
            var result = await System.Threading.Tasks.Task.Run(() => DirectedGraph.LoadFromFileWithLayout(dlg.FileName));
            // replace model and restore layout if present
            _positions.Clear();
            _frozenNodes.Clear();
            ClearSelection();
            if (result.Layout != null)
            {
                if (result.Layout.Positions != null)
                {
                    foreach (var kv in result.Layout.Positions)
                        _positions[kv.Key] = new Microsoft.Msagl.Core.Geometry.Point(kv.Value.X, kv.Value.Y);
                }
                if (result.Layout.FrozenIds != null)
                {
                    _frozenNodes.UnionWith(result.Layout.FrozenIds);
                    _globalFreeze = _frozenNodes.Count > 0;
                }
            }
            RenderGraph(result.Graph);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Import failed: " + ex.Message, "Import", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void ExportButton_Click(object? sender, EventArgs e)
    {
        if (_model == null)
        {
            MessageBox.Show(this, "No graph to export", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new SaveFileDialog { Filter = "JSON files|*.json|All files|*.*", FileName = "graph.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            // convert MSAGL points to PositionDto for core serialization
            var posDto = _positions.ToDictionary(kv => kv.Key, kv => new DiGraphLab.Core.DirectedGraph.PositionDto { X = kv.Value.X, Y = kv.Value.Y });
            await System.Threading.Tasks.Task.Run(() => _model.SaveToFile(dlg.FileName, posDto, _frozenNodes));
            MessageBox.Show(this, "Export complete", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Export failed: " + ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryResolveModelIdsFromViewerObject(object obj, out Guid? vertexId, out Guid? edgeId)
    {
        vertexId = null;
        edgeId = null;
        if (obj == null) return false;

        try
        {
            var t = obj.GetType();

            // try common property names that wrap drawing objects
            var prop = t.GetProperty("DrawingObject") ?? t.GetProperty("Node") ?? t.GetProperty("Label") ?? t.GetProperty("Edge") ?? t.GetProperty("DrawingEdge");
            object? drawingObj = prop?.GetValue(obj) ?? obj;

            if (drawingObj == null) return false;

            var idProp = drawingObj.GetType().GetProperty("Id");
            if (idProp != null)
            {
                var idVal = idProp.GetValue(drawingObj)?.ToString();
                if (Guid.TryParse(idVal, out var gid))
                {
                    vertexId = gid;
                    return true;
                }
            }

            // try edge with Source/Target
            var srcProp = drawingObj.GetType().GetProperty("Source");
            var tgtProp = drawingObj.GetType().GetProperty("Target");
            if (srcProp != null && tgtProp != null)
            {
                var srcNode = srcProp.GetValue(drawingObj);
                var tgtNode = tgtProp.GetValue(drawingObj);
                var srcId = srcNode?.GetType().GetProperty("Id")?.GetValue(srcNode)?.ToString();
                var tgtId = tgtNode?.GetType().GetProperty("Id")?.GetValue(tgtNode)?.ToString();
                if (Guid.TryParse(srcId, out var sgid) && Guid.TryParse(tgtId, out var tgid) && _model != null)
                {
                    var match = _model.Edges.FirstOrDefault(e => e.Source.Id == sgid && e.Target.Id == tgid);
                    if (match != null)
                    {
                        edgeId = match.Id;
                        return true;
                    }
                }
            }
        }
        catch
        {
            // ignore reflection errors
        }

        return false;
    }

    // Vertex context menu handlers
    private void VertexDelete_Click(object? sender, EventArgs e)
    {
        if (_model == null) return;
        var ids = _selectedVertexIds.ToList();
        if (ids.Count == 0 && _selectedVertexId.HasValue) ids.Add(_selectedVertexId.Value);
        foreach (var id in ids)
        {
            var (v, removedEdges) = _model.RemoveVertex(id);
            // capture position
            _positions.TryGetValue(id, out var pos);
            _positions.Remove(id);
            if (v != null)
                PushUndo(new DeleteVertexAction(v, removedEdges, pos));
        }
        ClearSelection();
        RenderGraph(_model);
    }

    private void VertexProperties_Click(object? sender, EventArgs e)
    {
        // allow editing of vertex label (simple inline properties)
        if (_selectedVertexId.HasValue && _model != null && _model.TryGetVertex(_selectedVertexId.Value, out var v))
        {
            string current = v.Label ?? string.Empty;
            string input;
            try { input = Microsoft.VisualBasic.Interaction.InputBox("Edit vertex label:", "Vertex Properties", current); }
            catch { input = current; }
            input = NormalizeChordLabelSymbols(input);
            if (!string.IsNullOrWhiteSpace(input) && input != current)
            {
                v.Label = input;
                RenderGraph(_model);
            }
        }
    }

    private void VertexFreeze_Click(object? sender, EventArgs e)
    {
        if (!_selectedVertexId.HasValue || _model == null)
        {
            return;
        }

        var id = _selectedVertexId.Value;
        if (_frozenNodes.Contains(id))
        {
            _frozenNodes.Remove(id);
            MessageBox.Show($"Vertex unfrozen.", "Freeze", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            _frozenNodes.Add(id);
            MessageBox.Show($"Vertex frozen.", "Freeze", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        RenderGraph(_model);
    }

    // Edge context menu handlers
    private void EdgeDelete_Click(object? sender, EventArgs e)
    {
        if (_model == null) return;
        var ids = _selectedEdgeIds.ToList();
        if (ids.Count == 0 && _selectedEdgeId.HasValue) ids.Add(_selectedEdgeId.Value);
        foreach (var id in ids)
        {
            var removed = _model.RemoveEdge(id);
            if (removed != null)
                PushUndo(new DeleteEdgeAction(removed));
        }
        ClearSelection();
        RenderGraph(_model);
    }

    private void EdgeProperties_Click(object? sender, EventArgs e)
    {
        if (_selectedEdgeId.HasValue && _model != null && _model.TryGetEdge(_selectedEdgeId.Value, out var edge))
        {
            string current = edge.Label ?? string.Empty;
            string input;
            try { input = Microsoft.VisualBasic.Interaction.InputBox("Edit edge label:", "Edge Properties", current); }
            catch { input = current; }
            if (!string.IsNullOrWhiteSpace(input) && input != current)
            {
                edge.Label = input;
                RenderGraph(_model);
            }
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Y)
        {
            Redo();
            e.Handled = true;
        }
    }

    private void PushUndo(IUndoableAction action)
    {
        _undoStack.Add(action);
        if (_undoStack.Count > MaxUndo) _undoStack.RemoveAt(0);
        _redoStack.Clear();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack[_undoStack.Count - 1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        action.Undo(this);
        _redoStack.Add(action);
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack[_redoStack.Count - 1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        action.Redo(this);
        _undoStack.Add(action);
    }

    private interface IUndoableAction
    {
        void Undo(MainForm f);
        void Redo(MainForm f);
    }

    private record ReplaceGraphAction(DirectedGraph? OldGraph, System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point> OldPositions, System.Collections.Generic.HashSet<Guid> OldFrozen, DirectedGraph NewGraph, System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point> NewPositions, System.Collections.Generic.HashSet<Guid> NewFrozen) : IUndoableAction
    {
        public void Undo(MainForm f)
        {
            f._model = OldGraph;
            f._positions.Clear();
            if (OldPositions != null)
            {
                foreach (var kv in OldPositions) f._positions[kv.Key] = kv.Value;
            }
            f._frozenNodes.Clear();
            if (OldFrozen != null)
            {
                foreach (var id in OldFrozen) f._frozenNodes.Add(id);
            }
            f.RenderGraph(f._model!);
        }

        public void Redo(MainForm f)
        {
            f._model = NewGraph;
            f._positions.Clear();
            foreach (var kv in NewPositions) f._positions[kv.Key] = kv.Value;
            f._frozenNodes.Clear();
            foreach (var id in NewFrozen) f._frozenNodes.Add(id);
            f.RenderGraph(f._model!);
        }
    }

    private record CreateVertexAction(Guid Id, string Label, Microsoft.Msagl.Core.Geometry.Point? Position) : IUndoableAction
    {
        public void Undo(MainForm f)
        {
            f._model?.RemoveVertex(Id);
            f._positions.Remove(Id);
            f.RenderGraph(f._model!);
        }

        public void Redo(MainForm f)
        {
            // recreate vertex with original label and restore position if provided
            f._model?.AddVertex(new DiGraphLab.Core.Vertex(Id, Label));
            if (Position.HasValue)
                f._positions[Id] = Position.Value;
            f.RenderGraph(f._model!);
        }
    }

    private record DeleteVertexAction(DiGraphLab.Core.Vertex Vertex, System.Collections.Generic.List<DiGraphLab.Core.Edge> IncidentEdges, Microsoft.Msagl.Core.Geometry.Point? Position) : IUndoableAction
    {
        public void Undo(MainForm f)
        {
            f._model?.AddVertex(Vertex);
            if (IncidentEdges != null)
            {
                foreach (var e in IncidentEdges)
                {
                    // rebind source/target to current vertex instances
                    var src = f._model?.TryGetVertex(e.Source.Id, out var sv) == true ? sv : null;
                    var tgt = f._model?.TryGetVertex(e.Target.Id, out var tv) == true ? tv : null;
                    if (src != null && tgt != null)
                    {
                        f._model.AddEdge(new DiGraphLab.Core.Edge(e.Id, src, tgt, e.Label));
                    }
                }
            }
            if (Position.HasValue)
                f._positions[Vertex.Id] = Position.Value;
            f.RenderGraph(f._model!);
        }

        public void Redo(MainForm f)
        {
            f._model?.RemoveVertex(Vertex.Id);
            f._positions.Remove(Vertex.Id);
            f.RenderGraph(f._model!);
        }
    }

    private record DeleteEdgeAction(DiGraphLab.Core.Edge Edge) : IUndoableAction
    {
        public void Undo(MainForm f)
        {
            // re-add edge
            if (f._model != null && f._model.TryGetVertex(Edge.Source.Id, out var sv) && f._model.TryGetVertex(Edge.Target.Id, out var tv))
            {
                f._model.AddEdge(new DiGraphLab.Core.Edge(Edge.Id, sv, tv, Edge.Label));
            }
            f.RenderGraph(f._model!);
        }

        public void Redo(MainForm f)
        {
            f._model?.RemoveEdge(Edge.Id);
            f.RenderGraph(f._model!);
        }
    }

    private record CreateEdgeAction(Guid Id, string Label, Guid SourceId, Guid TargetId) : IUndoableAction
    {
        public void Undo(MainForm f)
        {
            f._model?.RemoveEdge(Id);
            f.RenderGraph(f._model!);
        }

        public void Redo(MainForm f)
        {
            if (f._model != null && f._model.TryGetVertex(SourceId, out var sv) && f._model.TryGetVertex(TargetId, out var tv))
            {
                f._model.AddEdge(new DiGraphLab.Core.Edge(Id, sv!, tv!, Label));
            }
            f.RenderGraph(f._model!);
        }
    }

    private record MoveVertexAction(Guid Id, Microsoft.Msagl.Core.Geometry.Point? OldPosition, Microsoft.Msagl.Core.Geometry.Point NewPosition) : IUndoableAction
    {
        public void Undo(MainForm f)
        {
            if (OldPosition.HasValue)
                f._positions[Id] = OldPosition.Value;
            else
                f._positions.Remove(Id);
            f.RenderGraph(f._model!);
        }

        public void Redo(MainForm f)
        {
            f._positions[Id] = NewPosition;
            f.RenderGraph(f._model!);
        }
    }
}
