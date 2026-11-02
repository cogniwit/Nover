# Changelog

本项目采用 [Keep a Changelog](https://keepachangelog.com/) 的结构，并使用语义化版本号。

This project follows the [Keep a Changelog](https://keepachangelog.com/) format and Semantic Versioning.

## 中文

### [1.0.2] - 2026-11-02

#### 新增

- 增加外置语言目录 `lang\Chinese` 和 `lang\English`。
- 增加 `nover --lang`，用于列出可用语言并标记默认语言和当前语言。
- 增加 `nover --lang <目录名>`，用于切换并保存语言。
- 支持用户自行添加语言目录，只要目录中包含 `messages.ini` 和 `help.txt`。
- 构建时生成干净的 Windows Release ZIP 和 SHA-256 校验文件。

#### 变更

- `lang=auto` 成为默认配置：中文系统使用 Chinese，英文及其他系统使用 English。
- 手动选择的语言保存到 `nover.config`，后续命令无需重复指定。
- 配置语言不可用时优先回退 English，再尝试其他完整语言目录。
- 无参数运行时，未初始化则执行初始化，已初始化则显示帮助。
- 帮助页精简为用法、命令、选项、版本目标和示例。

#### 修复

- 修复精确版本选择只比较主版本和次版本、未比较补丁版本的问题。
- 修复默认语言文件缺失时会直接退出、不再寻找其他语言的问题。
- 修复语言目录不存在时可能保存无效语言设置的问题。

#### 移除

- 移除内嵌语言资源，语言统一从 `lang` 目录读取。

## English

### [1.0.2] - 2026-11-02

#### Added

- Added external `lang\Chinese` and `lang\English` language directories.
- Added `nover --lang` to list available languages and mark the default and active language.
- Added `nover --lang <directory>` to switch and save the language.
- Added support for user-provided language directories containing `messages.ini` and `help.txt`.
- Added clean Windows release ZIP and SHA-256 checksum generation during the build.

#### Changed

- `lang=auto` is now the default: Chinese systems use Chinese, while English and other systems use English.
- A manually selected language is saved in `nover.config`, so later commands do not need `--lang`.
- When a configured language is unavailable, Nover falls back to English first and then other complete language directories.
- With no arguments, Nover initializes when needed and otherwise shows help.
- Simplified help into usage, commands, options, targets, and examples.

#### Fixed

- Fixed exact version selection so it compares the patch component.
- Fixed startup exiting immediately when the configured language was missing.
- Fixed invalid language settings being saved when a language directory was unavailable.

#### Removed

- Removed embedded language resources; languages are now loaded from `lang`.
