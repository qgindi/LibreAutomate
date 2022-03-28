//CONSIDER: instead of static options use a settings file. Because now need to set static options for each script, and rare script uses multiple threads.

//SHOULDDO: add options for wnd/elm/uiimage find functions to wait. Now converting find to wait in script is tedious.
//	opt.wait.WndFind = 2;
//	or
//	opt.find.WndWait = 2;
//	But such options must be applied only in that file.
//	Or something in the find function. Maybe overload or last parameter. Or findW.

namespace Au
{
	/// <summary>
	/// Options for some functions of this library.
	/// </summary>
	/// <remarks>
	/// Some frequently used static functions of this library have some options (settings). For example <see cref="keys.send"/> allows to change speed, text sending method, etc. Passing options as parameters in each call usually isn't what you want to do in automation scripts. Instead you can set options using static properties. This class contains several groups of options for functions of various classes. See examples.
	/// 
	/// There are two sets of identical or similar options - in class <b>opt</b> and in class <see cref="opt.init"/>:
	/// - <b>opt</b> - thread-static options (each thread has its own instance). Functions of this library use them. You can change or change-restore them anywhere in script. Initial options are automatically copied from <b>opt.init</b> when that group of options (<b>Key</b>, <b>Mouse</b>, etc) is used first time in that thread (explicitly or by library functions).
	/// - <b>opt.init</b> - static options. Contains initial property values for <b>opt</b>. Normally you change them when script starts. Don't change later, it's not thread-safe.
	/// </remarks>
	public static class opt
	{
		/// <summary>
		/// Options for keyboard and clipboard functions (classes <see cref="keys"/>, <see cref="clipboard"/> and functions that use them).
		/// </summary>
		/// <remarks>
		/// Each thread has its own <b>opt.key</b> instance. It inherits options from <see cref="opt.init.key"/>.
		/// Also can be used when creating <see cref="keys"/> instances. See the second example.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.key.KeySpeed = 100;
		/// keys.send("Right*10 Ctrl+A");
		/// ]]></code>
		/// Use a <b>keys</b> instance.
		/// <code><![CDATA[
		/// var k = new keys(opt.key); //create new keys instance and copy options from opt.key to it
		/// k.Options.KeySpeed = 100; //changes option of k but not of opt.key
		/// k.Add("Right*10 Ctrl+A").Send(); //uses options of k
		/// ]]></code>
		/// </example>
		public static OKey key => t_key ??= new OKey(init.key);
		[ThreadStatic] static OKey t_key;

		/// <summary>
		/// Options for mouse functions (class <see cref="Au.mouse"/> and functions that use it).
		/// </summary>
		/// <remarks>
		/// Each thread has its own <b>opt.mouse</b> instance. It inherits options from <see cref="opt.init.mouse"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.mouse.ClickSpeed = 100;
		/// mouse.click();
		/// ]]></code>
		/// </example>
		public static OMouse mouse => t_mouse ??= new OMouse(init.mouse);
		[ThreadStatic] static OMouse t_mouse;

		/// <summary>
		/// Options for 'wait for' functions.
		/// </summary>
		/// <remarks>
		/// Each thread has its own <b>opt.wait</b> instance. There is no <b>opt.init.wait</b>.
		/// Most 'wait for' functions of this library use these options. Functions of .NET classes don't.
		/// </remarks>
		public static OWait wait => t_wait ??= new OWait();
		[ThreadStatic] static OWait t_wait;

		/// <summary>
		/// Options for showing run-time warnings and other info that can be useful to find problems in code at run time.
		/// </summary>
		/// <remarks>
		/// Each thread has its own <b>opt.warnings</b> instance. It inherits options from <see cref="opt.init.warnings"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// opt.warnings.Verbose = false;
		/// print.warning("Example");
		/// print.warning("Example");
		/// ]]></code>
		/// </example>
		public static OWarnings warnings => t_warnings ??= new OWarnings(init.warnings);
		[ThreadStatic] static OWarnings t_warnings;

		//rejected. Use opt.scope.
		///// <summary>
		///// Resets all options. Copies from <see cref="opt.init"/>.
		///// </summary>
		//public static void reset()
		//{
		//	t_key?.Reset();
		//	t_mouse?.Reset();
		//	t_waitFor?.Reset();
		//	t_warnings?.Reset();
		//}

