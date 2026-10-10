param([string]$Package,[string]$Workspace)
$ErrorActionPreference='Stop'
$package=(Resolve-Path -LiteralPath $Package).Path
$testRoot=Join-Path $Workspace ('cases-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$count=0
function Assert($test,$name) { if(!$test){throw $name}; $script:count++; Write-Output ('PASS '+$name) }
function Run($target) { $output=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package 'Install.ps1') -StarterPath $target 2>&1; $script:lastCode=$LASTEXITCODE; $output | Out-File -LiteralPath (Join-Path $testRoot ('run-'+[Guid]::NewGuid().ToString('N')+'.txt')) }
function Fixture($name) { $t=Join-Path $testRoot $name; New-Item -ItemType Directory -Path (Join-Path $t 'Engine'),(Join-Path $t 'Mods') -Force | Out-Null; return $t }
$t=Fixture 'Game with spaces - Проверка'
foreach($name in @('EIStarter.exe','starter.config','EIStarter.ini','background.png','Engine/game.exe','Mods/custom.txt')) { [IO.File]::WriteAllText((Join-Path $t $name),'player-owned-'+$name) }
$before=@{}; Get-ChildItem -LiteralPath $t -File -Recurse | ForEach-Object {$before[$_.FullName]=(Get-FileHash -LiteralPath $_.FullName).Hash}
Run $t
Assert ($lastCode -eq 0) 'fresh side-by-side install'
foreach($file in $before.Keys) { Assert ((Get-FileHash -LiteralPath $file).Hash -eq $before[$file]) ('preserved '+[IO.Path]::GetFileName($file)) }
Assert ((Get-FileHash -LiteralPath (Join-Path $t 'EIStarter-local.exe')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $package 'files/EIStarter-local.exe')).Hash) 'installed tested binary'
$backups=@(Get-ChildItem -LiteralPath $t -Directory -Filter 'Launcher-backup-*').Count
Run $t
Assert ($lastCode -eq 0 -and @(Get-ChildItem -LiteralPath $t -Directory -Filter 'Launcher-backup-*').Count -eq $backups) 'repeat install is no-op'
[IO.File]::WriteAllText((Join-Path $t 'EIStarter-local.exe'),'prior-local-release')
$prior=(Get-FileHash -LiteralPath (Join-Path $t 'EIStarter-local.exe')).Hash
Run $t
Assert ($lastCode -eq 0) 'update succeeds'
$saved=@(Get-ChildItem -LiteralPath $t -Recurse -File -Filter 'EIStarter-local.exe' | Where-Object {$_.Directory.Name -like 'Launcher-backup-*'})
Assert (@($saved | Where-Object {(Get-FileHash -LiteralPath $_.FullName).Hash -eq $prior}).Count -eq 1) 'previous local release backed up'
$t=Fixture 'library-conflict'
[IO.File]::WriteAllText((Join-Path $t 'System.Memory.dll'),'foreign library')
Run $t
Assert ($lastCode -ne 0 -and !(Test-Path -LiteralPath (Join-Path $t 'EIStarter-local.exe'))) 'foreign shared library refuses all writes'
$t=Fixture 'locked-update'
[IO.File]::WriteAllText((Join-Path $t 'EIStarter-local.exe'),'old exe')
[IO.File]::WriteAllText((Join-Path $t 'EIStarter-local.exe.config'),'old config')
$lock=[IO.File]::Open((Join-Path $t 'EIStarter-local.exe.config'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try { Run $t } finally { $lock.Dispose() }
Assert ($lastCode -ne 0 -and [IO.File]::ReadAllText((Join-Path $t 'EIStarter-local.exe')) -eq 'old exe' -and [IO.File]::ReadAllText((Join-Path $t 'EIStarter-local.exe.config')) -eq 'old config') 'locked second file rolls first file back'
Run $testRoot
Assert ($lastCode -ne 0) 'wrong target refused'
$damaged=Join-Path $testRoot 'damaged-package'
Copy-Item -LiteralPath $package -Destination $damaged -Recurse
$package=$damaged
[IO.File]::WriteAllText((Join-Path $package 'files/EIStarter-local.exe'),'corrupt')
$t=Fixture 'damaged-target'
Run $t
Assert ($lastCode -ne 0 -and !(Test-Path -LiteralPath (Join-Path $t 'EIStarter-local.exe'))) 'corrupt payload refused before writes'
Write-Output ('TOTAL '+$count+' PASS')