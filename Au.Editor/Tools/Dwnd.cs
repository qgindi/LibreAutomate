using System.Windows;
using System.Windows.Controls;
using Au.Controls;

//FUTURE: add UI to format code 'w = w.Get.Right();' etc.
//FUTURE: init from code string.
//SHOULDDO: try to find and select control in current tree when captured from same window. Like in Delm.

namespace Au.Tools;

[Flags]
enum DwndFlags {
	DontInsert = 1,
	NoControl = 2,
	CheckControl = 4,
	ForTrigger = 8|1|2,
}

class Dwnd : KDialogWindow {
	public static void Dialog(wnd w = default)
		=> TUtil.ShowDialogInNonmainThread(() => new Dwnd(w));

	wnd _wnd, _con;
	bool _dontInsert, _noControl, _checkControl, _forTrigger;
	string _wndName;

	KSciInfoBox _info, _winInfo;
	Button _bTest, _bInsert;
	KCheckBox _cCapture, _cControl, _cActivate, _cException;
	ComboBox _cbFunc;
	Separator _sepControl;
	ScrollViewer _scroller;
	KSciCodeBoxWnd _code;
	KTreeView _tree;

	Grid _gCon1, _gCon2;
	KCheckTextBox nameW, classW, programW, containsW, idC, nameC, classC, alsoW, alsoC, waitW, skipC;
	KCheckBox cHiddenTooW, cCloakedTooW, cHiddenTooC;

	public Dwnd(wnd w = default, DwndFlags flags = 0, string title = "Find window") {
		_dontInsert = flags.Has(DwndFlags.DontInsert);
		_noControl = flags.Has(DwndFlags.NoControl);
		_checkControl = flags.Has(DwndFlags.CheckControl);
		_forTrigger = flags.Has(DwndFlags.ForTrigger);

		Title = title;

		var b = new wpfBuilder(this).WinSize((500, 450..), (600, 430..)).Columns(-1);
		b.R.Add(out _info).Height(60);
		b.R.StartGrid().Columns(0, 76, 76, 0, 0, -1);
		_cCapture = b.xAddCheckIcon("*Unicons.Capture" + Menus.red, $"Enable capturing ({App.Settings.delm.hk_capture}) and show window/control rectangles");
		b.AddButton(out _bTest, "Test", _bTest_Click).Disabled().Tooltip("Execute the 'find' part of the code now and show the rectangle");
		b.AddButton(out _bInsert, _dontInsert ? "OK" : "Insert", _Insert).Disabled(); if (!_dontInsert) b.Tooltip("Insert code in editor");
		b.Add(out _cbFunc).Items("find|findOrRun|runAndFind|finder").Tooltip("Function").Width(90);
		_cbFunc.SelectionChanged += _cbFunc_SelectionChanged;
		b.Add(out _cActivate, "Activate").Tooltip("Activate the found window");
		b.Add(out _cException, "Fail if not found").Checked(!_forTrigger).Tooltip("Throw exception if not found");
		//cActivate.CheckChanged += (_, _) => { cException.Visibility = cActivate.IsChecked ? Visibility.Hidden : Visibility.Visible; }; //no, need for control too
		b.End();

		//window and control properties and search settings
		b.R.AddSeparator(false).Margin("B");
		b.Row(0); //auto height, else adds v scrollbar when textbox height changes when a textbox text is multiline or too long (with h scrollbar)
		_scroller = b.xStartPropertyGrid("L2 T3 R2 B1"); //actually never shows scrollbar because of row auto height, but sets some options etc
		_scroller.Visibility = Visibility.Hidden;
		b.Columns(-3, 0, -1.2);
		//window
		b.R.Add<TextBlock>("Window").Margin("T1 B3").xSetHeaderProp(); //rejected: vertical headers. Tested, looks not good, too small for vertical Control checkbox.
		b.Row(0).StartGrid().Columns(70, -1);
		nameW = b.xAddCheckText("name");
		classW = b.xAddCheckText("class");
		programW = b.xAddCheckTextDropdown("program");
		containsW = b.xAddCheckTextDropdown("contains");
		b.End();
		b.xAddSplitterV(span: 4, thickness: 12);
		b.StartGrid().Columns(44, -1);
		b.xAddCheck(out cHiddenTooW, "Find hidden too");
		b.xAddCheck(out cCloakedTooW, "Find cloaked too");
		alsoW = b.xAddCheckText("also", "o=>true");
		waitW = b.xAddCheckText("wait", "1", check: !_forTrigger);
		b.End();
		//control
		b.R.AddSeparator(false).Margin("T4 B0"); _sepControl = b.Last as Separator;
		b.R.Add(out _cControl, "Control").Margin("T5 B3").xSetHeaderProp();
		b.Row(0).StartGrid().Columns(70, -1); _gCon1 = b.Panel as Grid;
		nameC = b.xAddCheckTextDropdown("name");
		classC = b.xAddCheckText("class");
		idC = b.xAddCheckText("id");
		b.End().Skip();
		b.StartGrid().Columns(44, -1); _gCon2 = b.Panel as Grid;
		b.xAddCheck(out cHiddenTooC, "Find hidden too");
		alsoC = b.xAddCheckText("also", "o=>true");
		skipC = b.xAddCheckText("skip");
		b.End();
		b.xEndPropertyGrid();
		b.R.AddSeparator(false);

		if (_forTrigger) {
			_cbFunc.Visibility = Visibility.Hidden;
			_cActivate.Visibility = Visibility.Hidden;
			_cException.Visibility = Visibility.Hidden;
			waitW.Visible = false;
		}

		//code
		b.Row(64).xAddInBorder(out _code, "B");

		//tree and window info
		b.xAddSplitterH(span: -1);
		b.Row(-1).StartGrid().Columns(-1, 0, -1);
		b.Row(-1).xAddInBorder(out _tree, "TR");
		b.xAddSplitterV();
		b.xAddInBorder(out _winInfo, "TL"); _winInfo.AaWrapLines = false; _winInfo.Name = "window_info";
		b.End();

		b.End();

		_InitTree();

		_con = w;

		b.WinProperties(
			topmost: true,
			showActivated: _dontInsert || w.Is0 ? null : false //eg if captured a popup menu, activating this window closes the menu and we cannot get properties
			);

		WndSavedRect.Restore(this, App.Settings.wndpos.wnd, o => App.Settings.wndpos.wnd = o);
	}

