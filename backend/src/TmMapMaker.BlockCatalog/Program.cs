using TmMapMaker.BlockCatalog;

if (args.Length == 0)
{
    Console.WriteLine("uso: dotnet run -- inventory <cartella mappe .Map.Gbx> [cartella output JSON]");
    Console.WriteLine("     dotnet run -- verify-shapes <cartella mappe .Map.Gbx>");
    return 1;
}

return args[0] switch
{
    "inventory" => InventoryCommand.Run(args.Skip(1).ToArray()),
    "verify-shapes" => VerifyShapesCommand.Run(args.Length > 1 ? args[1] : ""),
    _ => PrintUnknownCommand(args[0]),
};

static int PrintUnknownCommand(string command)
{
    Console.WriteLine($"comando sconosciuto: {command} (usa 'inventory' o 'verify-shapes')");
    return 1;
}
