namespace Au {
	/// <summary>
	/// Ambient options for some functions of this library.
	/// </summary>
	/// <remarks>
	/// Some frequently used functions of this library have some options (settings). For example <see cref="keys.send"/> allows to change speed, text sending method, etc. Passing options as parameters in each call usually isn't what you want to do in automation scripts. Instead use this class. See examples.
	/// 
	/// To store these options, internally is used <see cref="AsyncLocal{T}"/>. It means that a new task or thread inherits a copy of options of the caller. It can't modify options of the caller or other tasks/threads.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// opt.key.KeySpeed = 50;
	/// ]]></code>
	/// Set options for trigger actions.
	/// <code><![CDATA[
	/// Triggers.Options.BeforeAction = o => { opt.key.KeySpeed = 50; };
	/// ]]></code>
	/// </example>
	public static class opt {
		/// <summary>
		/// Options for mouse functions (class <see cref="Au.mouse"/> and functions that use it).
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// opt.mouse.ClickSpeed = 100;
		/// mouse.click();
		/// ]]></code>
		/// </example>
		public static OMouse mouse => OMouse.Ambient_;
		
		/// <summary>
		/// Options for keyboard and clipboard functions (classes <see cref="keys"/>, <see cref="clipboard"/> and functions that use them).
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.KeySpeed = 100;
		/// keys.send("Right*10 Ctrl+A");
		/// ]]></code>
		/// Use a <b>keys</b> instance.
		/// <code><![CDATA[
		/// var k = new keys(opt.key); //create new keys instance and copy options from opt.key to it
		/// k.Options.KeySpeed = 100; //changes option of k but not of opt.key
		/// k.Add("Right*10 Ctrl+A").SendNow(); //uses options of k
		/// ]]></code>
		/// Set options for trigger actions.
		/// <code><![CDATA[
		/// Triggers.Options.BeforeAction = o => { opt.key.KeySpeed = 50; };
		/// ]]></code>
		/// </example>
		public static OKey key => OKey.Ambient_;
		
		/// <summary>
		/// Options for showing run-time warnings and other info that can be useful to find problems in code at run time.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// opt.warnings.Verbose = false;
		/// print.warning("Example");
		/// print.warning("Example");
		/// ]]></code>
		/// </example>
		public static OWarnings warnings => OWarnings.Ambient_;
		
		/// <summary>
		/// Obsolete. Use <see cref="Seconds"/> instead.
		/// For backward compatibility, wait functions still use <c>opt.wait.DoEvents</c> if <b>Seconds.DoEvents</b> not specified.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static OWait wait => OWait.Ambient_;
		
		/// <summary>
		/// Obsolete.
		/// </summary>
		[Obsolete("Use opt instead. To set options for triggers can be used code like this: Triggers.Options.BeforeAction = o => { opt.key.TextSpeed = 5; opt.mouse.ClickSpeed = 30; };"), EditorBrowsable(EditorBrowsableState.Never)]
		public static class init {
			/// <summary>
			/// Obsolete. Same as <see cref="opt.mouse"/>.
			/// </summary>
			public static OMouse mouse => opt.mouse;
			
			/// <summary>
			/// Obsolete. Same as <see cref="opt.key"/>.
			/// </summary>
			public static OKey key => opt.key;
			
			/// <summary>
			/// Obsolete. Same as <see cref="opt.warnings"/>.
			/// </summary>
			public static OWarnings warnings => opt.warnings;
		}
		
		/// <summary>
		/// Creates temporary scopes for options.
		/// Example: <c>using(opt.scope.key()) { opt.key.KeySpeed=5; ... }</c>.
		/// </summary>
		public static class scope {
			/// <summary>
			/// Creates temporary scope for <see cref="opt.mouse"/> options. See example.
			/// </summary>
			/// <param name="inherit">If <c>true</c> (default), inherit current options. If <c>false</c>, uses default options.</param>
			/// <example>
			/// <code><![CDATA[
			/// print.it(opt.mouse.ClickSpeed);
			/// using(opt.scope.mouse()) {
			/// 	opt.mouse.ClickSpeed = 100;
			/// 	print.it(opt.mouse.ClickSpeed);
			/// } //here restored automatically
			/// print.it(opt.mouse.ClickSpeed);
			/// ]]></code>
			/// </example>
			public static UsingEndAction mouse(bool inherit = true) {
				var old = OMouse.Scope_(inherit);
				return new UsingEndAction(() => OMouse.Ambient_ = old);
			}
			