	static Dwnd() {
		TUtil.OnAnyCheckTextBoxValueChanged<Dwnd>((d, o) => d._AnyCheckTextBoxValueChanged(o));
	}

	protected override void OnSourceInitialized(EventArgs e) {
		base.OnSourceInitialized(e);

		if (!_con.Is0) _SetWnd(false);
		_InitInfo();
		_cCapture.IsChecked = true;
	}

	protected override void OnClosing(CancelEventArgs e) {
		_cCapture.IsChecked = false;

		base.OnClosing(e);
	}

	void _SetWnd(bool captured) {
		_bTest.IsEnabled = true; _bInsert.IsEnabled = true;

		var wndOld = _wnd;
		_wnd = _con.Window;
		if (_wnd == _con || _noControl) _con = default;
		bool newWindow = _wnd != wndOld;

		_ClearTree();
		if (!_FillProperties(newWindow)) return;
		_FormatCode(false);
		_FillTree();
	}

	bool _FillProperties(bool newWindow) {
		bool isCon = !_con.Is0;

		_WinInfo f = default;

		if (!_GetClassName(_wnd, out f.wClass)) return false; //note: get even if !newWindow, to detect closed window
		if (isCon && !_GetClassName(_con, out f.cClass)) return false;

		bool _GetClassName(wnd w, out string cn) {
			cn = w.ClassName; if (cn != null) return true;
			_winInfo.aaaText = "Failed to get " + (w == _wnd ? "window" : "control") + " properties: \r\n" + lastError.message;
			_scroller.Visibility = Visibility.Hidden;
			return false;
		}

		_noeventValueChanged = true;

		var wndName = _wnd.NameTL_;
		if (newWindow) {
			nameW.Set(true, TUtil.EscapeWindowName(wndName, true));
			classW.Set(true, TUtil.StripWndClassName(f.wClass, true));
			f.wProg = _wnd.ProgramName;
			var ap = new List<string> { f.wProg, "WOwner.Process(processId)", "WOwner.Thread(threadId)" }; if (!_wnd.Get.Owner.Is0) ap.Add("WOwner.Window(ow)");
			programW.Set(wndName.NE(), f.wProg, ap);
			containsW.Set(false, null, _ContainsCombo_DropDown);
		} else if (wndName != _wndName) {
			if (TUtil.ShouldChangeTextBoxWildex(nameW.t.Text, wndName))
				nameW.Set(true, TUtil.EscapeWindowName(wndName, true));
		}
		f.wName = _wndName = wndName;

		if (isCon) {
			//name combo
			f.cName = _con.Name;
			int iSel = f.cName.NE() ? -1 : 0;
			var an = new List<string> { TUtil.EscapeWildex(f.cName) };
			_ConNameAdd("***wfName ", f.cWF = _con.NameWinforms);
			/*bool isElm =*/
			_ConNameAdd("***elmName ", f.cElm = _con.NameElm);
			//bool isLabel = _ConNameAdd("***label ", f.cLabel = _con.NameLabel);
			//if(isElm && isLabel && iSel == an.Count - 2 && f.cElm == f.cLabel) iSel++; //if label == elmName, prefer label
			if (iSel < 0) iSel = 0; //never select text, even if all others unavailable
			_ConNameAdd("***text ", f.cText = _con.ControlText);
			bool _ConNameAdd(string prefix, string value) {
				if (value.NE()) return false;
				if (iSel < 0) iSel = an.Count;
				an.Add(prefix + TUtil.EscapeWildex(value));
				return true;
			}

			bool idUseful = TUtil.GetUsefulControlId(_con, _wnd, out f.cId);
			//idC.Visible = idUseful;
			idC.Set(idUseful, f.cId.ToS() + (idUseful ? null : " /*probably not useful*/"));
			string sName = an[iSel], sClass = TUtil.StripWndClassName(f.cClass, true);
			nameC.Set(!idUseful, sName, an);
			classC.Set(!idUseful, sClass);
			bool hiddenToo = !_con.IsVisible;
			cHiddenTooC.IsChecked = hiddenToo;
			var skip = idUseful ? null : TUtil.GetControlSkip(_wnd, _con, sName, sClass, hiddenToo);
			skipC.Set(skip != null, skip);
		}

		bool checkControl = isCon && _checkControl;
		_cControl.IsChecked = checkControl;
		_ShowControlProperties(showGrid: checkControl, showAll: isCon);

		_noeventValueChanged = false;

		_scroller.Visibility = Visibility.Visible;
		_FillWindowInfo(f);
		return true;

		List<string> _ContainsCombo_DropDown() {
			try {
				var a1 = new List<string>();
				//child
				foreach (var c in _wnd.Get.Children(onlyVisible: true)) {
					var cn = c.Name; if (cn.NE()) continue;
					cn = "c '" + TUtil.StripWndClassName(c.ClassName, true) + "' " + TUtil.EscapeWildex(cn);
					if (!a1.Contains(cn)) a1.Add(cn);
				}
				//elm
				var a2 = new List<string>();
				var a3 = _wnd.Elm[name: "?*", prop: "notin=SCROLLBAR\0maxcc=100", flags: EFFlags.ClientArea].FindAll(); //all that have a name
				string prevName = null;
				for (int i = a3.Length; --i >= 0;) {
					if (!a3[i].GetProperties("Rn", out var prop)) continue;
					if (prop.Name == prevName && prop.Role == "WINDOW") continue; prevName = prop.Name; //skip parent WINDOW
					string rn = "e '" + prop.Role + "' " + TUtil.EscapeWildex(prop.Name);
					if (!a2.Contains(rn)) a2.Add(rn);
				}
				a2.Reverse();
				a1.AddRange(a2);

				return a1;
				//rejected: sort
			}
			catch (Exception ex) { Debug_.Print(ex); return null; }
		}
	}

