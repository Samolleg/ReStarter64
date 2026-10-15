param([string]$OutputPath = (Join-Path $PSScriptRoot 'native/bin'))
$ErrorActionPreference = 'Stop'
$locator=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $locator -latest -products '*' -property installationPath
$vc=Join-Path $vs 'VC/Tools/MSVC/14.29.30133'
if (!(Test-Path -LiteralPath $vc)) { throw 'Install the MSVC v142 (14.29) toolset for the Windows 7 target.' }
$sdk=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$version='10.0.22621.0'
$obj=Join-Path $PSScriptRoot 'native/obj'
New-Item -ItemType Directory -Force -Path $obj,$OutputPath | Out-Null
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
& (Join-Path $PSScriptRoot 'native/Generate-Schema.ps1')
$oldInclude=$env:INCLUDE; $oldLib=$env:LIB
try {
 $env:INCLUDE=(Join-Path $vc 'include')+';'+(Join-Path $sdk "Include/$version/ucrt")+';'+(Join-Path $sdk "Include/$version/shared")+';'+(Join-Path $sdk "Include/$version/um")
 $env:LIB=(Join-Path $vc 'lib/x86')+';'+(Join-Path $sdk "Lib/$version/ucrt/x86")+';'+(Join-Path $sdk "Lib/$version/um/x86")
 Push-Location (Join-Path $PSScriptRoot 'native')
 try {
  & (Join-Path $sdk "bin/$version/x64/rc.exe") /nologo /fo (Join-Path $obj 'launcher.res') launcher.rc
  if ($LASTEXITCODE) { throw "Resource build failed: $LASTEXITCODE" }
  $exe=Join-Path $OutputPath 'ReStarter.exe'
  & (Join-Path $vc 'bin/Hostx64/x86/cl.exe') /nologo /std:c++17 /EHsc /W4 /O2 /MT /utf-8 /DUNICODE /D_UNICODE /D_WIN32_WINNT=0x0601 /DWINVER=0x0601 /D_CRT_SECURE_NO_WARNINGS /I. /Fo"$obj\" app.cpp storage.cpp obj/schema.cpp (Join-Path $obj 'launcher.res') /Fe"$exe" /link /SUBSYSTEM:WINDOWS,6.01 /MACHINE:X86 /MANIFEST:NO /DYNAMICBASE /NXCOMPAT user32.lib gdi32.lib
  if ($LASTEXITCODE) { throw "Native build failed: $LASTEXITCODE" }
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'EIStarter/background.jpg') -Destination $OutputPath
 } finally { Pop-Location }
} finally { $env:INCLUDE=$oldInclude; $env:LIB=$oldLib }
Write-Host "Native ReStarter: $OutputPath"
