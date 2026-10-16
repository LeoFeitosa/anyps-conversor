# Como contribuir

**Português** | [English](CONTRIBUTING.en.md)

Obrigado pelo interesse! Este projeto é a interface gráfica; a conversão em si é feita pelo
[AnyPS5](https://github.com/boykopovar/AnyPS5). Problemas de compatibilidade de jogos, funções do
sistema não implementadas e falhas do relinker devem ser reportados lá.

## Regras

- **Nada de conteúdo protegido.** Não envie jogos, arquivos de jogos, chaves, firmware, links para
  download desse tipo de material nem capturas de tela de jogos comerciais. Issues e pull requests
  com esse conteúdo são fechados e removidos.
- Commits seguem o [Conventional Commits](https://www.conventionalcommits.org/pt-br/v1.0.0/)
  (`feat:`, `fix:`, `docs:`, `build:`, `ci:`, `test:`, `refactor:`).
- Versões seguem o [SemVer](https://semver.org/lang/pt-BR/). Toda mudança visível ao usuário entra
  em `CHANGELOG.md` e `CHANGELOG.en.md`, na seção `[Unreleased]`.
- Todo texto público existe em português e inglês: interface (`src/AnyPS5Conversor/Strings.cs`),
  READMEs, CHANGELOGs, documentos e modelos do GitHub. Um texto novo precisa das duas versões.

## Ambiente

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download) e PowerShell 7.

```powershell
pwsh build/download-dependencies.ps1   # baixa e confere o relinker e as bibliotecas do AnyPS5
dotnet test                            # testes (os de conversão real precisam de ANYPS5_HOMEBREW)
dotnet publish src/AnyPS5Conversor -c Release -o publish
```

O executável final fica em `publish/AnyPS5-Conversor.exe`.

## Atualizar o AnyPS5

É automático: o workflow `AnyPS5 update` procura todo dia uma versão nova do AnyPS5 publicada há 7
dias ou mais, atualiza `build/dependencies.json`, roda os testes e as conversões reais e publica. Para
testar uma versão à mão, rode o workflow pela aba Actions com `dry_run` marcado.

Para conferir localmente as conversões reais:

```powershell
pwsh build/download-homebrew.ps1 -Destination $env:TEMP\homebrew
$env:ANYPS5_HOMEBREW = "$env:TEMP\homebrew"; dotnet test
```

## Publicar uma versão

1. Mova as entradas de `[Unreleased]` para uma nova seção `[X.Y.Z] - AAAA-MM-DD` no `CHANGELOG.md` e
   no `CHANGELOG.en.md`.
2. Atualize `<VersionPrefix>` em `Directory.Build.props`. O build falha se a tag não for igual a ele.
3. Crie a tag `vX.Y.Z` e envie: o GitHub Actions compila, testa, publica o executável em Releases e
   atualiza as imagens do README.
