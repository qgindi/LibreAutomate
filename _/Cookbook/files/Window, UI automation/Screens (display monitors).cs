/// Use <see cref="screen"/> functions to get screens and screen properties.

var r = screen.primary.Rect;
print.it(r);

var s1 = screen.index(1); //the first non-primary screen
var k = s1.Info;
print.it(k.rect, k.workArea);

foreach (var v in screen.all) {
	print.it(v.Rect);
}

/// Also can be used with some functions of <b>wnd</b> and other classes.

var w = wnd.find(1, "*- Notepad", "Notepad");
w.Move(100, 100, workArea: true, screen: s1);
1.s();
w.MoveToScreenCenter(s1);

1.s();
var d2 = new dialog("Example") { Screen = screen.ofMouse };
d2.ShowDialog();
