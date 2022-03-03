using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace Au.Controls;

using static Sci;

/// <summary>
/// This .NET control wraps native Scintilla control.
/// It is not a universal Scintilla wrapper class. Just for this library and related software.
/// </summary>
/// <remarks>
/// Most functions throw ArgumentOutOfRangeException when: 1. A position or line index argument is negative. 2. Scintilla returned a negative position or line index.
/// If a position or line index argument is greater than text length or the number of lines, some functions return the text length or the last line, and it is documented; for other functions the behaviour is undefined, eg ArgumentOutOfRangeException or Scintilla's return value or like of the documented methods.
/// 
/// Almost all function/event names start with z or Z, because VS intellisense cannot group by inheritance and would mix with 300 WPF functions/events. Most public and internal Scintilla API wrapper functions have prefix z, others Z. Derived classes can use prefix for example zz and ZZ.
/// </remarks>
public unsafe partial class KScintilla : HwndHost {
	wnd _w;
	WNDPROC _wndproc;
	//Api.SUBCLASSPROC _wndproc2;
	nint _sciPtr;
	Sci_NotifyCallback _notifyCallback;
	internal int _dpi;

#if DEBUG
	internal bool test_; //we use many scintilla controls, but often want to test something on one of them. Then set test_ = true...
#endif

	static KScintilla() {
		filesystem.more.loadDll64or32Bit_("Scintilla.dll");
		filesystem.more.loadDll64or32Bit_("Lexilla.dll");
	}

	public nint ZSciPtr => _sciPtr;

	public SciImages ZImages { get; private set; }

	public SciTags ZTags { get; private set; }

	#region HwndHost

	public wnd Hwnd => _w; //not ZHwnd, to avoid accidental use of extension method Hwnd()

	public event Action ZHandleCreated;

	/// <summary>
	/// Invokes event <see cref="ZHandleCreated"/>.
	/// </summary>
	protected virtual void ZOnHandleCreated() => ZHandleCreated?.Invoke();

	protected override HandleRef BuildWindowCore(HandleRef hwndParent) {
		var wParent = (wnd)hwndParent.Handle;
		_dpi = Dpi.OfWindow(wParent);
		WS style = WS.CHILD; if (ZInitBorder) style |= WS.BORDER;
		//note: no WS_VISIBLE. WPF will manage it. It can cause visual artefacts occasionally, eg scrollbar in WPF area.
		_w = Api.CreateWindowEx(0, "Scintilla", Name, style, 0, 0, 0, 0, wParent);
		//size 0 0 is not the best, but it is a workaround for WPF bugs

		_sciPtr = _w.Send(SCI_GETDIRECTPOINTER);
		Call(SCI_SETNOTIFYCALLBACK, 0, Marshal.GetFunctionPointerForDelegate(_notifyCallback = _NotifyCallback));

		bool hasTags = ZInitTagsStyle != ZTagsStyle.NoTags;
		if (ZInitReadOnlyAlways) {
			MOD mask = 0;
			if (ZInitImages || hasTags) mask |= MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT;
			Call(SCI_SETMODEVENTMASK, (int)mask);
		}
		_InitDocument();
		Call(SCI_SETSCROLLWIDTHTRACKING, 1);
		Call(SCI_SETSCROLLWIDTH, 1); //SHOULDDO: later make narrower when need, eg when folded long lines (alas there is no direct notification). Maybe use timer.
		if (!ZInitUseDefaultContextMenu) Call(SCI_USEPOPUP);
		Call(SCI_SETCARETWIDTH, Dpi.Scale(2, _dpi));

		//Need to set selection colors or layer, because the default inactive selection color is darker than active.
		//	It is 0x3F808080, but it seems alpha is ignored if SC_LAYER_BASE (default).
		Call(SCI_SETSELECTIONLAYER, SC_LAYER_UNDER_TEXT);
		Call(SCI_SETELEMENTCOLOUR, SC_ELEMENT_SELECTION_BACK, unchecked((int)0xA0A0A0A0)); //use alpha to mix with indicators
		Call(SCI_SETELEMENTCOLOUR, SC_ELEMENT_SELECTION_INACTIVE_BACK, 0x60A0A0A0);

		if (ZInitWrapVisuals) {
			Call(SCI_SETWRAPVISUALFLAGS, SC_WRAPVISUALFLAG_START | SC_WRAPVISUALFLAG_END);
			Call(SCI_SETWRAPVISUALFLAGSLOCATION, SC_WRAPVISUALFLAGLOC_END_BY_TEXT);
			Call(SCI_SETWRAPINDENTMODE, SC_WRAPINDENT_INDENT);
		}
		if (ZWrapLines) {
			Call(SCI_SETWRAPMODE, SC_WRAP_WORD);
		}

		//note: cannot set styles here, because later derived class will call zStyleClearAll, which sets some special styles.

		if (ZInitImages) ZImages = new SciImages(this);
		if (hasTags) ZTags = new SciTags(this);

		if (FocusManager.GetFocusScope(this) is Window fs && FocusManager.GetFocusedElement(fs) == this && Api.GetFocus() == wParent)
			Api.SetFocus(_w);

		ZOnHandleCreated();

		if (!_text.NE()) zSetText(_text, SciSetTextFlags.NoUndoNoNotify); //after derived classes set styles etc

		Api.SetWindowLongPtr(_w, GWL.WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndproc = _WndProc));
		//WPF will subclass this window.
		//	It will respect the GWL.WNDPROC subclass, but break the SetWindowSubclass subclass, therefore call it async.
		//Dispatcher.InvokeAsync(() => {
		//	if (_w.Is0) return;
		//	Api.SetWindowSubclass(_w, _wndproc2 = _WndProc2, 1);
		//});