			/// <summary>
			/// Creates temporary scope for <see cref="opt.key"/> options. See example.
			/// </summary>
			/// <param name="inherit">If <c>true</c> (default), inherit current options. If <c>false</c>, uses default options.</param>
			/// <example>
			/// <code><![CDATA[
			/// print.it(opt.key.KeySpeed);
			/// using(opt.scope.key()) {
			/// 	opt.key.KeySpeed = 5;
			/// 	print.it(opt.key.KeySpeed);
			/// } //here restored automatically
			/// print.it(opt.key.KeySpeed);
			/// ]]></code>
			/// </example>
			public static UsingEndAction key(bool inherit = true) {
				var old = OKey.Scope_(inherit);
				return new UsingEndAction(() => OKey.Ambient_ = old);
			}
			
			/// <summary>
			/// Creates temporary scope for <see cref="opt.warnings"/> options. See example.
			/// </summary>
			/// <param name="inherit">If <c>true</c> (default), inherit current options. If <c>false</c>, uses default options.</param>
			/// <example>
			/// <code><![CDATA[
			/// opt.warnings.Verbose = false;
			/// print.it(opt.warnings.Verbose, opt.warnings.IsDisabled("Test*"));
			/// using(opt.scope.warnings()) {
			/// 	opt.warnings.Verbose = true;
			/// 	opt.warnings.Disable("Test*");
			/// 	print.it(opt.warnings.Verbose, opt.warnings.IsDisabled("Test*"));
			/// } //here restored automatically
			/// print.it(opt.warnings.Verbose, opt.warnings.IsDisabled("Test*"));
			/// ]]></code>
			/// </example>
			public static UsingEndAction warnings(bool inherit = true) {
				var old = OWarnings.Scope_(inherit);
				return new UsingEndAction(() => OWarnings.Ambient_ = old);
			}
			
			///
			[EditorBrowsable(EditorBrowsableState.Never)] //obsolete
			public static UsingEndAction wait(bool inherit = true) {
				var old = OWait.Scope_(inherit);
				return new UsingEndAction(() => OWait.Ambient_ = old);
			}
			
			/// <summary>
			/// Creates temporary scope for all options. See example.
			/// </summary>
			/// <param name="inherit">If <c>true</c> (default), inherit current options. If <c>false</c>, uses default options.</param>
			/// <example>
			/// <code><![CDATA[
			/// print.it(opt.key.KeySpeed, opt.mouse.ClickSpeed);
			/// using(opt.scope.all()) {
			/// 	opt.key.KeySpeed = 5;
			/// 	opt.mouse.ClickSpeed = 50;
			/// 	print.it(opt.key.KeySpeed, opt.mouse.ClickSpeed);
			/// } //here restored automatically
			/// print.it(opt.key.KeySpeed, opt.mouse.ClickSpeed);
			/// ]]></code>
			/// </example>
			public static UsingEndAction all(bool inherit = true/*, int? speed = null*/) {
				var o1 = OMouse.Scope_(inherit);
				var o2 = OKey.Scope_(inherit);
				var o3 = OWarnings.Scope_(inherit);
				var o4 = OWait.Scope_(inherit);
				return new UsingEndAction(() => {
					OMouse.Ambient_ = o1;
					OKey.Ambient_ = o2;
					OWarnings.Ambient_ = o3;
					OWait.Ambient_ = o4;
				});
			}
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// Options for functions of class <see cref="mouse"/>.
	/// </summary>
	/// <remarks>
	/// Total <c>Click(x, y)</c> time is: mouse move + <see cref="MoveSleepFinally"/> + button down + <see cref="ClickSpeed"/> + button up + <see cref="ClickSpeed"/> + <see cref="ClickSleepFinally"/>.
	/// </remarks>
	/// <seealso cref="opt.mouse"/>
	/// <example>
	/// <code><![CDATA[
	/// opt.mouse.MoveSpeed = 30;
	/// ]]></code>
	/// </example>
	public class OMouse {
		int _threadId;
		static AsyncLocal<OMouse> s_ambient = new();
		
		internal static OMouse Ambient_ {
			get => s_ambient.Value ??= new() { _threadId = Environment.CurrentManagedThreadId };
			set { Debug.Assert(value?._threadId != 0); s_ambient.Value = value; } //used only to restore scope
		}
		
		internal static OMouse Scope_(bool inherit) {
			var old = s_ambient.Value;
			s_ambient.Value = (old != null && inherit) ? new(old) { _threadId = Environment.CurrentManagedThreadId } : null; //lazy
			return old;
		}
		
		OMouse _ThisOrClone() {
			var r = this;
			if (_threadId != 0 && Environment.CurrentManagedThreadId is var tid && tid != _threadId) s_ambient.Value = r = new(this) { _threadId = tid };
			return r;
		}
		
		OMouse _ThisOrClone(int value, int max) {
			if ((uint)value > max) throw new ArgumentOutOfRangeException(null, "Max " + max);
			return _ThisOrClone();
		}
		
		struct _Fields { //makes easier to init, reset or copy fields
			public _Fields() { }
			public int ClickSpeed = 20, MoveSpeed, ClickSleepFinally = 10, MoveSleepFinally = 10;
			public bool Relaxed;
		}
		_Fields _f;
		
