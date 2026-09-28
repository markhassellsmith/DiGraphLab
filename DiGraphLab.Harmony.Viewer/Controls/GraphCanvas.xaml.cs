using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DiGraphLab.Harmony;

namespace DiGraphLab.Harmony.Viewer.Controls
{
    public partial class GraphCanvas : UserControl
    {
        // arrow and rendering constants
        private const double ArrowPadding = 2.0; // gap between arrow tip and node boundary
        private const double DefaultArrowLength = 14.0;
        private const double ArrowWidthRatio = 0.5; // arrow width relative to length
        private const double CurvatureOffsetDefault = 36.0; // default curvature for bidirectional edges
        private readonly DispatcherTimer _timer;
        private List<Node> _nodes = new();
        private List<Edge> _edges = new();
        private System.Collections.Generic.Dictionary<string, Node> _nodeById = new();
        private readonly Random _rand = new();

        public GraphCanvas()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += Timer_Tick;
            Loaded += (_, __) => { _timer.Start(); };
            Unloaded += (_, __) => { _timer.Stop(); };
            SizeChanged += (_, __) => ResetBounds();
        }

        /// <summary>
        /// Update the visual representation for a single node using the provided NodeDto.
        /// This updates the label and background brush without reloading the entire model.
        /// </summary>
        public void UpdateNodeVisual(string id, GraphAdapter.NodeDto dto)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || dto == null) return;
                if (!_nodeById.TryGetValue(id, out var node)) return;
                // update model fields
                var oldCount = node.Count;
                node.Traditional = dto.TraditionalLabel;
                node.Nashville = dto.NashvilleLabel;
                node.Quality = dto.Quality;
                node.PitchClasses = dto.PitchClasses.ToArray();
                node.RootPc = dto.RootPc;
                node.Inversion = dto.Inversion;
                node.Count = dto.Count;
                node.Styles = dto.Styles ?? Array.Empty<string>();

                // update visual: the node.Element is a Canvas with a Rectangle and TextBlock(s)
                if (node.Element is Canvas g)
                {
                    var rect = g.Children.OfType<Rectangle>().FirstOrDefault();
                    var tb = g.Children.OfType<TextBlock>().FirstOrDefault();
                    if (rect != null)
                    {
                        rect.Fill = GetBrushForQuality(node.Quality, node.Traditional);
                    }
                    if (tb != null)
                    {
                        tb.Text = node.Traditional + "\n" + node.Nashville;
                    }
                    // update count badge text (second textblock) and ensure badge visibility
                    var badgeTxt = g.Children.OfType<TextBlock>().Skip(1).FirstOrDefault();
                    var badgeEllipse = g.Children.OfType<Ellipse>().FirstOrDefault();
                    if (badgeTxt != null)
                    {
                        badgeTxt.Text = node.Count.ToString();
                    }
                    // animate badge when count changed
                    try
                    {
                        if (badgeEllipse != null && oldCount != node.Count)
                        {
                            // ensure a ScaleTransform exists
                            if (!(badgeEllipse.RenderTransform is ScaleTransform))
                            {
                                badgeEllipse.RenderTransform = new ScaleTransform(1.0, 1.0);
                                badgeEllipse.RenderTransformOrigin = new Point(0.5, 0.5);
                            }
                            var st = badgeEllipse.RenderTransform as ScaleTransform;
                            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, 1.4, new Duration(TimeSpan.FromMilliseconds(140))) { AutoReverse = true };
                            st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                            st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                        }
                    }
                    catch { }
                    // update style markers (stackpanel of rectangles)
                    var stylePanel = g.Children.OfType<StackPanel>().FirstOrDefault();
                    if (stylePanel != null)
                    {
                        stylePanel.Children.Clear();
                        int shown = 0;
                        foreach (var s in node.Styles)
                        {
                            if (shown++ >= 4) break;
                            var mark = new Rectangle { Width = 10, Height = 10, RadiusX = 2, RadiusY = 2, Margin = new Thickness(2, 0, 2, 0), Stroke = Brushes.Black, StrokeThickness = 0.5 };
                            mark.Fill = GetBrushForQuality(node.Quality, s ?? node.Quality);
                            stylePanel.Children.Add(mark);
                        }
                    }
                }
            }
            catch { }
        }

        // allow external callers to highlight/select nodes by id (used by matrix window)
        public void HighlightNodes(params string[] ids)
        {
            try
            {
                ClearSelection();
                Node? first = null;
                foreach (var id in ids.Where(x => !string.IsNullOrEmpty(x)))
                {
                    var n = _nodes.FirstOrDefault(x => x.Id == id);
                    if (n == null) continue;
                    _selectedIds.Add(n.Id);
                    if (n.Element is Canvas g)
                    {
                        var rect = g.Children.OfType<Rectangle>().FirstOrDefault();
                        if (rect != null) rect.Stroke = Brushes.OrangeRed;
                    }
                    if (first == null) first = n;
                }
                if (first != null) HighlightNode(first);
            }
            catch { }
        }

        // panning state
        private bool _isPanning = false;
        private Point _panStart;
        private double _panStartX, _panStartY;
        // selection-zoom state (Ctrl+Left-Drag)
        private bool _isSelecting = false;
        private Point _selectionStart;
        private Rectangle? _selectionRect;

        // zoom/translate state
        private double _zoom = 1.0;
        private const double ZoomStep = 1.15;
        // track last canvas size to preserve positions across resizes
        private double _lastCanvasWidth = 0.0;
        private double _lastCanvasHeight = 0.0;

        private void ResetBounds()
        {
            // ensure nodes remain within bounds and preserve relative positions across resizes
            var w = PART_Canvas.ActualWidth; var h = PART_Canvas.ActualHeight;
            if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0) return;

            // expose mapping for quick lookup by id (used by UpdateNodeVisual)
            _nodeById = _nodes.ToDictionary(x => x.Id);

            // if we have a previous canvas size, scale node positions to preserve layout
            if (_lastCanvasWidth > 0 && _lastCanvasHeight > 0 && (_lastCanvasWidth != w || _lastCanvasHeight != h))
            {
                var sx = w / _lastCanvasWidth; var sy = h / _lastCanvasHeight;
                // scale positions
                foreach (var n in _nodes)
                {
                    n.X = n.X * sx;
                    n.Y = n.Y * sy;
                }
            }

            // clamp to safe margins
            foreach (var n in _nodes)
            {
                n.X = Math.Max(20, Math.Min(w - 20, n.X));
                n.Y = Math.Max(20, Math.Min(h - 20, n.Y));
            }

            // record current size for next resize event
            _lastCanvasWidth = w; _lastCanvasHeight = h;
        }

        // helper to get half extents for a node element
        private double GetHalfWidth(Node node)
        {
            if (node.Element is FrameworkElement fe)
            {
                // prefer ActualWidth (rendered size) then Width property
                if (!double.IsNaN(fe.ActualWidth) && fe.ActualWidth > 0) return fe.ActualWidth / 2.0;
                if (!double.IsNaN(fe.Width) && fe.Width > 0) return fe.Width / 2.0;
            }
            return 60.0; // default half-width (120/2)
        }

        private double GetHalfHeight(Node node)
        {
            if (node.Element is FrameworkElement fe)
            {
                if (!double.IsNaN(fe.ActualHeight) && fe.ActualHeight > 0) return fe.ActualHeight / 2.0;
                if (!double.IsNaN(fe.Height) && fe.Height > 0) return fe.Height / 2.0;
            }
            return 18.0; // default half-height (36/2)
        }

        // compute a point on the boundary of src's rectangle towards dst
        private System.Windows.Point ComputeBoundaryPoint(Node src, Node dst)
        {
            double ax = src.X, ay = src.Y, bx = dst.X, by = dst.Y;
            var dx = bx - ax; var dy = by - ay; var len = Math.Sqrt(dx * dx + dy * dy);
            if (len <= 0.001) return new System.Windows.Point(ax, ay);
            var ux = dx / len; var uy = dy / len;
            var ha = GetHalfWidth(src); var va = GetHalfHeight(src);
            double sx = double.PositiveInfinity; double sy = double.PositiveInfinity;
            if (Math.Abs(ux) > 1e-6) sx = ha / Math.Abs(ux);
            if (Math.Abs(uy) > 1e-6) sy = va / Math.Abs(uy);
            var tRectA = Math.Min(sx, sy);
            var srcX = ax + ux * tRectA;
            var srcY = ay + uy * tRectA;
            return new System.Windows.Point(srcX, srcY);
        }

        public void LoadModel(IEnumerable<GraphAdapter.NodeDto> nodes, IEnumerable<GraphAdapter.EdgeDto> edges)
        {
            PART_Canvas.Children.Clear();
            _nodes = nodes.Select(n => new Node
            {
                Id = n.Id,
                Label = n.TraditionalLabel + "\n" + n.NashvilleLabel,
                Traditional = n.TraditionalLabel,
                Nashville = n.NashvilleLabel,
                Quality = n.Quality,
                PitchClasses = n.PitchClasses.ToArray(),
                RootPc = n.RootPc,
                Inversion = n.Inversion,
                Count = n.Count,
                Styles = n.Styles ?? Array.Empty<string>(),
                X = _rand.NextDouble() * Math.Max(100, PART_Canvas.ActualWidth - 200) + 100,
                Y = _rand.NextDouble() * Math.Max(100, PART_Canvas.ActualHeight - 200) + 100
            }).ToList();

            _edges = edges.Select(e => new Edge { From = e.From, To = e.To }).ToList();

            // create visuals
            for (int ei = 0; ei < _edges.Count; ei++)
            {
                var e = _edges[ei];
                // detect reciprocal edge for curved rendering
                bool hasReverse = _edges.Any(ev => ev.From == e.To && ev.To == e.From);
                var container = new Canvas { Width = 0, Height = 0, IsHitTestVisible = false };
                if (hasReverse)
                {
                    // curved path for bidirectional edge
                    var path = new Path { Stroke = Brushes.Gray, StrokeThickness = 1.2, Opacity = 0.9, IsHitTestVisible = false };
                    var arrow = new Polygon { Fill = Brushes.Gray, Stroke = Brushes.Gray, StrokeThickness = 1.0, IsHitTestVisible = false };
                    container.Children.Add(path);
                    container.Children.Add(arrow);
                }
                else
                {
                    var line = new Line { Stroke = Brushes.Gray, StrokeThickness = 1.2, Opacity = 0.9, IsHitTestVisible = false };
                    var arrow = new Polygon { Fill = Brushes.Gray, Stroke = Brushes.Gray, StrokeThickness = 1.0, IsHitTestVisible = false };
                    container.Children.Add(line);
                    container.Children.Add(arrow);
                }
                PART_Canvas.Children.Add(container);
                e.Element = container;
            }

            foreach (var n in _nodes)
            {
                var g = new Canvas { Width = 120, Height = 36, RenderTransformOrigin = new Point(0.5, 0.5) };
                // color by normalized quality key if available, otherwise fall back to Quality text
                var fill = GetBrushForQuality(n.Quality, n.Styles.FirstOrDefault() ?? n.Quality);
                var rect = new Rectangle { Width = 120, Height = 36, RadiusX = 6, RadiusY = 6, Fill = fill, Stroke = Brushes.DarkSlateGray, StrokeThickness = 1.2 };
                var txt = new TextBlock { Text = n.Label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Width = 110 };
                Canvas.SetLeft(txt, 5); Canvas.SetTop(txt, 6);
                // decoration: count badge (top-right) and style markers (bottom-left)
                var badge = new Ellipse { Width = 20, Height = 20, Fill = Brushes.CadetBlue, Stroke = Brushes.White, StrokeThickness = 1.0, IsHitTestVisible = false };
                var badgeTxt = new TextBlock { Text = n.Count.ToString(), Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.Bold, Width = 20, TextAlignment = TextAlignment.Center };
                Canvas.SetRight(badge, 4); Canvas.SetTop(badge, 4);
                Canvas.SetRight(badgeTxt, 4); Canvas.SetTop(badgeTxt, 6);
                var stylePanel = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
                Canvas.SetLeft(stylePanel, 6); Canvas.SetBottom(stylePanel, 4);
                // create small rectangles for each style (up to 4 shown)
                int shown = 0;
                foreach (var s in n.Styles)
                {
                    if (shown++ >= 4) break;
                    var mark = new Rectangle { Width = 10, Height = 10, RadiusX = 2, RadiusY = 2, Margin = new Thickness(2, 0, 2, 0), Stroke = Brushes.Black, StrokeThickness = 0.5 };
                    mark.Fill = GetBrushForQuality(n.Quality, s ?? n.Quality);
                    stylePanel.Children.Add(mark);
                }
                g.Children.Add(rect); g.Children.Add(txt); g.Children.Add(badge); g.Children.Add(badgeTxt); g.Children.Add(stylePanel);
                PART_Canvas.Children.Add(g);
                n.Element = g;

                // pointer events for dragging
                bool dragging = false; Point offset = default;
                g.MouseLeftButtonDown += (s, e) => { dragging = true; offset = e.GetPosition(PART_Canvas); offset.X = n.X - offset.X; offset.Y = n.Y - offset.Y; g.CaptureMouse(); };
                g.MouseMove += (s, e) => { if (!dragging) return; var p = e.GetPosition(PART_Canvas); n.X = p.X + offset.X; n.Y = p.Y + offset.Y; n.Vx = 0; n.Vy = 0; };
                g.MouseLeftButtonUp += (s, e) => {
                    var wasDragging = dragging;
                    dragging = false;
                    try { g.ReleaseMouseCapture(); } catch { }
                    // treat as click if it was not a drag movement
                    if (!wasDragging)
                    {
                        // Shift+Click toggles multi-selection without changing the inspector
                        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
                        {
                            ToggleSelectNode(n);
                        }
                        else
                        {
                            // single click should select only this node (clear others) so selection-order reflects click order
                            try { ClearSelection(); } catch { }
                            _selectedIds.Add(n.Id);
                            if (n.Element is Canvas gg)
                            {
                                var rect = gg.Children.OfType<Rectangle>().FirstOrDefault();
                                if (rect != null) rect.Stroke = Brushes.OrangeRed;
                            }
                            HighlightNode(n);
                            OnNodeClicked(n);
                        }
                    }
                };
                // allow right-button drag on nodes to pan the whole canvas
                g.MouseRightButtonDown += (s, e) => { _isPanning = true; _panStart = e.GetPosition(this); _panStartX = PART_Translate.X; _panStartY = PART_Translate.Y; g.CaptureMouse(); };
                g.MouseRightButtonUp += (s, e) => { _isPanning = false; try { g.ReleaseMouseCapture(); } catch { } };
                g.MouseMove += (s, e) => { if (!_isPanning) return; var p = e.GetPosition(this); PART_Translate.X = _panStartX + (p.X - _panStart.X); PART_Translate.Y = _panStartY + (p.Y - _panStart.Y); };
            }

            // apply current zoom
            ApplyZoomTransform();
        }

        private void HighlightNode(Node n)
        {
            // clear previous highlights
            foreach (var node in _nodes)
            {
                if (node.Element is Canvas g)
                {
                    var rect = g.Children.OfType<Rectangle>().FirstOrDefault();
                    if (rect != null)
                    {
                        rect.StrokeThickness = 1.2;
                        rect.Stroke = Brushes.DarkSlateGray;
                    }
                    g.RenderTransform = null;
                    Panel.SetZIndex(g, 0);
                    g.Effect = null;
                }
            }
            // highlight this one
            if (n.Element is Canvas gg)
            {
                var rect = gg.Children.OfType<Rectangle>().FirstOrDefault();
                if (rect != null) rect.StrokeThickness = 3.0;
                rect.Stroke = Brushes.OrangeRed;
                gg.RenderTransform = new ScaleTransform(1.06, 1.06);
                // elevate and add subtle shadow
                Panel.SetZIndex(gg, 10);
                gg.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 8, Opacity = 0.4, Direction = 270, ShadowDepth = 4 };
                // quick pulse animation to draw attention
                try
                {
                    var st = new System.Windows.Media.Animation.DoubleAnimation(1.0, 1.12, new Duration(TimeSpan.FromMilliseconds(180))) { AutoReverse = true };
                    gg.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, st);
                    gg.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, st);
                }
                catch { }
            }
        }

        // multi-selection support (Shift+Click to add/remove)
        private readonly List<string> _selectedIds = new();
        public IReadOnlyList<string> SelectedNodeIds => _selectedIds;

        // helper to return selected ids as a string array for external callers
        public string[] GetSelectedNodeIds()
        {
            try
            {
                return _selectedIds.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public void ClearSelection()
        {
            _selectedIds.Clear();
            // clear visual selection markers
            foreach (var node in _nodes)
            {
                if (node.Element is Canvas g)
                {
                    var rect = g.Children.OfType<Rectangle>().FirstOrDefault();
                    if (rect != null) rect.Stroke = Brushes.DarkSlateGray;
                }
            }
        }

        private void ToggleSelectNode(Node n)
        {
            if (_selectedIds.Contains(n.Id))
            {
                _selectedIds.Remove(n.Id);
                if (n.Element is Canvas g) { var rect = g.Children.OfType<Rectangle>().FirstOrDefault(); if (rect != null) { rect.Stroke = Brushes.DarkSlateGray; rect.StrokeThickness = 1.2; } }
            }
            else
            {
                _selectedIds.Add(n.Id);
                if (n.Element is Canvas g) { var rect = g.Children.OfType<Rectangle>().FirstOrDefault(); if (rect != null) { rect.Stroke = Brushes.OrangeRed; rect.StrokeThickness = 3.0; } }
            }
        }

        private Brush GetBrushForQuality(string q, string fallback)
        {
            var key = (q ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key)) key = (fallback ?? string.Empty).ToLowerInvariant();
            return key switch
            {
                var k when k.Contains("maj") || k == "maj" => Brushes.LightSteelBlue,
                var k when k.Contains("min") || k == "min" => Brushes.LightSalmon,
                var k when k.Contains("dim") || k == "dim" => Brushes.LightGray,
                var k when k.Contains("aug") || k == "aug" => Brushes.LightGoldenrodYellow,
                var k when k.Contains("7") || k == "7" => Brushes.MediumPurple,
                _ => Brushes.White,
            };
        }

        // handle mouse wheel for zoom (on the control)
        protected override void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            try
            {
                var pos = e.GetPosition(this);
                if (e.Delta > 0) SetZoom(_zoom * ZoomStep, pos);
                else SetZoom(_zoom / ZoomStep, pos);
                e.Handled = true;
            }
            catch { }
        }

        protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            // start selection-zoom when Ctrl is held and left button is pressed
            try
            {
                if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                {
                    _isSelecting = true;
                    _selectionStart = e.GetPosition(this);
                    if (_selectionRect == null)
                    {
                        _selectionRect = new Rectangle { Stroke = Brushes.Black, StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 2 }, Fill = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)) };
                        Panel.SetZIndex(_selectionRect, 1000);
                        PART_Container.Children.Add(_selectionRect);
                    }
                    Canvas.SetLeft(_selectionRect, _selectionStart.X);
                    Canvas.SetTop(_selectionRect, _selectionStart.Y);
                    _selectionRect.Width = 0; _selectionRect.Height = 0;
                    CaptureMouse();
                    e.Handled = true;
                }
            }
            catch { }
        }

        protected override void OnPreviewMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            try
            {
                if (_isSelecting && _selectionRect != null)
                {
                    var pt = e.GetPosition(this);
                    var x = Math.Min(pt.X, _selectionStart.X);
                    var y = Math.Min(pt.Y, _selectionStart.Y);
                    var w = Math.Abs(pt.X - _selectionStart.X);
                    var h = Math.Abs(pt.Y - _selectionStart.Y);
                    Canvas.SetLeft(_selectionRect, x);
                    Canvas.SetTop(_selectionRect, y);
                    _selectionRect.Width = w; _selectionRect.Height = h;
                    e.Handled = true;
                }
            }
            catch { }
        }

        protected override void OnPreviewMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            try
            {
                if (_isSelecting && _selectionRect != null)
                {
                    var rectLeft = Canvas.GetLeft(_selectionRect);
                    var rectTop = Canvas.GetTop(_selectionRect);
                    var rectW = _selectionRect.Width;
                    var rectH = _selectionRect.Height;
                    // remove visual
                    try { PART_Container.Children.Remove(_selectionRect); } catch { }
                    _selectionRect = null;
                    _isSelecting = false;
                    ReleaseMouseCapture();
                    // only zoom if selection is large enough
                    if (rectW > 8 && rectH > 8)
                    {
                        ZoomToRectangle(new Rect(rectLeft, rectTop, rectW, rectH));
                    }
                    e.Handled = true;
                }
            }
            catch { }
        }

        private void ZoomToRectangle(Rect screenRect)
        {
            try
            {
                // ensure we have valid view size
                var viewW = PART_Canvas.ActualWidth; var viewH = PART_Canvas.ActualHeight;
                if (viewW <= 0 || viewH <= 0) return;
                var rectW = screenRect.Width; var rectH = screenRect.Height;
                if (rectW <= 4 || rectH <= 4) return;
                var sOld = _zoom;
                var scaleFactor = Math.Min(viewW / rectW, viewH / rectH);
                var sNew = sOld * scaleFactor;
                sNew = Math.Max(0.1, Math.Min(4.0, sNew));
                // compute center in screen coords
                var cx = screenRect.Left + screenRect.Width / 2.0;
                var cy = screenRect.Top + screenRect.Height / 2.0;
                // content center in content coords
                var contentCx = (cx - PART_Translate.X) / sOld;
                var contentCy = (cy - PART_Translate.Y) / sOld;
                // new translate so content center maps to view center
                var newTx = (viewW / 2.0) - contentCx * sNew;
                var newTy = (viewH / 2.0) - contentCy * sNew;
                _zoom = sNew;
                ApplyZoomTransform();
                PART_Translate.X = newTx; PART_Translate.Y = newTy;
                try { ZoomChanged?.Invoke(_zoom); } catch { }
            }
            catch { }
        }

        // Zoom / Fit methods

        public event Action<double>? ZoomChanged;

        public double CurrentZoom => _zoom;

        public void SetZoom(double scale) => SetZoom(scale, null);

        public void ZoomIn() => SetZoom(_zoom * ZoomStep, null);
        public void ZoomOut() => SetZoom(_zoom / ZoomStep, null);

        private void SetZoom(double scale, Point? center)
        {
            var old = _zoom;
            scale = Math.Max(0.1, Math.Min(4.0, scale));
            if (center != null && PART_Translate != null)
            {
                // adjust translation so the point under 'center' remains fixed (screen = scale * content + translate)
                try
                {
                    var sOld = old;
                    var sNew = scale;
                    if (sOld <= 0) sOld = 1.0;
                    var cx = center.Value.X; var cy = center.Value.Y;
                    var tx = PART_Translate.X; var ty = PART_Translate.Y;
                    var factor = sNew / sOld;
                    var newTx = cx - factor * (cx - tx);
                    var newTy = cy - factor * (cy - ty);
                    PART_Translate.X = newTx; PART_Translate.Y = newTy;
                }
                catch { }
            }
            _zoom = scale;
            ApplyZoomTransform();
            try { ZoomChanged?.Invoke(_zoom); } catch { }
        }

        private void ApplyZoomTransform()
        {
            if (PART_Scale != null) { PART_Scale.ScaleX = _zoom; PART_Scale.ScaleY = _zoom; }
        }

        public void FitToView()
        {
            if (_nodes == null || _nodes.Count == 0) { SetZoom(1.0); PART_Translate.X = 0; PART_Translate.Y = 0; return; }
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var n in _nodes)
            {
                minX = Math.Min(minX, n.X - 80); maxX = Math.Max(maxX, n.X + 80);
                minY = Math.Min(minY, n.Y - 30); maxY = Math.Max(maxY, n.Y + 30);
            }
            var contentW = maxX - minX; var contentH = maxY - minY;
            var viewW = PART_Canvas.ActualWidth; var viewH = PART_Canvas.ActualHeight;
            if (viewW <= 0 || viewH <= 0) return;
            var scaleX = viewW / (contentW + 40); var scaleY = viewH / (contentH + 40);
            var targetScale = Math.Min(Math.Min(scaleX, scaleY), 2.5);
            SetZoom(Math.Max(0.3, targetScale));
            // center
            var centerX = (minX + maxX) / 2.0; var centerY = (minY + maxY) / 2.0;
            PART_Translate.X = (viewW / 2.0) - centerX * _zoom;
            PART_Translate.Y = (viewH / 2.0) - centerY * _zoom;
        }

        public event Action<string, string, string, int[], int, int>? NodeClicked;

        private void OnNodeClicked(Node n)
        {
            try
            {
                NodeClicked?.Invoke(n.Traditional, n.Nashville, n.Quality, n.PitchClasses, n.RootPc, n.Inversion);
            }
            catch { }
        }

        public void ApplyOptions(string notation, int tonicPc, bool preferSharps)
        {
            // update labels based on notation preference
            foreach (var n in _nodes)
            {
                if (notation == "traditional") n.Label = n.Traditional;
                else if (notation == "nashville") n.Label = n.Nashville;
                else n.Label = n.Traditional + "\n" + n.Nashville;
                // update visual text if available
                if (n.Element is Canvas g)
                {
                    var txt = g.Children.OfType<TextBlock>().FirstOrDefault();
                    if (txt != null) txt.Text = n.Label;
                }
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Simulate();
            Render();
        }

        private void Simulate()
        {
            var w = PART_Canvas.ActualWidth; var h = PART_Canvas.ActualHeight;
            if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0) return;
            // reset forces
            foreach (var n in _nodes) { n.Fx = 0; n.Fy = 0; }
            // repulsion
            for (int i = 0; i < _nodes.Count; i++)
            {
                for (int j = i + 1; j < _nodes.Count; j++)
                {
                    var a = _nodes[i]; var b = _nodes[j];
                    var dx = a.X - b.X; var dy = a.Y - b.Y; var dist2 = dx * dx + dy * dy + 0.01; var dist = Math.Sqrt(dist2);
                    var force = 4000.0 / dist2;
                    var fx = (dx / dist) * force; var fy = (dy / dist) * force;
                    a.Fx += fx; a.Fy += fy; b.Fx -= fx; b.Fy -= fy;
                }
            }
            // springs
            foreach (var e in _edges)
            {
                var a = _nodes.FirstOrDefault(n => n.Id == e.From);
                var b = _nodes.FirstOrDefault(n => n.Id == e.To);
                if (a == null || b == null) continue;
                var dx = b.X - a.X; var dy = b.Y - a.Y; var dist = Math.Sqrt(dx * dx + dy * dy) + 0.01;
                var desired = 120.0; var k = 0.02; var fs = k * (dist - desired);
                var fx = (dx / dist) * fs; var fy = (dy / dist) * fs;
                a.Fx += fx; a.Fy += fy; b.Fx -= fx; b.Fy -= fy;
            }
            // integrate
            foreach (var n in _nodes)
            {
                n.Vx = (n.Vx + n.Fx * 0.1) * 0.85; n.Vy = (n.Vy + n.Fy * 0.1) * 0.85;
                n.X += n.Vx; n.Y += n.Vy;
                // bounds
                n.X = Math.Max(20, Math.Min(w - 20, n.X)); n.Y = Math.Max(20, Math.Min(h - 20, n.Y));
            }
        }

        private void Render()
        {
            // edges first
            foreach (var e in _edges)
            {
                // support container with line + arrow polygon (directed edge)
                if (e.Element is Canvas container)
                {
                    var a = _nodes.FirstOrDefault(n => n.Id == e.From);
                    var b = _nodes.FirstOrDefault(n => n.Id == e.To);
                    if (a == null || b == null) continue;
                    var line = container.Children.OfType<Line>().FirstOrDefault();
                    var arrow = container.Children.OfType<Polygon>().FirstOrDefault();
                    if (line != null)
                    {
                        // compute intersection points with node rectangles so the line and arrowhead meet node boundaries
                        double ax = a.X, ay = a.Y, bx = b.X, by = b.Y;
                        var dx = bx - ax; var dy = by - ay; var len = Math.Sqrt(dx * dx + dy * dy);
                        if (len <= 0.001) continue;
                        var ux = dx / len; var uy = dy / len;

                        // use class helpers to get half extents

                        // compute target intersection (point on boundary of b's rectangle)
                        var hb = GetHalfWidth(b); var vb = GetHalfHeight(b);
                        double tx = double.PositiveInfinity;
                        double ty = double.PositiveInfinity;
                        if (Math.Abs(ux) > 1e-6) tx = hb / Math.Abs(ux);
                        if (Math.Abs(uy) > 1e-6) ty = vb / Math.Abs(uy);
                        var tRectB = Math.Min(tx, ty);
                        // tip point on b boundary
                        var tipX = bx - ux * tRectB;
                        var tipY = by - uy * tRectB;

                        // compute source intersection (point on boundary of a's rectangle)
                        var ha = GetHalfWidth(a); var va = GetHalfHeight(a);
                        double sx = double.PositiveInfinity;
                        double sy = double.PositiveInfinity;
                        if (Math.Abs(ux) > 1e-6) sx = ha / Math.Abs(ux);
                        if (Math.Abs(uy) > 1e-6) sy = va / Math.Abs(uy);
                        var tRectA = Math.Min(sx, sy);
                        var srcX = ax + ux * tRectA;
                        var srcY = ay + uy * tRectA;

                        // set line endpoints to rectangle boundary points (add tiny padding)
                        const double pad = 2.0;
                        line.X1 = srcX + ux * pad; line.Y1 = srcY + uy * pad;
                        line.X2 = tipX - ux * pad; line.Y2 = tipY - uy * pad;
                    }
                    if (arrow != null)
                    {
                        // if container has a Path child, it's a curved reciprocal edge; otherwise straight arrow
                        var path = container.Children.OfType<Path>().FirstOrDefault();
                        if (path != null)
                        {
                            // curved quadratic Bezier from src boundary to target boundary
                            var aPt = ComputeBoundaryPoint(a, b);
                            var bPt = ComputeBoundaryPoint(b, a);
                            // midpoint and perpendicular for control point
                            var mx = (aPt.X + bPt.X) / 2.0; var my = (aPt.Y + bPt.Y) / 2.0;
                            var dx = bPt.X - aPt.X; var dy = bPt.Y - aPt.Y; var len = Math.Sqrt(dx * dx + dy * dy);
                            if (len <= 0.001) continue;
                            var ux = dx / len; var uy = dy / len; var px = -uy; var py = ux;
                            var offset = CurvatureOffsetDefault;
                            // choose offset sign based on hash to avoid exact overlap both ways
                            var sign = (a.Id + ":" + b.Id).GetHashCode() % 2 == 0 ? 1.0 : -1.0;
                            var cx = mx + px * offset * sign; var cy = my + py * offset * sign;

                            var fig = new PathFigure { StartPoint = new System.Windows.Point(aPt.X, aPt.Y) };
                            var quad = new QuadraticBezierSegment { Point1 = new System.Windows.Point(cx, cy), Point2 = new System.Windows.Point(bPt.X, bPt.Y), IsStroked = true };
                            fig.Segments.Clear(); fig.Segments.Add(quad); fig.IsClosed = false;
                            var geo = new PathGeometry(); geo.Figures.Add(fig);
                            path.Data = geo;

                            // arrow tip direction along tangent from control to end
                            var tx = bPt.X; var ty = bPt.Y;
                            var tdx = tx - cx; var tdy = ty - cy; var tlen = Math.Sqrt(tdx * tdx + tdy * tdy);
                            if (tlen <= 0.001) continue;
                            var tux = tdx / tlen; var tuy = tdy / tlen;
                            double arrowLen = DefaultArrowLength; double arrowWidth = arrowLen * ArrowWidthRatio;
                            var baseX = tx - tux * arrowLen; var baseY = ty - tuy * arrowLen;
                            var p1 = new System.Windows.Point(tx, ty);
                            var p2 = new System.Windows.Point(baseX + -tuy * arrowWidth, baseY + tux * arrowWidth);
                            var p3 = new System.Windows.Point(baseX - -tuy * arrowWidth, baseY - tux * arrowWidth);
                            arrow.Points = new System.Windows.Media.PointCollection { p1, p2, p3 };
                        }
                        else
                        {
                            // straight arrow already handled above for line endpoints; here recompute tip at b boundary
                            double ax = a.X, ay = a.Y, bx = b.X, by = b.Y;
                            var dx = bx - ax; var dy = by - ay; var len = Math.Sqrt(dx * dx + dy * dy);
                            if (len <= 0.001) continue;
                            var ux = dx / len; var uy = dy / len; var px = -uy; var py = ux;
                            double arrowLen = DefaultArrowLength; double arrowWidth = arrowLen * ArrowWidthRatio;
                            var hb = GetHalfWidth(b); var vb = GetHalfHeight(b);
                            double tx = double.PositiveInfinity; double ty = double.PositiveInfinity;
                            if (Math.Abs(ux) > 1e-6) tx = hb / Math.Abs(ux);
                            if (Math.Abs(uy) > 1e-6) ty = vb / Math.Abs(uy);
                            var tRectB = Math.Min(tx, ty);
                            var tipX = bx - ux * tRectB; var tipY = by - uy * tRectB;
                            var baseX = tipX - ux * arrowLen; var baseY = tipY - uy * arrowLen;
                            var p1 = new System.Windows.Point(tipX, tipY);
                            var p2 = new System.Windows.Point(baseX + px * arrowWidth, baseY + py * arrowWidth);
                            var p3 = new System.Windows.Point(baseX - px * arrowWidth, baseY - py * arrowWidth);
                            arrow.Points = new System.Windows.Media.PointCollection { p1, p2, p3 };
                        }
                    }
                }
            }
            // nodes
            foreach (var n in _nodes)
            {
                if (n.Element is Canvas g)
                {
                    Canvas.SetLeft(g, n.X - g.Width / 2);
                    Canvas.SetTop(g, n.Y - g.Height / 2);
                }
            }
        }

        // simple DTOs for internal use
        private class Node
        {
            public string Id = string.Empty;
            public string Label = string.Empty;
            public string Traditional = string.Empty;
            public string Nashville = string.Empty;
            public string Quality = string.Empty;
            public int RootPc = 0;
            public int Inversion = 0;
            public int Count = 0;
            public string[] Styles = Array.Empty<string>();
            public int[] PitchClasses = Array.Empty<int>();
            public double X, Y, Vx, Vy, Fx, Fy;
            public FrameworkElement? Element;
        }

        private class Edge
        {
            public string From = string.Empty; public string To = string.Empty; public FrameworkElement? Element;
        }
    }
}