		return new HandleRef(this, _w.Handle);
	}

	void _InitDocument() {
		//these must be set for each document of this Scintilla window

		Call(SCI_SETCODEPAGE, Api.CP_UTF8);
		Call(SCI_SETTABWIDTH, 4);
		if (ZInitReadOnlyAlways) {
			Call(SCI_SETREADONLY, 1);
			Call(SCI_SETUNDOCOLLECTION);
		} //else if (_isReadOnly) Call(SCI_SETREADONLY, 1);
	}

	protected override void DestroyWindowCore(HandleRef hwnd) {
		WndUtil.DestroyWindow((wnd)hwnd.Handle);
		_w = default;
		_acc?.Dispose(); _acc = null;
	}

	//protected override void Dispose(bool disposing) {
	//	base.Dispose(disposing); //then follows DestroyWindowCore, probably base calls it
	//}

	nint _WndProc(wnd w, int msg, nint wp, nint lp) {
		//if (Name == "Recipe_text") WndUtil.PrintMsg(w, msg, wp, lp);
		//if(Name == "Recipe_text") WndUtil.PrintMsg(_w, msg, wp, lp, Api.WM_TIMER, Api.WM_MOUSEMOVE, Api.WM_SETCURSOR, Api.WM_NCHITTEST, Api.WM_PAINT, Api.WM_IME_SETCONTEXT, Api.WM_IME_NOTIFY);

		MButtons button = msg switch { Api.WM_LBUTTONDOWN => MButtons.Left, Api.WM_RBUTTONDOWN => MButtons.Right, Api.WM_MBUTTONDOWN => MButtons.Middle, _ => 0 };
		if (button != 0 && Api.GetFocus() != _w) {
			bool setFocus = true;
			if (msg == Api.WM_LBUTTONDOWN) ZTags?.OnLButtonDownWhenNotFocused_(wp, lp, ref setFocus); //Tags may not want to set focus eg when a hotspot clicked
			if (setFocus && !ZNoMouseSetFocus.Has(button)) this.Focus();
		}

		switch (msg) {
		case Api.WM_SETFOCUS:
			if (!_inOnWmSetFocus) if (_OnWmSetFocus()) return 0;
			break;
		case Api.WM_KILLFOCUS:
			if (_inOnWmSetFocus) return 0;
			break;
		}

		var R = CallRetPtr(msg, wp, lp);

		return R;
	}

	protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
		if (msg == Api.WM_GETOBJECT) { //WPF steals it from _WndProc
			handled = true;
			return (_acc ??= new _Accessible(this)).WmGetobject(wParam, lParam);
		}
		return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
	}

	//nint _WndProc2(wnd w, int msg, nint wp, nint lp, nint uIdSubclass, nint dwRefData) {
	//	return Api.DefSubclassProc(w, msg, wp, lp);
	//}

	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
		if (!_w.Is0 && newDpi.PixelsPerDip != oldDpi.PixelsPerDip) {
			_dpi = newDpi.PixelsPerInchY.ToInt();
			Call(SCI_SETCARETWIDTH, Dpi.Scale(2, _dpi));
			zMarginWidthsDpiChanged_();
		}
		base.OnDpiChanged(oldDpi, newDpi);
	}

	#region problems with focus, keyboard, destroying

	//Somehow WPF does not care about native control focus, normal keyboard work, destroying, etc.
	//1. No Tab key navigation. Also does not set focus when parent tab item selected.
	//	Workaround: override TabIntoCore and call API SetFocus.
	//2. Does not set logical focus to HwndHost when its native control is really focused. Then eg does not restore real focus after using menu.
	//	Workaround: set focus on WM_LBUTTONDOWN etc. Also on WM_SETFOCUS, with some tricks.
	//3. Steals arrow keys, Tab and Enter from native control and sets focus to other controls or closes dialog.
	//	Workaround: override TranslateAcceleratorCore, pass the keys to the control and return true.
	//4. When closing parent window, does not destroy hwnhosted controls. Instead moves to a hidden parking window, and destroys later on GC if you are careful.
	//	Need to always test whether hwnhosted controls are destroyed on GC, to avoid leaked windows + many managed objects.
	//	Eg to protect wndproc delegate from GC don't add it to a thread-static array until destroyed; let it be a field of the wrapper class.
	//	Or let app dispose the HwndHost in OnClosing. But control itself cannot reliably know when to self-destroy.
	//5. When closing parent window, briefly tries to show native control, and focus if was focused.
	//	Workaround: let app dispose the HwndHost in OnClosing.
	//Never mind: after SetFocus, Keyboard.FocusedElement is null.

	bool _OnWmSetFocus() {
		//keep logical focus on HwndHost, else will not work eg restoring of real focus when closing menu.
		if (IsVisible && Focusable) { //info: !IsVisible when closing window without disposing this (WPF bug)
			var fs = FocusManager.GetFocusScope(this);
			if (fs != null && FocusManager.GetFocusedElement(fs) != this) { //focused not by WPF
				_inOnWmSetFocus = true;
				FocusManager.SetFocusedElement(fs, this); //in some cases would work better than this.Focus()
				_inOnWmSetFocus = false;
				//all WPF 'Focus' functions make the main window focused. Then OnGotKeyboardFocus makes _w focused again.
				//	Wndproc receives wm_setfocus, wm_killfocus and wm_setfocus. Passes to scintilla only the last wm_setfocus.
				//	Can prevent this, eg set IsFocusable=false before, but then WPF in some cases does not restore focus after switching windows etc.
				//	To prevent this on click, Wndproc calls Focus, and scintilla does not call SetFocus (mod).
				//	Could not find a way to avoid this in other cases, never mind.
				return true;
			}
		}
		return false;
	}
	bool _inOnWmSetFocus;

	//Makes _w focused when called this.Focus() or Keyboard.Focus(this).
	protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) {
		e.Handled = true;
		Api.SetFocus(_w);
		base.OnGotKeyboardFocus(e);
	}

	//Sets focus when tabbed to this or when clicked the parent tab item. Like eg WPF TextBox.
	protected override bool TabIntoCore(TraversalRequest request) {
		Focus();
		return true;
		//base.TabIntoCore(request); //empty func, returns false
	}

	protected override bool TranslateAcceleratorCore(ref System.Windows.Interop.MSG msg, ModifierKeys modifiers) {
		var m = msg.message;
		var k = (KKey)msg.wParam;
		//if (m == Api.WM_KEYDOWN) print.it(m, k);
		if (m is Api.WM_KEYDOWN or Api.WM_KEYUP /*or Api.WM_SYSKEYDOWN or Api.WM_SYSKEYUP*/)
			if (!modifiers.Has(ModifierKeys.Alt)) {
				switch (k) {
				case KKey.Left or KKey.Right or KKey.Up or KKey.Down:
				case KKey.Enter when modifiers == 0 && !zIsReadonly:
				case KKey.Tab when !modifiers.Has(ModifierKeys.Control) && !zIsReadonly:
					Call(msg.message, msg.wParam, msg.lParam); //not DispatchMessage or Send
					return true;
				case KKey.Insert when modifiers == 0: return true;
				}
			}

		return base.TranslateAcceleratorCore(ref msg, modifiers);
	}

	//Without this, user cannot type eg character 'a' in HwndHost'ed control if there is button with text like "_Apply".
	protected override bool TranslateCharCore(ref System.Windows.Interop.MSG msg, ModifierKeys modifiers) {
		if (msg.message is not Api.WM_CHAR or Api.WM_DEADCHAR) return false; //WM_SYSCHAR etc if with Alt
		if (msg.hwnd != _w.Handle) return false; //WPF bug. Eg when on key down the app makes this control focused.
		if ((int)msg.wParam <= 32) return false; //eg control chars on Ctrl+key
		_w.Send(msg.message, msg.wParam, msg.lParam); //not Call or WndProc
		return true;
	}

	#endregion

	#endregion

	void _NotifyCallback(void* cbParam, ref SCNotification n) {
		var code = n.nmhdr.code;
		//if(code != NOTIF.SCN_PAINTED) print.qm2.write(code.ToString());
		switch (code) {
		case NOTIF.SCN_MODIFIED:
			if ((n.modificationType & (MOD.SC_MULTISTEPUNDOREDO | MOD.SC_LASTSTEPINUNDOREDO)) == MOD.SC_MULTISTEPUNDOREDO) return;
			_NotifyModified(n);
			if (ZDisableModifiedNotifications) return;
			break;
		case NOTIF.SCN_HOTSPOTRELEASECLICK:
			ZTags?.OnLinkClick_(n.position, 0 != (n.modifiers & SCMOD_CTRL));
			break;
		}
		ZOnSciNotify(ref n);
	}

	//void _Print(object text)
	//{
	//	var t = Name ?? GetType().ToString();
	//	if(Name != "Status_text") print.qm2.write($"{t}: {text}");
	//}

	unsafe void _NotifyModified(in SCNotification n) {
		var mt = n.modificationType;
		//if(this.Name!= "Output_text") print.it(mt, n.position);
		if (mt.HasAny(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT)) {
			_text = null;
			_posState = default;
			_aPos.Clear();

			bool ins = mt.Has(MOD.SC_MOD_INSERTTEXT);
			ZImages?.OnTextChanged_(ins, n);
			ZTags?.OnTextChanged_(ins, n);
		}
		//if(mt.Has(MOD.SC_MOD_CHANGEANNOTATION)) ChangedAnnotation?.Invoke(this, ref n);
	}

	/// <summary>
	/// Raises the <see cref="ZNotify"/> event.
	/// </summary>
	protected virtual void ZOnSciNotify(ref SCNotification n) {
		ZNotify?.Invoke(this, ref n);
		switch (n.nmhdr.code) {
		case NOTIF.SCN_MODIFIED:
			var e = ZTextChanged;
			if (e != null && n.modificationType.HasAny(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT)) e(this, EventArgs.Empty);
			break;
		}
	}

	public delegate void ZEventHandler(KScintilla c, ref SCNotification n);

	/// <summary>
	/// Occurs when any Scintilla notification is received.
	/// </summary>
	public event ZEventHandler ZNotify;

	/// <summary>
	/// Occurs when text changed.
	/// </summary>
	public event EventHandler ZTextChanged;

	/// <summary>
	/// Sends a Scintilla message to the control and returns int.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public int Call(int sciMessage, nint wParam = 0, nint lParam = 0) => (int)CallRetPtr(sciMessage, wParam, lParam);

	/// <summary>
	/// Sends a Scintilla message to the control and returns int.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public int Call(int sciMessage, nint wParam, void* lParam) => (int)CallRetPtr(sciMessage, wParam, (nint)lParam);

	/// <summary>
	/// Sends a Scintilla message to the control and returns int.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public int Call(int sciMessage, nint wParam, bool lParam) => (int)CallRetPtr(sciMessage, wParam, lParam ? 1 : 0);

	/// <summary>
	/// Sends a Scintilla message to the control and returns int.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public int Call(int sciMessage, bool wParam, nint lParam = 0) => (int)CallRetPtr(sciMessage, wParam ? 1 : 0, lParam);

	/// <summary>
	/// Sends a Scintilla message to the control and returns nint.
	/// Don't call this function from another thread.
	/// </summary>
	[DebuggerStepThrough]
	public nint CallRetPtr(int sciMessage, nint wParam = 0, nint lParam = 0) {
#if DEBUG
		if (ZDebugPrintMessages_) _DebugPrintMessage(sciMessage);
#endif

		Debug.Assert(!_w.Is0);
		//Debug.Assert(!_w.Is0 || this.DesignMode);
		//if(!IsHandleCreated) CreateHandle();
		//note: auto-creating handle is not good:
		//	1. May create parked control. Not good for performance.
		//	2. Can be dangerous, eg if passing a reusable buffer that also is used when creating handle.

		Debug_.PrintIf(process.thisThreadId != _w.ThreadId, "wrong thread");

		return Sci_Call(_sciPtr, sciMessage, wParam, lParam);
	}

