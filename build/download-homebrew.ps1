#Requires -Version 7
<#
.SYNOPSIS
    Downloads the free homebrew from build/homebrew.json straight from each author's release, checks the
    SHA-256 and extracts each one to <Destination>/<id>. Used by the real conversion test
    (HomebrewConversionTests); none of it goes into the repository.
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
