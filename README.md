# Nover

简体中文 | [English](README.en.md)

Nover 是一个便携式 Windows Node.js 全局版本管理器。它使用稳定的 shim 切换 `node`、`npm` 和 `npx`，不需要管理员权限，也不会创建 junction。

```powershell
nover install lts --use
node -v
npm install
```

Nover 管理当前用户的一个全局 Node.js 版本。所有版本、配置和运行状态都保存在程序目录中，整个目录可以直接移动。

## 设计原则

- 全局版本切换应保持简单、直接
- 不依赖管理员权限或目录链接
- 配置、版本和运行状态应便于整体移动
- 下载、解压和删除操作应具备明确的安全边界

## 特点

- 只维护一个全局 Node.js 版本
- 切换版本不需要管理员权限
- 不创建 junction 或符号链接
- 支持 `x64`、`arm64` 和 `x86`
- 语言资源完全外置
- 便携运行，配置和版本跟随程序目录
- 提供在线版本查询和安装状态诊断

## 环境要求

- Windows 10 或更高版本
- .NET Framework 4.5 或更高版本
- 源码构建需要 PowerShell 5.1

## 快速开始

从 GitHub Releases 获取 Nover，并解压到一个固定位置：

```text
D:\Tools\Nover
```

进入该目录并运行：

```powershell
.\nover.exe
```

首次运行会提示创建 shim，并将 Nover 目录加入用户 PATH：

```text
Nover setup is required.
Create shims in "D:\Tools\Nover" and add this directory to the user PATH? [y/n]:
```

输入 `y` 继续。初始化完成后，请打开新终端：

```powershell
nover install lts --use
node -v
```

初始化或修复完成后，请重新打开终端以确保环境得到更新。

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

### 安装选项

| 选项 | 说明 |
| --- | --- |
| `--use` | 安装后立即切换 |
| `--arch <arch>` | `x64`、`arm64` 或 `x86` |
| `--mirror <url>` | 临时使用其他 Node.js 镜像 |

### 在线查询选项

| 选项 | 说明 |
| --- | --- |
| `--lts` | 只显示 LTS 版本 |
| `--all` | 显示全部匹配版本 |
| `--major <major>` | 按主版本筛选 |
| `--offset <count>` | 跳过指定数量的结果 |
| `--limit <count>` | 限制结果数量，默认 20，最大 5000 |
| `--mirror <url>` | 临时使用其他 Node.js 镜像 |

`--all` 和 `--limit` 不能同时使用。

## 版本选择

Nover 只支持清晰的数字版本选择：

| 格式 | 含义 |
| --- | --- |
| `latest` | 最新 Current 版本 |
| `lts` | 最新 LTS 版本 |
| `20` | 最高的 Node 20 版本 |
| `20.20` | 最高的 Node 20.20 版本 |
| `20.20.2` | 精确的 Node 20.20.2 |

## 常用操作

安装并切换：

```powershell
nover install 20.20.2 --use
```

查看本地版本：

```powershell
nover list
```

```text
Current: v20.20.2

Installed:
  22.23.4
  20.20.2
```

查询在线版本：

```powershell
nover remote --limit 50
nover remote 20 --all
nover remote --major 24 --lts
```

删除版本：

```powershell
nover remove 20
```

当前全局版本不能删除。删除前请先切换到其他版本。

检查安装状态：

```powershell
nover check
```

## 工作原理

Nover 在程序目录中维护：

```text
nover.exe       主程序
node.exe        根据 active.txt 转发到真实 node.exe
npm.cmd         转发到当前版本 npm
npx.cmd         转发到当前版本 npx
active.txt      当前全局版本
versions\       已安装的 Node.js 版本
```

切换版本时只更新 `active.txt`，因此不需要删除或重建目录链接。

## 移动目录

关闭正在使用 Nover 的终端，然后移动整个目录：

```powershell
Move-Item -LiteralPath D:\Tools\Nover -Destination E:\Apps\Nover
cd E:\Apps\Nover
.\nover.exe
```

如果 `install_path` 与当前位置不同，Nover 会询问是否更新用户 PATH。确认后会删除已知旧路径，并将新路径放到用户 PATH 前部。

Nover 只管理用户 PATH。手动添加到系统 PATH 的旧路径需要单独删除。

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

构建完成后还会在项目根目录生成干净的发布包和校验文件：

```text
Nover-<版本>-windows.zip
Nover-<版本>-windows.zip.sha256
```

ZIP 内容为 `nover.exe`、`nover.config`、`LICENSE` 和 `lang/`，不包含本机的 `versions`、shim 或 `active.txt`。

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
  outputs/
  Nover-<版本>-windows.zip
  Nover-<版本>-windows.zip.sha256
```

## 许可证

本项目使用 [MIT License](LICENSE)。

Nover 是独立项目，与 Node.js 官方没有隶属关系。
