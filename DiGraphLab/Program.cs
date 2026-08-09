using DiGraphLab.Core;
using System.Windows.Forms;

namespace DiGraphLab;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        // Build a sample graph and display it in the main form
        var graph = new DirectedGraph();
        var a = graph.CreateVertex("A");
        var b = graph.CreateVertex("B");
        var c = graph.CreateVertex("C");
        graph.CreateEdge(a.Id, b.Id);
        graph.CreateEdge(b.Id, c.Id);
        graph.CreateEdge(a.Id, a.Id, "self-loop");

        var main = new MainForm();
        main.RenderGraph(graph);
        Application.Run(main);
    }
}