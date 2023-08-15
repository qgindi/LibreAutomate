/// At first need to <help wnd.find>find<> the window and create a <see cref="wnd"/> variable.

var w = wnd.find(1, "*- Notepad", "Notepad");

/// Then type the variable name and dot (.), and select a function from the list. If need help, click the function name in the code editor and press F1.

/// Close the found window.

w.Close();

/// Close all similar windows.

var a1 = wnd.findAll(cn: "Notepad", of: "notepad.exe");
foreach (var v in a1) {
	//print.it(v);
	v.Close();
}

/// Move, resize.

w.Move(100, 100, workArea: true);
w.Resize(500, 300);
w.Move(100, 100, 500, 300, workArea: true); //move and resize

/// Get rectangle.

var r = w.Rect;
print.it(r);

/// Is active?

if (w.IsActive) print.it("active");

/// Is closed?

if (!w.IsAlive) print.it("closed");

/// Maximize, minimize, restore, hide, show.

w.ShowMaximized();
1.s();
w.ShowMinimized();
1.s();
w.ShowNotMinimized();
1.s();
w.ShowNotMinMax();
1.s();
w.Show(false);
1.s();
w.Show(true);

/// Also <b>wnd</b> variables can be used with functions of various classes as arguments.

mouse.move(w, 10, 10);

wnd w2 = wnd.getwnd.nextMain(w);
