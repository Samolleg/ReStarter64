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
& (Join-Path $PSScriptRoot 'native/Generate-Schema.ps1')
$oldInclude=$env:INCLUDE; $oldLib=$env:LIB
try {
    $env:INCLUDE=(Join-Path $vc 'include')+';'+(Join-Path $sdk "Include/$version/ucrt")+';'+(Join-Path $sdk "Include/$version/shared")+';'+(Join-Path $sdk "Include/$version/um")
    $env:LIB=(Join-Path $vc 'lib/x64')+';'+(Join-Path $sdk "Lib/$version/ucrt/x64")+';'+(Join-Path $sdk "Lib/$version/um/x64")
    Push-Location (Join-Path $PSScriptRoot 'native')
    try {
        & (Join-Path $sdk "bin/$version/x64/rc.exe") /nologo /fo (Join-Path $obj 'launcher.res') launcher.rc
        if ($LASTEXITCODE) { throw "Resource build failed: $LASTEXITCODE" }
        $exe=Join-Path $OutputPath 'ReStarter64.exe'
        $pdb=Join-Path $OutputPath 'ReStarter64.pdb'
        & (Join-Path $vc 'bin/Hostx64/x64/cl.exe') /nologo /std:c++17 /EHsc /W4 /O2 /MT /Zi /utf-8 /DUNICODE /D_UNICODE /D_WIN32_WINNT=0x0601 /DWINVER=0x0601 /D_CRT_SECURE_NO_WARNINGS /I. /Fo"$obj\" /Fd"$obj/compiler.pdb" app.cpp storage.cpp obj/schema.cpp (Join-Path $obj 'launcher.res') /Fe"$exe" /link /SUBSYSTEM:WINDOWS /MACHINE:X64 /MANIFEST:NO /DYNAMICBASE /NXCOMPAT /DEBUG:FULL /INCREMENTAL:NO /OPT:REF /OPT:ICF /PDB:"$pdb" user32.lib gdi32.lib
        if ($LASTEXITCODE) { throw "Native build failed: $LASTEXITCODE" }
    } finally { Pop-Location }
} finally { $env:INCLUDE=$oldInclude; $env:LIB=$oldLib }
Write-Host "Native ReStarter64: $OutputPath"
