using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using System.Drawing;
//using System.Linq;

using Au;
using Au.Types;
using static Au.AStatic;

namespace Au.Controls
{
	using static Sci;

	//Why don't use ScintillaNET:
	// 1. Delays to update for the newest Scintilla version.
	// 2. Possibly will be abandoned some day.
	// 3. Adds yet another layer of bugs, and I found some.
	// 4. I don't like some things how it is implemented. Eg aggressively uses "clamping", creates much garbage, eg new Line object for each line-related Scintilla message call.
	// 5. For me it does not make much easier because I used Scintilla in QM2 (C++) and am familiar with its API (which is well documented). When using ScintillaNET, I often search its source code just to find which function calls the API I need. Now I can simply convert much of QM2 code to C#.
	// 6. I use modified Scintilla. Would need to synchronize some modifications with ScintillaNET. For example I use a callback function instead of WM_NOTIFY/WM_REFLECT.

	/// <summary>
	/// This .NET control wraps native Scintilla control.
	/// This is not a universal Scintilla wrapper class. Designed just for purposes of this library and related software.
	/// Responsible for creating and initializing the control. Also used to set/change control properties.
	/// The Z property returns a SciText object that can be used to work with text, code styling etc.
	/// </summary>
	public partial class AuScintilla : Control
	{
		static SciFnDirect s_fnDirect;
		SciFnDirect _fnDirect;
		LPARAM _ptrDirect;
		Sci_NotifyCallback _notifyCallback;

		[Browsable(false)]
		public SciImages ZImages { get; private set; }

		[Browsable(false)]
		public SciTags ZTags { get; private set; }

		/// <summary>
		/// Gets the SciText object that contains most Scintilla-related functions.
		/// </summary>
		public readonly SciText Z;

		///
		public AuScintilla()
		{
			Z = new SciText(this);

			base.SetStyle(ControlStyles.CacheText, true);

			//this is like TextBoxBase does
			base.SetStyle(ControlStyles.StandardClick |
					 ControlStyles.StandardDoubleClick |
					 ControlStyles.UseTextForAccessibility |
					 ControlStyles.UserPaint,
					 false);

			this.Size = new Size(200, 100);
			this.AccessibleRole = AccessibleRole.Text;
		}

		protected override CreateParams CreateParams {
			get {
				if(s_fnDirect == null) {
					var path = ZDllPath;
					if(!Api.GetDelegate(out s_fnDirect, path, "Scintilla_DirectFunction")) throw new AuException(0, $"*load '{path}'");
				}
				_fnDirect = s_fnDirect;

				var cp = base.CreateParams;
				cp.ClassName = "Scintilla";

				//this is like TextBoxBase does
				cp.ExStyle &= ~(int)WS_EX.CLIENTEDGE;
				cp.Style &= ~(int)WS.BORDER;
				switch(ZInitBorderStyle) {
				case BorderStyle.Fixed3D: cp.ExStyle |= (int)WS_EX.CLIENTEDGE; break;
				case BorderStyle.FixedSingle: cp.Style |= (int)WS.BORDER; break;
				}

				return cp;
			}
		}

