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
        _edges[e.Id] = e;
    }

    public Edge? RemoveEdge(Guid id)
    {
        if (!_edges.TryGetValue(id, out var e)) return null;
        _edges.Remove(id);
        return e;
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
    }

    private class EdgeDto
    {
        public Guid Id { get; set; }
        public string? Label { get; set; }
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
        public string? Color { get; set; }
    }
}
