#Requires -Version 7
<#
.SYNOPSIS
    Baixa os homebrews livres de build/homebrew.json direto do release de cada autor, confere o
    SHA-256 e extrai cada um em <Destination>/<id>. Usado pelo teste de conversão real
    (HomebrewConversionTests); nada disso entra no repositório.
#>
param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'

$manifest = Get-Content (Join-Path $PSScriptRoot 'homebrew.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $Destination | Out-Null

foreach ($title in $manifest.titles) {
    $archive = Join-Path $Destination $title.asset
    $url = "https://github.com/$($title.repo)/releases/download/$($title.tag)/$($title.asset)"
    Write-Host "download  $url"
    Invoke-WebRequest -Uri $url -OutFile $archive -MaximumRetryCount 3 -RetryIntervalSec 5

    $actual = (Get-FileHash $archive -Algorithm SHA256).Hash
    if ($actual -ne $title.sha256) {
        throw "SHA-256 mismatch for $($title.asset): expected $($title.sha256), got $actual"
    }
    Expand-Archive -Path $archive -DestinationPath (Join-Path $Destination $title.id) -Force
    Remove-Item $archive
}

Write-Host "$($manifest.titles.Count) homebrews ready in $Destination"
