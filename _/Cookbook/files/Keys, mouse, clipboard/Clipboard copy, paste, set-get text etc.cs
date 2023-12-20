/// To quickly insert <see cref="clipboard.copy"/> code, use snippet copySnippet: type copy and select from the list.

string s = clipboard.copy(); //get the selected text
print.it(s);

/// Paste. To insert <see cref="clipboard.paste"/> code can be used pasteSnippet.

clipboard.paste("text");

/// Get and set clipboard text.

var s2 = clipboard.text;
if (!s2.NE()) clipboard.text = s2.Upper();

/// Get file paths from the clipboard.

var a = clipboardData.getFiles();
if (a != null) {
	foreach (var f in a) {
		print.it(f);
	}
}

/// Clear clipboard contents.

clipboard.clear();

/// Wait until the clipboard contains text, and get it.

clipboard.clear();
var text = wait.forCondition(0, () => clipboard.text);
print.it(text);
