using MatrixTea.Engine.Packaging;
if (args.Length != 3 || args[0] is not ("pack" or "unpack"))
{
    Console.Error.WriteLine("MatrixTea ATR1: pack <game-folder> <output.atr> | unpack <input.atr> <empty-folder>");
    return 2;
}
try
{
    if (args[0] == "pack") AtrArchive.Pack(args[1], args[2]); else AtrArchive.Extract(args[1], args[2]);
    Console.WriteLine("MatrixTea ATR1 completed."); return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
