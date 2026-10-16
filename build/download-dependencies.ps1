#Requires -Version 7
<#
.SYNOPSIS
    Baixa o relinker e as bibliotecas para Windows do AnyPS5 na versão fixada em
    build/dependencies.json, confere o SHA-256 e grava em deps/. Usa o release oficial do AnyPS5 e, se ele
    não estiver mais disponível, a cópia guardada no release interno anyps5-<versão> deste repositório.
    Nos dois casos só aceita o arquivo com o SHA-256 fixado.
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

    $sources = @(
        "https://github.com/boykopovar/AnyPS5/releases/download/$($manifest.anyps5)/$($entry.Value.asset)"
        "https://github.com/LeoFeitosa/anyps5-conversor/releases/download/anyps5-$($manifest.anyps5)/$($entry.Value.asset)"
    )
    $downloaded = $false
    foreach ($url in $sources) {
        Write-Host "download  $url"
        try {
            Invoke-WebRequest -Uri $url -OutFile "$target.part" -MaximumRetryCount 3 -RetryIntervalSec 5
            $downloaded = $true
            break
        }
        catch {
            Write-Warning "unavailable: $url ($($_.Exception.Message))"
        }
    }
    if (-not $downloaded) {
        throw "AnyPS5 $($manifest.anyps5) $($entry.Value.asset) is unavailable from the official release and from the mirror"
    }

    $actual = (Get-FileHash "$target.part" -Algorithm SHA256).Hash
    if ($actual -ne $expected) {
        Remove-Item "$target.part"
        throw "SHA-256 mismatch for $($entry.Name): expected $expected, got $actual"
    }
    Move-Item -Force "$target.part" $target
}

Set-Content -Path (Join-Path $deps 'VERSION') -Value $manifest.anyps5 -NoNewline
Write-Host "AnyPS5 $($manifest.anyps5) ready in $deps"
