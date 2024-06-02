/// Most mouse functions are in class <see cref="mouse"/>. To create mouse-click code can be used:
/// - Menu <b>Code > Input recorder<>. It also can record other actions: wheel, drag, move.
/// - Menu <b>Code > Find UI element<>. Also <b>Code > Find image<> and <b>Code > Find OCR text<>.
/// - Hotkey <mono>Ctrl+Shift+Q<>.

/// Click at x=10, y=20 in the client area of <_>Notepad</_> window.

var w = wnd.find(1, "*- Notepad", "Notepad");
mouse.click(w, 10, 20);

/// Move cursor to: x=10 from the right edge, y=center.

mouse.move(w, ^10, .5f);

/// Relative move: x+=50, y+=0.

mouse.moveBy(50, 0);

/// Change speed and other options. To quickly insert code can be used <b>speedSnippet<>.

opt.mouse.ClickSpeed = 100;
opt.mouse.ClickSleepFinally = 100;
opt.mouse.MoveSpeed = 20;
//opt.mouse.Relaxed = true;
mouse.rightClick(w, 10, 20);

/// Restore initial cursor position (or position saved with <b>mouse.save<>).

mouse.restore();

/// Relative drag from x=34, y=8 in <_>Notepad</_> window by x+=54, y+=0. It can be recorded.

var wNotepad = wnd.find(0, @"*- Notepad", "Notepad").Activate();
mouse.drag(wNotepad, 34, 8, 54, 0);

/// <mono>Ctrl<>+drag file <.c>abc<> to folder <.c>Backup<> in File Explorer (folder window name <b>Test<>).

var wExplorer = wnd.find(1, "Test", "CabinetWClass").Activate();
var e1 = wExplorer.Elm["LISTITEM", "abc", "class=DirectUIHWND"].Find(1);
var e2 = wExplorer.Elm["LISTITEM", "Backup", "class=DirectUIHWND"].Find(1);
mouse.drag(e1, e2, mod: KMod.Ctrl);
