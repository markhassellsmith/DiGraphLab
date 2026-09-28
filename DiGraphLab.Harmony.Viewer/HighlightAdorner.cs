using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DiGraphLab.Harmony.Viewer
{
    // Lightweight adorner that draws a soft highlighted rectangle around the adorned element
    // and a short label box to the right. Non-interactive (IsHitTestVisible = false).
    public class HighlightAdorner : Adorner
    {
        private readonly string _text;
        private readonly Brush _fillBrush = new SolidColorBrush(Color.FromArgb(48, 255, 165, 0)); // semi-transparent orange
        private readonly Pen _borderPen = new Pen(Brushes.OrangeRed, 3.0);

        public HighlightAdorner(UIElement adornedElement, string text) : base(adornedElement)
        {
            _text = text ?? string.Empty;
            IsHitTestVisible = false;
            _fillBrush.Freeze();
            _borderPen.Freeze();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            try
            {
                var size = AdornedElement.RenderSize;
                var rect = new Rect(new Point(0, 0), size);

                // draw a soft filled rounded rectangle and a thicker border
                drawingContext.DrawRoundedRectangle(_fillBrush, _borderPen, rect, 8, 8);

                // prepare label text
                var dpi = VisualTreeHelper.GetDpi(this);
                var ft = new FormattedText(_text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 12, Brushes.White, dpi.PixelsPerDip);

                var labelPadding = new Thickness(6, 4, 6, 4);
                var labelW = ft.Width + labelPadding.Left + labelPadding.Right;
                var labelH = ft.Height + labelPadding.Top + labelPadding.Bottom;

                // draw the label to the right of the target rect if space allows; otherwise draw above
                var offsetX = rect.Right + 12;
                var offsetY = rect.Top;
                var labelRect = new Rect(offsetX, offsetY, labelW, labelH);

                // if label would go off the adorner layer, try placing above
                var adornerLayer = this.Parent as AdornerLayer;
                if (adornerLayer != null)
                {
                    var layerSize = new Size(adornerLayer.RenderSize.Width, adornerLayer.RenderSize.Height);
                    if (labelRect.Right > layerSize.Width - 8)
                    {
                        labelRect = new Rect(Math.Max(8, rect.Left), Math.Max(8, rect.Top - labelH - 8), labelW, labelH);
                    }
                }

                var labelBrush = new SolidColorBrush(Color.FromArgb(220, 255, 140, 0));
                labelBrush.Freeze();
                drawingContext.DrawRoundedRectangle(labelBrush, null, labelRect, 6, 6);
                drawingContext.DrawText(ft, new Point(labelRect.Left + labelPadding.Left, labelRect.Top + labelPadding.Top));

                // small arrow from label to rect
                var arrowStart = new Point(labelRect.Left, labelRect.Top + labelRect.Height / 2);
                var arrowEnd = new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(arrowStart, false, false);
                    ctx.LineTo(arrowEnd, true, true);
                }
                drawingContext.DrawGeometry(null, new Pen(Brushes.White, 1.0), g);
            }
            catch { }
        }
    }
}
