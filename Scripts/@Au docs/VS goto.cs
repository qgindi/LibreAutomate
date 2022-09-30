var s = args.Length == 0 ? "Au.clipboard.copy" : args[0];

if (s.Ends("..ctor")) s=s[..^6];
else {
	int i=s.Find(".op_");
	if(i>0) s=s[..i];
}

var w = wnd.find(0, "Au - Microsoft Visual Studio (Administrator)", "HwndWrapper[DefaultDomain;*");
w.Activate();
//keys.send("Ctrl+T");
keys.send("Ctrl+(1 S)");
clipboard.paste(s);
mouse.move(screen.primary);
//if (miscInfo.getTextCursorRect(out var r, out _)) mouse.move(r); else mouse.move(screen.primary);
