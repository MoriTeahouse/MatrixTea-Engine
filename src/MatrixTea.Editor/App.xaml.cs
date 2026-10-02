// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Windows;
namespace MatrixTea.Editor;
public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var window = new MainWindow(); MainWindow = window; window.Show();
        if (e.Args.Contains("--smoke"))
        {
            int i = Array.IndexOf(e.Args, "--output"); string output = i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : System.IO.Path.Combine(AppContext.BaseDirectory, "smoke");
            try { await window.SmokeAsync(output); Shutdown(0); }
            catch (Exception ex) { System.IO.Directory.CreateDirectory(output); System.IO.File.WriteAllText(System.IO.Path.Combine(output, "error.txt"), ex.ToString()); Shutdown(1); }
        }
        else if (e.Args.Length > 0 && e.Args[0].EndsWith(".mtproject", StringComparison.OrdinalIgnoreCase)) window.OpenPath(e.Args[0]);
    }
}
