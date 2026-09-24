using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiGraphLab.Harmony
{
    /// <summary>
    /// Service that manages a HarmonyGraph and exposes utility operations for UI or other consumers.
    /// </summary>
    public sealed class HarmonyService
    {
        private readonly HarmonyGraph _graph = new HarmonyGraph();

        public HarmonyGraph Graph => _graph;

        public HarmonyService()
        {
        }

        public string AddChord(Chord chord, int tonicPc, string? style = null, ChordFormatter.Options? fmtOptions = null)
        {
            return _graph.AddChord(chord, tonicPc, style, fmtOptions);
        }

        public void AddProgression(IEnumerable<Chord> progression, int tonicPc, string? style = null)
        {
            Chord? prev = null;
            foreach (var chord in progression)
            {
                var id = _graph.AddChord(chord, tonicPc, style);
                if (prev != null)
                {
                    _graph.AddEdge(prev, chord, tonicPc, style);
                }
                prev = chord;
            }
        }

        public void AddEdge(Chord from, Chord to, int tonicPc, string? style = null)
        {
            _graph.AddEdge(from, to, tonicPc, style);
        }

        public void ExportJson(string path)
        {
            _graph.ExportJson(path);
        }

        public void ExportCsv(string nodesPath, string edgesPath)
        {
            _graph.ExportCsv(nodesPath, edgesPath);
        }

        /// <summary>
        /// Produce a new HarmonyGraph with every representative chord transposed by semitones.
        /// This is a utility to create a transposed copy; the original graph is unchanged.
        /// </summary>
        public HarmonyGraph TransposeGraph(int semitoneShift)
        {
            var result = new HarmonyGraph();
            // transpose each node's representative chord and re-add with same style tags
            foreach (var node in _graph.Nodes)
            {
                var rep = node.Representative;
                var transposedPcs = rep.PitchClasses.Select(pc => (pc + semitoneShift) % 12).ToArray();
                var transposed = new Chord(rep.Label, rep.RootPc + semitoneShift, rep.Quality, rep.Inversion, transposedPcs, preserveDoubling: true);
                // use tonic shift equal to semitoneShift when formatting Nashville ids; caller can choose tonic when using adapter
                result.AddChord(transposed, Mod12(rep.RootPc + semitoneShift));
            }
            // re-add edges by mapping ids from original to transposed forms using representative root shift
            foreach (var edge in _graph.Edges)
            {
                // find original nodes
                if (!_graph.Nodes.Any(n => n.Id == edge.From) || !_graph.Nodes.Any(n => n.Id == edge.To))
                    continue;
                // attempt to find corresponding transposed nodes by matching representative pitch-classes shifted
                var fromNode = _graph.Nodes.First(n => n.Id == edge.From);
                var toNode = _graph.Nodes.First(n => n.Id == edge.To);
                var fromTransPcs = fromNode.Representative.PitchClasses.Select(pc => (pc + semitoneShift) % 12).OrderBy(i => i).ToArray();
                var toTransPcs = toNode.Representative.PitchClasses.Select(pc => (pc + semitoneShift) % 12).OrderBy(i => i).ToArray();
                // find matching nodes in result
                var fromMatch = result.Nodes.FirstOrDefault(n => n.Representative.PitchClasses.SequenceEqual(fromTransPcs));
                var toMatch = result.Nodes.FirstOrDefault(n => n.Representative.PitchClasses.SequenceEqual(toTransPcs));
                if (fromMatch != null && toMatch != null)
                {
                    // add edge using representative chords
                    result.AddEdge(fromMatch.Representative, toMatch.Representative, Mod12(fromMatch.Representative.RootPc));
                }
            }
            return result;
        }

        private static int Mod12(int x) => ((x % 12) + 12) % 12;
    }
}