	private void _cbFunc_SelectionChanged(object sender, SelectionChangedEventArgs e) {
		int iFunc = _cbFunc.SelectedIndex;
		_noeventValueChanged = true;
		var vis = iFunc == 3 ? Visibility.Hidden : Visibility.Visible;
		_cActivate.Visibility = vis;
		_cException.Visibility = vis;
		if (iFunc is 1 or 2) _cActivate.IsChecked = true;
		waitW.Visible = iFunc is 0 or 2;
		if (iFunc == 2) {
			if (waitW.t.Text == "1") waitW.t.Text = "30";
			waitW.c.IsChecked = true;
		}
		_noeventValueChanged = false;
		_FormatCode();
	}

	//when checked/unchecked any checkbox, and when text changed of any textbox
	void _AnyCheckTextBoxValueChanged(object source) {
		if (source == _cCapture) {
			_cCapture_CheckedChanged();
		} else if (!_noeventValueChanged) {
			_noeventValueChanged = true;
			if (source is KCheckBox c) {
				bool on = c.IsChecked;
				if (c == _cControl) {
					_checkControl = on;
					_ShowControlProperties(on, null);
				}
			} else if (source is TextBox t && t.Tag is KCheckTextBox k) {
				_noeventValueChanged = _formattedOnValueChanged = false; //allow auto-check but prevent formatting twice
				k.CheckIfTextNotEmpty();
				if (_formattedOnValueChanged) return;
			}
			_noeventValueChanged = false;
			_formattedOnValueChanged = true;
			_FormatCode();
		}
	}
	bool _noeventValueChanged = true, _formattedOnValueChanged;

