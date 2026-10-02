// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using MatrixTea.Engine.Packaging;
if (args.Length != 3 || args[0] is not ("pack" or "unpack"))
{
    Console.Error.WriteLine("MatrixTea ATR1: pack <game-folder> <output.atr> | unpack <input.atr> <empty-folder>");
    return 2;
}
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += interrupt;
try
{
    if (args[0] == "pack") AtrArchive.Pack(args[1], args[2], cancellation.Token); else AtrArchive.Extract(args[1], args[2], cancellation: cancellation.Token);
    Console.WriteLine("MatrixTea ATR1 completed."); return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("MatrixTea ATR1 canceled."); return 130; }
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
finally { Console.CancelKeyPress -= interrupt; }
