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

        private void ReloadGraphButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_lastImportedGraphPath)) { MessageBox.Show("No graph file recorded to reload."); return; }
                if (!File.Exists(_lastImportedGraphPath)) { MessageBox.Show("Graph file not found: " + _lastImportedGraphPath); UpdateReloadButtonState(); return; }
                var svc = new HarmonyService();
                svc.Graph.ImportJson(_lastImportedGraphPath);
                var (nodes, edges) = GraphAdapter.Convert(svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                _loadedNodes = nodes.ToArray();
                GraphCanvasControl.LoadModel(nodes, edges);
                PopulateAnalysis(svc.Graph);
                SendViewerOptions();
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
            catch { ReloadGraphButton.IsEnabled = false; }
        }

        public MainWindow()
        {
            InitializeComponent();
            // subscribe to canvas zoom changes to update toolbar display
            GraphCanvasControl.ZoomChanged += z => { try { Dispatcher.Invoke(() => ToolbarZoomText.Text = ((int)(z * 100)).ToString() + "%"); } catch { } };
        }

        private void ImportGraphButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Harmony Graph JSON|*.json;*.graph.json|All Files|*.*" };
            if (dlg.ShowDialog(this) != true) return;
            var path = dlg.FileName;
            try
            {
                var svc = new HarmonyService();
                svc.Graph.ImportJson(path);
                var (nodes, edges) = GraphAdapter.Convert(svc.Graph, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true });
                _loadedNodes = nodes.ToArray();
                GraphCanvasControl.LoadModel(nodes, edges);
                PopulateAnalysis(svc.Graph);
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
            var svc = new HarmonyService();
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
                    TraditionalText.Text = trad;
                    NashvilleText.Text = nash;
                    QualityText.Text = quality;
                    PitchClassesText.Text = pcs != null ? string.Join(", ", pcs) : "-";
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
                if (ids.Length == 0)
                {
                    MessageBox.Show("No nodes selected. Use Shift+Click to build a sequence.");
                    return;
                }
                // reconstruct chords in the order selected; prefer explicit sequence editor if loaded
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
                await PlayChordSequenceAsync(chords, _playbackOctave, _tempo, _playCts.Token);
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
