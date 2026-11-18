#requires -Version 7.6.10
#requires -PSEdition Core
param([string]$OutputPath = (Join-Path $PSScriptRoot 'native/bin64'))
$ErrorActionPreference = 'Stop'
$locator = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs = & $locator -latest -products '*' -property installationPath
if (!$vs) { throw 'MSVC v143 Build Tools are required.' }
$vc = Get-ChildItem -LiteralPath (Join-Path $vs 'VC/Tools/MSVC') -Directory |
    Where-Object { [version]$_.Name -ge [version]'14.30' -and [version]$_.Name -lt [version]'14.50' } |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1 -ExpandProperty FullName
if (!$vc) { throw 'Install the MSVC v143 toolset.' }
$sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$version = '10.0.22621.0'
$obj = Join-Path $PSScriptRoot 'native/obj'
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path $obj,$OutputPath | Out-Null
$msbuild=Join-Path $vs 'MSBuild/Current/Bin/MSBuild.exe'
& (Join-Path $PSScriptRoot 'tools/invoke-build.ps1') $msbuild (Join-Path $PSScriptRoot 'native/ReStarter64.vcxproj') /nologo /nr:false /m:1 /v:minimal /p:Configuration=Release /p:Platform=x64 /p:PreferredToolArchitecture=x64 "/p:VCToolsVersion=$([IO.Path]::GetFileName($vc))" "/p:NativeOutput=$OutputPath"
if($LASTEXITCODE){throw "Native build failed: $LASTEXITCODE"}
Write-Host "Native ReStarter64: $OutputPath"