	void _ShowControlProperties(bool showGrid, bool? showAll) {
		if (showAll != null) {
			var vis = showAll.Value ? Visibility.Visible : Visibility.Hidden;
			_cControl.Visibility = vis;
			_sepControl.Visibility = vis;
			_gCon1.Visibility = vis;
			_gCon2.Visibility = vis;
		}
		_gCon1.IsEnabled = showGrid;
		_gCon2.IsEnabled = showGrid;
	}

	(string code, string wndVar) _FormatCode(bool forTest = false) {
		if (!_scroller.IsVisible) return default; //still not captured, or failed to get window props
		int iFunc = _cbFunc.SelectedIndex;

		var f = new TUtil.WindowFindCodeFormatter {
			Test = forTest,
			Finder = iFunc == 3,
			NeedControl = !_con.Is0 && _cControl.IsChecked,
			Throw = _cException.IsChecked,
			Activate = _cActivate.IsChecked,
			hiddenTooW = cHiddenTooW.IsChecked,
			cloakedTooW = cCloakedTooW.IsChecked,
			hiddenTooC = cHiddenTooC.IsChecked,
		};

		if (!forTest && iFunc is 1 or 2) {
			var k = TUtil.PathInfo.FromWindow(_wnd);
			if (k != null) {
				var s1 = k.FormatCode(TUtil.PathCode.Run);
				if (iFunc == 1) f.orRunW = s1; else f.andRunW = s1;
			}
		}

		nameW.GetText(out f.nameW, emptyToo: true);
		classW.GetText(out f.classW);
		programW.GetText(out f.programW);
		alsoW.GetText(out f.alsoW);
		containsW.GetText(out f.containsW);
		if (!forTest) waitW.GetText(out f.waitW, emptyToo: true);

		if (f.NeedControl) {
			nameC.GetText(out f.nameC, emptyToo: true);
			classC.GetText(out f.classC);
			idC.GetText(out f.idC);
			alsoC.GetText(out f.alsoC);
			skipC.GetText(out f.skipC);
			f.nameC_comments = nameC.t.Text;
			f.classC_comments = classC.t.Text;
		}

		var R = f.Format();

		if (!forTest) {
			_code.AaSetText(R);
		}

		return (R, "w");
	}

	#region capture

	TUtil.CapturingWithHotkey _capt;

