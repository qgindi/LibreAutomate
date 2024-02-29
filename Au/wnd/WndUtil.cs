namespace Au.More {
	/// <summary>
	/// Miscellaneous window-related functions. Rarely used in automation scripts.
	/// </summary>
	public static class WndUtil {
		//public void ShowAnimate(bool show)
		//{
		//	//Don't add wnd function, because:
		//		//Rarely used.
		//		//Api.AnimateWindow() works only with windows of current thread.
		//		//Only programmers would need it, and they can call the API directly.
		//}

		/// <summary>
		/// Registers new window class in this process.
		/// </summary>
		/// <param name="className">Class name.</param>
		/// <param name="wndProc">
		/// Delegate of a window procedure. See <msdn>Window Procedures</msdn>.
		/// 
		/// Use <c>null</c> when you need a different delegate (method or target object) for each window instance; create windows with <see cref="CreateWindow(WNDPROC, bool, string, string, WS, WSE, int, int, int, int, wnd, nint, IntPtr, nint)"/> or <see cref="CreateMessageOnlyWindow(WNDPROC, string, string)"/>.
		/// If not <c>null</c>, it must be a static named method; create windows with any other function, including API <msdn>CreateWindowEx</msdn>.
		/// </param>
		/// <param name="etc">
		/// Can be used to specify API <msdn>WNDCLASSEX</msdn> fields.
		/// To set cursor use field <b>mCursor</b> (standard cursor) or <b>hCursor</b> (native handle of a custom cursor).
		/// If <c>null</c>, this function sets arrow cursor and style <c>CS_VREDRAW | CS_HREDRAW</c>.
		/// </param>
		/// <exception cref="ArgumentException"><i>wndProc</i> is an instance method. Must be static method or <c>null</c>. If need instance method, use <c>null</c> here and pass <i>wndProc</i> to <see cref="CreateWindow"/>.</exception>
		/// <exception cref="InvalidOperationException">The class already registered with this function and different <i>wndProc</i> (another method or another target object).</exception>
		/// <exception cref="Win32Exception">Failed, for example if the class already exists and was registered not with this function.</exception>
		/// <remarks>
		/// Calls API <msdn>RegisterClassEx</msdn>.
		/// The window class is registered until this process ends. Don't need to unregister.
		/// If called next time for the same window class, does nothing if <i>wndProc</i> is equal to the previous (or both <c>null</c>). Then ignores <i>etc</i>. Throws exception if different.
		/// Thread-safe.
		/// Protects the <i>wndProc</i> delegate from GC.
		/// </remarks>
		public static unsafe void RegisterWindowClass(string className, WNDPROC wndProc = null, RWCEtc etc = null) {
			if (wndProc?.Target != null) throw new ArgumentException("wndProc must be static method or null");
			//never mind: Target of static lambda != null. Could use code `&& !wndProc.Target.GetType().FullName.Ends("+<>c")`, but it's undocumented and may stop working in new .NET version.

			lock (s_classes) {
				if (s_classes.TryGetValue(className, out var wpPrev)) {
					if (wpPrev != wndProc) throw new InvalidOperationException("Window class already registered"); //another method or another target object
					return;
				}
				var x = new Api.WNDCLASSEX(etc);

				fixed (char* pCN = className) {
					x.lpszClassName = pCN;
					if (wndProc != null) {
						x.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc);
					} else {
						x.lpfnWndProc = s_cwProcFP;
					}
					x.style |= Api.CS_GLOBALCLASS;

					if (0 == Api.RegisterClassEx(x)) throw new Win32Exception();
					//note: we don't return atom because: 1. Rarely used. 2. If assigned to an unused field, compiler may remove the function call.

					s_classes.Add(className, wndProc);
				}
			}
		}

		internal static bool IsClassRegistered_(string name, out WNDPROC wndProc) {
			lock (s_classes) {
				return s_classes.TryGetValue(name, out wndProc);
			}
		}

		static Dictionary<string, WNDPROC> s_classes = new(StringComparer.OrdinalIgnoreCase); //allows to find registered classes and protects their wndProc delegates from GC
		[ThreadStatic] static Dictionary<wnd, WNDPROC> t_windows; //allows to dispatch messages and protects wndProc delegates of windows created in this thread from GC

		static nint _CWProc(wnd w, int msg, nint wParam, nint lParam) {
			//PrintMsg(w, msg, wParam, lParam);
			if (t_cwUnsafe) {
				t_cwUnsafe = false;
				var wndProc = t_cwProc;
				t_cwProc = null;
				Api.SetWindowLongPtr(w, GWL.WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProc));
				//print.it("subclassed", w);
				return wndProc(w, msg, wParam, lParam);
			} else {
				var a = t_windows;
				if (a == null || !a.TryGetValue(w, out var wndProc)) {
					wndProc = t_cwProc;
					if (wndProc == null) {
						//print.it("DefWindowProc", w);
						return Api.DefWindowProc(w, msg, wParam, lParam); //creating not with our CreateWindow(wndProc, ...)
					}
					a[w] = wndProc;
					//print.it("added", a.Count, w);
					t_cwProc = null;
				}

				var R = wndProc(w, msg, wParam, lParam);

				if (msg == Api.WM_NCDESTROY) {
					a.Remove(w);
					//print.it("removed", a.Count, w);
				}

				return R;
			}
		}
		static WNDPROC s_cwProc; //GC
		static IntPtr s_cwProcFP = Marshal.GetFunctionPointerForDelegate(s_cwProc = _CWProc);
		[ThreadStatic] static WNDPROC t_cwProc;
		[ThreadStatic] static bool t_cwUnsafe;

		/// <summary>
		/// Creates native/unmanaged window (API <msdn>CreateWindowEx</msdn>) and sets its window procedure.
		/// </summary>
		/// <param name="wndProc">Window procedure.</param>
		/// <param name="keepAlive">
		/// Protect <i>wndProc</i> from GC (garbage collector) until the window is destroyed (message <msdn>WM_NCDESTROY</msdn> recived or thread ended).
		/// <para>IMPORTANT: In some cases it may prevent destroying the window until thread ends, and it can be a big memory leak. For example WPF then does not destroy <b>HwndHost</b>-ed controls. Then let <i>keepAlive</i>=<c>false</c> and manually manage <i>wndProc</i> lifetime, for example keep it as a field of the wrapper class.</para>
		/// </param>
		/// <exception cref="AuException">Failed to create window. Unlikely.</exception>
		/// <remarks>
		/// If the class was registered with <see cref="RegisterWindowClass"/> with <c>null</c> <i>wndProc</i>, the <i>wndProc</i> function will receive all messages. Else will not receive messages sent before <b>CreateWindowEx</b> returns (<b>WM_CREATE</b> etc).
		/// 
		/// To destroy the window can be used any function, including API <msdn>DestroyWindow</msdn>, <see cref="DestroyWindow"/>, <see cref="wnd.Close"/>, API <msdn>WM_CLOSE</msdn>.
		/// </remarks>
		public static wnd CreateWindow(WNDPROC wndProc, bool keepAlive, string className, string name = null, WS style = 0, WSE exStyle = 0, int x = 0, int y = 0, int width = 0, int height = 0, wnd parent = default, nint controlId = 0, IntPtr hInstance = default, nint param = 0) {
			Not_.Null(wndProc, className);

			t_windows ??= new();
			wnd w;
			if (IsClassRegistered_(className, out var wp) && wp == null) {
				//if keepAlive, need to cubclass the new window, else add hwnd+wndProc to t_windows.
				//	But not after CreateWindowEx, because wndProc must receive all messages.
				//	Let _CWProc do it on first message.
				t_cwProc = wndProc;
				t_cwUnsafe = !keepAlive;
				try { w = Api.CreateWindowEx(exStyle, className, name, style, x, y, width, height, parent, controlId, hInstance, param); }
				finally { t_cwProc = null; t_cwUnsafe = false; } //if CreateWindowEx failed and _CWProc not called
				if (w.Is0) throw new AuException(0);
			} else {
				w = Api.CreateWindowEx(exStyle, className, name, style, x, y, width, height, parent, controlId, hInstance, param);
				if (w.Is0) throw new AuException(0);
				if (keepAlive) {
					t_windows[w] = wndProc;
					Api.SetWindowLongPtr(w, GWL.WNDPROC, s_cwProcFP);
				} else {
					Api.SetWindowLongPtr(w, GWL.WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProc));
				}
			}

			return w;
		}

		/// <summary>
		/// Creates native/unmanaged window.
		/// </summary>
		/// <exception cref="AuException">Failed to create window. Unlikely.</exception>
		/// <remarks>
		/// Calls API <msdn>CreateWindowEx</msdn>.
		/// To destroy the window can be used any function, including API <msdn>DestroyWindow</msdn>, <see cref="DestroyWindow"/>, <see cref="wnd.Close"/>, API <msdn>WM_CLOSE</msdn>.
		/// </remarks>
		/// <seealso cref="RegisterWindowClass"/>
		public static wnd CreateWindow(string className, string name = null, WS style = 0, WSE exStyle = 0, int x = 0, int y = 0, int width = 0, int height = 0, wnd parent = default, nint controlId = 0, IntPtr hInstance = default, nint param = 0) {
			var w = Api.CreateWindowEx(exStyle, className, name, style, x, y, width, height, parent, controlId, hInstance, param);
			if (w.Is0) throw new AuException(0);
			return w;
		}

		/// <summary>
		/// Creates native/unmanaged <msdn>message-only window</msdn>.
		/// </summary>
		/// <param name="className">Window class name. Can be any existing class.</param>
		/// <param name="name">Window name or <c>null</c>.</param>
		/// <exception cref="AuException">Failed to create window. Unlikely.</exception>
		/// <remarks>
		/// Styles: <b>WS_POPUP</b>, <b>WS_EX_NOACTIVATE</b>.
		/// To destroy the window can be used any function, including API <msdn>DestroyWindow</msdn>, <see cref="DestroyWindow"/>, <see cref="wnd.Close"/>, API <msdn>WM_CLOSE</msdn>.
		/// </remarks>
		public static wnd CreateMessageOnlyWindow(string className, string name = null) {
			return CreateWindow(className, name, WS.POPUP, WSE.NOACTIVATE, parent: SpecHWND.MESSAGE);
			//note: WS_EX_NOACTIVATE is important.
		}

		/// <summary>
		/// Creates native/unmanaged <msdn>message-only window</msdn> and sets its window procedure.
		/// </summary>
		/// <param name="wndProc"></param>
		/// <param name="className">Window class name.</param>
		/// <param name="name">Window name or <c>null</c>.</param>
		/// <exception cref="AuException">Failed to create window. Unlikely.</exception>
		/// <remarks>
		/// Styles: <b>WS_POPUP</b>, <b>WS_EX_NOACTIVATE</b>.
		/// Calls <see cref="CreateWindow(WNDPROC, bool, string, string, WS, WSE, int, int, int, int, wnd, nint, IntPtr, nint)"/> with <i>keepAlive</i>=<c>true</c>.
		/// </remarks>
		public static wnd CreateMessageOnlyWindow(WNDPROC wndProc, string className, string name = null) {
			return CreateWindow(wndProc, true, className, name, WS.POPUP, WSE.NOACTIVATE, parent: SpecHWND.MESSAGE);
			//note: WS_EX_NOACTIVATE is important.
		}

		/// <summary>
		/// Auto-registers window class "Au.DWP" with wndproc = DefWindowProc and creates hidden window.
		/// </summary>
		/// <param name="messageOnly"></param>
		/// <param name="wndProcUnsafe">If not <c>null</c>, replaces window procedure (SetWindowLongPtr). The caller must protect the delegate from GC.</param>
		/// <exception cref="AuException">Failed to create window. Unlikely.</exception>
		internal static wnd CreateWindowDWP_(bool messageOnly, WNDPROC wndProcUnsafe = null) {
			var cn = WindowClassDWP_;
			var w = messageOnly ? CreateMessageOnlyWindow(cn) : CreateWindow(cn);
			if (wndProcUnsafe != null) Api.SetWindowLongPtr(w, GWL.WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProcUnsafe));
			return w;
		}
		static int s_registeredDWP;
		const string c_wndClassDWP = "Au.DWP";

		/// <summary>
		/// Auto-registers window class "Au.DWP" with wndproc = DefWindowProc and returns "Au.DWP".
		/// </summary>
		internal static unsafe string WindowClassDWP_ {
			get {
				if (0 == Interlocked.CompareExchange(ref s_registeredDWP, 1, 0)) {
					var x = new Api.WNDCLASSEX { cbSize = sizeof(Api.WNDCLASSEX), style = Api.CS_GLOBALCLASS };
					fixed (char* pCN = c_wndClassDWP) {
						x.lpszClassName = pCN;
						x.lpfnWndProc = Api.GetProcAddress("user32.dll", "DefWindowProcW");
						if (0 == Api.RegisterClassEx(x)) throw new Win32Exception();
					}
				}
				return c_wndClassDWP;
			}
		}

		/// <summary>
		/// Replaces window procedure (SetWindowLongPtr). Returns previous window procedure.
		/// The caller must protect the delegate from GC.
		/// </summary>
		internal static IntPtr SubclassUnsafe_(wnd w, WNDPROC wndProc) {
			return Api.SetWindowLongPtr(w, GWL.WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProc));
		}

		/// <summary>
		/// Destroys a native window of this thread.
		/// Calls API <msdn>DestroyWindow</msdn>.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <seealso cref="wnd.Close"/>
		public static bool DestroyWindow(wnd w) {
			return Api.DestroyWindow(w);
		}

		/// <summary>
		/// Sets font.
		/// </summary>
		/// <param name="w"></param>
		/// <param name="font">
		/// Native font handle.
		/// If <c>default(IntPtr)</c>, sets font that is used by most windows and controls on this computer, usually Segoe UI, 9, DPI-scaled for <i>w</i> screen.
		/// </param>
		/// <remarks>
		/// Sends <msdn>WM_SETFONT</msdn> message.
		/// </remarks>
		public static void SetFont(wnd w, IntPtr font = default) {
			w.Send(Api.WM_SETFONT, font != default ? font : NativeFont_.RegularCached(Dpi.OfWindow(w)).Handle);
		}

		//rejected. Rarely used. Easy to send message.
		///// <summary>
		///// Gets native font handle.
		///// Sends message API <msdn>WM_GETFONT</msdn>.
		///// Does not copy the font; don't need to dispose.
		///// Use this function only with windows of current process.
		///// </summary>
		//public static IntPtr GetFont(wnd w)
		//{
		//	return w.Send(Api.WM_GETFONT);
		//}

		/// <summary>
		/// Gets window Windows Store app user model id, like <c>"Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"</c>.
		/// </summary>
		/// <returns><c>null</c> if failed. On Windows 7 returns <c>null</c> unless <i>getExePathIfNotWinStoreApp</i> <c>true</c>.</returns>
		/// <param name="w">A top-level window.</param>
		/// <param name="prependShellAppsFolder">Prepend <c>@"shell:AppsFolder\"</c> (to run or get icon).</param>
		/// <param name="getExePathIfNotWinStoreApp">Get program path if it is not a Windows Store app.</param>
		/// <remarks>
		/// Most Windows Store app windows have class name "Windows.UI.Core.CoreWindow" or "ApplicationFrameWindow".
		/// </remarks>
		public static unsafe string GetWindowsStoreAppId(wnd w, bool prependShellAppsFolder = false, bool getExePathIfNotWinStoreApp = false) {
			string appId = null;

			if (osVersion.minWin8) {
				var cn = w.ClassName;
				if (osVersion.minWin10 && cn == "ApplicationFrameWindow") {
					var w2 = w.ChildFast(null, "Windows.UI.Core.CoreWindow");
					if (!w2.Is0) {
						w = w2;
						cn = "Windows.UI.Core.CoreWindow";
					} else { //probably minimized. Very slow, ~20 times slower than GetApplicationUserModelId.
						if (0 == Api.SHGetPropertyStoreForWindow(w, Api.IID_IPropertyStore, out Api.IPropertyStore ps)) {
							if (0 == ps.GetValue(Api.PKEY_AppUserModel_ID, out var v)) {
								if (v.vt == Api.VARENUM.VT_LPWSTR) appId = Marshal.PtrToStringUni(v.value);
								v.Dispose();
							}
							Marshal.ReleaseComObject(ps);
						}
					}
				}

				//this code works with "Windows.UI.Core.CoreWindow" and WinUI 3 windows. Not with "ApplicationFrameWindow".
				if (appId == null) {
					using var p = Handle_.OpenProcess(w);
					if (!p.Is0) {
						int na = 1024; var b = stackalloc char[na];
						if (0 == Api.GetApplicationUserModelId(p, ref na, b) && na > 1) appId = new(b, 0, na - 1);
					}
				}

				if (appId != null) {
					if (cn is not ("Windows.UI.Core.CoreWindow" or "ApplicationFrameWindow")) { //is it really a Store window?
						var s = w.ProgramPath;
						if (s != null && !s.Starts(folders.ProgramFiles + @"WindowsApps\", true)) {
							Debug_.Print(s);
							return getExePathIfNotWinStoreApp ? s : null;
						}
					}
					if (prependShellAppsFolder) appId = @"shell:AppsFolder\" + appId;
					return appId;
				}
			}

			return getExePathIfNotWinStoreApp ? w.ProgramPath : null;
		}

		/// <summary>
		/// Calls API <msdn>GetClassLongPtr</msdn>.
		/// </summary>
		/// <remarks>
		/// Supports <see cref="lastError"/>.
		/// For <i>index</i> can be used constants from <see cref="GCL"/>. All values are the same in 32-bit and 64-bit process.
		/// In 32-bit process actually calls <b>GetClassLong</b>, because <b>GetClassLongPtr</b> is unavailable.
		/// </remarks>
		public static nint GetClassLong(wnd w, int index) => Api.GetClassLongPtr(w, index);

		/// <summary>
		/// Changes the owner window.
		/// </summary>
		/// <returns>If fails, returns <c>false</c>; supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// A window that has an owner window is always on top of it.
		/// Don't call this for controls, they don't have an owner window.
		/// Fails for example if the owner's process has higher [](xref:uac) integrity level or is a Store app.
		/// </remarks>
		/// <seealso cref="wnd.getwnd.Owner"/>
		public static bool SetOwnerWindow(wnd w, wnd owner) {
			Api.SetWindowLongPtr(w, GWL.HWNDPARENT, (nint)owner);
			if (w.Get.Owner != owner) return false;
			if (!owner.Is0) {
				bool tm = owner.IsTopmost;
				if (tm != w.IsTopmost) { if (tm) w.ZorderTopmost(); else w.ZorderNoTopmost(); }
				if (!w.ZorderIsAbove(owner)) w.ZorderAbove(owner);
			}
			return true;
		}

		//probably not useful. Dangerous.
		///// <summary>
		///// Calls API <msdn>SetClassLongPtr</msdn> (SetClassLong in 32-bit process).
		///// </summary>
		///// <exception cref="AuWndException"/>
		//public static nint SetClassLong(wnd w, int index, nint newValue)
		//{
		//	lastError.clear();
		//	nint R = Api.SetClassLongPtr(w, index, newValue);
		//	if(R == 0 && lastError.code != 0) w.ThrowUseNative();
		//	return R;
		//}

		//rejected. Does not work with many windows. Unreliable. Rarely used.
		///// <summary>
		///// Gets atom of a window class.
		///// To get class atom when you have a window w, use <c>WndUtil.GetClassLong(w, GCL.ATOM)</c>.
		///// </summary>
		///// <param name="className">Class name.</param>
		///// <param name="moduleHandle">Native module handle of the exe or dll that registered the class. Don't use if it is a global class (CS_GLOBALCLASS style).</param>
		//public static ushort GetClassAtom(string className, IntPtr moduleHandle = default)
		//{
		//	var x = new Api.WNDCLASSEX();
		//	x.cbSize = Api.SizeOf(x);
		//	return Api.GetClassInfoEx(moduleHandle, className, ref x);
		//}

		/// <summary>
		/// Calls API <msdn>RegisterWindowMessage</msdn>.
		/// </summary>
		/// <param name="name">Message name. Can be any unique string.</param>
		/// <param name="uacEnable">Also call API <msdn>ChangeWindowMessageFilter</msdn> for the message. More info: <see cref="UacEnableMessages"/>.</param>
		public static int RegisterMessage(string name, bool uacEnable = false) {
			var m = Api.RegisterWindowMessage(name);
			if (uacEnable && m != 0) Api.ChangeWindowMessageFilter(m, 1);
			return m;
		}

		/// <summary>
		/// Calls API <msdn>ChangeWindowMessageFilter</msdn> for each message in the list of messages.
		/// It allows processes of lower [](xref:uac) integrity level to send these messages to this process.
		/// </summary>
		public static void UacEnableMessages(params int[] messages) {
			foreach (var m in messages) Api.ChangeWindowMessageFilter(m, 1);
		}

		#region print msg

		/// <summary>
		/// Writes a Windows message to a string.
		/// If the message is specified in <i>options</i>, sets <c>s=null</c> and returns <c>false</c>.
		/// </summary>
		public static bool PrintMsg(out string s, wnd w, int msg, nint wParam, nint lParam, PrintMsgOptions options = null, [CallerMemberName] string m_ = null) {
			//Could instead use System.Windows.Forms.Message.ToString, but its list is incomplete, eg no dpichange messages.
			//	https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/MessageDecoder.cs,b19021e2f4480d57

			if (options?.Skip is int[] a) {
				s = null;
				int prev = 0;
				foreach (var v in a) {
					if (v < 0) {
						if (msg >= prev && msg <= (v == int.MinValue ? int.MaxValue : -v)) return false;
						prev = int.MaxValue;
					} else {
						if (v == msg) return false;
						prev = v;
					}
				}
			}

			var (name, plus) = _Name(ref msg, out bool reflect);

			using (new StringBuilder_(out var b)) {
				if (options?.Number ?? true) {
					//uint counter = (uint)w.Prop["PrintMsg"]; w.Prop.Set("PrintMsg", ++counter);
					//b.Append(counter).Append(". ");
					b.Append(++s_pm_counter).Append(". ");
				}

				if (options?.Indent ?? true) { //makes ~10 times slower, but not too slow
					int i = 0;
					MethodBase m0 = null;
					foreach (var f in new StackTrace(1).GetFrames()) {
						var m1 = f.GetMethod();
						if (m1.Name != m_) continue;
						if (m0 == null) m0 = m1; else if ((object)m1 == m0) i += 4;
					}
					if (i > 0) b.Append(' ', i);
				}

				if (reflect) b.Append("WM_REFLECT+");
				if (name == null) b.AppendFormat("0x{0:X}", msg);
				else if (plus != 0) b.AppendFormat("{0}+0x{1:X}", name, plus);
				else if (msg >= 0xc000 && msg <= 0xffff) b.AppendFormat("\"{0}\"", name);
				else b.Append(name);

				b.AppendFormat(", 0x{0:X8}, 0x{1:X8}, hwnd={2}", (int)wParam, (int)lParam, w.Handle);
				if (options?.WindowProperties ?? false) {
					if (!w.Is0) b.AppendFormat(" ({0} \"{1}\" {{{2}}})", w.ClassName?.Limit(30), w.Name?.Limit(30), w.Rect.ToStringSimple());
				}

				s = b.ToString();
				return true;
			}

			static (string name, int plus) _Name(ref int m, out bool reflect) {
				reflect = false;
				if (m >= 0x10000) return default; //reserved by the system
				if (m >= 0xC000) return (ClipFormats.GetName(m, orNull: true), 0); //registered
				if (m >= Api.WM_APP) return ("WM_APP", m - Api.WM_APP); //0x8000
				if (reflect = m >= Api.WM_REFLECT && m < Api.WM_REFLECT * 2) m -= Api.WM_REFLECT; //0x2000
				if (m >= Api.WM_USER) return ("WM_USER", m - Api.WM_USER); //0x400
				#region switch
				var s = m switch {
					0x0 => "WM_NULL",
					0x1 => "WM_CREATE",
					0x2 => "WM_DESTROY",
					0x3 => "WM_MOVE",
					0x5 => "WM_SIZE",
					0x6 => "WM_ACTIVATE",
					0x7 => "WM_SETFOCUS",
					0x8 => "WM_KILLFOCUS",
					0xA => "WM_ENABLE",
					0xB => "WM_SETREDRAW",
					0xC => "WM_SETTEXT",
					0xD => "WM_GETTEXT",
					0xE => "WM_GETTEXTLENGTH",
					0xF => "WM_PAINT",
					0x10 => "WM_CLOSE",
					0x11 => "WM_QUERYENDSESSION",
					0x13 => "WM_QUERYOPEN",
					0x16 => "WM_ENDSESSION",
					0x12 => "WM_QUIT",
					0x14 => "WM_ERASEBKGND",
					0x15 => "WM_SYSCOLORCHANGE",
					0x18 => "WM_SHOWWINDOW",
					0x1A => "WM_SETTINGCHANGE",
					0x1B => "WM_DEVMODECHANGE",
					0x1C => "WM_ACTIVATEAPP",
					0x1D => "WM_FONTCHANGE",
					0x1E => "WM_TIMECHANGE",
					0x1F => "WM_CANCELMODE",
					0x20 => "WM_SETCURSOR",
					0x21 => "WM_MOUSEACTIVATE",
					0x22 => "WM_CHILDACTIVATE",
					0x23 => "WM_QUEUESYNC",
					0x24 => "WM_GETMINMAXINFO",
					0x26 => "WM_PAINTICON",
					0x27 => "WM_ICONERASEBKGND",
					0x28 => "WM_NEXTDLGCTL",
					0x2A => "WM_SPOOLERSTATUS",
					0x2B => "WM_DRAWITEM",
					0x2C => "WM_MEASUREITEM",
					0x2D => "WM_DELETEITEM",
					0x2E => "WM_VKEYTOITEM",
					0x2F => "WM_CHARTOITEM",
					0x30 => "WM_SETFONT",
					0x31 => "WM_GETFONT",
					0x32 => "WM_SETHOTKEY",
					0x33 => "WM_GETHOTKEY",
					0x37 => "WM_QUERYDRAGICON",
					0x39 => "WM_COMPAREITEM",
					0x3D => "WM_GETOBJECT",
					0x41 => "WM_COMPACTING",
					0x44 => "WM_COMMNOTIFY",
					0x46 => "WM_WINDOWPOSCHANGING",
					0x47 => "WM_WINDOWPOSCHANGED",
					0x48 => "WM_POWER",
					0x4A => "WM_COPYDATA",
					0x4B => "WM_CANCELJOURNAL",
					0x4E => "WM_NOTIFY",
					0x50 => "WM_INPUTLANGCHANGEREQUEST",
					0x51 => "WM_INPUTLANGCHANGE",
					0x52 => "WM_TCARD",
					0x53 => "WM_HELP",
					0x54 => "WM_USERCHANGED",
					0x55 => "WM_NOTIFYFORMAT",
					0x7B => "WM_CONTEXTMENU",
					0x7C => "WM_STYLECHANGING",
					0x7D => "WM_STYLECHANGED",
					0x7E => "WM_DISPLAYCHANGE",
					0x7F => "WM_GETICON",
					0x80 => "WM_SETICON",
					0x81 => "WM_NCCREATE",
					0x82 => "WM_NCDESTROY",
					0x83 => "WM_NCCALCSIZE",
					0x84 => "WM_NCHITTEST",
					0x85 => "WM_NCPAINT",
					0x86 => "WM_NCACTIVATE",
					0x87 => "WM_GETDLGCODE",
					0x88 => "WM_SYNCPAINT",
					0xA0 => "WM_NCMOUSEMOVE",
					0xA1 => "WM_NCLBUTTONDOWN",
					0xA2 => "WM_NCLBUTTONUP",
					0xA3 => "WM_NCLBUTTONDBLCLK",
					0xA4 => "WM_NCRBUTTONDOWN",
					0xA5 => "WM_NCRBUTTONUP",
					0xA6 => "WM_NCRBUTTONDBLCLK",
					0xA7 => "WM_NCMBUTTONDOWN",
					0xA8 => "WM_NCMBUTTONUP",
					0xA9 => "WM_NCMBUTTONDBLCLK",
					0xAB => "WM_NCXBUTTONDOWN",
					0xAC => "WM_NCXBUTTONUP",
					0xAD => "WM_NCXBUTTONDBLCLK",
					0xFE => "WM_INPUT_DEVICE_CHANGE",
					0xFF => "WM_INPUT",
					0x100 => "WM_KEYDOWN",
					0x101 => "WM_KEYUP",
					0x102 => "WM_CHAR",
					0x103 => "WM_DEADCHAR",
					0x104 => "WM_SYSKEYDOWN",
					0x105 => "WM_SYSKEYUP",
					0x106 => "WM_SYSCHAR",
					0x107 => "WM_SYSDEADCHAR",
					0x109 => "WM_UNICHAR",
					0x10D => "WM_IME_STARTCOMPOSITION",
					0x10E => "WM_IME_ENDCOMPOSITION",
					0x10F => "WM_IME_COMPOSITION",
					0x110 => "WM_INITDIALOG",
					0x111 => "WM_COMMAND",
					0x112 => "WM_SYSCOMMAND",
					0x113 => "WM_TIMER",
					0x114 => "WM_HSCROLL",
					0x115 => "WM_VSCROLL",
					0x116 => "WM_INITMENU",
					0x117 => "WM_INITMENUPOPUP",
					0x119 => "WM_GESTURE",
					0x11A => "WM_GESTURENOTIFY",
					0x11F => "WM_MENUSELECT",
					0x120 => "WM_MENUCHAR",
					0x121 => "WM_ENTERIDLE",
					0x122 => "WM_MENURBUTTONUP",
					0x123 => "WM_MENUDRAG",
					0x124 => "WM_MENUGETOBJECT",
					0x125 => "WM_UNINITMENUPOPUP",
					0x126 => "WM_MENUCOMMAND",
					0x127 => "WM_CHANGEUISTATE",
					0x128 => "WM_UPDATEUISTATE",
					0x129 => "WM_QUERYUISTATE",
					0x132 => "WM_CTLCOLORMSGBOX",
					0x133 => "WM_CTLCOLOREDIT",
					0x134 => "WM_CTLCOLORLISTBOX",
					0x135 => "WM_CTLCOLORBTN",
					0x136 => "WM_CTLCOLORDLG",
					0x137 => "WM_CTLCOLORSCROLLBAR",
					0x138 => "WM_CTLCOLORSTATIC",
					0x200 => "WM_MOUSEMOVE",
					0x201 => "WM_LBUTTONDOWN",
					0x202 => "WM_LBUTTONUP",
					0x203 => "WM_LBUTTONDBLCLK",
					0x204 => "WM_RBUTTONDOWN",
					0x205 => "WM_RBUTTONUP",
					0x206 => "WM_RBUTTONDBLCLK",
					0x207 => "WM_MBUTTONDOWN",
					0x208 => "WM_MBUTTONUP",
					0x209 => "WM_MBUTTONDBLCLK",
					0x20A => "WM_MOUSEWHEEL",
					0x20B => "WM_XBUTTONDOWN",
					0x20C => "WM_XBUTTONUP",
					0x20D => "WM_XBUTTONDBLCLK",
					0x20E => "WM_MOUSEHWHEEL",
					0x210 => "WM_PARENTNOTIFY",
					0x211 => "WM_ENTERMENULOOP",
					0x212 => "WM_EXITMENULOOP",
					0x213 => "WM_NEXTMENU",
					0x214 => "WM_SIZING",
					0x215 => "WM_CAPTURECHANGED",
					0x216 => "WM_MOVING",
					0x218 => "WM_POWERBROADCAST",
					0x219 => "WM_DEVICECHANGE",
					0x220 => "WM_MDICREATE",
					0x221 => "WM_MDIDESTROY",
					0x222 => "WM_MDIACTIVATE",
					0x223 => "WM_MDIRESTORE",
					0x224 => "WM_MDINEXT",
					0x225 => "WM_MDIMAXIMIZE",
					0x226 => "WM_MDITILE",
					0x227 => "WM_MDICASCADE",
					0x228 => "WM_MDIICONARRANGE",
					0x229 => "WM_MDIGETACTIVE",
					0x230 => "WM_MDISETMENU",
					0x231 => "WM_ENTERSIZEMOVE",
					0x232 => "WM_EXITSIZEMOVE",
					0x233 => "WM_DROPFILES",
					0x234 => "WM_MDIREFRESHMENU",
					0x238 => "WM_POINTERDEVICECHANGE",
					0x239 => "WM_POINTERDEVICEINRANGE",
					0x23A => "WM_POINTERDEVICEOUTOFRANGE",
					0x240 => "WM_TOUCH",
					0x241 => "WM_NCPOINTERUPDATE",
					0x242 => "WM_NCPOINTERDOWN",
					0x243 => "WM_NCPOINTERUP",
					0x245 => "WM_POINTERUPDATE",
					0x246 => "WM_POINTERDOWN",
					0x247 => "WM_POINTERUP",
					0x249 => "WM_POINTERENTER",
					0x24A => "WM_POINTERLEAVE",
					0x24B => "WM_POINTERACTIVATE",
					0x24C => "WM_POINTERCAPTURECHANGED",
					0x24D => "WM_TOUCHHITTESTING",
					0x24E => "WM_POINTERWHEEL",
					0x24F => "WM_POINTERHWHEEL",
					0x251 => "WM_POINTERROUTEDTO",
					0x252 => "WM_POINTERROUTEDAWAY",
					0x253 => "WM_POINTERROUTEDRELEASED",
					0x281 => "WM_IME_SETCONTEXT",
					0x282 => "WM_IME_NOTIFY",
					0x283 => "WM_IME_CONTROL",
					0x284 => "WM_IME_COMPOSITIONFULL",
					0x285 => "WM_IME_SELECT",
					0x286 => "WM_IME_CHAR",
					0x288 => "WM_IME_REQUEST",
					0x290 => "WM_IME_KEYDOWN",
					0x291 => "WM_IME_KEYUP",
					0x2A1 => "WM_MOUSEHOVER",
					0x2A3 => "WM_MOUSELEAVE",
					0x2A0 => "WM_NCMOUSEHOVER",
					0x2A2 => "WM_NCMOUSELEAVE",
					0x2B1 => "WM_WTSSESSION_CHANGE",
					0x2E0 => "WM_DPICHANGED",
					0x2E2 => "WM_DPICHANGED_BEFOREPARENT",
					0x2E3 => "WM_DPICHANGED_AFTERPARENT",
					0x2E4 => "WM_GETDPISCALEDSIZE",
					0x300 => "WM_CUT",
					0x301 => "WM_COPY",
					0x302 => "WM_PASTE",
					0x303 => "WM_CLEAR",
					0x304 => "WM_UNDO",
					0x305 => "WM_RENDERFORMAT",
					0x306 => "WM_RENDERALLFORMATS",
					0x307 => "WM_DESTROYCLIPBOARD",
					0x308 => "WM_DRAWCLIPBOARD",
					0x309 => "WM_PAINTCLIPBOARD",
					0x30A => "WM_VSCROLLCLIPBOARD",
					0x30B => "WM_SIZECLIPBOARD",
					0x30C => "WM_ASKCBFORMATNAME",
					0x30D => "WM_CHANGECBCHAIN",
					0x30E => "WM_HSCROLLCLIPBOARD",
					0x30F => "WM_QUERYNEWPALETTE",
					0x310 => "WM_PALETTEISCHANGING",
					0x311 => "WM_PALETTECHANGED",
					0x312 => "WM_HOTKEY",
					0x317 => "WM_PRINT",
					0x318 => "WM_PRINTCLIENT",
					0x319 => "WM_APPCOMMAND",
					0x31A => "WM_THEMECHANGED",
					0x31D => "WM_CLIPBOARDUPDATE",
					0x31E => "WM_DWMCOMPOSITIONCHANGED",
					0x31F => "WM_DWMNCRENDERINGCHANGED",
					0x320 => "WM_DWMCOLORIZATIONCOLORCHANGED",
					0x321 => "WM_DWMWINDOWMAXIMIZEDCHANGE",
					0x323 => "WM_DWMSENDICONICTHUMBNAIL",
					0x326 => "WM_DWMSENDICONICLIVEPREVIEWBITMAP",
					0x33F => "WM_GETTITLEBARINFOEX",
					0x8000 => "WM_APP",
					0x400 => "WM_USER",
					_ => null
				};
				#endregion
				return (s, 0);
			}
		}
		[ThreadStatic] static uint s_pm_counter;

#if !true //this script creates the switch { ... }
//var a=new List<string>();
print.clear();
var b = new StringBuilder("var s = m switch {\r\n");
var s1 = File.ReadAllText(@"C:\code\au\Au\Api\Api_const.cs");
foreach (var m in s1.RxFindAll(@"(?m)^\h*internal const int (WM_\w+) *= *(\w+);")) {
	var s = m[1].Value;
	if (s.Ends("FIRST") || s.Ends("LAST") || s.Starts("WM_PSD_") || s.Starts("WM_DDE_") || s.Starts("WM_CHOOSEFONT_") || s == "WM_WININICHANGE") {
		//print.it(s);
		continue;
	}
	//print.it(s, m[2]);
	//a.Add(s);
	b.AppendFormat("{0} => \"{1}\",\r\n", m[2].Value, s);
}
b.Append("_ => null};\r\nreturn (s, 0);");
//a.Sort();
//print.it(a);
var s2 = b.ToString();
print.it(s2);
#endif

		/// <summary>
		/// Writes a Windows message to the output, unless it is specified in <i>options</i>.
		/// </summary>
		public static void PrintMsg(wnd w, int msg, nint wParam, nint lParam, PrintMsgOptions options = null, [CallerMemberName] string m_ = null) {
			if (PrintMsg(out string s, w, msg, wParam, lParam, options, m_)) print.it(s);
		}

		/// <summary>
		/// Writes a Windows message to a string.
		/// If the message is specified in <i>options</i>, sets <c>s=null</c> and returns <c>false</c>.
		/// </summary>
		/// <remarks>
		/// The <i>m</i> parameter also accepts <b>System.Windows.Interop.MSG</b> (WPF) and <b>System.Windows.Forms.Message</b>.
		/// </remarks>
		public static bool PrintMsg(out string s, in MSG m, PrintMsgOptions options = null, [CallerMemberName] string m_ = null) {
			return PrintMsg(out s, m.hwnd, m.message, m.wParam, m.lParam, options, m_);
		}

		/// <summary>
		/// Writes a Windows message to the output, unless it is specified in <i>options</i>.
		/// </summary>
		/// <remarks>
		/// The <i>m</i> parameter also accepts <b>System.Windows.Interop.MSG</b> (WPF) and <b>System.Windows.Forms.Message</b>.
		/// </remarks>
		public static void PrintMsg(in MSG m, PrintMsgOptions options = null, [CallerMemberName] string m_ = null) {
			PrintMsg(m.hwnd, m.message, m.wParam, m.lParam, options, m_);
		}

		#endregion

		/// <summary>
		/// Simple non-OLE drag operation.
		/// </summary>
		/// <returns><c>true</c> if dropped, <c>false</c> if canceled.</returns>
		/// <param name="window">Window or control that owns the drag operation. Must be of this thread.</param>
		/// <param name="mouseButton">Mouse button that is used for the drag operation: Left, Right, Middle.</param>
		/// <param name="onMouseKeyMessage">Callback function, called on each received mouse/key message. Optional.</param>
		public static bool DragLoop(AnyWnd window, MButtons mouseButton = MButtons.Left, Action<WDLArgs> onMouseKeyMessage = null) {
			wnd w = window.Hwnd;
			Api.SetCapture(w);

			bool R = false;
			var x = new WDLArgs();
			for (; ; ) {
				if (Api.GetCapture() != w) return false;
				if (!Api.GetMessage(out x.msg)) {
					if (x.msg.message == Api.WM_QUIT) Api.PostQuitMessage((int)x.msg.wParam);
					break;
				}

				bool call = false;
				int m = x.msg.message;
				if (m >= Api.WM_MOUSEFIRST && m <= Api.WM_MOUSELAST) {
					if (m == Api.WM_LBUTTONUP) {
						if (R = mouseButton.Has(MButtons.Left)) break;
					} else if (m == Api.WM_RBUTTONUP) {
						if (R = mouseButton.Has(MButtons.Right)) break;
					} else if (m == Api.WM_MBUTTONUP) {
						if (R = mouseButton.Has(MButtons.Middle)) break;
					}
					call = true;
				} else if (m == Api.WM_KEYDOWN || m == Api.WM_KEYUP || m == Api.WM_SYSKEYDOWN || m == Api.WM_SYSKEYUP) {
					//on key down/up caller may want to update cursor when eg Ctrl pressed/released
					if (x.msg.wParam == (byte)KKey.Escape) break;
					call = true;
				}

				if (call && onMouseKeyMessage != null) {
					onMouseKeyMessage(x);
					if (x._stopped) break;
					if (x.cursor != default) {
						Api.SetCursor(x.cursor);
						x.cursor = default;
					}
				}

				Api.DispatchMessage(x.msg);
			}

			Api.ReleaseCapture();
			return R;
		}

		/// <summary>
		/// Waits while there is no active window.
		/// </summary>
		/// <param name="doEvents">While waiting call <see cref="wait.doEvents"/> to process Windows messages etc.</param>
		/// <remarks>
		/// When there is no active window, functions <see cref="wnd.active"/> and API <msdn>GetForegroundWindow</msdn> return 0.
		/// It sometimes happens after closing, minimizing or switching the active window, briefly until another window becomes active.
		/// This function waits max 500 ms, then returns <c>false</c> if there is no active window.
		/// Don't need to call this after calling functions of this library.
		/// </remarks>
		public static bool WaitForAnActiveWindow(bool doEvents = false) {
			for (int i = 0; ;) {
				if (doEvents) wait.doEvents();
				if (!wnd.active.Is0) return true;
				if (++i == 32) break;
				wait.ms(i);
			}
			return false;
			//Call WaitForAnActiveWindow(true) after showing a dialog API.
			//	In a thread that does not process messages, after closing a dialog may be not updated key states.
			//	Processing remaining unprocessed messages fixes it.
		}

		/// <summary>
		/// Temporarily enables this process to activate windows with API <msdn>SetForegroundWindow</msdn>.
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		/// <param name="processId">Process id. If not 0, enables that process to activate windows too. If -1, all processes will be enabled.</param>
		/// <remarks>
		/// In some cases you may need this function because Windows often disables API <msdn>SetForegroundWindow</msdn> to not allow background applications to activate windows while the user is working (using keyboard/mouse) with the currently active window. Then <b>SetForegroundWindow</b> usually just makes the window's taskbar button flash.
		/// Usually you don't call <b>SetForegroundWindow</b> directly. It is called by some other functions.
		/// Don't need to call this function before calling <see cref="wnd.Activate"/> and other functions of this library that activate windows.
		/// </remarks>
		public static bool EnableActivate(int processId = 0) {
			if (!wnd.Internal_.EnableActivate(false)) return false;
			return processId == 0 || Api.AllowSetForegroundWindow(processId);
		}

		/// <summary>
		/// Calls API <msdn>PostThreadMessage</msdn>. 
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		public static bool PostThreadMessage(int threadId, int message, nint wParam = 0, nint lParam = 0) {
			return Api.PostThreadMessage(threadId, message, wParam, lParam);
		}

		/// <summary>
		/// Subclasses a window.
		/// </summary>
		/// <param name="w">A window or control of this thread.</param>
		/// <param name="proc">The new window procedure. It is called on every message received by the window (unless blocked by another subclass added later). Let it call <see cref="DefSubclassProc"/>, except when you want to block the message.</param>
		/// <returns>A cookie for <see cref="Unsubclass"/>. Returns <c>null</c> if failed.</returns>
		/// <remarks>
		/// Uses API <msdn>SetWindowSubclass</msdn>.
		/// Implicitly unsubclasses when the window is destroyed.
		/// Protects <i>proc</i> from GC for as long as need.
		/// </remarks>
		public static object Subclass(wnd w, WNDPROC proc) {
			Api.SUBCLASSPROC sp = null;
			if (!Api.SetWindowSubclass(w, sp = _WndProc, 1)) return null;
			(t_asp ??= new()).Add(sp);
			nint _WndProc(wnd w, int msg, nint wp, nint lp, nint idSubclass, nint refData) {
				var r = proc(w, msg, wp, lp);
				if (msg == Api.WM_NCDESTROY) if (Api.RemoveWindowSubclass(w, sp, 1)) t_asp.Remove(sp);
				return r;
			}
			return new _SubclassCookie(w, sp);
		}
		[ThreadStatic] static List<Api.SUBCLASSPROC> t_asp; //GC
		record class _SubclassCookie(wnd w, Api.SUBCLASSPROC sp);

		/// <summary>
		/// Unsubclasses window subclassed by <see cref="Subclass"/>.
		/// Unsubclassing is optional; the window is implicitly unsubclassed when closed.
		/// </summary>
		/// <param name="cookie">The return value of <b>Subclass</b>.</param>
		public static void Unsubclass(object cookie) {
			if (cookie == null) return;
			if (cookie is not _SubclassCookie c) throw new ArgumentException();
			if (Api.RemoveWindowSubclass(c.w, c.sp, 1) || !c.w.IsAlive) t_asp.Remove(c.sp);
		}

		/// <summary>
		/// Let your callback function used with <see cref="Subclass"/> call this function and return its return value. After or before processing the message.
		/// </summary>
		public static nint DefSubclassProc(wnd w, int msg, nint wp, nint lp)
			=> Api.DefSubclassProc(w, msg, wp, lp);
	}
}

