using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiGraphLab.Core;

public class DirectedGraph
{
    private readonly Dictionary<Guid, Vertex> _vertices = new();
    private readonly Dictionary<Guid, Edge> _edges = new();

    public IReadOnlyCollection<Vertex> Vertices => _vertices.Values.ToList().AsReadOnly();

    public IReadOnlyCollection<Edge> Edges => _edges.Values.ToList().AsReadOnly();

    public Vertex CreateVertex(string label)
    {
        if (label is null) throw new ArgumentNullException(nameof(label));

        var v = new Vertex(label);
        // assign 1-based ordinal: max existing + 1
        try
        {
            var max = _vertices.Values.Select(x => x.Ordinal).DefaultIfEmpty(0).Max();
            v.Ordinal = max + 1;
        }
        catch { v.Ordinal = 1; }
        _vertices[v.Id] = v;
        // invoke optional provider for default vertex color
        if (VertexColorProvider != null && string.IsNullOrEmpty(v.Color))
        {
            try { v.Color = VertexColorProvider(v.Id); } catch { }
        }
        return v;
    }

    public void AddVertex(Vertex v)
    {
        if (v is null) throw new ArgumentNullException(nameof(v));
        if (_vertices.ContainsKey(v.Id)) throw new ArgumentException("Vertex with same id already exists", nameof(v));
        // if ordinal not set, assign next
        if (v.Ordinal <= 0)
        {
            var max = _vertices.Values.Select(x => x.Ordinal).DefaultIfEmpty(0).Max();
            v.Ordinal = max + 1;
        }
        _vertices[v.Id] = v;
    }

    public (Vertex? vertex, List<Edge> removedEdges) RemoveVertex(Guid id)
    {
        if (!_vertices.TryGetValue(id, out var v))
            return (null, new List<Edge>());

        // Remove incident edges
        var incident = _edges.Values.Where(e => e.Source.Id == id || e.Target.Id == id).ToList();
        foreach (var edge in incident)
        {
            _edges.Remove(edge.Id);
        }

        _vertices.Remove(id);
        return (v, incident);
    }

    public Edge CreateEdge(Guid sourceId, Guid targetId, string label = "", string? color = null)
    {
        if (!_vertices.TryGetValue(sourceId, out var source))
            throw new ArgumentException("Source vertex not found", nameof(sourceId));

        if (!_vertices.TryGetValue(targetId, out var target))
            throw new ArgumentException("Target vertex not found", nameof(targetId));

        var e = new Edge(source, target, label ?? string.Empty);
        try
        {
            var max = _edges.Values.Select(x => x.Ordinal).DefaultIfEmpty(0).Max();
            e.Ordinal = max + 1;
        }
        catch { e.Ordinal = 1; }
        if (!string.IsNullOrEmpty(color))
            e.Color = color;
        else if (EdgeColorProvider != null)
        {
            try { e.Color = EdgeColorProvider(sourceId, targetId); } catch { }
        }
        _edges[e.Id] = e;
        return e;
    }

    // Optional color providers that UI can set to inject default colors for newly created items
    public Func<Guid, string?>? VertexColorProvider { get; set; }
    public Func<Guid, Guid, string?>? EdgeColorProvider { get; set; }

    public void AddEdge(Edge e)
    {
        if (e is null) throw new ArgumentNullException(nameof(e));
        if (!_vertices.ContainsKey(e.Source.Id) || !_vertices.ContainsKey(e.Target.Id))
            throw new ArgumentException("Source or target vertex not found in graph", nameof(e));
        if (_edges.ContainsKey(e.Id)) throw new ArgumentException("Edge with same id already exists", nameof(e));
        if (e.Ordinal <= 0)
        {
            var max = _edges.Values.Select(x => x.Ordinal).DefaultIfEmpty(0).Max();
            e.Ordinal = max + 1;
        }
        _edges[e.Id] = e;
    }

    public Edge? RemoveEdge(Guid id)
    {
        if (!_edges.TryGetValue(id, out var e)) return null;
        _edges.Remove(id);
        return e;
    }