	void _cCapture_CheckedChanged() {
		_capt ??= new TUtil.CapturingWithHotkey(
			_cCapture,
			p => (wnd.fromXY(p, _noControl ? WXYFlags.NeedWindow : 0).Rect, null),
			(App.Settings.delm.hk_capture, _Capture)
			);
		_capt.Capturing = _cCapture.IsChecked;
	}

	void _Capture() {
		var c = wnd.fromMouse(); if (c.Is0) return;
		_con = c;
		_SetWnd(true);
		var w = this.Hwnd();
		if (w.IsMinimized) {
			w.ShowNotMinMax();
			w.ActivateL();
		}
	}

	#endregion

	#region Insert, Test

	/// <summary>
	/// When OK clicked, the top-level window (even when <see cref="AaResultUseControl"/> is true).
	/// </summary>
	public wnd AaResultWindow => _wnd;

	/// <summary>
	/// When OK clicked, the control (even when <see cref="AaResultUseControl"/> is false) or default(wnd).
	/// </summary>
	public wnd AaResultControl => _con;

	/// <summary>
	/// When OK clicked, true if a control was selected and the 'Control' checkbox checked.
	/// Use <see cref="AaResultWindow"/> or <see cref="AaResultControl"/>, depending on this property.
	/// </summary>
	public bool AaResultUseControl { get; private set; }

	/// <summary>
	/// When OK clicked, contains C# code. Else null.
	/// </summary>
	public string AaResultCode { get; private set; }

	//rejected. Can use Closed event; then aaResultCode not null if OK.
	///// <summary>
	///// When closed with OK button.
	///// </summary>
	//public event Action OK;

	private void _Insert(WBButtonClickArgs e) {
		var s = _code.aaaText.NullIfEmpty_();

		if (_forTrigger && s != null) s = TUtil.ArgsFromWndFindCode(s);

		if (_dontInsert) {
			AaResultCode = s;
			if (s == null) e.Cancel = true;
			else AaResultUseControl = !_con.Is0 && _cControl.IsChecked;

			if (this.IsModal_() == false) Close();
			else DialogResult = s != null;
		} else if (_close) {
			Close();
		} else if (s != null) {
			InsertCode.Statements(s, ICSFlags.MakeVarName1);
			_close = true;
			_bInsert.Content = "Close";
			_bInsert.MouseLeave += (_, _) => {
				_close = !true;
				_bInsert.Content = "Insert";
			};
		}
	}
	bool _close;

	private void _bTest_Click(WBButtonClickArgs ea) {
		var (code, wndVar) = _FormatCode(true); if (code.NE()) return;
		var rr = TUtil.RunTestFindObject(this, code, wndVar, _wnd, getRect: o => {
			var w = (wnd)o;
			var r = w.Rect;
			if (w.IsMaximized && !w.IsChild) {
				var k = w.Screen.Rect; k.Inflate(-2, -2);
				r.Intersect(k);
			}
			return r;
		});
		_info.InfoErrorOrInfo(rr.info);
	}

	#endregion

	#region tree

	void _InitTree() {
		_tree.SingleClickActivate = true;
		_tree.ItemActivated += e => {
			var x = e.Item as _TreeItem;
			_con = x.c == _wnd ? default : x.c;
			if (!_FillProperties(false)) return;
			_FormatCode();
			if (!_con.Is0) TUtil.ShowOsdRect(_con.Rect);
		};
	}

	void _ClearTree() {
		_tree.SetItems(null);
	}

	void _FillTree() {
		_TreeItem xWindow = new() { c = _wnd }, xSelect = _con.Is0 ? xWindow : null;
		_AddChildren(_wnd, xWindow);
		void _AddChildren(wnd wParent, _TreeItem nParent) {
			for (wnd t = wParent.Get.FirstChild; !t.Is0; t = t.Get.Next()) {
				var x = new _TreeItem() { c = t };
				nParent.AddChild(x);
				if (t == _con && xSelect == null) xSelect = x;
				_AddChildren(t, x);
			}
		}

		_tree.SetItems(new _TreeItem[1] { xWindow });

		//print.it(xWindow.Descendants().Select(o=>o.c));

		if (xSelect != null) {
			_tree.EnsureVisible(xSelect);
			_tree.SelectSingle(xSelect, true);
		}
	}

