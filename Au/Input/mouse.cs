//SHOULDDO: test how mouse moves through non-screen area between screens A and C when screen B is in between.
//	QM2 has problems crossing non-screen corners at default speed. Au works well.

namespace Au {
	/// <summary>
	/// Mouse functions.
	/// </summary>
	/// <remarks>
	/// Should not be used to click windows of own thread. It may work or not. If need, use another thread. Example in <see cref="keys.send"/>.
	/// </remarks>
	public static class mouse {
		/// <summary>
		/// Gets cursor (mouse pointer) position.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// var p = mouse.xy;
		/// print.it(p.x, p.y);
		/// 
		/// var (x, y) = mouse.xy;
		/// print.it(x, y);
		/// ]]></code>
		/// <code><![CDATA[
		/// POINT mousePos = mouse.xy;
		/// mouse.moveBy(20, 50);
		/// POINT mousePos2 = mouse.xy;
		/// 
		/// double dist = Math2.Distance(mousePos2, mousePos);
		/// print.it(dist, (int)dist, dist.ToInt(), mousePos2.x - mousePos.x, mousePos2.y - mousePos.y);
		/// ]]></code>
		/// </example>
		public static POINT xy { get { Api.GetCursorPos(out var p); return p; } }

		///// <summary>
		///// Gets cursor (mouse pointer) X coordinate (<b>mouse.xy.x</b>).
		///// </summary>
		//public static int x => xy.x;

		///// <summary>
		///// Gets cursor (mouse pointer) Y coordinate (<b>mouse.xy.y</b>).
		///// </summary>
		//public static int y => xy.y;

		static void _Move(POINT p, bool fast) {
			bool relaxed = opt.mouse.Relaxed, willFail = false;

			if (!screen.isInAnyScreen(p)) {
				if (!relaxed) throw new ArgumentOutOfRangeException(null, "Cannot mouse-move. This x y is not in screen. " + p.ToString());
				willFail = true;
			}

			if (!fast) _MoveSlowTo(p);

			POINT p0 = xy;
			//bool retry = false; g1:
			bool ok = false;
			for (int i = 0, n = relaxed ? 3 : 10; i < n; i++) {
				//perf.first();
				_SendMove(p);
				//info: now XY is still not updated in ~10% cases.
				//	In my tests was always updated after sleeping 0 or 1 ms.
				//	But the user etc also can move the mouse at the same time. Then the i loop always helps.
				//perf.next();
				int j = 0;
				for (; ; j++) {
					var pNow = xy;
					ok = (pNow == p);
					if (ok || pNow != p0 || j > 3) break;
					wait.ms(j); //0+1+2+3
				}
				//perf.nw();
				//print.it(j, i);
				if (ok || willFail) break;
				//note: don't put the _Sleep(7) here

				//Accidentally this is also a workaround for SendInput bug:
				//	When moving to other screen, moves to a wrong place if new x or y is outside of rect of old screen.
				//	To reproduce:
				//		Let now mouse is in second screen, and try to move to 0 0 (primary screen).
				//		Case 1: Let second screen is at the bottom and its left is eg 300. Single SendInput will move to 300 0.
				//		Case 2: Let second screen is at the right and its top is eg 300. Will move x to the right of the primary screen.
				//	Tested only on Win10.
				//	Workaround: call SendInput twice.
			}
			if (!ok && !relaxed) {
				var es = $"*mouse-move to this x y in screen. " + p.ToString();
				wnd.active.UacCheckAndThrow_(es + ". The active"); //it's a mystery for users. API SendInput fails even if the point is not in the window.
																   //rejected: wnd.getwnd.root.ActivateL()
				InputDesktopException.ThrowIfBadDesktop(es);
				throw new AuException(es);
				//known reasons:
				//	Active window of higher UAC IL.
				//	BlockInput, hook, some script etc that blocks mouse movement or restores mouse position.
				//	ClipCursor.
			}
			t_prevMousePos.last = p;

			_Sleep(opt.mouse.MoveSleepFinally);
		}

		static void _MoveSlowTo(POINT p) {
			bool drag = t_pressedButtons != 0;
			int speed = opt.mouse.MoveSpeed;
			if (drag) speed++; else if (speed == 0) return; //need at least 1 intermediate point, else some apps don't drag or don't select text etc
			var p1 = mouse.xy; //the start point; p is the end point
			int x2 = p.x - p1.x, y2 = p.y - p1.y; //x and y distances
			bool xNeg, yNeg; if (xNeg = x2 < 0) x2 = -x2; if (yNeg = y2 < 0) y2 = -y2; //make code easier
			double dist = Math.Sqrt(x2 * x2 + y2 * y2); if (dist < 4) return;
			double angle = Math.Atan2(y2, x2);
			var (sin, cos) = Math.SinCos(angle);
			double speed2 = speed / 10.0;

			for (double z = 0; ;) {
				bool startDragSlowly = drag && z < Math.Min(20, dist); //some apps refuse to drag if too fast
				double d = ((startDragSlowly ? z : dist - z) / 10 + 1) / speed2; //the speed depends on the distance, and is decreasing; increasing while startDragSlowly
				if (d > dist / 2) d = dist / 2; else if (d < 2) d = 2; //need at least 1 intermediate point; don't need too many points
				z += d;
				int x = (z * cos).ToInt(), y = (z * sin).ToInt();

				//make the line smoother. Converting double to int creates ugly bumps etc.
				//	Usually don't need it, but in some cases it may be better.
				//	Try to add 1 to x or/and y and use it if then the angle difference is smaller.
				int xPlus = 0, yPlus = 0; double anDiff = 4;
				for (int i = 0; i < 2; i++) {
					for (int j = 0; j < 2; j++) {
						double ad = Math.Abs(Math.Atan2(y + j, x + i) - angle);
						if (ad < anDiff) { anDiff = ad; xPlus = i; yPlus = j; }
					}
				}
				if (xPlus > 0 || yPlus > 0) {
					x += xPlus; y += yPlus;
					z = Math.Sqrt(x * x + y * y);
				}

				if (dist - z < .5) break;

				_SendMove(new(p1.x + (xNeg ? -x : x), p1.y + (yNeg ? -y : y)));
				_Sleep(7 + (speed - 1) / 10); //7-8 is the natural max WM_MOUSEMOVE period, even when the system timer period is 15.625 (default).
			}
		}

		/// <summary>
		/// Moves the cursor (mouse pointer) to the position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <returns>Cursor position in screen coordinates.</returns>
		/// <param name="w">Window or control.</param>
		/// <param name="x">X coordinate relative to the client area of <i>w</i>. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate relative to the client area of <i>w</i>. Default - center.</param>
		/// <param name="nonClient"><i>x y</i> are relative to the window rectangle.</param>
		/// <exception cref="AuWndException">
		/// - Invalid window.
		/// - The window is hidden. No exception if just cloaked, for example in another desktop; then on click will activate, which usually uncloaks. No exception if <i>w</i> is a control.
		/// - Other window-related failures.
		/// </exception>
		/// <inheritdoc cref="move(POINT)" path="//exception|//remarks"/>
		public static POINT move(wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			WaitForNoButtonsPressed_();
			w.ThrowIfInvalid();
			var wTL = w.Window;
			if (!wTL.IsVisible) throw new AuWndException(wTL, "Cannot mouse-move. The window is invisible"); //should make visible? Probably not. If cloaked because in an inactive virtual desktop etc, Click activates and it usually uncloaks.
			if (wTL.IsMinimized) { wTL.ShowNotMinimized(1); _Sleep(500); } //never mind: if w is a control...
			var p = Coord.NormalizeInWindow(x, y, w, nonClient, centerIfEmpty: true);
			if (!w.MapClientToScreen(ref p)) w.ThrowUseNative();
			_Move(p, fast: false);
			return p;
		}

		/// <summary>
		/// Moves the cursor (mouse pointer) to the position <i>x y</i> relative to UI object <i>obj</i>.
		/// </summary>
		/// <param name="obj">Can be <b>wnd</b>, <b>elm</b>, <b>uiimage</b>, <b>screen</b>, <b>RECT</b> in screen, <b>RECT</b> in window, <see cref="lastXY"/> (<c>true</c>), <see cref="xy"/> (<c>false</c>)</param>
		/// <param name="x">X coordinate relative to <i>obj</i>. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate relative to <i>obj</i>. Default - center.</param>
		/// <inheritdoc cref="move(POINT)" path="//exception|//remarks"/>
		/// <exception cref="Exception">Other exceptions. Depends on <i>obj</i> type.</exception>
		public static void move(MObject obj, Coord x = default, Coord y = default) {
			switch (obj.Value) {
			case wnd w:
				move(w, x, y);
				break;
			case elm e:
				e.MouseMove(x, y);
				break;
			case uiimage u:
				u.MouseMove(x, y);
				break;
			case RECT r:
				move(Coord.NormalizeInRect(x, y, r, centerIfEmpty: true));
				break;
			case (wnd w, RECT r):
				var p = Coord.NormalizeInRect(x, y, r, centerIfEmpty: true);
				move(w, p.x, p.y);
				break;
			case (wnd w, bool nonClient):
				move(w, x, y, nonClient);
				break;
			case (screen s, bool workArea):
				move(Coord.Normalize(x, y, workArea, s, centerIfEmpty: true));
				break;
			case bool useLastXY:
				move(_CoordToRelativeXY(x, y, useLastXY));
				break;
			case null: //default(MObject)
				move(x, y);
				break;
			}
		}