namespace Au.Types {
	/// <summary>
	/// Options for <see cref="WndUtil.PrintMsg"/>.
	/// </summary>
	public class PrintMsgOptions {
		///
		public PrintMsgOptions() { }

		/// <summary>
		/// Sets <see cref="Skip"/>.
		/// </summary>
		public PrintMsgOptions(params int[] skip) { Skip = skip; }

		/// <summary>
		/// Prepend counter 1, 2, 3...
		/// Default <c>true</c>.
		/// </summary>
		public bool Number { get; set; } = true;

		/// <summary>
		/// Prepend one or more tabs if the caller function (usually a window procedure) is called recursively.
		/// Default <c>true</c>.
		/// </summary>
		public bool Indent { get; set; } = true;

		/// <summary>
		/// Ignore these messages.
		/// To specify a range of messages, use two array elements: first message and negative last message.
		/// </summary>
		public int[] Skip { get; set; }

		/// <summary>
		/// Append window classname, name and rectangle.
		/// </summary>
		public bool WindowProperties { get; set; }
	}

	/// <summary>
	/// <see cref="WndUtil.DragLoop"/> callback function arguments.
	/// </summary>
	public class WDLArgs {
		/// <summary>
		/// Current message retrieved by API <msdn>GetMessage</msdn>.
		/// API <msdn>MSG</msdn>.
		/// </summary>
		public MSG msg;

		/// <summary>
		/// Native cursor handle. The callback function can set this to temporarily set cursor.
		/// </summary>
		public IntPtr cursor;

		/// <summary>
		/// The callback function can call this to end the operation.
		/// </summary>
		public void Stop() { _stopped = true; }
		internal bool _stopped;
	}

	/// <summary>
	/// Used with <see cref="WndUtil.RegisterWindowClass"/>.
	/// </summary>
	[NoDoc]
	public class RWCEtc {
#pragma warning disable 1591 //XML doc
		public uint style;
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public MCursor mCursor;
		public nint hbrBackground;
		public IntPtr hIconSm;
#pragma warning restore 1591 //XML doc
	}
}
