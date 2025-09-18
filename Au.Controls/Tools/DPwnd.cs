using Au.Controls;
using System.Windows;
using System.Windows.Controls;

namespace Au.Tools;

/// <summary>
/// Dialog page for capturing a top-level window and editing its properties.
/// The result is <b>wnd.find</b> arguments string.
/// </summary>
class DPwnd : UserControl {
	wnd _wnd;
	string _wndName;
	
	KSciInfoBox _info;
	Button _bTest;
	KCheckBox _cCapture;
	Controls _k;
	
	public DPwnd() : this(null) { }
	
	public DPwnd(string header) {
		var b = new wpfBuilder(this).Columns(-1);
		Background = SystemColors.ControlBrush; //no tooltip etc without a brush
		
		if (header != null) b.xAddGroupSeparator(header);
		b.Row(50).Add(out _info);
		
		b.R.StartGrid().Columns(0, 76, -1);
		b.xAddCheckIcon(out _cCapture, "*Unicons.Capture" + EdIcons.red, $"Enable capturing ({Editor.Settings.delm.hk_capture}) and show window rectangles");
		b.AddButton(out _bTest, "Test", _bTest_Click).Span(1).Disabled().Tooltip("Find the window and show the rectangle");
		b.End();
		
		b.R.xStartPropertyGrid("L2 T3 R2 B1");
		_k = new(b, true);
		b.xEndPropertyGrid();
		
		b.End();
		
		bool loadedOnce = false;
		this.Loaded += (_, e) => {
			if (!loadedOnce) {
				loadedOnce = true;
				_InitInfo();
			}
			_cCapture.IsChecked = true;
		};
		
		IsVisibleChanged += (_, e) => {
			if (!(bool)e.NewValue) _cCapture.IsChecked = false;
		};
	}
	
	static DPwnd() {
		TUtil.OnAnyCheckTextBoxValueChanged<DPwnd>((d, o) => d._AnyCheckTextBoxValueChanged(o));
	}
	
	public void SetWnd(wnd w) {
		bool newWindow = w != _wnd;
		_wnd = w;
		_noeventValueChanged = true;
		
		string wndName = _wnd.NameTL_;
		if (newWindow) _k.Fill(_wnd, wndName, _wnd.ClassName, _wnd.ProgramName);
		else if (wndName != _wndName) _k.FillChangedName(wndName);
		_wndName = wndName;
		
		_noeventValueChanged = false;
	}
	
	public void Clear() {
		_wnd = default;
		_wndName = null;
		_noeventValueChanged = true;
		_k.Clear();
		_noeventValueChanged = false;
	}
	
	//when checked/unchecked any checkbox, and when text changed of any textbox
	void _AnyCheckTextBoxValueChanged(object source) {
		if (source == _cCapture) {
			_cCapture_CheckedChanged();
		} else if (!_noeventValueChanged) {
			_noeventValueChanged = true;
			if (source is TextBox t && t.Tag is KCheckTextBox k) {
				k.CheckIfTextNotEmpty();
			}
			_noeventValueChanged = false;
		}
		
		if (source is KCheckBox c && _k.IsMyCheckbox(c)) _bTest.IsEnabled = _k.HasResults;
	}
	bool _noeventValueChanged;
	
	string _FormatCode(bool forTest = false) {
		var f = new TUtil.WindowFindCodeFormatter { Test = forTest };
		_k.GetResults(f);
		return f.Format();
	}
	
	#region capture
	
	TUtil.CapturingWithHotkey _capt;
	
	void _cCapture_CheckedChanged() {
		_capt ??= new TUtil.CapturingWithHotkey(
			_cCapture,
			p => (wnd.fromXY(p, WXYFlags.NeedWindow).Rect, null),
			(Editor.Settings.delm.hk_capture, _Capture)
			);
		_capt.Capturing = _cCapture.IsChecked;
	}
	
	void _Capture() {
		var c = wnd.fromMouse(WXYFlags.NeedWindow); if (c.Is0) return;
		SetWnd(c);
		var w = this.Hwnd();
		if (w.IsMinimized) {
			w.ShowNotMinMax();
			w.ActivateL();
		}
	}
	
	#endregion
	
	#region Result, Test
	
	/// <summary>
	/// true if any checkbox checked.
	/// </summary>
	public bool HasResult => _bTest.IsEnabled;
	
	///// <summary>
	///// The captured window. Or default if not captured.
	///// </summary>
	//public wnd AaResultWindow => _wnd;
	
