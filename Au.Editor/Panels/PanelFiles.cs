using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;

partial class PanelFiles {
	FilesModel.FilesView _tv;
	TextBox _tFind;
	timer _timerFind;

	public PanelFiles() {
		P.UiaSetName("Files panel");
		P.Background = SystemColors.ControlBrush;

		var b = new wpfBuilder(P).Columns(-1).Options(margin: new());
		b.Row(-1).Add(out _tv).Name("Files_list", true);

		b.Row(2) //maybe 4 would look better, but then can be confused with a splitter
			.Add<Border>().Border(thickness2: new(0, 1, 0, 1));

		_tFind = new() { BorderThickness = default };
		b.R.Add<AdornerDecorator>().Add(_tFind, flags: WBAdd.ChildOfLast).Name("Find_file", true)
			.Watermark("Find file").Tooltip(@"Part of file name, or wildcard expression.
Examples: part, start*, *end.cs, **r regex, **m green.cs||blue.cs.");

		//CONSIDER: File bookmarks. And/or tags. Probably not very useful. Unless many users will want it.

		b.End();

		_tFind.TextChanged += (_, _) => { (_timerFind ??= new(_ => _Find())).After(_tFind.Text.Length switch { 1 => 1200, 2 => 600, _ => 300 }); };
		_tFind.GotKeyboardFocus += (_, _) => P.Dispatcher.InvokeAsync(() => _tFind.SelectAll());
		_tFind.PreviewMouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) _tFind.Clear(); };

		EditGoBack.DisableUI();
	}

	public UserControl P { get; } = new();

	public FilesModel.FilesView TreeControl => _tv;

	private void _Find() {
		var s = _tFind.Text;
		if (s.NE()) {
			Panels.Found.ClearResults(FoundKind.Files);
			return;
		}
		Panels.Found.Prepare(FoundKind.Files, s);

		var wild = wildex.hasWildcardChars(s) ? new wildex(s, noException: true) : null;
		var b = new StringBuilder();

		foreach (var f in App.Model.Root.Descendants()) {
			var text = f.Name;
			int i = -1;
			if (wild != null) {
				if (!wild.Match(text)) continue;
			} else {
				i = text.Find(s, true);
				if (i < 0) continue;
			}

			var path = f.ItemPath;
			string link = f.IdStringWithWorkspace;
			int i1 = path.Length - text.Length;
			string s1 = path[..i1], s2 = path[i1..];
			b.AppendFormat("<+open \"{0}\"><c #808080>{1}<>", link, s1);
			if (i < 0) {
				b.Append(s2);
			} else { //hilite
				int to = i + s.Length;
				b.Append(s2, 0, i).Append("<bc #ffff5f>").Append(s2, i, s.Length).Append("<>").Append(s2, to, s2.Length - to);
			}
			if (f.IsFolder) b.Append("    <c #008000>//folder<>");
			b.AppendLine("<>");
		}

		if (b.Length == 0) return;
		b.AppendLine("<bc #FFC000><+caf><c #80ff>Close opened files<><><>");
		Panels.Found.SetFilesFindResults(b.ToString());
	}
}