		/// <summary>
		/// Initializes this instance with default values or values copied from another instance.
		/// </summary>
		/// <param name="other">If not <c>null</c>, copies its options into this variable.</param>
		internal OMouse(OMouse other = null) //don't need public like OKey
		{
			if (other != null) {
				_f = other._f;
			} else {
				_f = new();
			}
		}
		
		/// <summary>
		/// How long to wait (milliseconds) after sending each mouse button down or up event (2 events for click, 4 for double-click).
		/// Default: 20.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 s).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <example>
		/// <code><![CDATA[
		/// opt.mouse.ClickSpeed = 30;
		/// ]]></code>
		/// </example>
		public int ClickSpeed {
			get => _f.ClickSpeed;
			set => _ThisOrClone(value, 1000)._f.ClickSpeed = value;
		}
		
		/// <summary>
		/// If not 0, makes mouse movements slower, not instant.
		/// Default: 0.
		/// </summary>
		/// <value>Valid values: 0 (instant) - 10000 (slowest).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Used by <see cref="mouse.move"/>, <see cref="mouse.click"/> and other functions that generate mouse movement events, except <see cref="mouse.moveBy(string, double)"/>.
		/// It is not milliseconds or some other unit. It adds intermediate mouse movements and small delays when moving the mouse cursor to the specified point. The speed also depends on the distance.
		/// Value 0 (default) does not add intermediate mouse movements. Adds at least 1 if some mouse buttons are pressed. Value 1 adds at least 1 intermediate mouse movement. Values 10-50 are good for visually slow movements.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.mouse.MoveSpeed = 30;
		/// ]]></code>
		/// </example>
		public int MoveSpeed {
			get => _f.MoveSpeed;
			set => _ThisOrClone(value, 10000)._f.MoveSpeed = value;
		}
		
		/// <summary>
		/// How long to wait (milliseconds) before a "mouse click" or "mouse wheel" function returns.
		/// Default: 10.
		/// </summary>
		/// <value>Valid values: 0 - 10000 (10 s).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// The "click" functions also sleep <see cref="ClickSpeed"/> ms after button down and up. Default <b>ClickSpeed</b> is 20, default <b>ClickSleepFinally</b> is 10, therefore default click time without mouse-move is 20+20+10=50.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.mouse.ClickSpeedFinally = 30;
		/// ]]></code>
		/// </example>
		public int ClickSleepFinally {
			get => _f.ClickSleepFinally;
			set => _ThisOrClone(value, 10000)._f.ClickSleepFinally = value;
		}
		
		/// <summary>
		/// How long to wait (milliseconds) after moving the mouse cursor. Used in "move+click" functions too.
		/// Default: 10.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 s).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Used by <see cref="mouse.move"/> (finally), <see cref="mouse.click"/> (between moving and clicking) and other functions that generate mouse movement events.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.mouse.MoveSpeedFinally = 30;
		/// ]]></code>
		/// </example>
		public int MoveSleepFinally {
			get => _f.MoveSleepFinally;
			set => _ThisOrClone(value, 1000)._f.MoveSleepFinally = value;
		}
		
		/// <summary>
		/// Make some functions less strict (throw less exceptions etc).
		/// Default: <c>false</c>.
		/// </summary>
		/// <remarks>
		/// This option is used by these functions:
		/// - <see cref="mouse.move"/>, <see cref="mouse.click"/> and other functions that move the cursor (mouse pointer):\
		///   <c>false</c> - throw exception if cannot move the cursor to the specified x y. For example if the x y is not in screen.\
		///   <c>true</c> - try to move anyway. Don't throw exception, regardless of the final cursor position (which probably will be at a screen edge).
		/// - <see cref="mouse.move"/>, <see cref="mouse.click"/> and other functions that move the cursor (mouse pointer):\
		///   <c>false</c> - before moving the cursor, wait while a mouse button is pressed by the user or another thread. It prevents an unintended drag-drop.\
		///   <c>true</c> - do not wait.
		/// - <see cref="mouse.click"/> and other functions that click or press a mouse button using window coordinates:\
		///   <c>false</c> - don't allow to click in another window. If need, activate the specified window (or its top-level parent). If that does not help, throw exception. However if the window is a control, allow x y anywhere in its top-level parent window.\
		///   <c>true</c> - allow to click in another window. Don't activate the window and don't throw exception.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.mouse.Relaxed = true;
		/// ]]></code>
		/// </example>
		public bool Relaxed {
			get => _f.Relaxed;
			set => _ThisOrClone()._f.Relaxed = value;
		}
		
		///
		public override string ToString() =>
			$"{{{string.Join(", ", GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => $"{p.Name} = {p.GetValue(this)}"))}}}";
	}
	
