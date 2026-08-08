namespace TmMapMaker.BlockCatalog;

public static class VerifyShapesCommand
{
    // Candidate hypotheses for the v1 shape vocabulary. These are UNVERIFIED until this
    // command's report is reviewed by a human against the printed evidence - do not treat
    // these numbers as ground truth, they are starting guesses based on the standard TM2020
    // Stadium grid convention (32-unit cells, forward = +Z in local space before rotation).
    private static readonly ShapeHypothesis[] CandidateHypotheses =
    {
        new("Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0),
        new("Curve1", LocalForwardDx: 1, LocalForwardDz: 0, LocalForwardDy: 0),
        new("Checkpoint", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0),
        new("Start", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 0),
        new("Finish", LocalForwardDx: 0, LocalForwardDz: -1, LocalForwardDy: 0),
        new("Slope2Straight", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 1),
        new("Slope2Up", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: 1),
        new("Slope2Down", LocalForwardDx: 0, LocalForwardDz: 1, LocalForwardDy: -1),
    };

    public static int Run(string inputDir)
    {
        if (string.IsNullOrWhiteSpace(inputDir) || !Directory.Exists(inputDir))
        {
            Console.WriteLine($"ERRORE: la cartella di input non esiste: {inputDir}");
            return 1;
        }

        var mapFiles = Directory.EnumerateFiles(inputDir, "*.Map.Gbx", SearchOption.AllDirectories).ToList();
        Console.WriteLine($"trovate {mapFiles.Count} mappe in {inputDir}");

        var blocksByMapFile = new Dictionary<string, IReadOnlyList<PlacedBlock>>();
        var readFailureCount = 0;
        foreach (var mapFile in mapFiles)
        {
            try
            {
                blocksByMapFile[Path.GetFileName(mapFile)] = GbxMapReader.ReadBlocks(mapFile);
            }
            catch (Exception ex)
            {
                readFailureCount++;
                Console.WriteLine($"  ERRORE lettura {Path.GetFileName(mapFile)}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("-- VERIFICA IPOTESI FORME (da rivedere a mano prima di bloccare i valori) --");
        foreach (var hypothesis in CandidateHypotheses)
        {
            var result = ShapeHypothesisVerifier.Verify(hypothesis, blocksByMapFile);
            var verdict = result.TotalOccurrences == 0
                ? "NESSUNA OCCORRENZA (forma non trovata in queste mappe)"
                : result.MismatchCount == 0
                    ? "TUTTE CONFERMATE"
                    : $"{result.MismatchCount}/{result.TotalOccurrences} SENZA RISCONTRO";

            Console.WriteLine($"  {hypothesis.ShapeSuffix} (dx={hypothesis.LocalForwardDx},dy={hypothesis.LocalForwardDy},dz={hypothesis.LocalForwardDz}): {result.MatchCount}/{result.TotalOccurrences} confermate - {verdict}");
            foreach (var example in result.ExampleMismatches)
                Console.WriteLine($"      mismatch: {example}");
        }

        if (readFailureCount > 0)
            Console.WriteLine($"  {readFailureCount} mappe non lette a causa di errori");

        return (mapFiles.Count == 0 || readFailureCount > 0) ? 1 : 0;
    }
}