	class _TreeItem : TreeBase<_TreeItem>, ITreeViewItem {
		public wnd c;
		string _displayText;
		bool _isExpanded;
		bool _isFailed;

		#region ITreeViewItem

		string ITreeViewItem.DisplayText {
			get {
				if (_displayText == null) {
					var cn = c.ClassName;
					if (cn == null) {
						_isFailed = true;
						return _displayText = "Failed: " + lastError.message;
					}

					var name = c.Name;
					if (name.NE()) _displayText = cn;
					else {
						using (new StringBuilder_(out var b)) {
							name = name.Escape(limit: 250);
							b.Append(cn).Append("  \"").Append(name).Append('"');
							_displayText = b.ToString();
						}
					}
				}
				return _displayText;
			}
		}

		void ITreeViewItem.SetIsExpanded(bool yes) { _isExpanded = yes; }

		bool ITreeViewItem.IsExpanded => _isExpanded;

		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();

		bool ITreeViewItem.IsFolder => _IsFolder;
		bool _IsFolder => base.HasChildren;

		object ITreeViewItem.Image => _IsFolder ? EdResources.FolderArrow(_isExpanded) : null;

		int ITreeViewItem.TextColor(TVColorInfo ci)
			=> _isFailed ? 0xff
			: !c.IsVisible ? ColorInt.SwapRB(Api.GetSysColor(Api.COLOR_GRAYTEXT))
			: -1;

		#endregion
	}

	#endregion

	#region info

	struct _WinInfo {
		public string wClass, wName, wProg, cClass, cName, cText, /*cLabel,*/ cElm, cWF;
		public int cId;

		public string Format(wnd w, wnd c, int wcp) {
			var b = new StringBuilder();

			if (wcp == 2 && c.Is0) wcp = 1;
			if (!w.IsAlive) return "";
			if (wcp == 1) { //window
				b.AppendLine("<lc #B0E0B0><b>Window<>    <+switch 2>Control<>    <+switch 3>Process<><>");
				if (wClass == null) {
					wClass = w.ClassName;
					wName = w.Name;
				}
				_Common(false, b, w, wName, wClass);
			} else if (wcp == 2) { //control
				b.AppendLine("<lc #B0E0B0><+switch 1>Window<>    <b>Control<>    <+switch 3>Process<><>");
				if (c.IsAlive) {
					if (cClass == null) {
						cClass = c.ClassName;
						cName = c.Name;
						cText = c.ControlText;
						//cLabel = c.NameLabel;
						cElm = c.NameElm;
						cWF = c.NameWinforms;
						cId = c.ControlId;
					}
					_Common(true, b, c, cName, cClass);
				}
			} else { //program
				b.AppendLine("<lc #B0E0B0><+switch 1>Window<>    <+switch 2>Control<>    <b>Process<><>");
			g1:
				if (wProg == null) {
					wProg = w.ProgramName;
				}
				b.Append("<i>ProgramName<>:    ").AppendLine(wProg);
				b.Append("<i>ProgramPath<>:    ").AppendLine(w.ProgramPath);
				b.Append("<i>ProgramDescription<>:    ").AppendLine(w.ProgramDescription);
				int pid = w.ProcessId, tid = w.ThreadId;
				b.Append("<i>ProcessId<>:    ").AppendLine(pid.ToString());
				b.Append("<i>ThreadId<>:    ").AppendLine(tid.ToString());
				b.Append("<i>Is32Bit<>:    ").AppendLine(w.Is32Bit ? "true" : "false");
				using (var uac = uacInfo.ofProcess(pid)) {
					b.Append("<i><help articles/UAC>UAC<> IL, elevation<>:    ")
						.Append(uac.IntegrityLevel.ToString())
						.Append(", ").AppendLine(uac.Elevation.ToString());
				}
				b.Append("<i>CommandLine<>:    ").AppendLine(process.getCommandLine(pid));

				//if control's process or thread is different...
				if (!c.Is0) {
					int pid2 = c.ProcessId;
					if (pid2 != pid && pid2 != 0) {
						b.AppendLine("\r\n<c red>Control is in other process:<>");
						w = c; wProg = null;
						goto g1;
					}
					int tid2 = c.ThreadId;
					if (tid2 != tid && tid2 != 0) {
						b.AppendLine("\r\n<c red>Control is in other thread:<>");
						b.Append("<i>ThreadId<>:    ").AppendLine(tid2.ToString());
					}
				}
			}

			return b.ToString();
		}

