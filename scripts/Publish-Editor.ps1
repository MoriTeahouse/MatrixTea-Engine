# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 MoriTeahouse (森之宿茶室)
param([string]$OutputPath,[string]$ArchivePath)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
if(-not $OutputPath){$OutputPath=Join-Path $taskRepo 'artifacts/windows/MatrixTeaEditor'}
if(-not $ArchivePath){$ArchivePath=Join-Path $taskRepo 'artifacts/MatrixTeaEditor-0.1.0-test.1-win-x64.zip'}
$OutputPath=[IO.Path]::GetFullPath($OutputPath);$ArchivePath=[IO.Path]::GetFullPath($ArchivePath)
if((Test-Path -LiteralPath $OutputPath) -and (Get-ChildItem -LiteralPath $OutputPath -Force | Select-Object -First 1)){throw 'Output directory must be empty.'}
if(Test-Path -LiteralPath $ArchivePath){throw 'Archive already exists.'}
dotnet publish (Join-Path $taskRepo 'src/MatrixTea.Editor') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $OutputPath --nologo
if($LASTEXITCODE -ne 0){throw 'Editor publish failed.'}
$taskLicensePath=Join-Path $OutputPath 'LICENSES';New-Item -ItemType Directory -Path $taskLicensePath -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $taskRepo 'LICENSES') -File | Copy-Item -Destination $taskLicensePath
$taskRuntime=Get-Content -LiteralPath (Join-Path $OutputPath 'MatrixTeaEditor.runtimeconfig.json') -Raw | ConvertFrom-Json
$taskRuntimeVersion=($taskRuntime.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App').version
$taskAssets=Get-Content -LiteralPath (Join-Path $taskRepo 'src/MatrixTea.Editor/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
$taskInventory=@();$taskCopied=$false
foreach($taskLib in $taskAssets.libraries.GetEnumerator()){
 if($taskLib.Value.type -ne 'package'){continue}
 $taskParts=$taskLib.Key.Split('/')
 foreach($taskFolder in $taskAssets.packageFolders.Keys){
  $taskPackage=Join-Path $taskFolder $taskLib.Value.path
  $taskSpec=Get-ChildItem -LiteralPath $taskPackage -Filter '*.nuspec' -File -ErrorAction SilentlyContinue | Select-Object -First 1
  if($taskSpec){[xml]$taskMetadata=Get-Content -LiteralPath $taskSpec.FullName -Raw;$taskMeta=$taskMetadata.package.metadata;$taskInventory+=@{id=$taskParts[0];version=$taskParts[1];authors=[string]$taskMeta.authors;license=[string]$taskMeta.license.InnerText;source=[string]$taskMeta.projectUrl};break}
 }
}
$taskInventory | Sort-Object {$_['id']} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskLicensePath 'dependencies.json') -Encoding utf8NoBOM
foreach($taskFolder in $taskAssets.packageFolders.Keys){
 $taskRuntimeFolder=Join-Path $taskFolder "microsoft.netcore.app.runtime.win-x64/$taskRuntimeVersion"
 if(Test-Path -LiteralPath (Join-Path $taskRuntimeFolder 'LICENSE.TXT')){
  Copy-Item -LiteralPath (Join-Path $taskRuntimeFolder 'LICENSE.TXT') -Destination (Join-Path $taskLicensePath 'DotNet-Runtime.txt')
  Copy-Item -LiteralPath (Join-Path $taskRuntimeFolder 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $taskLicensePath 'DotNet-Third-Party.txt')
  $taskCopied=$true;break
 }
}
if(-not $taskCopied){throw 'Actual runtime license files not found.'}
$taskDesktopVersion=($taskRuntime.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App').version
$taskDesktopSource=''
if($taskDesktopVersion){
 foreach($taskFolder in $taskAssets.packageFolders.Keys){
  $taskDesktopFolder=Join-Path $taskFolder "microsoft.windowsdesktop.app.runtime.win-x64/$taskDesktopVersion"
  if(Test-Path -LiteralPath (Join-Path $taskDesktopFolder 'LICENSE')){
   Copy-Item -LiteralPath (Join-Path $taskDesktopFolder 'LICENSE') -Destination (Join-Path $OutputPath 'LICENSES/DotNet-WindowsDesktop.txt')
   [xml]$taskDesktopMetadata=Get-Content -LiteralPath (Join-Path $taskDesktopFolder 'microsoft.windowsdesktop.app.runtime.win-x64.nuspec') -Raw
   $taskDesktopSource="$($taskDesktopMetadata.package.metadata.repository.url)/tree/$($taskDesktopMetadata.package.metadata.repository.commit)"
   break
  }
 }
 if(-not $taskDesktopSource){throw 'Actual Windows Desktop runtime license files not found.'}
}
$taskRevision=(git -C $taskRepo rev-parse HEAD).Trim()
@"
# MatrixTea Studio corresponding source
MoriTeahouse (森之宿茶室). AGPL-3.0-only.
Editor and engine: https://github.com/MoriTeahouse/MatrixTea-Engine/tree/$taskRevision
Source archive: https://github.com/MoriTeahouse/MatrixTea-Engine/archive/$taskRevision.zip
Build: dotnet publish src/MatrixTea.Editor -c Release -r win-x64 --self-contained true
.NET Windows Desktop $taskDesktopVersion source: $taskDesktopSource
.NET $taskRuntimeVersion source: https://github.com/dotnet/runtime/tree/v$taskRuntimeVersion
MonoGame: https://github.com/MonoGame/MonoGame/tree/v3.8.1
SDL2: https://github.com/libsdl-org/SDL/tree/release-2.0.20
OpenAL Soft: https://github.com/kcat/openal-soft
NAudio: https://github.com/naudio/NAudio/tree/v2.2.1
Third-party notices and actual package inventory: LICENSES.
"@ | Set-Content -LiteralPath (Join-Path $OutputPath 'SOURCE.md') -Encoding utf8NoBOM
Copy-Item -LiteralPath (Join-Path $taskRepo 'docs/editor.md') -Destination (Join-Path $OutputPath 'README.md')
New-Item -ItemType Directory -Path (Join-Path $OutputPath 'Samples') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRepo 'examples/TeaGarden/TeaGarden.mtproject') -Destination (Join-Path $OutputPath 'Samples/TeaGarden.mtproject')
New-Item -ItemType Directory -Path (Split-Path -Parent $ArchivePath) -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($OutputPath,$ArchivePath,[IO.Compression.CompressionLevel]::Optimal,$false)
$taskHash=(Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$taskHash  $([IO.Path]::GetFileName($ArchivePath))" | Set-Content -LiteralPath ($ArchivePath+'.sha256') -Encoding utf8NoBOM
Write-Output "Editor: $OutputPath"
Write-Output "Archive: $ArchivePath"