	/// <summary>
	/// Options for functions of class <see cref="keys"/>.
	/// Some options also are used with <see cref="clipboard"/> functions that send keys (<c>Ctrl+V</c> etc).
	/// </summary>
	/// <seealso cref="opt.key"/>
	/// <example>
	/// <code><![CDATA[
	/// opt.key.KeySpeed = 50;
	/// ]]></code>
	/// Set options for trigger actions.
	/// <code><![CDATA[
	/// Triggers.Options.BeforeAction = o => { opt.key.KeySpeed = 50; };
	/// ]]></code>
	/// </example>
	public class OKey {
		int _threadId;
		static AsyncLocal<OKey> s_ambient = new();
		
		internal static OKey Ambient_ {
			get => s_ambient.Value ??= new() { _threadId = Environment.CurrentManagedThreadId };
			set { Debug.Assert(value?._threadId != 0); s_ambient.Value = value; } //used only to restore scope
		}
		
		internal static OKey Scope_(bool inherit) {
			var old = s_ambient.Value;
			s_ambient.Value = (old != null && inherit) ? new(old) { _threadId = Environment.CurrentManagedThreadId } : null; //lazy
			return old;
		}
		
		OKey _ThisOrClone() {
			var r = this;
			if (_threadId != 0 && Environment.CurrentManagedThreadId is var tid && tid != _threadId) s_ambient.Value = r = new(this) { _threadId = tid };
			return r;
		}
		
		OKey _ThisOrClone(int value, int max) {
			if ((uint)value > max) throw new ArgumentOutOfRangeException(null, "Max " + max);
			return _ThisOrClone();
		}
		
		/// <summary>
		/// Initializes this instance with default values or values copied from another instance.
		/// </summary>
		/// <param name="cloneOptions">If not <c>null</c>, copies its options into this variable.</param>
		public OKey(OKey cloneOptions = null) {
			CopyOrDefault_(cloneOptions);
		}
		
		/// <summary>
		/// Copies options from <i>o</i>, or sets default if <c>o==null</c>. Like ctor does.
		/// </summary>
		internal void CopyOrDefault_(OKey o) { _f = o?._f ?? new(); }
		
		struct _Fields { //makes easier to init, reset or copy fields
			public _Fields() { }
			public int TextSpeed, KeySpeed = 2, KeySpeedClipboard = 5, SleepFinally = 10, PasteLength = 200;
			public OKeyText TextHow = OKeyText.Characters;
			public bool TextShiftEnter, PasteWorkaround, RestoreClipboard = true, NoModOff, NoCapsOff, NoBlockInput;
			public Action<OKeyHookData> Hook;
		}
		_Fields _f;
		
		/// <summary>
		/// Returns this variable, or <b>OKey</b> cloned from this variable and possibly modified by <b>Hook</b>.
		/// </summary>
		/// <param name="wFocus">The focused or active window. Use <b>GetWndFocusedOrActive</b>.</param>
		internal OKey GetHookOptionsOrThis_(wnd wFocus) {
			var call = this.Hook;
			if (call == null || wFocus.Is0) return this;
			var R = new OKey(this);
			call(new OKeyHookData(R, wFocus));
			return R;
		}
		
		/// <summary>
		/// How long to wait (milliseconds) between pressing and releasing each character key. Used by <see cref="keys.sendt"/>. Also by <see cref="keys.send"/> and similar functions for <c>"!text"</c> arguments.
		/// Default: 0.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 second).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Used only for "text" arguments, not for "keys" arguments. See <see cref="KeySpeed"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.TextSpeed = 50;
		/// ]]></code>
		/// </example>
		public int TextSpeed {
			get => _f.TextSpeed;
			set => _ThisOrClone(value, 1000)._f.TextSpeed = value;
		}
		
		/// <summary>
		/// How long to wait (milliseconds) between pressing and releasing each key. Used by <see cref="keys.send"/> and similar functions, except for <c>"!text"</c> arguments.
		/// Default: 2.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 second).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Used only for "keys" arguments, not for "text" arguments. See <see cref="TextSpeed"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.KeySpeed = 50;
		/// ]]></code>
		/// </example>
		public int KeySpeed {
			get => _f.KeySpeed;
			set => _ThisOrClone(value, 1000)._f.KeySpeed = value;
		}
		
		/// <summary>
		/// How long to wait (milliseconds) between sending <c>Ctrl+V</c> and <c>Ctrl+C</c> keys of clipboard functions (paste, copy).
		/// Default: 5.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 second).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.KeySpeedClipboard = 50;
		/// ]]></code>
		/// </example>
		public int KeySpeedClipboard {
			get => _f.KeySpeedClipboard;
			set => _ThisOrClone(value, 1000)._f.KeySpeedClipboard = value;
		}
		
