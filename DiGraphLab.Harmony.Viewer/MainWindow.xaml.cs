using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using DiGraphLab.Harmony;

namespace DiGraphLab.Harmony.Viewer
{
    public partial class MainWindow : Window
    {
        private WebView2? _webViewControl;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _webViewControl = WebView;
            try
            {
                await _webViewControl.EnsureCoreWebView2Async();
            }
            catch (Exception)
            {
                // ignore; WebView2 may not be available in some environments
            }
            try
            {
                _webViewControl.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            }
            catch { }
        }

        private async void LoadDemoButton_Click(object sender, RoutedEventArgs e)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var outDir = Path.Combine(desktop, "DiGraphLabViewerDemo");
            Directory.CreateDirectory(outDir);
            Demo.RunSample(outDir);

            // read the exported JSON
            var jsonPath = Path.Combine(outDir, "harmony-demo.json");
            string json = File.Exists(jsonPath) ? File.ReadAllText(jsonPath) : "{}";

            // navigate to local viewer.html
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var viewerPath = Path.Combine(exeDir, "viewer.html");
            if (!File.Exists(viewerPath))
            {
                MessageBox.Show($"viewer.html not found at {viewerPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var uri = new Uri(viewerPath);
            _webViewControl!.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.local", exeDir, CoreWebView2HostResourceAccessKind.Allow);
            _webViewControl.CoreWebView2.Navigate("https://appassets.local/viewer.html");

            // wait for navigation and then post the graph JSON
            _webViewControl.CoreWebView2.NavigationCompleted += (_, __) =>
            {
                try
                {
                    _webViewControl.CoreWebView2.PostWebMessageAsJson(json);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to post graph JSON to viewer: " + ex.Message);
                }
            };
            // send initial options based on current UI selections
            SendViewerOptions();
        }

        private void CoreWebView2_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.WebMessageAsJson;
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("type", out var typeEl))
                {
                    var t = typeEl.GetString();
                    if (t == "nodeClick")
                    {
                        var node = doc.RootElement.GetProperty("node");
                        var rep = node.GetProperty("Representative");
                        var trad = rep.GetProperty("Traditional").GetString() ?? "-";
                        var nash = rep.GetProperty("Nashville").GetString() ?? "-";
                        var quality = rep.GetProperty("Quality").GetString() ?? "-";
                        string pcs = "-";
                        if (rep.TryGetProperty("PitchClasses", out var pcsEl) && pcsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var arr = pcsEl.EnumerateArray();
                            pcs = string.Join(", ", arr.Select(x => x.GetInt32().ToString()));
                        }
                        Dispatcher.Invoke(() =>
                        {
                            TraditionalText.Text = trad;
                            NashvilleText.Text = nash;
                            QualityText.Text = quality;
                            PitchClassesText.Text = pcs;
                        });
                    }
                }
            }
            catch { }
        }

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
            if (_webViewControl?.CoreWebView2 == null) return;
            try
            {
                var notationItem = NotationCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
                var notation = notationItem?.Tag?.ToString() ?? "both";
                var keyItem = KeyCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
                int tonic = 0;
                if (keyItem != null && int.TryParse(keyItem.Tag?.ToString() ?? "0", out var v)) tonic = v;
                var opts = new { type = "options", notation = notation, tonicPc = tonic, preferSharps = true };
                var json = System.Text.Json.JsonSerializer.Serialize(opts);
                _webViewControl.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }
    }
}
