---
uid: key_names
---

# Key names and operators

This string syntax is used with [keys.send]() and other keyboard functions of this library.

Example:

```csharp
keys.send("A F2 Ctrl+Shift+A Enter*2"); //keys A, F2, Ctrl+Shift+A, Enter Enter
```

## Key names

### Named keys
- **Modifier:** `Alt`, `Ctrl`, `Shift`, `Win`, `RAlt`, `RCtrl`, `RShift`, `RWin`
- **Navigate:** `Esc`, `End`, `Home`, `PgDn`, `PgUp`, `Down`, `Left`, `Right`, `Up`
- **Other:** `Back`, `Del`, `Enter`, `Apps`, `Pause`, `PrtSc`, `Space`, `Tab`
- **Function:** `F1`-`F24`<br>**Lock:** `CapsLock`, `NumLock`, `ScrollLock`, `Ins`

Must start with an uppercase character. Only the first 3 characters are significant; others can be any ASCII letters. For example, can be `"Back"`, `"Bac"`, `"Backspace"` or `"BACK"`, but not `"back"` or `"Ba"` or `"Back5"`.

Alias: `AltGr`=`RAlt`, `Menu`=`Apps`, `PageDown`=`PgDn`, `PD`=`PgDn`, `PageUp`=`PgUp`, `PU`=`PgUp`, `PrintScreen`=`PrtSc`, `PS`=`PrtSc`, `BS`=`Back`, `PB`=`Pause`, `CL`=`CapsLock`, `NL`=`NumLock`, `SL`=`ScrollLock`, `HM`=`Home`.

### Text keys
- **Alphabetic:** `A`-`Z` (or `a`-`z`)
- **Number:** `0`-`9`
- **Numeric keypad:** `#/`, `#*`, `#-`, `#+`, `#.`, `#0`-`#9`
- **Other:** `` ` ``, `-`, `=`, `[`, `]`, `\`, `;`, `'`, `,`, `.`, `/`

Spaces between keys are optional, except for uppercase A-Z. For example, can be `"A B"`, `"a b"`, `"A b"` or `"ab"`, but not `"AB"` or `"Ab"`.

Alias: `~`=`` ` ``, `{`=`[`, `}`=`]`, `|`=`\`, `:`=`;`, `"`=`'`, `<`=`,`, `>`=`.`, `?`=`/`.

### Other keys
- Names of enum [KKey]() members. Example: `keys.send("BrowserBack");`
- Virtual-key codes. Prefix `VK` or `Vk`. Example: `keys.send("VK65 VK0x42");`
- Unavailable: `Fn` (hardware-only key).

### Special characters
- **Operator:** `+`, `*`, `(`, `)`, `_`, `^`
- **Numpad key prefix:** `#`
- **Text/HTML argument prefix:** `!`, `%`
- **Reserved:** `@`, `$`, `&`

These characters cannot be used as keys. Instead use `=`, `8`, `9`, `0`, `-`, `6`, `3`, `1`, `5`, `2`, `4`, `7`.

## Operators

| Operator | Examples | Description |
| --- | --- | --- |
| `*n` | `"Left*3"`<br>`$"Left*{i}"` | Press key n times, like `"Left Left Left"`.<br>See [keys.AddRepeat](). |
| `*down` | `"Ctrl*down"` | Press key and don't release. |
| `*up` | `"Ctrl*up"` | Release key. |
| `+` | `"Ctrl+Shift+A"`<br>`"Alt+E+P"` | The same as `"Ctrl*down Shift*down A Shift*up Ctrl*up"` and `"Alt*down E*down P E*up Alt*up"`. |
| `+()` | `"Alt+(E P)"` | The same as `"Alt*down E P Alt*up"`.<br>Inside `()` cannot be used operators `+`, `+()` and `^`. |
| `_` | `"Tab _A_b Tab"`<br>`"Alt+_e_a"`<br>`"_**20"` | Send next character like text with option [OKeyText.KeysOrChar]().<br>Can be used to `Alt`-select items in menus, ribbons and dialogs regardless of current keyboard layout.<br>Next character can be any 16-bit character, including operators and whitespace. |
| `^` | `"Alt+^ea"` | Send all remaining characters and whitespace like text with option [OKeyText.KeysOrChar]().<br>For example `"Alt+^ed b"` is the same as `"Alt+_e_d Space _b"`.<br>`Alt` is released after the first character. Don't use other modifiers. |

Operators and related keys can be in separate arguments. Examples: `keys.send("Shift+", KKey.A); keys.send(KKey.A, "*3");`.
