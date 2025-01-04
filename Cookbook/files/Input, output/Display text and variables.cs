/// Display text and variables in the <b>Output<> panel.

print.it("text");

int i = 3;
string s = "text";
print.it(i);
print.it(i, s);
print.it("text", i, s);

print.it($"text with variables: i={i}, s={s}");

/// To quickly insert <see cref="print.it"/> code, use snippet <b>piPrintItSnippet<> or <b>outPrintItSnippet<>: type <.c>pi<> or out and select from the list.
///
/// Can be used <help articles/Output tags>colors, bold etc, links, images, code<>.

print.it("<><lc DarkSeaGreen>Text, <google big bang>link<>.<>");

/// Display text in a message box. To insert <see cref="dialog.show"/> code can be used <b>dsDialogShowSnippet<>.

dialog.show("Big text", "Small text.");
dialog.showInfo(null, s);

/// Display text on screen.

osdText.showTransparentText("Transparent text");
osdText.showText("Tooltip " + s);

/// Auto-hide OSD when the script or function ends.

using var osd = osdText.showTransparentText("Default OSD time depends on text length.\nBut this OSD disappears when the function or script exits.", -1, new(y: .25f));
dialog.show("Close me");
