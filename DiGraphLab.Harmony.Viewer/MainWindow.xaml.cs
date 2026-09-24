using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using DiGraphLab.Harmony;
using DiGraphLab.Harmony.Viewer.Controls;

namespace DiGraphLab.Harmony.Viewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
            GraphCanvasControl.LoadModel(nodes, edges);
            // subscribe to node click events from GraphCanvas
            GraphCanvasControl.NodeClicked += (trad, nash, quality, pcs) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TraditionalText.Text = trad;
                    NashvilleText.Text = nash;
                    QualityText.Text = quality;
                    PitchClassesText.Text = pcs != null ? string.Join(", ", pcs) : "-";
                });
            };
            // apply initial options
            SendViewerOptions();
        }

        // receive node click events from native canvas by wiring selection in code (handled below via GraphCanvas events if needed)

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Playback not implemented in demo.");
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
                var keyItem = KeyCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
                int tonic = 0;
                if (keyItem != null && int.TryParse(keyItem.Tag?.ToString() ?? "0", out var v)) tonic = v;
                // apply to native canvas
                GraphCanvasControl.ApplyOptions(notation, tonic, preferSharps: true);
            }
            catch { }
        }
    }
}
