#Requires -Version 7
<#
.SYNOPSIS
    Gera docs/screenshot-pt.png e docs/screenshot-en.png a partir do executável, com o modo interno
    --screenshot (janela inicial, pasta de exemplo neutra, sem tocar nas configurações de quem roda).
#>
param([Parameter(Mandatory)][string]$Exe)
$ErrorActionPreference = 'Stop'

$docs = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs'
foreach ($language in 'pt', 'en') {
    $file = Join-Path $docs "screenshot-$language.png"
    $process = Start-Process (Resolve-Path $Exe) -ArgumentList '--screenshot', $language, "`"$file`"" -PassThru -Wait
    if ($process.ExitCode -ne 0 -or -not (Test-Path $file) -or (Get-Item $file).Length -lt 10KB) {
        throw "Screenshot $language was not generated"
    }
    Write-Host "ok        $file"
}
