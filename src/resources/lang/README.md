# Nover Language Packs

Each language directory contains:

```text
messages.ini   Localized messages and errors
help.txt       Complete help text
```

To add a language, copy `default` to a new directory named with a BCP 47 language code:

```powershell
Copy-Item -LiteralPath .\default -Destination .\fr -Recurse
```

Translate only the values in `messages.ini`; keep all keys unchanged.

The `{0}`, `{1}`, and similar placeholders are runtime values and must remain in the translated text.

`messages.ini` supports partial translations. Missing keys fall back to English.

`help.txt` replaces the complete help page. Keep `{0}` where the Nover version should appear.

Enable the language in `nover.config` next to `nover.exe`. The configured value is used directly as the directory name:

```ini
lang=zh-CN
```

Language matching is strict:

```text
lang=<code> requires lang\<code>
if the directory does not exist, fall back to lang\default
```

Examples:

```text
lang=fr      -> lang\fr
lang=fr-CA   -> lang\fr-CA
lang=zh-CN   -> lang\zh-CN
lang=zh      -> lang\zh
```

`lang=fr` does not use `lang\fr-CA`, and `lang=zh` does not use `lang\zh-CN`.

If neither the requested language directory nor `lang\default` exists, Nover reports:

```text
Language pack missing. Please download Nover again.
```

## Language Code Source

`lang=auto` uses:

```csharp
System.Globalization.CultureInfo.CurrentUICulture.Name
```

Windows and .NET expose BCP 47-style tags such as `zh-CN`, `en-US`, and `ja-JP`.

Check the current value with:

```powershell
(Get-UICulture).Name
```

Matching is case-insensitive but exact. No parent-language or regional fallback is performed.

The `default` directory is the fallback pack, the translation reference, and the baseline for partially translated messages. If a separate `en-US` pack is required, create `lang\en-US` explicitly.