		/// <summary>
		/// Default <see cref="opt"/> properties of a thread.
		/// </summary>
		/// <remarks>
		/// You can change these options at the start of your script/program. Don't change later.
		/// </remarks>
		public static class init
		{
			/// <summary>
			/// Default option values for <see cref="opt.key"/> of a thread.
			/// </summary>
			/// <remarks>
			/// Also can be used when creating <see cref="keys"/> instances. See the second example.
			/// </remarks>
			/// <example>
			/// <code><![CDATA[
			/// opt.init.key.KeySpeed = 10;
			/// ...
			/// keys.send("Tab Ctrl+V"); //uses opt.key, which is implicitly copied from opt.init.key
			/// ]]></code>
			/// Use a <b>keys</b> instance.
			/// <code><![CDATA[
			/// var k = new keys(opt.init.key); //create new keys instance and copy options from opt.init.key to it
			/// k.Options.KeySpeed = 100; //changes option of k
			/// k.Add("Tab Ctrl+V").Send(); //uses options of k
			/// ]]></code>
			/// </example>
			public static OKey key { get; } = new OKey();

			/// <summary>
			/// Default option values for <see cref="opt.mouse"/> of a thread.
			/// </summary>
			/// <example>
			/// <code><![CDATA[
			/// opt.init.mouse.ClickSpeed = 10;
			/// ...
			/// mouse.click(); //uses opt.mouse, which is implicitly copied from opt.init.mouse
			/// ]]></code>
			/// </example>
			public static OMouse mouse { get; } = new OMouse();

			/// <summary>
			/// Default option values for <see cref="opt.warnings"/> of a thread.
			/// </summary>
			/// <example>
			/// <code><![CDATA[
			/// opt.init.warnings.Verbose = false;
			/// ]]></code>
			/// </example>
			public static OWarnings warnings { get; } = new OWarnings();

		}

		/// <summary>
		/// Creates temporary scopes for options.
		/// Example: <c>using(opt.scope.key()) { opt.key.KeySpeed=5; ... }</c>.
		/// </summary>
		public static class scope
		{
			/// <summary>
			/// Creates temporary scope for <see cref="opt.mouse"/> options. See example.
			/// </summary>
			/// <param name="inherit">If true (default), inherit current options. If false, inherit default options (<see cref="opt.init"/>).</param>
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
				var old = _Mouse(inherit);
				return new UsingEndAction(() => t_mouse = old);
			}

			static OMouse _Mouse(bool inherit) {
				var old = t_mouse;
				//t_mouse = new OMouse((old != null && inherit) ? old : Static.Mouse);
				t_mouse = (old != null && inherit) ? new OMouse(old) : null; //lazy
				return old;
			}

			/// <summary>
			/// Creates temporary scope for <see cref="opt.key"/> options. See example.
			/// </summary>
			/// <param name="inherit">If true (default), inherit current options. If false, inherit default options (<see cref="opt.init"/>).</param>
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
				var old = _Key(inherit);
				return new UsingEndAction(() => t_key = old);
			}

			static OKey _Key(bool inherit) {
				var old = t_key;
				//t_key = new OKey((old != null && inherit) ? old : Static.Key);
				t_key = (old != null && inherit) ? new OKey(old) : null; //lazy
				return old;
			}

			/// <summary>
			/// Creates temporary scope for <see cref="opt.wait"/> options. See example.
			/// </summary>
			/// <param name="inherit">If true (default), inherit current options. If false, inherit default options (<see cref="opt.init"/>).</param>
			/// <example>
			/// <code><![CDATA[
			/// print.it(opt.wait.Period);
			/// using(opt.scope.wait()) {
			/// 	opt.wait.Period = 5;
			/// 	print.it(opt.wait.Period);
			/// } //here restored automatically
			/// print.it(opt.wait.Period);
			/// ]]></code>
			/// </example>
			public static UsingEndAction wait(bool inherit = true) {
				var old = _WaitFor(inherit);
				return new UsingEndAction(() => t_wait = old);
			}

			static OWait _WaitFor(bool inherit) {
				var old = t_wait;
				//t_waitFor = (old != null && inherit) ? new OWait(old.Period, old.DoEvents) : new OptWaitFor();
				t_wait = (old != null && inherit) ? new OWait(old.Period, old.DoEvents) : null; //lazy
				return old;
			}

