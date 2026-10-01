param([switch]$Graphics,[string]$LegacyAtr)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $root
try {
    foreach ($project in @('src/MatrixTea.Engine.MonoGame','src/MatrixTea.Packaging.Cli','tests/MatrixTea.Engine.HostSmoke')) {
        dotnet build $project -c Release --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
    $arguments = @('run','--project','tests/MatrixTea.Engine.Tests','-c','Release','--')
    if ($LegacyAtr) { $arguments += @('--legacy-atr',[System.IO.Path]::GetFullPath($LegacyAtr)) }
    dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Core/packaging regression failed.' }
    if ($Graphics) {
        dotnet run --project tests/MatrixTea.Engine.HostSmoke -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Graphics verification failed.' }
    }
} finally { Pop-Location }
