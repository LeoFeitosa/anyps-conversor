#Requires -Version 7
<#
.SYNOPSIS
    Keeps a copy of the AnyPS5 binaries pinned in build/dependencies.json in an internal release of this
    repository (anyps5-<version>, marked as pre-release). If the official release stops existing, the
    build keeps using the copy, checked against the same SHA-256. Requires an authenticated gh.
#>
$ErrorActionPreference = 'Stop'

$manifest = Get-Content (Join-Path $PSScriptRoot 'dependencies.json') -Raw | ConvertFrom-Json
$tag = "anyps5-$($manifest.anyps5)"
gh release view $tag *> $null
if ($LASTEXITCODE -eq 0) {
    Write-Host "mirror $tag already exists"
    exit 0
}

$download = Join-Path ([IO.Path]::GetTempPath()) ("anyps5-mirror-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $download | Out-Null
try {
    $files = foreach ($entry in $manifest.files.PSObject.Properties) {
        $file = Join-Path $download $entry.Value.asset
        Invoke-WebRequest -Uri "https://github.com/boykopovar/AnyPS5/releases/download/$($manifest.anyps5)/$($entry.Value.asset)" `
            -OutFile $file -MaximumRetryCount 3 -RetryIntervalSec 5
        if ((Get-FileHash $file -Algorithm SHA256).Hash -ne $entry.Value.sha256) {
            throw "SHA-256 mismatch for $($entry.Value.asset)"
        }
        $file
    }
    $notes = @"
Backup copy of the official [AnyPS5 $($manifest.anyps5)](https://github.com/boykopovar/AnyPS5/releases/tag/$($manifest.anyps5)) binaries, unchanged, used only to build the converter if the official release is no longer available. This is not an AnyPS Conversor version: to download the program, use the [latest release](https://github.com/LeoFeitosa/anyps-conversor/releases/latest). GPL-2.0 license; source code at https://github.com/boykopovar/AnyPS5/tree/$($manifest.anyps5).
"@
    $notesFile = Join-Path $download 'notes.md'
    Set-Content $notesFile $notes
    gh release create $tag @files --prerelease --title "Internal dependency: AnyPS5 $($manifest.anyps5)" --notes-file $notesFile
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}
finally {
    Remove-Item $download -Recurse -Force
}
