param([Parameter(Mandatory=$true)][string]$VerifiedPackage, [Parameter(Mandatory=$true)][string]$OutputDirectory, [Parameter(Mandatory=$true)][string]$SourceCommit)
$ErrorActionPreference='Stop'
$source=(Resolve-Path -LiteralPath $VerifiedPackage).Path
$output=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Output already exists; use a new release directory.' }
$expected='08ACF023A028C2C0A330801C9CB6A755EC2AA9D9F9673A64E2615B114AE12DAF'
if ((Get-FileHash -LiteralPath (Join-Path $source 'EIStarter.exe')).Hash -ne $expected) { throw 'This packaging revision requires tested launcher 0a0ef2e.' }
$original=Get-Content -LiteralPath (Join-Path $source 'manifest.json') -Raw | ConvertFrom-Json
$release=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../EIStarter/bin/Release/EIStarter.exe')).Path
if ((Get-FileHash -LiteralPath $release).Hash -ne '0D8D05B98D90A42AFF8B65E25C6D2DA3E7B30E616F61CA359F6A37A0B1AFBD8A') { throw 'Distribution binary does not match verified no-debug build.' }
$files=Join-Path $output 'files'
New-Item -ItemType Directory -Path $files -Force | Out-Null
$entries=@()
foreach($entry in $original.files) {
    if($entry.path -in @('starter.config','ПРОЧИТАЙТЕ.txt')) { continue }
    $src=Join-Path $source $entry.path
    if((Get-FileHash -LiteralPath $src).Hash -ne $entry.sha256) { throw 'Source manifest mismatch.' }
    if($entry.path -eq 'EIStarter.exe') { $src=$release }
    $name=$entry.path -replace '^EIStarter.exe','EIStarter-local.exe'
    $dest=Join-Path $files $name
    New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
    Copy-Item -LiteralPath $src -Destination $dest
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Launch-EIStarter.cmd') -Destination $files
foreach($name in @('Install.cmd','Install.ps1','README-RU.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $output }
$entries=@(Get-ChildItem -LiteralPath $files -File -Recurse | ForEach-Object { [pscustomobject]@{path=$_.FullName.Substring($files.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
[pscustomobject]@{release='2026.09.10-r1';sourceCommit=$SourceCommit;functionalBase='0a0ef2ec9d17a500187c453b82d30b35446d1af0';files=$entries} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
$licenseDir=Join-Path $output 'licenses'
New-Item -ItemType Directory -Path $licenseDir | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Microsoft-MIT.txt') -Destination $licenseDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../packages/System.Text.Encoding.CodePages.10.0.3/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $licenseDir 'CodePages-NOTICES.txt')
@'
Microsoft components (MIT):
System.Buffers 4.6.1
System.Memory 4.6.3
System.Numerics.Vectors 4.6.1
System.Runtime.CompilerServices.Unsafe 6.1.2
System.Text.Encoding.CodePages 10.0.3
See licenses/Microsoft-MIT.txt and licenses/CodePages-NOTICES.txt.
This binary package does not grant a license to private launcher source or game assets.
'@ | Set-Content -LiteralPath (Join-Path $output 'THIRD-PARTY.txt') -Encoding UTF8
Write-Output $output
