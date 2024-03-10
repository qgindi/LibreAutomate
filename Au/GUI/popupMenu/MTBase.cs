using System.Drawing;

namespace Au.Types;

/// <summary>
/// Base class of <see cref="popupMenu"/> and <see cref="toolbar"/>.
/// </summary>
/// <remarks>
/// <i>image</i> argument of "add item" functions can be:
/// - icon name, like <c>"*Pack.Icon color"</c> (you can get it from the <b>Icons</b> tool). See <see cref="ImageUtil.LoadWpfImageElement"/>.
/// - file/folder path (string) - the "show" function calls <see cref="icon.of"/> to get its icon. It also supports file type icons like <c>".txt"</c>, etc.
/// - file path with prefix <c>"imagefile:"</c> or resource path that starts with <c>"resources/"</c> or has prefix <c>"resource:"</c> - the "show" function loads <c>.png</c> or <c>.xaml</c> image file or resource.
/// - string with prefix <c>"image:"</c> - Base64 encoded image file. Can be created with the <b>Find image</b> tool.
/// - <see cref="FolderPath"/> - same as folder path string.
/// - <see cref="Image"/> - image.
/// - <see cref="icon"/> - icon. The "add item" function disposes it.
/// - <see cref="StockIcon"/> - the "show" function calls <see cref="icon.stock"/>.
/// - <c>null</c> - if <see cref="ExtractIconPathFromCode"/> <c>true</c>, the "show" function tries to extract a file path from action code; then calls <see cref="icon.of"/>. Else no image.
/// - string <c>""</c> - no image, even if <b>ExtractIconPathFromCode</b> <c>true</c>.
/// 
/// Item images should be of size 16x16 (small icon size). If high DPI, will scale images automatically, which makes them slightly blurred. To avoid scaling, can be used XAML images, but then slower.
/// 
/// Images are loaded on demand, when showing the menu or submenu etc. If fails to load, prints warning (<see cref="print.warning"/>).
/// 
/// For icon/image files use full path, unless they are in <see cref="folders.ThisAppImages"/>
/// 
/// To add an image resource in Visual Studio, use build action <b>Resource</b> for the image file.
/// </remarks>
public abstract partial class MTBase {
	private protected readonly string _name;
	private protected readonly string _sourceFile;
	private protected readonly int _sourceLine;
	private protected readonly int _threadId;
	private protected wnd _w;
	private protected int _dpi;
	(wnd tt, MTItem item, RECT rect) _tt;

	private protected MTBase() {
		_threadId = Api.GetCurrentThreadId();
	}

	private protected MTBase(string name, string f_, int l_, string m_ = null) : this() {
		if (name == null && !m_.NE())
			if (m_[0] is not ('<' or '.')) name = m_; //<Main>$, .ctor
		_name = name;
		_sourceFile = f_;
		_sourceLine = l_;
	}

	private protected virtual void _WmNccreate(wnd w) {
		_w = w;
		MouseCursor.SetArrowCursor_(); //workaround for: briefly shows "wait" cursor when entering mouse first time in process
	}

	private protected virtual void _WmNcdestroy() {
		_w = default;
		_tt = default;
		if (_stdAO != null) {
			Marshal.ReleaseComObject(_stdAO);
			_stdAO = null;
		}
	}

	/// <summary>
	/// Extract file path or script path from item action code (for example <see cref="run.it"/> or <see cref="script.run"/> argument) and use icon of that file or script.
	/// This property is applied to items added afterwards; submenus inherit it.
	/// </summary>
	/// <value>Default: <b>toolbar</b> <c>true</c>, <b>popupMenu</b> with <i>name</i> <c>true</c>, <b>popupMenu</b> without <i>name</i> <c>false</c>.</value>
	/// <remarks>
	/// Gets path from code that contains a string like <c>@"c:\windows\system32\notepad.exe"</c> or <c>@"%folders.System%\notepad.exe"</c> or URL/shell or <c>@"\folder\script.cs"</c>.
	/// Also supports code patterns like <c>folders.System + "notepad.exe"</c>, <c>folders.shell.RecycleBin</c>.
	/// 
	/// If extracts path, also in the context menu adds item <b>Find file</b> which selects the file in Explorer or <b>Open script</b> which opens the script in editor.
	/// </remarks>
	public bool ExtractIconPathFromCode { get; set; }

