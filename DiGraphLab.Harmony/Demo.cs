using System;
using System.IO;

namespace DiGraphLab.Harmony
{
    /// <summary>
    /// Small demo that builds a simple I-IV-V-I progression in C major and exports JSON/CSV to a folder.
    /// Call Demo.RunSample("C:\\temp\\out") from a quick console or unit test to generate files.
    /// </summary>
    public static class Demo
    {
        public static void RunSample(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var svc = new HarmonyService();
            // Tonic C = 0
            int tonic = 0;

            // Build I, IV, V7, I using factories
            var I = Chord.FromMajorDiatonic("I", tonic, 1, addSeventh: false);
            var IV = Chord.FromMajorDiatonic("IV", tonic, 4, addSeventh: false);
            var V7 = Chord.FromMajorDiatonic("V7", tonic, 5, addSeventh: true);
            var I2 = Chord.FromMajorDiatonic("I", tonic, 1, addSeventh: false);

            svc.AddProgression(new[] { I, IV, V7, I2 }, tonic, style: "demo");

            var jsonPath = Path.Combine(outDir, "harmony-demo.json");
            svc.ExportJson(jsonPath);

            var nodesCsv = Path.Combine(outDir, "harmony-nodes.csv");
            var edgesCsv = Path.Combine(outDir, "harmony-edges.csv");
            svc.ExportCsv(nodesCsv, edgesCsv);

            Console.WriteLine("Demo exported to: " + outDir);
            Console.WriteLine("JSON: " + jsonPath);
            Console.WriteLine("Nodes CSV: " + nodesCsv);
            Console.WriteLine("Edges CSV: " + edgesCsv);
        }
    }
}
