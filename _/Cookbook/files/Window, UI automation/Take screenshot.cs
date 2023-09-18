/// Take screenshot of the primary screen and save in a file.

var file = folders.Temp + "screenshot.png";
using (var b = CaptureScreen.Image(screen.primary.Rect)) {
	b.Save(file);
}
run.it(file); //see what we have

/// Take screenshot of the active window and put in the clipboard.

wnd.active.GetRect(out var r, true);
using (var b = CaptureScreen.Image(r)) {
	new clipboardData().AddImage(b).SetClipboard();
}

/// Take screenshot of a user-selected area and save in a file.

var file2 = folders.Temp + "screenshot2.png";
if (!CaptureScreen.ImageColorRectUI(out var captured)) return;
captured.image.Save(file2);
run.it(file2); //see what we have
