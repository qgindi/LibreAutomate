# Clipboard copy, paste, set-get text etc
To quickly insert <a href='/api/Au.clipboard.copy.html'>clipboard.copy</a> code, use snippet copySnippet: type copy and select from the list.

```csharp
string s = clipboard.copy(); //get the selected text
print.it(s);
```

Paste. To insert <a href='/api/Au.clipboard.paste.html'>clipboard.paste</a> code can be used pasteSnippet.

```csharp
clipboard.paste("text");
```

Get and set clipboard text.

```csharp
var s2 = clipboard.text;
if (!s2.NE()) clipboard.text = s2.Upper();
```

Get file paths from the clipboard.

```csharp
var a = clipboardData.getFiles();
if (a != null) {
	foreach (var f in a) {
		print.it(f);
	}
}
```

Clear clipboard contents.

```csharp
clipboard.clear();
```

Wait until the clipboard contains text, and get it.

```csharp
clipboard.clear();
var text = wait.forCondition(0, () => clipboard.text);
print.it(text);
```

