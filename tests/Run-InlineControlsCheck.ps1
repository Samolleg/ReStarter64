param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '../EIStarter/bin/Release/EIStarter.exe'),
    [string]$PreviewDirectory
)
$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('EIStarter-inline-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$executable = Join-Path $testDirectory 'InlineControlsCheck.exe'
try {
    & $compiler /nologo /target:exe /platform:x86 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:Accessibility.dll "/out:$executable" (Join-Path $PSScriptRoot 'InlineControlsCheck.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Inline controls check compilation failed.' }
    Copy-Item -LiteralPath ($assembly + '.config') -Destination ($executable + '.config')
    if ($PreviewDirectory) {
        $preview = (Resolve-Path -LiteralPath $PreviewDirectory).Path
        & $executable $assembly $preview
    } else { & $executable $assembly }
    if ($LASTEXITCODE -ne 0) { throw 'Inline controls checks failed.' }
}
finally {
    foreach ($file in @($executable, ($executable + '.config'))) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $testDirectory
}