    private void NormalizeVertexOrdinals()
    {
        var list = _vertices.Values.OrderBy(v => v.Ordinal).ThenBy(v => v.Label).ToList();
        for (int i = 0; i < list.Count; i++) list[i].Ordinal = i + 1;
    }

    private void NormalizeEdgeOrdinals()
    {
        var list = _edges.Values.OrderBy(e => e.Ordinal).ThenBy(e => e.Label).ToList();
        for (int i = 0; i < list.Count; i++) list[i].Ordinal = i + 1;
    }

    public bool TryGetVertex(Guid id, out Vertex? vertex)
    {
        return _vertices.TryGetValue(id, out vertex);
    }

    public bool TryGetEdge(Guid id, out Edge? edge)
    {
        return _edges.TryGetValue(id, out edge);
    }

    public void Clear()
    {
        _edges.Clear();
        _vertices.Clear();
    }

    // Export the graph structure (ids, labels, colors) to JSON. Data payloads are intentionally
    // excluded because they may be arbitrary objects; add them later if you need typed payloads.
    public string ToJson()
    {
        var dto = new GraphDto
        {
            Vertices = _vertices.Values.Select(v => new VertexDto
            {
                Id = v.Id,
                Label = v.Label,
                Color = v.Color,
                Ordinal = v.Ordinal
            }).ToList(),
            Edges = _edges.Values.Select(e => new EdgeDto
            {
                Id = e.Id,
                Label = e.Label,
                SourceId = e.Source.Id,
                TargetId = e.Target.Id,
                Color = e.Color,
                Ordinal = e.Ordinal
            }).ToList()
        };

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(dto, opts);
    }

