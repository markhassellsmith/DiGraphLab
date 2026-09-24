using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DiGraphLab.Harmony
{
    public sealed class HarmonyGraph
    {
        public sealed class Node
        {
            public string Id { get; init; } = string.Empty; // canonical Nashville id
            public Chord Representative { get; init; } = null!;
            public int Count { get; set; }
            public HashSet<string> Styles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public sealed class Edge
        {
            public string From { get; init; } = string.Empty;
            public string To { get; init; } = string.Empty;
            public double Weight { get; set; }
            public int Count { get; set; }
        }

        private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<(string, string), Edge> _edges = new();

        public IReadOnlyCollection<Node> Nodes => _nodes.Values;
        public IReadOnlyCollection<Edge> Edges => _edges.Values;

        public HarmonyGraph()
        {
        }

        public string AddChord(Chord chord, int tonicPc, string? style = null, ChordFormatter.Options? fmtOptions = null)
        {
            fmtOptions ??= new ChordFormatter.Options { UseNashville = true, TonicPc = tonicPc, PreferSharps = true };
            var id = ChordFormatter.Format(chord, fmtOptions);
            if (!_nodes.TryGetValue(id, out var node))
            {
                node = new Node { Id = id, Representative = chord, Count = 0 };
                _nodes[id] = node;
            }
            node.Count++;
            if (!string.IsNullOrWhiteSpace(style)) node.Styles.Add(style!);
            return id;
        }

        public void AddEdge(Chord fromChord, Chord toChord, int tonicPc, string? style = null)
        {
            var fmt = new ChordFormatter.Options { UseNashville = true, TonicPc = tonicPc, PreferSharps = true };
            var fromId = AddChord(fromChord, tonicPc, style, fmt);
            var toId = AddChord(toChord, tonicPc, style, fmt);

            var key = (fromId, toId);
            var cost = VoiceLeading.ComputeAverageVoiceLeadingCost(fromChord, toChord);
            if (!_edges.TryGetValue(key, out var edge))
            {
                edge = new Edge { From = fromId, To = toId, Weight = cost, Count = 1 };
                _edges[key] = edge;
            }
            else
            {
                // update running average weight
                edge.Weight = (edge.Weight * edge.Count + cost) / (edge.Count + 1);
                edge.Count++;
            }
        }

        public void ExportJson(string path)
        {
            var model = new
            {
                Nodes = _nodes.Values.Select(n => new
                {
                    n.Id,
                    Representative = new
                    {
                        Traditional = ChordFormatter.Format(n.Representative, new ChordFormatter.Options { PreferSharps = true }),
                        Nashville = ChordFormatter.Format(n.Representative, new ChordFormatter.Options { UseNashville = true, TonicPc = n.Representative.RootPc }),
                        RootPc = n.Representative.RootPc,
                        PitchClasses = n.Representative.PitchClasses,
                        Quality = n.Representative.Quality,
                        Inversion = n.Representative.Inversion
                    },
                    n.Count,
                    Styles = n.Styles.ToArray()
                }).ToArray(),
                Edges = _edges.Values.Select(e => new { e.From, e.To, e.Weight, e.Count }).ToArray()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(model, options);
            File.WriteAllText(path, json);
        }

        public void ExportCsv(string nodesPath, string edgesPath)
        {
            using var ns = new StreamWriter(nodesPath);
            ns.WriteLine("Id,Traditional,Nashville,RootPc,PitchClasses,Quality,Inversion,Count,Styles");
            foreach (var n in _nodes.Values)
            {
                var trad = EscapeCsv(ChordFormatter.Format(n.Representative, new ChordFormatter.Options { PreferSharps = true }));
                var nash = EscapeCsv(ChordFormatter.Format(n.Representative, new ChordFormatter.Options { UseNashville = true, TonicPc = n.Representative.RootPc }));
                var pcs = string.Join("|", n.Representative.PitchClasses.Select(i => i.ToString()));
                var styles = string.Join(";", n.Styles);
                ns.WriteLine($"{EscapeCsv(n.Id)},{trad},{nash},{n.Representative.RootPc},{pcs},{EscapeCsv(n.Representative.Quality)},{n.Representative.Inversion},{n.Count},{EscapeCsv(styles)}");
            }

            using var es = new StreamWriter(edgesPath);
            es.WriteLine("From,To,Weight,Count");
            foreach (var e in _edges.Values)
            {
                es.WriteLine($"{EscapeCsv(e.From)},{EscapeCsv(e.To)},{e.Weight},{e.Count}");
            }
        }

        private static string EscapeCsv(string s)
        {
            if (s == null) return string.Empty;
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            {
                return '"' + s.Replace("\"", "\"\"") + '"';
            }
            return s;
        }
    }
}
