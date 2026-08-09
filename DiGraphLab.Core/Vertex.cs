namespace DiGraphLab.Core;

public class Vertex
{
    public Guid Id { get; set; }

    public string Label { get; set; }

    public object? Data { get; set; }

    // Optional color expressed as HTML hex (e.g. "#RRGGBB") or named color. When null the UI should
    // pick a theme-appropriate default (e.g., opposite of the WinForms background).
    public string? Color { get; set; }

    public Vertex(string label)
    {
        Id = Guid.NewGuid();
        Label = label;
    }

    // constructor to rehydrate with existing id
    public Vertex(Guid id, string label)
    {
        Id = id;
        Label = label;
    }
}
