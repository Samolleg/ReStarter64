param([string]$StarterPath)
$ErrorActionPreference = 'Stop'
try {
    if (!$StarterPath) {
        Add-Type -AssemblyName System.Windows.Forms
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = 'Select the existing Starter folder (contains Engine and Mods).'
        try {
            if ($dialog.ShowDialog() -ne 'OK') { exit 0 }
            $StarterPath = $dialog.SelectedPath
        } finally { $dialog.Dispose() }
    }
    $target = (Resolve-Path -LiteralPath $StarterPath).Path.TrimEnd('\')
    foreach ($folder in @('Engine','Mods')) {
        if (!(Test-Path -LiteralPath (Join-Path $target $folder) -PathType Container)) { throw 'Choose the existing Starter folder containing Engine and Mods.' }
    }
    $framework = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release -ErrorAction SilentlyContinue
    if (!$framework -or $framework.Release -lt 461808) { throw '.NET Framework 4.7.2 or later is required.' }
    if (@(Get-Process -Name game,EIStarter,EIStarter-local -ErrorAction SilentlyContinue).Count) { throw 'Close the game and both launchers before installation.' }
    $payload = Join-Path $PSScriptRoot 'files'
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
    $replace = @('EIStarter-local.exe','EIStarter-local.exe.config','Launch-EIStarter.cmd')
    $libraries = @('System.Buffers.dll','System.Memory.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','System.Text.Encoding.CodePages.dll')
    $seen = @{}
    $plan = @()
    foreach ($entry in $manifest.files) {
        $name = [string]$entry.path
        if ($seen.ContainsKey($name)) { throw 'Duplicate package entry.' }
        $seen[$name] = $true
        if ($name -notin ($replace + $libraries + @('background.png')) -and $name -notmatch '^lang\\(en|ru|de|fr|pl|ko|zh)\\(lang.ini|readme.txt)$') { throw 'Unexpected package entry.' }
        $src = Join-Path $payload $name
        if ((Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash -ne $entry.sha256) { throw ('Damaged package: ' + $name) }
        $dst = Join-Path $target $name
        # Reject junctions/symlinks on the entire destination chain.
        $part = $dst
        while ($part) {
            if ((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Installation through links or junctions is not supported.' }
            $part = Split-Path -Parent $part
        }
        $before = $null
        if (Test-Path -LiteralPath $dst) {
            $before = (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash
            if ($before -eq $entry.sha256) { continue }
            if ($name -in $libraries) { throw ('A different shared library already exists: ' + $name + '. No files changed; ask the package author to check compatibility.') }
            if ($name -notin $replace) { continue } # Keep the player's artwork and language files.
        }
        $plan += [pscustomobject]@{path=$name;source=$src;target=$dst;before=$before;after=$entry.sha256}
    }
    foreach ($name in ($replace + $libraries + @('background.png'))) {
        if (!$seen.ContainsKey($name)) { throw ('Incomplete package: ' + $name) }
    }
    if (!$plan.Count) { Write-Host 'This version is already installed.'; exit 0 }
    $backup = Join-Path $target ('Launcher-backup-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $backup | Out-Null
    foreach ($item in $plan) {
        if ($item.before) {
            $saved = Join-Path $backup $item.path
            New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
            Copy-Item -LiteralPath $item.target -Destination $saved
            if ((Get-FileHash -LiteralPath $saved).Hash -ne $item.before) { throw 'Backup verification failed; installation stopped.' }
        }
    }
    $plan | Select-Object path,before,after | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $backup 'files.json') -Encoding UTF8
    $written = New-Object System.Collections.ArrayList
    try {
        foreach ($item in $plan) {
            $current = if (Test-Path -LiteralPath $item.target) { (Get-FileHash -LiteralPath $item.target).Hash } else { $null }
            if ($current -ne $item.before) { throw 'A destination file changed during installation.' }
            New-Item -ItemType Directory -Path (Split-Path $item.target) -Force | Out-Null
            [void]$written.Add($item)
            Copy-Item -LiteralPath $item.source -Destination $item.target -Force
            if ((Get-FileHash -LiteralPath $item.target).Hash -ne $item.after) { throw 'Installed file verification failed.' }
        }
    } catch {
        $failure = $_
        for ($i=$written.Count-1; $i -ge 0; $i--) {
            $item = $written[$i]
            if ($item.before) {
                if (!(Test-Path -LiteralPath $item.target) -or (Get-FileHash -LiteralPath $item.target).Hash -ne $item.before) {
                    Copy-Item -LiteralPath (Join-Path $backup $item.path) -Destination $item.target -Force
                }
            }
            elseif (Test-Path -LiteralPath $item.target) { Remove-Item -LiteralPath $item.target }
        }
        throw $failure
    }
    Write-Host 'Installed successfully. Start Launch-EIStarter.cmd in the Starter folder.'
    Write-Host ('Previous files: ' + $backup)
    Write-Host 'Your old EIStarter.exe, settings, mods and engine were not replaced.'
} catch {
    Write-Host ('Installation stopped: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
