namespace Au.More;

/// <summary>
/// Registers a hotkey using API <msdn>RegisterHotKey</msdn>. Unregisters when disposing.
/// </summary>
/// <remarks>
/// Can be used as a lightweight alternative to hotkey triggers.
/// 
/// The variable must be disposed, either explicitly (call <b>Dispose</b> or <b>Unregister</b>) or with <c>using</c>.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// using System.Windows;
/// using System.Windows.Interop;
/// 
/// new DialogClass().ShowDialog();
/// 
/// class DialogClass : Window {
/// 	RegisteredHotkey _hk1, _hk2;
/// 	
/// 	public DialogClass() {
/// 		Title = "Hotkeys";
/// 		var b = new wpfBuilder(this).WinSize(250);
/// 		b.R.AddOkCancel();
/// 		b.End();
/// 	}
/// 
/// 	protected override void OnSourceInitialized(EventArgs e) {
/// 		base.OnSourceInitialized(e);
/// 		var hs = PresentationSource.FromVisual(this) as HwndSource;
/// 		hs.AddHook(_WndProc);
/// 		bool r1 = _hk1.Register(1, "Ctrl+Alt+F10", this);
/// 		bool r2 = _hk2.Register(2, (KMod.Ctrl | KMod.Shift, KKey.D), this); //Ctrl+Shift+D
/// 		print.it(r1, r2);
/// 	}
/// 
/// 	protected override void OnClosed(EventArgs e) {
/// 		base.OnClosed(e);
/// 		_hk1.Unregister();
/// 		_hk2.Unregister();
/// 	}
/// 
/// 	IntPtr _WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
/// 		if (msg == RegisteredHotkey.WM_HOTKEY) print.it(wParam);
/// 
/// 		return default;
/// 	}
/// }
/// ]]></code>
/// </example>
/// <seealso cref="keys.waitForHotkey(Seconds, KHotkey, bool)"/>
public struct RegisteredHotkey : IDisposable {
	wnd _w;
	int? _id;
	
	///// <summary>The hotkey.</summary>
	//public KHotkey Hotkey { get; private set; }
	
	/// <summary>
	/// Registers a hotkey using API <msdn>RegisterHotKey</msdn>.
	/// </summary>
	/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
	/// <param name="id">Hotkey id. Must be 0 to 0xBFFF or value returned by API <msdn>GlobalAddAtom</msdn>. It will be <i>wParam</i> of the <msdn>WM_HOTKEY</msdn> message.</param>
	/// <param name="hotkey">Hotkey. Can be: string like <c>"Ctrl+Shift+Alt+Win+K"</c>, tuple <b>(KMod, KKey)</b>, enum <b>KKey</b>, enum <b>Keys</b>, struct <b>KHotkey</b>.</param>
	/// <param name="window">Window/form that will receive the <msdn>WM_HOTKEY</msdn> message. Must be of this thread. If default, the message must be retrieved in the message loop of this thread.</param>
	///	<param name="noRepeat">Add flag <b>MOD_NOREPEAT</b>.</param>
	/// <exception cref="ArgumentException">Error in hotkey string.</exception>
	/// <exception cref="InvalidOperationException">This variable already registered a hotkey.</exception>
	/// <remarks>
	/// Fails if the hotkey is currently registered by this or another application or used by Windows. Also if <c>F12</c>.
	/// <note>Most single-key and <c>Shift+key</c> hotkeys don't work when the active window has higher UAC integrity level (eg admin) than this process. Media keys may work.</note>
	/// A single variable cannot register multiple hotkeys simultaneously. Use multiple variables, for example array.
	/// </remarks>
	/// <seealso cref="keys.waitForHotkey"/>
	public bool Register(int id, [ParamString(PSFormat.Hotkey)] KHotkey hotkey, AnyWnd window = default, bool noRepeat = false) {
		if (_id != null) throw new InvalidOperationException("This variable already registered a hotkey. Use multiple variables or call Unregister.");
		var w = window.Hwnd;
		var (mod, key) = Normalize_(hotkey);
		if (noRepeat) mod |= Api.MOD_NOREPEAT;
		if (!Api.RegisterHotKey(w, id, mod, key)) return false;
		_w = w; _id = id;
		//Hotkey = hotkey;
		return true;
	}
	
	internal static (uint mod, KKey key) Normalize_(KHotkey hotkey) {
		var (mod, key) = hotkey;
		if (key == KKey.Pause && mod.Has(KMod.Ctrl)) key = KKey.Break;
		//if(key == KKey.NumPad5 && mod.Has(KMod.Shift)) key = KKey.Clear; //Shift+numpad don't work
		return (Math2.SwapBits((uint)mod, 0, 2, 1), key);
	}
	
	/// <summary>
	/// Unregisters the hotkey.
	/// </summary>
	/// <remarks>
	/// Called implicitly when disposing this variable.
	/// Must be called from the same thread as when registering, and the window must be still alive.
	/// If fails, calls <see cref="print.warning"/>.
	/// </remarks>
	public void Unregister() {
		if (_id != null) {
			if (!Api.UnregisterHotKey(_w, _id.Value)) {
				var es = lastError.message;
				print.warning($"Failed to unregister hotkey, id={_id.Value}. {es}");
				return;
			}
			_id = null;
			_w = default;
			//Hotkey = default;
		}
	}
	
	/// <summary>
	/// Calls <see cref="Unregister"/>.
	/// </summary>
	public void Dispose() => Unregister();
	
	//~RegisteredHotkey() => Unregister(); //makes no sense. Called from wrong thread and when the window is already destroyed.
	
	/// <summary>
	/// This message is posted to the window or to the thread's message loop.
	/// More info: <msdn>WM_HOTKEY</msdn>.
	/// </summary>
	public const int WM_HOTKEY = Api.WM_HOTKEY;
}