			/// <summary>
			/// Creates temporary scope for <see cref="opt.warnings"/> options. See example.
			/// </summary>
			/// <param name="inherit">If true (default), inherit current options. If false, inherit default options (<see cref="opt.init"/>).</param>
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
				var old = _Warnings(inherit);
				return new UsingEndAction(() => t_warnings = old);
			}

			static OWarnings _Warnings(bool inherit) {
				var old = t_warnings;
				//t_warnings = new OWarnings((old != null && inherit) ? old : Static.Warnings);
				t_warnings = (old != null && inherit) ? new OWarnings(old) : null; //lazy
				return old;
			}

			/// <summary>
			/// Creates temporary scope for all options. See example.
			/// </summary>
			/// <param name="inherit">If true (default), inherit current options. If false, inherit default options (<see cref="opt.init"/>).</param>
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
				var o1 = _Mouse(inherit);
				var o2 = _Key(inherit);
				var o3 = _WaitFor(inherit);
				var o4 = _Warnings(inherit);
				//rejected
				///// <param name="speed">If not null, sets <c>opt.mouse.MoveSpeed = speed; opt.key.KeySpeed = speed; opt.key.TextSpeed = speed;</c>. See <see cref="OMouse.MoveSpeed"/>, <see cref="OKey.KeySpeed"/>, <see cref="OKey.TextSpeed"/>.</param>
				//if (speed != null) {
				//	int i = speed.Value;
				//	opt.mouse.MoveSpeed = i;
				//	opt.key.KeySpeed = i;
				//	opt.key.TextSpeed = i;
				//	//if (i > 20) opt.mouse.ClickSpeed = i; //no, can be too big. Or neeed to clamp.
				//	//if (i > 10) opt.mouse.ClickSleepFinally = i;
				//	//if (i > 10) opt.key.SleepFinally = i;
				//}
				return new UsingEndAction(() => {
					t_mouse = o1;
					t_key = o2;
					t_wait = o3;
					t_warnings = o4;
				});
			}
		}
	}
}

namespace Au.Types
{
	/// <summary>
	/// Options for run-time warnings (<see cref="print.warning"/>).
	/// </summary>
	public class OWarnings
	{
		bool? _verbose;
		List<string> _disabledWarnings;

		/// <summary>
		/// Initializes this instance with default values or values copied from another instance.
		/// </summary>
		/// <param name="cloneOptions">If not null, copies its options into this variable.</param>
		internal OWarnings(OWarnings cloneOptions = null) {
			if (cloneOptions != null) {
				_Copy(cloneOptions);
			}
		}

		void _Copy(OWarnings o) {
			_verbose = o._verbose;
			_disabledWarnings = o._disabledWarnings == null ? null : new List<string>(o._disabledWarnings);
		}

		//rejected. Use opt.scope.
		///// <summary>
		///// Resets all options. Copies from <see cref="opt.init.warnings"/>.
		///// </summary>
		//public void Reset() => _Copy(opt.init.warnings);

		/// <summary>
		/// If true, some library functions may display more warnings and other info.
		/// If not explicitly set, the default value depends on the build configuration of the main assembly: true if Debug, false if Release (optimize true). See <see cref="AssemblyUtil_.IsDebug"/>.
		/// </summary>
		public bool Verbose {
			get => (_verbose ??= script.isDebug) == true;
			set => _verbose = value;
		}

		/// <summary>
		/// Disables one or more run-time warnings.
		/// </summary>
		/// <param name="warningsWild">One or more warnings as case-insensitive wildcard strings. See <see cref="ExtString.Like(string, string, bool)"/>.</param>
		/// <remarks>
		/// Adds the strings to an internal list. When <see cref="print.warning"/> is called, it looks in the list. If finds the warning in the list, does not show the warning.
		/// It's easy to auto-restore warnings with 'using', like in the second example. Restoring is optional.
		/// </remarks>
		/// <example>
		/// This code at the start of script disables two warnings in all threads.
		/// <code><![CDATA[
		/// opt.init.warnings.Disable("*part of warning 1 text*", "*part of warning 2 text*");
		/// ]]></code>
		/// Temporarily disable all warnings in this thread.
		/// <code><![CDATA[
		/// opt.warnings.Verbose = true;
		/// print.warning("one");
		/// using(opt.warnings.Disable("*")) {
		/// 	print.warning("two");
		/// }
		/// print.warning("three");
		/// ]]></code>
		/// Don't use code <c>using(opt.init.warnings.Disable...</c>, it's not thread-safe.
		/// </example>
		public UsingEndAction Disable(params string[] warningsWild) {
			_disabledWarnings ??= new List<string>();
			int restoreCount = _disabledWarnings.Count;
			_disabledWarnings.AddRange(warningsWild);
			return new UsingEndAction(() => _disabledWarnings.RemoveRange(restoreCount, _disabledWarnings.Count - restoreCount));
		}

