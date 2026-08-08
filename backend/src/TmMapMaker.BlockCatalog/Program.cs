using System.Text.Json;
using TmMapMaker.BlockCatalog;

if (args.Length == 0)
{
    Console.WriteLine("uso: dotnet run -- <cartella mappe .Map.Gbx> [cartella output JSON]");
    return 1;
}

var inputDir = args[0];
var outputDir = args.Length > 1 ? args[1] : "inventory-output";
Directory.CreateDirectory(outputDir);

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
var mapFiles = Directory.EnumerateFiles(inputDir, "*.Map.Gbx", SearchOption.AllDirectories).ToList();

Console.WriteLine($"trovate {mapFiles.Count} mappe in {inputDir}");

foreach (var mapFile in mapFiles)
{
    try
    {
        var blocks = GbxMapReader.ReadBlocks(mapFile);
        var report = MapInventoryReport.From(mapFile, blocks);

        var outFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(mapFile) + ".inventory.json");
        File.WriteAllText(outFile, JsonSerializer.Serialize(report, jsonOptions));

        var flag = report.UnrecognizedNames.Count > 0
            ? $"  ATTENZIONE: {report.UnrecognizedNames.Count} nomi non riconosciuti"
            : "";
        Console.WriteLine($"  OK {Path.GetFileName(mapFile)}: {report.TotalBlocks} blocchi ({report.GridBlocks} griglia, {report.FreeBlocks} free){flag}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ERRORE {Path.GetFileName(mapFile)}: {ex.Message}");
    }
}

return 0;