		/// <summary>
		/// How long to wait (milliseconds) before a "send keys or text" function returns.
		/// Default: 10.
		/// </summary>
		/// <value>Valid values: 0 - 10000 (10 seconds).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Not used by <see cref="clipboard.copy"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.SleepFinally = 50;
		/// ]]></code>
		/// </example>
		public int SleepFinally {
			get => _f.SleepFinally;
			set => _ThisOrClone(value, 10000)._f.SleepFinally = value;
		}
		
		/// <summary>
		/// How to send text to the active window (keys, characters or clipboard).
		/// Default: <see cref="OKeyText.Characters"/>.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.TextHow = OKeyText.Paste;
		/// ]]></code>
		/// </example>
		public OKeyText TextHow {
			get => _f.TextHow;
			set => _ThisOrClone()._f.TextHow = value;
		}
		
		/// <summary>
		/// When sending text, instead of <c>Enter</c> send <c>Shift+Enter</c>.
		/// Default: false.
		/// </summary>
		/// <remarks>
		/// This option is applied when sending text with <see cref="keys.sendt"/> (like <c>keys.sendt("A\nB")</c>) or with operator <c>!</c> (like <c>keys.send("!A\nB")</c>) or with <see cref="keys.AddText(string, string)"/>. Ignored when using operator <c>^</c>, <c>_</c>, <see cref="keys.AddText(string, OKeyText)"/>, <see cref="keys.AddChar(char)"/>.
		/// </remarks>
		public bool TextShiftEnter {
			get => _f.TextShiftEnter;
			set => _ThisOrClone()._f.TextShiftEnter = value;
		}
		
		/// <summary>
		/// To send text use clipboard (like with <see cref="OKeyText.Paste"/>) if text length is &gt;= this value.
		/// Default: 200.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.PasteLength = 50;
		/// ]]></code>
		/// </example>
		public int PasteLength {
			get => _f.PasteLength;
			set => _ThisOrClone(value, int.MaxValue)._f.PasteLength = value;
		}
		
		/// <summary>
		/// When pasting text that ends with space, tab or/and newline characters, remove them and after pasting send them as keys.
		/// Default: <c>false</c>.
		/// </summary>
		/// <remarks>
		/// Some apps trim these characters when pasting.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.PasteWorkaround = true;
		/// ]]></code>
		/// </example>
		public bool PasteWorkaround {
			get => _f.PasteWorkaround;
			set => _ThisOrClone()._f.PasteWorkaround = value;
		}
		
		//rejected: rarely used. Eg can be useful for Python programmers. Let call clipboard.paste() explicitly or set the Paste option eg in hook.
		///// <summary>
		///// To send text use <see cref="OKeyText.Paste"/> if text contains characters <c>'\n'</c> followed by <c>'\t'</c> (tab) or spaces.
		///// </summary>
		///// <remarks>
		///// Some apps auto-indent. This option is a workaround.
		///// </remarks>
		//public bool PasteMultilineIndented { get; set; }
		
		/// <summary>
		/// Whether to restore clipboard data when copying or pasting text.
		/// Default: <c>true</c>.
		/// By default restores only text. See also <see cref="RestoreClipboardAllFormats"/>, <see cref="RestoreClipboardExceptFormats"/>.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.RestoreClipboard = true;
		/// ]]></code>
		/// </example>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.RestoreClipboard = false;
		/// ]]></code>
		/// </example>
		public bool RestoreClipboard {
			get => _f.RestoreClipboard;
			set => _ThisOrClone()._f.RestoreClipboard = value;
		}
		
		#region static RestoreClipboard options
		
		/// <summary>
		/// When copying or pasting text, restore clipboard data of all formats that are possible to restore.
		/// Default: <c>false</c> - restore only text.
		/// </summary>
		/// <remarks>
		/// Restoring data of all formats set by some apps can be slow or cause problems. More info: <see cref="RestoreClipboardExceptFormats"/>.
		/// 
		/// This property is static, not thread-static. It should be set (if need) at the start of script and not changed later.
		/// </remarks>
		/// <seealso cref="RestoreClipboard"/>
		/// <seealso cref="RestoreClipboardExceptFormats"/>
		/// <example>
		/// <code><![CDATA[
		/// OKey.RestoreClipboardAllFormats = true;
		/// ]]></code>
		/// </example>
		public static bool RestoreClipboardAllFormats { get; set; }
		
