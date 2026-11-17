# Resolve the canonical project tools from either main or a task worktree.
# No param block: preserve native dash-prefixed arguments.
$ErrorActionPreference = 'Stop'
$buildWorkspace = $PSScriptRoot
while (!(Test-Path -LiteralPath (Join-Path $buildWorkspace 'RULES.md') -PathType Leaf)) {
    $nextBuildParent = Split-Path -Parent $buildWorkspace
    if (!$nextBuildParent -or $nextBuildParent -eq $buildWorkspace) { throw 'Evil Islands workspace not found.' }
    $buildWorkspace = $nextBuildParent
}
& (Join-Path $buildWorkspace 'tools/project/invoke-build.ps1') @args
$global:LASTEXITCODE = $LASTEXITCODE
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
