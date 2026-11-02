# Nover

English | [简体中文](README.md) | [Changelog](CHANGELOG.md)

Nover is a portable global Node.js version manager for Windows. It uses stable shims to switch `node`, `npm`, and `npx` without administrator privileges or directory junctions.

```powershell
nover install lts --use
node -v
npm install
```

Nover manages one global Node.js version for the current user. Versions, configuration, and runtime state remain inside the program directory, so the entire directory can be moved.

## Design Principles

- Global version switching should stay simple and direct
- No administrator rights or directory links should be required
- Configuration, versions, and runtime state should remain easy to move together
- Downloads, extraction, and deletion should have clear security boundaries

## Features

- Manages one global Node.js version
- Switches versions without administrator privileges
- Does not create junctions or symbolic links
- Supports `x64`, `arm64`, and `x86`
- Follows the system language by default and supports external language packs
- Remains portable, with configuration and versions stored beside the executable
- Provides remote version listing and installation diagnostics

## Requirements

- Windows 10 or later
- .NET Framework 4.5 or later
- PowerShell 5.1 when building from source

## Quick Start

Obtain Nover from GitHub Releases and extract it to a permanent location:

```text
D:\Tools\Nover
```

Enter that directory and run:

```powershell
.\nover.exe
```

To force English on a Chinese-language system, run `nover --lang English` first.

The first run asks whether to create shims and add the Nover directory to the user PATH:

```text
Nover setup is required.
Create shims in "D:\Tools\Nover" and add this directory to the user PATH? [y/n]:
```

Enter `y` to continue. After initialization, open a new terminal:

```powershell
nover install lts --use
node -v
```

After initialization or repair, reopen the terminal to ensure the environment is updated.

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

### General Option

| Option | Description |
| --- | --- |
| `--lang <language>` | Switch and save a language directory under `lang\` |

### Install Options

| Option | Description |
| --- | --- |
| `--use` | Activate after installation |
| `--arch <arch>` | `x64`, `arm64`, or `x86` |
| `--mirror <url>` | Use another Node.js distribution mirror |

### Remote Query Options

| Option | Description |
| --- | --- |
| `--lts` | Show LTS releases only |
| `--all` | Show all matching releases |
| `--major <major>` | Filter by major version |
| `--offset <count>` | Skip a number of results |
| `--limit <count>` | Limit results; default 20, maximum 5000 |
| `--mirror <url>` | Use another Node.js distribution mirror |

`--all` and `--limit` cannot be used together.

## Languages

When no language is configured manually, Nover follows the Windows UI language:

```text
Chinese system -> Chinese
English system -> English
Other systems  -> English
```

The default setting is `lang=auto`.

After a manual change, the selected language is saved in `nover.config`; later commands do not need `--lang`:

```powershell
nover --lang
nover --lang English
nover list

nover --lang Chinese
```

Runtime language files are stored in `lang\Chinese` and `lang\English`. To add another language, copy either directory to a new name and translate its files:

```text
lang\MyLanguage\
  messages.ini
  help.txt
```

Then run:

```powershell
nover --lang MyLanguage
```

Each language directory must contain both `messages.ini` and `help.txt`.

If the configured language directory is missing, Nover falls back to English first. If English is also unavailable, it tries the remaining complete language directories in name order.

## Version Selection

Nover supports only clear numeric version selectors:

| Format | Meaning |
| --- | --- |
| `latest` | Latest Current release |
| `lts` | Latest LTS release |
| `24` | Highest Node 24 release |
| `24.22` | Highest Node 24.22 patch release |
| `24.22.1` | Exact Node 24.22.1 |

## Common Workflows

Install and activate:

```powershell
nover install 24.22.1 --use
```

List local versions:

```powershell
nover list
```

```text
Current: v24.22.1

Installed:
  24.22.1
  22.23.4
```

Query remote versions:

```powershell
nover remote --limit 50
nover remote 24 --all
nover remote --major 24 --lts
```

Remove a version:

```powershell
nover remove 24
```

The active global version cannot be removed. Activate another version first.

Check the installation:

```powershell
nover check
```

## How It Works

Nover keeps the following files in its program directory:

```text
nover.exe       Main executable
node.exe        Dispatches to the real node.exe selected by active.txt
npm.cmd         Dispatches to npm from the active version
npx.cmd         Dispatches to npx from the active version
active.txt      Active global version
versions\       Installed Node.js versions
```

Switching versions updates `active.txt`; it does not delete or rebuild directory links.

## Moving the Directory

Close terminals that are using Nover, then move the entire directory:

```powershell
Move-Item -LiteralPath D:\Tools\Nover -Destination E:\Apps\Nover
cd E:\Apps\Nover
.\nover.exe
```

If `install_path` differs from the current location, Nover asks whether to update the user PATH. After confirmation, it removes the known old path and places the new path at the front of the user PATH.

Nover only manages the user PATH. Old paths added manually to the system PATH must be removed separately.

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

The build also creates a clean release archive and checksum in the project root:

```text
Nover-<version>-windows.zip
Nover-<version>-windows.zip.sha256
```

The ZIP contains `nover.exe`, `nover.config`, `LICENSE`, and `lang/`. It does not include local `versions`, shims, or `active.txt`.

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

This project is licensed under the [MIT License](LICENSE).

Nover is an independent project and is not affiliated with the official Node.js project.
