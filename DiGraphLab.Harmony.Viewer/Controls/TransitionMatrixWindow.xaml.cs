using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace DiGraphLab.Harmony.Viewer.Controls
{
    public partial class TransitionMatrixWindow : Window
    {
        public TransitionMatrixWindow()
        {
            InitializeComponent();
        }

        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (SortCombo.SelectedItem is ComboBoxItem it)
                {
                    var tag = it.Tag?.ToString() ?? "default";
                    if (tag == "default")
                    {
                        if (_currentLabels != null && _currentMatrix != null) SetMatrix(_currentLabels, _currentMatrix);
                    }
                    else if (_currentLabels != null && _currentMatrix != null)
                    {
                        SortBy(tag, _currentLabels, _currentMatrix);
                    }
                }
            }
            catch { }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                var pct = (int)e.NewValue;
                SetZoomPercent(pct);
            }
            catch { }
        }

        private string[]? _currentLabels;
        private double[,]? _currentMatrix;

        public event Action<int,int>? CellClicked;

        private void OnCellClicked(int i, int j)
        {
            try { CellClicked?.Invoke(i, j); } catch { }
        }

        private double _zoom = 1.0;
        public void SetZoomPercent(double pct)
        {
            _zoom = Math.Max(0.5, Math.Min(3.0, pct / 100.0));
            try { MatrixViewbox.LayoutTransform = new ScaleTransform(_zoom, _zoom); } catch { }
        }

        public void SortBy(string mode, string[] labels, double[,] matrix)
        {
            // simple sorts supported: frequency (row sum), outdegree (row nonzero count)
            int n = labels.Length;
            var rowSum = new double[n];
            var rowCount = new int[n];
            for (int i = 0; i < n; i++)
            {
                double s = 0; int c = 0;
                for (int j = 0; j < n; j++) { s += matrix[i, j]; if (matrix[i, j] > 0) c++; }
                rowSum[i] = s; rowCount[i] = c;
            }
            int[] order = Enumerable.Range(0, n).ToArray();
            if (mode == "frequency") order = order.OrderByDescending(i => rowSum[i]).ToArray();
            else if (mode == "outdegree") order = order.OrderByDescending(i => rowCount[i]).ToArray();

            // permute matrix and labels according to order
            var newLabels = order.Select(i => labels[i]).ToArray();
            var newMat = new double[n, n];
            for (int a = 0; a < n; a++) for (int b = 0; b < n; b++) newMat[a, b] = matrix[order[a], order[b]];
            SetMatrix(newLabels, newMat);
        }

        public void SetMatrix(string[] labels, double[,] matrix)
        {
            _currentLabels = labels.ToArray();
            _currentMatrix = (double[,])matrix.Clone();
            MatrixGrid.Items.Clear();
            int n = labels.Length;
            // create header row
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Width = 140 });
            for (int j = 0; j < n; j++)
            {
                header.Children.Add(new TextBlock { Text = labels[j], Width = 60, TextAlignment = TextAlignment.Center, ToolTip = labels[j] });
            }
            MatrixGrid.Items.Add(header);

            for (int i = 0; i < n; i++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = labels[i], Width = 140, ToolTip = labels[i] });
                for (int j = 0; j < n; j++)
                {
                    var v = matrix[i, j];
                    var rect = new Border { Width = 60, Height = 24, Margin = new Thickness(1), Tag = (i, j) };
                    byte intensity = (byte)(Math.Min(1.0, v) * 255);
                    rect.Background = new SolidColorBrush(Color.FromRgb((byte)(255 - intensity), (byte)(255 - intensity), 255));
                    var tb = new TextBlock { Text = v.ToString("0.00"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    rect.Child = tb;
                    rect.MouseLeftButtonUp += (s, e) => OnCellClicked(i, j);
                    row.Children.Add(rect);
                }
                MatrixGrid.Items.Add(row);
            }
        }
    }
}