		protected override unsafe void OnHandleCreated(EventArgs e)
		{
			var hwnd = (AWnd)Handle;
			_ptrDirect = hwnd.Send(SCI_GETDIRECTPOINTER);
			Call(SCI_SETNOTIFYCALLBACK, 0, Marshal.GetFunctionPointerForDelegate(_notifyCallback = _NotifyCallback));

			bool hasImages = ZInitImagesStyle != ZImagesStyle.NoImages;
			bool hasTags = ZInitTagsStyle != ZTagsStyle.NoTags;
			if(ZInitReadOnlyAlways) {
				MOD mask = 0;
				if(hasImages || hasTags) mask |= MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT;
				Call(SCI_SETMODEVENTMASK, (int)mask);
			}
			_InitDocument();
			Call(SCI_SETSCROLLWIDTHTRACKING, 1);
			Call(SCI_SETSCROLLWIDTH, 100);
			if(!ZInitUseDefaultContextMenu) Call(SCI_USEPOPUP);
			int cw = SystemInformation.CaretWidth; if(cw > 1) Call(SCI_SETCARETWIDTH, cw);
			if(ZInitWrapVisuals) {
				Call(SCI_SETWRAPVISUALFLAGS, SC_WRAPVISUALFLAG_START | SC_WRAPVISUALFLAG_END);
				Call(SCI_SETWRAPVISUALFLAGSLOCATION, SC_WRAPVISUALFLAGLOC_END_BY_TEXT);
				Call(SCI_SETWRAPINDENTMODE, SC_WRAPINDENT_INDENT);
			}
			if(ZWrapLines) {
				Call(SCI_SETWRAPMODE, SC_WRAP_WORD);
			}

			//note: cannot set styles here, because later inherited class will call StyleClearAll, which sets some special styles.

			if(hasImages) ZImages = new SciImages(this, ZInitImagesStyle == ZImagesStyle.AnyString);
			if(hasTags) ZTags = new SciTags(this);

			if(AccessibleName == null) AccessibleName = Name;

			if(this.AllowDrop) Api.RevokeDragDrop(hwnd);

			base.OnHandleCreated(e);
		}

		void _InitDocument()
		{
			//these must be set for each document of this Scintilla window

			Call(SCI_SETCODEPAGE, Api.CP_UTF8);
			Call(SCI_SETTABWIDTH, 4);
			if(ZInitReadOnlyAlways) {
				Call(SCI_SETREADONLY, 1);
				Call(SCI_SETUNDOCOLLECTION);
				Call(SCI_SETLEXER, (int)LexLanguage.SCLEX_NULL); //don't need style-needed notification, we'll set styles for whole text
			}
		}

		protected override void WndProc(ref Message m)
		{
			//if(this.Parent?.Name == "Output") AWnd.More.PrintMsg(m, Api.WM_TIMER, Api.WM_MOUSEMOVE, Api.WM_SETCURSOR, Api.WM_NCHITTEST, Api.WM_PAINT, Api.WM_IME_SETCONTEXT, Api.WM_IME_NOTIFY);
			//if(Focused) AWnd.More.PrintMsg(m, Api.WM_TIMER, Api.WM_MOUSEMOVE, Api.WM_SETCURSOR, Api.WM_NCHITTEST, Api.WM_PAINT, Api.WM_IME_SETCONTEXT, Api.WM_IME_NOTIFY);

			var hwnd = (AWnd)m.HWnd;
			//LPARAM wParam = m.WParam, lParam = m.LParam;

			switch(m.Msg) {
			case Api.WM_SETCURSOR:
			//case Api.WM_SETFOCUS: //no, it prevents changing default button etc. Don't remember why it was added here.
			//case Api.WM_KILLFOCUS:
			case Api.WM_LBUTTONUP:
			case Api.WM_LBUTTONDBLCLK:
				_DefWndProc(ref m);
				return;

			case Api.WM_LBUTTONDOWN:
				if(Api.GetFocus() != hwnd) {
					bool setFocus = true;
					ZTags?.LibOnLButtonDownWhenNotFocused(ref m, ref setFocus); //Tags may not want to set focus eg when a hotspot clicked
					if(setFocus && !ZNoMouseLeftSetFocus) Api.SetFocus(hwnd);
				}
				_DefWndProc(ref m);
				return;
			case Api.WM_RBUTTONDOWN:
				if(!ZNoMouseRightSetFocus) Api.SetFocus(hwnd);
				_DefWndProc(ref m);
				return;
			}

			base.WndProc(ref m);

			switch(m.Msg) {
			case Api.WM_CREATE: //after inherited classes set styles etc
				if(!Empty(_text)) Z.SetText(_text, SciSetTextFlags.NoUndoNoNotify);
				break;
			}
		}

