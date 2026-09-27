using System;
using System.Linq;
using System.Windows;

namespace DiGraphLab.Harmony.Viewer
{
    public partial class EditNodeWindow : Window
    {
        public string? NodeId { get; private set; }
        public string? Traditional { get; private set; }
        public string? Nashville { get; private set; }
        public int? RootPc { get; private set; }
        public string? Quality { get; private set; }
        public int? Inversion { get; private set; }
        public int[]? PitchClasses { get; private set; }
        public string[]? Styles { get; private set; }
        public int? Count { get; private set; }
        public bool MergeStyles { get; private set; }

        public EditNodeWindow()
        {
            InitializeComponent();
            // ensure CountText and MergeStylesCheck exist at runtime even if XAML wasn't updated
            try
            {
                // locate main StackPanel inside the Grid (or inside a ScrollViewer)
                var root = this.Content as System.Windows.Controls.Grid;
                if (root != null)
                {
                    System.Windows.Controls.StackPanel mainPanel = null;
                    foreach (var child in root.Children)
                    {
                        if (child is System.Windows.Controls.StackPanel sp) { mainPanel = sp; break; }
                        if (child is System.Windows.Controls.ScrollViewer sv && sv.Content is System.Windows.Controls.StackPanel s2) { mainPanel = s2; break; }
                    }
                    if (mainPanel != null && this.FindName("CountText") == null)
                    {
                        var lbl = new System.Windows.Controls.TextBlock { Text = "Count:", Margin = new Thickness(0, 8, 0, 0) };
                        var ct = new System.Windows.Controls.TextBox { Name = "CountText" };
                        var cb = new System.Windows.Controls.CheckBox { Name = "MergeStylesCheck", Content = "Merge styles with existing", Margin = new Thickness(0, 8, 0, 0) };
                        // register names so FindName can locate them
                        try { this.RegisterName(ct.Name, ct); } catch { }
                        try { this.RegisterName(cb.Name, cb); } catch { }
                        // insert after StylesText if present
                        var stylesBox = mainPanel.Children.OfType<System.Windows.Controls.TextBox>().FirstOrDefault(t => t.Name == "StylesText");
                        var idx = stylesBox != null ? mainPanel.Children.IndexOf(stylesBox) + 1 : mainPanel.Children.Count;
                        mainPanel.Children.Insert(idx, lbl);
                        mainPanel.Children.Insert(idx + 1, ct);
                        mainPanel.Children.Insert(idx + 2, cb);
                    }
                }
            }
            catch { }
        }

        public void LoadFromDto(DiGraphLab.Harmony.GraphAdapter.NodeDto dto)
        {
            NodeId = dto.Id;
            IdText.Text = dto.Id;
            TraditionalText.Text = dto.TraditionalLabel ?? string.Empty;
            NashvilleText.Text = dto.NashvilleLabel ?? string.Empty;
            RootText.Text = dto.RootPc.ToString();
            QualityText.Text = dto.Quality ?? string.Empty;
            InversionText.Text = dto.Inversion.ToString();
            PcsText.Text = dto.PitchClasses != null ? string.Join(",", dto.PitchClasses) : string.Empty;
            StylesText.Text = dto.Styles != null ? string.Join(",", dto.Styles) : string.Empty;
            // populate CountText if control present (lookup by name to avoid generated field dependency)
            try
            {
                var ct = this.FindName("CountText") as System.Windows.Controls.TextBox;
                if (ct != null) ct.Text = dto.Count.ToString();
                var cb = this.FindName("MergeStylesCheck") as System.Windows.Controls.CheckBox;
                if (cb != null) cb.IsChecked = false;
            }
            catch { }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // basic validation
            var errors = new System.Text.StringBuilder();
            Traditional = TraditionalText.Text?.Trim();
            Nashville = NashvilleText.Text?.Trim();
            if (string.IsNullOrWhiteSpace(Traditional) && string.IsNullOrWhiteSpace(Nashville))
                errors.AppendLine("Provide at least a Traditional or Nashville label.");

            if (!int.TryParse(RootText.Text, out var r))
            {
                errors.AppendLine("Root PC must be an integer (0..11).");
                RootPc = null;
            }
            else if (r < 0 || r > 11)
            {
                errors.AppendLine("Root PC must be between 0 and 11.");
                RootPc = null;
            }
            else
            {
                RootPc = r;
            }

            Quality = QualityText.Text?.Trim();

            if (!int.TryParse(InversionText.Text, out var inv))
            {
                // allow empty inversion (treat as null)
                if (string.IsNullOrWhiteSpace(InversionText.Text)) Inversion = null;
                else { errors.AppendLine("Inversion must be an integer >= 0."); Inversion = null; }
            }
            else if (inv < 0)
            {
                errors.AppendLine("Inversion must be >= 0.");
                Inversion = null;
            }
            else
            {
                Inversion = inv;
            }

            // parse pitch classes
            var pcsList = PcsText.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            var pcsParsed = new System.Collections.Generic.List<int>();
            foreach (var s in pcsList)
            {
                if (!int.TryParse(s, out var pc)) { errors.AppendLine($"Invalid pitch-class '{s}'. Use integers 0..11 separated by commas."); continue; }
                if (pc < 0 || pc > 11) { errors.AppendLine($"Pitch-class {pc} out of range (0..11)."); continue; }
                pcsParsed.Add(pc);
            }
            PitchClasses = pcsParsed.ToArray();

            Styles = StylesText.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length>0).ToArray();
            // parse count if present (lookup by name to avoid generated field dependency)
            try
            {
                var ct = this.FindName("CountText") as System.Windows.Controls.TextBox;
                if (ct != null && int.TryParse(ct.Text, out var cnt)) Count = cnt; else Count = null;
                var cb = this.FindName("MergeStylesCheck") as System.Windows.Controls.CheckBox;
                MergeStyles = cb != null && cb.IsChecked == true;
            }
            catch { Count = null; }

            if (errors.Length > 0)
            {
                MessageBox.Show(errors.ToString(), "Invalid node attributes", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
