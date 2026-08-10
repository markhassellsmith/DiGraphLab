using System;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace DiGraphLab
{
    internal static class IconResources
    {
        public static Bitmap Add => _add.Value;
        public static Bitmap Edge => _edge.Value;
        public static Bitmap Import => _import.Value;
        public static Bitmap Export => _export.Value;
        public static Bitmap Matrix => _matrix.Value;

        private static readonly Lazy<Bitmap> _add = new(() => CreateIcon(Color.FromArgb(46, 204, 113), "+"));
        private static readonly Lazy<Bitmap> _edge = new(() => CreateIcon(Color.FromArgb(22, 160, 133), "=>"));
        private static readonly Lazy<Bitmap> _import = new(() => CreateIcon(Color.FromArgb(52, 152, 219), "↓"));
        private static readonly Lazy<Bitmap> _export = new(() => CreateIcon(Color.FromArgb(243, 156, 18), "↑"));
        private static readonly Lazy<Bitmap> _matrix = new(() => CreateIcon(Color.FromArgb(155, 89, 182), "□"));

        private static Bitmap CreateIcon(Color bg, string glyph)
        {
            const int size = 24;
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Transparent);

                var rect = new RectangleF(0.5f, 0.5f, size - 1, size - 1);
                using var path = new GraphicsPath();
                float r = 5f;
                path.AddArc(rect.X, rect.Y, r, r, 180, 90);
                path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
                path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
                path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
                path.CloseFigure();

                using var lg = new LinearGradientBrush(rect, ControlPaint.Light(bg), ControlPaint.Dark(bg), 90f);
                using var pen = new Pen(ControlPaint.Dark(bg)) { Width = 1f };
                g.FillPath(lg, path);
                g.DrawPath(pen, path);

                using var f = new Font(SystemFonts.DefaultFont.FontFamily, 11f, FontStyle.Bold, GraphicsUnit.Pixel);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                var glyphRect = new RectangleF(0, 0, size, size);
                using var brush = new SolidBrush(Color.FromArgb(230, Color.White));
                g.DrawString(glyph, f, brush, glyphRect, sf);
            }

            return bmp;
        }
    }
}