		static POINT _CoordToRelativeXY(Coord x, Coord y, bool useLastXY) {
			var p = useLastXY ? lastXY : xy;
			if (x.Type is CoordType.Normal or CoordType.None) p.x += x.Value; else throw new ArgumentException(null, "x");
			if (y.Type is CoordType.Normal or CoordType.None) p.y += y.Value; else throw new ArgumentException(null, "y");
			return p;
		}

		/// <summary>
		/// Moves the cursor (mouse pointer) to the specified position in screen.
		/// </summary>
		/// <returns>Normalized cursor position.</returns>
		/// <param name="x">X coordinate. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate.</param>
		/// <inheritdoc cref="move(POINT)" path="//exception|//remarks"/>
		public static POINT move(Coord x, Coord y) {
			WaitForNoButtonsPressed_();
			var p = Coord.Normalize(x, y);
			_Move(p, fast: false);
			return p;
		}
		//rejected: parameters bool workArea = false, screen screen = default. Rarely used. Can use the POINT overload and Coord.Normalize.

		/// <summary>
		/// Moves the cursor (mouse pointer) to the specified position in screen.
		/// </summary>
		/// <param name="p">
		/// Coordinates.
		/// Tip: To specify coordinates relative to the right, bottom, work area or a non-primary screen, use <see cref="Coord.Normalize"/>, like in the example.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">The position is not in screen. No exception if option <b>Relaxed</b> is <c>true</c> (then moves to a screen edge).</exception>
		/// <exception cref="AuException">
		/// Failed to move the cursor to that position. Some reasons:
		/// - The active window belongs to a process of higher [](xref:uac) integrity level.
		/// - Another thread blocks or modifies mouse input (API <b>BlockInput</b>, mouse hooks, frequent API <b>SendInput</b> etc).
		/// - Some application called API <b>ClipCursor</b>. No exception if option <b>Relaxed</b> is <c>true</c> (then final cursor position is undefined).
		/// </exception>
		/// <exception cref="InputDesktopException"></exception>
		/// <remarks>
		/// Uses <see cref="opt.mouse"/>: <see cref="OMouse.MoveSpeed"/>, <see cref="OMouse.MoveSleepFinally"/>, <see cref="OMouse.Relaxed"/>.
		/// </remarks>
		/// <example>
		/// Save-restore mouse position.
		/// <code><![CDATA[
		/// var p = mouse.xy;
		/// //...
		/// mouse.move(p);
		/// ]]></code>
		/// Use coordinates in the first non-primary screen.
		/// <code><![CDATA[
		/// mouse.move(Coord.Normalize(10, ^10, screen: screen.index(1))); //10 from left, 10 from bottom
		/// ]]></code>
		/// </example>
		public static void move(POINT p) {
			WaitForNoButtonsPressed_();
			_Move(p, fast: false);
		}

		/// <summary>
		/// Remembers current mouse cursor position to be later restored with <see cref="restore"/>.
		/// </summary>
		public static void save() {
			if (t_prevMousePos == null) t_prevMousePos = new _PrevMousePos();
			else t_prevMousePos.first = xy;
		}

		/// <summary>
		/// Moves the mouse cursor where it was at the time of the last <see cref="save"/> call in this thread. If it was not called - of the first "mouse move" or "mouse click" function call in this thread. Does nothing if these functions were not called.
		/// </summary>
		/// <remarks>
		/// Uses <see cref="opt.mouse"/>: <see cref="OMouse.MoveSleepFinally"/>, <see cref="OMouse.Relaxed"/>.
		/// </remarks>
		/// <inheritdoc cref="move(POINT)" path="/exception"/>
		public static void restore() {
			if (t_prevMousePos == null) return;
			WaitForNoButtonsPressed_();
			_Move(t_prevMousePos.first, fast: true);
		}

		class _PrevMousePos {
			public POINT first, last;
			public _PrevMousePos() { first = last = xy; }
			public _PrevMousePos(POINT p) { first = last = p; }
		}
		[ThreadStatic] static _PrevMousePos t_prevMousePos;

		/// <summary>
		/// Mouse cursor position of the most recent successful "mouse move" or "mouse click" function call in this thread.
		/// If such functions are still not called in this thread, returns <see cref="xy"/>.
		/// </summary>
		public static POINT lastXY => t_prevMousePos?.last ?? xy;

		//rejected. MoveRelative usually is better. If need, can use code: Move(mouse.xy.x+dx, mouse.xy.y+dy).
		//public static void MoveFromCurrent(int dx, int dy)
		//{
		//	var p = XY;
		//	p.Offset(dx, dy);
		//	Move(p);
		//}

		/// <summary>
		/// Moves the cursor (mouse pointer) relative to <see cref="lastXY"/> or <see cref="xy"/>.
		/// </summary>
		/// <returns>Final cursor position in screen.</returns>
		/// <param name="dx">X offset from <b>lastXY.x</b> or <b>xy.x</b>.</param>
		/// <param name="dy">Y offset from <b>lastXY.y</b> or <b>xy.y</b>.</param>
		/// <param name="useLastXY">If <c>true</c> (default), moves relative to <see cref="lastXY"/>, else relative to <see cref="xy"/>.</param>
		/// <inheritdoc cref="move(POINT)" path="//exception|//remarks"/>
		public static POINT moveBy(int dx, int dy, bool useLastXY = true) {
			WaitForNoButtonsPressed_();
			var p = useLastXY ? lastXY : xy;
			p.x += dx; p.y += dy;
			_Move(p, fast: false);
			return p;
		}

		/// <summary>
		/// Moves the cursor (mouse pointer) relative to <see cref="lastXY"/>. Uses multiple x y offsets.
		/// </summary>
		/// <returns>Final cursor position in screen.</returns>
		/// <param name="offsets">String containing multiple x y offsets. Created by a mouse recorder tool with <see cref="RecordingUtil.MouseToString"/>.</param>
		/// <param name="speedFactor">Speed factor. For example, 0.5 makes 2 times faster.</param>
		/// <exception cref="FormatException">Invalid Base64 string.</exception>
		/// <exception cref="ArgumentException">The string is not compatible with this library version (recorded with a newer version and has additional options).</exception>
		/// <inheritdoc cref="move(POINT)" path="/exception"/>
		/// <remarks>
		/// Uses <see cref="opt.mouse"/>: <see cref="OMouse.Relaxed"/> (only for the last movement; always relaxed in intermediate movements).
		/// </remarks>
		public static POINT moveBy(string offsets, double speedFactor = 1.0) {
			WaitForNoButtonsPressed_();

			var a = Convert.FromBase64String(offsets);

			byte flags = a[0];
			const int knownFlags = 1; if ((flags & knownFlags) != flags) throw new ArgumentException("Unknown string version");
			bool withSleepTimes = 0 != (flags & 1);
			bool isSleep = withSleepTimes;

			var p = lastXY;
			int pdx = 0, pdy = 0;

			for (int i = 1; i < a.Length;) {
				if (i > 1 && (isSleep || !withSleepTimes)) {
					_SendMove(p);
					if (!withSleepTimes) _Sleep((7 * speedFactor).ToInt());
				}

				int v = a[i++], nbytes = (v & 3) + 1;
				for (int j = 1; j < nbytes; j++) v |= a[i++] << j * 8;
				v = (int)((uint)v >> 2);
				if (isSleep) {
					//print.it($"nbytes={nbytes}    sleep={v}");

					_Sleep((v * speedFactor).ToInt());
				} else {
					int shift = nbytes * 4 - 1, mask = (1 << shift) - 1;
					int x = v & mask, y = (v >> shift) & mask;
					shift = 32 - shift; x <<= shift; x >>= shift; y <<= shift; y >>= shift; //sign-extend
					int dx = pdx + x; pdx = dx;
					int dy = pdy + y; pdy = dy;

					//print.it($"dx={dx} dy={dy}    x={x} y={y}    nbytes={nbytes}    v=0x{v:X}");

					p.x += dx; p.y += dy;
				}
				isSleep ^= withSleepTimes;
			}
			_Move(p, fast: true);
			return p;
		}

		/// <summary>
		/// Sends single mouse movement event.
		/// x y are normal absolute coordinates.
		/// </summary>
		static void _SendMove(POINT p) {
			t_prevMousePos ??= new _PrevMousePos(); //sets .first=.last=XY
			_SendRaw(Api.IMFlags.Move, p.x, p.y);
		}