		/// <summary>
		/// When copying or pasting text, and <see cref="RestoreClipboardAllFormats"/> is <c>true</c>, do not restore clipboard data of these formats.
		/// Default: <c>null</c>.
		/// </summary>
		/// <remarks>
		/// To restore clipboard data, the copy/paste functions at first get clipboard data. Getting data of some formats set by some apps can be slow (100 ms or more) or cause problems (the app can change something in its window or even show a dialog).
		/// It also depends on whether this is the first time the data is being retrieved. The app can render data on demand, when some app is retrieving it from the clipboard first time; then can be slow etc.
		/// 
		/// You can use function <see cref="PrintClipboard"/> to see format names and get-data times.
		/// 
		/// There are several kinds of clipboard formats - registered, standard, private and display. Only registered formats have string names. For standard formats use API constant names, like <c>"CF_WAVE"</c>. Private, display and metafile formats are never restored.
		/// These formats are never restored: <b>CF_METAFILEPICT</b>, <b>CF_ENHMETAFILE</b>, <b>CF_PALETTE</b>, <b>CF_OWNERDISPLAY</b>, <b>CF_DSPx</b> formats, <b>CF_GDIOBJx</b> formats, <b>CF_PRIVATEx</b> formats. Some other formats too, but they are automatically synthesized from other formats if need. Also does not restore if data size is 0 or &gt; 10 MB.
		/// 
		/// This property is static, not thread-static. It should be set (if need) at the start of script and not changed later.
		/// </remarks>
		/// <seealso cref="RestoreClipboard"/>
		/// <seealso cref="PrintClipboard"/>
		/// <example>
		/// <code><![CDATA[
		/// OKey.RestoreClipboardExceptFormats = ["CF_UNICODETEXT", "HTML Format"];
		/// ]]></code>
		/// </example>
		public static string[] RestoreClipboardExceptFormats { get; set; }
		
		/// <summary>
		/// Writes to the output some info about current clipboard data.
		/// </summary>
		/// <remarks>
		/// Shows this info for each clipboard format: format name, time spent to get data (microseconds), data size (bytes), and whether this format would be restored (depends on <see cref="RestoreClipboardExceptFormats"/>).
		/// <note>Copy something to the clipboard each time before calling this function. Don't use <see cref="clipboard.copy"/> and don't call this function in loop. Else it shows small times.</note>
		/// The time depends on app, etc. More info: <see cref="RestoreClipboardExceptFormats"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// OKey.PrintClipboard();
		/// ]]></code>
		/// </example>
		public static void PrintClipboard() => clipboard.PrintClipboard_();
		
		#endregion
		
		/// <summary>
		/// When starting to send keys or text, don't release modifier keys.
		/// Default: <c>false</c>.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.NoModOff = true;
		/// ]]></code>
		/// </example>
		public bool NoModOff {
			get => _f.NoModOff;
			set => _ThisOrClone()._f.NoModOff = value;
		}
		
		/// <summary>
		/// When starting to send keys or text, don't turn off <c>CapsLock</c>.
		/// Default: <c>false</c>.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.NoCapsOff = true;
		/// ]]></code>
		/// </example>
		public bool NoCapsOff {
			get => _f.NoCapsOff;
			set => _ThisOrClone()._f.NoCapsOff = value;
		}
		
		/// <summary>
		/// While sending or pasting keys or text, don't block user-pressed keys.
		/// Default: <c>false</c>.
		/// </summary>
		/// <remarks>
		/// If <c>false</c> (default), user-pressed keys are sent afterwards. If <c>true</c>, user-pressed keys can be mixed with script-pressed keys, which is particularly dangerous when modifier keys are mixed (and combined) with non-modifier keys.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.NoBlockInput = true;
		/// ]]></code>
		/// </example>
		public bool NoBlockInput {
			get => _f.NoBlockInput;
			set => _ThisOrClone()._f.NoBlockInput = value;
		}
		
		/// <summary>
		/// Callback function that can modify options of "send keys or text" functions depending on active window etc.
		/// Default: <c>null</c>.
		/// </summary>
		/// <remarks>
		/// The callback function is called by <see cref="keys.send"/>, <see cref="keys.sendt"/>, <see cref="keys.SendNow"/>, <see cref="clipboard.paste"/> and similar functions. Not called by <see cref="clipboard.copy"/>.
		/// </remarks>
		/// <seealso cref="OKeyHookData"/>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.Hook = k => {
		/// 	print.it(k.w);
		/// 	var w = k.w.Window; //if k.w is a control, get its top-level window
		/// 	var name = w.Name;
		/// 	if (name.Like("* Slow App")) {
		/// 		k.optk.KeySpeed = 50;
		/// 		k.optk.TextSpeed = 50;
		/// 	}
		/// };
		/// 
		/// for (int i = 0; i < 10; i++) {
		/// 	1.s();
		/// 	keys.send("Home");
		/// }
		/// ]]></code>
		/// </example>
		public Action<OKeyHookData> Hook {
			get => _f.Hook;
			set => _ThisOrClone()._f.Hook = value;
		}
		
		///
		public override string ToString() =>
			$"{{{string.Join(", ", GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => $"{p.Name} = {p.GetValue(this)}"))}}}";
	}
	
