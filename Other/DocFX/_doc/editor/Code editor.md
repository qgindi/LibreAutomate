---
uid: code_editor
---

# Code editor

In the code editor you edit automation scripts and other C# code. It is a text editor with various features for easier C# code editing: lists of symbols, autocompletion, bracket completion, statement completion, auto indentation, parameter info, quick info, XML documentation comments, go to documentation, go to definition/source, error info, code coloring, text folding, separators between functions, images in code, snippets, comment/uncomment/indent/unindent lines, drag/drop files, find/replace text, find namespace, find Windows API, insert keys/regex/etc, capture UI elements.

C# code may look like this:
```csharp
mouse.click(10, 20);
if (keys.isCtrl) {
	print.it("text");
}
```

You can type this text instead:
```csharp
mo.c 10, 20
if ke.isct

pi "text
```

While typing, editor completes words, inserts code snippets, adds `()`, `;`, `{}` and indentation. The result is the first code.

#### Lists of symbols and autocompletion
When you start typing a word, editor shows a list of symbols (classes, functions, variables, C# keywords, etc) available there. Or you can press Ctrl+Space to show the list anywhere, including regular expression strings.

The list shows only items that contain the partially typed text. The text in list items is highlighted. Auto-selects an item that is considered the first best match. On Ctrl+Space shows all. You can use the vertical toolbar to filter and group list items, for example to show only methods.

While the list is visible, when you enter a non-word character (space, comma, etc) or press Enter or Tab or double-click an item, the partially or fully typed word is replaced with the text of the selected list item. Also may add `()` or `{}`, for example if it is a method or a keyword like `if` or `finally`. The added text may be different when you press Enter; for example may add `{}` instead of `()`. By default adds `()` only if completed with space; see Options -> Code.

To select list items you also can click or press arrow or page keys. It does not hide the list. The tooltip-like window next to the list shows more info about the selected item, including links to online documentation and source code if available. Shows all overloads (functions with same name but different parameters), and you can click them to view their info.

To hide the list without inserting item text you can press Esc or click somewhere in code.

#### Automatic bracket completion
When you type `(`, `[`, `{`, `<`, `"` or `'`, editor adds the closing `)`, `]`, `}`, `>`, `"` or `'`. Then, while the text cursor is before the added `)` etc, typing another `)` or tab just leaves the enclosed area. Also then Backspace erases both characters.

#### Statement completion
When you press Enter inside a function argument list before the last `)`, editor adds missing `;` or `{  }`, adds new line and moves the text cursor there. To avoid it, press Esc+Enter. To complete statement without new line, use `;` instead of Enter.

Ctrl+Enter, Shift+Enter and Ctrl+; will complete statement when the text cursor is anywhere in it.

#### Auto indentation
When you press Enter, editor adds new line with correct number of tabs (indentation). The same with Ctrl+Enter. To avoid it, press Esc+Enter.

#### Parameter info
When you type a function name and `(`, editor shows a tooltip-like window with info about the function and current parameter. To show the window from anywhere in an argument list, press Ctrl+Shift+Space. You can select oveloads with arrow keys or the mouse.

#### Quick info
Whenever the mouse dwells on a symbol etc in the editor, a tooltip displays some info about the symbol, including documented exceptions the function may throw.

#### XML documentation comments
Editor gets data for quick info, parameter info and other info about symbols from their assemblies and XML documentation comments. You can write XML documentation comments for your functions and types; look for how to on the internet. Documentation of the automation library and .NET is installed with the program. Documentation of other assemblies comes from their assembly.dll + assembly.xml files.

Editor also helps to write XML documentation comments. Adds empty summary and parameters when you type `///` above a class, method etc. Adds `///` on Enter, shows list of tags, autocompletes tags, color-highlights tags, text and see references.

#### Go to symbol documentation
To show symbol documentation if available, press F1 when the text cursor is in it. Or click the "more info" link in the autocompletion item info or parameter info window.

If the symbol is from the automation library, it opens the online documentation page in your web browser. If the symbol is from .NET runtime or other assembly or unmanaged code (DllImport or ComImport), it opens the Google search page.

#### Go to symbol definition (source code)
Click a symbol and press F12. Or click the "source code" link in the autocompletion item info or parameter info window.

If the symbol is defined in your code, it opens that file and moves the text cursor. If the symbol is from .NET runtime, it can help to find the source code online.

#### Error info
Errors are detected in editor, as well as when compiling the code. Code parts with errors have red squiggly underlines, warnings green. A tooltip shows error/warning description. Also can contain links to fix the error: add missing `using namespace` or Windows API declaration.

#### Code coloring
Different kinds of code parts have different colors. Comments, strings, keywords, types, functions, etc.

#### Text folding
You can hide and show code regions like in a tree view control: click the minus (-) or plus (+) in the left margin. Folding is available for functions, types, multiline comments, disabled code (`#if`), `#region` ... `#endregion` and `//.` ... `//..`.

#### Separators between functions/types
Editor draws horizontal lines at the end of each function and type definition.

#### Images in code
Whenever code contains a string or comment that looks like an image file path or image embedded in code (`image:...`, usually hidden text), editor draws the image at the left. This feature can be enabled/disabled with the toolbar button.

#### Snippets
Autocompletion lists also contain snippets. For example the outSnippet inserts code `print.it();` when you type `out` and space or Tab or Enter or click it.

#### Comment/uncomment/indent/unindent lines
Often you'll want to disable or enable one or more lines of code by converting them to/from comments. The easiest way - right click the selection margin. If multiple lines are selected, it converst all.

Press Tab or Shift+Tab to indent or unindent all selected lines. It adds or removes one tab character before each line.

#### Drag and drop files to insert path
You can drag and drop files from File Explorer etc to the code editor. It inserts code with file path.

#### Find and replace
Use the Find panel to find and replace text in editor. It marks all matches in editor with yellow. Also can find files by name and files containing text. Can replace text in multiple files.

#### Code tools
The Code menu contains dialogs and simple commands for creating code to find a window, UI element or image, for inserting parts of regular expression or keys string, and more.

#### Focus
To focus the code editor control without changing selection: middle-click.
