using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DiGraphLab.Harmony
{
    public static class Analytics
    {
        public sealed record NodeStat(string Id, string Label, int Count, int InDegree, int OutDegree, double TotalWeight);
        public sealed record EdgeStat(string From, string To, double Weight, int Count);
        public sealed record GraphSummary(int NodeCount, int EdgeCount, double AvgDegree);

        public static (GraphSummary summary, IEnumerable<NodeStat> nodes, IEnumerable<EdgeStat> edges) ComputeStatistics(HarmonyGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var nodes = graph.Nodes.ToArray();
            var edges = graph.Edges.ToArray();

            var nodeDict = nodes.ToDictionary(n => n.Id, n => n);

            // compute degrees
            var inDeg = nodes.ToDictionary(n => n.Id, n => 0);
            var outDeg = nodes.ToDictionary(n => n.Id, n => 0);
            var totalWeight = nodes.ToDictionary(n => n.Id, n => 0.0);

            foreach (var e in edges)
            {
                if (outDeg.ContainsKey(e.From)) outDeg[e.From]++;
                if (inDeg.ContainsKey(e.To)) inDeg[e.To]++;
                if (totalWeight.ContainsKey(e.From)) totalWeight[e.From] += e.Weight;
            }

            var nodeStats = nodes.Select(n => new NodeStat(
                n.Id,
                n.Representative != null ? ChordFormatter.Format(n.Representative, new ChordFormatter.Options { PreferSharps = true, IncludeBass = true }) : n.Id,
                n.Count,
                inDeg.TryGetValue(n.Id, out var idv) ? idv : 0,
                outDeg.TryGetValue(n.Id, out var odv) ? odv : 0,
                totalWeight.TryGetValue(n.Id, out var tw) ? tw : 0.0
            )).OrderByDescending(s => s.Count).ToArray();

            var edgeStats = edges.Select(e => new EdgeStat(e.From, e.To, e.Weight, e.Count)).OrderByDescending(e => e.Count).ToArray();

            var summary = new GraphSummary(nodes.Length, edges.Length, nodes.Length == 0 ? 0.0 : edges.Length * 1.0 / nodes.Length);

            return (summary, nodeStats, edgeStats);
        }

        public static (string[] nodes, double[,] matrix) ComputeTransitionMatrix(HarmonyGraph graph)
        {
            var nodes = graph.Nodes.Select(n => n.Id).ToArray();
            var index = nodes.Select((id, i) => new { id, i }).ToDictionary(x => x.id, x => x.i);
            var n = nodes.Length;
            var mat = new double[n, n];
            // accumulate counts
            var outCounts = new double[n];
            foreach (var e in graph.Edges)
            {
                if (!index.TryGetValue(e.From, out var i)) continue;
                if (!index.TryGetValue(e.To, out var j)) continue;
                mat[i, j] += e.Count;
                outCounts[i] += e.Count;
            }
            // normalize to probabilities per row
            for (int i = 0; i < n; i++)
            {
                if (outCounts[i] <= 0) continue;
                for (int j = 0; j < n; j++) mat[i, j] = mat[i, j] / outCounts[i];
            }
            return (nodes, mat);
        }

        public static void WriteCsv(TextWriter writer, IEnumerable<NodeStat> nodes, IEnumerable<EdgeStat> edges)
        {
            writer.WriteLine("NodeId,Label,Count,InDegree,OutDegree,TotalWeight");
            foreach (var n in nodes)
            {
                writer.WriteLine($"{Escape(n.Id)},{Escape(n.Label)},{n.Count},{n.InDegree},{n.OutDegree},{n.TotalWeight}");
            }
            writer.WriteLine();
            writer.WriteLine("From,To,Weight,Count");
            foreach (var e in edges)
            {
                writer.WriteLine($"{Escape(e.From)},{Escape(e.To)},{e.Weight},{e.Count}");
            }
        }

        public static void WriteJson(TextWriter writer, GraphSummary summary, IEnumerable<NodeStat> nodes, IEnumerable<EdgeStat> edges)
        {
            var dto = new { Summary = summary, Nodes = nodes, Edges = edges };
            var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            writer.Write(json);
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n')) return '"' + s.Replace("\"", "\"\"") + '"';
            return s;
        }

        // Enumerate simple paths up to a given maxLength. Limit total paths returned with maxPaths.
        public static IEnumerable<string[]> EnumeratePaths(HarmonyGraph graph, int maxLength = 6, int maxPaths = 1000)
        {
            var results = new List<string[]>();
            var nodes = graph.Nodes.Select(n => n.Id).ToArray();
            foreach (var start in nodes)
            {
                var stack = new Stack<List<string>>();
                stack.Push(new List<string> { start });
                while (stack.Count > 0 && results.Count < maxPaths)
                {
                    var path = stack.Pop();
                    results.Add(path.ToArray());
                    if (path.Count >= maxLength) continue;
                    var last = path.Last();
                    var outs = graph.Edges.Where(e => e.From == last).Select(e => e.To).Distinct();
                    foreach (var to in outs)
                    {
                        if (path.Contains(to)) continue; // simple path
                        var np = new List<string>(path) { to };
                        stack.Push(np);
                    }
                }
                if (results.Count >= maxPaths) break;
            }
            return results;
        }

        // Longest common subsequence length based similarity normalized by max length
        public static double SequenceSimilarity(string[] a, string[] b)
        {
            if (a == null || b == null) return 0.0;
            int n = a.Length, m = b.Length;
            if (n == 0 || m == 0) return 0.0;
            var dp = new int[n + 1, m + 1];
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    if (a[i - 1] == b[j - 1]) dp[i, j] = dp[i - 1, j - 1] + 1;
                    else dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
            var lcs = dp[n, m];
            return lcs / (double)Math.Max(n, m);
        }
    }
}