		/// <summary>
		/// Don't set focus on mouse left button down.
		/// </summary>
		[DefaultValue(false)]
		public bool ZNoMouseLeftSetFocus { get; set; }

		/// <summary>
		/// Don't set focus on mouse right button down.
		/// </summary>
		[DefaultValue(false)]
		public bool ZNoMouseRightSetFocus { get; set; }

		void _DefWndProc(ref Message m)
		{
			m.Result = CallRetPtr(m.Msg, m.WParam, m.LParam);
			//This is faster than base.DefWndProc, which calls CallWindowProc.
			//Howewer cannot override DefWndProc with this. Then crashes.
		}

		unsafe void _NotifyCallback(void* cbParam, ref SCNotification n)
		{
			var code = n.nmhdr.code;
			//if(code != NOTIF.SCN_PAINTED) AOutput.QM2.Write(code.ToString());
			switch(code) {
			case NOTIF.SCN_MODIFIED:
				_NotifyModified(ref n);
				if(ZDisableModifiedNotifications) return;
				break;
			case NOTIF.SCN_HOTSPOTRELEASECLICK:
				ZTags?.LibOnLinkClick(n.position, 0 != (n.modifiers & SCMOD_CTRL));
				break;
			}
			ZOnSciNotify(ref n);
		}

		//void _Print(object text)
		//{
		//	var t = Name ?? GetType().ToString();
		//	if(Name != "Status_text") AOutput.QM2.Write($"{t}: {text}");
		//}

		unsafe void _NotifyModified(ref SCNotification n)
		{
			_text = null;
			var code = n.modificationType;
			if((code & (MOD.SC_MULTISTEPUNDOREDO | MOD.SC_LASTSTEPINUNDOREDO)) == MOD.SC_MULTISTEPUNDOREDO) return;
			//Print(code, n.position);
			if(0 != (code & (MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT))) {
				bool ins = 0 != (code & MOD.SC_MOD_INSERTTEXT);
				ZImages?.LibOnTextChanged(ins, ref n);
				ZTags?.LibOnTextChanged(ins, ref n);
			}
			//if(0!=(code& MOD.SC_MOD_CHANGEANNOTATION)) ChangedAnnotation?.Invoke(this, ref n);
		}

		/// <summary>
		/// Raises the <see cref="ZNotify"/> event.
		/// </summary>
		/// <param name="n"></param>
		protected virtual void ZOnSciNotify(ref SCNotification n) {
			ZNotify?.Invoke(this, ref n);
			var e = ZTextChanged;
			if(e != null && n.nmhdr.code == NOTIF.SCN_MODIFIED && 0 != (n.modificationType & (MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT))) e(this, EventArgs.Empty);
		}

		public delegate void ZEventHandler(AuScintilla c, ref SCNotification n);

		/// <summary>
		/// Occurs when any Scintilla notification is received.
		/// </summary>
		public event ZEventHandler ZNotify;

		/// <summary>
		/// Occurs when text changed.
		/// </summary>
		public event EventHandler ZTextChanged;

		/// <summary>
		/// On SCN_MODIFIED notifications suppress <see cref="ZOnSciNotify"/>, <see cref="ZNotify"/> and <see cref="ZTextChanged"/>.
		/// Use to temporarily disable 'modified' notifications. Never use SCI_SETMODEVENTMASK, because then the control would stop working correctly.
		/// </summary>
		[DefaultValue(false)]
		public bool ZDisableModifiedNotifications { get; set; }

		/// <summary>
		/// Sends a Scintilla message to the control and returns int.
		/// Don't call this function from another thread.
		/// </summary>
		[DebuggerStepThrough]
		public int Call(int sciMessage, LPARAM wParam = default, LPARAM lParam = default) => (int)CallRetPtr(sciMessage, wParam, lParam);