		/// <summary>
		/// Sends single mouse button down or up event.
		/// Does not use the action flags of button.
		/// Applies <b>SM_SWAPBUTTON</b>.
		/// Also moves to <i>p</i> in the same API <b>SendInput</b> call.
		/// </summary>
		internal static void SendButton_(MButton button, bool down, POINT p) {
			//CONSIDER: release user-pressed modifier keys, like keys class does.
			//CONSIDER: block user input, like keys class does.

			Api.IMFlags f; MButtons mb;
			switch (button & (MButton.Left | MButton.Right | MButton.Middle | MButton.X1 | MButton.X2)) {
			case 0: //allow 0 for left. Example: wnd.find(...).MouseClick(x, y, MButton.DoubleClick)
			case MButton.Left: f = down ? Api.IMFlags.LeftDown : Api.IMFlags.LeftUp; mb = MButtons.Left; break;
			case MButton.Right: f = down ? Api.IMFlags.RightDown : Api.IMFlags.RightUp; mb = MButtons.Right; break;
			case MButton.Middle: f = down ? Api.IMFlags.MiddleDown : Api.IMFlags.MiddleUp; mb = MButtons.Middle; break;
			case MButton.X1: f = down ? Api.IMFlags.XDown | Api.IMFlags.X1 : Api.IMFlags.XUp | Api.IMFlags.X1; mb = MButtons.X1; break;
			case MButton.X2: f = down ? Api.IMFlags.XDown | Api.IMFlags.X2 : Api.IMFlags.XUp | Api.IMFlags.X2; mb = MButtons.X2; break;
			default: throw new ArgumentException("Several buttons specified", nameof(button)); //rejected: InvalidEnumArgumentException. It's in System.ComponentModel namespace.
			}

			//maybe mouse left/right buttons are swapped
			if (0 != (button & (MButton.Left | MButton.Right)) && 0 != Api.GetSystemMetrics(Api.SM_SWAPBUTTON))
				f ^= down ? Api.IMFlags.LeftDown | Api.IMFlags.RightDown : Api.IMFlags.LeftUp | Api.IMFlags.RightUp;

			//If this is a Click(x y), the sequence of sent events is like: move, sleep, down, sleep, up. Even Click() sleeps between down and up.
			//During the sleep the user can move the mouse. Correct it now if need.
			//tested: if don't need to move, mouse messages are not sent. Hooks not tested. In some cases are sent one or more mouse messages but it depends on other things.
			//Alternatively could temporarily block user input, but it is not good. Need a hook (UAC disables Api.BlockInput), etc. Better let scripts do it explicitly. If script contains several mouse/keys statements, it's better to block input once for all.
			f |= Api.IMFlags.Move;

			//normally don't need this, but this is a workaround for the SendInput bug with multiple screens
			if (p != xy) _SendRaw(Api.IMFlags.Move, p.x, p.y);

			_SendRaw(f, p.x, p.y);

			if (down) t_pressedButtons |= mb; else t_pressedButtons &= ~mb;
		}

		/// <summary>
		/// Calls <b>Api.SendInput</b> to send single mouse movement or/and button down or up or wheel event.
		/// Converts <i>x</i>, <i>y</i> as need for <b>MOUSEINPUT</b>.
		/// For X buttons use <c>Api.IMFlag.XDown|Api.IMFlag.X1</c> etc.
		/// If <b>Api.IMFlag.Move</b>, adds <b>Api.IMFlag.Absolute</b>.
		/// </summary>
		static unsafe void _SendRaw(Api.IMFlags flags, int x = 0, int y = 0, int wheel = 0) {
			if (0 != (flags & Api.IMFlags.Move)) {
				flags |= Api.IMFlags.Absolute;
				var psr = screen.primary.Rect;
				x = (int)((((long)x << 16) + (x >= 0 ? 0x8000 : -0x8000)) / psr.Width);
				y = (int)((((long)y << 16) + (y >= 0 ? 0x8000 : -0x8000)) / psr.Height);
			}

			int mouseData;
			if (0 != (flags & (Api.IMFlags.XDown | Api.IMFlags.XUp))) {
				mouseData = (int)((uint)flags >> 24);
				flags &= (Api.IMFlags)0xffffff;
			} else mouseData = wheel;

			var k = new Api.INPUTM(flags, x, y, mouseData);
			Api.SendInput(&k);
		}

		static void _Sleep(int ms) {
			wait.doEventsPrecise_(ms);

			//note: always doevents, even if window from point is not of our thread. Because:
			//	Cannot always reliably detect what window will receive the message and what then happens.
			//	There is not much sense to avoid doevents. If no message loop, it is fast and safe; else the script author should use another thread or expect anything.
			//	API SendInput dispatches sent messages anyway.
			//	_Click shows warning if window of this thread.

			//FUTURE: sync better, especially finally.
		}

		[ThreadStatic] static MButtons t_pressedButtons;

		static void _Click(MButton button, POINT p, wnd w = default) {
			if (w.Is0) w = Api.WindowFromPoint(p);
			bool windowOfThisThread = w.IsOfThisThread;
			if (windowOfThisThread) print.warning("Click(window of own thread) may not work. Use another thread.");
			//Sending a click to a window of own thread often does not work.
			//Reason 1: often the window on down event enters a message loop that waits for up event. But then this func cannot send the up event because it is in the loop (if it does doevents).
			//	Known workarounds:
			//	1 (applied). Don't sleepdoevents between sending down and up events.
			//	2. Let this func send the click from another thread, and sleepdoevents until that thread finishes the click.
			//Reason 2: if this func called from a click handler, OS does not send more mouse events.
			//	Known workarounds:
			//	1. (applied): show warning. Let the user modify the script: either don't click own windows or click from another thread.

			int sleep = opt.mouse.ClickSpeed;

			switch (button & (MButton.Down | MButton.Up | MButton.DoubleClick)) {
			case MButton.DoubleClick:
				sleep = Math.Min(sleep, Api.GetDoubleClickTime() / 4);
				//info: default double-click time is 500. Control Panel can set 200-900. API can set 1.
				//info: to detect double-click, some apps use time between down and down (that is why /4), others between up and down.

				SendButton_(button, true, p);
				if (!windowOfThisThread) _Sleep(sleep);
				SendButton_(button, false, p);
				if (!windowOfThisThread) _Sleep(sleep);
				goto case 0;
			case 0: //click
				SendButton_(button, true, p);
				if (!windowOfThisThread) _Sleep(sleep);
				SendButton_(button, false, p);
				break;
			case MButton.Down:
				SendButton_(button, true, p);
				break;
			case MButton.Up:
				SendButton_(button, false, p);
				break;
			default: throw new ArgumentException("Incompatible flags: Down, Up, DoubleClick", nameof(button));
			}
			_Sleep(sleep + opt.mouse.ClickSleepFinally);

			//rejected: detect click failures (UAC, BlockInput, hooks).
			//	Difficult. Cannot detect reliably. SendInput returns true.
			//	Eg when blocked by UAC, GetKeyState shows changed toggle state. Then probably hooks also called, did not test.
		}

		/// <summary>
		/// Clicks, double-clicks, presses or releases a mouse button at position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <param name="button">Button and action. Default: left click.</param>
		/// <exception cref="ArgumentException">Invalid <i>button</i> flags (multiple buttons or actions specified).</exception>
		/// <inheritdoc cref="click(wnd, Coord, Coord, bool)"/>
		/// <example>
		/// <code><![CDATA[
		/// mouse.clickEx(MButton.Middle, w1, 695, 110);
		/// mouse.clickEx(MButton.Right | MButton.Down, w1, 695, 110);
		/// mouse.clickEx(MButton.Right | MButton.Up, w1, 695, 110);
		/// ]]></code>
		/// </example>
		public static MRelease clickEx(MButton button, wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			POINT p = move(w, x, y, nonClient);

			//Make sure will click w, not another window.
			var action = button & (MButton.Down | MButton.Up | MButton.DoubleClick);
			if (action != MButton.Up && !opt.mouse.Relaxed) { //allow to release anywhere, eg it could be a drag-drop
				var wTL = w.Window;
				bool bad = !wTL.Rect.Contains(p);
				if (!bad) {
					if (!_CheckWindowFromPoint()) {
						//Debug_.Print("need to activate");
						//info: activating brings to the Z top and also uncloaks
						if (!wTL.IsEnabled(false)) bad = true; //probably an owned modal dialog disabled the window
						else if (wTL.ThreadId == wnd.getwnd.shellWindow.ThreadId) bad = true; //desktop
						else if (wTL.IsActive) wTL.ZorderTop(); //can be below another window in the same topmost/normal Z order, although it is rare.
						else bad = !wTL.Activate_(wnd.Internal_.ActivateFlags.NoThrowIfInvalid | wnd.Internal_.ActivateFlags.IgnoreIfNoActivateStyleEtc | wnd.Internal_.ActivateFlags.NoGetWindow);

						//rejected: if wTL is desktop, minimize windows. Scripts should not have a reason to click desktop. If need, they can minimize windows explicitly.
						//CONSIDER: activate always, because some controls don't respond when clicked while the window is inactive. But there is a risk to activate a window that does not want to be activated on click, even if we don't activate windows that have noactivate style. Probably better let the script author insert Activate before Click when need.
						//CONSIDER: what if the window is hung?

						if (!bad) bad = !_CheckWindowFromPoint();
					} else if (!wTL.IsActive && !wTL.IsNoActivateStyle_()) {
						//activate window, because some windows/controls have this nasty feature:
						//	If window inactive, the first click just activates the window but does not execute the click action.
						//	Example: ribbon controls.
						//	Usually on WM_MOUSEACTIVATE they return MA_ACTIVATEANDEAT. We could send the message to detect it, but it's dirty and dangerous, eg some windows try to activate or focus self.
						//	In any case, activating could make more reliable. In QM2 it worked well, don't remember any problems.
						wTL.ActivateL();
						wTL.MinimalSleepIfOtherThread_();
					}
				}
				if (bad) throw new AuWndException(wTL, "Cannot click. The point is not in the window");

				bool _CheckWindowFromPoint() {
					var wfp = wnd.fromXY(p, WXYFlags.NeedWindow);
					if (wfp == wTL) return true;
					//forgive if same thread and no caption. Eg a tooltip that disappears and relays the click to its owner window. But not if wTL is disabled.
					if (wTL.IsEnabled(false) && wfp.ThreadId == wTL.ThreadId && !wfp.HasStyle(WS.CAPTION)) return true;
					return false;
				}
			}

			_Click(button, p, w);
			return button;
		}

