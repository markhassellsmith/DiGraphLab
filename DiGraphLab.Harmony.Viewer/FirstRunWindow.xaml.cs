using System;
using System.Linq;
using System.IO;
using System.Windows;

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
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_step > 0) _step--;
            UpdateStep();
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
    }
}