		/// <summary>
		/// Returns true if the specified warning text matches a wildcard string added with <see cref="Disable"/>.
		/// </summary>
		/// <param name="text">Warning text. Case-insensitive.</param>
		public bool IsDisabled(string text) {
			string s = text ?? "";
			var a = _disabledWarnings;
			if (a != null) foreach (var k in a) if (s.Like(k, true)) return true;
			return false;
		}
	}

	/// <summary>
	/// Options for functions of class <see cref="mouse"/>.
	/// </summary>
	/// <remarks>
	/// Total <c>Click(x, y)</c> time is: mouse move + <see cref="MoveSleepFinally"/> + button down + <see cref="ClickSpeed"/> + button down + <see cref="ClickSpeed"/> + <see cref="ClickSleepFinally"/>.
	/// </remarks>
	/// <seealso cref="opt.mouse"/>
	/// <seealso cref="opt.init.mouse"/>
	public class OMouse
	{
		struct _Options //makes easier to copy and reset fields
		{
			public int ClickSpeed, MoveSpeed, ClickSleepFinally, MoveSleepFinally;
			public bool Relaxed;
		}
		_Options _o;

		/// <summary>
		/// Initializes this instance with default values or values copied from another instance.
		/// </summary>
		/// <param name="cloneOptions">If not null, copies its options into this variable.</param>
		internal OMouse(OMouse cloneOptions = null) //don't need public like OKey
		{
			if (cloneOptions != null) {
				_o = cloneOptions._o;
			} else {
				_o.ClickSpeed = 20;
				//_o.MoveSpeed = 0;
				_o.ClickSleepFinally = 10;
				_o.MoveSleepFinally = 10;
				//_o.Relaxed = false;
			}
		}

		//rejected. Use opt.scope.
		///// <summary>
		///// Resets all options. Copies from <see cref="opt.init.mouse"/>.
		///// </summary>
		//public void Reset() => _o = opt.init.mouse._o;

		bool _IsStatic => this == opt.init.mouse;

		int _SetValue(int value, int max, int maxStatic) {
			var m = _IsStatic ? maxStatic : max;
			if ((uint)value > m) throw new ArgumentOutOfRangeException(null, "Max " + m.ToString());
			return value;
		}

		/// <summary>
		/// How long to wait (milliseconds) after sending each mouse button down or up event (2 events for click, 4 for double-click).
		/// Default: 20.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 s). Valid values for <see cref="opt.init.mouse"/>: 0 - 100 (1 s).</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public int ClickSpeed {
			get => _o.ClickSpeed;
			set => _o.ClickSpeed = _SetValue(value, 1000, 100);
		}

		/// <summary>
		/// If not 0, makes mouse movements slower, not instant.
		/// Default: 0.
		/// </summary>
		/// <value>Valid values: 0 (instant) - 10000 (slowest). Valid values for <see cref="opt.init.mouse"/>: 0 - 100.</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Used by <see cref="mouse.move"/>, <see cref="mouse.click"/> and other functions that generate mouse movement events, except <see cref="mouse.moveBy(string, double)"/>.
		/// It is not milliseconds or some other unit. It adds intermediate mouse movements and small delays when moving the mouse cursor to the specified point. The speed also depends on the distance.
		/// Value 0 (default) does not add intermediate mouse movements. Adds at least 1 if some mouse buttons are pressed. Value 1 adds at least 1 intermediate mouse movement. Values 10-50 are good for visually slow movements.
		/// </remarks>
		public int MoveSpeed {
			get => _o.MoveSpeed;
			set => _o.MoveSpeed = _SetValue(value, 10000, 100);
		}

		/// <summary>
		/// How long to wait (milliseconds) before a 'mouse click' or 'mouse wheel' function returns.
		/// Default: 10.
		/// </summary>
		/// <value>Valid values: 0 - 10000 (10 s). Valid values for <see cref="opt.init.mouse"/>: 0 - 100.</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// The 'click' functions also sleep <see cref="ClickSpeed"/> ms after button down and up. Default <b>ClickSpeed</b> is 20, default <b>ClickSleepFinally</b> is 10, therefore default click time without mouse-move is 20+20+10=50.
		/// </remarks>
		public int ClickSleepFinally {
			get => _o.ClickSleepFinally;
			set => _o.ClickSleepFinally = _SetValue(value, 10000, 100);
		}

