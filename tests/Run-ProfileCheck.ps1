#requires -Version 7.6.10
#requires -PSEdition Core
param([string]$EvidenceDirectory, [string]$LegacyRoot)
$ErrorActionPreference = 'Stop'
if (!$EvidenceDirectory -or !$LegacyRoot) { throw 'Specify private EvidenceDirectory and a read-only LegacyRoot containing the three legacy import files.' }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('ReStarter-R28-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $testRoot 'Engine') | Out-Null
$assembly = Join-Path $PSScriptRoot '../EIStarter/bin/Release/ReStarter.exe'
Get-ChildItem -File (Split-Path $assembly) | Where-Object { $_.Extension -in @('.exe', '.dll', '.config', '.jpg') } | Copy-Item -Destination $testRoot
Copy-Item -LiteralPath (Join-Path $LegacyRoot 'portable-settings.xml'), (Join-Path $LegacyRoot 'starter.config') -Destination $testRoot
Copy-Item -LiteralPath (Join-Path $LegacyRoot 'Engine/eid3d11.ini') -Destination (Join-Path $testRoot 'Engine')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
$testExe = Join-Path $testRoot 'ProfileCheck.exe'
& $compiler /nologo /target:exe /r:System.Xml.Linq.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$testExe" (Join-Path $PSScriptRoot 'ProfileCheck.cs')
if ($LASTEXITCODE -ne 0) { throw 'Profile test compilation failed.' }
Copy-Item -LiteralPath ($assembly + '.config') -Destination ($testExe + '.config')
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$ErrorActionPreference = 'Continue'
& $testExe (Join-Path $testRoot 'ReStarter.exe') $testRoot 2>&1 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'profile-test.log')
$testExit = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
[pscustomobject]@{ TestRoot=$testRoot; ExitCode=$testExit; AssemblySHA256=(Get-FileHash -LiteralPath $assembly).Hash } | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $EvidenceDirectory 'profile-test.json')
if ($testExit -ne 0) { throw "Profile tests failed: $testExit" }