		/// <summary>
		/// Sends a Scintilla message to the control and returns LPARAM.
		/// Don't call this function from another thread.
		/// </summary>
		[DebuggerStepThrough]
		public LPARAM CallRetPtr(int sciMessage, LPARAM wParam, LPARAM lParam)
		{
#if DEBUG
			if(ZDebugPrintMessages) _DebugPrintMessage(sciMessage);
#endif
			if(!IsHandleCreated) {
				Debug.Assert(!Visible);
				CreateHandle(); //because did not create handle if initially Visible is false
			}
			//Debug.Assert(IsHandleCreated || this.DesignMode);
			//if(!IsHandleCreated) CreateHandle();
			//note: auto-creating handle is not good:
			//	1. May create parked control. Not good for performance.
			//	2. Can be dangerous, eg if passing a reusable buffer that also is used by OnHandleCreated.

			return _fnDirect(_ptrDirect, sciMessage, wParam, lParam);
		}

#if DEBUG
		void _DebugPrintMessage(int sciMessage)
		{
			if(sciMessage < Sci.SCI_START) return;
			switch(sciMessage) {
			case SCI_COUNTCODEUNITS:
			case SCI_POSITIONRELATIVECODEUNITS:
			case SCI_CANUNDO:
			case SCI_CANREDO:
			case SCI_GETREADONLY:
			case SCI_GETSELECTIONEMPTY:
			//case SCI_GETTEXTLENGTH:
				return;
			}
			if(s_debugPM == null) {
				s_debugPM = new Dictionary<int, string>();
				foreach(var v in typeof(Sci).GetFields()) {
					var s = v.Name;
					//Print(v.Name);
					if(s.Starts("SCI_")) s_debugPM.Add((int)v.GetRawConstantValue(), s);
				}
			}
			if(!s_debugPM.TryGetValue(sciMessage, out var k)) {
				k = sciMessage.ToString();
			}
			AOutput.QM2.Write(k);
		}
		static Dictionary<int, string> s_debugPM;

		[Browsable(false)]
		public bool ZDebugPrintMessages { get; set; }
#endif

		/// <summary>
		/// Scintilla dll path.
		/// Default is <c>AFolders.ThisAppBS + (AVersion.Is64BitProcess ? "64" : "32") + @"\SciLexer.dll"</c>. If you want to change it, call this before creating first control.
		/// </summary>
		public static string ZDllPath {
			get {
				if(s_dllPath == null) {
					s_dllPath = AFolders.ThisAppBS + (AVersion.Is64BitProcess ? "64" : "32") + @"\SciLexer.dll";
					if(!AFile.ExistsAsFile(s_dllPath, true)) { //in designer?
						s_dllPath = @"Q:\app\Au\_\" + (AVersion.Is64BitProcess ? "64" : "32") + @"\SciLexer.dll";
					}
				}
				return s_dllPath;
			}
			set {
				s_dllPath = APath.Normalize(value);
			}
		}
		static string s_dllPath;

		/// <summary>
		/// Border style.
		/// Must be set before creating control handle.
		/// </summary>
		[DefaultValue(BorderStyle.None)]
		public virtual BorderStyle ZInitBorderStyle { get; set; }

		/// <summary>
		/// Use the default Scintilla's context menu.
		/// Must be set before creating control handle.
		/// </summary>
		[DefaultValue(false)]
		public virtual bool ZInitUseDefaultContextMenu { get; set; }

		/// <summary>
		/// This control is used just to display text, not to edit.
		/// Must be set before creating control handle.
		/// </summary>
		[DefaultValue(false)]
		public virtual bool ZInitReadOnlyAlways { get; set; }

		/// <summary>
		/// See <see cref="ZInitImagesStyle"/>.
		/// </summary>
		public enum ZImagesStyle
		{
			/// <summary>Don't show images. The <see cref="ZImages"/> property is null.</summary>
			NoImages,

			/// <summary>Display only images specified in tags like &lt;image "image file path"&gt;, including icons of non-image file types.</summary>
			ImageTag,

			/// <summary>Display images specified in any string like "image file path", and only of image file types. Then limits image height to 10 lines.</summary>
			AnyString
		}

