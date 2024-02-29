---
uid: snippets
---

# Snippets

Code snippets are ready-made snippets of code you can quickly insert into your code. For example, the **piPrintItSnippet** inserts code `print.it();` and moves the text cursor into the `()`. Snippets appear in the completion list, together with types, functions, etc. To show the list, in the code editor start typing a snippet name or press `Ctrl+Space`.

In the **Snippets** window (menu **Tools > Snippets**) you can add/delete/edit/hide/unhide your snippets and hide/unhide default snippets. Right-click to add/delete. Uncheck to hide (don't show in the completion list).

A snippet can show a menu of sub-snippets. Use it to group similar snippets together, to reduce the number of snippets in the completion list. To create a menu of sub-snippets, at first create a snippet (without code or with code of the first menu item), then right-click it and add more menu items.

Snippet properties:
- **Name** - is displayed in the completion list. Also it is the text shortcut.
- **Info** - short text displayed in the completion list item flyout (info window) above code. Also menu item text; use `&` for keyboard shortcuts.
- **Info+** - optional text displayed in the flyout below code.
- **Print** - optional text displayed in the output panel when inserting the snippet. Can contain [output tags](xref:output_tags) if starts with `<>`.
- **using** - zero or more namespaces separated by semicolon. The program will insert using directives in the correct place if need. Example: `System.Windows;System.Windows.Controls`.
- **var** - `$var$` variable type and default name, like `Au.popupMenu,m`. When inserting the snippet, the program looks for a local variable of the specified type, and replaces `$var$` in snippet code with the variable name, or with the default name if not found. To see how it works, in code editor insert **popupMenuSnippet** and then **menuItemSnippet** (it contains `$var$`).
- **Code** - snippet code.

Snippet code can contain these variables:
- `$end$` - final text cursor position.
- `$end$text$end$` - finally selected text.
- `$guid$` - new GUID.
- `$random$` - random int number.
- `$var$` - local variable of type specified in the `$var$` field.

A context specifies where in code to add the snippets to the completion list. Several contexts can be combined.
- **Function** - inside function body. Also in the main script code (top-level statements).
- **Type** - inside a class, struct or interface but not inside functions. Use for snippets that insert entire methods, properties, etc.
- **Namespace** - outside of types. Use for snippets that insert entire types.
- **Attributes** - use for snippets that insert an `[attribute]`.
- **Line** - at the start of a line. For example check **Line** and **Any** for snippets that insert a `#directive`.
- **Any** - anywhere.

The program loads all snippet files with names that end with `Snippets.xml` from the program settings folder in the Documents folder. You can add/delete snippets files there. You can edit them in an XML editor too. A snippets file contains multiple snippets, grouped by context like in the **Snippets** window.

Default snippets are read-only, but you can clone a default snippet (right click, copy, paste) and edit the clone. If both snippets are checked and have the same name, the clone will be the first in the completion list.