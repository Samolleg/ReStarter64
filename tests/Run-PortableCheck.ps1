param([string]$AssemblyPath = (Join-Path $PSScriptRoot '../EIStarter/bin/Release/EIStarter.exe'), [string]$Workspace)
$ErrorActionPreference = 'Stop'
if (!$Workspace) { $Workspace = Join-Path ([IO.Path]::GetTempPath()) ('EIStarter-portable-' + [Guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $Workspace -Force | Out-Null
$Workspace = (Resolve-Path -LiteralPath $Workspace).Path
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$target = Join-Path $Workspace 'PortableCheck.exe'
$source = Join-Path $PSScriptRoot 'PortableCheck.cs'
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 "/out:$target" $source
if ($LASTEXITCODE -ne 0) { throw 'Portable test compilation failed.' }
Copy-Item -LiteralPath ($assembly + '.config') -Destination ($target + '.config') -Force
& $target $assembly (Join-Path $Workspace 'fixtures') (Join-Path $PSScriptRoot '../standalone/portable-defaults.xml')
if ($LASTEXITCODE -ne 0) { throw 'Portable checks failed.' }