	/// <summary>
	/// Execute item actions asynchronously in new threads.
	/// This property is applied to items added afterwards; submenus inherit it.
	/// </summary>
	/// <value>Default: <b>toolbar</b> <c>true</c><b>, popupMenu</b> <c>false</c>.</value>
	/// <remarks>
	/// If current thread is a UI thread (has windows etc) or has triggers or hooks, and item action functions execute some long automations etc in current thread, current thread probably is hung during that time. Set this property = <c>true</c> to avoid it.
	/// </remarks>
	public bool ActionThread { get; set; }

	/// <summary>
	/// Whether to handle exceptions in item action code. If <c>false</c> (default), handles exceptions and on exception calls <see cref="print.warning"/>.
	/// This property is applied to items added afterwards; submenus inherit it.
	/// </summary>
	/// <value>Default: <b>toolbar</b> <c>false</c><b>, popupMenu</b> <c>false</c>.</value>
	public bool ActionException { get; set; }

	/// <summary>
	/// If an item has file path, show it in tooltip.
	/// This property is applied to items added afterwards; submenus inherit it.
	/// </summary>
	/// <value>Default: <b>toolbar</b> <c>false</c><b>, popupMenu</b> <c>false</c>.</value>
	public bool PathInTooltip { get; set; }

	private protected void _CopyProps(MTBase m) {
		//m.ImageCache=this.ImageCache;
		m.ActionException = ActionException;
		m.ActionThread = ActionThread;
		m.ExtractIconPathFromCode = ExtractIconPathFromCode;
		m.PathInTooltip = PathInTooltip;
	}

	private protected string _SourceLink(MTItem x, string text) => x.sourceFile == null ? null : $"<open {x.sourceFile}|{x.sourceLine}>{text}<>";

	private protected bool _IsOtherThread => _threadId != Api.GetCurrentThreadId();

	private protected void _ThreadTrap() {
		if (_threadId != Api.GetCurrentThreadId()) throw new InvalidOperationException("Wrong thread.");
	}

	/// <summary>
	/// Converts <c>x.image</c> (object containing string, <b>Image</b>, etc or <c>null</c>) to <b>Image</b>. Extracts icon path from code if need. Returns default if will extract async.
	/// </summary>
	private protected (Image image, bool dispose) _GetImage(MTItem x) {
		Image im = null; bool dontDispose = false;

		if (x.extractIconPath == 1) { //extract path always, not only when x.image==null, or we would not have path for other purposes
			x.file = icon.ExtractIconPathFromCode_(x.clicked.Method, out bool cs);
			if (x.file != null) {
				x.image ??= x.file;
				x.extractIconPath = (byte)(cs ? 4 : 2);
			} else x.extractIconPath = 3;
		}

		switch (x.image) {
		case Image g:
			im = g;
			dontDispose = true;
			break;
		case string s when s.Length > 0:
			try {
				dontDispose = true;
				bool isImage = ImageUtil.HasImageOrResourcePrefix(s);
				im = IconImageCache.Common.Get(s, _dpi, isImage, _OnException);
				if (im == null && isImage) _OnException(s, null);
			}
			catch (Exception e1) { _OnException(null, e1); }

			void _OnException(string s, Exception e) {
				print.it($"<>Failed to load image. {e?.ToStringWithoutStack().TrimEnd('.') ?? s}. {_SourceLink(x, "Edit")}");
			}
			break;
		case StockIcon si:
			im = icon.stock(si)?.ToGdipBitmap();
			break;
		}
		return (im, im != null && !dontDispose);
	}

	private protected string _GetFullTooltip(MTItem b) {
		var s = b.Tooltip;
		if (this is toolbar tb) {
			var v = b as TBItem;
			if (!(tb.DisplayText || v.textAlways)) {
				if (s.NE()) s = v.Text;
				else if (!v.Text.NE()) s = b.Text + "\n" + s;
			}
		}
		if (b.pathInTooltip) {
			var sf = b.File;
			if (!(sf.NE() || sf.Starts("::") || sf.Starts("shell:"))) s = s.NE() ? sf : s + "\n" + sf;
		}
		return s;
	}