	/// <summary>
	/// Parameter type of the <see cref="OKey.Hook"/> callback function.
	/// </summary>
	public struct OKeyHookData {
		internal OKeyHookData(OKey optk, wnd w) { this.optk = optk; this.w = w; }
		
		/// <summary>
		/// Options used by the "send keys or text" function. The callback function can modify them, except <b>Hook</b>, <b>NoModOff</b>, <b>NoCapsOff</b>, <b>NoBlockInput</b>.
		/// </summary>
		public readonly OKey optk;
		
		/// <summary>
		/// The focused control. If there is no focused control - the active window. Use <c>w.Window</c> to get top-level window; if <c>w.Window == w</c>, <b>w</b> is the active window, else the focused control. The callback function is not called if there is no active window.
		/// </summary>
		public readonly wnd w;
	}
	
	/// <summary>
	/// How functions send text.
	/// See <see cref="OKey.TextHow"/>.
	/// </summary>
	/// <remarks>
	/// There are three ways to send text to the active app using keys:
	/// - Characters (default) - use special key code <b>VK_PACKET</b>. Can send most characters.
	/// - Keys - use virtual-key codes, with <c>Shift</c> etc where need. Can send only characters that can be simply entered with the keyboard using current keyboard layout.
	/// - Paste - use the clipboard and <c>Ctrl+V</c>. Can send any text.
	/// 
	/// Most but not all apps support all three ways.
	/// </remarks>
	public enum OKeyText {
		/// <summary>
		/// Send most text characters using special key code <b>VK_PACKET</b>.
		/// This option is default. Few apps don't support it.
		/// For newlines, tab and space sends keys (<c>Enter</c>, <c>Tab</c>, <c>Space</c>), because <b>VK_PACKET</b> often does not work well.
		/// If text contains Unicode characters with Unicode code above 0xffff, clipboard-pastes whole text, because many apps don't support Unicode surrogates sent as <b>WM_PACKET</b> pairs.
		/// </summary>
		Characters,
		//Tested many apps/controls/frameworks. Works almost everywhere.
		//Does not work with Pidgin (GTK), but works eg with Inkscape (GTK too).
		//I guess does not work with many games.
		//In PhraseExpress this is default. Its alternative methods are SendKeys (does not send Unicode chars) and clipboard. It uses clipboard if text is long, default 100. Allows to choose different for specified apps. Does not add any delays between chars; for some apps too fast, eg VirtualBox edit fields when text contains Unicode surrogates.
		
		/// <summary>
		/// Send virtual-key codes, with <c>Shift</c> etc where need.
		/// All apps support it.
		/// If a character cannot be simply typed with the keyboard using current keyboard layout, sends it like with the <b>Characters</b> option.
		/// </summary>
		KeysOrChar,
		
		/// <summary>
		/// Send virtual-key codes, with <c>Shift</c> etc where need.
		/// All apps support it.
		/// If text contains characters that cannot be simply typed with the keyboard using current keyboard layout, clipboard-pastes whole text.
		/// </summary>
		KeysOrPaste,
		
		/// <summary>
		/// Paste text using the clipboard and <c>Ctrl+V</c>.
		/// Few apps don't support it.
		/// This option is recommended for long text, because other ways then are too slow.
		/// Other options are unreliable when text length is more than 4000 and the target app is too slow to process sent characters. Then <see cref="OKey.TextSpeed"/> can help.
		/// Also, other options are unreliable when the target app modifies typed text, for example has such features as auto-complete, auto-indent or auto-correct. However some apps modify even pasted text, for example trim the last newline or space.
		/// When pasting text, previous clipboard data of some formats is lost. Text is restored by default.
		/// </summary>
		Paste,
		
		//rejected: WmPaste. Few windows support it.
		//rejected: WM_CHAR. It isn't sync with keyboard/mouse input. It has sense only if window specified (send to inactive window). Maybe will add a function in the future.
	}
	
	/// <summary>
	/// Options for run-time warnings (<see cref="print.warning"/>).
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// opt.warnings.Verbose = false;
	/// ]]></code>
	/// </example>
	public class OWarnings {
		int _threadId;
		static AsyncLocal<OWarnings> s_ambient = new();
		
		internal static OWarnings Ambient_ {
			get => s_ambient.Value ??= new() { _threadId = Environment.CurrentManagedThreadId };
			set { Debug.Assert(value?._threadId != 0); s_ambient.Value = value; } //used only to restore scope
		}
		
		internal static OWarnings Scope_(bool inherit) {
			var old = s_ambient.Value;
			s_ambient.Value = (old != null && inherit) ? new(old) { _threadId = Environment.CurrentManagedThreadId } : null; //lazy
			return old;
		}
		
		OWarnings _ThisOrClone() {
			var r = this;
			if (_threadId != 0 && Environment.CurrentManagedThreadId is var tid && tid != _threadId) s_ambient.Value = r = new(this) { _threadId = tid };
			return r;
		}
		
