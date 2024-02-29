---
uid: wildcard_expression
---

# Wildcard expression

*Wildcard expression* is a simple text format that supports wildcard characters, regular expression, "match case", "text1 or text2" and "not text". Like a regular expression, but much simpler. Used with "find" functions, for example [wnd.find]().

Wildcard characters:

| Character | Will match | Examples |
| :- | :- | :- |
| `*` | Zero or more of any characters. | `"start*"`, `"*end"`, `"*middle*"` |
| `?` | Any single character. | `"date ????-??-??"` |

There are no escape sequences for `*` and `?` characters, unless you use regular expression.

By default case-insensitive. Always culture-insensitive.

Can start with `**options `:

| Option | Description | Examples |
| :- | :- | :- |
| `t` | `*` and `?` in text are not wildcard characters. | `"**t text"` |
| `r` | Text is PCRE regular expression ([regexp]()).<br/>Syntax: [full](https://www.pcre.org/current/doc/html/pcre2pattern.html), [short](https://www.pcre.org/current/doc/html/pcre2syntax.html). | `"**r regex"` |
| `R` | Text is .NET regular expression (**Regex**).<br/>Cannot be used with [elm]() and [elmFinder](). | `"**R regex"` |
| `c` | Must match case. | `"**tc text"`, `"**rc regex"` |
| `m` | Multi-part, separated by `||`. | `"**m findAAA||orBBB||**r orCCC"` |
| `m(sep)` | Multi-part, separated by `sep`. | `"**m(^^^) findAAA^^^orBBB"` |
| `n` | Must not match. | `"**mn notAAA||andNotBBB"` |

If the function argument is `null` or omitted, it usually means "match any". Wildcard expression `""` matches only `""`. Exception **ArgumentException** if invalid `**options ` or regular expression.

Examples:
```csharp
//Find window. Its name ends with "- Notepad" and program is "notepad.exe".
var w = wnd.find("*- Notepad", program: "notepad.exe");

//Find item in x. Its property 1 is "example" (case-insensitive), property 2 starts with "2017-" and property 3 matches a case-sensitive regular expression.
var item = x.FindItem("example", "2017-*", "**rc regex");
```

### See also

[wildex]()<br/>[ExtString.Like]()
