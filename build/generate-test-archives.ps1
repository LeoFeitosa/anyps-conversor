<#
.SYNOPSIS
    Recria os arquivos com senha de tests/AnyPSConversor.Tests/Fixtures (senha "segredo123").
    Requer 7-Zip e WinRAR instalados; só é preciso para mudar os casos de teste.
#>
$ErrorActionPreference = 'Stop'
$sevenZip = "$env:ProgramFiles\7-Zip\7z.exe"
$rar = "$env:ProgramFiles\WinRAR\Rar.exe"
$fixtures = Join-Path (Split-Path -Parent $PSScriptRoot) 'tests\AnyPSConversor.Tests\Fixtures'
$source = Join-Path ([IO.Path]::GetTempPath()) ("anyps5-fixtures-" + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Force (Join-Path $source 'jogo\sce_sys') | Out-Null
Set-Content (Join-Path $source 'jogo\sce_sys\param.json') '{"titleId":"PPSA00099"}'
# Cabeçalho ELF mínimo: o programa exige um eboot.bin antes de extrair o resto. Não é um jogo.
[IO.File]::WriteAllBytes((Join-Path $source 'jogo\eboot.bin'), [byte[]](0x7F, 0x45, 0x4C, 0x46) + [byte[]]::new(60))
[IO.File]::WriteAllBytes((Join-Path $source 'jogo\dados.bin'), [byte[]](1..5000 | ForEach-Object { Get-Random -Maximum 256 }))

Push-Location $source
try {
    Remove-Item (Join-Path $fixtures '*') -Include *.zip, *.7z, *.rar -ErrorAction SilentlyContinue
    & $sevenZip a -tzip -mem=AES256 -psegredo123 (Join-Path $fixtures 'aes.zip') jogo | Out-Null
    & $sevenZip a -tzip -mem=ZipCrypto -psegredo123 (Join-Path $fixtures 'zipcrypto.zip') jogo | Out-Null
    & $sevenZip a -t7z -mhe=on -psegredo123 (Join-Path $fixtures 'nomes-ocultos.7z') jogo | Out-Null
    & $rar a -psegredo123 (Join-Path $fixtures 'dados.rar') jogo | Out-Null
    & $rar a -hpsegredo123 (Join-Path $fixtures 'nomes-ocultos.rar') jogo | Out-Null
}
finally {
    Pop-Location
    Remove-Item $source -Recurse -Force
}
Write-Host "fixtures written to $fixtures"
