# Display text and variables
Display text and variables in the Output panel.

```csharp
print.it("text");

int i = 3;
string s = "text";
print.it(i);
print.it(i, s);
print.it("text", i, s);

print.it($"text with variables: i={i}, s={s}");
```

To quickly insert <a href='/api/Au.print.it.html'>print.it</a> code, use snippet piPrintItSnippet or outPrintItSnippet: type pi or out and select from the list.

Can be used <a href='/articles/Output tags.html'>colors, bold etc, links, images, code</a>.

```csharp
print.it("<><lc DarkSeaGreen>Text, <google big bang>link<>.<>");
```

Display text in a message box. To insert <a href='/api/Au.dialog.show.html'>dialog.show</a> code can be used dsDialogShowSnippet.

```csharp
dialog.show("Big text", "Small text.");
dialog.showInfo(null, s);
```

Display text on screen.

```csharp
osdText.showTransparentText("Transparent text");
osdText.showText("Tooltip " + s);
```

Auto-hide OSD when the script or function ends.

```csharp
using var osd = osdText.showTransparentText("Default OSD time depends on text length.\nBut this OSD disappears when the function or script exits.", -1, new(y: .25f));
dialog.show("Close me");
```

