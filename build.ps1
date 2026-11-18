#requires -Version 7.6.10
#requires -PSEdition Core
param([string]$OutputPath = (Join-Path $PSScriptRoot 'native/bin64'))
& (Join-Path $PSScriptRoot 'build-native.ps1') -OutputPath $OutputPath
