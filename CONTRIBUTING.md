# Como contribuir

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
  em `CHANGELOG.md`, na seção `[Unreleased]`.
- Textos da interface existem em português e inglês (`src/AnyPS5Conversor/Strings.cs`); um texto
  novo precisa das duas versões.

## Ambiente

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download) e PowerShell 7.

```powershell
pwsh build/download-dependencies.ps1   # baixa e confere o relinker e as bibliotecas do AnyPS5
dotnet test                            # testes
dotnet publish src/AnyPS5Conversor -c Release -o publish
```

O executável final fica em `publish/AnyPS5-Conversor.exe`.

## Atualizar o AnyPS5

1. Altere a versão e os arquivos em `build/dependencies.json` e calcule os novos SHA-256.
2. Rode `pwsh build/download-dependencies.ps1` e converta ao menos um homebrew livre para conferir.
3. Registre a mudança em `CHANGELOG.md`.

## Publicar uma versão

1. Mova as entradas de `[Unreleased]` para uma nova seção `[X.Y.Z] - AAAA-MM-DD` no `CHANGELOG.md`.
2. Atualize `<Version>` em `Directory.Build.props`.
3. Crie a tag `vX.Y.Z` e envie: o GitHub Actions compila, testa e publica o executável em Releases.