		/// <summary>
		/// Clicks, double-clicks, presses or releases a mouse button at position <i>x y</i> relative to UI object <i>obj</i>.
		/// </summary>
		/// <param name="button">Button and action. Default: left click.</param>
		/// <param name="obj">Can be <b>wnd</b>, <b>elm</b> (<see cref="elm.MouseClick"/>), <b>uiimage</b> (<see cref="uiimage.MouseClick"/>), <b>screen</b>, <b>RECT</b> in screen, <b>RECT</b> in window, <see cref="lastXY"/> (<c>true</c>), <see cref="xy"/> (<c>false</c>).</param>
		/// <param name="x">X coordinate relative to <i>obj</i>. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate relative to <i>obj</i>. Default - center.</param>
		/// <example></example>
		/// <inheritdoc cref="clickEx(MButton, wnd, Coord, Coord, bool)"/>
		/// <exception cref="Exception">Other exceptions. Depends on <i>obj</i> type.</exception>
		public static MRelease clickEx(MButton button, MObject obj, Coord x = default, Coord y = default) {
			switch (obj.Value) {
			case wnd w:
				return clickEx(button, w, x, y);
			case elm e:
				return e.MouseClick(x, y, button);
			case uiimage u:
				return u.MouseClick(x, y, button);
			case RECT r:
				return mouse.clickEx(button, Coord.NormalizeInRect(x, y, r, centerIfEmpty: true));
			case (wnd w, RECT r):
				var p = Coord.NormalizeInRect(x, y, r, centerIfEmpty: true);
				return mouse.clickEx(button, w, p.x, p.y);
			case (wnd w, bool nonClient):
				return mouse.clickEx(button, w, x, y, nonClient);
			case (screen s, bool workArea):
				return mouse.clickEx(button, Coord.Normalize(x, y, workArea, s, centerIfEmpty: true));
			case bool useLastXY:
				return mouse.clickEx(button, _CoordToRelativeXY(x, y, useLastXY));
			case null: //default(MObject)
				return clickEx(button, x, y);
			}
			return default; //never
		}

		/// <summary>
		/// Clicks, double-clicks, presses or releases a mouse button at the specified position in screen.
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <param name="button">Button and action. Default: left click.</param>
		/// <exception cref="ArgumentException">Invalid <i>button</i> flags (multiple buttons or actions specified).</exception>
		/// <inheritdoc cref="click(Coord, Coord)"/>
		public static MRelease clickEx(MButton button, Coord x, Coord y) {
			POINT p = move(x, y);
			_Click(button, p);
			return button;
		}

		/// <param name="p">
		/// Coordinates.
		/// Tip: To specify coordinates relative to the right, bottom, work area or a non-primary screen, use <see cref="Coord.Normalize"/>, like in the example.
		/// </param>
		/// <inheritdoc cref="clickEx(MButton, Coord, Coord)"/>
		/// <example>
		/// Click at 100 200.
		/// <code><![CDATA[
		/// mouse.clickEx(MButton.Left, (100, 200));
		/// ]]></code>
		/// 
		/// Right-click at 50 from left and 100 from bottom of the work area.
		/// <code><![CDATA[
		/// mouse.clickEx(MButton.Right, Coord.Normalize(50, ^100, workArea: true));
		/// ]]></code>
		/// </example>
		public static MRelease clickEx(MButton button, POINT p) {
			move(p);
			_Click(button, p);
			return button;
		}

		/// <summary>
		/// Clicks, double-clicks, presses or releases a mouse button.
		/// By default does not move the mouse cursor.
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <param name="button">Button and action. Default: left click.</param>
		/// <param name="useLastXY">
		/// Use <see cref="lastXY"/>. It is the mouse cursor position set by the most recent "mouse move" or "mouse click" function called in this thread. Use this option for reliability.
		/// Example: <c>mouse.move(100, 100); mouse.clickEx(..., true);</c>. The click is always at 100 100, even if somebody changes cursor position between <c>mouse.move</c> sets it and <c>mouse.clickEx</c> uses it. In such case this option atomically moves the cursor to <b>lastXY</b>. This movement is instant and does not use <see cref="opt"/>.
		/// If <c>false</c> (default), clicks at the current cursor position (does not move it).
		/// </param>
		/// <exception cref="ArgumentException">Invalid <i>button</i> flags (multiple buttons or actions specified).</exception>
		/// <exception cref="Exception">If <i>lastXY</i> <c>true</c> and need to move the cursor - exceptions of <see cref="move(POINT)"/>.</exception>
		/// <exception cref="InputDesktopException"></exception>
		/// <remarks>
		/// Uses <see cref="opt.mouse"/>: <see cref="OMouse.ClickSpeed"/>, <see cref="OMouse.ClickSleepFinally"/> and maybe those used by <see cref="move(POINT)"/>.
		/// </remarks>
		public static MRelease clickEx(MButton button = MButton.Left, bool useLastXY = false) {
			POINT p;
			if (useLastXY) p = lastXY;
			else {
				p = xy;
				if (t_prevMousePos == null) t_prevMousePos = new _PrevMousePos(p); //sets .first=.last=p
				else t_prevMousePos.last = p;
			}
			_Click(button, p);
			return button;
		}

		/// <summary>
		/// Left button click at position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <param name="w">Window or control.</param>
		/// <param name="x">X coordinate relative to the client area of <i>w</i>. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate relative to the client area of <i>w</i>. Default - center.</param>
		/// <param name="nonClient">The specified position is relative to the window rectangle, not to its client area.</param>
		/// <exception cref="AuWndException">
		/// - The specified position is not in the window (read more in Remarks).
		/// - Invalid window.
		/// - The window is hidden. No exception if just cloaked, for example in another desktop; then on click will activate, which usually uncloaks. No exception if <i>w</i> is a control.
		/// - Other window-related failures.
		/// </exception>
		/// <inheritdoc cref="move(POINT)" path="/exception"/>
		/// <remarks>
		/// To move the mouse cursor, calls <see cref="move(wnd, Coord, Coord, bool)"/>.
		/// If after moving the cursor it is not in the window (or a window of its thread), activates the window (or its top-level parent window). Throws exception if then <i>x y</i> is still not in the window. Skips all this when just releasing button or if option <b>Relaxed</b> is <c>true</c>. If <i>w</i> is a control, <i>x y</i> can be somewhere else in its top-level parent window.
		/// 
		/// Uses <see cref="opt.mouse"/>: <see cref="OMouse.MoveSpeed"/>, <see cref="OMouse.MoveSleepFinally"/> (between moving and clicking), <see cref="OMouse.ClickSpeed"/>, <see cref="OMouse.ClickSleepFinally"/>, <see cref="OMouse.Relaxed"/>.
		/// </remarks>
		public static void click(wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			clickEx(MButton.Left, w, x, y, nonClient);
		}

		/// <summary>
		/// Left button click at position <i>x y</i>.
		/// </summary>
		/// <param name="x">X coordinate in the screen. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate in the screen.</param>
		/// <inheritdoc cref="move(POINT)" path="/exception"/>
		/// <remarks>
		/// Uses <see cref="opt.mouse"/>: <see cref="OMouse.ClickSpeed"/>, <see cref="OMouse.ClickSleepFinally"/> and those used by <see cref="move(POINT)"/>.
		/// </remarks>
		public static void click(Coord x, Coord y) {
			//note: most Click functions don't have a workArea and screen parameter. It is rarely used. For reliability better use the overloads that use window coordinates.

			clickEx(MButton.Left, x, y);
		}

		/// <summary>
		/// Left button click.
		/// </summary>
		/// <param name="useLastXY">Use <see cref="lastXY"/>, not current cursor position. More info: <see cref="clickEx(MButton, bool)"/>.</param>
		/// <exception cref="Exception">If <i>lastXY</i> <c>true</c> and need to move the cursor - exceptions of <see cref="move(POINT)"/>.</exception>
		/// <exception cref="InputDesktopException"></exception>
		/// <remarks>
		/// Uses <see cref="opt.mouse"/>: <see cref="OMouse.ClickSpeed"/>, <see cref="OMouse.ClickSleepFinally"/> and maybe those used by <see cref="move(POINT)"/>.
		/// </remarks>
		public static void click(bool useLastXY = false) {
			clickEx(MButton.Left, useLastXY);
		}

		/// <summary>
		/// Right button click at position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <inheritdoc cref="click(wnd, Coord, Coord, bool)"/>
		public static void rightClick(wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			clickEx(MButton.Right, w, x, y, nonClient);
		}

		/// <summary>
		/// Right button click at position <i>x y</i>.
		/// </summary>
		/// <inheritdoc cref="click(Coord, Coord)"/>
		public static void rightClick(Coord x, Coord y) {
			clickEx(MButton.Right, x, y);
		}

		/// <summary>
		/// Right button click.
		/// </summary>
		/// <inheritdoc cref="click(bool)"/>
		public static void rightClick(bool useLastXY = false) {
			clickEx(MButton.Right, useLastXY);
		}

