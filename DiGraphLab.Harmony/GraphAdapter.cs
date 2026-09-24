using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGraphLab.Harmony
{
    /// <summary>
    /// Adapter converting a HarmonyGraph into generic node/edge DTOs suitable for UI consumption or integration with a generic DiGraph.
    /// </summary>
    public static class GraphAdapter
    {
        public sealed class NodeDto
        {
            public string Id { get; init; } = string.Empty;
            public string TraditionalLabel { get; init; } = string.Empty;
            public string NashvilleLabel { get; init; } = string.Empty;
            public int RootPc { get; init; }
            public IReadOnlyList<int> PitchClasses { get; init; } = Array.Empty<int>();
            public string Quality { get; init; } = string.Empty;
            public int Inversion { get; init; }
            public int Count { get; init; }
            public string[] Styles { get; init; } = Array.Empty<string>();
        }

        public sealed class EdgeDto
        {
            public string From { get; init; } = string.Empty;
            public string To { get; init; } = string.Empty;
            public double Weight { get; init; }
            public int Count { get; init; }
        }

        public static (IEnumerable<NodeDto> nodes, IEnumerable<EdgeDto> edges) Convert(HarmonyGraph graph, ChordFormatter.Options? fmtOptions = null)
        {
            fmtOptions ??= new ChordFormatter.Options { PreferSharps = true, IncludeBass = true };

            var nodes = graph.Nodes.Select(n =>
            {
                var optTrad = new ChordFormatter.Options
                {
                    PreferSharps = fmtOptions.PreferSharps,
                    IncludeBass = fmtOptions.IncludeBass,
                    UseNashville = false,
                    ShowExtensions = fmtOptions.ShowExtensions,
                    TonicPc = fmtOptions.TonicPc
                };
                var optNash = new ChordFormatter.Options
                {
                    PreferSharps = fmtOptions.PreferSharps,
                    IncludeBass = fmtOptions.IncludeBass,
                    UseNashville = true,
                    ShowExtensions = fmtOptions.ShowExtensions,
                    TonicPc = n.Representative.RootPc
                };
                return new NodeDto
                {
                    Id = n.Id,
                    TraditionalLabel = ChordFormatter.Format(n.Representative, optTrad),
                    NashvilleLabel = ChordFormatter.Format(n.Representative, optNash),
                    RootPc = n.Representative.RootPc,
                    PitchClasses = n.Representative.PitchClasses,
                    Quality = n.Representative.Quality,
                    Inversion = n.Representative.Inversion,
                    Count = n.Count,
                    Styles = n.Styles.ToArray()
                };
            }).ToArray();

            var edges = graph.Edges.Select(e => new EdgeDto
            {
                From = e.From,
                To = e.To,
                Weight = e.Weight,
                Count = e.Count
            }).ToArray();

            return (nodes, edges);
        }
    }
}
