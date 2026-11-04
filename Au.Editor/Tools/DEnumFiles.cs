using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using Au.Controls;
using ToolLand;

namespace LA;

class DEnumFiles : KDialogWindow {
	public static void Dialog(string initFolder = null) {
		new DEnumFiles(initFolder).Show();
	}
	
	KTextBoxFile _tFolder;
	ComboBox _cbGet, _cbResult;
	DPPathFilter _filter;
	KCheckBox _cForeach, _cRecurse, _cIgnore, _cSymlink, _cSkipHidden, _cSkipHiddenSystem, _cWhere, _cOrderBy, _cThenBy, _cSelect;
	KSciCodeBox _code;
	
	const string c_Where = ".Where(o => EditMe)",
		c_OrderBy = ".OrderBy(o => EditMe)",
		c_ThenBy = ".ThenBy(o => EditMe)",
		c_Select = ".Select(o => EditMe)";
	
	internal DEnumFiles(string initFolder = null) {
		InitWinProp("Get files in folder", App.Wmain);
		
		_noeventValueChanged = true;
		var b = new wpfBuilder(this).WinSize(600).Columns(-1);
		
		//top
		using (b.R.StartGrid().Columns(76, 76, -1)) {
			CancellationTokenSource cts = null;
			b.AddButton("Test", async e => {
				if (cts == null) {
					var s = _FormatCode(true);
					print.clear();
					e.Button.Content = "Stop";
					cts = new();
					var r = await TUtil.RunTestCodeAsync(s, cts.Token);
					cts = null;
					if (!IsLoaded) return;
					e.Button.Content = "Test";
					if (r?.isError == true) print.it($"<><b>{r.header}<>\r\n{r.text}");
				} else {
					cts.Cancel();
				}
			});
			Closing += (_, _) => { cts?.Cancel(); };
			b.AddButton("OK", _ => {
				Close();
				var s = _FormatCode();
				InsertCode.Statements(new(s, makeVarName1: true));
			}).Skip();
		}
		b.R.AddSeparator();
		
		//folder, kind/type, filter
		using (b.R.StartGrid().Columns(48, 100, 0, 250, -1)) {
			b.R.Add("Folder", out _tFolder, initFolder).Tooltip("Folder path.\nPaste, drop or right-click.").Focus();
			_tFolder.IsFolder = true;
			_tFolder.Unexpand = App.Settings.tools_pathUnexpand;
			
			b.R.Add("Get", out _cbGet).Items("Files|Folders|All");
			b.Add("As", out _cbResult).Span(1)
				.Items("FLPath[]  (path, name, attributes)|IEnumerable<FLPath>|string[]  (names)|string[]  (paths)|FileSystemInfo[]  (all properties)|IEnumerable<FileSystemInfo>|(tuple)[]  (selected properties)|IEnumerable<(tuple)>")
				.Tooltip("If array, stores results in an array variable before starting to use them.\nThen it is safe to delete/rename/move/copy the files.\nBut then cannot start using results until all retrieved from the file system.\nUse IEnumerable if going to search in a potentially large directory.");
			
			b.R.Add(out _filter).Margin(0);
		}
		
		//flags and results
		using (b.R.StartGrid().Columns(-1, -1)) {
			b.R.StartGrid<KGroupBox>("Flags");
			b.R.Add(out _cRecurse, nameof(FLFlags.Recurse))
				.Tooltip("Get all descendants, not only direct children");
			b.Add(out _cSymlink, "Follow links").Margin("L22").xBindCheckedVisible(_cRecurse)
				.Tooltip("Follow NTFS directory links");
			b.R.Add(out _cIgnore, "Ignore inaccessible")
				.Tooltip("Don't throw exception if a directory is inaccessible because of its security settings");
			b.R.Add(out _cSkipHidden, "Skip hidden");
			b.R.Add(out _cSkipHiddenSystem, "Skip hidden system");
			b.End();
			
			using (b.StartGrid<KGroupBox>("Results")) {
				b.R.Add(out _cWhere, "Where")
					.Tooltip($"Add code '{c_Where}' to filter results by some properties.\nThen in the code editor you'll edit it.");
				b.R.StartStack();
				b.Add(out _cOrderBy, "OrderBy")
					.Tooltip($"Add code '{c_OrderBy}' to sort results by some property.\nThen in the code editor you'll edit it. You can replace OrderBy with OrderByDescending.");
				b.Add(out _cThenBy, "ThenBy").xBindCheckedVisible(_cOrderBy)
					.Tooltip($"Adds code '{c_ThenBy}' to sort results by another property.\nThen in the code editor you'll edit it. You can replace ThenBy with ThenByDescending, and append more ThenBy/ThenByDescending if need.");
				b.End();
				b.R.StartStack();
				b.Add(out _cSelect, "Select")
					.Tooltip($"Add code '{c_Select}' to change the type of results.\nFor example select a property or several properties (as tuple or named type).\nThen in the code editor you'll edit it.");
				b.End();
				
				b.R.Add(out _cForeach, "Use foreach").Checked();
			}
		}
		
		b.R.AddSeparator();
		b.Row(150..).xAddInBorder(out _code);
		
		b.End();
		_noeventValueChanged = false;
		
		b.Loaded += () => {
			_FormatCode();
		};
		_tFolder.UnexpandChanged += () => {
			App.Settings.tools_pathUnexpand = _tFolder.Unexpand;
			_FormatCode();
		};
	}
	
