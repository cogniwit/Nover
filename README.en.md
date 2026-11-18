# Nover

English | [简体中文](README.md) | [Changelog](CHANGELOG.md)

Nover is a portable Windows Node.js global version manager.

It keeps a single global version and switches `node`, `npm`, and `npx` through shims — no administrator rights, no junctions or symlinks — with versions, configuration, and runtime state travelling alongside the program directory. Builds cover `x64`/`arm64`/`x86`, the UI follows the system language, and remote version listing plus installation diagnostics are built in.

```powershell
nover install lts --use
node -v
npm install
```

## Requirements

- Windows 10 or later
- .NET Framework 4.5 or later
- PowerShell 5.1 when building from source

## Quick Start

Download Nover from GitHub Releases, extract it to a permanent location (e.g. `D:\Tools\Nover`), and run the executable from that directory:

```powershell
.\nover.exe
```

The first run asks whether to create shims and add the Nover directory to the user PATH:

```text
Nover setup is required.
Create shims in "D:\Tools\Nover" and add this directory to the user PATH? [y/n]:
```

Enter `y`, then reopen the terminal so the PATH takes effect:

```powershell
nover install lts --use
node -v
```

On a Chinese-language system, run `nover --lang English` to keep the UI in English.

Without arguments, Nover initializes itself when needed; otherwise it shows help.

## Commands

| Command | Description |
| --- | --- |
| `nover install <target> [options]` | Install a Node.js version |
| `nover use <version>` | Activate an installed version |
| `nover list` / `nover ls` | List local and active versions |
| `nover remote [major] [options]` | Query remote versions |
| `nover remove <version>` / `nover rm <version>` | Remove an installed version |
| `nover check` | Check directories, shims, PATH, and configuration |
| `nover --version` | Show the Nover version |
| `nover --help` | Show help |

### Options

| Option | Applies to | Description |
| --- | --- | --- |
| `--lang <language>` | all | Switch and save a language directory under `lang\` |
| `--use` | install | Activate after installation |
| `--arch <arch>` | install | `x64`, `arm64`, or `x86` |
| `--mirror <url>` | install, remote | Use another Node.js distribution mirror |
| `--lts` | remote | Show LTS releases only |
| `--all` | remote | Show all matching releases; conflicts with `--limit` |
| `--major <major>` | remote | Filter by major version |
| `--offset <count>` | remote | Skip a number of results |
| `--limit <count>` | remote | Limit results; default 20, maximum 5000 |

## Version Selection

| Format | Meaning |
| --- | --- |
| `latest` | Latest Current release |
| `lts` | Latest LTS release |
| `24` | Highest Node 24 release |
| `24.22` | Highest Node 24.22 patch release |
| `24.22.1` | Exact Node 24.22.1 |

Only the forms above are accepted; fuzzy selectors are rejected.

## Common Workflows

```powershell
nover install 24.22.1 --use    # install and activate
nover list                     # list local versions
nover remote 24 --all          # all Node 24 releases
nover remote --major 24 --lts  # LTS releases only
nover remove 24                # remove a version
nover check                    # verify the installation
```

Output of `nover list`:

```text
Current: v24.22.1

Installed:
  24.22.1
  22.23.4
```

The active global version cannot be removed; activate another version first.

## How It Works

Nover keeps these files in the program directory:

```text
nover.exe       Main executable
node.exe        Dispatches to the real node.exe selected by active.txt
npm.cmd         Dispatches to npm from the active version
npx.cmd         Dispatches to npx from the active version
active.txt      Active global version
versions\       Installed Node.js versions
```

Switching versions only rewrites `active.txt`, so no directory links are deleted or rebuilt.

## Languages

The default is `lang=auto`: Chinese systems use Chinese, everything else uses English. A manual switch is saved to `nover.config`, so later commands need no `--lang`:

```powershell
nover --lang English
nover list
```

Runtime ships with `lang\Chinese` and `lang\English`. To add a language, copy either directory and translate its files:

```text
lang\MyLanguage\
  messages.ini
  help.txt
```

Then run `nover --lang MyLanguage`. Every language directory must contain both `messages.ini` and `help.txt`.

If the configured language directory is missing, Nover falls back to English; if English is unavailable too, it picks the first complete directory in name order.

## Moving the Directory

Close any terminal using Nover, then move the whole directory:

```powershell
Move-Item -LiteralPath D:\Tools\Nover -Destination E:\Apps\Nover
cd E:\Apps\Nover
.\nover.exe
```

If `install_path` differs from the current location, Nover asks whether to update the user PATH; on confirmation it removes the known old path and puts the new one at the front. Nover manages the user PATH only — old paths added manually to the system PATH must be cleaned up separately.

## Build from Source

Run the build from the directory containing `build.ps1`:

```powershell
.\build.ps1
```

The build output is written to `outputs` inside the project:

```text
outputs/
  nover.exe
  nover.config
  LICENSE
  lang/
```

It also produces a release archive and checksum in the project root:

```text
Nover-<version>-windows.zip
Nover-<version>-windows.zip.sha256
```

The ZIP holds `nover.exe`, `nover.config`, `LICENSE`, and `lang/` only — no local `versions`, shims, or `active.txt`.

Project structure:

```text
Nover/
  build.ps1
  README.md
  README.en.md
  LICENSE
  src/
    *.cs
    resources/
      nover.config
      nover.ico
      lang/
        Chinese/
        English/
  outputs/
  Nover-<version>-windows.zip
  Nover-<version>-windows.zip.sha256
```

## License

This project is licensed under the [MIT License](LICENSE). Nover is an independent project and is not affiliated with the official Node.js project.
