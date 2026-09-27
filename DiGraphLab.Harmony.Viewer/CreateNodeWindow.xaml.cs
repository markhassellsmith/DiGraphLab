using System;
using System.Linq;
using System.Windows;

namespace DiGraphLab.Harmony.Viewer
{
    public partial class CreateNodeWindow : Window
    {
        public CreateNodeWindow()
        {
            InitializeComponent();
        }

        public CreateNodeResult Result { get; private set; } = new CreateNodeResult();

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Result.Id = IdText.Text?.Trim() ?? Guid.NewGuid().ToString();
                Result.Label = Result.Id;
                Result.RootPc = int.TryParse(RootText.Text, out var r) ? r % 12 : 0;
                Result.Quality = QualityText.Text ?? string.Empty;
                Result.Inversion = int.TryParse(InversionText.Text, out var inv) ? inv : 0;
                Result.PitchClasses = (PcsText.Text ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s.Trim(), out var v) ? v % 12 : 0).ToArray();
                Result.Style = StyleText.Text;
                DialogResult = true;
                Close();
            }
            catch (Exception ex) { MessageBox.Show("Invalid input: " + ex.Message); }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class CreateNodeResult
    {
        public string Id { get; set; } = string.Empty;
        public string? Label { get; set; }
        public int RootPc { get; set; }
        public string? Quality { get; set; }
        public int Inversion { get; set; }
        public int[] PitchClasses { get; set; } = Array.Empty<int>();
        public string? Style { get; set; }
    }
}
