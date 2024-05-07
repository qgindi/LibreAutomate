using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using Au.Controls;
using Au.Tools;

partial class PanelFiles {
	FilesModel.FilesView _tv;
	TextBox _tFind;
	timer _timerFind;
	FileNode _firstFoundFile;
	KPopup _ttError;
	
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
		_tFind.KeyDown += (_, e) => { if (e.Key is Key.Enter && _firstFoundFile != null) App.Model.OpenAndGoTo(_firstFoundFile); };
		
		_tv.EditLabelStarted += e => {
			var f = e.item as FileNode;
			var s = e.Text;
			if (f.IsFolder) {
				if (s[0] == '@') e.SelectText(1, s.Length);
			} else if (f.IsOtherFileType) {
				int i = pathname.findExtension(s);
				if (i > 0) e.SelectText(0, i);
			}
		};
		
		EditGoBack.DisableUI();
	}
	
	public UserControl P { get; } = new();
	
	public FilesModel.FilesView TreeControl => _tv;
	
	private void _Find() {
		_firstFoundFile = null;
		var s = _tFind.Text;
		
		wildex wild = null; string err = null;
		if (wildex.hasWildcardChars(s)) {
			try { wild = new wildex(s); }
			catch (Exception ex1) {
				err = ex1.Message;
				if (err.Starts("Invalid \"**options") && !s.Contains(' ')) err = null;
				s = null;
			}
		}
		if (err != null) TUtil.InfoTooltip(ref _ttError, _tFind, err); else _ttError?.Close();
		
		if (s.NE()) {
			Panels.Found.ClearResults(PanelFound.Found.Files);
			return;
		}
		
		var workingState = Panels.Found.Prepare(PanelFound.Found.Files, s, out var b);
		
		foreach (var f in App.Model.Root.Descendants()) {
			var name = f.Name;
			int i = -1;
			if (wild != null) {
				if (!wild.Match(name)) continue;
			} else {
				i = name.Find(s, true);
				if (i < 0) continue;
			}
			
			var path = f.ItemPath;
			int i1 = path.Length - name.Length;
			b.Link2(f).Gray(path.AsSpan(0, i1)).Text(name);
			if (i >= 0) {
				i += b.Length - name.Length;
				b.Indic(PanelFound.Indicators.HiliteY, i, i + s.Length);
			}
			b.Link_();
			if (f.IsFolder) b.Green("    //folder");
			b.NL();
			_firstFoundFile ??= f;
		}
		
		if (b.Length == 0) return;
		
		Panels.Found.SetResults(workingState, b);
	}
}