		/// <summary>
		/// Left button double click at position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <inheritdoc cref="click(wnd, Coord, Coord, bool)"/>
		public static void doubleClick(wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			clickEx(MButton.Left | MButton.DoubleClick, w, x, y, nonClient);
		}

		/// <summary>
		/// Left button double click at position <i>x y</i>.
		/// </summary>
		/// <inheritdoc cref="click(Coord, Coord)"/>
		public static void doubleClick(Coord x, Coord y) {
			clickEx(MButton.Left | MButton.DoubleClick, x, y);
		}

		/// <summary>
		/// Left button double click.
		/// </summary>
		/// <inheritdoc cref="click(bool)"/>
		public static void doubleClick(bool useLastXY = false) {
			clickEx(MButton.Left | MButton.DoubleClick, useLastXY);
		}

		/// <summary>
		/// Left down (press and don't release) at position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <inheritdoc cref="click(wnd, Coord, Coord, bool)"/>
		public static MRelease leftDown(wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			return clickEx(MButton.Left | MButton.Down, w, x, y, nonClient);
		}

		/// <summary>
		/// Left button down (press and don't release) at position <i>x y</i>.
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <inheritdoc cref="click(Coord, Coord)"/>
		public static MRelease leftDown(Coord x, Coord y) {
			return clickEx(MButton.Left | MButton.Down, x, y);
		}

		/// <summary>
		/// Left button down (press and don't release).
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <inheritdoc cref="click(bool)"/>
		public static MRelease leftDown(bool useLastXY = false) {
			return clickEx(MButton.Left | MButton.Down, useLastXY);
		}

		/// <summary>
		/// Left button up (release pressed button) at position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <inheritdoc cref="click(wnd, Coord, Coord, bool)"/>
		public static void leftUp(wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			clickEx(MButton.Left | MButton.Up, w, x, y, nonClient);
		}

		/// <summary>
		/// Left button up (release pressed button) at position <i>x y</i>.
		/// </summary>
		/// <inheritdoc cref="click(Coord, Coord)"/>
		public static void leftUp(Coord x, Coord y) {
			clickEx(MButton.Left | MButton.Up, x, y);
		}

		/// <summary>
		/// Left button up (release pressed button).
		/// </summary>
		/// <inheritdoc cref="click(bool)"/>
		public static void leftUp(bool useLastXY = false) {
			clickEx(MButton.Left | MButton.Up, useLastXY);
		}

		/// <summary>
		/// Right button down (press and don't release) at position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <inheritdoc cref="click(wnd, Coord, Coord, bool)"/>
		public static MRelease rightDown(wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			return clickEx(MButton.Right | MButton.Down, w, x, y, nonClient);
		}

		/// <summary>
		/// Right button down (press and don't release) at position <i>x y</i>.
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <inheritdoc cref="click(Coord, Coord)"/>
		public static MRelease rightDown(Coord x, Coord y) {
			return clickEx(MButton.Right | MButton.Down, x, y);
		}

		/// <summary>
		/// Right button down (press and don't release).
		/// </summary>
		/// <returns>The return value can be used to auto-release the pressed button. Example: <see cref="MRelease"/>.</returns>
		/// <inheritdoc cref="click(bool)"/>
		public static MRelease rightDown(bool useLastXY = false) {
			return clickEx(MButton.Right | MButton.Down, useLastXY);
		}

		/// <summary>
		/// Right button up (release pressed button) at position <i>x y</i> relative to window <i>w</i>.
		/// </summary>
		/// <inheritdoc cref="click(wnd, Coord, Coord, bool)"/>
		public static void rightUp(wnd w, Coord x = default, Coord y = default, bool nonClient = false) {
			clickEx(MButton.Right | MButton.Up, w, x, y, nonClient);
		}

		/// <summary>
		/// Right button up (release pressed button) at position <i>x y</i>.
		/// </summary>
		/// <inheritdoc cref="click(Coord, Coord)"/>
		public static void rightUp(Coord x, Coord y) {
			clickEx(MButton.Right | MButton.Up, x, y);
		}

		/// <summary>
		/// Right button up (release pressed button).
		/// </summary>
		/// <inheritdoc cref="click(bool)"/>
		public static void rightUp(bool useLastXY = false) {
			clickEx(MButton.Right | MButton.Up, useLastXY);
		}

		/// <summary>
		/// Mouse wheel forward or backward.
		/// </summary>
		/// <param name="ticks">Number of wheel ticks forward (positive) or backward (negative).</param>
		/// <param name="horizontal">Horizontal wheel.</param>
		/// <remarks>
		/// Uses <see cref="opt.mouse"/>: <see cref="OMouse.ClickSleepFinally"/>.
		/// </remarks>
		/// <exception cref="InputDesktopException"></exception>
		public static void wheel(double ticks, bool horizontal = false) {
			bool neg = ticks < 0; if (neg) ticks = -ticks;
			ticks *= 120;
			while (ticks > 0) {
				short t = (short)(ticks < 30000 ? Math.Ceiling(ticks) : 30000); //max 250 full ticks
				_SendRaw(horizontal ? Api.IMFlags.HWheel : Api.IMFlags.Wheel, 0, 0, neg ? -t : t);
				ticks -= t;
			}
			_Sleep(opt.mouse.ClickSleepFinally);
		}

		//rejected. Not so often used. It's easy to move(); wheel().
		///// <summary>
		///// Mouse move and wheel.
		///// </summary>
		//public static void wheel(Coord x, Coord y, double ticks, bool horizontal = false) {
		//	move(x, y);
		//	wheel(ticks, horizontal);
		//}

		///// <summary>
		///// Mouse move and wheel.
		///// </summary>
		//public static void wheel(wnd w, Coord x, Coord y, double ticks, bool horizontal = false) {
		//	move(w, x, y);
		//	wheel(ticks, horizontal);
		//}

		/// <summary>
		/// Presses a mouse button in object <i>o1</i>, moves the mouse cursor to object <i>o2</i> and releases the button.
		/// </summary>
		/// <param name="o1">UI object (window, UI element, etc) where to press the mouse button.</param>
		/// <param name="o2">UI object where to release the mouse button.</param>
		/// <param name="x1">X offset in <i>o1</i> rectangle. Default: center.</param>
		/// <param name="y1">Y offset in <i>o1</i> rectangle. Default: center.</param>
		/// <param name="x2">X offset in <i>o2</i> rectangle. Default: center.</param>
		/// <param name="y2">Y offset in <i>o2</i> rectangle. Default: center.</param>
		/// <param name="button">Mouse button. Default: left.</param>
		/// <param name="mod">Modifier keys (<c>Ctrl</c> etc).</param>
		/// <param name="sleep">Wait this number of milliseconds after pressing the mouse button.</param>
		/// <param name="speed">The drag speed. See <see cref="OMouse.MoveSpeed"/>.</param>
		/// <inheritdoc cref="clickEx(MButton, MObject, Coord, Coord)" path="/exception"/>
		public static void drag(MObject o1, MObject o2, Coord x1 = default, Coord y1 = default, Coord x2 = default, Coord y2 = default, MButton button = MButton.Left, KMod mod = 0, int sleep = 0, int speed = 5) {
			if (o2.Value is bool useLastXY) { //get mouse position before moving to o1
				var p = _CoordToRelativeXY(x2, y2, useLastXY);
				o2 = default; x2 = p.x; y2 = p.y;
			}
			_Drag(o1, x1, y1, button, mod, sleep, speed, () => move(o2, x2, y2));
		}

		/// <summary>
		/// Presses a mouse button in object <i>obj</i>, moves the mouse cursor by offset <i>dx</i> <i>dy</i> and releases the button.
		/// </summary>
		/// <param name="obj">UI object (window, UI element, etc) where to press the mouse button.</param>
		/// <param name="x">X offset in <i>obj</i> rectangle. Default: center.</param>
		/// <param name="y">Y offset in <i>obj</i> rectangle. Default: center.</param>
		/// <param name="dx">X offset from the start position.</param>
		/// <param name="dy">Y offset from the start position.</param>
		/// <param name="button">Mouse button. Default: left.</param>
		/// <param name="mod">Modifier keys (<c>Ctrl</c> etc).</param>
		/// <param name="sleep">Wait this number of milliseconds after pressing the mouse button.</param>
		/// <param name="speed">The drag speed. See <see cref="OMouse.MoveSpeed"/>.</param>
		/// <inheritdoc cref="clickEx(MButton, MObject, Coord, Coord)" path="/exception"/>
		public static void drag(MObject obj, Coord x, Coord y, int dx, int dy, MButton button = MButton.Left, KMod mod = 0, int sleep = 0, int speed = 5) {
			_Drag(obj, x, y, button, mod, sleep, speed, () => moveBy(dx, dy));
		}

		/// <summary>
		/// Presses a mouse button in object <i>obj</i>, moves the mouse cursor using multiple recorded offsets and releases the button.
		/// </summary>
		/// <param name="obj">UI object (window, UI element, etc) where to press the mouse button.</param>
		/// <param name="x">X offset in <i>obj</i> rectangle. Default: center.</param>
		/// <param name="y">Y offset in <i>obj</i> rectangle. Default: center.</param>
		/// <param name="offsets">String containing multiple x y offsets from the start position. See <see cref="moveBy(string, double)"/>.</param>
		/// <param name="button">Mouse button. Default: left.</param>
		/// <param name="mod">Modifier keys (<c>Ctrl</c> etc).</param>
		/// <param name="sleep">Wait this number of milliseconds after pressing the mouse button.</param>
		/// <inheritdoc cref="clickEx(MButton, MObject, Coord, Coord)" path="/exception"/>
		public static void drag(MObject obj, Coord x, Coord y, string offsets, MButton button = MButton.Left, KMod mod = 0, int sleep = 0) {
			_Drag(obj, x, y, button, mod, sleep, 0, () => moveBy(offsets));
		}

