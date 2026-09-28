using System;
using System.Windows;

namespace DiGraphLab.Harmony.Viewer
{
    public partial class FirstRunWindow : Window
    {
        public FirstRunWindow()
        {
            InitializeComponent();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DontShowCheck.IsChecked == true)
                {
                    // persist a simple flag in local settings file
                    var cfg = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firstrun.flag");
                    try { System.IO.File.WriteAllText(cfg, "hidden"); } catch { }
                }
            }
            catch { }
            this.Close();
        }

        private void OpenGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var repo = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                var guide = System.IO.Path.Combine(repo, "DiGraphLab_User_Guide.md");
                if (System.IO.File.Exists(guide)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = guide, UseShellExecute = true });
            }
            catch { }
        }
    }
}
