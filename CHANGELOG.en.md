# Changelog

[Português](CHANGELOG.md) | **English**

All notable changes to this project are recorded here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.9.0] - 2026-10-16

### Changed

- Disk space is checked for the whole process before starting: for an archive, the extracted game, the
  AnyPS5 libraries and the generated `.exe`. If space is short, the warning comes before anything is
  extracted, not after a long extraction.
- Time left covers the whole process: extraction, measured live, and conversion, estimated from how
  long this PC took in earlier conversions. On the first conversion on a PC, with no measurement yet,
  only extraction is estimated, with the text "to extract".

### Fixed

- Games with backup copies of modules (such as `libc.prx.esbak`, left by signing tools) were refused
  with "Ambiguous guest dependency name". Only real modules (`.prx`, `.sprx`, `.suprx`, `.debug_prx`)
  are passed to the relinker.

## [0.8.0] - 2026-10-16

### Changed

- Archives are now extracted and converted in one go, with a single progress bar, just like a folder.
  When the archive is chosen, the program only checks its index, in seconds and without extracting
  anything: it refuses what has no game, asks for the password if there is one and, in non-solid
  `.zip` and `.rar` archives, already shows the game name, code and icon. Extraction starts when
  **Convert** is clicked, and the bar is split between extraction and conversion according to the size
  of the game.
- If converting an archive fails or is cancelled, the extraction is deleted and the next attempt starts
  from scratch, with nothing left on disk.
- Settings from the old version are only migrated when no old version is open.

### Added

- "Open the game as soon as the conversion finishes" option, which can be checked or unchecked at any
  time, even during the conversion, and is remembered between runs.

### Fixed

- The number and the done mark in the step circles are centered.

## [0.7.0] - 2026-10-16

### Changed

- The project is now called **AnyPS Conversor** (`anyps-conversor`), so it can take other PlayStation
  consoles in the future. The executable is now `AnyPS-Conversor.exe`, and the default folder for
  converted games is `%USERPROFILE%\AnyPS`. PS5 is still handled by AnyPS5.
- Settings and logs from the previous version (`%APPDATA%\AnyPS5-Conversor` and
  `%LOCALAPPDATA%\AnyPS5-Conversor`) are moved to the new folders on the first run: language and chosen
  location stay the same.
- Leftovers of earlier versions in temporary folders with the old name are cleaned up, but only when
  the program that owns them is no longer open; an old version running at the same time is not
  affected.
- Internally, everything that depends on the console (the file that identifies a game, validation,
  reading and conversion) goes through a platform, currently only PS5.

## [0.6.3] - 2026-10-16

### Added

- Below the status, the program shows what is happening: the file being extracted, the executable
  being read, the relinker step (instruction scan, system calls, game libraries, assembling and writing
  the `.exe`) and the file being copied. Long names are shortened in the middle.
- Time left for extraction ("about N min left"), shown only after 5 seconds and 3% measured on the PC
  itself, when the estimate is stable. Building the `.exe` has no estimate: the relinker steps take
  very different times from game to game, and the number would not be reliable.
- `.zip`, `.rar` and `.7z` archives are checked before extracting: without `eboot.bin`, or with an
  encrypted one, the archive is refused right away, without writing anything to disk.

### Fixed

- In `.tar.gz`, `.tar.xz` and `.tar.bz2`, the progress bar stood still during a large file; it now
  moves along with the reading.

## [0.6.2] - 2026-10-16

### Added

- The program images in the READMEs are generated from the `.exe` itself on every release, in
  Portuguese (`docs/screenshot-pt.png`) and English (`docs/screenshot-en.png`), and always show the
  published version. The output folder shown is a neutral example.

## [0.6.1] - 2026-10-16

### Added

- **Licenses** link in the program footer, with the license notices of the third-party components
  embedded in the `.exe` (`THIRD-PARTY-NOTICES.txt`, also attached to every release).
- Full legal notice in Portuguese and English (`LEGAL.md`), security policy (`SECURITY.md`) and a
  required confirmation, in the issue form, that no protected content is being shared. Every release
  note carries the legal notice and the link to the source code of the included AnyPS5.
- Full English README (`README.en.md`), with a language switch, and a direct download link to the
  latest version at the top of both.
- Real conversion test (`HomebrewConversionTests`) with free homebrew downloaded from each author's
  release by `build/download-homebrew.ps1`, with version and SHA-256 pinned in `build/homebrew.json`.
- Automatic AnyPS5 update (`.github/workflows/anyps5-update.yml`): once a day it adopts the new AnyPS5
  version published 7 or more days earlier, builds, runs the tests and the real conversions and
  publishes the new converter version. It notifies the repository owner through an issue whenever
  there is a new version, whether it works or not; if anything fails, nothing is published.

## [0.6.0] - 2026-10-16

### Changed

- Building the `.exe` runs at the same time as copying the game into `app0/` and installing the
  libraries; a conversion takes as long as the slowest step, not the sum of them.
