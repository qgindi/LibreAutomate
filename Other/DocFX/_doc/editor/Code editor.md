---
uid: code_editor
---

# Code editor
In the code editor you edit automation scripts and other C# code.

C# code example:
```csharp
mouse.click(10, 20);
if (keys.isCtrl) {
	print.it("text");
}
```

To create the above code, you can type this text:
```csharp
mo.c 10, 20
if ke.isct

pi "text
```

While typing, editor completes words, inserts code snippets, adds `()`, `;`, `{}` and indentation. The result is the first code.

## Features

<!--
To generate TOC with #links, use the VS context menu command "Generate TOC". Then remove the indentation.
SHOULDDO: now the Back button does not scroll to TOC. And the same in all DocFX-generated pages.
-->
<!--TOC-->
- [List of symbols, autocompletion](#list-of-symbols-autocompletion)
- [Bracket completion](#bracket-completion)
- [Statement completion](#statement-completion)
- [Auto indentation](#auto-indentation)
- [Parameter info](#parameter-info)
- [Quick info](#quick-info)
- [Error info](#error-info)
- [XML documentation comments](#xml-documentation-comments)
- [Go to symbol documentation](#go-to-symbol-documentation)
- [Go to symbol definition (source code), base](#go-to-symbol-definition-source-code-base)
- [Go to script, file, URL](#go-to-script-file-url)
- [Find and replace text](#find-and-replace-text)
- [Find symbol references](#find-symbol-references)
- [Highlight symbol references and matching braces](#highlight-symbol-references-and-matching-braces)
- [Find symbol](#find-symbol)
- [Rename symbol](#rename-symbol)
- [Outline of current file](#outline-of-current-file)
- [Navigate back/forward](#navigate-backforward)
- [Bookmarks](#bookmarks)
- [Code coloring](#code-coloring)
- [Text folding](#text-folding)
- [Separators between functions/types](#separators-between-functionstypes)
- [Snippets](#snippets)
- [Images in code](#images-in-code)
- [Format code](#format-code)
- [Comment/uncomment/indent/unindent lines](#commentuncommentindentunindent-lines)
- [Capture UI elements, insert regex etc, implement interface](#capture-ui-elements-insert-regex-etc-implement-interface)
- [Find Windows API and insert declarations](#find-windows-api-and-insert-declarations)
- [Drag and drop files to insert path](#drag-and-drop-files-to-insert-path)
- [Focus](#focus)
- [WPF window preview](#wpf-window-preview)
<!--/TOC-->

### List of symbols, autocompletion
When you start typing a word, editor shows a list of symbols (classes, functions, variables, C# keywords, etc) available there. Or you can press `Ctrl+Space` to show the list anywhere.

The list shows only items that contain the partially typed text. The text in list items is highlighted. Auto-selects an item that is considered the first best match. On `Ctrl+Space` shows all. You can use the vertical toolbar to filter and group list items, for example to show only methods.

While the list is visible, when you enter a non-word character (space, comma, etc) or press `Enter` or `Tab` or double-click an item, the partially or fully typed word is replaced with the text of the selected list item. Also may add `()` or `{}`, for example if it is a method or a keyword like `if` or `finally`. The added text may be different when you press `Enter`; for example may add `{}` instead of `()`. By default adds `()` only if completed with space; see **Options > Code**.

To select list items you also can click or press arrow or page keys. It does not hide the list. The tooltip-like window next to the list shows more info about the selected item, including links to online documentation and source code if available. Shows all overloads (functions with same name but different parameters), and you can click them to view their info.

To hide the list without inserting item text you can press `Esc` or click somewhere in code.

While the list is visible, you can press the **[+]** button or `Ctrl+Space` to show another list that includes all types or extension methods, not only those from `using` namespaces.

### Bracket completion
When you type `(`, `[`, `{`, `<`, `"` or `'`, editor adds the closing `)`, `]`, `}`, `>`, `"` or `'`. Then, while the text cursor is before the added `)` etc, typing another `)` or `Tab` just leaves the enclosed area. Also then `Backspace` erases both characters.

### Statement completion
When you press `Enter` inside a function argument list before the last `)`, editor adds missing `;` or `{  }`, adds new line and moves the text cursor there. To avoid it, press `Esc+Enter`. To complete statement without new line, use `;` instead of `Enter`.

`Ctrl+Enter`, `Shift+Enter` and `Ctrl+;` complete current statement when the text cursor is anywhere in it. If in an empty line at the end of a `{ }` block, leaves the block and removes empty lines. In a `switch` `case` section adds missing `break;`.

### Auto indentation
When you press `Enter`, editor adds new line with correct number of tabs (indentation). The same with `Ctrl+Enter`.

### Parameter info
When you type a function name and `(`, editor shows a tooltip-like window with info about the function and current parameter. To show the window from anywhere in an argument list, press `Ctrl+Shift+Space`. You can select oveloads with arrow keys or the mouse.

### Quick info
Whenever the mouse dwells on a symbol etc in the editor, a tooltip displays some info about the symbol, including documented exceptions the function may throw.

### Error info
Errors are detected in editor, as well as when compiling the code. Code parts with errors have red squiggly underlines, warnings green. A tooltip shows error/warning description. Also can contain links to fix the error, for example add a missing `using namespace` or Windows API declaration.

### XML documentation comments
Editor gets data for quick info, parameter info and other info about symbols from their assemblies and XML documentation comments. You can write XML documentation comments for your functions and types; look for how to on the internet. Documentation of the automation library and .NET is installed with the program. Documentation of other assemblies comes from their `assembly.dll` + `assembly.xml` files.

Editor also helps to write XML documentation comments. Adds empty summary and parameters when you type `///` above a class, method etc. Adds `///` on `Enter`, shows a list of tags, autocompletes tags, color-highlights tags, text and `see` references.

### Go to symbol documentation
To show symbol documentation if available, press `F1` when the text cursor is in it. Or click the **more info** link in the autocompletion item info or parameter info window.

If the symbol is from the automation library, it opens the online documentation page in your web browser. If the symbol is from .NET runtime or other assembly or unmanaged code (`[DllImport]` or `[ComImport]`), it opens the Google search page.

### Go to symbol definition (source code), base
Click a symbol and press `F12`. Or click the **source code** link in the autocompletion item info or parameter info window.

If the symbol is defined in your code, it opens that file and moves the text cursor. Else it can help to find the source code online.

The **Go to base** command finds the base class or implemented/overriden method definition.

### Go to script, file, URL
Click a file path string and press `F12` to select it in File Explorer. In the same way you can open folders, script files and web pages. It also works in meta comments, other comments and in code like `folders.System + @"notepad.exe"`. If the path etc does not start and end with `"`, at first select it.

### Find and replace text
Use the **Find** panel to find and replace text in editor. It marks all matches in editor with yellow. Also can find files by name and files containing text. Can replace text in multiple files.

### Find symbol references
The **Find references** command takes the symbol (class name, function name, variable, etc) at the current position and finds all places in current and related files where the symbol is used. Ignores matching words in comments, strings and `#if`-disabled code. Displays results in the **Found** panel. You can click a result line to go to that place. You can middle-click to close the file if it was opened from that results.

The **Find implementations** command is similar. If the symbol is an interface or class, finds types that implement or inherit it. If the symbol is an interface/abstract/virtual function, finds functions that implement or override it.

What is "related files":
- If current file is part of a @Project folder - all files of that project.
- Files used by current file/project through `/*/ c /*/`.
- If current file or project uses project references (`/*/ pr /*/`) and that project can use that symbol - all files of that projects.
- If the symbol definition file or project is used by other files or projects through `c` or `pr` - all that files/projects.
- If the symbol is defined in a dll or `global.cs`, does not search in entire workspace (it could be slow or gather many unwanted results), but searches in files/projects that use current file/project through `c`/`pr`.

### Highlight symbol references and matching braces
When the text cursor is in a symbol name, the editor automatically highlights that name everywhere in current document.

When the text cursor is before an opening brace `({[<`, highlights the matching closing brace `)}]>`. When after a closing brace, highlights the matching opening brace. When at the `#` of `#if` etc, highlights matching `#endif` etc.

To turn off these features: **Options > Font, colors > Symbol/brace highlight**, set color white and alpha 0.

### Find symbol
The **Find symbol** command shows a temporary window to find a symbol by name and go to its definition.

The search query can contain one or several parts of symbol name, like `part1 part2`. Or include the containing type, like `Type.name`. Or like `FM` to find `FindMe`.

To find only types, use prefix `t`, like `t name` or `t:name`. Prefix `m` is for members (function, field), `n` for namespaces.

To select a symbol, click or use arrow/page keys and `Enter`.

Finds symbols defined in current file or project + files and projects included through `/*/ c /*/` and `/*/ pr /*/`.

### Rename symbol
The **Rename symbol** command finds symbol definition/references like the **Find references** command, and replaces all with the specified new name. It is similar to the "Find and replace text in files" feature, but more precise.

If checked **Include comments/disabled/strings** and finds the word in comments/disabled/strings, at first displays these results in the **Found** panel. You can right-click some of them to exclude. Finally click the **Replace** link. The same if finds symbol usages that are ambiguous or error, for example a method name without parameters used in documentation comments `cref`.

It also automatically resolves name conflicts where possible, for example prepends namespace name.

### Outline of current file
The **Outline** panel shows functions and fields defined in current file. Also types, if there are multiple. And regions. Click to go to the definition.

### Navigate back/forward
The **Back** and **Forward** buttons work like in web browsers.

### Bookmarks
You can mark lines in code, and later go there. Use menu **Edit > Navigate**, panel **Bookmarks** and the markers margin.

Menu commands **Previous bookmark** and **Next bookmark** visit only active bookmarks. If there are no active bookmarks - only bookmarks in current document.

### Code coloring
Different kinds of code parts have different colors. Comments, strings, keywords, types, functions, etc.

### Text folding
You can hide and show code regions like in a tree view control: click the **[-]** or **[+]** in the left margin. Folding is available for functions, events, types, multiline comments, disabled code (`#if`), `#region` ... `#endregion` and `//.` ... `//..`.

`Ctrl`+click to show/hide descendant folds as well. `Shift`+click to show descendants. For more options right-click the folding margin.

### Separators between functions/types
Editor draws horizontal lines at the end of each function and type definition.

### Snippets
The autocompletion list also contains snippets. For example the **outSnippet** inserts code `print.it();` when you type `out` and space or `Tab` or `Enter` or click it.

### Images in code
Whenever code contains a string or comment that looks like an image file path or image embedded in code (`image:...`, usually hidden text), editor draws the image at the left. This feature can be enabled/disabled with the toolbar button.

### Format code
The **Format document/selection** commands insert/remove spaces, indentation and newlines to make code uniformly formatted. See also **Options > Code editor > Formatting**.

### Comment/uncomment/indent/unindent lines
To disable or enable a line of code by converting it to/from comments, you can use the toolbar button or **Edit** menu or right-click the selection margin. If multiple lines are selected, it converts all. If not full line(s) selected, the button uses a `/*block comment*/`.

Press `Tab` or `Shift+Tab` to indent or unindent all selected lines. It adds or removes one tab character before each line.

### Capture UI elements, insert regex etc, implement interface
The **Code** and **Edit** menus contain tools for creating code to find a window, UI element or image, for inserting parts of regular expression or keys string, generating interface/abstract implementation methods, and more.

### Find Windows API and insert declarations
The program comes with a database of Windows API declarations, and helps to find and insert them. More info: menu **Code > Windows API**, click **[?]**.

### Drag and drop files to insert path
You can drag and drop files from File Explorer etc to the code editor. It inserts code with file path. Links too.

### Focus
To focus the code editor control without changing selection: middle-click.

### WPF window preview
The program does not have a dialog window editor/designer, but it's easy to create windows in code, using class **wpfBuilder**. The program can automatically show/update the window while you edit its code, if it contains code like this:

```csharp
#if WPF_PREVIEW
b.Window.Preview();
#endif
```

This code must be after building the window but before showing it (**ShowDialog** etc).

To activate this feature for current document, check toolbar button **WPF preview** or menu **Edit > View > WPF preview**.

How it works: If **WPF preview** is checked and current script contains `#if WPF_PREVIEW`, the program launches/restarts the script whenever you make changes in its code, unless there are errors. The script runs like when clicked the **Run** button, with these changes:
- Defined **WPF_PREVIEW**. In script you use `#if WPF_PREVIEW` to include preview-specific code. Or use **script.isWpfPreview**.
- Function **Preview** shows the window without activating. Also changes some its properties. Ends the process when the window closed.
- Some `/\*/ properties /\*/` are ignored: `role`, `ifRunning`, `uac`, `bit32`, `console`, `optimize`, `outputPath`, `preBuild`, `postBuild`, `xmlDoc`.
- If current file (or its main project file) is a class file, runs it as a script; ignores the test script. Therefore need `#if WPF_PREVIEW` code that runs at startup and calls the function that contains the window code.
- Function **script.setup** does nothing.

In preview mode the script must go straight to the window code. If normally it doesn't, add code like this somewhere at the start:
```csharp
#if WPF_PREVIEW
FunctionContainingWindowCode();
#endif
```

If it's a dialog class, add code like this:
```csharp
#if WPF_PREVIEW
new DialogClass().Preview();
#endif
```

or this:
```csharp
#if WPF_PREVIEW
class Program { static void Main() { new DialogClass().Preview(); }}
#endif
```

In preview mode the script must not activate or move the window. Should skip slow/expensive operations that aren't necessary for preview. Should not show the tray icon.