	private protected unsafe void _SetTooltip(MTItem b, RECT r, nint lParam, int submenuDelay = -1) {
		string s = _GetFullTooltip(b);
		bool setTT = !s.NE() && b != _tt.item;
		if (!setTT && (setTT = _tt.item != null && _tt.item.rect != _tt.rect)) b = _tt.item; //update tooltip tool rect
		if (setTT) {
			_tt.rect = r;
			_tt.item = b;
			if (!_tt.tt.IsAlive) {
				_tt.tt = Api.CreateWindowEx(WSE.TOPMOST | WSE.TRANSPARENT, "tooltips_class32", null, Api.TTS_ALWAYSTIP | Api.TTS_NOPREFIX, 0, 0, 0, 0, _w);
				_tt.tt.Send(Api.TTM_ACTIVATE, 1);
				_tt.tt.Send(Api.TTM_SETMAXTIPWIDTH, 0, screen.of(_w).WorkArea.Width / 3);
			}

			if (b is PMItem) { //ensure the tooltip is above submenu in Z order
				_tt.tt.ZorderTopRaw_();
				if (submenuDelay > 0) submenuDelay += 100;
				_tt.tt.Send(0x403, 3, submenuDelay > (int)_tt.tt.Send(0x415, 3) ? submenuDelay : -1); //TTM_SETDELAYTIME,TTM_GETDELAYTIME,TTDT_INITIAL
			}

			fixed (char* ps = s) {
				var g = new Api.TTTOOLINFO { cbSize = sizeof(Api.TTTOOLINFO), hwnd = _w, uId = 1, lpszText = ps, rect = r };
				_tt.tt.Send(Api.TTM_DELTOOL, 0, &g);
				_tt.tt.Send(Api.TTM_ADDTOOL, 0, &g);
			}
		} else {
			if (b != _tt.item) _HideTooltip();
		}

		if (_tt.item != null) {
			var v = new MSG { hwnd = _w, message = Api.WM_MOUSEMOVE, lParam = lParam };
			_tt.tt.Send(Api.TTM_RELAYEVENT, 0, &v);
		}
	}

	private protected unsafe void _HideTooltip() {
		if (_tt.item != null) {
			_tt.item = null;
			var g = new Api.TTTOOLINFO { cbSize = sizeof(Api.TTTOOLINFO), hwnd = _w, uId = 1 };
			_tt.tt.Send(Api.TTM_DELTOOL, 0, &g);
		}
	}
}

/// <summary>
/// Base of <see cref="PMItem"/> etc.
/// </summary>
public abstract class MTItem {
	internal Delegate clicked;
	internal object image;
	/// <summary>1 if need to extract, 2 if already extracted (the image field is the path), 3 if failed to extract, 4 if extracted <c>"script.cs"</c></summary>
	internal byte extractIconPath; //from MTBase.ExtractIconPathFromCode
	internal bool actionThread; //from MTBase.ActionThread
	internal bool actionException; //from MTBase.ActionException
	internal bool pathInTooltip; //from MTBase.PathInTooltip
	internal int sourceLine;
	internal string sourceFile;
	internal string file;
	internal RECT rect;
	internal Image image2;

	internal bool HasImage_ => image2 != null;

	/// <summary>
	/// Item text.
	/// </summary>
	public string Text { get; set; }

	/// <summary>
	/// Item tooltip.
	/// </summary>
	public string Tooltip { get; set; }

	/// <summary>
	/// Any value. Not used by this library.
	/// </summary>
	public object Tag { get; set; }

	///
	public ColorInt TextColor { get; set; }

	/// <summary>
	/// Gets file or script path extracted from item action code (see <see cref="MTBase.ExtractIconPathFromCode"/>) or sets path as it would be extracted.
	/// </summary>
	/// <remarks>
	/// Can be used to set file or script path when it cannot be extracted from action code.
	/// When you set this property, the menu/toolbar item uses icon of the specified file, and its context menu contains <b>Find file</b> or <b>Open script</b>.
	/// </remarks>
	public string File {
		get => file;
		set {
			file = value;
			if (file == null) {
				extractIconPath = 3;
			} else {
				image ??= file;
				bool cs = file.Ends(".cs", true) && !pathname.isFullPath(file, orEnvVar: true);
				extractIconPath = (byte)(cs ? 4 : 2);
			}
		}
	}

