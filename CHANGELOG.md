# Changelog

Todas as mudanças relevantes deste projeto são registradas aqui.
O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa
[Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Unreleased]

## [0.3.0] - 2026-10-16

### Adicionado

- Opção "Ao terminar, mover os arquivos originais do jogo para a Lixeira", desmarcada por padrão. Move
  o arquivo compactado escolhido ou só a pasta com o `eboot.bin`, nunca uma pasta acima dela, e
  mantém os originais quando eles estão na raiz do disco, numa pasta do sistema ou do usuário
  (Área de Trabalho, Documentos, Downloads…) ou quando contêm a pasta de destino.

### Alterado

- A extração de um arquivo compactado é apagada logo depois da conversão, e não só ao fechar o
  programa.

## [0.2.1] - 2026-10-16

### Corrigido

- Extrações e arquivos de trabalho ficavam para sempre em `%TEMP%\AnyPS5-Conversor` quando o programa
  era encerrado à força ou o PC desligava, ocupando gigabytes. Cada execução agora usa uma subpasta
  própria, e ao abrir o programa apaga o que sobrou de execuções anteriores.

## [0.2.0] - 2026-10-16

### Adicionado

- Arquivos compactados com senha (`.zip` AES ou ZipCrypto, `.7z` e `.rar`, inclusive com a lista de
  arquivos protegida): o programa pede a senha numa janela própria, avisa quando ela está errada e não
  guarda a senha. Não é preciso ter 7-Zip ou WinRAR instalado.

## [0.1.0] - 2026-10-16

### Adicionado

- Interface gráfica em português e inglês para converter a pasta de um jogo ou app de PS5 em um
  executável do Windows, sem linha de comando.
- Entrada por pasta, pelo `eboot.bin` ou por arquivo compactado (`.zip`, `.rar`, `.7z`, `.tar`,
  `.tar.gz`, `.tgz`, `.bz2`, `.xz`), por botão ou arrastando para a janela. O `eboot.bin` é procurado
  em até quatro níveis de subpastas. Entradas que tentam gravar fora da pasta de destino são recusadas.
- Leitura do nome, do código e do ícone do título em `sce_sys/param.json`; o ícone do jogo vai para o
  `.exe` gerado.
- Desembrulho de contêineres SELF/fSELF descriptografados (porte de `tools/unwrap_self.py` do
  AnyPS5) e recusa de arquivos criptografados.
- Conversão com o relinker do AnyPS5 v0.1.2 e instalação das bibliotecas do AnyPS5 para Windows,
  tudo embutido no executável, sem instalar nada nem acessar a internet.
- Uso automático de `--to-intel` em processadores Intel e aviso quando a CPU não tem as instruções
  exigidas pelos jogos.
- Base fixa `0x400000` (`--windows-fixed-base`) automática para títulos que leem o próprio
  `imports.txt`, como Ruffle, VLC e XPSemu.
- Barra de progresso com a porcentagem real da geração do `.exe`, da cópia dos arquivos e da extração.
- Registro de cada conversão em `%LOCALAPPDATA%\AnyPS5-Conversor\logs`, indicado na mensagem de erro.
- Botões para jogar e abrir a pasta ao final, com as últimas linhas de `anyps5.log` se o jogo
  fechar com erro.

[Unreleased]: https://github.com/LeoFeitosa/anyps5-conversor/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/LeoFeitosa/anyps5-conversor/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/LeoFeitosa/anyps5-conversor/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/LeoFeitosa/anyps5-conversor/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/LeoFeitosa/anyps5-conversor/releases/tag/v0.1.0
