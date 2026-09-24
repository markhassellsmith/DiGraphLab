using System;
using System.IO;
using DiGraphLab.Harmony;

class Program
{
    static void Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DiGraphLabDemo");
        try
        {
            Console.WriteLine($"Running DiGraphLab Harmony demo. Output folder: {outDir}");
            Demo.RunSample(outDir);
            Console.WriteLine("Demo completed. Inspect exported files in the output folder.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Demo failed: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }
}
