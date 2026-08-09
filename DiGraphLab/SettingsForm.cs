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
        private Panel _previewPanel;

        public SettingsForm(Settings settings)
        {
            _settings = settings;
            Text = "Settings";
            Width = 400;
            Height = 200;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var lbl = new Label { Text = "Theme:", Left = 10, Top = 10, Width = 50 };
            Controls.Add(lbl);

            _light = new RadioButton { Text = "Light", Left = 70, Top = 10, Width = 80 };
            _dark = new RadioButton { Text = "Dark", Left = 160, Top = 10, Width = 80 };
            Controls.Add(_light);
            Controls.Add(_dark);

            _assignDefaultColor = new CheckBox { Text = "Assign default color to new nodes/edges", Left = 10, Top = 50, Width = 350 };
            Controls.Add(_assignDefaultColor);

            _previewPanel = new Panel { Left = 10, Top = 80, Width = 360, Height = 60, BorderStyle = BorderStyle.FixedSingle };
            _previewPanel.Paint += PreviewPanel_Paint;
            Controls.Add(_previewPanel);

            var ok = new Button { Text = "OK", Left = 200, Width = 80, Top = 110, DialogResult = DialogResult.OK };
            ok.Click += Ok_Click;
            Controls.Add(ok);

            var cancel = new Button { Text = "Cancel", Left = 290, Width = 80, Top = 110, DialogResult = DialogResult.Cancel };
            Controls.Add(cancel);

            // load values
            if (string.Equals(_settings.Theme, "Light", StringComparison.OrdinalIgnoreCase))
                _light.Checked = true;
            else
                _dark.Checked = true;

            _assignDefaultColor.Checked = _settings.AssignDefaultColorToNew;

            // wire change events to update preview
            _light.CheckedChanged += (s, e) => UpdatePreview();
            _dark.CheckedChanged += (s, e) => UpdatePreview();
            _assignDefaultColor.CheckedChanged += (s, e) => UpdatePreview();

            UpdatePreview();
        }

        private void Ok_Click(object? sender, EventArgs e)
        {
            _settings.Theme = _light.Checked ? "Light" : "Dark";
            _settings.AssignDefaultColorToNew = _assignDefaultColor.Checked;
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
            g.DrawString("Node preview", font, Brushes.White, new Rectangle(60, 10, 280, 40), sf);
        }
    }
}
