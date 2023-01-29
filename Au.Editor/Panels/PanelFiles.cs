using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;

partial class PanelFiles {
	FilesModel.FilesView _tv;
	TextBox _tFind;
	timer _timerFind;
	List<FileNode> _aClose;

	public PanelFiles() {
		P.UiaSetName("Files panel");
		P.Background = SystemColors.ControlBrush;

		var b = new wpfBuilder(P).Columns(-1).Options(margin: new());
		b.Row(-1).Add(out _tv).Name("Files_list", true);

		b.Row(4).Add<Border>().Border(thickness2: new(0, 1, 0, 1));

		_tFind = new() { BorderThickness = default };
		b.R.Add<AdornerDecorator>().Add(_tFind, flags: WBAdd.ChildOfLast).Name("Find_file", true)
			.Watermark("Find file").Tooltip(@"Part of file name, or wildcard expression.
Examples: part, start*, *end.cs, **r regex, **m green.cs||blue.cs.");

		//CONSIDER: File bookmarks. And/or tags. Probably not very useful. Unless many users will want it.

		b.End();

		_tFind.TextChanged += (_, _) => { (_timerFind ??= new(_ => _Find())).After(200); };
		_tFind.GotKeyboardFocus += (_, _) => P.Dispatcher.InvokeAsync(() => _tFind.SelectAll());
		_tFind.PreviewMouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) _tFind.Clear(); };

		EditGoBack.DisableUI();
	}

	public UserControl P { get; } = new();

	public FilesModel.FilesView TreeControl => _tv;

	private void _Find() {
		var cFound = Panels.Find.PrepareFindResultsPanel();
		_aClose = new();

		var s = _tFind.Text; if (s.NE()) return;
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

			_aClose.Add(f);
		}

		if (b.Length == 0) return;

		if (_aClose.Count > 0) b.AppendLine("<bc #FFC000><+caff><c #80ff>Close all<><><>");

		cFound.aaaSetText(b.ToString());
	}

	public void CloseAll() {
		App.Model.CloseFiles(_aClose);
		App.Model.CollapseAll(exceptWithOpenFiles: true);
	}
}
