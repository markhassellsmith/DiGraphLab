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
        }
    }
}