		static void _Drag(MObject from, Coord x1, Coord y1, MButton button, KMod mod, int sleep, int speed, Action action) {
			if (button == 0) button = MButton.Left; else if (button != (button & (MButton.Left | MButton.Right | MButton.Middle | MButton.X1 | MButton.X2))) throw new ArgumentException(null, nameof(button));
			if ((uint)sleep >= 10000) throw new ArgumentException(null, nameof(sleep));
			if ((uint)speed >= 10000) throw new ArgumentException(null, nameof(speed));
			int speed0 = opt.mouse.MoveSpeed;
			bool isMod = false, isButton = false;
			try {
				clickEx(button | MButton.Down, from, x1, y1);
				isButton = true;

				if (sleep > 0) wait.ms(sleep);

				_SendMod(true); //note: after button down. If before, it could eg Ctrl+select multiple objects instead of one.
				isMod = true;

				opt.mouse.MoveSpeed = speed;

				action();
			}
			finally {
				if (isButton) clickEx(button | MButton.Up, useLastXY: true);
				opt.mouse.MoveSpeed = speed0;
				if (isMod) _SendMod(false);
			}

			void _SendMod(bool down) {
				if (mod == 0) return;
				var k = new keys(opt.key);
				if (mod.Has(KMod.Ctrl)) k.AddKey(KKey.Ctrl, down);
				if (mod.Has(KMod.Shift)) k.AddKey(KKey.Shift, down);
				if (mod.Has(KMod.Alt)) k.AddKey(KKey.Alt, down);
				if (mod.Has(KMod.Win)) k.AddKey(KKey.Win, down);
				k.SendNow(); //and sleeps opt.key.SleepFinally (default 10)
			}
		}

		//not used
		///// <summary>
		///// Releases mouse buttons pressed by this thread (<b>t_pressedButtons</b>).
		///// </summary>
		///// <param name="p">If not <c>null</c>, and XY is different, moves to this point. Used for reliability.</param>
		//static void _ReleaseButtons(POINT? p = null)
		//{
		//	var b = t_pressedButtons;
		//	if(0 != (b & MButtons.Left)) _Click(MButton.Left | MButton.Up, p);
		//	if(0 != (b & MButtons.Right)) _Click(MButton.Right | MButton.Up, p);
		//	if(0 != (b & MButtons.Middle)) _Click(MButton.Middle | MButton.Up, p);
		//	if(0 != (b & MButtons.X1)) _Click(MButton.X1 | MButton.Up, p);
		//	if(0 != (b & MButtons.X2)) _Click(MButton.X2 | MButton.Up, p);
		//}
		//rejected: finally release script-pressed buttons, especially on exception. Instead let use code: using(mouse.leftDown(...)), it auto-releases pressed button.

		/// <summary>
		/// Returns <c>true</c> if some mouse buttons are pressed.
		/// </summary>
		/// <param name="buttons">Return <c>true</c> if some of these buttons are down. Default: any.</param>
		/// <remarks>
		/// Uses API <msdn>GetAsyncKeyState</msdn>.
		/// When processing user input in UI code (forms, WPF), instead use class <see cref="keys.gui"/> or .NET functions. They use API <msdn>GetKeyState</msdn>.
		/// When mouse left and right buttons are swapped, gets logical state, not physical.
		/// </remarks>
		/// <seealso cref="waitForNoButtonsPressed"/>
		public static bool isPressed(MButtons buttons = MButtons.Left | MButtons.Right | MButtons.Middle | MButtons.X1 | MButtons.X2) {
			if (0 != (buttons & MButtons.Left) && keys.isPressed(KKey.MouseLeft)) return true;
			if (0 != (buttons & MButtons.Right) && keys.isPressed(KKey.MouseRight)) return true;
			if (0 != (buttons & MButtons.Middle) && keys.isPressed(KKey.MouseMiddle)) return true;
			if (0 != (buttons & MButtons.X1) && keys.isPressed(KKey.MouseX1)) return true;
			if (0 != (buttons & MButtons.X2) && keys.isPressed(KKey.MouseX2)) return true;
			return false;
		}

		//rejected: not useful.
		///// <summary>
		///// Returns a value indicating which mouse buttons are pressed.
		///// </summary>
		///// <param name="buttons">Check only these buttons. Default: all.</param>
		///// <remarks>See <see cref="IsPressed"/>.</remarks>
		//public static MButtons buttons(MButtons buttons = MButtons.Left | MButtons.Right | MButtons.Middle | MButtons.X1 | MButtons.X2)
		//{
		//	MButtons R = 0;
		//	if(0 != (buttons & MButtons.Left) && keys.isKey(KKey.MouseLeft)) R |= MButtons.Left;
		//	if(0 != (buttons & MButtons.Right) && keys.isKey(KKey.MouseRight)) R |= MButtons.Right;
		//	if(0 != (buttons & MButtons.Middle) && keys.isKey(KKey.MouseMiddle)) R |= MButtons.Middle;
		//	if(0 != (buttons & MButtons.X1) && keys.isKey(KKey.MouseX1)) return R |= MButtons.X1;
		//	if(0 != (buttons & MButtons.X2) && keys.isKey(KKey.MouseX2)) return R |= MButtons.X2;
		//	return R;
		//}

		//rejected: rarely used. Can use IsPressed.
		///// <summary>
		///// Returns <c>true</c> if the left mouse button is pressed.
		///// </summary>
		///// <remarks>See <see cref="IsPressed"/>.</remarks>
		//public static bool isLeft => keys.isPressed(KKey.MouseLeft);

		///// <summary>
		///// Returns <c>true</c> if the right mouse button is pressed.
		///// </summary>
		///// <remarks>See <see cref="IsPressed"/>.</remarks>
		//public static bool isRight => keys.isPressed(KKey.MouseRight);

		/// <summary>
		/// Waits while some mouse buttons are pressed. See <see cref="isPressed"/>.
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout). Default 0.</param>
		/// <param name="buttons">Wait only for these buttons. Default - all.</param>
		/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <seealso cref="keys.waitForNoModifierKeysAndMouseButtons"/>
		public static bool waitForNoButtonsPressed(Seconds timeout = default, MButtons buttons = MButtons.Left | MButtons.Right | MButtons.Middle | MButtons.X1 | MButtons.X2) {
			return keys.waitForNoModifierKeysAndMouseButtons(timeout, 0, buttons);
		}

		/// <summary>
		/// Waits while some buttons are pressed, except those pressed by a <see cref="mouse"/> class function in this thread.
		/// Does nothing if option <b>Relaxed</b> is <c>true</c>.
		/// </summary>
		internal static void WaitForNoButtonsPressed_() {
			//not public, because we have WaitForNoButtonsPressed, which is unaware about script-pressed buttons, and don't need this awareness because the script author knows what is pressed by that script

			if (opt.mouse.Relaxed) return;
			var mb = (MButtons.Left | MButtons.Right | MButtons.Middle | MButtons.X1 | MButtons.X2)
				& ~t_pressedButtons;
			if (waitForNoButtonsPressed(-5, mb)) return;
			print.warning("Info: Waiting for releasing mouse buttons. See opt.mouse.Relaxed.");
			waitForNoButtonsPressed(0, mb);
		}

		/// <summary>
		/// Waits for button-down or button-up event of the specified mouse button or buttons.
		/// </summary>
		/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="button">Mouse button. If several buttons specified, waits for any of them.</param>
		/// <param name="up">Wait for button-up event.</param>
		/// <param name="block">Make the event invisible for other apps. If <i>up</i> is <c>true</c>, makes the down event invisible too, if it comes while waiting for the up event.</param>
		/// <exception cref="ArgumentException"><i>button</i> is 0.</exception>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <remarks>
		/// Unlike <see cref="waitForNoButtonsPressed"/>, waits for down or up event, not for button state.
		/// Uses low-level mouse hook.
		/// Ignores mouse events injected by functions of this library.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// mouse.waitForClick(0, MButtons.Left, up: true, block: false);
		/// print.it("click");
		/// ]]></code>
		/// </example>
		public static bool waitForClick(Seconds timeout, MButtons button, bool up = false, bool block = false) {
			if (button == 0) throw new ArgumentException();
			return 0 != _WaitForClick(timeout, button, up, block);
		}

		/// <summary>
		/// Waits for button-down or button-up event of any mouse button, and gets the button code.
		/// </summary>
		/// <returns>Returns the button code. On timeout returns 0 if <i>timeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <example>
		/// <code><![CDATA[
		/// var button = mouse.waitForClick(0, up: true, block: true);
		/// print.it(button);
		/// ]]></code>
		/// </example>
		/// <inheritdoc cref="waitForClick(Seconds, MButtons, bool, bool)" path="/param"/>
		public static MButtons waitForClick(Seconds timeout, bool up = false, bool block = false) {
			return _WaitForClick(timeout, 0, up, block);
		}

