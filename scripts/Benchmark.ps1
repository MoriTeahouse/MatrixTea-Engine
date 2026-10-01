param([switch]$IncludePackaging)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $root
try {
    $arguments = @('run','--project','benchmarks/MatrixTea.Engine.Benchmarks','-c','Release','--')
    if ($IncludePackaging) {
        $baselineRoot = Join-Path $root 'artifacts/benchmark-baseline'
        New-Item -ItemType Directory -Path $baselineRoot -Force | Out-Null
        $source = git show '9d56e36:src/MatrixTea.Engine.Packaging/AtrArchive.cs'
        if ($LASTEXITCODE -ne 0) { throw 'Baseline commit unavailable; fetch full history first.' }
        $source | Set-Content -LiteralPath (Join-Path $baselineRoot 'AtrArchive.cs') -Encoding utf8NoBOM
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AssemblyName>MatrixTea.Engine.Packaging.Legacy</AssemblyName></PropertyGroup></Project>' | Set-Content -LiteralPath (Join-Path $baselineRoot 'Legacy.csproj') -Encoding utf8NoBOM
        dotnet build (Join-Path $baselineRoot 'Legacy.csproj') -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Baseline build failed.' }
        $arguments += @('--baseline-assembly',(Join-Path $baselineRoot 'bin/Release/net8.0/MatrixTea.Engine.Packaging.Legacy.dll'))
    }
    dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Benchmark failed.' }
} finally { Pop-Location }
