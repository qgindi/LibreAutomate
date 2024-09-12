using System.Windows;
using System.Windows.Controls;
using Au.Controls;

namespace Au.Tools;

class DWinapi : KDialogWindow {
	sqlite _db;
	TextBox tName;
	KSciCodeBox code;

	public DWinapi(string name = null) {
		var doc = Panels.Editor.ActiveDoc;
		InitWinProp("Find Windows API", App.Wmain);

		if (name == null) {
			name = doc.aaaSelectedText();
			if (!name.NE()) {
				name = name.RxReplace(@"\W+", " ");
			}
		}

		var b = new wpfBuilder(this).WinSize(800, 500);
		b.R.Add("Name", out tName, name);
		b.Row(-1).Add(out code); code.AaInitBorder = true;
		b.R.AddButton("?", _ => _Help());
		b.AddOkCancel("OK, copy to clipboard");
		b.End();

		b.OkApply += o => {
			string s = code.aaaText;
			if (s.NE()) return;
			Clipboard.SetText(s);
		};

		tName.TextChanged += (_, _) => _TextChanged();

		void _Help() {
			var s = @"/**
Here you can find Windows API declarations: function, struct, enum, interface, delegate, constant, GUID.
Enter API name in the above field. Case-sensitive. Wildcard examples: Start*, *End, *Part*.
Or multiple full names, like Name1 Name2 Name3.
You can in editor select text with one or more names and open this window. Or use error tooltips.
If script is without a class, create new class (or convert script to class) and paste declarations there.

Also try snippet nativeApiSnippet (select it from the completion list when text cursor is where classes can be,
for example at the end of script). It adds a special class. Then anywhere in script just type class name, dot,
and select from the list. It adds the declaration to the class, and more declarations if need.

The database contains ~200_000 declarations. They are not perfect. You can edit.
Data source: https://github.com/microsoft/win32metadata
*/";
			code.AaSetText(s);
		}
	}

	protected override void OnSourceInitialized(EventArgs e) {
		_db = EdDatabases.OpenWinapi();
		if (!tName.Text.NE()) _TextChanged();

		base.OnSourceInitialized(e);
	}

	protected override void OnClosed(EventArgs e) {
		_db?.Dispose();
		_db = null;
		base.OnClosed(e);
	}

	void _TextChanged() {
		string name = tName.Text;
		var a = new List<(string name, string code)>();

		int nWild = 0; for (int i = 0; i < name.Length; i++) { switch (name[i]) { case '*': case '?': nWild++; break; } }
		if (name.Length > 0 && (nWild == 0 || name.Length - nWild >= 2)) {
			string sql;
			if (name.Contains(' ')) sql = $"in ('{string.Join("', '", name.RxFindAll(@"\b[A-Za-z_]\w\w+", (RXFlags)0))}')";
			else if (name.FindAny("*?") >= 0) sql = $"GLOB '{name}'";
			else sql = $"= '{name}'";
			try {
				using var stat = _db.Statement("SELECT name, code FROM api WHERE name " + sql);
				//perf.first();
				while (stat.Step()) a.Add((stat.GetText(0), stat.GetText(1)));
				//perf.nw(); //30 ms cold, 10 ms warm. Without index.
			}
			catch (SLException ex) { Debug_.Print(ex); }
		}

		string s = "";
		if (a.Count != 0) {
			s = a[0].code;
			if (a.Count > 1) s = string.Join(s.Starts("internal const") ? "\r\n" : "\r\n\r\n", a.Select(o => o.code));
			s += "\r\n";
		}
		code.AaSetText(s);
	}
}