		/// <summary>
		/// How long to wait (milliseconds) after moving the mouse cursor. Used in 'move+click' functions too.
		/// Default: 10.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 s). Valid values for <see cref="opt.init.mouse"/>: 0 - 100.</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Used by <see cref="mouse.move"/> (finally), <see cref="mouse.click"/> (between moving and clicking) and other functions that generate mouse movement events.
		/// </remarks>
		public int MoveSleepFinally {
			get => _o.MoveSleepFinally;
			set => _o.MoveSleepFinally = _SetValue(value, 1000, 100);
		}

		/// <summary>
		/// Make some functions less strict (throw less exceptions etc).
		/// Default: false.
		/// </summary>
		/// <remarks>
		/// This option is used by these functions:
		/// - <see cref="mouse.move"/>, <see cref="mouse.click"/> and other functions that move the cursor (mouse pointer):
		/// <br/>false - throw exception if cannot move the cursor to the specified x y. For example if the x y is not in screen.
		/// <br/>true - try to move anyway. Don't throw exception, regardless of the final cursor position (which probably will be at a screen edge).
		/// - <see cref="mouse.move"/>, <see cref="mouse.click"/> and other functions that move the cursor (mouse pointer):
		/// <br/>false - before moving the cursor, wait while a mouse button is pressed by the user or another thread. It prevents an unintended drag-drop.
		/// <br/>true - do not wait.
		/// - <see cref="mouse.click"/> and other functions that click or press a mouse button using window coordinates:
		/// <br/>false - don't allow to click in another window. If need, activate the specified window (or its top-level parent). If that does not help, throw exception. However if the window is a control, allow x y anywhere in its top-level parent window.
		/// <br/>true - allow to click in another window. Don't activate the window and don't throw exception.
		/// </remarks>
		public bool Relaxed { get => _o.Relaxed; set => _o.Relaxed = value; }
	}

	/// <summary>
	/// Options for functions of class <see cref="keys"/>.
	/// Some options also are used with <see cref="clipboard"/> functions that send keys (Ctrl+V etc).
	/// </summary>
	/// <seealso cref="opt.key"/>
	/// <seealso cref="opt.init.key"/>
	public class OKey
	{
		/// <summary>
		/// Initializes this instance with default values or values copied from another instance.
		/// </summary>
		/// <param name="cloneOptions">If not null, copies its options into this variable.</param>
		public OKey(OKey cloneOptions = null) {
			CopyOrDefault_(cloneOptions);
		}

		/// <summary>
		/// Copies options from o, or sets default if o==null. Like ctor does.
		/// </summary>
		internal void CopyOrDefault_(OKey o) {
			if (o != null) {
				_textSpeed = o._textSpeed;
				_keySpeed = o._keySpeed;
				_clipboardKeySpeed = o._clipboardKeySpeed;
				_sleepFinally = o._sleepFinally;
				_pasteLength = o._pasteLength;
				TextHow = o.TextHow;
				PasteWorkaround = o.PasteWorkaround;
				RestoreClipboard = o.RestoreClipboard;
				NoModOff = o.NoModOff;
				NoCapsOff = o.NoCapsOff;
				NoBlockInput = o.NoBlockInput;
				Hook = o.Hook;
			} else {
				_textSpeed = 0;
				_keySpeed = 2;
				_clipboardKeySpeed = 5;
				_sleepFinally = 10;
				_pasteLength = 200;
				TextHow = OKeyText.Characters;
				PasteWorkaround = default;
				RestoreClipboard = true;
				NoModOff = default;
				NoCapsOff = default;
				NoBlockInput = default;
				Hook = default;
			}
			//#if DEBUG
			//			if(o != null) {
			//				Debug1 = o.Debug1;
			//				Debug2 = o.Debug2;
			//			} else {
			//				Debug1 = default;
			//				Debug2 = default;
			//			}
			//#endif
		}

		//rejected. Use opt.scope.
		///// <summary>
		///// Resets all options. Copies from <see cref="opt.init.key"/>.
		///// </summary>
		//public void Reset() => CopyOrDefault_(opt.init.key);

		/// <summary>
		/// Returns this variable, or <b>OKey</b> cloned from this variable and possibly modified by <b>Hook</b>.
		/// </summary>
		/// <param name="wFocus">The focused or active window. Use Lib.GetWndFocusedOrActive().</param>
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
		/// <value>Valid values: 0 - 1000 (1 second). Valid values for <see cref="opt.init.key"/>: 0 - 100.</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Used only for 'text' arguments, not for 'keys' arguments. See <see cref="KeySpeed"/>.
		/// </remarks>
		public int TextSpeed {
			get => _textSpeed;
			set => _textSpeed = _SetValue(value, 1000, 100);
		}
		int _textSpeed;

		/// <summary>
		/// How long to wait (milliseconds) between pressing and releasing each key. Used by <see cref="keys.send"/> and similar functions, except for <c>"!text"</c> arguments.
		/// Default: 2.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 second). Valid values for <see cref="opt.init.key"/>: 0 - 100.</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Used only for 'keys' arguments, not for 'text' arguments. See <see cref="TextSpeed"/>.
		/// </remarks>
		public int KeySpeed {
			get => _keySpeed;
			set => _keySpeed = _SetValue(value, 1000, 100);
		}
		int _keySpeed;

		/// <summary>
		/// How long to wait (milliseconds) between sending Ctrl+V and Ctrl+C keys of clipboard functions (paste, copy).
		/// Default: 5.
		/// </summary>
		/// <value>Valid values: 0 - 1000 (1 second). Valid values for <see cref="opt.init.key"/>: 0 - 100.</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// In most apps copy/paste works without this delay. Known apps that need it: Internet Explorer's address bar, BlueStacks.
		/// </remarks>
		public int KeySpeedClipboard {
			get => _clipboardKeySpeed;
			set => _clipboardKeySpeed = _SetValue(value, 1000, 100);
		}
		int _clipboardKeySpeed;

		/// <summary>
		/// How long to wait (milliseconds) before a 'send keys or text' function returns.
		/// Default: 10.
		/// </summary>
		/// <value>Valid values: 0 - 10000 (10 seconds). Valid values for <see cref="opt.init.key"/>: 0 - 100.</value>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <remarks>
		/// Not used by <see cref="clipboard.copy"/>.
		/// </remarks>
		public int SleepFinally {
			get => _sleepFinally;
			set => _sleepFinally = _SetValue(value, 10000, 100);
		}
		int _sleepFinally;

		bool _IsStatic => this == opt.init.key;

		int _SetValue(int value, int max, int maxStatic) {
			var m = _IsStatic ? maxStatic : max;
			if ((uint)value > m) throw new ArgumentOutOfRangeException(null, "Max " + m.ToString());
			return value;
		}
		//T _SetValue<T>(T value, T max, T maxStatic) where T : IComparable<T>
		//{
		//	var m = _IsStatic ? maxStatic : max;
		//	if(value.CompareTo(m) > 0 || value.CompareTo(default) < 0) throw new ArgumentOutOfRangeException(null, "Max " + m.ToString());
		//	return value;
		//}

		/// <summary>
		/// How to send text to the active window (keys, characters or clipboard).
		/// Default: <see cref="OKeyText.Characters"/>.
		/// </summary>
		public OKeyText TextHow { get; set; }

		/// <summary>
		/// To send text use clipboard (like with <see cref="OKeyText.Paste"/>) if text length is &gt;= this value.
		/// Default: 200.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public int PasteLength {
			get => _pasteLength;
			set => _pasteLength = _SetValue(value, int.MaxValue, int.MaxValue);
		}
		int _pasteLength;

		/// <summary>
		/// When pasting text that ends with space, tab or/and newline characters, remove them and after pasting send them as keys.
		/// Default: false.
		/// </summary>
		/// <remarks>
		/// Some apps trim these characters when pasting.
		/// </remarks>
		public bool PasteWorkaround { get; set; }

		//rejected: rarely used. Eg can be useful for Python programmers. Let call clipboard.paste() explicitly or set the Paste option eg in hook.
		///// <summary>
		///// To send text use <see cref="OKeyText.Paste"/> if text contains characters '\n' followed by '\t' (tab) or spaces.
		///// </summary>
		///// <remarks>
		///// Some apps auto-indent. This option is a workaround.
		///// </remarks>
		//public bool PasteMultilineIndented { get; set; }

		/// <summary>
		/// Whether to restore clipboard data when copying or pasting text.
		/// Default: true.
		/// By default restores only text. See also <see cref="RestoreClipboardAllFormats"/>, <see cref="RestoreClipboardExceptFormats"/>.
		/// </summary>
		public bool RestoreClipboard { get; set; }

		#region static RestoreClipboard options

		/// <summary>
		/// When copying or pasting text, restore clipboard data of all formats that are possible to restore.
		/// Default: false - restore only text.
		/// </summary>
		/// <remarks>
		/// Restoring data of all formats set by some apps can be slow or cause problems. More info: <see cref="RestoreClipboardExceptFormats"/>.
		/// 
		/// This property is static, not thread-static. It should be set (if need) at the start of script and not changed later.
		/// </remarks>
		/// <seealso cref="RestoreClipboard"/>
		/// <seealso cref="RestoreClipboardExceptFormats"/>
		public static bool RestoreClipboardAllFormats { get; set; }

		/// <summary>
		/// When copying or pasting text, and <see cref="RestoreClipboardAllFormats"/> is true, do not restore clipboard data of these formats.
		/// Default: null.
		/// </summary>
		/// <remarks>
		/// To restore clipboard data, the copy/paste functions at first get clipboard data. Getting data of some formats set by some apps can be slow (100 ms or more) or cause problems (the app can change something in its window or even show a dialog).
		/// It also depends on whether this is the first time the data is being retrieved. The app can render data on demand, when some app is retrieving it from the clipboard first time; then can be slow etc.
		/// 
		/// You can use function <see cref="PrintClipboard"/> to see format names and get-data times.
		/// 
		/// There are several kinds of clipboard formats - registered, standard, private and display. Only registered formats have string names. For standard formats use API contant names, like "CF_WAVE". Private, display and metafile formats are never restored.
		/// These formats are never restored: CF_METAFILEPICT, CF_ENHMETAFILE, CF_PALETTE, CF_OWNERDISPLAY, CF_DSPx formats, CF_GDIOBJx formats, CF_PRIVATEx formats. Some other formats too, but they are automatically synthesized from other formats if need. Also does not restore if data size is 0 or &gt; 10 MB.
		/// 
		/// This property is static, not thread-static. It should be set (if need) at the start of script and not changed later.
		/// </remarks>
		/// <seealso cref="RestoreClipboard"/>
		/// <seealso cref="PrintClipboard"/>
		public static string[] RestoreClipboardExceptFormats { get; set; }

		/// <summary>
		/// Writes to the output some info about current clipboard data.
		/// </summary>
		/// <remarks>
		/// Shows this info for each clipboard format: format name, time spent to get data (microseconds), data size (bytes), and whether this format would be restored (depends on <see cref="RestoreClipboardExceptFormats"/>).
		/// <note>Copy something to the clipboard each time before calling this function. Don't use <see cref="clipboard.copy"/> and don't call this function in loop. Else it shows small times.</note>
		/// The time depends on app, etc. More info: <see cref="RestoreClipboardExceptFormats"/>.
		/// </remarks>
		public static void PrintClipboard() => clipboard.PrintClipboard_();

		#endregion

		/// <summary>
		/// When starting to send keys or text, don't release modifier keys.
		/// Default: false.
		/// </summary>
		public bool NoModOff { get; set; }

		/// <summary>
		/// When starting to send keys or text, don't turn off CapsLock.
		/// Default: false.
		/// </summary>
		public bool NoCapsOff { get; set; }

		/// <summary>
		/// While sending or pasting keys or text, don't block user-pressed keys.
		/// Default: false.
		/// </summary>
		/// <remarks>
		/// If false (default), user-pressed keys are sent afterwards. If true, user-pressed keys can be mixed with script-pressed keys, which is particularly dangerous when modifier keys are mixed (and combined) with non-modifier keys.
		/// </remarks>
		public bool NoBlockInput { get; set; }

		/// <summary>
		/// Callback function that can modify options of 'send keys or text' functions depending on active window etc.
		/// Default: null.
		/// </summary>
		/// <remarks>
		/// The callback function is called by <see cref="keys.send"/>, <see cref="keys.sendt"/>, <see cref="keys.SendIt"/>, <see cref="clipboard.paste"/> and similar functions. Not called by <see cref="clipboard.copy"/>.
		/// </remarks>
		/// <seealso cref="OKeyHookData"/>
		public Action<OKeyHookData> Hook { get; set; }

		//#if DEBUG
		//		public int Debug1 { get; set; }
		//		public int Debug2 { get; set; }
		//#endif
	}

	/// <summary>
	/// Parameter type of the <see cref="OKey.Hook"/> callback function.
	/// </summary>
	public struct OKeyHookData
	{
		internal OKeyHookData(OKey optk, wnd w) { this.optk = optk; this.w = w; }

		/// <summary>
		/// Options used by the 'send keys or text' function. The callback function can modify them, except Hook, NoModOff, NoCapsOff, NoBlockInput.
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
	/// - Characters (default) - use special key code VK_PACKET. Can send most characters.
	/// - Keys - use virtual-key codes, with Shift etc where need. Can send only characters that can be simply entered with the keyboard using current keyboard layout.
	/// - Paste - use the clipboard and Ctrl+V. Can send any text.
	/// 
	/// Most but not all apps support all three ways.
	/// </remarks>
	public enum OKeyText
	{
		/// <summary>
		/// Send most text characters using special key code VK_PACKET.
		/// This option is default. Few apps don't support it.
		/// For newlines, tab and space sends keys (Enter, Tab, Space), because VK_PACKET often does not work well.
		/// If text contains Unicode characters with Unicode code above 0xffff, clipboard-pastes whole text, because many apps don't support Unicode surrogates sent as WM_PACKET pairs.
		/// </summary>
		Characters,
		//Tested many apps/controls/frameworks. Works almost everywhere.
		//Does not work with Pidgin (GTK), but works eg with Inkscape (GTK too).
		//I guess does not work with many games.
		//In PhraseExpress this is default. Its alternative methods are SendKeys (does not send Unicode chars) and clipboard. It uses clipboard if text is long, default 100. Allows to choose different for specified apps. Does not add any delays between chars; for some apps too fast, eg VirtualBox edit fields when text contains Unicode surrogates.

		/// <summary>
		/// Send virtual-key codes, with Shift etc where need.
		/// All apps support it.
		/// If a character cannot be simply typed with the keyboard using current keyboard layout, sends it like with the <b>Characters</b> option.
		/// </summary>
		KeysOrChar,

		/// <summary>
		/// Send virtual-key codes, with Shift etc where need.
		/// All apps support it.
		/// If text contains characters that cannot be simply typed with the keyboard using current keyboard layout, clipboard-pastes whole text.
		/// </summary>
		KeysOrPaste,

		/// <summary>
		/// Paste text using the clipboard and Ctrl+V.
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
	/// Options for 'wait for' functions.
	/// </summary>
	/// <seealso cref="opt.wait"/>
	/// <seealso cref="wait.forCondition"/>
	/// <seealso cref="wait.Loop"/>
	public class OWait
	{
		/// <summary>
		/// The sleep time between checking the wait condition. Milliseconds.
		/// Default: 10.
		/// </summary>
		/// <value>Valid values: 1-1000.</value>
		/// <remarks>
		/// Most 'wait for' functions of this library use <see cref="wait.Loop"/>, which repeatedly checks the wait condition and sleeps (waits) several ms. This property sets the initial sleep time, which then is incremented by <b>Period</b>/10 ms (default 1 ms) in each loop until reaches <b>Period</b>*50 (default 500 ms).
		/// This property makes the response time shorter or longer. If &lt;10, makes it shorter (faster response), but increases CPU usage; if &gt;10, makes it longer (slower response).
		/// </remarks>
		/// <seealso cref="wait.Loop.Period"/>
		/// <example>
		/// <code><![CDATA[
		/// opt.wait.Period = 100;
		/// ]]></code>
		/// </example>
		public int Period { get => _period; set => _period = Math.Clamp(value, 1, 1000); }
		int _period;

		/// <summary>
		/// Use <see cref="wait.doEvents(int)"/> instead of <see cref="wait.ms"/>.
		/// Default: false.
		/// </summary>
		/// <remarks>
		/// Use this property when need to process Windows messages, events, hooks, timers, etc while waiting. More info: <see cref="wait.doEvents(int)"/>.
		/// </remarks>
		/// <seealso cref="wait.forMessagesAndCondition"/>
		/// <example>
		/// <code><![CDATA[
		/// opt.wait.DoEvents = true;
		/// ]]></code>
		/// </example>
		public bool DoEvents { get; set; }

		/// <param name="period">Sets <see cref="Period"/>.</param>
		/// <param name="doEvents">Sets <see cref="DoEvents"/>.</param>
		public OWait(int period = 10, bool doEvents = false) {
			Period = period; DoEvents = doEvents;
		}

		//rejected. Use opt.scope.
		///// <summary>
		///// Resets all options.
		///// </summary>
		//public void Reset()
		//{
		//	Period = 10; DoEvents = false;
		//}

		//no
		///// <summary>
		///// Implicit conversion from int. Sets <see cref="Period"/>.
		///// </summary>
		//public static implicit operator OWait(int period) => new OptWaitFor(period, false);

		///// <summary>
		///// Implicit conversion from bool. Sets <see cref="DoEvents"/>.
		///// </summary>
		//public static implicit operator OWait(bool doEvents) => new OptWaitFor(10, doEvents);
	}
}