		bool? _verbose;
		List<string> _disabledWarnings;
		
		/// <summary>
		/// Initializes this instance with default values or values copied from another instance.
		/// </summary>
		/// <param name="other">If not <c>null</c>, copies its options into this variable.</param>
		internal OWarnings(OWarnings other = null) {
			if (other != null) {
				_verbose = other._verbose;
				_disabledWarnings = other._disabledWarnings == null ? null : new(other._disabledWarnings);
			}
		}
		
		/// <summary>
		/// If <c>true</c>, some library functions may display more warnings and other info.
		/// If not explicitly set, the default value depends on the build configuration of the main assembly: <c>true</c> if Debug, <c>false</c> if Release (<c>optimize true</c>). See <see cref="AssemblyUtil_.IsDebug"/>.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// opt.warnings.Verbose = false;
		/// ]]></code>
		/// </example>
		public bool Verbose {
			get => (_verbose ??= script.isDebug) == true;
			set => _ThisOrClone()._verbose = value;
		}
		
		/// <summary>
		/// Disables one or more run-time warnings.
		/// </summary>
		/// <param name="warningsWild">One or more warnings as case-insensitive wildcard strings. See <see cref="ExtString.Like(string, string, bool)"/>.</param>
		/// <remarks>
		/// Adds the strings to an internal list. When <see cref="print.warning"/> is called, it looks in the list. If finds the warning in the list, does not show the warning.
		/// It's easy to auto-restore warnings with <c>using</c>, like in the second example. Restoring is optional.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.warnings.Disable("*part of warning 1 text*", "*part of warning 2 text*");
		/// ]]></code>
		/// Temporarily disable all warnings.
		/// <code><![CDATA[
		/// opt.warnings.Verbose = true;
		/// print.warning("one");
		/// using(opt.warnings.Disable("*")) {
		/// 	print.warning("two");
		/// }
		/// print.warning("three");
		/// ]]></code>
		/// </example>
		public UsingEndAction Disable(params string[] warningsWild) => _ThisOrClone()._Disable(warningsWild);
		
		UsingEndAction _Disable(params string[] warningsWild) {
			_disabledWarnings ??= new List<string>();
			int restoreCount = _disabledWarnings.Count;
			_disabledWarnings.AddRange(warningsWild);
			return new UsingEndAction(() => _disabledWarnings.RemoveRange(restoreCount, _disabledWarnings.Count - restoreCount));
		}
		
		/// <summary>
		/// Returns <c>true</c> if the specified warning text matches a wildcard string added with <see cref="Disable"/>.
		/// </summary>
		/// <param name="text">Warning text. Case-insensitive.</param>
		public bool IsDisabled(string text) {
			string s = text ?? "";
			if (_disabledWarnings is { } a) foreach (var k in a) if (s.Like(k, true)) return true;
			return false;
		}
	}
	
	/// <summary>
	/// Obsolete. Use <see cref="Seconds"/> instead. Some wait functions may still use some <b>OWait</b> properties for backward compatibility.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class OWait {
		int _threadId;
		static AsyncLocal<OWait> s_ambient = new();
		
		internal static OWait Ambient_ {
			get => s_ambient.Value ??= new() { _threadId = Environment.CurrentManagedThreadId };
			set { Debug.Assert(value?._threadId != 0); s_ambient.Value = value; } //used only to restore scope
		}
		
		internal static OWait Scope_(bool inherit) {
			var old = s_ambient.Value;
			s_ambient.Value = (old != null && inherit) ? new(old) { _threadId = Environment.CurrentManagedThreadId } : null; //lazy
			return old;
		}
		
		OWait _ThisOrClone() {
			var r = this;
			if (_threadId != 0 && Environment.CurrentManagedThreadId is var tid && tid != _threadId) s_ambient.Value = r = new(this) { _threadId = tid };
			return r;
		}
		
		internal OWait() { _period = 10; }
		
		internal OWait(OWait other) { _doEvents = other._doEvents; _period = other._period; }
		
		/// <summary>
		/// Obsolete. Use <see cref="Seconds"/> instead.
		/// </summary>
		public OWait(int? period = null, bool? doEvents = null) {
			_doEvents = doEvents ?? opt.wait._doEvents;
			_period = period ?? opt.wait._period;
		}
		
		/// <summary>
		/// Obsolete. Use <see cref="Seconds"/> instead.
		/// </summary>
		public bool DoEvents {
			get => _doEvents;
			set => _ThisOrClone()._doEvents = value;
		}
		bool _doEvents;
		
		/// <summary>
		/// Obsolete. Used only by obsolete/hidden wait functions. Use <see cref="Seconds"/> instead.
		/// </summary>
		public int Period {
			get => _period;
			set => _ThisOrClone()._period = value;
		}
		int _period;
	}
}
