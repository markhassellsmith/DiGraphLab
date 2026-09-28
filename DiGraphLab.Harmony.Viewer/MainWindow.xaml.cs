using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using DiGraphLab.Harmony;
using DiGraphLab.Harmony.Viewer.Controls;
using System.Text;

namespace DiGraphLab.Harmony.Viewer
{
    public partial class MainWindow : Window
    {
        // last imported graph file path for reload action
        private string? _lastImportedGraphPath;

        private void UpdateReloadButtonState()
        {
            try
            {
                ReloadGraphButton.IsEnabled = !string.IsNullOrEmpty(_lastImportedGraphPath) && File.Exists(_lastImportedGraphPath);
            }
            catch
            {
                ReloadGraphButton.IsEnabled = false;
            }
        }


        private void TransposeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(TransposeAmountText.Text, out var amt)) { MessageBox.Show("Invalid transpose amount."); return; }
                var transposed = _svc.TransposeGraph(amt);
                // replace current graph with transposed copy
                _svc.ResetGraph();
                // serialize transposed graph to JSON then import via service.Graph.ImportJson path
                var tmp = System.IO.Path.GetTempFileName();
                transposed.ExportJson(tmp);
                _svc.Graph.ImportJson(tmp);
                System.IO.File.Delete(tmp);
                var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                _loadedNodes = nodes.ToArray();
                GraphCanvasControl.LoadModel(nodes, edges);
                PopulateAnalysis(_svc.Graph);
            }
            catch (Exception ex) { MessageBox.Show("Transpose failed: " + ex.Message); }
        }

        private void ImportMidiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "MIDI files (*.mid;*.midi)|*.mid;*.midi|All files (*.*)|*.*" };
                if (dlg.ShowDialog() != true) return;
                var path = dlg.FileName;
                // Use NAudio.Midi reader if available to extract note-on events and group by time (simple approach)
                try
                {
                    var midiEvents = new System.Collections.Generic.List<(long time, int note)>();
                    var rdr = new NAudio.Midi.MidiFile(path, false);
                    for (int t = 0; t < rdr.Tracks; t++)
                    {
                        var events = rdr.Events.GetTrackEvents(t);
                        foreach (var ev in events)
                        {
                            if (ev is NAudio.Midi.NoteOnEvent noe && noe.Velocity > 0)
                            {
                                midiEvents.Add((noe.AbsoluteTime, noe.NoteNumber));
                            }
                        }
                    }
                    // group by quantized time (simple quantization into buckets)
                    var groups = midiEvents.GroupBy(x => x.time).OrderBy(g => g.Key);
                    foreach (var g in groups)
                    {
                        var pcs = g.Select(x => x.note % 12).Distinct().ToArray();
                        var label = string.Join("/", pcs.Select(p => p.ToString()));
                        var chord = new DiGraphLab.Harmony.Chord(label, pcs.Length>0?pcs[0]:0, "", 0, pcs);
                        _svc.AddChord(chord, pcs.Length>0?pcs[0]:0);
                    }
                    var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                    _loadedNodes = nodes.ToArray();
                    GraphCanvasControl.LoadModel(nodes, edges);
                    PopulateAnalysis(_svc.Graph);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("MIDI import failed: " + ex.Message);
                }
            }
            catch (Exception ex) { MessageBox.Show("Import MIDI failed: " + ex.Message); }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var repo = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                var guide = System.IO.Path.Combine(repo, "DiGraphLab_User_Guide.md");
                if (System.IO.File.Exists(guide))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = guide, UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("User guide not found in repository root: " + guide);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open user guide: " + ex.Message);
            }
        }

        private void ApplyNodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = GraphCanvasControl.GetSelectedNodeIds();
                if (selected == null || selected.Length == 0) { MessageBox.Show("No node selected."); return; }
                var id = selected[0];
                var dto = _loadedNodes?.FirstOrDefault(n => n.Id == id);
                if (dto == null) { MessageBox.Show("Selected node not found."); return; }
                // read inline inspector values
                var trad = Inspector_Traditional.Text?.Trim();
                var nash = Inspector_Nashville.Text?.Trim();
                var qual = Inspector_Quality.Text?.Trim();
                var pcs = (Inspector_PitchClasses.Text ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length>0).Select(s => int.TryParse(s, out var v) ? v % 12 : 0).ToArray();
                var root = dto.RootPc; // keep original root unless quality/root editing UI added
                var inversion = dto.Inversion;
                var label = !string.IsNullOrWhiteSpace(trad) ? trad : (!string.IsNullOrWhiteSpace(nash) ? nash : dto.TraditionalLabel ?? dto.Id);
                var chord = new DiGraphLab.Harmony.Chord(label ?? dto.Id, root, qual ?? dto.Quality, inversion, pcs);
                var updated = _svc.UpdateNodeRepresentative(dto.Id, chord);
                if (!updated) { MessageBox.Show("Failed to update node representative in graph."); return; }
                // no styles/count from inline inspector; do a lightweight visual update
                var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                _loadedNodes = nodes.ToArray();
                GraphCanvasControl.LoadModel(nodes, edges);
                PopulateAnalysis(_svc.Graph);
            }
            catch (Exception ex) { MessageBox.Show("Apply failed: " + ex.Message); }
        }



        private void ImportGraphJsonButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Graph JSON (*.json)|*.json|All files (*.*)|*.*" };
                if (dlg.ShowDialog() != true) return;
                var path = dlg.FileName;
                // HarmonyGraph.ImportJson expects a file path. Call it directly with selected path.
                try
                {
                    _svc.ResetGraph();
                    _svc.Graph.ImportJson(path);
                }
                catch (InvalidDataException ide)
                {
                    MessageBox.Show("Import failed: invalid graph JSON. " + ide.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                catch (FileNotFoundException fnf)
                {
                    MessageBox.Show("Import failed: file not found. " + fnf.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                catch (JsonException je)
                {
                    MessageBox.Show("Import failed: JSON parse error. " + je.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Import failed: " + ex.Message);
                    return;
                }
                // convert to DTOs and load
                var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                _loadedNodes = nodes.ToArray();
                GraphCanvasControl.LoadModel(nodes, edges);
                _lastImportedGraphPath = path;
                UpdateReloadButtonState();
                PopulateAnalysis(_svc.Graph);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Import JSON failed: " + ex.Message);
            }
        }

        private void EditNodeButton_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                // find the currently selected node DTO from loaded nodes via GraphCanvas selection
                var selected = GraphCanvasControl.GetSelectedNodeIds();
                if (selected == null || selected.Length == 0)
                {
                    MessageBox.Show("No node selected to edit.");
                    return;
                }
                // use the first selected node
                var id = selected[0];
                var dto = _loadedNodes?.FirstOrDefault(n => n.Id == id);
                if (dto == null)
                {
                    MessageBox.Show("Selected node not found in loaded model.");
                    return;
                }
                var win = new EditNodeWindow();
                win.LoadFromDto(dto);
                if (win.ShowDialog() == true)
                {
                    // persist changes to the HarmonyService by constructing a Chord from edited fields
                    var label = !string.IsNullOrWhiteSpace(win.Traditional) ? win.Traditional : (!string.IsNullOrWhiteSpace(win.Nashville) ? win.Nashville : dto.TraditionalLabel ?? dto.Id);
                    var rootPc = win.RootPc ?? dto.RootPc;
                    var quality = !string.IsNullOrWhiteSpace(win.Quality) ? win.Quality : dto.Quality;
                    var inversion = win.Inversion ?? dto.Inversion;
                    var pcs = (win.PitchClasses != null && win.PitchClasses.Length > 0) ? win.PitchClasses : dto.PitchClasses.ToArray();
                    var chord = new DiGraphLab.Harmony.Chord(label ?? dto.Id, rootPc, quality, inversion, pcs);
                    var updated = _svc.UpdateNodeRepresentative(dto.Id, chord);
                    if (!updated) MessageBox.Show("Failed to update node representative in graph.");

                    // persist styles and count as well when provided
                    // currently EditNodeWindow does return Styles and may return a Count via its public properties
                    try
                    {
                        // If Edit dialog provided styles or count, update node attributes in the graph
                        if ((win.Styles != null && win.Styles.Length > 0) || win.Count != null)
                        {
                            var countToUse = win.Count ?? dto.Count;
                            var merge = false;
                            try { merge = win.MergeStyles; } catch { merge = false; }
                            _svc.UpdateNodeAttributes(dto.Id, chord, win.Styles, countToUse, merge);
                        }
                    }
                    catch { }

                    // update the single node visual instead of reloading full model
                    try
                    {
                        // rebuild a fresh NodeDto for the updated node by converting the graph for that node only
                        var (nodesAll, edgesAll) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                        var updatedDto = nodesAll.FirstOrDefault(n => n.Id == dto.Id);
                        if (updatedDto != null)
                        {
                            _loadedNodes = nodesAll.ToArray();
                            GraphCanvasControl.UpdateNodeVisual(dto.Id, updatedDto);
                        }
                        PopulateAnalysis(_svc.Graph);
                    }
                    catch (Exception)
                    {
                        // fallback to full reload if incremental update fails
                        var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                        _loadedNodes = nodes.ToArray();
                        GraphCanvasControl.LoadModel(nodes, edges);
                        PopulateAnalysis(_svc.Graph);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Edit failed: " + ex.Message);
            }
        }

        private void ExportNodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selected = GraphCanvasControl.GetSelectedNodeIds();
                if (selected == null || selected.Length == 0)
                {
                    MessageBox.Show("No node selected to export.");
                    return;
                }
                var id = selected[0];
                var dto = _loadedNodes?.FirstOrDefault(n => n.Id == id);
                if (dto == null)
                {
                    MessageBox.Show("Selected node not found in loaded model.");
                    return;
                }
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var path = System.IO.Path.Combine(desktop, dto.Id + "_node.json");
                var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(path, json);
                MessageBox.Show("Exported node to " + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed: " + ex.Message);
            }
        }

        private void RemoveNodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ids = GraphCanvasControl.SelectedNodeIds.ToArray();
                if (ids.Length == 0)
                {
                    MessageBox.Show("Select a node to remove.");
                    return;
                }
                var id = ids[0];
                var removed = _svc.RemoveNode(id);
                if (removed)
                {
                    var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                    _loadedNodes = nodes.ToArray();
                    GraphCanvasControl.LoadModel(nodes, edges);
                    MessageBox.Show($"Node removed: {id}");
                }
                else
                {
                    MessageBox.Show($"Node not found: {id}");
                }
            }
            catch (Exception ex) { MessageBox.Show("Remove node failed: " + ex.Message); }
        }

        private void ReloadGraphButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_lastImportedGraphPath)) { MessageBox.Show("No graph file recorded to reload."); return; }
                if (!File.Exists(_lastImportedGraphPath)) { MessageBox.Show("Graph file not found: " + _lastImportedGraphPath); UpdateReloadButtonState(); return; }
                _svc.ResetGraph();
                _svc.Graph.ImportJson(_lastImportedGraphPath);
                var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                _loadedNodes = nodes.ToArray();
                GraphCanvasControl.LoadModel(nodes, edges);
                PopulateAnalysis(_svc.Graph);
                // refresh inspector combo sources based on loaded graph
                SendViewerOptions();
                PopulateInspectorChoices(nodes);
                MessageBox.Show("Graph reloaded: " + _lastImportedGraphPath);
            }
            catch (InvalidDataException ide)
            {
                MessageBox.Show("Reload failed: invalid graph JSON. " + ide.Message, "Reload Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reload failed: " + ex.Message, "Reload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private readonly HarmonyService _svc = new HarmonyService();

        public MainWindow()
        {
            InitializeComponent();
            // subscribe to canvas zoom changes to update toolbar display
            GraphCanvasControl.ZoomChanged += z => { try { Dispatcher.Invoke(() => ToolbarZoomText.Text = ((int)(z * 100)).ToString() + "%"); } catch { } };
            // keyboard shortcut: Ctrl+E to open editor for first selected node
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            // ensure inspector combo sources are populated at startup from current (empty) graph
            Loaded += (_, __) => {
                try
                {
                    var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                    PopulateInspectorChoices(nodes);
                }
                catch { }
            };
        }

        private void PopulateInspectorChoices(IEnumerable<DiGraphLab.Harmony.GraphAdapter.NodeDto> nodes)
        {
            try
            {
                var qualities = nodes.Select(n => n.Quality).Where(q => !string.IsNullOrWhiteSpace(q)).Distinct().Take(30).ToList();
                Inspector_Quality.Items.Clear();
                foreach (var q in new[] { "maj", "maj7", "m", "m7", "7", "dim", "sus4" }) Inspector_Quality.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = q });
                foreach (var q in qualities)
                {
                    if (!Inspector_Quality.Items.OfType<System.Windows.Controls.ComboBoxItem>().Any(i => (i.Content?.ToString() ?? string.Empty) == q))
                        Inspector_Quality.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = q });
                }

                // populate Traditional and Nashville recent values
                var trads = nodes.Select(n => n.TraditionalLabel).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(50).ToList();
                Inspector_Traditional.Items.Clear();
                foreach (var t in trads) Inspector_Traditional.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = t });

                var nashes = nodes.Select(n => n.NashvilleLabel).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(50).ToList();
                Inspector_Nashville.Items.Clear();
                foreach (var t in nashes) Inspector_Nashville.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = t });

                // populate Notation and Key combos with values found in nodes (keys derived from RootPc)
                var keys = nodes.Select(n => n.RootPc).Distinct().OrderBy(x => x).ToArray();
                if (keys.Length > 0)
                {
                    var firstKey = keys[0];
                    foreach (System.Windows.Controls.ComboBoxItem item in KeyCombo.Items)
                    {
                        if (int.TryParse(item.Tag?.ToString() ?? "", out var v) && v == firstKey)
                        {
                            KeyCombo.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private void MainWindow_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e == null) return;
                if (e.Key == System.Windows.Input.Key.E && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    // open editor for the first selected node
                    EditNodeButton_Click(null, null);
                    e.Handled = true;
                }
            }
            catch { }
        }

        private void CreateEdgeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // require exactly two selected nodes (order: from -> to). If sequence buffer has items, prefer that order.
                var ids = GraphCanvasControl.SelectedNodeIds.ToArray();
                if (ids.Length < 2 && _sequenceBuffer.Count < 2)
                {
                    MessageBox.Show("Select two nodes (Shift+Click) or load a sequence with two items to create an edge.");
                    return;
                }
                string fromId, toId;
                if (_sequenceBuffer.Count >= 2)
                {
                    fromId = _sequenceBuffer[0].Id; toId = _sequenceBuffer[1].Id;
                }
                else
                {
                    fromId = ids[0]; toId = ids[1];
                }

                // lookup DTOs in loaded nodes
                var fromDto = _loadedNodes?.FirstOrDefault(n => n.Id == fromId);
                var toDto = _loadedNodes?.FirstOrDefault(n => n.Id == toId);
                if (fromDto == null || toDto == null) { MessageBox.Show("Selected nodes not found in loaded nodes."); return; }

                // create chord instances from DTOs and add edge via HarmonyService
                var fromChord = new DiGraphLab.Harmony.Chord(fromDto.TraditionalLabel ?? fromDto.Id, fromDto.RootPc, fromDto.Quality, fromDto.Inversion, fromDto.PitchClasses);
                var toChord = new DiGraphLab.Harmony.Chord(toDto.TraditionalLabel ?? toDto.Id, toDto.RootPc, toDto.Quality, toDto.Inversion, toDto.PitchClasses);
                // add edge to shared service
                _svc.AddEdge(fromChord, toChord, fromDto.RootPc, style: null);
                // reload viewer with updated graph
                var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                _loadedNodes = nodes.ToArray();
                GraphCanvasControl.LoadModel(nodes, edges);
                MessageBox.Show($"Edge created: {fromId} -> {toId}");
            }
            catch (Exception ex) { MessageBox.Show("Create edge failed: " + ex.Message); }
        }

        private void RemoveEdgeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ids = GraphCanvasControl.SelectedNodeIds.ToArray();
                if (ids.Length < 2 && _sequenceBuffer.Count < 2)
                {
                    MessageBox.Show("Select two nodes (Shift+Click) or load a sequence with two items to remove an edge.");
                    return;
                }
                string fromId = ids.Length >= 2 ? ids[0] : _sequenceBuffer[0].Id;
                string toId = ids.Length >= 2 ? ids[1] : _sequenceBuffer[1].Id;
                var removed = _svc.RemoveEdge(fromId, toId);
                if (removed)
                {
                    var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                    _loadedNodes = nodes.ToArray();
                    GraphCanvasControl.LoadModel(nodes, edges);
                    MessageBox.Show($"Edge removed: {fromId} -> {toId}");
                }
                else
                {
                    MessageBox.Show($"Edge not found: {fromId} -> {toId}");
                }
            }
            catch (Exception ex) { MessageBox.Show("Remove edge failed: " + ex.Message); }
        }

        private void CreateNodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new CreateNodeWindow();
                dlg.Owner = this;
                if (dlg.ShowDialog() == true)
                {
                    var dto = dlg.Result;
                // create chord and add to graph using AddChord on shared service
                    var chord = new Chord(dto.Label ?? dto.Id, dto.RootPc, dto.Quality, dto.Inversion, dto.PitchClasses);
                    _svc.AddChord(chord, dto.RootPc, dto.Style);
                    var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                    _loadedNodes = nodes.ToArray();
                    GraphCanvasControl.LoadModel(nodes, edges);
                }
            }
            catch (Exception ex) { MessageBox.Show("Create node failed: " + ex.Message); }
        }

        private void ImportGraphButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Harmony Graph JSON|*.json;*.graph.json|All Files|*.*" };
            if (dlg.ShowDialog(this) != true) return;
            var path = dlg.FileName;
            try
            {
                _svc.ResetGraph();
                _svc.Graph.ImportJson(path);
                var (nodes, edges) = GraphAdapter.Convert(_svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                _loadedNodes = nodes.ToArray();
                GraphCanvasControl.LoadModel(nodes, edges);
                PopulateAnalysis(_svc.Graph);
                SendViewerOptions();
                _lastImportedGraphPath = path;
                UpdateReloadButtonState();
                MessageBox.Show("Graph imported: " + path);
            }
            catch (InvalidDataException ide)
            {
                MessageBox.Show("Import failed: invalid graph JSON. " + ide.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (FileNotFoundException fnf)
            {
                MessageBox.Show("Import failed: file not found. " + fnf.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (JsonException je)
            {
                MessageBox.Show("Import failed: JSON parse error. " + je.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Import failed: " + ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowMatrixButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new HarmonyService();
                var graph = svc.Graph;
                var (labels, mat) = Analytics.ComputeTransitionMatrix(graph);
                var w = new Controls.TransitionMatrixWindow();
                w.SetMatrix(labels, mat);
                w.Owner = this;
                // wire interactive cell clicks to highlight nodes/edges in the graph
                w.CellClicked += (i, j) =>
                {
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // labels are node ids in the same order
                            var fromId = labels[i];
                            var toId = labels[j];
                            // clear previous selection and highlight the two nodes and any connecting edge
                            GraphCanvasControl.ClearSelection();
                            // find node ids in loaded nodes and select
                            var fromNode = _loadedNodes?.FirstOrDefault(n => n.Id == fromId);
                            var toNode = _loadedNodes?.FirstOrDefault(n => n.Id == toId);
                            if (fromNode != null && toNode != null)
                            {
                                GraphCanvasControl.HighlightNodes(fromNode.Id, toNode.Id);
                                // center view around first node
                                GraphCanvasControl.FitToView();
                            }
                        });
                    }
                    catch { }
                };
                w.Show();
            }
            catch (Exception ex) { MessageBox.Show("Matrix failed: " + ex.Message); }
        }

        private void ExportStatsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_loadedNodes == null) { MessageBox.Show("No graph loaded."); return; }
                var svc = new HarmonyService();
                // assume svc.Graph has been populated when loading demo; if not, we still use GraphAdapter to reconstruct - prefer existing graph reference
                var graph = svc.Graph; // NOTE: in this demo flow HarmonyService used earlier already constructed the graph; for robustness you may expose the graph instance globally
                var (summary, nodes, edges) = Analytics.ComputeStatistics(graph);
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv|JSON|*.json", FileName = "graph_stats" };
                if (dlg.ShowDialog(this) == true)
                {
                    using var fs = System.IO.File.CreateText(dlg.FileName);
                    if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) Analytics.WriteJson(fs, summary, nodes, edges);
                    else Analytics.WriteCsv(fs, nodes, edges);
                    MessageBox.Show("Exported stats: " + dlg.FileName);
                }
            }
            catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
        }

        private void PopulateAnalysis(HarmonyGraph graph)
        {
            try
            {
                var (summary, nodes, edges) = Analytics.ComputeStatistics(graph);
                AnalysisNodesText.Text = summary.NodeCount.ToString();
                AnalysisEdgesText.Text = summary.EdgeCount.ToString();
                TopNodesList.ItemsSource = nodes.Take(10).ToArray();
                TopEdgesList.ItemsSource = edges.Take(10).ToArray();
            }
            catch { }
        }

        private void FindMotifsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new HarmonyService();
                var graph = svc.Graph;
                var paths = Analytics.EnumeratePaths(graph, maxLength: 5, maxPaths: 200).ToArray();
                // group by stringified path and count
                var grouped = paths.Select(p => string.Join("->", p)).GroupBy(s => s).Select(g => new { Path = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(20).ToArray();
                var dlg = new StringBuilder();
                foreach (var g in grouped) dlg.AppendLine($"{g.Count}\t{g.Path}");
                MessageBox.Show(dlg.ToString(), "Motifs (top)");
            }
            catch (Exception ex) { MessageBox.Show("Motif search failed: " + ex.Message); }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                var pct = (int)e.NewValue;
                if (ZoomPercentText != null) ZoomPercentText.Text = pct + "%";
                var scale = pct / 100.0;
                GraphCanvasControl?.SetZoom(scale);
            }
            catch { }
        }

        private void PlaybackOctaveCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (PlaybackOctaveCombo.SelectedItem is System.Windows.Controls.ComboBoxItem it && int.TryParse(it.Tag?.ToString(), out var oct))
                {
                    _playbackOctave = oct;
                }
            }
            catch { }
        }

        private void FitButton_Click(object sender, RoutedEventArgs e)
        {
            try { GraphCanvasControl.FitToView(); } catch { }
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            try { GraphCanvasControl.ZoomIn(); } catch { }
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            try { GraphCanvasControl.ZoomOut(); } catch { }
        }

        private async void LoadDemoButton_Click(object sender, RoutedEventArgs e)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var outDir = Path.Combine(desktop, "DiGraphLabViewerDemo");
            Directory.CreateDirectory(outDir);
            Demo.RunSample(outDir);

            // load model via HarmonyService and GraphAdapter
            // use shared service for demo load
            _svc.ResetGraph();
            var svc = _svc;
            // recreate progression used in Demo
            var tonic = 0;
            var I = Chord.FromMajorDiatonic("I", tonic, 1, addSeventh: false);
            var IV = Chord.FromMajorDiatonic("IV", tonic, 4, addSeventh: false);
            var V7 = Chord.FromMajorDiatonic("V7", tonic, 5, addSeventh: true);
            svc.AddProgression(new[] { I, IV, V7, I }, tonic, style: "demo");

            var (nodes, edges) = GraphAdapter.Convert(svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
            _loadedNodes = nodes.ToArray();
            GraphCanvasControl.LoadModel(nodes, edges);
            // populate analysis UI based on the service's graph
            PopulateAnalysis(svc.Graph);
            // subscribe to node click events from GraphCanvas
                GraphCanvasControl.NodeClicked += (trad, nash, quality, pcs, rootPc, inversion) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Inspector_Traditional.Text = trad;
                        Inspector_Nashville.Text = nash;
                        Inspector_Quality.Text = quality;
                        Inspector_PitchClasses.Text = pcs != null ? string.Join(", ", pcs) : "-";
                        // reconstruct chord for playback
                        _selectedChord = new DiGraphLab.Harmony.Chord(trad, rootPc, quality, inversion, pcs ?? Array.Empty<int>());
                    });
                };
            // apply initial options
            SendViewerOptions();
        }

        // receive node click events from native canvas by wiring selection in code (handled below via GraphCanvas events if needed)

        private DiGraphLab.Harmony.Chord? _selectedChord;
        private int _playbackOctave = 4;
        private System.Threading.CancellationTokenSource? _playCts;
        private int _tempo = 120;
        private GraphAdapter.NodeDto[]? _loadedNodes;
        private readonly List<GraphAdapter.NodeDto> _sequenceBuffer = new();
        private int _perItemBeats = 1;

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedChord == null)
                {
                    MessageBox.Show("No chord selected.");
                    return;
                }
                // compute voiced semitones and map to MIDI using playback octave
                var voiced = _selectedChord.GetVoicedSemitones();
                int octaveBase = _playbackOctave * 12; // e.g., octave 4 -> 48
                int baseMidi = 12 + octaveBase + _selectedChord.RootPc; // keep within typical piano range
                var midiNotes = voiced.Select(iv => baseMidi + iv).ToArray();
                DiGraphLab.Harmony.Viewer.Playback.SimpleSynth.PlaySemitones(midiNotes, durationMs: 900);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Playback failed: " + ex.Message);
                return;
            }

        }

        private async void PlaySequenceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ids = GraphCanvasControl.SelectedNodeIds.ToArray();
                if (ids.Length == 0 && _sequenceBuffer.Count == 0)
                {
                    MessageBox.Show("No nodes selected. Use Shift+Click to build a sequence.");
                    return;
                }
                // reconstruct chords in selected order; prefer explicit sequence editor if loaded
                var chords = new List<Chord>();
                var idList = _sequenceBuffer.Count > 0 ? _sequenceBuffer.Select(n => n.Id).ToArray() : ids;
                foreach (var id in idList)
                {
                    var node = GraphCanvasControlLoadedNodeById(id);
                    if (node != null) chords.Add(node);
                }
                if (chords.Count == 0) { MessageBox.Show("No playable chords found."); return; }
                _playCts?.Cancel(); _playCts = new System.Threading.CancellationTokenSource();
                PlaySequenceButton.IsEnabled = false; StopButton.IsEnabled = true;
                int loopCount = 1;
                if (LoopToggle?.IsChecked == true)
                {
                    if (!int.TryParse(LoopCountText?.Text, out loopCount) || loopCount <= 0) loopCount = -1; // -1 = infinite
                }

                int played = 0;
                while (loopCount == -1 || played < loopCount)
                {
                    _playCts.Token.ThrowIfCancellationRequested();
                    await PlayChordSequenceAsync(chords, _playbackOctave, _tempo, _playCts.Token);
                    played++;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { MessageBox.Show("Playback error: " + ex.Message); }
            finally { PlaySequenceButton.IsEnabled = true; StopButton.IsEnabled = false; }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _playCts?.Cancel();
        }

        private void TempoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                _tempo = (int)e.NewValue;
                TempoText?.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, _tempo + " BPM");
            }
            catch { }
        }

        private void LoadSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _sequenceBuffer.Clear();
                foreach (var id in GraphCanvasControl.SelectedNodeIds)
                {
                    var dto = _loadedNodes?.FirstOrDefault(n => n.Id == id);
                    if (dto != null) _sequenceBuffer.Add(dto);
                }
                SequenceList.ItemsSource = null;
                SequenceList.ItemsSource = _sequenceBuffer;
            }
            catch { }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var idx = SequenceList.SelectedIndex;
                if (idx > 0)
                {
                    var item = _sequenceBuffer[idx];
                    _sequenceBuffer.RemoveAt(idx);
                    _sequenceBuffer.Insert(idx - 1, item);
                    SequenceList.ItemsSource = null; SequenceList.ItemsSource = _sequenceBuffer;
                    SequenceList.SelectedIndex = idx - 1;
                }
            }
            catch { }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var idx = SequenceList.SelectedIndex;
                if (idx >= 0 && idx < _sequenceBuffer.Count - 1)
                {
                    var item = _sequenceBuffer[idx];
                    _sequenceBuffer.RemoveAt(idx);
                    _sequenceBuffer.Insert(idx + 1, item);
                    SequenceList.ItemsSource = null; SequenceList.ItemsSource = _sequenceBuffer;
                    SequenceList.SelectedIndex = idx + 1;
                }
            }
            catch { }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var idx = SequenceList.SelectedIndex;
                if (idx >= 0)
                {
                    _sequenceBuffer.RemoveAt(idx);
                    SequenceList.ItemsSource = null; SequenceList.ItemsSource = _sequenceBuffer;
                }
            }
            catch { }
        }

        private void SaveSequenceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sequenceBuffer.Count == 0) { MessageBox.Show("No sequence to save."); return; }
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Sequence JSON|*.seq.json", FileName = "sequence.seq.json" };
                if (dlg.ShowDialog(this) == true)
                {
                    var data = _sequenceBuffer.Select(n => new { n.Id, n.TraditionalLabel, n.NashvilleLabel, n.RootPc, n.Quality, n.Inversion, pcs = n.PitchClasses.ToArray() }).ToArray();
                    var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dlg.FileName, json);
                    MessageBox.Show("Sequence saved: " + dlg.FileName);
                }
            }
            catch (Exception ex) { MessageBox.Show("Save failed: " + ex.Message); }
        }

        private void LoadSequenceFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Sequence JSON|*.seq.json" };
                if (dlg.ShowDialog(this) == true)
                {
                    var txt = System.IO.File.ReadAllText(dlg.FileName);
                    var items = System.Text.Json.JsonSerializer.Deserialize<LoadedSeqItem[]>(txt);
                    if (items != null)
                    {
                        _sequenceBuffer.Clear();
                        foreach (var it in items)
                        {
                            // try to map to loaded nodes by id, else create minimal DTO
                            var dto = _loadedNodes?.FirstOrDefault(n => n.Id == it.Id) ?? new GraphAdapter.NodeDto { Id = it.Id, TraditionalLabel = it.TraditionalLabel ?? it.Id, NashvilleLabel = it.NashvilleLabel ?? it.Id, RootPc = it.RootPc, Quality = it.Quality ?? string.Empty, Inversion = it.Inversion, PitchClasses = it.pcs ?? Array.Empty<int>() };
                            _sequenceBuffer.Add(dto);
                        }
                        SequenceList.ItemsSource = null; SequenceList.ItemsSource = _sequenceBuffer;
                        MessageBox.Show("Sequence loaded: " + dlg.FileName);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Load failed: " + ex.Message); }
        }

        private record LoadedSeqItem(string Id, string? TraditionalLabel, string? NashvilleLabel, int RootPc, string? Quality, int Inversion, int[]? pcs);

        // Drag/drop support for SequenceList
        private Point _dragStartPoint;

        private void SequenceList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void SequenceList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
                var pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                if (SequenceList.SelectedItem == null) return;
                var data = new DataObject("SequenceItem", SequenceList.SelectedItem);
                DragDrop.DoDragDrop(SequenceList, data, DragDropEffects.Move);
            }
            catch { }
        }

        private void SequenceList_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent("SequenceItem") ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void SequenceList_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent("SequenceItem")) return;
                var item = e.Data.GetData("SequenceItem") as GraphAdapter.NodeDto;
                if (item == null) return;
                var target = GetListBoxItemUnderMouse(SequenceList, e.GetPosition(SequenceList));
                int oldIndex = _sequenceBuffer.IndexOf(item);
                int newIndex = target >= 0 ? target : _sequenceBuffer.Count - 1;
                if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;
                _sequenceBuffer.RemoveAt(oldIndex);
                _sequenceBuffer.Insert(newIndex, item);
                SequenceList.ItemsSource = null; SequenceList.ItemsSource = _sequenceBuffer;
            }
            catch { }
        }

        private int GetListBoxItemUnderMouse(System.Windows.Controls.ListBox listBox, Point point)
        {
            for (int i = 0; i < listBox.Items.Count; i++)
            {
                var item = listBox.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (item == null) continue;
                var bounds = new Rect(item.TranslatePoint(new Point(0, 0), listBox), new Size(item.ActualWidth, item.ActualHeight));
                if (bounds.Contains(point)) return i;
            }
            return -1;
        }

        private void DurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                _perItemBeats = (int)e.NewValue;
                // Avoid double-evaluation of DurationText (prevents rare race where it becomes null
                // between the null check and the assignment). Use a local copy or the null-conditional
                // operator to be safe.
                var dt = DurationText;
                if (dt != null) dt.Text = _perItemBeats.ToString();
                // Alternatively: DurationText?.Text = _perItemBeats.ToString();
            }
            catch (Exception ex)
            {
                // Do not swallow exceptions silently; log at least so we can diagnose future issues.
                System.Diagnostics.Debug.WriteLine($"DurationSlider_ValueChanged exception: {ex}");
            }
        }

        private async System.Threading.Tasks.Task PlayChordSequenceAsync(IEnumerable<Chord> chords, int octave, int tempo, System.Threading.CancellationToken token)
        {
            // quarter-note duration in ms
            var quarterMs = 60000.0 / tempo;
            var list = chords.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var ch = list[i];
                var voiced = ch.GetVoicedSemitones();
                int baseMidi = 12 + octave * 12 + ch.RootPc;
                var midiNotes = voiced.Select(iv => baseMidi + iv).ToArray();
                // metronome click on each chord's downbeat
                if (MetronomeCheck.IsChecked == true)
                {
                    // short click: play a high partial
                    DiGraphLab.Harmony.Viewer.Playback.SimpleSynth.PlaySemitones(new[] { 84 }, durationMs: 80);
                }
                var duration = (int)(quarterMs * _perItemBeats);
                if (ArpeggiateCheck.IsChecked == true)
                {
                    // split chord into small arpeggio notes
                    var steps = midiNotes.Length;
                    var per = Math.Max(30, duration / Math.Max(1, steps));
                    for (int j = 0; j < midiNotes.Length; j++)
                    {
                        token.ThrowIfCancellationRequested();
                        var note = new[] { midiNotes[j] };
                        DiGraphLab.Harmony.Viewer.Playback.SimpleSynth.PlaySemitones(note, durationMs: per);
                        await System.Threading.Tasks.Task.Delay(per, token);
                        // update playhead proportionally
                        UpdatePlayhead(i, list.Length, (double)(j + 1) / Math.Max(1, steps));
                    }
                }
                else
                {
                    DiGraphLab.Harmony.Viewer.Playback.SimpleSynth.PlaySemitones(midiNotes, durationMs: duration);
                    await System.Threading.Tasks.Task.Delay(duration, token);
                    UpdatePlayhead(i, list.Length, 1.0);
                }
            }
            // reset playhead
            UpdatePlayhead(list.Length - 1, list.Length, 1.0);
        }

        private void UpdatePlayhead(int index, int total, double innerFraction)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    double overall = (index + innerFraction) / Math.Max(1, total);
                    PlayheadBar.Value = overall;
                    PlayheadText.Text = $"{index + 1} / {total}";
                });
            }
            catch { }
        }

        // helper to find a chord instance for a node id by looking up via current graph adapter mapping
        private Chord? GraphCanvasControlLoadedNodeById(string id)
        {
            // try to find in the loaded NodeDto array
            if (_loadedNodes != null)
            {
                var found = _loadedNodes.FirstOrDefault(n => n.Id == id);
                if (found != null)
                {
                    return new Chord(found.TraditionalLabel, found.RootPc, found.Quality, found.Inversion, found.PitchClasses);
                }
            }
            // fallback: inspect currently selected chord
            if (_selectedChord != null && _selectedChord.Label == id) return _selectedChord;
            return null;
        }

        private void ExportMidiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ids = GraphCanvasControl.SelectedNodeIds.ToArray();
                if (ids.Length == 0) { MessageBox.Show("No nodes selected to export."); return; }
                // reconstruct chords
                var chords = new List<Chord>();
                foreach (var id in ids)
                {
                    var node = GraphCanvasControlLoadedNodeById(id);
                    if (node != null) chords.Add(node);
                }
                if (chords.Count == 0) { MessageBox.Show("No chords available for export."); return; }
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "MIDI files|*.mid", FileName = "progression.mid" };
                if (dlg.ShowDialog(this) == true)
                {
                    var seq = new List<(int[] notes, int ms)>();
                    var quarterMs = (int)(60000.0 / _tempo);
                    foreach (var c in chords)
                    {
                        var voiced = c.GetVoicedSemitones();
                        int baseMidi = 12 + _playbackOctave * 12 + c.RootPc;
                        var midiNotes = voiced.Select(iv => baseMidi + iv).ToArray();
                        seq.Add((midiNotes, quarterMs));
                    }
                    DiGraphLab.Harmony.Viewer.Playback.MidiExporter.WriteMidiFile(dlg.FileName, seq, _tempo);
                    MessageBox.Show("MIDI exported: " + dlg.FileName);
                }
            }
            catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
        }

        private void NotationCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SendViewerOptions();
        }

        private void KeyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SendViewerOptions();
        }

        private void SendViewerOptions()
        {
            try
            {
                var notationItem = NotationCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
                var notation = notationItem?.Tag?.ToString() ?? "both";
                var keyItem = KeyCombo?.SelectedItem as System.Windows.Controls.ComboBoxItem;
                int tonic = 0;
                if (keyItem != null && int.TryParse(keyItem.Tag?.ToString() ?? "0", out var v)) tonic = v;
                // apply to native canvas (guard in case GraphCanvasControl is not yet initialized)
                GraphCanvasControl?.ApplyOptions(notation, tonic, preferSharps: true);
            }
            catch { }
        }
    }
}
