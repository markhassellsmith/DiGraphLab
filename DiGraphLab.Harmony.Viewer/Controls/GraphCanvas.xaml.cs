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
        private readonly DispatcherTimer _timer;
        private List<Node> _nodes = new();
        private List<Edge> _edges = new();
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

        private void ResetBounds()
        {
            // ensure nodes remain within bounds
            var w = PART_Canvas.ActualWidth; var h = PART_Canvas.ActualHeight;
            foreach (var n in _nodes)
            {
                n.X = Math.Max(20, Math.Min(w - 20, n.X));
                n.Y = Math.Max(20, Math.Min(h - 20, n.Y));
            }
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
                X = _rand.NextDouble() * Math.Max(100, PART_Canvas.ActualWidth - 200) + 100,
                Y = _rand.NextDouble() * Math.Max(100, PART_Canvas.ActualHeight - 200) + 100
            }).ToList();

            _edges = edges.Select(e => new Edge { From = e.From, To = e.To }).ToList();

            // create visuals
            foreach (var e in _edges)
            {
                var line = new Line { Stroke = Brushes.Gray, StrokeThickness = 1.2, Opacity = 0.9 };
                PART_Canvas.Children.Add(line);
                e.Element = line;
            }

            foreach (var n in _nodes)
            {
                var g = new Canvas { Width = 120, Height = 36, RenderTransformOrigin = new Point(0.5, 0.5) };
                var rect = new Rectangle { Width = 120, Height = 36, RadiusX = 6, RadiusY = 6, Fill = Brushes.White, Stroke = Brushes.DarkSlateGray };
                var txt = new TextBlock { Text = n.Label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Width = 110 };
                Canvas.SetLeft(txt, 5); Canvas.SetTop(txt, 6);
                g.Children.Add(rect); g.Children.Add(txt);
                PART_Canvas.Children.Add(g);
                n.Element = g;

                // pointer events for dragging
                bool dragging = false; Point offset = default;
                g.MouseLeftButtonDown += (s, e) => { dragging = true; offset = e.GetPosition(PART_Canvas); offset.X = n.X - offset.X; offset.Y = n.Y - offset.Y; g.CaptureMouse(); };
                g.MouseMove += (s, e) => { if (!dragging) return; var p = e.GetPosition(PART_Canvas); n.X = p.X + offset.X; n.Y = p.Y + offset.Y; n.Vx = 0; n.Vy = 0; };
                g.MouseLeftButtonUp += (s, e) => { var wasDragging = dragging; dragging = false; try { g.ReleaseMouseCapture(); } catch { } ;
                    // treat as click if it was not a drag movement
                    if (!wasDragging)
                    {
                        OnNodeClicked(n);
                    }
                };
            }
        }

        public event Action<string, string, string, int[]>? NodeClicked;

        private void OnNodeClicked(Node n)
        {
            try
            {
                NodeClicked?.Invoke(n.Traditional, n.Nashville, n.Quality, n.PitchClasses);
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
                if (e.Element is Line line)
                {
                    var a = _nodes.FirstOrDefault(n => n.Id == e.From);
                    var b = _nodes.FirstOrDefault(n => n.Id == e.To);
                    if (a == null || b == null) continue;
                    line.X1 = a.X; line.Y1 = a.Y; line.X2 = b.X; line.Y2 = b.Y;
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
