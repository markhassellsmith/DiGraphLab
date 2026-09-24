using System;
using System.Drawing;
using System.Windows.Forms;

namespace DiGraphLab
{
    public class SettingsForm : Form
    {
        private readonly Settings _settings;
        private RadioButton _light;
        private RadioButton _dark;
        private CheckBox _assignDefaultColor;
        private CheckBox _autoScaleLabels;
        private NumericUpDown _occupancy;
        private NumericUpDown _minFont;
        private NumericUpDown _maxFont;
        private NumericUpDown _maxLabelChars;
        private Panel _previewPanel;

        public SettingsForm(Settings settings)
        {
            _settings = settings;
            Text = "Settings";
            ClientSize = new Size(560, 320);
            MinimumSize = new Size(560, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Padding = new Padding(10);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var themeRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            var lbl = new Label { Text = "Theme:", AutoSize = true, Margin = new Padding(0, 7, 8, 0) };
            themeRow.Controls.Add(lbl);

            _light = new RadioButton { Text = "Light", AutoSize = true, Margin = new Padding(0, 4, 10, 0) };
            _dark = new RadioButton { Text = "Dark", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            themeRow.Controls.Add(_light);
            themeRow.Controls.Add(_dark);
            root.Controls.Add(themeRow, 0, 0);

            _assignDefaultColor = new CheckBox { Text = "Assign default color to new nodes/edges", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            root.Controls.Add(_assignDefaultColor, 0, 1);
            _autoScaleLabels = new CheckBox { Text = "Auto-scale node labels", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            root.Controls.Add(_autoScaleLabels, 0, 2);

            var numericGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };
            numericGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            numericGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            numericGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            numericGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var lblOcc = new Label { Text = "Occupancy (0-1):", AutoSize = true, Margin = new Padding(0, 7, 8, 0) };
            _occupancy = new NumericUpDown { Width = 90, DecimalPlaces = 2, Increment = 0.05M, Minimum = 0.05M, Maximum = 0.9M };
            var lblMin = new Label { Text = "Min font:", AutoSize = true, Margin = new Padding(16, 7, 8, 0) };
            _minFont = new NumericUpDown { Width = 90, Minimum = 4, Maximum = 24 };

            var lblMax = new Label { Text = "Max font:", AutoSize = true, Margin = new Padding(0, 7, 8, 0) };
            _maxFont = new NumericUpDown { Width = 90, Minimum = 6, Maximum = 48 };
            var lblMaxChars = new Label { Text = "Max label chars:", AutoSize = true, Margin = new Padding(16, 7, 8, 0) };
            _maxLabelChars = new NumericUpDown { Width = 90, Minimum = 10, Maximum = 200 };

            numericGrid.Controls.Add(lblOcc, 0, 0);
            numericGrid.Controls.Add(_occupancy, 1, 0);
            numericGrid.Controls.Add(lblMin, 2, 0);
            numericGrid.Controls.Add(_minFont, 3, 0);
            numericGrid.Controls.Add(lblMax, 0, 1);
            numericGrid.Controls.Add(_maxFont, 1, 1);
            numericGrid.Controls.Add(lblMaxChars, 2, 1);
            numericGrid.Controls.Add(_maxLabelChars, 3, 1);
            root.Controls.Add(numericGrid, 0, 3);

            _previewPanel = new Panel { Dock = DockStyle.Fill, Height = 80, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 10, 0, 0) };
            _previewPanel.Paint += PreviewPanel_Paint;
            root.Controls.Add(_previewPanel, 0, 4);

            var buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 10, 0, 0)
            };

            var ok = new Button { Text = "OK", Width = 90, DialogResult = DialogResult.OK };
            ok.Click += Ok_Click;
            var cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
            buttonRow.Controls.Add(cancel);
            buttonRow.Controls.Add(ok);
            root.Controls.Add(buttonRow, 0, 5);

            AcceptButton = ok;
            CancelButton = cancel;

            // load values
            if (string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase))
                _light.Checked = true;
            else
                _dark.Checked = true;

            _assignDefaultColor.Checked = _settings.AssignDefaultColorToNew;
            _autoScaleLabels.Checked = _settings.AutoScaleNodeLabels;
            _occupancy.Value = (decimal)_settings.OccupancyFactor;
            _minFont.Value = _settings.MinFontSize;
            _maxFont.Value = _settings.MaxFontSize;
            _maxLabelChars.Value = _settings.MaxLabelChars;

            // wire change events to update preview
            _light.CheckedChanged += (s, e) => UpdatePreview();
            _dark.CheckedChanged += (s, e) => UpdatePreview();
            _assignDefaultColor.CheckedChanged += (s, e) => UpdatePreview();
            _autoScaleLabels.CheckedChanged += (s, e) => UpdatePreview();
            _occupancy.ValueChanged += (s, e) => UpdatePreview();
            _minFont.ValueChanged += (s, e) => UpdatePreview();
            _maxFont.ValueChanged += (s, e) => UpdatePreview();
            _maxLabelChars.ValueChanged += (s, e) => UpdatePreview();

            UpdatePreview();
        }

        private void Ok_Click(object? sender, EventArgs e)
        {
            _settings.Theme = _light.Checked ? "Light" : "Dark";
            _settings.AssignDefaultColorToNew = _assignDefaultColor.Checked;
            _settings.AutoScaleNodeLabels = _autoScaleLabels.Checked;
            _settings.OccupancyFactor = (double)_occupancy.Value;
            _settings.MinFontSize = (int)_minFont.Value;
            _settings.MaxFontSize = (int)_maxFont.Value;
            _settings.MaxLabelChars = (int)_maxLabelChars.Value;
            _settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdatePreview()
        {
            _previewPanel.Invalidate();
        }

        private void PreviewPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_light.Checked ? Color.White : Color.FromArgb(30, 30, 30));

            // draw a sample node circle showing default color if enabled
            Color bg = _light.Checked ? Color.White : Color.FromArgb(30, 30, 30);
            Color nodeColor;
            if (_assignDefaultColor.Checked)
            {
                nodeColor = Color.FromArgb(255 - bg.R, 255 - bg.G, 255 - bg.B);
            }
            else
            {
                nodeColor = Color.Gray;
            }

            var rect = new Rectangle(10, 10, 40, 40);
            using var brush = new SolidBrush(nodeColor);
            g.FillEllipse(brush, rect);
            using var pen = new Pen(Color.Black);
            g.DrawEllipse(pen, rect);

            // label
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
            using var font = new Font(FontFamily.GenericSansSerif, 9);
            var labelBrush = _light.Checked ? Brushes.Black : Brushes.White;
            g.DrawString("Node preview", font, labelBrush, new Rectangle(60, 10, Math.Max(100, _previewPanel.Width - 70), 40), sf);
        }
    }
}
