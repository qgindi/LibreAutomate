## Version 1..0 (2024-)

### Editor
More snippet features:
- Snippets can contain multiple fields that can be selected with `Tab` or `Enter`. When leaving an edited field, updates text of related fields.
- Formats the inserted snippet code.
- The **Snippets** tool can import Visual Studio and VSCode snippets.
- Surround with snippet.
- Snippet context detected automatically.
- A snippet can add `/*/ meta comments /*/`.

Auto-format when completing current statement (adding `;`, `{  }` etc). See **Options > Code editor > Formatting > Auto-format**.

Changes in code correction features:
- Now the "complete statement" hotkey (`Ctrl+Enter` etc) can be set in **Options**.
- Removed: `Esc` prevents statement completion on `Enter` before `)`. Now instead use `Shift` etc or type space before `)`. Also added option to disable this feature.
- Use `Backspace` to exit current multiline `{ block }` when the text cursor is in the last line of the block and that line is blank. Previously `Ctrl+Enter` worked in a similar way; now doesn't.
- Removed: `Ctrl+Enter` adds `break;` in `switch`.
- Several improvements.

New small features:
- Drag and drop a non-script file from the **Files** panel to the code editor: can add `/*/ meta comments /*/`.

New tools:
- .

New cookbook recipes:
- .

Improved:
- Several small improvements.

Fixed bugs:
- Debugger settings: does not work **not**.
- And more.

### Library
New classes:
- .

New members:
- **wnd.IsMatch** overload for multiple windows.

New parameters:
- .

Improved:
- .

Fixed bugs:
- .

### Breaking changes