#if DEBUG
	static void _DebugPrintMessage(int sciMessage) {
		if (sciMessage < SCI_START) return;
		switch (sciMessage) {
		case SCI_COUNTCODEUNITS:
		case SCI_POSITIONRELATIVECODEUNITS:
		case SCI_CANUNDO:
		case SCI_CANREDO:
		case SCI_GETREADONLY:
		case SCI_GETSELECTIONEMPTY:
			//case SCI_GETTEXTLENGTH:
			return;
		}
		if (s_debugPM == null) {
			s_debugPM = new();
			foreach (var v in typeof(Sci).GetFields()) {
				var s = v.Name;
				//print.it(v.Name);
				if (s.Starts("SCI_")) s_debugPM.Add((int)v.GetRawConstantValue(), s);
			}
		}
		if (!s_debugPM.TryGetValue(sciMessage, out var k)) {
			k = sciMessage.ToString();
		}
		print.qm2.write(k);
	}
	static Dictionary<int, string> s_debugPM;

	internal bool ZDebugPrintMessages_ { get; set; }
#endif

	#region properties

	/// <summary>
	/// Border style.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual bool ZInitBorder { get; set; }

	/// <summary>
	/// Use the default Scintilla's context menu.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual bool ZInitUseDefaultContextMenu { get; set; }

	/// <summary>
	/// This control is used just to display text, not to edit.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual bool ZInitReadOnlyAlways { get; set; }

	/// <summary>
	/// Whether to show images specified in tags like &lt;image "image file path"&gt;, including icons of non-image file types.
	/// Must be set before creating control handle.
	/// If false, <see cref="ZImages"/> property is null.
	/// </summary>
	public virtual bool ZInitImages { get; set; }

	/// <summary>
	/// See <see cref="ZInitTagsStyle"/>.
	/// </summary>
	public enum ZTagsStyle {
		/// <summary>Don't support tags. The <see cref="ZTags"/> property is null.</summary>
		NoTags,

		/// <summary>Let <see cref="zText"/>, zSetText and zAppendText parse tags when the text has prefix "&lt;&gt;".</summary>
		AutoWithPrefix,

		/// <summary>Let <see cref="zText"/>, zSetText and zAppendText parse tags always.</summary>
		AutoAlways,

		/// <summary>Tags are parsed only when calling Tags.AddText.</summary>
		User,
	}

	/// <summary>
	/// Whether and when supports tags.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual ZTagsStyle ZInitTagsStyle { get; set; }

	/// <summary>
	/// Whether to show arrows etc to make wrapped lines more visible.
	/// Must be set before creating control handle.
	/// </summary>
	public virtual bool ZInitWrapVisuals { get; set; } = true;

	/// <summary>
	/// Word-wrap.
	/// </summary>
	public virtual bool ZWrapLines {
		get => _wrapLines;
		set {
			if (value != _wrapLines) {
				_wrapLines = value;
				if (!_w.Is0) Call(SCI_SETWRAPMODE, value ? SC_WRAP_WORD : 0);
			}
		}
	}
	bool _wrapLines;

	/// <summary>
	/// Whether uses Enter key.
	/// If null (default), false if <see cref="ZInitReadOnlyAlways"/> is true.
	/// </summary>
	public bool? ZAcceptsEnter { get; set; }

	/// <summary>
	/// On SCN_MODIFIED notifications suppress <see cref="ZOnSciNotify"/>, <see cref="ZNotify"/> and <see cref="ZTextChanged"/>.
	/// Use to temporarily disable 'modified' notifications. Never use SCI_SETMODEVENTMASK, because then the control would stop working correctly.
	/// </summary>
	public bool ZDisableModifiedNotifications { get; set; }

	/// <summary>
	/// Don't set focus on mouse left/right/middle button down.
	/// </summary>
	public MButtons ZNoMouseSetFocus { get; set; }

	#endregion

	#region acc

	_Accessible _acc;

	class _Accessible : HwndHostAccessibleBase_ {
		readonly KScintilla _sci;

		internal _Accessible(KScintilla sci) : base(sci, sci.Hwnd) {
			_sci = sci;
		}

		public override ERole Role(int child) => _sci.ZAccessibleRole;

		public override string Name(int child) => _sci.ZAccessibleName;

		public override string Description(int child) => _sci.ZAccessibleDescription;

		public override string Value(int child) => _sci.ZAccessibleValue;

		public override EState State(int child) {
			var r = base.State(child);
			if (_sci.zIsReadonly) r |= EState.READONLY;
			return r;
		}
	}

	protected virtual ERole ZAccessibleRole => ERole.TEXT; //_sci.ZInitReadOnlyAlways ? ERole.STATICTEXT : ERole.TEXT;

	protected virtual string ZAccessibleName => Name;

	protected virtual string ZAccessibleDescription => null;

	protected virtual string ZAccessibleValue => ZInitReadOnlyAlways ? zText?.Limit(0xffff) : null;

	#endregion
}
