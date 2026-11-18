# Nover

简体中文 | [English](README.en.md) | [更新日志](CHANGELOG.md)

Nover 是一个便携的 Windows Node.js 全局版本管理器。

只维护一个全局版本，使用 shim 切换 `node`、`npm`、`npx`，无需管理员权限、不建 junction 或符号链接，版本、配置与运行状态随程序目录整体移动。支持 `x64`/`arm64`/`x86`，界面语言跟随系统，并提供在线版本查询与安装诊断。

```powershell
nover install lts --use
node -v
npm install
```

## 环境要求

- Windows 10 或更高版本
- .NET Framework 4.5 或更高版本
- 从源码构建需 PowerShell 5.1

## 快速开始

从 GitHub Releases 下载，解压到固定位置（如 `D:\Tools\Nover`），进入该目录运行：

```powershell
.\nover.exe
```

首次运行会提示创建 shim 并把 Nover 目录加入用户 PATH：

```text
需要初始化 Nover。
是否在 "D:\Tools\Nover" 创建 shim，并将该目录加入用户 PATH？[y/n]：
```

输入 `y` 后重开终端，使 PATH 生效，再执行：

```powershell
nover install lts --use
node -v
```

无参数运行时：未初始化则执行初始化；已初始化且无需修复则显示帮助。

## 命令

| 命令 | 说明 |
| --- | --- |
| `nover install <target> [options]` | 安装 Node.js 版本 |
| `nover use <version>` | 切换到已安装版本 |
| `nover list` / `nover ls` | 列出本地版本和当前版本 |
| `nover remote [major] [options]` | 查询在线版本 |
| `nover remove <version>` / `nover rm <version>` | 删除已安装版本 |
| `nover check` | 检查目录、shim、PATH 和配置 |
| `nover --version` | 显示 Nover 版本 |
| `nover --help` | 显示帮助 |

### 选项

| 选项 | 适用 | 说明 |
| --- | --- | --- |
| `--lang <language>` | 通用 | 切换并保存 `lang\` 下的语言目录 |
| `--use` | install | 安装后立即切换 |
| `--arch <arch>` | install | `x64`、`arm64` 或 `x86` |
| `--mirror <url>` | install、remote | 临时使用其他 Node.js 镜像 |
| `--lts` | remote | 只显示 LTS 版本 |
| `--all` | remote | 显示全部匹配版本，与 `--limit` 互斥 |
| `--major <major>` | remote | 按主版本筛选 |
| `--offset <count>` | remote | 跳过指定数量的结果 |
| `--limit <count>` | remote | 限制结果数量，默认 20，最大 5000 |

## 版本选择

| 格式 | 含义 |
| --- | --- |
| `latest` | 最新 Current 版本 |
| `lts` | 最新 LTS 版本 |
| `24` | 最高的 Node 24 版本 |
| `24.22` | 最高的 Node 24.22 补丁版本 |
| `24.22.1` | 精确的 Node 24.22.1 |

只支持上表中的数字写法，不接受模糊版本。

## 常用操作

```powershell
nover install 24.22.1 --use    # 安装并切换
nover list                     # 查看本地版本
nover remote 24 --all          # 查询 Node 24 全部版本
nover remote --major 24 --lts  # 只查 LTS
nover remove 24                # 删除版本
nover check                    # 检查安装状态
```

`nover list` 的输出：

```text
Current: v24.22.1

Installed:
  24.22.1
  22.23.4
```

当前全局版本不能删除，删除前请先切换到其他版本。

## 工作原理

Nover 在程序目录中维护：

```text
nover.exe       主程序
node.exe        按 active.txt 转发到真实 node.exe
npm.cmd         转发到当前版本 npm
npx.cmd         转发到当前版本 npx
active.txt      当前全局版本
versions\       已安装的 Node.js 版本
```

切换版本只更新 `active.txt`，因此不需要删除或重建目录链接。

## 语言

默认为 `lang=auto`：中文系统用 Chinese，其他系统用 English。手动切换后会写入 `nover.config`，后续命令无需重复指定：

```powershell
nover --lang English
nover list
```

运行时自带 `lang\Chinese` 和 `lang\English`。新增语言只需复制任一目录、翻译其中的文件：

```text
lang\MyLanguage\
  messages.ini
  help.txt
```

再执行 `nover --lang MyLanguage` 即可。每个语言目录必须同时包含 `messages.ini` 和 `help.txt`。

配置的语言目录不存在时，优先回退到 English；English 也不可用时，按名称顺序寻找其他完整目录。

## 移动目录

关闭正在使用 Nover 的终端，然后移动整个目录：

```powershell
Move-Item -LiteralPath D:\Tools\Nover -Destination E:\Apps\Nover
cd E:\Apps\Nover
.\nover.exe
```

若 `install_path` 与当前位置不符，Nover 会询问是否更新用户 PATH；确认后删除已知旧路径，并把新路径放到用户 PATH 前部。Nover 只管理用户 PATH，手动加到系统 PATH 的旧路径需自行清理。

## 从源码构建

在 `build.ps1` 所在目录执行：

```powershell
.\build.ps1
```

构建结果写入项目内的 `outputs`：

```text
outputs/
  nover.exe
  nover.config
  LICENSE
  lang/
```

同时在项目根目录生成发布包与校验文件：

```text
Nover-<版本>-windows.zip
Nover-<版本>-windows.zip.sha256
```

ZIP 仅含 `nover.exe`、`nover.config`、`LICENSE` 和 `lang/`，不含本机的 `versions`、shim 和 `active.txt`。

项目结构：

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
  Nover-<版本>-windows.zip
  Nover-<版本>-windows.zip.sha256
```

## 许可证

本项目使用 [MIT License](LICENSE) 发布。Nover 是独立项目，与 Node.js 官方没有隶属关系。
