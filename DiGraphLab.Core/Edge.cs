namespace DiGraphLab.Core;

public class Edge
{
    public Guid Id { get; set; }

    public string Label { get; set; }

    public object? Data { get; set; }

    // Optional color expressed as HTML hex (e.g. "#RRGGBB") or named color. When null the UI should
    // pick a theme-appropriate default.
    public string? Color { get; set; }

    public Vertex Source { get; set; }

    public Vertex Target { get; set; }

    public Edge(Vertex source, Vertex target, string label = "")
    {
        Id = Guid.NewGuid();
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Label = label;
    }

    public Edge(Guid id, Vertex source, Vertex target, string label = "")
    {
        Id = id;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Label = label;
    }
}
