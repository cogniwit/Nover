# Nover Languages

Nover includes two external language packs:

```text
Chinese/
  messages.ini
  help.txt
English/
  messages.ini
  help.txt
```

By default, `nover.config` uses `lang=auto`. Chinese systems use `Chinese`,
English systems use `English`, and other systems use `English`.

Switch and save a language with:

```powershell
nover --lang
nover --lang Chinese
nover --lang English
```

`nover --lang` lists every complete language directory and marks the default
and current language.

Each language directory must contain both `messages.ini` and `help.txt`.

If the configured language is unavailable, Nover uses `English`. If English
is also unavailable, it tries the remaining complete language directories in
name order.

To add another language, copy `Chinese` or `English` to a new directory under
`lang`, translate the values while preserving message keys and `{0}`
placeholders, then run:

```powershell
nover --lang MyLanguage
```