	static DEnumFiles() {
		TUtil.OnAnyCheckTextBoxValueChanged<DEnumFiles>((d, o) => d._AnyCheckTextBoxComboValueChanged(o), comboToo: true);
	}
	
	//when checked/unchecked any checkbox, and when text changed of any textbox or combobox
	void _AnyCheckTextBoxComboValueChanged(object source) {
		if (!_noeventValueChanged) {
			_noeventValueChanged = true;
			if (source == _cSkipHidden && _cSkipHidden.IsChecked) _cSkipHiddenSystem.IsChecked = false;
			if (source == _cSkipHiddenSystem && _cSkipHiddenSystem.IsChecked) _cSkipHidden.IsChecked = false;
			_noeventValueChanged = false;
			
			_FormatCode();
		}
	}
	bool _noeventValueChanged;
	
	string _FormatCode(bool forTest = false, bool onOK = false) {
		int getWhat = _cbGet.SelectedIndex;
		string filter = _filter.AaResultCode;
		bool hasFilter = filter != null;
		
		var b = new StringBuilder();
		b.AppendLine($"var folder = {_tFolder.GetCode()};");
		b.Append("var a = ");
		b.Append("filesystem.list(folder");
		if (hasFilter) b.AppendOtherArg(filter);
		bool rec = _cRecurse.IsChecked;
		if (rec || _cIgnore.IsChecked || _cSkipHidden.IsChecked || _cSkipHiddenSystem.IsChecked) {
			if (TUtil.FormatFlags(out var s1,
				(rec && !_cSymlink.IsChecked, FLFlags.Recurse),
				(rec && _cSymlink.IsChecked, FLFlags.RecurseFollowLinks),
				(_cIgnore.IsChecked, FLFlags.IgnoreInaccessible),
				(_cSkipHidden.IsChecked, FLFlags.SkipHidden),
				(_cSkipHiddenSystem.IsChecked, FLFlags.SkipHiddenSystem)
				)) b.AppendOtherArg(s1, hasFilter ? null : "flags");
		}
		b.Append(").");
		
		int iFunc = forTest ? 1 : _cbResult.SelectedIndex;
		const string c_transform = "(ref e) => (first: e.EditMe, second: e.EditMe)";
		string sFunc = iFunc switch {
			0 => getWhat switch { 0 => "Files", 1 => "Directories", _ => "All" },
			1 => getWhat switch { 0 => "Enumerate(true)", 1 => "Enumerate(false)", _ => "Enumerate()" },
			2 => getWhat switch { 0 => "FileNames", 1 => "DirectoryNames", _ => "AllNames" },
			3 => getWhat switch { 0 => "FilePaths", 1 => "DirectoryPaths", _ => "AllPaths" },
			4 => getWhat switch { 0 => "FileInfos", 1 => "DirectoryInfos", _ => "AllInfos" },
			5 => getWhat switch { 0 => "EnumerateInfos(true)", 1 => "EnumerateInfos(false)", _ => "EnumerateInfos()" },
			6 => getWhat switch { 0 => $"Enumerate({c_transform}, true).ToArray()", 1 => $"Enumerate({c_transform}, false).ToArray()", _ => $"Enumerate({c_transform}).ToArray()" },
			_ => getWhat switch { 0 => $"Enumerate({c_transform}, true)", 1 => $"Enumerate({c_transform}, false)", _ => $"Enumerate({c_transform})" },
		};
		b.Append(sFunc);
		
		if (!forTest) {
			if (_cWhere.IsChecked) b.Append("\r\n\t").Append(c_Where);
			if (_cOrderBy.IsChecked) { b.Append("\r\n\t").Append(c_OrderBy); if (_cThenBy.IsChecked) b.Append(c_ThenBy); }
			if (_cSelect.IsChecked) b.Append("\r\n\t").Append(c_Select);
		}
		b.Append(';');
		
		if (_cForeach.IsChecked || forTest) {
			b.Append("\r\nforeach (var f in a) {\r\n\tprint.it(f);\r\n\t");
			if (forTest) b.Append("if (cancel.IsCancellationRequested) break;");
			b.Append("\r\n}");
		}
		
		var R = b.ToString();
		
		if (!(forTest | onOK)) _code.AaSetText(R);
		
		return R;
	}
}