	internal void GoToFile_() {
		if (file.NE()) return;
		if (extractIconPath == 2) run.selectInExplorer(file);
		else ScriptEditor.Open(file);
	}

	internal static (bool edit, bool go, string goText) CanEditOrGoToFile_(string sourceFile, MTItem item) {
		if (sourceFile != null) {
			if (ScriptEditor.Available) {
				if (item?.file == null) return (true, false, null);
				return (true, true, item.extractIconPath == 2 ? "Find file" : "Open script");
			} else if (item?.extractIconPath == 2) {
				return (false, true, "Find file");
			}
		}
		return default;
	}

	/// <summary>
	/// Call when adding menu/toolbar item.
	/// Sets text and tooltip (from text). Sets <b>clicked</b>, <b>image</b> and <b>sourceLine</b> fields.
	/// Sets <b>extractIconPath</b>, <b>actionThread</b> and <b>actionException</b> fields from <i>mt</i> properties.
	/// </summary>
	internal void Set_(MTBase mt, string text, Delegate click, MTImage im, int l_, string f_) {
		if (!text.NE()) {
			var mi = this as PMItem;
			bool rawText = mi?.rawText ?? false;
			int i = text.IndexOf('\0');
			if (i < 0 && !rawText) i = text.IndexOf('|');
			if (i >= 0) {
				var v = _Split(text, i);
				text = v.Item1;
				Tooltip = v.Item2;
			}
			int len = text.Lenn();
			if (len > 0 && text[^1] == '\a') {
				text = text[..^1]; //remove for menu too, because user may move items from toolbar to menu and forget to remove '\a'
				len--;
				if (this is TBItem ti) ti.textAlways = true; //note: textAlways of groups is already true
			}
			if (mi != null && !rawText && len > 1) {
				i = text.IndexOf('\t', 1);
				if (i > 0) {
					var v = _Split(text, i);
					text = v.Item1;
					mi.Hotkey = v.Item2;
				}
			}
			if (!text.NE()) Text = text;

			static (string, string) _Split(string s, int i) {
				int j = i + 1; if (s.Eq(j, ' ')) j++;
				return (i > 0 ? s[..i] : null, j < s.Length ? s[j..] : null);
			}
		}

		image = im.Value;
		if (image is icon ic) { image = ic.ToGdipBitmap(); image ??= ""; } //DestroyIcon now; don't extract from code.

		clicked = click;
		sourceLine = l_;
		sourceFile = f_;

		extractIconPath = (byte)((mt.ExtractIconPathFromCode && clicked is not (null or Action<popupMenu> or Func<popupMenu>)) ? 1 : 0);
		actionThread = mt.ActionThread;
		actionException = mt.ActionException;
		pathInTooltip = mt.PathInTooltip;
	}

	///
	public override string ToString() => Text;
}

/// <summary>
/// Used for menu/toolbar function parameters to specify an image in different ways (file path, <b>Image</b> object, etc).
/// </summary>
/// <remarks>
/// Has implicit conversions from string, <see cref="Image"/>, <see cref="icon"/>, <see cref="StockIcon"/>, <see cref="FolderPath"/>.
/// More info: <see cref="MTBase"/>.
/// </remarks>
public struct MTImage {
	readonly object _o;
	MTImage(object o) { _o = o; }

	///
	public static implicit operator MTImage(string pathEtc) => new(pathEtc);
	///
	public static implicit operator MTImage(Image image) => new(image);
	///
	public static implicit operator MTImage(icon icon) => new(icon);
	///
	public static implicit operator MTImage(StockIcon icon) => new(icon);
	///
	public static implicit operator MTImage(FolderPath path) => new((string)path);

	/// <summary>
	/// Gets the raw value stored in this variable. Can be <b>string</b>, <b>Image</b>, <b>icon</b>, <b>StockIcon</b> or <c>null</c>.
	/// </summary>
	public object Value => _o;
}
