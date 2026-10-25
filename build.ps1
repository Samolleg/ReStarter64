param([ValidateSet("Release", "Release64")][string]$Configuration = "Release")
$ErrorActionPreference = 'Stop'
$locator = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $locator -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'Visual Studio Build Tools with MSBuild and .NET Framework 4.7.2 targeting pack are required.' }
# A portable application: dependencies stay next to EIStarter.exe, without legacy ILMerge or ClickOnce.
& $msbuild (Join-Path $PSScriptRoot 'EIStarter.sln') /restore /p:RestorePackagesConfig=true /p:Configuration=$Configuration /p:PostBuildEvent= /p:GenerateManifests=false /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