	/// <summary>
	/// <b>wnd.find</b> arguments string, like <c>"\"name\", \"class\"..."</c>. Or null if not specified.
	/// </summary>
	public string AaResultCode {
		get {
			var s = _FormatCode();
			s = TUtil.ArgsFromWndFindCode(s);
			if (s.Starts("null, null, \"")) s = s.ReplaceAt(0, 13, "of: \"");
			return s == "null" ? null : s;
		}
	}
	
	private void _bTest_Click(WBButtonClickArgs ea) {
		var code = _FormatCode(true);
		var rr = TUtil.RunTestFindObject(this, code, "w", _wnd, getRect: o => {
			var w = (wnd)o;
			var r = w.Rect;
			if (w.IsMaximized) {
				var k = w.Screen.Rect; k.Inflate(-2, -2);
				r.Intersect(k);
			}
			return r;
		});
		_info.InfoErrorOrInfo(rr.info);
	}
	
	#endregion
	
	#region info
	
	TUtil.CommonInfos _commonInfos;
	void _InitInfo() {
		_commonInfos = new TUtil.CommonInfos(_info);
		string s1 = _wnd.Is0 ? "C" : "You can c", s = $@"{s1}apture a window with <+hotkey>hotkey<> <b>{Editor.Settings.delm.hk_capture}<>.";
		_info.aaaText = s;
		_info.AaAddElem(this, s);
		TUtil.RegisterLink_DialogHotkey(_info);
		_k.InitInfo(_info);
	}
	
	#endregion
	
	//used by Dwnd too
	public class Controls {
		public KCheckTextBox name, cn, program, contains, also;
		
		public Controls(wpfBuilder b, bool addAlso) {
			b.Columns(70, -1);
			name = b.xAddCheckText<KTextExpressionBox>("name");
			cn = b.xAddCheckText<KTextExpressionBox>("class");
			program = b.xAddCheckTextDropdown<KTextExpressionBox>("program");
			contains = b.xAddCheckTextDropdown<KTextExpressionBox>("contains");
			if (addAlso) also = b.xAddCheckText("also", "o=>true");
		}
		
		public void InitInfo(KSciInfoBox ib) {
			ib.InfoCT(name, "Window name.", true);
			ib.InfoCT(cn, "Window class name.", true);
			ib.InfoCT(program, "Program.", true);
			ib.InfoCT(contains, """
A UI element in the window. Format: e 'role' name.
Or a control in the window. Format: c 'class' text.
""", true, "name/class/text");
			ib.InfoCT(also, "<help>wnd.find<> " + TUtil.CommonInfos.c_alsoParameter);
		}
		
		public void Fill(wnd w, string name_, string cn_, string program_) {
			name.Set(true, TUtil.EscapeWindowName(name_, true));
			cn.Set(true, TUtil.StripWndClassName(cn_, true));
			var ap = new List<string> { program_, "WOwner.Process(processId)", "WOwner.Thread(threadId)" }; if (!w.Get.Owner.Is0) ap.Add("WOwner.Window(ow)");
			program.Set(name_.NE(), program_, ap);
			contains.Set(false, null, _ContainsCombo_DropDown);
			
			List<string> _ContainsCombo_DropDown() {
				try {
					var a1 = new List<string>();
					//child
					foreach (var c in w.Get.Children(onlyVisible: true)) {
						var cn = c.Name; if (cn.NE()) continue;
						cn = "c '" + TUtil.StripWndClassName(c.ClassName, true) + "' " + TUtil.EscapeWildex(cn);
						if (!a1.Contains(cn)) a1.Add(cn);
					}
					//elm
					var a2 = new List<string>();
					var a3 = w.Elm[name: "?*", prop: "notin=SCROLLBAR\0maxcc=100", flags: EFFlags.ClientArea].FindAll(); //all that have a name
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
		
		public void FillChangedName(string name_) {
			if (TUtil.ShouldChangeTextBoxWildex(name.t.Text, name_))
				name.Set(true, TUtil.EscapeWindowName(name_, true));
		}
		
		public void GetResults(TUtil.WindowFindCodeFormatter f) {
			name.GetText(out f.nameW, emptyToo: true);
			cn.GetText(out f.classW);
			program.GetText(out f.programW);
			also.GetText(out f.alsoW);
			contains.GetText(out f.containsW);
		}
		
		public bool IsMyCheckbox(KCheckBox c) => c == name.c || c == cn.c || c == program.c || c == also.c || c == contains.c;
		
		public bool HasResults => name.c.IsChecked || cn.c.IsChecked || program.c.IsChecked || also.c.IsChecked || contains.c.IsChecked;
		
		public void Clear() {
			name.Clear();
			cn.Clear();
			program.Clear();
			contains.Clear();
			also.Clear("o=>true");
		}
	}
}