    public void SaveToFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        var json = ToJson();
        File.WriteAllText(path, json);
    }

    // New graph load result to include optional layout information
    public class GraphLoadResult
    {
        public DirectedGraph Graph { get; set; } = null!;
        public LayoutResult? Layout { get; set; }
    }

    public class LayoutResult
    {
        public Dictionary<Guid, PositionDto>? Positions { get; set; }
        public HashSet<Guid>? FrozenIds { get; set; }
    }

    // ToJson overload that accepts optional layout information
    public string ToJson(Dictionary<Guid, PositionDto>? positions = null, IEnumerable<Guid>? frozenIds = null)
    {
        var dto = new GraphDto
        {
            Vertices = _vertices.Values.Select(v => new VertexDto
            {
                Id = v.Id,
                Label = v.Label,
                Color = v.Color
            }).ToList(),
            Edges = _edges.Values.Select(e => new EdgeDto
            {
                Id = e.Id,
                Label = e.Label,
                SourceId = e.Source.Id,
                TargetId = e.Target.Id,
                Color = e.Color
            }).ToList()
        };

        if (positions != null || frozenIds != null)
        {
            dto.Layout = new LayoutDto();

            if (positions != null)
            {
                dto.Layout.Positions = positions.ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            if (frozenIds != null)
            {
                dto.Layout.FrozenIds = frozenIds.ToList();
            }
        }

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(dto, opts);
    }

    public void SaveToFile(string path, Dictionary<Guid, PositionDto>? positions = null, IEnumerable<Guid>? frozenIds = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        var json = ToJson(positions, frozenIds);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Export the graph as an adjacency matrix using Vertex.Ordinal ordering (1-based ordinals).
    /// Returns a tuple of (matrix, labels) where labels[i] corresponds to row/column i.
    /// </summary>
    public (bool[,] matrix, string[] labels) ToAdjacencyMatrix()
    {
        // Order vertices by ordinal (fallback to label) to build index mapping
        var ordered = _vertices.Values.OrderBy(v => v.Ordinal).ThenBy(v => v.Label).ToList();
        var n = ordered.Count;
        var matrix = new bool[n, n];
        var labels = new string[n];
        for (int i = 0; i < n; i++) labels[i] = ordered[i].Label ?? string.Empty;

        // Build lookup from vertex id to index
        var indexById = new Dictionary<Guid, int>(n);
        for (int i = 0; i < n; i++) indexById[ordered[i].Id] = i;

        foreach (var e in _edges.Values)
        {
            if (indexById.TryGetValue(e.Source.Id, out var s) && indexById.TryGetValue(e.Target.Id, out var t))
            {
                matrix[s, t] = true;
            }
        }

        return (matrix, labels);
    }

    /// <summary>
    /// Create a DirectedGraph from a square adjacency matrix. Optional labels array may be provided
    /// (length must equal matrix dimension). Vertex ordinals and edge ordinals will be assigned
    /// to preserve the matrix ordering (vertices 1..N, edges in discovery order).
    /// </summary>
    public static DirectedGraph FromAdjacencyMatrix(bool[,] matrix, string[]? labels = null)
    {
        if (matrix == null) throw new ArgumentNullException(nameof(matrix));
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        if (rows != cols) throw new ArgumentException("Adjacency matrix must be square", nameof(matrix));

        var n = rows;
        if (labels != null && labels.Length != n) throw new ArgumentException("Labels length must match matrix dimensions", nameof(labels));

        var g = new DirectedGraph();
        var vertices = new Vertex[n];

        // create vertices with ordinals matching matrix order
        for (int i = 0; i < n; i++)
        {
            var label = labels != null ? (labels[i] ?? string.Empty) : $"v{i + 1}";
            var v = new Vertex(label) { Ordinal = i + 1 };
            g.AddVertex(v);
            vertices[i] = v;
        }

        // create edges in row-major order and assign ordinals sequentially
        int edgeOrdinal = 1;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (matrix[i, j])
                {
                    var src = vertices[i];
                    var tgt = vertices[j];
                    var e = new Edge(Guid.NewGuid(), src, tgt, string.Empty) { Ordinal = edgeOrdinal++ };
                    g.AddEdge(e);
                }
            }
        }

        return g;
    }

    // Wrapper JSON schema for adjacency export/import
    private class AdjacencyWrapperDto
    {
        public string? Format { get; set; }
        public int Version { get; set; }
        public string? Representation { get; set; }
        public int N { get; set; }
        public List<string>? Labels { get; set; }
        public List<List<int>>? Dense { get; set; }
        public List<List<int>>? Edges { get; set; }
    }

    /// <summary>
    /// Produce a wrapper JSON string containing either a dense matrix or sparse edge list (or both) depending on representationChoice.
    /// representationChoice: "auto" (default), "dense", "sparse", or "both".
    /// </summary>
    public string ToAdjacencyJson(string representationChoice = "auto")
    {
        var (matrix, labels) = ToAdjacencyMatrix();
        var n = matrix.GetLength(0);

        // compute edge count
        int edges = 0;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++) if (matrix[i, j]) edges++;

        // decide representation
        string rep = representationChoice ?? "auto";
        if (rep == "auto")
        {
            if (n <= 20) rep = "dense";
            else
            {
                double density = (double)edges / (n * n);
                rep = density < 0.25 ? "sparse" : "dense";
            }
        }

        var dto = new AdjacencyWrapperDto
        {
            Format = "adjacency",
            Version = 1,
            Representation = rep,
            N = n,
            Labels = labels.ToList()
        };

        if (rep == "dense" || rep == "both")
        {
            dto.Dense = new List<List<int>>(n);
            for (int i = 0; i < n; i++)
            {
                var row = new List<int>(n);
                for (int j = 0; j < n; j++) row.Add(matrix[i, j] ? 1 : 0);
                dto.Dense.Add(row);
            }
        }

        if (rep == "sparse" || rep == "both")
        {
            dto.Edges = new List<List<int>>();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) if (matrix[i, j]) dto.Edges.Add(new List<int> { i, j });
        }

        var opts = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(dto, opts);
    }

    /// <summary>
    /// Save adjacency wrapper JSON to file (auto picks representation by heuristic).
    /// </summary>
    public void SaveAdjacencyJson(string path, string representationChoice = "auto")
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        var json = ToAdjacencyJson(representationChoice);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Parse adjacency wrapper JSON and return a DirectedGraph.
    /// Accepts 'dense', 'sparse' or 'both' representations.
    /// </summary>
    public static DirectedGraph FromAdjacencyJson(string json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dto = JsonSerializer.Deserialize<AdjacencyWrapperDto>(json, opts) ?? throw new ArgumentException("Invalid adjacency JSON");

        if (dto.Format != "adjacency") throw new ArgumentException("Unsupported format");
        if (dto.N <= 0) throw new ArgumentException("Invalid dimension N");

        int n = dto.N;
        string[] labels = dto.Labels != null ? dto.Labels.ToArray() : Enumerable.Range(1, n).Select(i => $"v{i}").ToArray();

        bool[,] matrix = new bool[n, n];
        if ((dto.Representation == "dense" || dto.Representation == "both") && dto.Dense != null)
        {
            if (dto.Dense.Count != n) throw new ArgumentException("Dense matrix size mismatch");
            for (int i = 0; i < n; i++)
            {
                var row = dto.Dense[i];
                if (row.Count != n) throw new ArgumentException("Dense matrix size mismatch");
                for (int j = 0; j < n; j++) matrix[i, j] = row[j] != 0;
            }
        }
        else if ((dto.Representation == "sparse" || dto.Representation == "both") && dto.Edges != null)
        {
            foreach (var pair in dto.Edges)
            {
                if (pair.Count < 2) continue;
                int s = pair[0];
                int t = pair[1];
                if (s >= 0 && s < n && t >= 0 && t < n) matrix[s, t] = true;
            }
        }
        else
        {
            throw new ArgumentException("No valid representation found in adjacency JSON");
        }

        return FromAdjacencyMatrix(matrix, labels);
    }

    public static DirectedGraph LoadFromAdjacencyFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        var json = File.ReadAllText(path);
        return FromAdjacencyJson(json);
    }

    public static GraphLoadResult FromJsonWithLayout(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var dto = JsonSerializer.Deserialize<GraphDto>(json, opts) ?? new GraphDto();

        var g = new DirectedGraph();

        // Recreate vertices preserving ids and colors
        var idToVertex = new Dictionary<Guid, Vertex>();
        foreach (var v in dto.Vertices ?? Enumerable.Empty<VertexDto>())
        {
            var vertex = new Vertex(v.Id, v.Label ?? string.Empty)
            {
                Color = v.Color
                , Ordinal = v.Ordinal
            };
            g.AddVertex(vertex);
            idToVertex[vertex.Id] = vertex;
        }

        // Recreate edges preserving ids, labels and colors
        foreach (var e in dto.Edges ?? Enumerable.Empty<EdgeDto>())
        {
            if (!idToVertex.TryGetValue(e.SourceId, out var source)) continue;
            if (!idToVertex.TryGetValue(e.TargetId, out var target)) continue;

            var edge = new Edge(e.Id, source, target, e.Label ?? string.Empty)
            {
                Color = e.Color
                , Ordinal = e.Ordinal
            };
            g.AddEdge(edge);
        }

        var result = new GraphLoadResult { Graph = g };

        if (dto.Layout != null)
        {
            var layout = new LayoutResult();
            if (dto.Layout.Positions != null)
            {
                layout.Positions = dto.Layout.Positions.ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            if (dto.Layout.FrozenIds != null)
            {
                layout.FrozenIds = new HashSet<Guid>(dto.Layout.FrozenIds);
            }
            result.Layout = layout;
        }

        return result;
    }

    public static GraphLoadResult LoadFromFileWithLayout(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        var json = File.ReadAllText(path);
        return FromJsonWithLayout(json);
    }

    private class GraphDto
    {
        public List<VertexDto>? Vertices { get; set; }
        public List<EdgeDto>? Edges { get; set; }
        public LayoutDto? Layout { get; set; }
    }

    private class LayoutDto
    {
        // map of vertex id -> position
        public Dictionary<Guid, PositionDto>? Positions { get; set; }

        // list of frozen vertex ids
        public List<Guid>? FrozenIds { get; set; }
    }

    public class PositionDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    private class VertexDto
    {
        public Guid Id { get; set; }
        public string? Label { get; set; }
        public string? Color { get; set; }
        public int Ordinal { get; set; }
    }

    private class EdgeDto
    {
        public Guid Id { get; set; }
        public string? Label { get; set; }
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
        public string? Color { get; set; }
        public int Ordinal { get; set; }
    }
}