		void _Common(bool isCon, StringBuilder b, wnd w, string name, string className) {
			string s, sh = w.Handle.ToString();
			b.Append("<i>Handle<>:    ").AppendLine(sh);
			b.Append("<i>ClassName<>:    ").AppendLine(className);
			if (!isCon || !name.NE()) b.Append("<i>Name<>:    ").AppendLine(name);
			if (isCon) {
				//if(!cLabel.NE()) b.Append("<i>NameLabel<>:    ").AppendLine(cLabel);
				if (!cElm.NE()) b.Append("<i>NameElm<>:    ").AppendLine(cElm);
				if (!cWF.NE()) b.Append("<i>NameWinForms<>:    ").AppendLine(cWF);
				if (!cText.NE()) b.Append("<i>ControlText<>:    ").Append("<\a>").Append(cText.Escape(10000, true)).AppendLine("</\a>");
				b.Append("<i>ControlId<>:    ").AppendLine(cId.ToString());
				b.AppendFormat("<+rect {0}><i>RectInWindow<><>:    ", sh).AppendLine(w.RectInWindow.ToString());
			} else {
				var wo = w.Get.Owner;
				if (!wo.Is0) b.AppendFormat("<+rect {0}><i>Owner<><>:    ", wo.Handle.ToString()).AppendLine(wo.ToString());
				b.AppendFormat("<+rect {0}><i>Rect<><>:    ", sh).AppendLine(w.Rect.ToString());
			}
			b.AppendFormat("<+rect {0} 1><i>ClientRect<><>:    ", sh).AppendLine(w.ClientRect.ToString());
			var style = w.Style;
			s = (style & (WS)0xffff0000).ToString();
			if (isCon) s = s.Replace("MINIMIZEBOX", "GROUP").Replace("MAXIMIZEBOX", "TABSTOP");
			uint style2 = ((uint)style) & 0xffff; //unknown styles of that class
			b.Append("<i>Style<>:  0x").Append(((uint)style).ToString("X8")).Append(" (").Append(s);
			if (style2 != 0) b.Append(", 0x").Append(style2.ToString("X4"));
			b.AppendLine(")");
			var estyle = w.ExStyle;
			b.Append("<i>ExStyle<>:  0x").Append(((uint)estyle).ToString("X8")).Append(" (").Append(estyle.ToString()).AppendLine(")");
			//b.Append("<i>Class style<>:  0x").AppendLine(((uint)WndUtil.GetClassLong(w, GCL.STYLE)).ToString("X8"));
			if (!isCon) {
				b.Append("<i>Is...<>:    ");
				_AppendIs(w.IsPopupWindow, "IsPopupWindow");
				_AppendIs(w.IsToolWindow, "IsToolWindow");
				_AppendIs(w.IsTopmost, "IsTopmost");
				_AppendIs(w.IsFullScreen, "IsFullScreen");
				_AppendIs(0 != w.IsUwpApp, "IsUwpApp");
				_AppendIs(w.IsWindows8MetroStyle, "IsWindows8MetroStyle");
				b.AppendLine();
			}
			b.Append("<i>Prop[\"...\"]<>:    "); bool isProp = false;
			foreach (var p in w.Prop.GetList()) {
				if (p.Key.Starts('#')) continue;
				if (!isProp) isProp = true; else b.Append(", ");
				b.Append(p.Key).Append(" = ").Append(p.Value.ToString());
			}
			b.AppendLine();

			void _AppendIs(bool yes, string prop) {
				if (b[^1] != ' ') b.Append(", ");
				if (!yes) b.Append('!');
				b.Append(prop);
			}
		}
	}

