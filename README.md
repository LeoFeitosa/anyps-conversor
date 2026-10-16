# AnyPS5 Conversor

[![Build](https://github.com/LeoFeitosa/anyps5-conversor/actions/workflows/build.yml/badge.svg)](https://github.com/LeoFeitosa/anyps5-conversor/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/LeoFeitosa/anyps5-conversor)](https://github.com/LeoFeitosa/anyps5-conversor/releases/latest)
[![License: GPL-2.0](https://img.shields.io/badge/license-GPL--2.0-blue)](LICENSE)

Interface gráfica para Windows que converte jogos e apps de PS5 em executáveis do Windows, sem
linha de comando. Você escolhe a pasta do jogo, clica em **Converter para Windows** e, no fim,
em **Jogar agora**.

*English version below.*

![Tela do AnyPS5 Conversor](docs/screenshot.png)

## Baseado no AnyPS5

Este projeto é uma interface para o [AnyPS5](https://github.com/boykopovar/AnyPS5), de
[boykopovar](https://github.com/boykopovar) e colaboradores. Todo o trabalho pesado é deles: o
relinker que transforma o executável do PS5 num executável nativo e as bibliotecas do sistema
reimplementadas. O AnyPS5 Conversor só junta essas peças num programa que qualquer pessoa consegue
usar:

| Parte | Origem |
|---|---|
| Relinker (`relinker.exe`) | Release oficial do AnyPS5, embutido sem alterações |
| Bibliotecas para Windows (`libs/`) | Release oficial do AnyPS5 (`prx-windows`), embutidas sem alterações |
| Desembrulho de SELF | Porte para C# de [`tools/unwrap_self.py`](https://github.com/boykopovar/AnyPS5/blob/main/tools/unwrap_self.py) do AnyPS5 |
| Interface, idiomas e montagem da pasta final | Este repositório |

A versão do AnyPS5 usada em cada build fica fixada em [`build/dependencies.json`](build/dependencies.json),
com o hash SHA-256 de cada arquivo.

## Como usar

1. Baixe o `AnyPS5-Conversor.exe` na página de [Releases](https://github.com/LeoFeitosa/anyps5-conversor/releases/latest).
2. Abra o programa. Não precisa instalar nada.
3. Arraste o jogo para a janela, ou use **Escolher pasta** ou **Escolher arquivo**. Pode ser a pasta
   onde fica o `eboot.bin` ou um arquivo `.zip`, `.rar`, `.7z`, `.tar`, `.tar.gz`, `.tgz`, `.bz2` ou
   `.xz` com ela dentro. Se o arquivo tiver senha, o programa pede. Não precisa ter 7-Zip
   ou WinRAR instalado.
4. Clique em **Converter para Windows** e depois em **Jogar agora**.

O jogo convertido fica numa pasta nova, por padrão em `%USERPROFILE%\AnyPS5\<nome do jogo>`, com o
executável, a pasta `libs/` e os arquivos originais em `app0/`. A pasta pode ser movida para
qualquer lugar.

Também dá para arrastar a pasta do jogo direto sobre o ícone do `AnyPS5-Conversor.exe`.

## Requisitos

- Windows 10 ou 11, 64 bits.
- Processador com AVX2, FMA, F16C, BMI1, BMI2, LZCNT e MOVBE (Ryzen e Intel Core a partir de 2015,
  em geral). O programa avisa se faltar alguma. Em processadores Intel a conversão usa
  automaticamente a opção `--to-intel` do AnyPS5.
- Placa de vídeo com driver Vulkan atualizado.
- O jogo precisa estar **descriptografado**. Arquivos criptografados são recusados, assim como pacotes
  `.pkg` da loja, que são criptografados.

Cada conversão deixa um registro em `%LOCALAPPDATA%\AnyPS5-Conversor\logs`. Se um jogo converte mas não abre ou trava, quase sempre é uma função do sistema que o AnyPS5 ainda
não implementa. O progresso por título fica no
[mapa de compatibilidade](https://alxcbtw.github.io/anyps5-roadmap/) e os relatos vão nas
[discussões do AnyPS5](https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up).

## Compilar

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download) e PowerShell 7.

```powershell
pwsh build/download-dependencies.ps1
dotnet test
dotnet publish src/AnyPS5Conversor -c Release -o publish
```

O resultado é um único `publish/AnyPS5-Conversor.exe`, com o runtime do .NET, o relinker e as
bibliotecas embutidos. Os builds oficiais são feitos pelo GitHub Actions a cada tag `vX.Y.Z` e
publicados em Releases junto com o SHA-256.

Detalhes sobre versões, commits e testes estão em [CONTRIBUTING.md](CONTRIBUTING.md). O histórico
de mudanças está em [CHANGELOG.md](CHANGELOG.md).

## Aviso legal

- Este projeto não inclui, distribui nem ajuda a obter jogos, firmware, chaves ou bibliotecas
  proprietárias. Use apenas jogos que você extraiu do seu próprio console ou homebrew de
  distribuição livre.
- O programa não quebra nenhuma proteção: ele só aceita executáveis que já estão descriptografados.
- Projeto independente, sem vínculo com a Sony Interactive Entertainment. PlayStation e PS5 são
  marcas da Sony Interactive Entertainment, citadas aqui apenas para descrever compatibilidade.

## Licença

[GNU General Public License v2.0](LICENSE), a mesma do AnyPS5. O executável distribui o relinker e
as bibliotecas do AnyPS5; o código-fonte de ambos está em
[boykopovar/AnyPS5](https://github.com/boykopovar/AnyPS5) e o deste programa, aqui.

---

## English

A Windows GUI that converts PS5 games and apps into Windows executables, with no command line.
Choose the game folder, click **Convert to Windows**, then **Play now**. The interface is available
in English and Portuguese.

It is a front end for [AnyPS5](https://github.com/boykopovar/AnyPS5) by boykopovar and
contributors, who wrote the relinker and the reimplemented system libraries that do the actual
work. This program embeds their official release unchanged (version pinned in
`build/dependencies.json`), adds a C# port of their `unwrap_self.py`, and arranges the output
folder.

**Usage:** download `AnyPS5-Conversor.exe` from Releases, run it (no install), drop the game folder
(the one with `eboot.bin`) on the window, click **Convert to Windows**, then **Play now**.

**Requirements:** Windows 10/11 x64, a CPU with AVX2, FMA, F16C, BMI1, BMI2, LZCNT and MOVBE, a GPU
with a current Vulkan driver, and a decrypted game. Encrypted files are refused.

**Legal:** no games, firmware, keys or proprietary libraries are included, and nothing is decrypted
or circumvented. Not affiliated with Sony Interactive Entertainment; PlayStation and PS5 are their
trademarks. Licensed under GPL-2.0, like AnyPS5.
