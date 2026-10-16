# Contributing

[Português](CONTRIBUTING.md) | **English**

Thanks for your interest! This project is the graphical interface; the conversion itself is done by
[AnyPS5](https://github.com/boykopovar/AnyPS5). Game compatibility problems, unimplemented system
functions and relinker failures should be reported there.

## Rules

- **No protected content.** Do not submit games, game files, keys, firmware, download links for such
  material or screenshots of commercial games. Issues and pull requests with such content are closed
  and removed.
- Commits follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/)
  (`feat:`, `fix:`, `docs:`, `build:`, `ci:`, `test:`, `refactor:`).
- Versions follow [SemVer](https://semver.org/). Every user-visible change goes into `CHANGELOG.md`
  and `CHANGELOG.en.md`, under `[Unreleased]`.
- Every public text exists in Portuguese and English: the interface (`src/AnyPSConversor/Strings.cs`),
  READMEs, changelogs, documents and GitHub templates. A new text needs both versions.

## Environment

Requirements: [.NET SDK 10](https://dotnet.microsoft.com/download) and PowerShell 7.

```powershell
pwsh build/download-dependencies.ps1   # downloads and verifies the AnyPS5 relinker and libraries
dotnet test                            # tests (the real conversion ones need ANYPS5_HOMEBREW)
dotnet publish src/AnyPSConversor -c Release -o publish
```

The final executable is `publish/AnyPS-Conversor.exe`.

## Updating AnyPS5

It is automatic: every day the `AnyPS5 update` workflow looks for a new AnyPS5 version published 7 or
more days earlier, updates `build/dependencies.json`, runs the tests and the real conversions and
publishes. To try a version by hand, run the workflow from the Actions tab with `dry_run` checked. Every
adopted version gets a backup copy of the binaries in an internal `anyps5-<version>` release
(`build/mirror-anyps5.ps1`), which `build/download-dependencies.ps1` uses if the official release
disappears, always checking the same SHA-256.

To run the real conversions locally:

```powershell
pwsh build/download-homebrew.ps1 -Destination $env:TEMP\homebrew
$env:ANYPS5_HOMEBREW = "$env:TEMP\homebrew"; dotnet test
```

## Publishing a version

1. Move the `[Unreleased]` entries to a new `[X.Y.Z] - YYYY-MM-DD` section in `CHANGELOG.md` and
   `CHANGELOG.en.md`.
2. Update `<VersionPrefix>` in `Directory.Build.props`. The build fails if the tag does not match it.
3. Create and push the `vX.Y.Z` tag: GitHub Actions builds, tests, publishes the executable in
   Releases and updates the README images.
