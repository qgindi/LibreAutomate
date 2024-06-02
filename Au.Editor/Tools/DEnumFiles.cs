using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using Au.Controls;
using Au.Tools;

class DEnumDir : KDialogWindow {
	public static void Dialog(string initFolder = null) {
		new DEnumDir(initFolder).Show();
	}
	
	KTextBoxFile _tFolder;
	ComboBox _cbGet;
	TextBox _tFilter;
	KCheckBox _cArray, _cForeach, _cFileFilter, _cDirFilter, _cRecurse, _cIgnore, _cSymlink, _cRelative, _cSkipHidden, _cSkipHiddenSystem, _cWhere, _cOrderBy, _cThenBy, _cSelectPath, _cSelectOther/*, _cNet*/;
	Panel _p1, _p2;
	KSciCodeBox _code;
	
	const string c_Where = ".Where(o => o.EditMe > EditMe)",
		c_OrderBy = ".OrderBy(o => o.EditMe)",
		c_ThenBy = ".ThenBy(o => o.EditMe)",
		c_Select = ".Select(o => o.EditMe)";
	
	DEnumDir(string initFolder = null) {
		InitWinProp("Get files in folder", App.Wmain);
		
		_noeventValueChanged = true;
		var b = new wpfBuilder(this).WinSize(600).Columns(0, 90, 90, -1);
		
		b.R.StartGrid().Columns(76, 76, 0, 0, -1);
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
			InsertCode.Statements(s, ICSFlags.MakeVarName1);
		});
		
		b.Add(out _cArray, "Get array").Checked().Tooltip("Store results in an array variable before starting to use them.\nThen it is safe to delete/rename/move/copy the files.\nBut then cannot start using results until all retrieved from the file system.\nCheck if going to delete/rename/move/copy files.\nUncheck if going to search in a potentially large directory.")
			.Add(out _cForeach, "Use foreach").Checked();
		//b.Add(out _cNet, "Use only .NET classes");
		b.End();
		b.R.AddSeparator();
		
		b.R.Add("Folder", out _tFolder, initFolder).Tooltip("Folder path.\nPaste, drop or right-click.").Focus();
		_tFolder.IsFolder = true;
		_tFolder.Unexpand = App.Settings.tools_pathUnexpand;
		
		b.R.Add("Get", out _cbGet).Items("files|folders|all").Margin("R12");
		_cbGet.SelectionChanged += (_, _) => {
			_p1.Visibility = _cbGet.SelectedIndex == 2 ? Visibility.Hidden : Visibility.Visible;
			_p2.Visibility = _cbGet.SelectedIndex == 2 ? Visibility.Visible : Visibility.Hidden;
		};
		
		b.StartGrid().Columns(0, -1, 28);
		_p1 = b.Panel;
		b.Add("Name wildex", out _tFilter).Tooltip("Optional file name wildex.\nExamples:\n*.txt\n**m *.png||*.jpg")
			.AddButton("?", _ => HelpUtil.AuHelp("articles/Wildcard expression"));
		b.End().Span(2);
		
		b.And(0).StartStack();
		_p2 = b.Panel;
		b.Add(out _cFileFilter, "Use file filter").Add(out _cDirFilter, "Use folder filter").Margin("L8");
		b.End().Hidden();
		
		b.R.StartGrid<KGroupBox>("Flags");
		b.R.Add(out _cRecurse, "Include subfolders");
		b.R.Add(out _cIgnore, "Ignore inaccessible").Margin("L22").xBindCheckedVisible(_cRecurse);
		b.R.Add(out _cSymlink, "Follow NTFS links").Margin("L22").xBindCheckedVisible(_cRecurse).Tooltip("Enumerate the target folder of NTFS links (symbolic links, volume mount points, etc)");
		b.R.Add(out _cRelative, "Get relative path").Margin("L22").xBindCheckedVisible(_cRecurse).Tooltip("Store relative path in the Name property");
		b.R.Add(out _cSkipHidden, "Skip hidden");
		b.R.Add(out _cSkipHiddenSystem, "Skip hidden system");
		b.End().Span(3);
		
		b.StartGrid<KGroupBox>("Results");
		b.R.Add(out _cWhere, "Where")
			.Tooltip($"Add code '{c_Where}' to filter results by size, date, etc.\nThen in the code editor you'll edit it.");
		b.R.StartStack();
		b.Add(out _cOrderBy, "OrderBy")
			.Tooltip($"Add code '{c_OrderBy}' to sort results by some property.\nThen in the code editor you'll edit it. You can replace OrderBy with OrderByDescending.");
		b.Add(out _cThenBy, "ThenBy").xBindCheckedVisible(_cOrderBy)
			.Tooltip($"Adds code '{c_ThenBy}' to sort results by another property.\nThen in the code editor you'll edit it. You can replace ThenBy with ThenByDescending, and append more ThenBy/ThenByDescending if need.");
		b.End();
		b.R.StartStack();
		b.Add(out _cSelectPath, "Select path")
			.Tooltip($"Get only paths, not all properties.");
		b.Add(out _cSelectOther, "Select other")
			.Tooltip($"Add code '{c_Select}' to change the type of results.\nFor example select a property or several properties (tuple).\nThen in the code editor you'll edit it.");
		b.End();
		b.End();
		
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
	
	static DEnumDir() {
		TUtil.OnAnyCheckTextBoxValueChanged<DEnumDir>((d, o) => d._AnyCheckTextBoxComboValueChanged(o), comboToo: true);
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
		string filter = _tFilter.Text;
		bool hasFilter = getWhat != 2 && !filter.NE();
		
		var b = new StringBuilder();
		b.AppendLine($"var folder = {_tFolder.GetCode()};");
		b.Append("var a = ");
		//if (_cNet.IsChecked) {
		
		//} else {
		b.Append("filesystem.").Append(getWhat switch { 0 => "enumFiles", 1 => "enumDirectories", _ => "enumerate" });
		b.Append("(folder");
		if (hasFilter) b.AppendStringArg(filter);
		bool rec = _cRecurse.IsChecked;
		if (rec || _cSkipHidden.IsChecked || _cSkipHiddenSystem.IsChecked) {
			if (TUtil.FormatFlags(out var s1,
				(rec, FEFlags.AllDescendants),
				(rec && _cIgnore.IsChecked, FEFlags.IgnoreInaccessible),
				(rec && _cSymlink.IsChecked, FEFlags.RecurseNtfsLinks),
				(rec && _cRelative.IsChecked, FEFlags.NeedRelativePaths),
				(_cSkipHidden.IsChecked, FEFlags.SkipHidden),
				(_cSkipHiddenSystem.IsChecked, FEFlags.SkipHiddenSystem)
				)) b.AppendOtherArg(s1, hasFilter ? null : "flags");
		}
		if (getWhat == 2 && !forTest) {
			if (_cFileFilter.IsChecked) b.AppendOtherArg("""f => f.Name.Ends(".example", true)""");
			if (_cDirFilter.IsChecked) b.AppendOtherArg("""d => d.Name.Eqi("Example") ? 0 : 3""");
			//rejected: errorHandler parameter.
		}
		b.Append(')');
		if (!forTest) {
			int len1 = b.Length;
			if (_cWhere.IsChecked) b.Append("\r\n\t").Append(c_Where);
			if (_cOrderBy.IsChecked) { b.Append("\r\n\t").Append(c_OrderBy); if (_cThenBy.IsChecked) b.Append(c_ThenBy); }
			if (_cSelectOther.IsChecked) b.Append("\r\n\t").Append(_cSelectPath.IsChecked ? ".Select(o => (path: o.FullPath, other: o.EditMe))" : c_Select);
			else if (_cSelectPath.IsChecked) b.Append("\r\n\t.Select(o => o.FullPath)");
			if (b.Length > len1) b.Append("\r\n\t");
		}
		//}
		
		b.Append(_cArray.IsChecked && !forTest ? ".ToArray();" : ";");
		if (_cForeach.IsChecked || forTest) {
			b.Append("\r\nforeach ");
			if (!forTest && (_cSelectPath.IsChecked || _cSelectOther.IsChecked)) b.Append("(var f in a) {\r\n\tprint.it(f);");
			else b.Append("(var f in a) {\r\n\tvar path = f.FullPath;\r\n\tprint.it(path);");
			b.Append("\r\n\t");
			if (forTest) b.Append("if (cancel.IsCancellationRequested) break;");
			b.Append("\r\n}");
		}
		
		var R = b.ToString();
		
		if (!(forTest | onOK)) _code.AaSetText(R);
		
		return R;
	}
}
