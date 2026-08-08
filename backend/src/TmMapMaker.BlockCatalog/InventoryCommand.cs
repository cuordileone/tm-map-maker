using System.Text.Json;

namespace TmMapMaker.BlockCatalog;

public static class InventoryCommand
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("uso: dotnet run -- <cartella mappe .Map.Gbx> [cartella output JSON]");
            return 1;
        }

        var inputDir = args[0];
        var outputDir = args.Length > 1 ? args[1] : "inventory-output";

        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine($"ERRORE: la cartella di input non esiste: {inputDir}");
            return 1;
        }

        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERRORE: impossibile creare la cartella di output {outputDir}: {ex.Message}");
            return 1;
        }

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var mapFiles = Directory.EnumerateFiles(inputDir, "*.Map.Gbx", SearchOption.AllDirectories).ToList();

        Console.WriteLine($"trovate {mapFiles.Count} mappe in {inputDir}");

        var failureCount = 0;

        foreach (var mapFile in mapFiles)
        {
            try
            {
                var relativePath = Path.GetRelativePath(inputDir, mapFile);
                var blocks = GbxMapReader.ReadBlocks(mapFile);
                var report = MapInventoryReport.From(relativePath, blocks);

                var outName = relativePath.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
                var outFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(outName) + ".inventory.json");
                File.WriteAllText(outFile, JsonSerializer.Serialize(report, jsonOptions));

                var flag = report.UnrecognizedNames.Count > 0
                    ? $"  ATTENZIONE: {report.UnrecognizedNames.Count} nomi non riconosciuti"
                    : "";
                Console.WriteLine($"  OK {Path.GetFileName(mapFile)}: {report.TotalBlocks} blocchi ({report.GridBlocks} griglia, {report.FreeBlocks} free){flag}");
            }
            catch (Exception ex)
            {
                failureCount++;
                Console.WriteLine($"  ERRORE {Path.GetFileName(mapFile)}: {ex.Message}");
            }
        }

        return failureCount > 0 ? 1 : 0;
    }
}
