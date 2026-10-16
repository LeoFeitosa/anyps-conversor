#Requires -Version 7
<#
.SYNOPSIS
    Baixa o relinker e as bibliotecas para Windows do AnyPS5 na versão fixada em
    build/dependencies.json, confere o SHA-256 e grava em deps/.
#>
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $PSScriptRoot 'dependencies.json') -Raw | ConvertFrom-Json
$deps = Join-Path $root 'deps'
New-Item -ItemType Directory -Force -Path $deps | Out-Null

foreach ($entry in $manifest.files.PSObject.Properties) {
    $target = Join-Path $deps $entry.Name
    $expected = $entry.Value.sha256

    if ((Test-Path $target) -and (Get-FileHash $target -Algorithm SHA256).Hash -eq $expected) {
        Write-Host "ok        $($entry.Name)"
        continue
    }

    $url = "https://github.com/boykopovar/AnyPS5/releases/download/$($manifest.anyps5)/$($entry.Value.asset)"
    Write-Host "download  $url"
    Invoke-WebRequest -Uri $url -OutFile "$target.part" -MaximumRetryCount 3 -RetryIntervalSec 5

    $actual = (Get-FileHash "$target.part" -Algorithm SHA256).Hash
    if ($actual -ne $expected) {
        Remove-Item "$target.part"
        throw "SHA-256 mismatch for $($entry.Name): expected $expected, got $actual"
    }
    Move-Item -Force "$target.part" $target
}

Set-Content -Path (Join-Path $deps 'VERSION') -Value $manifest.anyps5 -NoNewline
Write-Host "AnyPS5 $($manifest.anyps5) ready in $deps"
