using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Drawing;
using DiGraphLab.Core;

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

    private readonly ContextMenuStrip _vertexContextMenu;
    private readonly ContextMenuStrip _edgeContextMenu;
    private readonly System.Collections.Generic.Dictionary<Guid, Microsoft.Msagl.Core.Geometry.Point> _positions = new();
    private readonly ToolStrip _toolStrip;
    private readonly ToolStripButton _freezeToggleButton;
    private ToolTip _hoverToolTip;
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

        Controls.Add(_viewer);

        // toolbar
        _toolStrip = new ToolStrip { Dock = DockStyle.Top };
        _freezeToggleButton = new ToolStripButton("Freeze layout") { CheckOnClick = true };
        _freezeToggleButton.CheckedChanged += FreezeToggle_CheckedChanged;
        _toolStrip.Items.Add(_freezeToggleButton);

        _addVertexBtn = new ToolStripButton("Add Vertex") { CheckOnClick = true };
        _addVertexBtn.Click += (s, e) => ToggleAddVertexMode();
        _toolStrip.Items.Add(_addVertexBtn);

        _addEdgeBtn = new ToolStripButton("Add Edge") { CheckOnClick = true };
        _addEdgeBtn.Click += (s, e) => ToggleAddEdgeMode();
        _toolStrip.Items.Add(_addEdgeBtn);

        var importButton = new ToolStripButton("Import") { ToolTipText = "Import graph from JSON" };
        importButton.Click += ImportButton_Click;
        _toolStrip.Items.Add(importButton);

        var exportButton = new ToolStripButton("Export") { ToolTipText = "Export graph to JSON" };
        exportButton.Click += ExportButton_Click;
        _toolStrip.Items.Add(exportButton);

        var themeDrop = new ToolStripDropDownButton("Theme") { ToolTipText = "Select theme" };
        var lightItem = new ToolStripMenuItem("Light");
        lightItem.Click += (s, e) => ApplyLightTheme();
        var darkItem = new ToolStripMenuItem("Dark");
        darkItem.Click += (s, e) => ApplyDarkTheme();
        themeDrop.DropDownItems.Add(lightItem);
        themeDrop.DropDownItems.Add(darkItem);

        _toolStrip.Items.Add(themeDrop);

        var optimizeBtn = new ToolStripButton("Optimize Layout") { ToolTipText = "Optimize graph layout" };
        optimizeBtn.Click += (s, e) => { try { OptimizeLayout(); } catch { } };
        _toolStrip.Items.Add(optimizeBtn);

        _settingsBtn = new ToolStripButton();
        _settingsBtn.DisplayStyle = ToolStripItemDisplayStyle.Image;
        try
        {
            var asm = typeof(MainForm).Assembly;
            using var stream = asm.GetManifestResourceStream("DiGraphLab.Resources.gear.png");
            if (stream != null)
            {
                _settingsBtn.Image = System.Drawing.Image.FromStream(stream);
            }
            else
            {
                _settingsBtn.Image = System.Drawing.SystemIcons.Application.ToBitmap();
            }
            _settingsBtn.ToolTipText = "Settings";
        }
        catch
        {
            _settingsBtn.Text = "⚙";
        }
        _settingsBtn.Click += SettingsBtn_Click;
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(_settingsBtn);
        _statusLabel = new ToolStripLabel();
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(_statusLabel);
        Controls.Add(_toolStrip);

        // Context menus
        _vertexContextMenu = new ContextMenuStrip();
        _vertexContextMenu.Items.Add(new ToolStripMenuItem("Delete", null, VertexDelete_Click));
        _vertexContextMenu.Items.Add(new ToolStripMenuItem("Properties", null, VertexProperties_Click));
        _vertexContextMenu.Items.Add(new ToolStripMenuItem("Freeze position", null, VertexFreeze_Click));

        _edgeContextMenu = new ContextMenuStrip();
        _edgeContextMenu.Items.Add(new ToolStripMenuItem("Delete", null, EdgeDelete_Click));
        _edgeContextMenu.Items.Add(new ToolStripMenuItem("Properties", null, EdgeProperties_Click));

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
    }

    private void Viewer_MouseEnter(object? sender, EventArgs e)
    {
        UpdateStatusLabel();
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

        var msagl = new Microsoft.Msagl.Drawing.Graph("graph") { Directed = true };

        // compute default color as inverse of the form background so themes are visible
        var bg = this.BackColor;
        var defaultMsaglColor = bg.ToMsagl();
        defaultMsaglColor = new Microsoft.Msagl.Drawing.Color((byte)(255 - defaultMsaglColor.R), (byte)(255 - defaultMsaglColor.G), (byte)(255 - defaultMsaglColor.B));

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
            node.Attr.FillColor = nodeColor;
            node.Attr.Color = nodeColor;
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
            // edge color from model or theme default
            Microsoft.Msagl.Drawing.Color edgeColor = defaultMsaglColor;
            if (!string.IsNullOrEmpty(e.Color))
            {
                try
                {
                    edgeColor = ColorExtensions.FromHtmlToMsagl(e.Color);
                }
                catch { }
            }
            me.Attr.Color = edgeColor;
        }

        _viewer.Graph = msagl;
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
                        if (source != target)
                        {
                            // prompt for label
                            string label;
                            try { label = Microsoft.VisualBasic.Interaction.InputBox("Edge label (optional):", "Add Edge", ""); }
                            catch { label = string.Empty; }
                            var edge = _model.CreateEdge(source, target, label ?? string.Empty);
                            PushUndo(new CreateEdgeAction(edge.Id, edge.Label, edge.Source.Id, edge.Target.Id));
                            _pendingEdgeSourceId = null;
                            RenderGraph(_model);
                            SelectEdge(edge.Id);
                        }
                        else
                        {
                            MessageBox.Show(this, "Cannot create self-edge. Select a different target.", "Add Edge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            _pendingEdgeSourceId = null;
                        }
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
            var v = _model.CreateVertex(label);
            // set position and record
            _positions[v.Id] = gpt;
            // select and prompt properties (label already set) - allow editing again
            ClearSelection();
            SelectVertex(v.Id);
            RenderGraph(_model);
            return;
        }

        // Right-click behavior: show context menu for object or create vertex on empty space
        if (e.Button == System.Windows.Forms.MouseButtons.Right)
        {
            var obj = _viewer.ObjectUnderMouseCursor;
            if (obj == null)
            {
                if (_model == null)
                    return;
                // create a simple auto-labeled vertex
                var label = "V" + _nextAutoLabel++;
                // create with default color inverted from background
                Vertex v;
                try
                {
                    v = _model.CreateVertex(label);
                    if (_settings.AssignDefaultColorToNew)
                    {
                        var bg = this.BackColor;
                        var inv = new Microsoft.Msagl.Drawing.Color((byte)(255 - bg.R), (byte)(255 - bg.G), (byte)(255 - bg.B));
                        // use HTML hex for storage
                        var sysInv = System.Drawing.Color.FromArgb(inv.R, inv.G, inv.B);
                        var hex = System.Drawing.ColorTranslator.ToHtml(sysInv);
                        v.Color = hex;
                    }
                }
                catch
                {
                    v = _model.CreateVertex(label);
                }
                // attempt to capture creation position in graph coordinates
                Microsoft.Msagl.Core.Geometry.Point? createdPos = null;
                try
                {
                    var pt = e.Location;
                    var viewerType = _viewer.GetType();
                    var methods = viewerType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    foreach (var m in methods)
                    {
                        if (!m.Name.ToLower().Contains("screen") && !m.Name.ToLower().Contains("point")) continue;
                        var parameters = m.GetParameters();
                        object? ret = null;
                        try
                        {
                            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(System.Drawing.Point))
                                ret = m.Invoke(_viewer, new object[] { pt });
                            else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(int) && parameters[1].ParameterType == typeof(int))
                                ret = m.Invoke(_viewer, new object[] { pt.X, pt.Y });
                        }
                        catch { ret = null; }

                        if (ret == null) continue;

                        // handle common return types
                        if (ret is Microsoft.Msagl.Core.Geometry.Point gpt)
                        {
                            createdPos = gpt;
                            break;
                        }
                        if (ret is System.Drawing.PointF pf)
                        {
                            createdPos = new Microsoft.Msagl.Core.Geometry.Point(pf.X, pf.Y);
                            break;
                        }
                        if (ret is System.Drawing.Point p2)
                        {
                            createdPos = new Microsoft.Msagl.Core.Geometry.Point(p2.X, p2.Y);
                            break;
                        }
                    }
                }
                catch { }

                if (createdPos.HasValue)
                {
                    _positions[v.Id] = createdPos.Value;
                }

                // record position and push undo action (store label so redo restores it)
                PushUndo(new CreateVertexAction(v.Id, v.Label, createdPos));
                RenderGraph(_model);
                return;
            }

            if (TryResolveModelIdsFromViewerObject(obj, out var vId, out var eId))
            {
                if (vId.HasValue)
                {
                    SelectVertex(vId.Value);
                    _vertexContextMenu.Show(_viewer, e.Location);
                }
                else if (eId.HasValue)
                {
                    SelectEdge(eId.Value);
                    _edgeContextMenu.Show(_viewer, e.Location);
                }
            }
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