		static MButtons _WaitForClick(Seconds timeout, MButtons button, bool up, bool block) {
			//info: this and related functions use similar code as keys._WaitForKey.

			MButtons R = 0;
			using (WindowsHook.Mouse(x => {
				MButtons b = 0;
				switch (x.Event) {
				case HookData.MouseEvent.LeftButton: b = MButtons.Left; break;
				case HookData.MouseEvent.RightButton: b = MButtons.Right; break;
				case HookData.MouseEvent.MiddleButton: b = MButtons.Middle; break;
				case HookData.MouseEvent.X1Button: b = MButtons.X1; break;
				case HookData.MouseEvent.X2Button: b = MButtons.X2; break;
				}
				if (b == 0) return;
				if (button != 0 && !button.Has(b)) return;
				if (x.IsButtonUp != up) {
					if (up && block) { //button down when we are waiting for up. If block, now block down too.
						if (button == 0) button = b;
						x.BlockEvent();
					}
					return;
				}
				R = b;
				if (block) x.BlockEvent();
			})) wait.doEventsUntil(timeout, () => R != 0);

			return R;
		}
		//FUTURE:
		//	waitForWheel(Seconds timeout, bool? forward, bool block = false)
		//	waitForMouseMove, waitForMouseStop.
		//	In QM2 these functions were created because somebody asked, but I don't use.

		/// <summary>
		/// Waits for a standard mouse cursor (pointer) visible.
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="cursor">Id of a standard cursor.</param>
		/// <param name="not">Wait until this cursor disappears.</param>
		/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		public static bool waitForCursor(Seconds timeout, MCursor cursor, bool not = false) {
			IntPtr hcur = Api.LoadCursor(default, cursor);
			if (hcur == default) throw new AuException(0, "*load cursor");

			return wait.until(timeout, () => (MouseCursor.GetCurrentVisibleCursor(out var c) && c == hcur) ^ not);
		}

		/// <summary>
		/// Waits for a nonstandard mouse cursor (pointer) visible.
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="cursorHash">Cursor hash, as returned by <see cref="MouseCursor.Hash"/>.</param>
		/// <param name="not">Wait until this cursor disappears.</param>
		/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		public static bool waitForCursor(Seconds timeout, long cursorHash, bool not = false) {
			if (cursorHash == 0) throw new ArgumentException();
			return wait.until(timeout, () => (MouseCursor.GetCurrentVisibleCursor(out var c) && MouseCursor.Hash(c) == cursorHash) ^ not);
		}

		/// <summary>
		/// Posts mouse-click messages to the window.
		/// </summary>
		/// <param name="w">Window or control.</param>
		/// <param name="x">X coordinate in <b>w</b> client area or <b>rect</b>. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate in <b>w</b> client area or <b>rect</b>. Default - center.</param>
		/// <param name="button">Can specify the left (default), right or middle button. Also flag for double-click, press or release.</param>
		/// <param name="rect">A rectangle in <b>w</b> client area. If <c>null</c> (default), <i>x y</i> are relative to the client area.</param>
		/// <exception cref="AuWndException">Invalid window.</exception>
		/// <exception cref="ArgumentException">Unsupported button specified.</exception>
		/// <remarks>
		/// Does not move the mouse.
		/// Does not wait until the target application finishes processing the message.
		/// Works not with all windows.
		/// </remarks>
		public static void postClick(wnd w, Coord x = default, Coord y = default, MButton button = MButton.Left, RECT? rect = null) {
			RECT r;
			if (rect != null) {
				r = rect.Value;
				w.ThrowIfInvalid();
			} else {
				if (!w.GetClientRect(out r)) w.ThrowUseNative();
			}
			PostClick_(w, r, x, y, button);
		}

		internal static void PostClick_(wnd w, RECT r, Coord x = default, Coord y = default, MButton button = MButton.Left) {
			MButton mask = MButton.Down | MButton.Up | MButton.DoubleClick, b = button & ~mask, dud = button & mask;
			if (b == 0) b = MButton.Left;
			int m = b switch {
				MButton.Left => Api.WM_LBUTTONDOWN,
				MButton.Right => Api.WM_RBUTTONDOWN,
				MButton.Middle => Api.WM_MBUTTONDOWN,
				_ => throw new ArgumentException("supported buttons: left, right, middle")
			};
			if (dud is not (0 or MButton.Down or MButton.Up or MButton.DoubleClick)) throw new ArgumentException();

			POINT point = Coord.NormalizeInRect(x, y, r, centerIfEmpty: true);

			//if the control is mouse-transparent, use its ancestor. Example: the color selection controls in the classic color dialog.
			if (w.HasStyle(WS.CHILD)) {
				var w2 = w; var ps = point; w.MapClientToScreen(ref ps);
				while (!w2.Is0 && Api.HTTRANSPARENT == w2.Send(Api.WM_NCHITTEST, 0, Math2.MakeLparam(ps))) w2 = w2.Get.DirectParent;
				if (w2 != w && !w2.Is0) { w.MapClientToClientOf(w2, ref point); w = w2; }
			}

			using var workaround = new ButtonPostClickWorkaround_(w);

			nint xy = Math2.MakeLparam(point);
			nint mk = 0; if (keys.isCtrl) mk |= Api.MK_CONTROL; if (keys.isShift) mk |= Api.MK_SHIFT;
			nint mk1 = mk; if (dud != MButton.Up) mk1 |= b switch { MButton.Left => Api.MK_LBUTTON, MButton.Right => Api.MK_RBUTTON, _ => Api.MK_MBUTTON };
			if (dud != MButton.Up) w.Post(m, mk1, xy);
			if (dud != MButton.Down) {
				w.Post(Api.WM_MOUSEMOVE, mk1, xy);
				w.Post(m + 1, mk, xy);
			}
			if (dud == MButton.DoubleClick) {
				w.Post(m + 2, mk1, xy);
				w.Post(m + 1, mk, xy);
			}
			//_MinimalSleep(); //don't need. Eg elm.Invoke() does not wait too.

			//never mind: support nonclient (WM_NCRBUTTONDOWN etc)
		}

		/// <summary>
		/// Workaround for the documented <b>BM_CLICK</b>/<b>WM_LBUTTONDOWN</b> bug of classic button controls: randomly fails if inactive window.
		/// If <i>c</i> is a button in a dialog box, posts <b>WM_ACTIVATE</b> messages to the dialog box.
		/// </summary>
		internal ref struct ButtonPostClickWorkaround_ {
			readonly wnd _w;

			public ButtonPostClickWorkaround_(wnd c) {
				//this func is fast, but slower is elm.wndcontainer (to get c) and JIT
				_w = default;
				var w = c.Window; //not c.DirectParent. Eg in taskdialog it is not #32770. The postclick/invoke worked without this workaround in all tested top-level non-#32770 windows, with or without an intermediate #32770.
				if (w != c && !w.IsActive && w.ClassNameIs("#32770") && c.CommonControlType == WControlType.Button) {
					w.Post(Api.WM_ACTIVATE, 1);
					_w = w;
				}
			}

			public void Dispose() {
				if (!_w.Is0) _w.Post(Api.WM_ACTIVATE);
			}
		}
	}

#if unfinished
	public static partial class mouse
	{
		/// <summary>
		/// Sends a mouse click or wheel message directly to window or control.
		/// It often works even when the window is inactive and/or the mouse cursor is anywhere. However it often does not work altogether.
		/// </summary>
		public static class message
		{
			//not useful
			//public static void move(wnd w, Coord x=default, Coord y=default, bool nonClient = false, int waitMS=0)
			//{

			//}

			/// <summary>
			/// Sends a mouse click, double-click, down or up message(s) directly to window or control.
			/// </summary>
			/// <param name="w">Window or control.</param>
			/// <param name="x">X coordinate relative to the client area, to send with the message.</param>
			/// <param name="y">Y coordinate relative to the client area, to send with the message.</param>
			/// <param name="button"></param>
			/// <param name="nonClient"><i>x y</i> are relative to the window rectangle. By default they are relative to the client area.</param>
			/// <exception cref="AuException">Failed.</exception>
			/// <remarks>
			/// Does not move the mouse cursor, therefore does not work if the window gets cursor position not from the message.
			/// Does not activate the window (unless the window activates itself).
			/// </remarks>
			public static void click(wnd w, Coord x=default, Coord y=default, MButton button = MButton.Left, bool nonClient = false, bool isCtrl=false, bool isShift=false/*, int waitMS = 0*/)
			{

			}

			/// <summary>
			/// Sends a mouse wheel message (<b>WM_MOUSEWHEEL</b>) directly to window or control.
			/// </summary>
			/// <param name="w">Window or control.</param>
			/// <param name="ticks">Number of wheel ticks forward (positive) or backward (negative).</param>
			public static void wheel(wnd w, int ticks, bool isCtrl=false, bool isShift=false/*, int waitMS = 0*/)
			{

			}

			static void _Send(wnd w, uint message, uint wParam, uint lParam, bool isCtrl, bool isShift)
			{
				if(isCtrl || keys.isCtrl) wParam |= 8; //Api.MK_CONTROL
				if(isShift || keys.isShift) wParam |= 4; //Api.MK_SHIFT
				if(!w.Post(message, wParam, lParam)) throw new AuException(0);
				_SleepMax(-1, w.IsOfThisThread);
			}

			//rejected:
			///// <param name="waitMS">
			///// Maximal time to wait, milliseconds. Also which API to use.
			///// If 0 (default), calls API <msdn>PostMessage</msdn> (it does not wait) and waits <c>opt.mouse.ClickSpeed</c> ms.
			///// If less than 0 (eg <b>Timeout.Infinite</b>), calls API <msdn>SendMessage</msdn> which usually waits until the window finishes to process the message.
			///// Else calls API <msdn>SendMessageTimeout</msdn> which waits max <i>waitMS</i> milliseconds, then throws <b>AuException</b>.
			///// The SendX functions are not natural and less likely to work.
			///// If the window shows a dialog, the SendX functions usually wait until the dialog is closed.
			///// </param>
			///// <exception cref="AuException">Failed, or timeout.</exception>
			//static void _SendOrPost(int waitMS, wnd w, uint message, nint wParam, nint lParam)
			//{
			//	bool ok;
			//	if(message == 0) {
			//		ok=w.Post(message, wParam, lParam);
			//		_Sleep(opt.mouse.ClickSpeed);
			//	} else if(waitMS < 0) {
			//		w.Send(message, wParam, lParam);
			//		ok = true;
			//	} else {
			//		ok = w.SendTimeout(waitMS, message, wParam, lParam);
			//	}
			//	if(!ok) throw new AuException(0);
			//}
		}
	}