		/// <summary>
		/// Whether and how to show images.
		/// Must be set before creating control handle.
		/// </summary>
		[DefaultValue(ZImagesStyle.NoImages)]
		public virtual ZImagesStyle ZInitImagesStyle { get; set; }

		/// <summary>
		/// See <see cref="ZInitImagesStyle"/>.
		/// </summary>
		public enum ZTagsStyle
		{
			/// <summary>Don't support tags. The <see cref="ZTags"/> property is null.</summary>
			NoTags,

			/// <summary>Let <see cref="Text"/>, Z.SetText and Z.AppendText parse tags when the text has prefix "&lt;&gt;".</summary>
			AutoWithPrefix,

			/// <summary>Let <see cref="Text"/>, Z.SetText and Z.AppendText parse tags always.</summary>
			AutoAlways,

			/// <summary>Tags are parsed only when calling Tags.AddText.</summary>
			User,
		}

		/// <summary>
		/// Whether and when supports tags.
		/// Must be set before creating control handle.
		/// </summary>
		[DefaultValue(ZTagsStyle.NoTags)]
		public virtual ZTagsStyle ZInitTagsStyle { get; set; }

		/// <summary>
		/// Whether to show arrows etc to make wrapped lines more visible.
		/// Must be set before creating control handle.
		/// </summary>
		[DefaultValue(true)]
		public virtual bool ZInitWrapVisuals { get; set; } = true;

		/// <summary>
		/// Word-wrap.
		/// </summary>
		[DefaultValue(false)]
		public virtual bool ZWrapLines {
			get => _wrapLines;
			set {
				if(value != _wrapLines) {
					_wrapLines = value;
					if(IsHandleCreated) Call(SCI_SETWRAPMODE, value ? SC_WRAP_WORD : SC_WRAP_NONE);
				}
			}
		}
		bool _wrapLines;

		/// <summary>
		/// Like <see cref="TextBox.AcceptsReturn"/>.
		/// If null (default), does not accept if <see cref="ZInitReadOnlyAlways"/> is true.
		/// </summary>
		[DefaultValue(null)]
		public bool? ZAcceptsReturn { get; set; }

		//Enables tabstopping when ZInitReadOnlyAlways (scintilla would eat Tab). Implements AcceptsReturn.
		protected override bool IsInputKey(Keys keyData)
		{
			switch(keyData & Keys.KeyCode) {
			case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
			case Keys.Enter when ZAcceptsReturn != null: return ZAcceptsReturn.GetValueOrDefault();
			}
			return !ZInitReadOnlyAlways;
			//don't call base. It sends WM_GETDLGCODE, and scintilla always returns DLGC_WANTALLKEYS.
		}

		/// <summary>
		/// The 'get' function calls <see cref="SciText.AllText"/> (Z.AllText) when need.
		/// The 'set' function calls <see cref="SciText.SetText"/> (Z.SetText) when need. Uses default parameters (with undo and notifications, unless ZInitReadOnlyAlways).
		/// Unlike the above methods, this property can be used before creating handle.
		/// </summary>
		public override string Text {
			get {
				if(_text == null && IsHandleCreated) _text = Z.AllText(); //_NotifyModified sets _text=null
				return _text;
			}
			set {
				if(IsHandleCreated) Z.SetText(value); //_NotifyModified sets _text=null. Control text can be != value, eg when tags parsed.
				else _text = value; //will set control text on WM_CREATE
			}
		}
		string _text;

		protected override AccessibleObject CreateAccessibilityInstance()
		{
			return _acc ??= new _Acc(this);
		}
		_Acc _acc;

		class _Acc : ControlAccessibleObject
		{
			AuScintilla _control;

			public _Acc(AuScintilla ownerControl) : base(ownerControl) => _control = ownerControl;

			public override AccessibleStates State => base.State | (_control.Z.IsReadonly ? AccessibleStates.ReadOnly : 0);
		}
	}
}