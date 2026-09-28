using System;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;

namespace DiGraphLab.Harmony.Viewer
{
    public partial class FirstRunWindow : Window
    {
        private int _step = 0;
        private readonly string[] _steps = new[] {
            "Click 'Load Demo' on the toolbar to populate a sample graph.",
            "Click any node on the canvas to populate the Inspector at the right.",
            "Use the Inspector fields (Quality, Pitch Classes) and click 'Apply' to update the node.",
            "Shift+Click two nodes and click 'Create Edge' to add a directed edge."
        };

        public FirstRunWindow()
        {
            InitializeComponent();
            UpdateStep();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DontShowCheck.IsChecked == true)
                {
                    // persist a simple flag in local settings file
                    var cfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firstrun.flag");
                    try { File.WriteAllText(cfg, "hidden"); } catch { }
                }
            }
            catch { }
            this.Close();
        }

        private void OpenGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var repo = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                var guide = Path.Combine(repo, "DiGraphLab_User_Guide.md");
                if (File.Exists(guide)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = guide, UseShellExecute = true });
            }
            catch { }
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_step < _steps.Length - 1) _step++;
            UpdateStep();
            // show highlight for current step
            ClearHighlight();
            switch (_step)
            {
                case 0:
                    ShowHighlight("LoadDemoButton", _steps[_step]);
                    break;
                case 1:
                    ShowHighlight("GraphCanvasControl", _steps[_step]);
                    break;
                case 2:
                    ShowHighlight("Inspector_Quality", _steps[_step]);
                    break;
                case 3:
                    ShowHighlight("CreateEdgeButton", _steps[_step]);
                    break;
                default:
                    ClearHighlight();
                    break;
            }
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_step > 0) _step--;
            UpdateStep();
            ClearHighlight();
            switch (_step)
            {
                case 0:
                    ShowHighlight("LoadDemoButton", _steps[_step]);
                    break;
                case 1:
                    ShowHighlight("GraphCanvasControl", _steps[_step]);
                    break;
                case 2:
                    ShowHighlight("Inspector_Quality", _steps[_step]);
                    break;
                case 3:
                    ShowHighlight("CreateEdgeButton", _steps[_step]);
                    break;
                default:
                    ClearHighlight();
                    break;
            }
        }

        private void UpdateStep()
        {
            try
            {
                var panel = this.Content as System.Windows.Controls.Grid;
                if (panel == null) return;
                var textBlocks = panel.Children.OfType<System.Windows.Controls.TextBlock>().ToArray();
                if (textBlocks.Length > 1)
                {
                    // the second TextBlock was the long description area; replace its text with the current step
                    textBlocks[1].Text = _steps[_step];
                }
                BackBtn.IsEnabled = _step > 0;
                NextBtn.IsEnabled = _step < _steps.Length - 1;
            }
            catch { }
        }

        // highlight helper: show an adorner on the owner window for a named target
        private HighlightAdorner? _currentAdorner;
        private void ShowHighlight(string elementName, string tip)
        {
            try
            {
                if (this.Owner is not MainWindow main) return;
                var target = main.FindName(elementName) as System.Windows.FrameworkElement;
                if (target == null) return;
                var layer = AdornerLayer.GetAdornerLayer((Visual)main.Content!);
                if (layer == null) return;
                ClearHighlight();
                _currentAdorner = new HighlightAdorner(target, tip);
                layer.Add(_currentAdorner);
                // update on layout changes
                target.LayoutUpdated += Target_LayoutUpdated;
            }
            catch { }
        }

        private void Target_LayoutUpdated(object? sender, EventArgs e)
        {
            try { _currentAdorner?.InvalidateVisual(); } catch { }
        }

        private void ClearHighlight()
        {
            try
            {
                if (_currentAdorner != null)
                {
                    if (this.Owner is MainWindow main && main.Content is Visual root)
                    {
                        var layer = AdornerLayer.GetAdornerLayer(root);
                        if (layer != null) layer.Remove(_currentAdorner);
                    }
                    _currentAdorner = null;
                }
            }
            catch { }
        }
    }
}
