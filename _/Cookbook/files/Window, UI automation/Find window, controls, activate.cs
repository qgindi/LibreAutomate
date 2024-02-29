/// To create <see cref="wnd.find"/> code can be used:
/// - Tools in the <b>Code<> menu: <b>Find window<> (<mono>Ctrl+Shift+W<>), <b>Input recorder<>.
/// - Hotkey <mono>Ctrl+Shift+Q<>.
/// - Snippets <b>winFindSnippet<>, <b>actWinSnippet<>. You can see window properties in panel <b>Mouse<>.

/// Find window and create variable <i>w1<> that contains window handle. Window name must end with <.c>"- Notepad"<>; class name must be <.c>"Notepad"<>. Can wait/retry max 1 s; exception if not found. Activate when found.

var w1 = wnd.find(1, "*- Notepad", "Notepad");
w1.Activate();

/// Find window with no name, class name <.c>"Shell_TrayWnd"<>, program <.c>"explorer.exe"<>. Exit if not found.

var w2 = wnd.find("", "Shell_TrayWnd", "explorer.exe");
if (w2.Is0) return;

/// Function <see cref="wnd.findOrRun"/> opens window if does not exist. Also activates.

var w3 = wnd.findOrRun("* Notepad", run: () => run.it("notepad.exe")); //if not found, run "notepad.exe"
print.it(w3);

/// Get the active window.

var w4 = wnd.active;

/// Get the mouse window.

var w5 = wnd.fromMouse(WXYFlags.NeedWindow);
//w5 = wnd.fromXY(mouse.xy, WXYFlags.NeedWindow); //the same

/// Find all windows classnamed <.c>"Notepad"<> of program <.c>"notepad.exe"<>.

var a1 = wnd.findAll(cn: "Notepad", of: "notepad.exe");
foreach (var v in a1) {
	print.it(v);
}

/// Get all visible windows.

var a2 = wnd.getwnd.allWindows(onlyVisible: true);
foreach (var v in a2) {
	print.it(v);
}

/// A window can contain child windows, also known as <i>controls<>. This code finds a child window. Exception if not found. When found, moves the mouse cursor to its center.

var w6 = wnd.find(0, "*- WordPad", "WordPadClass");
var c1 = w6.Child(0, cn: "msctls_trackbar32", id: 53254); // "Zoom Slider"
mouse.move(c1);

/// To create <see cref="wnd.Child"/> code can be used the same tools as for <b>wnd.find<>.
