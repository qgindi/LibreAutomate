/// To show a context menu or drop-down list can be used class <see cref="popupMenu"/>. Or WPF class <b>ContextMenu<>, but it can't have many items (slow, no scrollbar). To create <b>popupMenu<> code can be used snippet <b>menuSnippet<>.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

var b = new wpfBuilder("Window").WinSize(250);

b.R.Add(out Label label1, "Right-click here").Border(Brushes.Gray, 1);
label1.ContextMenuOpening += (_, e) => {
	e.Handled = true;
	var m = new popupMenu();
	m["A"] = o => { print.it(o); };
	m["B"] = o => {  };
	m.Submenu("Submenu", m => {
		m["C"] = o => {  };
		m["D"] = o => {  };
	});
	m.Separator();
	m["E"] = o => {  };
	m.Show(owner: e.Source as UIElement);
};

b.R.AddButton("Windows", o => {
	var m = new popupMenu();
	var a = wnd.getwnd.allWindows();
	foreach (var w in a) m[w.ClassName] = o => { print.it(w); };
	
	var r = o.Button.RectInScreen();
	m.Show(excludeRect: r, owner: o.Window);
});

b.R.AddButton("Files", o => {
	var m = new popupMenu();
	_Dir(m, new DirectoryInfo(@"C:\"));
	m.Show(owner: o.Window);

	static void _Dir(popupMenu m, DirectoryInfo dir) {
		foreach (var v in dir.EnumerateFileSystemInfos()) {
			if(v.Attributes.Has(FileAttributes.System|FileAttributes.Hidden)) continue;
			if(v.Attributes.Has(FileAttributes.Directory)) {
				m.Submenu(v.Name, m=> _Dir(m, v as DirectoryInfo));
			} else {
				m[v.Name]=o=>print.it(v.FullName);
			}
			m.Last.File = v.FullName;
		}
	}
});

b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;
