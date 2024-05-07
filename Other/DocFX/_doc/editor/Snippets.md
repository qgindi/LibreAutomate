---
uid: snippets
---

# Snippets

Code snippets are small blocks of reusable code that you can quickly insert into your C# code. For example, the **piPrintItSnippet** inserts code `print.it();` and moves the text cursor into the `()`. Snippets appear in the completion list, together with types, functions, etc. To show the list, in the code editor start typing a snippet name or press `Ctrl+Space`. Snippets containing `${TM_SELECTED_TEXT}` also can be used to surround selected code (toolbar button **Surround** or menu **Edit -> Selection -> Surround**).

In the **Snippets** window (menu **Tools > Snippets**) you can add/delete/edit/hide/unhide your snippets and hide/unhide default snippets. Right-click to add/delete. Uncheck to hide (don't show in the completion list).

A snippet can show a menu of sub-snippets. Use it to group similar snippets together, to reduce the number of snippets in the completion list. To create a menu of sub-snippets, at first create a snippet (without code or with code of the first menu item), then right-click it and add more menu items.

Snippet properties:
- **Name** - is displayed in the completion list. Also it is the text shortcut. If ends with `Surround`, the snippet is available only for surround.
- **Info** - short text displayed in the completion list item flyout (info window). Also menu item text; use `&` for keyboard shortcuts.
- **Info+** - text displayed at the bottom of the flyout.
- **Print** - text to print in the output panel when inserting the snippet. Can contain [output tags](xref:output_tags) if starts with `<>`.
- **using** - namespaces to add as `using` directives if need. Example: `System.Windows; System.Windows.Controls`.
- **Meta** - file properties to add as `/*/ meta comments /*/` if need. Example: `c A.cs; nuget -\B`.
- ${VAR} - `${VAR}` variable type and default name, like `Au.popupMenu,m`. When inserting the snippet, the program looks for a local variable of the specified type, and replaces `${VAR}` in snippet code with the variable name, or with the default name if not found. To see how it works, in code editor insert **menuSnippet** and then **menuItemSnippet** (it contains `${VAR}`).
- **Code** - snippet code.

Snippet code can contain fields and variables, like in [VSCode](https://code.visualstudio.com/docs/editor/userdefinedsnippets).

Example:
```csharp
for (int ${1:i} = 0; $1 < ${2:count}; $1++) {
	${TM_SELECTED_TEXT}$0
}
```

Fields:
- `$0` - final text cursor position when the user presses `Enter` or several `Tab`.
- `${n:text}` - a field with tab stop index `n` (`1`, `2` ...) and default text. After inserting the snippet code, fields can be selected with `Tab`. Default text can be a snippet variable, like `${1:$TM_FILENAME_BASE}`.
- `$n` or `${n}` - another instance of a `${n:text}` field. Its text is updated together. If there is no matching `${n:text}` field, it's a field without default text.

Variables:
- `${TM_SELECTED_TEXT}` - selected text. Used only by the "surround" feature; else no text is added.
- `${TM_FILENAME_BASE}` - the name of the current C# file, without path and extension.
- `${RANDOM}` - random number in decimal format.
- `${RANDOM_HEX}` - random number in hexadecimal format.
- `${UUID}` - new GUID.
- `${VAR}` - local variable of type specified in the `${VAR}` field.

Escape sequences: `\$`, `\\`, `\}`.

Other VSCode snippet variables and field features are not supported.

The program loads all snippet files with names that end with `Snippets.xml` from the program settings folder in the Documents folder. You can add/delete snippets files there. You can edit them in an XML editor too.

Default snippets are read-only, but you can clone a default snippet (right click, copy, paste) and edit the clone. If both snippets are checked and have the same name, the clone will be the first in the completion list.