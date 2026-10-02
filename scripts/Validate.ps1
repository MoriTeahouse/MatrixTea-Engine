param([switch]$Graphics,[string]$LegacyAtr)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $root
try {
    foreach ($project in @('src/MatrixTea.Engine.MonoGame','src/MatrixTea.Packaging.Cli','tests/MatrixTea.Engine.HostSmoke')) {
        dotnet build $project -c Release --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
    if ($IsWindows) {
        dotnet build src/MatrixTea.Engine.Desktop -c Release --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { throw 'Windows desktop adapter build failed.' }
        dotnet build src/MatrixTea.Editor -c Release --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { throw 'Visual editor build failed.' }
        dotnet build tools/MatrixTea.IconTool -c Release --nologo -warnaserror
        if ($LASTEXITCODE -ne 0) { throw 'SVG icon tool build failed.' }
    }
    $arguments = @('run','--project','tests/MatrixTea.Engine.Tests','-c','Release','--')
    if ($LegacyAtr) { $arguments += @('--legacy-atr',[System.IO.Path]::GetFullPath($LegacyAtr)) }
    dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Core/packaging regression failed.' }
    dotnet run --project tests/MatrixTea.Editor.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Editor regression failed.' }
    if ($Graphics) {
        dotnet run --project tests/MatrixTea.Engine.HostSmoke -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Graphics verification failed.' }
    }
} finally { Pop-Location }
