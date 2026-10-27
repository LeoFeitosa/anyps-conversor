#Requires -Version 7
<#
.SYNOPSIS
    Generates docs/screenshot-en.png from the executable, using the internal
    --screenshot mode (start window, neutral example folder, without touching the settings of whoever runs it).
#>
param([Parameter(Mandatory)][string]$Exe)
$ErrorActionPreference = 'Stop'

$docs = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs'
foreach ($language in 'en') {
    $file = Join-Path $docs "screenshot-$language.png"
    $process = Start-Process (Resolve-Path $Exe) -ArgumentList '--screenshot', $language, "`"$file`"" -PassThru -Wait
    if ($process.ExitCode -ne 0 -or -not (Test-Path $file) -or (Get-Item $file).Length -lt 10KB) {
        throw "Screenshot $language was not generated"
    }
    Write-Host "ok        $file"
}
