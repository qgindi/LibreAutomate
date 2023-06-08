using Au.Controls;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

static class DCustomizeContextMenu {
	
	public static void Dialog(string menuName, string ownerName) {
		string file = AppSettings.DirBS + menuName + " context menu.txt";
		
		var b = new wpfBuilder("Customize context menu").WinSize(550, 550).Columns(-1, -1);
		b.WinProperties(showInTaskbar: false);
		
		b.Row(40).Add(out KSciInfoBox info);
		info.aaaText = $"""
Left - all menu commands. Select a line and drag or copy to the right.
Right - commands to add to the context menu of the {ownerName}. Separator -.
""";
		
		b.Row(-1).xAddInBorder(out KScintilla t1);
		t1.AaInitReadOnlyAlways = true;
		b.Loaded += () => {
			t1.aaaText = _GetAllCommands(menuName, out int goTo);
			if (goTo > 0) t1.Call(Sci.SCI_LINESCROLL, 0, t1.aaaLineFromPos(false, goTo));
		};
		
		b.xAddInBorder(out KScintilla t2);
		t2.AaInitUseDefaultContextMenu = true;
		if (filesystem.exists(file)) t2.aaaText = filesystem.loadText(file);
		//b.Validation(o => never mind);
		t2.AaNotify += (KScintilla c, ref Sci.SCNotification n) => {
			unsafe {
				if (n.code == Sci.NOTIF.SCN_MODIFIED) {
					//trim tabs
					if (n.modificationType.Has(Sci.MOD.SC_MOD_INSERTCHECK) && n.length > 1 && n.textUTF8[0] == 9) {
						var s = n.Text.RxReplace(@"(?m)^\t+", "");
						c.aaaSetString(Sci.SCI_CHANGEINSERTION, 0, s, true);
					}
				}
			}
		};
		
		b.R.AddOkCancel();
		b.End();
		if (!b.ShowDialog(App.Wmain)) return;
		
		filesystem.saveText(file, t2.aaaText);
	}
	
	static string _GetAllCommands(string menuName, out int goTo) {
		int go = -1;
		var b = new StringBuilder();
		_Menu(Panels.Menu, 0);
		goTo = go;
		return b.ToString();
		
		void _Menu(ItemsControl sourceMenu, int level) {
			foreach (var o in sourceMenu.Items) {
				if (o is not MenuItem ms || ms.Command is not KMenuCommands.Command c) continue;
				bool isMenu = ms.Role is MenuItemRole.SubmenuHeader or MenuItemRole.TopLevelHeader;
				if (isMenu && go < 0 && c.Name == menuName) go = b.Length;
				b.Append('\t', level).AppendLine(c.Name);
				if (isMenu) _Menu(ms, level + 1);
			}
		}
	}
	
	public static void AddToMenu(KWpfMenu m, string menuName) {
		try {
			string file = AppSettings.DirBS + menuName + " context menu.txt";
			if (!filesystem.exists(file)) return;
			var a = File.ReadAllLines(file);
			bool needSeparator = false;
			foreach (var line in a) {
				var s = line.Trim();
				if (s.Length == 0 || s.Starts('/')) continue;
				if (s == "-") {
					m.Separator();
					needSeparator = false;
				} else if (App.Commands.TryFind(s, out var c)) {
					var k = new MenuItem();
					c.CopyToMenu(k);
					m.Items.Add(k);
					needSeparator = true;
				} else {
					//print.it($"<>Unknown menu command: {s}.");
				}
			}
			if (needSeparator) m.Items.Add(new Separator());
		}
		catch (Exception e1) { Debug_.Print(e1); }
	}
}