	void _FillWindowInfo(in _WinInfo f) {
		if (_wiWCP == 0) {
			_wiWCP = 1;
			_winInfo.AaTags.AddLinkTag("+switch", s => {
				_wiWCP = s.ToInt();
				_SetText(default);
			});
			_winInfo.AaTags.AddLinkTag("+rect", s => {
				var w = (wnd)s.ToInt(0, out int e);
				int client = s.ToInt(e);
				var r = client == 1 ? w.ClientRectInScreen : w.Rect;
				TUtil.ShowOsdRect(r, limitToScreen: w.IsMaximized);
			});
		}
		_SetText(f);

		void _SetText(in _WinInfo wi) {
			var s1 = wi.Format(_wnd, _con, _wiWCP);
			_winInfo.aaaText = s1.TrimEnd("\r\n");
		}
	}
	int _wiWCP; //0 not inited, 1 window, 2 control, 3 program

	TUtil.CommonInfos _commonInfos;
	void _InitInfo() {
		_commonInfos = new TUtil.CommonInfos(_info);

		_info.aaaText = _dialogInfo;
		_info.AaAddElem(this, _dialogInfo);
		TUtil.RegisterLink_DialogHotkey(_info);

		_info.InfoCT(nameW, "Window name.", true);
		_info.InfoCT(classW, "Window class name.", true);
		_info.InfoCT(programW, "Program.", true);
		_info.InfoCT(containsW,
@"A UI element in the window. Format: e 'role' name.
Or a control in the window. Format: c 'class' text.", true, "name/class/text");
		_info.InfoCT(idC, "Control id.");
		_info.InfoCT(nameC, "Control name.", true);
		_info.InfoCT(classC, "Control class name.", true);

		_info.InfoC(cHiddenTooW, "Flag <help>Au.Types.WFlags<>.HiddenToo.");
		_info.InfoC(cCloakedTooW, "Flag <help>Au.Types.WFlags<>.CloakedToo.");
		_info.InfoCT(waitW,
@"The wait timeout, seconds.
The function waits for such window max this time interval. On timeout throws exception if <b>Fail...<> checked, else returns default(wnd). If empty, uses 8e88 (infinite).");
		_info.InfoCT(alsoW,
@"<help>wnd.find<> " + TUtil.CommonInfos.c_alsoParameter);
		_info.InfoC(cHiddenTooC, "Flag <help>Au.Types.WCFlags<>.HiddenToo.");
		_info.InfoCT(alsoC,
@"<help>wnd.Child<> " + TUtil.CommonInfos.c_alsoParameter);
		_info.InfoCT(skipC,
@"0-based index of matching control.
For example, if 1, gets the second matching control.");

		_info.Info(_tree, "Tree view", "All child/descendant controls in the window.");

		//SHOULDDO: now no info for HwndHost
		//			_Info(_code, "Code",
		//@"Created code to find the window or control.
		//Some parts can be edited directly.
		//");
		//			_Info(_winInfo, "Window info",
		//@"Various properties of the selected window, control and process.
		//For example can be useful when creating <i>also<> function.
		//");
	}

	string _dialogInfo =
$@"This tool creates code to find <help wnd.find>window<> or <help wnd.Child>control<>.
1. Move the mouse to a window or control. Press <+hotkey>hotkey<> <b>{App.Settings.delm.hk_capture}<>.
2. Click the Test button to see how the 'find' code works.
3. If need, change some fields or select another window/control.
4. Click Insert. Click Close, or capture/insert again.
5. If need, edit the code in editor. For example rename variables, delete duplicate wnd.find lines, replace part of window name with *. Add code to use the window or control. Examples: w.Activate(); var s = w.Name;.

If the hotkey does not work when the target window is active, probably its process is admin and this process isn't. Or the process steals the hotkey; try another hotkey (Options > Hotkeys).";

	#endregion
}