#endif
}

namespace Au.Types {
	/// <summary>
	/// <i>button</i> parameter type for <see cref="mouse.clickEx(MButton, bool)"/> and similar functions.
	/// </summary>
	/// <remarks>
	/// There are two groups of values:
	/// 1. Button (<b>Left</b>, <b>Right</b>, <b>Middle</b>, <b>X1</b>, <b>X2</b>). Default or 0: <b>Left</b>.
	/// 2. Action (<b>Down</b>, <b>Up</b>, <b>DoubleClick</b>). Default: click.
	/// 
	/// Multiple values from the same group cannot be combined. For example <c>Left|Right</c> is invalid.
	/// Values from different groups can be combined. For example <c>Right|Down</c>.
	/// </remarks>
	[Flags]
	public enum MButton {
		/// <summary>The left button.</summary>
		Left = 1,

		/// <summary>The right button.</summary>
		Right = 2,

		/// <summary>The middle button.</summary>
		Middle = 4,

		/// <summary>The 4-th button.</summary>
		X1 = 8,

		/// <summary>The 5-th button.</summary>
		X2 = 16,

		//rejected: not necessary. Can be confusing.
		///// <summary>
		///// Click (press and release).
		///// This is default. Value 0.
		///// </summary>
		//Click = 0,

		/// <summary>(flag) Press and don't release.</summary>
		Down = 32,

		/// <summary>(flag) Don't press, only release.</summary>
		Up = 64,

		/// <summary>(flag) Double-click.</summary>
		DoubleClick = 128,
	}

	/// <summary>
	/// Flags for mouse buttons.
	/// Used with functions that check mouse button states (pressed or not).
	/// </summary>
	/// <remarks>
	/// The values are the same as <see cref="System.Windows.Forms.MouseButtons"/>, therefore can be cast to/from.
	/// </remarks>
	[Flags]
	public enum MButtons {
		/// <summary>The left button.</summary>
		Left = 0x00100000,

		/// <summary>The right button.</summary>
		Right = 0x00200000,

		/// <summary>The middle button.</summary>
		Middle = 0x00400000,

		/// <summary>The 4-th button.</summary>
		X1 = 0x00800000,

		/// <summary>The 5-th button.</summary>
		X2 = 0x01000000,
	}

	/// <summary>
	/// At the end of <c>using(...) { ... }</c> block releases mouse buttons pressed by the function that returned this variable. See example.
	/// </summary>
	/// <example>
	/// Drag and drop: start at x=8 y=8, move 20 pixels down, drop.
	/// <code><![CDATA[
	/// using(mouse.leftDown(w, 8, 8)) mouse.moveBy(0, 20); //the button is auto-released when the 'using' code block ends
	/// ]]></code>
	/// </example>
	public struct MRelease : IDisposable {
		MButton _buttons;
		///
		public static implicit operator MRelease(MButton b) => new MRelease() { _buttons = b };

		/// <summary>
		/// Releases mouse buttons pressed by the function that returned this variable.
		/// </summary>
		public void Dispose() {
			if (0 == (_buttons & MButton.Down)) return;
			if (0 != (_buttons & MButton.Left)) mouse.clickEx(MButton.Left | MButton.Up, true);
			if (0 != (_buttons & MButton.Right)) mouse.clickEx(MButton.Right | MButton.Up, true);
			if (0 != (_buttons & MButton.Middle)) mouse.clickEx(MButton.Middle | MButton.Up, true);
			if (0 != (_buttons & MButton.X1)) mouse.clickEx(MButton.X1 | MButton.Up, true);
			if (0 != (_buttons & MButton.X2)) mouse.clickEx(MButton.X2 | MButton.Up, true);
		}
	}

	/// <summary>
	/// Standard cursor ids.
	/// Used with <see cref="mouse.waitForCursor(Seconds, MCursor, bool)"/>.
	/// </summary>
	public enum MCursor {
		/// <summary>Standard arrow.</summary>
		Arrow = 32512,

		/// <summary>I-beam (text editing).</summary>
		IBeam = 32513,

		/// <summary>Hourglass.</summary>
		Wait = 32514,

		/// <summary>Crosshair.</summary>
		Cross = 32515,

		/// <summary>Vertical arrow.</summary>
		UpArrow = 32516,

		/// <summary>Double-pointed arrow pointing northwest and southeast.</summary>
		SizeNWSE = 32642,

		/// <summary>Double-pointed arrow pointing northeast and southwest.</summary>
		SizeNESW = 32643,

		/// <summary>Double-pointed arrow pointing west and east.</summary>
		SizeWE = 32644,

		/// <summary>Double-pointed arrow pointing north and south.</summary>
		SizeNS = 32645,

		/// <summary>Four-pointed arrow pointing north, south, east, and west.</summary>
		SizeAll = 32646,

		/// <summary>Slashed circle.</summary>
		No = 32648,

		/// <summary>Hand.</summary>
		Hand = 32649,

		/// <summary>Standard arrow and small hourglass.</summary>
		AppStarting = 32650,

		/// <summary>Arrow and question mark.</summary>
		Help = 32651,
	}

	/// <summary>
	/// This type is used for parameters of <see cref="mouse"/> functions that accept multiple types of UI objects (window, UI element, screen, etc).
	/// Has implicit conversions from <b>wnd</b>, <b>elm</b>, <b>uiimage</b>, <b>screen</b>, <b>RECT</b> and <b>bool</b> (relative coordinates). Also has static functions to specify more parameters.
	/// </summary>
	public struct MObject {
		object _o;
		MObject(object o) => _o = o;

		///
		public object Value => _o;

		/// <summary>
		/// Allows to specify coordinates in the client area of a window or control.
		/// </summary>
		/// <exception cref="AuWndException">The window handle is 0.</exception>
		/// <seealso cref="Window(wnd, bool)"/>
		public static implicit operator MObject(wnd w) { w.ThrowIf0(); return new(w); }

		/// <summary>
		/// Allows to specify coordinates in the rectangle of a UI element.
		/// </summary>
		/// <exception cref="ArgumentNullException"/>
		public static implicit operator MObject(elm e) => new(Not_.NullRet(e));

		/// <summary>
		/// Allows to specify coordinates in the rectangle of an image found in a window etc.
		/// </summary>
		/// <exception cref="ArgumentNullException"/>
		public static implicit operator MObject(uiimage i) => new(Not_.NullRet(i));

		/// <summary>
		/// Allows to specify coordinates in a rectangle anywhere on screen.
		/// </summary>
		/// <seealso cref="RectInWindow(wnd, RECT)"/>
		public static implicit operator MObject(RECT r) => new(r);

		/// <summary>
		/// Allows to specify coordinates in a screen.
		/// </summary>
		/// <seealso cref="Screen(screen, bool)"/>
		public static implicit operator MObject(screen s) => new((s, false));

		/// <summary>
		/// Allows to specify coordinates relative to <see cref="mouse.xy"/> or <see cref="mouse.lastXY"/>.
		/// </summary>
		public static implicit operator MObject(bool useLastXY) => new(useLastXY);

		/// <summary>
		/// Allows to specify coordinates in a screen, either in the work area or in entire rectangle.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// mouse.move(MObject.Screen(screen.primary, true), ^10, ^10); //near the bottom-right corner of the work area of the primary screen
		/// ]]></code>
		/// </example>
		public static MObject Screen(screen s, bool workArea) => new((s, workArea));

		/// <summary>
		/// Allows to specify coordinates in a window or control, either in the client area or in entire rectangle.
		/// </summary>
		/// <exception cref="AuWndException">The window handle is 0.</exception>
		public static MObject Window(wnd w, bool nonClient) { w.ThrowIf0(); return new((w, true)); }

		/// <summary>
		/// Allows to specify coordinates in a rectangle in the client area of a window or control.
		/// </summary>
		/// <exception cref="AuWndException">The window handle is 0.</exception>
		public static MObject RectInWindow(wnd w, RECT r) { w.ThrowIf0(); return new((w, r)); }
	}
}
