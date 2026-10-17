# Changelog

**Português** | [English](CHANGELOG.en.md)

Todas as mudanças relevantes deste projeto são registradas aqui.
O formato segue o [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa
[Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Unreleased]

## [0.8.0] - 2026-10-16

### Alterado

- Arquivo compactado agora é extraído e convertido de uma vez, numa única barra de progresso, como uma
  pasta. Ao escolher o arquivo, o programa só confere o índice, em segundos e sem extrair nada:
  recusa o que não tem jogo, pede a senha se houver e, em `.zip` e `.rar` não sólidos, já mostra nome,
  código e ícone do jogo. A extração começa ao clicar em **Converter**, e a barra é dividida entre
  extração e conversão conforme o tamanho do jogo.
- Se a conversão de um arquivo compactado falha ou é cancelada, a extração é apagada e a próxima
  tentativa começa do zero, sem sobras no disco.
- As configurações da versão antiga só são migradas quando nenhuma versão antiga está aberta.

### Adicionado

- Opção "Abrir o jogo assim que a conversão terminar", que pode ser marcada ou desmarcada a qualquer
  momento, inclusive durante a conversão, e fica lembrada entre execuções.

### Corrigido

- O número e o sinal de concluído nas bolinhas dos passos ficam centralizados.

## [0.7.0] - 2026-10-16

### Alterado

- O projeto passa a se chamar **AnyPS Conversor** (`anyps-conversor`), para poder receber outros consoles
  PlayStation no futuro. O executável agora é `AnyPS-Conversor.exe`, e a pasta padrão dos jogos
  convertidos, `%USERPROFILE%\AnyPS`. O PS5 continua sendo feito pelo AnyPS5.
- As configurações e os registros da versão anterior (`%APPDATA%\AnyPS5-Conversor` e
  `%LOCALAPPDATA%\AnyPS5-Conversor`) são levados para as pastas novas na primeira execução: idioma e
  local escolhido continuam os mesmos.
- O que versões anteriores deixaram em pastas temporárias com o nome antigo é limpo, mas só quando o
  programa dono delas não está mais aberto; uma versão antiga rodando ao mesmo tempo não é afetada.
- Internamente, tudo o que depende do console (arquivo que identifica o jogo, validação, leitura e
  conversão) passa por uma plataforma, hoje só a do PS5.

## [0.6.3] - 2026-10-16

### Adicionado

- Embaixo do status aparece o que está acontecendo: o arquivo sendo extraído, o executável sendo lido,
  a etapa do relinker (análise de instruções, chamadas de sistema, bibliotecas do jogo, montagem e
  gravação do `.exe`) e o arquivo sendo copiado. Nomes longos são encurtados pelo meio.
- Tempo restante da extração ("cerca de N min restantes"), mostrado só depois de 5 segundos e 3%
  medidos no próprio PC, quando a estimativa já é estável. A geração do `.exe` não tem estimativa:
  as etapas do relinker têm durações muito diferentes de jogo para jogo, e o número não seria confiável.
- Arquivos `.zip`, `.rar` e `.7z` são conferidos antes de extrair: sem `eboot.bin`, ou com ele
  criptografado, o arquivo é recusado na hora, sem gravar nada no disco.

### Corrigido

- Em `.tar.gz`, `.tar.xz` e `.tar.bz2`, a barra de progresso ficava parada durante um arquivo grande;
  agora avança junto com a leitura.

## [0.6.2] - 2026-10-16

### Adicionado

- As imagens do programa nos READMEs são geradas a partir do próprio `.exe` a cada release, em
  português (`docs/screenshot-pt.png`) e em inglês (`docs/screenshot-en.png`), e sempre mostram a
  versão publicada. A pasta de saída mostrada é um exemplo neutro.

## [0.6.1] - 2026-10-16

### Adicionado

- Link **Licenças** no rodapé do programa, com os avisos de licença dos componentes de terceiros
  embutidos no `.exe` (`THIRD-PARTY-NOTICES.txt`, também anexado a cada release).
- Aviso legal completo em português e inglês (`LEGAL.md`), política de segurança (`SECURITY.md`) e
  confirmação obrigatória, no formulário de issue, de que nenhum conteúdo protegido está sendo
  enviado. As notas de cada release trazem o aviso legal e o link do código-fonte do AnyPS5 incluído.
- README completo em inglês (`README.en.md`), com seletor de idioma, e link de download direto da
  versão mais recente no topo das duas versões.
- Teste de conversão real (`HomebrewConversionTests`) com homebrews livres baixados do release de cada
  autor por `build/download-homebrew.ps1`, com versão e SHA-256 fixados em `build/homebrew.json`.
- Atualização automática do AnyPS5 (`.github/workflows/anyps5-update.yml`): uma vez por dia, adota a
  versão nova do AnyPS5 publicada há 7 dias ou mais, compila, roda os testes e as conversões reais e
  publica a nova versão do conversor. Avisa o dono do repositório por issue quando houver versão nova,
  dando certo ou não; se algo falhar, nada é publicado.

## [0.6.0] - 2026-10-16

### Alterado

- A geração do `.exe` roda ao mesmo tempo que a cópia do jogo para `app0/` e a instalação das
  bibliotecas; a conversão leva o tempo da etapa mais lenta, não a soma delas.
- Pasta de jogo no mesmo disco do destino vai para `app0/` por link físico: instantâneo e sem ocupar
  espaço a mais. O original não é alterado, e apagar um não afeta o outro. Arquivos somente leitura,
  outros discos e sistemas de arquivos sem suporte continuam sendo copiados.
- As cópias reservam o tamanho do arquivo antes de gravar.

### Corrigido

- Cancelar para na hora: o relinker é encerrado de imediato, a limpeza do que já foi gerado não trava a
  janela e o botão fica bloqueado depois do primeiro clique, com o aviso "Cancelando…".
- Arquivo compactado sem `eboot.bin` ganha um aviso próprio, que fala do arquivo e não de "esta pasta".
- Extração que não vira jogo (sem `eboot.bin`, senha cancelada ou erro) apaga na hora a pasta de
  trabalho em "Onde salvar", em vez de esperar o programa fechar.

## [0.5.0] - 2026-10-16

### Alterado

- A extração de um arquivo compactado é feita numa pasta oculta (`.AnyPS5-Conversor`) dentro do local
  escolhido em "Onde salvar", e não mais em `%TEMP%` no C:. Salvar em outro disco leva a extração
  junto.
- No mesmo disco, os arquivos extraídos são movidos para `app0/` em vez de copiados: a etapa fica
  instantânea e o pico de espaço cai para o arquivo compactado mais uma cópia do conteúdo.
- O local escolhido em "Onde salvar" fica lembrado entre execuções.

## [0.4.1] - 2026-10-16

### Alterado

- Avisos e erros aparecem numa janela no visual do programa, no lugar da caixa de mensagem do Windows.
  Textos longos, como trechos de registro, ganham barra de rolagem.

## [0.4.0] - 2026-10-16

### Adicionado

- Verificação de espaço em disco antes de extrair um arquivo compactado e antes de converter. Se faltar
  espaço, o programa diz quanto é necessário, quanto há livre e quanto liberar, sem começar nada. Se o
  disco encher mesmo assim no meio do processo, os arquivos incompletos são apagados e a mensagem explica.
- O idioma pode ser trocado a qualquer momento, inclusive durante uma extração ou conversão.

### Corrigido

- Trocar o idioma depois de converter um jogo vindo de arquivo compactado dava erro ao recarregar o
  ícone da extração já apagada.

### Alterado

- A versão do programa vem só de `Directory.Build.props`: o build de uma tag falha se ela for diferente,
  e builds fora de uma versão publicada aparecem como `-dev.N` no rodapé.

## [0.3.1] - 2026-10-16

### Corrigido

- Na janela de senha, nomes de arquivo longos cortavam o fim da mensagem. Agora o nome é encurtado pelo
  meio, mantendo o início e a extensão, a mensagem aparece sempre inteira e a janela ajusta a altura.

### Alterado

- Nada fica no disco sem necessidade: o relinker é extraído na pasta temporária da execução (antes
  ficava uma cópia por versão em `%LOCALAPPDATA%`), o registro de uma conversão bem-sucedida é
  apagado ao final e só ficam os 10 registros de falha mais recentes, por até 30 dias.

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

[Unreleased]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.8.0...HEAD
[0.8.0]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.6.3...v0.7.0
[0.6.3]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.6.2...v0.6.3
[0.6.2]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.6.1...v0.6.2
[0.6.1]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.6.0...v0.6.1
[0.6.0]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.4.1...v0.5.0
[0.4.1]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/LeoFeitosa/anyps-conversor/releases/tag/v0.1.0