- A game folder on the same drive as the destination goes into `app0/` through hard links: instant
  and with no extra space. The original is not changed, and deleting one does not affect the other.
  Read-only files, other drives and file systems without support are still copied.
- Copies reserve the file size before writing.

### Fixed

- Cancel stops right away: the relinker is ended at once, cleaning up what was already created does
  not freeze the window, and the button is locked after the first click, showing "Cancelling…".
- An archive without `eboot.bin` gets its own message, which talks about the file and not "this
  folder".
- An extraction that does not turn into a game (no `eboot.bin`, password cancelled or error) deletes
  the work folder in "Where to save" right away, instead of waiting for the program to close.

## [0.5.0] - 2026-10-16

### Changed

- Archives are extracted in a hidden folder (`.AnyPS5-Conversor`) inside the location chosen in "Where
  to save", no longer in `%TEMP%` on C:. Saving to another drive takes the extraction with it.
- On the same drive, extracted files are moved into `app0/` instead of copied: the step is instant and
  peak disk use drops to the archive plus one copy of its contents.
- The location chosen in "Where to save" is remembered between runs.

## [0.4.1] - 2026-10-16

### Changed

- Warnings and errors appear in a window that matches the program design, instead of the Windows
  message box. Long texts, such as log excerpts, get a scroll bar.

## [0.4.0] - 2026-10-16

### Added

- Disk space check before extracting an archive and before converting. If space is short, the program
  says how much is needed, how much is free and how much to free up, without starting anything. If the
  disk still fills up during the process, incomplete files are deleted and the message explains it.
- The language can be changed at any time, even during an extraction or conversion.

### Fixed

- Changing the language after converting a game from an archive failed while reloading the icon from
  the already deleted extraction.

### Changed

- The program version comes only from `Directory.Build.props`: a tag build fails if they differ, and
  builds outside a published version show as `-dev.N` in the footer.

## [0.3.1] - 2026-10-16

### Fixed

- In the password window, long file names cut off the end of the message. Now the name is shortened in
  the middle, keeping the start and the extension, the message always shows in full and the window
  adjusts its height.

### Changed

- Nothing stays on disk without need: the relinker is extracted to the run's temporary folder (a copy
  per version used to stay in `%LOCALAPPDATA%`), the log of a successful conversion is deleted at the
  end, and only the 10 most recent failure logs are kept, for up to 30 days.

## [0.3.0] - 2026-10-16

### Added

- "When done, move the original game files to the Recycle Bin" option, unchecked by default. It moves
  the chosen archive or only the folder with `eboot.bin`, never a folder above it, and keeps the
  originals when they are at the root of a drive, in a system or user folder (Desktop, Documents,
  Downloads…) or when they contain the destination folder.

### Changed

- The extraction of an archive is deleted right after the conversion, not only when the program
  closes.

## [0.2.1] - 2026-10-16

### Fixed

- Extractions and work files stayed forever in `%TEMP%\AnyPS5-Conversor` when the program was killed
  or the PC shut down, taking up gigabytes. Each run now uses its own subfolder, and opening the
  program deletes what was left by earlier runs.

## [0.2.0] - 2026-10-16

### Added

- Password-protected archives (AES or ZipCrypto `.zip`, `.7z` and `.rar`, including archives with a
  protected file list): the program asks for the password in its own window, says when it is wrong and
  does not store it. 7-Zip or WinRAR are not needed.

## [0.1.0] - 2026-10-16

### Added

- Graphical interface in Portuguese and English to convert a PS5 game or app folder into a Windows
  executable, with no command line.
- Input by folder, by `eboot.bin` or by archive (`.zip`, `.rar`, `.7z`, `.tar`, `.tar.gz`, `.tgz`,
  `.bz2`, `.xz`), with a button or by dragging onto the window. `eboot.bin` is searched up to four
  subfolder levels deep. Entries that try to write outside the destination folder are refused.
- Title name, code and icon read from `sce_sys/param.json`; the game icon goes into the generated
  `.exe`.
- Unwrapping of decrypted SELF/fSELF containers (port of AnyPS5's `tools/unwrap_self.py`) and refusal
  of encrypted files.
- Conversion with the AnyPS5 v0.1.2 relinker and installation of the AnyPS5 Windows libraries, all
  embedded in the executable, with nothing to install and no internet access.
- Automatic `--to-intel` on Intel CPUs and a warning when the CPU lacks the instructions games
  require.
- Automatic fixed base `0x400000` (`--windows-fixed-base`) for titles that read their own
  `imports.txt`, such as Ruffle, VLC and XPSemu.
- Progress bar with the real percentage of building the `.exe`, copying the files and extracting.
- Log of every conversion in `%LOCALAPPDATA%\AnyPS5-Conversor\logs`, shown in the error message.
- Buttons to play and open the folder at the end, with the last lines of `anyps5.log` if the game
  closes with an error.

[Unreleased]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.9.0...HEAD
[0.9.0]: https://github.com/LeoFeitosa/anyps-conversor/compare/v0.8.0...v0.9.0
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
