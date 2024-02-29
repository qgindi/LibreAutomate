namespace Au;

public partial struct wnd {
	/// <summary>
	/// Gets related windows and controls.
	/// Use like <c>wnd w2 = w1.Get.Owner;</c> (here w1 is a <b>wnd</b> variable).
	/// </summary>
	public getwnd Get => new(this);

	/// <summary>
	/// Static functions of this class are used to get special windows (used like <c>wnd w = wnd.getwnd.top;</c>) and all windows.
	/// Instances of this class are used to get related windows and controls, like <c>wnd w2 = w1.Get.FirstChild;</c> (here w1 is a <b>wnd</b> variable).
	/// </summary>
	public partial struct getwnd {
		wnd _w;
		///
		public getwnd(wnd wThis) => _w = wThis;

		#region instance

		/// <summary>
		/// Gets nearest visible sibling control to the left from this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if not found.</returns>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too. Skips maximized/minimized windows and desktop.
		/// </remarks>
		public wnd SiblingLeft() => _w._SiblingXY(_SibXY.Left);
		//FUTURE: add these to the window tool, like navig in Delm. Also add previous/next/parent/child in Z order.

		/// <summary>
		/// Gets nearest visible sibling control to the right from this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if not found.</returns>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too. Skips maximized/minimized windows and desktop.
		/// </remarks>
		public wnd SiblingRight() => _w._SiblingXY(_SibXY.Right);

		/// <summary>
		/// Gets nearest visible sibling control above this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if not found.</returns>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too. Skips maximized/minimized windows and desktop.
		/// </remarks>
		public wnd SiblingAbove() => _w._SiblingXY(_SibXY.Above);

		/// <summary>
		/// Gets nearest visible sibling control to below this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if not found.</returns>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too. Skips maximized/minimized windows and desktop.
		/// </remarks>
		public wnd SiblingBelow() => _w._SiblingXY(_SibXY.Below);

		/// <summary>
		/// Gets a visible sibling control to the left from this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if there is no sibling.</returns>
		/// <param name="distance">Horizontal distance from the left of this control.</param>
		/// <param name="yOffset">Vertical offset from the top of this control. If negative - up. Default 5.</param>
		/// <param name="topChild">If at that point is a visible child of the sibling, get that child. Default <c>false</c>.</param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too.
		/// </remarks>
		public wnd SiblingLeft(int distance, int yOffset = 5, bool topChild = false) => _w._SiblingXY(_SibXY.Left, distance, yOffset, topChild);

		/// <summary>
		/// Gets a visible sibling control to the right from this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if there is no sibling.</returns>
		/// <param name="distance">Horizontal distance from the right of this control.</param>
		/// <param name="yOffset">Vertical offset from the top of this control. If negative - up. Default 5.</param>
		/// <param name="topChild">If at that point is a visible child of the sibling, get that child. Default <c>false</c>.</param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too.
		/// </remarks>
		public wnd SiblingRight(int distance, int yOffset = 5, bool topChild = false) => _w._SiblingXY(_SibXY.Right, distance, yOffset, topChild);

		/// <summary>
		/// Gets a visible sibling control above this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if there is no sibling.</returns>
		/// <param name="distance">Vertical distance from the top of this control.</param>
		/// <param name="xOffset">Horizontal offset from the left of this control. If negative - to the left. Default 5.</param>
		/// <param name="topChild">If at that point is a visible child of the sibling, get that child. Default <c>false</c>.</param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too.
		/// </remarks>
		public wnd SiblingAbove(int distance, int xOffset = 5, bool topChild = false) => _w._SiblingXY(_SibXY.Above, distance, xOffset, topChild);

		/// <summary>
		/// Gets a visible sibling control below this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if there is no sibling.</returns>
		/// <param name="distance">Vertical distance from the bottom of this control.</param>
		/// <param name="xOffset">Horizontal offset from the left of this control. If negative - to the left. Default 5.</param>
		/// <param name="topChild">If at that point is a visible child of the sibling, get that child. Default <c>false</c>.</param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too.
		/// </remarks>
		public wnd SiblingBelow(int distance, int xOffset = 5, bool topChild = false) => _w._SiblingXY(_SibXY.Below, distance, xOffset, topChild);

		wnd _GetWindow(int dir, int skip) {
			if (skip < 0) return default;
			wnd w;
			for (w = _w; skip >= 0 && !w.Is0; skip--) {
				w = Api.GetWindow(w, dir);
			}
			return w;
		}

		/// <summary>
		/// Gets next sibling window or control in the Z order.
		/// </summary>
		/// <returns><c>default(wnd)</c> if this is the last or if failed.</returns>
		/// <param name="skip">How many next windows to skip.</param>
		/// <remarks>
		/// If this is a top-level window, gets next top-level window, else gets next control of the same direct parent.
		/// Calls API <msdn>GetWindow</msdn>(<b>GW_HWNDNEXT</b>).
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd Next(int skip = 0) => _GetWindow(Api.GW_HWNDNEXT, skip);

		/// <summary>
		/// Gets previous sibling window or control in the Z order.
		/// </summary>
		/// <returns><c>default(wnd)</c> if this is the first or if failed.</returns>
		/// <param name="skip">How many previous windows to skip.</param>
		/// <remarks>
		/// If this is a top-level window, gets previous top-level window, else gets previous control of the same direct parent.
		/// Calls API <msdn>GetWindow</msdn>(<b>GW_HWNDPREV</b>).
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd Previous(int skip = 0) => _GetWindow(Api.GW_HWNDPREV, skip);

		/// <summary>
		/// Gets the first sibling window or control in the Z order.
		/// If this is the first, returns this.
		/// </summary>
		/// <remarks>
		/// If this is a top-level window, gets the first top-level window, else gets the first control of the same direct parent.
		/// Calls API <msdn>GetWindow</msdn>(this, <b>GW_HWNDFIRST</b>).
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd FirstSibling => Api.GetWindow(_w, Api.GW_HWNDFIRST);

		/// <summary>
		/// Gets the last sibling window or control in the Z order.
		/// If this is the last, returns this, not <c>default(wnd)</c>.
		/// </summary>
		/// <remarks>
		/// If this is a top-level window, gets the last top-level window, else gets the last control of the same direct parent.
		/// Calls API <msdn>GetWindow</msdn>(this, <b>GW_HWNDLAST</b>).
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd LastSibling => Api.GetWindow(_w, Api.GW_HWNDLAST);

		/// <summary>
		/// Gets the first direct child control in the Z order.
		/// </summary>
		/// <returns><c>default(wnd)</c> if no children or if failed.</returns>
		/// <remarks>
		/// Calls API <msdn>GetWindow</msdn>(<b>GW_CHILD</b>).
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd FirstChild => Api.GetWindow(_w, Api.GW_CHILD);

		/// <summary>
		/// Gets the last direct child control in the Z order.
		/// </summary>
		/// <returns><c>default(wnd)</c> if no children or if failed.</returns>
		/// <remarks>
		/// Calls API <msdn>GetWindow</msdn>.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd LastChild { get { var t = Api.GetWindow(_w, Api.GW_CHILD); return t.Is0 ? t : Api.GetWindow(t, Api.GW_HWNDLAST); } }

		/// <summary>
		/// Gets a direct child control by index.
		/// </summary>
		/// <returns><c>default(wnd)</c> if no children or if index is invalid or if failed.</returns>
		/// <param name="index">0-based index of the child control in the Z order.</param>
		/// <remarks>
		/// Calls API <msdn>GetWindow</msdn>.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd Child(int index) {
			if (index < 0) return default;
			wnd c = Api.GetWindow(_w, Api.GW_CHILD);
			for (; index > 0 && !c.Is0; index--) c = Api.GetWindow(c, Api.GW_HWNDNEXT);
			return c;
		}

		/// <summary>
		/// Gets the owner window of this top-level window.
		/// </summary>
		/// <returns><c>default(wnd)</c> if this window isn't owned or if failed.</returns>
		/// <remarks>
		/// A window that has an owner window is always on top of it.
		/// Controls don't have an owner window.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		/// <seealso cref="WndUtil.SetOwnerWindow"/>
		public wnd Owner => Api.GetWindow(_w, Api.GW_OWNER);

		/// <summary>
		/// Gets the top-level parent window of this control.
		/// If this is a top-level window, returns this.
		/// </summary>
		/// <returns><c>default(wnd)</c> if failed.</returns>
		/// <remarks>
		/// Supports <see cref="lastError"/>.
		/// This function is the same as <see cref="wnd.Window"/>.
		/// </remarks>
		public wnd Window => _w.Window;

		/// <summary>
		/// Gets the direct parent window of this control. It can be the top-level window or another control.
		/// </summary>
		/// <returns><c>default(wnd)</c> if this is a top-level window or if failed.</returns>
		/// <remarks>
		/// Supports <see cref="lastError"/>.
		/// Unlike API <msdn>GetParent</msdn>, this function never returns the owner window.
		/// </remarks>
		public wnd DirectParent {
			get {
#if true
				var p = _w.GetWindowLong(GWL.HWNDPARENT);
				if (p == default) {
					//#if DEBUG
					//var p2 = Api.GetAncestor(_w, Api.GA_PARENT);
					//Debug.Assert(p2.Is0 || p2 == Root);
					//#endif
					return default;
				}
				lastError.clear();
				var o = Api.GetWindow(_w, Api.GW_OWNER);
				if (o.Is0) {
					var ec = lastError.code;
					if (ec == 0) return (wnd)p;
					Debug.Assert(ec == Api.ERROR_INVALID_WINDOW_HANDLE);
				}
				return default;
#else //the msdn-recommended version, but >=6 times slower. The same IsTopLevelWindow (undocumented API).
					var p = Api.GetAncestor(_w, Api.GA_PARENT);
					if(p.Is0 || p == Root) return default;
					return p;
#endif
			}
		}

		//rejected. Not much faster than our DirectParent.
		///// <summary>
		///// Calls API <msdn>GetParent</msdn>.
		///// </summary>
		///// <remarks>
		///// The API function is fast but unreliable. It can get parent or owner window, and fails in some cases. Read more in the API documentation. It is reliable only if you know that this window is a child window and has WS_CHILD style.
		///// Supports <see cref="lastError"/>.
		///// </remarks>
		//[Obsolete("Unreliable")]
		//public wnd GetParentApi => Api.GetParent(_w);

		/// <summary>
		/// Gets the first (in Z order) enabled window owned by this window.
		/// </summary>
		/// <param name="orThis">Return this window if there are no enabled owned windows. If <c>false</c>, then returns <c>default(wnd)</c>.</param>
		/// <remarks>
		/// This window should be top-level, not child; see <see cref="wnd.Window"/>.
		/// 
		/// Calls API <msdn>GetWindow</msdn>(<b>GW_ENABLEDPOPUP</b>). Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd EnabledOwned(bool orThis = false) {
			var r = Api.GetWindow(_w, Api.GW_ENABLEDPOPUP);
			if (orThis) {
				if (r.Is0) r = _w;
			} else {
				if (r == _w) r = default;
			}
			return r;
			//MSDN doc says it returns this window if there are no owned, but actually returns 0.
		}

		/// <summary>
		/// Gets the most recently active window in the chain of windows owned by this window, or this window itself if there are no such windows.
		/// </summary>
		/// <returns><c>default(wnd)</c> if failed.</returns>
		/// <param name="includeOwners">Can return an owner (or owner's owner and so on) of this window too.</param>
		/// <remarks>
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public wnd LastActiveOwnedOrThis(bool includeOwners = false) {
			var wRoot = RootOwnerOrThis(); //always use the root owner because GetLastActivePopup always returns _w if _w is owned
			var R = Api.GetLastActivePopup(wRoot);
			if (!includeOwners) {
				if (R != _w && wRoot != _w && !R.Is0) {
					for (var t = _w; !t.Is0; t = t.Get.Owner) if (t == R) return _w;
				}
			}
			return R;
		}

		/// <summary>
		/// Gets the bottom-most owner window in the chain of owner windows of this window.
		/// If this window is not owned, returns this window.
		/// </summary>
		/// <returns><c>default(wnd)</c> if failed.</returns>
		/// <param name="supportControls">If this is a child window, use its top-level parent window instead.</param>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public wnd RootOwnerOrThis(bool supportControls = false) {
			//return Api.GetAncestor(_w, Api.GA_ROOTOWNER); //slow, and can return Get.Root, eg for combolbox

			if (supportControls) {
				var r = Api.GetAncestor(_w, Api.GA_ROOTOWNER);
				return r == root ? _w : r;
				//never mind speed, better make it simple
			} else { //fast
				for (wnd r = _w, t; ; r = t) {
					t = r.Get.Owner;
					if (t.Is0) return r.IsAlive ? r : default;
				}
			}
		}

		/// <summary>
		/// Gets all owner windows (owner, its owner and so on) of this window.
		/// </summary>
		/// <param name="andThisWindow">Add this window (or its top-level parent if control) as the first list element.</param>
		/// <param name="onlyVisible">Skip invisible windows.</param>
		/// <remarks>
		/// This window can be top-level window or control.
		/// </remarks>
		public wnd[] AllOwners(bool andThisWindow = false, bool onlyVisible = false)
			=> Owners(andThisWindow, onlyVisible).ToArray();

		///
		[EditorBrowsable(EditorBrowsableState.Never)] //returns List, whereas all other public functions return array
		public List<wnd> Owners(bool andThisWindow = false, bool onlyVisible = false) {
			var a = new List<wnd>();
			for (var w = Window; !w.Is0 && w != root; w = w.Get.Owner) {
				if (!andThisWindow) { andThisWindow = true; continue; }
				if (!onlyVisible || w.IsVisible) a.Add(w);
			}
			return a;
		}

		/// <summary>
		/// Gets windows owned by this window.
		/// </summary>
		/// <param name="allDescendants">Also get indirectly owned windows (owned by owned windows).</param>
		/// <param name="andThisWindow">Add this window to the array. Since the array is sorted like in the Z order, usually it is the last element, but not always.</param>
		/// <returns>Array containing zero or more elements. Sorted like in the Z order.</returns>
		public wnd[] AllOwned(bool allDescendants, bool andThisWindow = false) {
			if (allDescendants) {
				var od = new OwnedDescendants_();
				var ai = od.GetIndices(_w, andOwner: andThisWindow);
				if (ai.Count == 0) return Array.Empty<wnd>();
				var ar = new wnd[ai.Count];
				for (int i = 0; i < ai.Count; i++) ar[i] = od.all[ai[i]];
				return ar;
			} else {
				var ar = new List<wnd>();
				var all = allWindowsZorder(); //the slowest part
				foreach (var k in all) {
					if (k == _w) { if (andThisWindow) ar.Add(k); continue; }
					if (k.Get.Owner == _w) ar.Add(k);
				}
				return ar.ToArray();
			}
		}

		/// <summary>
		/// Gets indices of owned windows of the specified window, including all descendants.
		/// </summary>
		internal class OwnedDescendants_ {
			/// <summary>
			/// All top-level windows in Z order.
			/// The array is created once in ctor.
			/// </summary>
			public readonly wnd[] all;

			/// <summary>
			/// Owners of all windows that are in the <i>all</i> array.
			/// The array is created once in ctor.
			/// </summary>
			public readonly int[] owners;

			public OwnedDescendants_() {
				all = getwnd.allWindowsZorder();
				owners = new int[all.Length];
				for (int i = 0; i < all.Length; i++) owners[i] = (int)all[i].Get.Owner;
			}

			/// <summary>
			/// Gets indices of owned windows of the specified window, including all descendants.
			/// Can be called multiple times for different owner windows; uses arrays created in ctor (the slowest part).
			/// </summary>
			/// <param name="owner">Owner window.</param>
			/// <param name="skip">A callback function that receives descendant indice and can return <c>true</c> to skip that window and its descendants.</param>
			/// <param name="andOwner">Add <i>owner</i> to the list too, at the position matching the Z order.</param>
			/// <returns>List of <see cref="all"/> indices of owned windows. Sorted like in the Z order. Not <c>null</c>.</returns>
			public List<int> GetIndices(wnd owner, Func<int, bool> skip = null, bool andOwner = false) {
				var ai = new List<int>();
				_Owned(owner);

				void _Owned(wnd owner) {
					int oint = (int)owner;
					for (int i = 0; i < owners.Length; i++) {
						if (owners[i] == oint) {
							if (skip != null && skip(i)) continue;
							ai.Add(i);
							_Owned(all[i]);
						}
					}
				}

				if (andOwner) {
					int j = Array.IndexOf(all, owner);
					if (j >= 0) ai.Add(j); else Debug_.Print("owner not in all");
				}
				ai.Sort();
				return ai;
			}
		}

		#endregion

		#region static

		/// <summary>
		/// Gets the first top-level window in the Z order.
		/// </summary>
		/// <remarks>
		/// Probably it is a topmost window. To get the first non-topmost window, use <see cref="top2"/>.
		/// Calls API <msdn>GetTopWindow</msdn>.
		/// </remarks>
		public static wnd top => Api.GetTopWindow(default);

		/// <summary>
		/// Finds and returns the first non-topmost window in the Z order.
		/// </summary>
		/// <param name="lastTopmost">Receives the last topmost window.</param>
		/// <remarks>
		/// This function is slower than <see cref="top"/> etc. Enumerates windows, because there is no API to get directly.
		/// </remarks>
		public static wnd top2(out wnd lastTopmost) {
			wnd w, lastTM, w2 = default, lastTM2 = default;
			for (; ; ) { //repeat until gets same results 2 times. At any time windows can be destroyed, reordered, reparented. The second time 2-3 times faster.
				lastTM = default; w = top; if (w.Is0) break; //no windows in this session
				for (; w.IsTopmost; w = w.Get.Next()) lastTM = w;
				if (w == w2 && lastTM == lastTM2 && !w.Is0) break;
				w2 = w; lastTM2 = lastTM;
			}
			lastTopmost = lastTM;
			return w;
		}

		/// <summary>
		/// Calls API <msdn>GetDesktopWindow</msdn>. It gets the virtual parent window of all top-level windows.
		/// </summary>
		/// <remarks>
		/// <note>It is not the desktop window (see <see cref="desktop"/>) that displays icons and wallpaper.</note>
		/// </remarks>
		public static wnd root { get; } = Api.GetDesktopWindow();

		/// <summary>
		/// Calls API <msdn>GetShellWindow</msdn>. It gets a window of the shell process (usually process "explorer", class name "Progman").
		/// </summary>
		/// <returns><c>default(wnd)</c> if there is no shell process, for example Explorer process killed/crashed and still not restarted, or if using a custom shell that does not register a shell window.</returns>
		/// <remarks>
		/// It can be the window that contains desktop icons (see <see cref="desktop"/>) or other window of the same thread.
		/// </remarks>
		public static wnd shellWindow => Api.GetShellWindow();

		/// <summary>
		/// Gets the desktop window and its child control that displays desktop icons and wallpaper.
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		/// <param name="desktopWindow">Receives the top-level desktop window. Class name "Progman" or "WorkerW".</param>
		/// <param name="control">Receives the control of "SysListView32" class that contains icons and wallpaper.</param>
		/// <remarks>
		/// This function is not very reliable. May stop working on a new Windows version or don't work with a custom shell.
		/// Fails if there is no shell process, for example Explorer process killed/crashed and still not restarted, or if using a custom shell that does not register a shell window.
		/// </remarks>
		public static bool desktop(out wnd desktopWindow, out wnd control) {
			wnd w = shellWindow;
			var f = new wndChildFinder(cn: "SysListView32");
			if (!f.Exists(w)) w = wnd.find(null, "WorkerW", WOwner.Thread(w.ThreadId), also: t => f.Exists(t));
			desktopWindow = w;
			control = f.Result;
			return !w.Is0;

			//info:
			//If no wallpaper, desktop is GetShellWindow, else a visible WorkerW window.
			//When was no wallpaper and user selects a wallpaper, explorer creates WorkerW and moves the same SysListView32 control to it.

			//tested: with COM (IShellWindows -> IShellBrowser -> IShellView.GetWindow) slower 17 times.
		}

		//FUTURE:
		//public static wnd editorWindow =>
		//public static wnd editorCodeControl =>

		#endregion
	}

	#region main windows

	public partial struct getwnd {
		/// <summary>
		/// Returns <c>true</c> if window <i>w</i> is considered a main window, ie probably is in the Windows taskbar.
		/// Returns <c>false</c> if it is invisible, cloaked, owned, toolwindow, menu, etc.
		/// </summary>
		/// <param name="w">A top-level window.</param>
		/// <param name="allDesktops">On Windows 10 include (return <c>true</c> for) windows on all virtual desktops. On Windows 8 include Windows Store apps if possible; read more: <see cref="allWindows(bool, bool)"/>.</param>
		/// <param name="skipMinimized">Return <c>false</c> if <i>w</i> is minimized.</param>
		public static bool isMainWindow(wnd w, bool allDesktops = false, bool skipMinimized = false) {
			if (!w.IsVisible) return false;

			var exStyle = w.ExStyle;
			if ((exStyle & WSE.APPWINDOW) == 0) {
				if ((exStyle & (WSE.TOOLWINDOW | WSE.NOACTIVATE)) != 0) return false;
				if (!w.Get.Owner.Is0) return false;
			}

			if (skipMinimized && w.IsMinimized) return false;

			if (osVersion.minWin10) {
				if (w.IsCloaked) {
					if (!allDesktops) return false;
					if ((exStyle & WSE.NOREDIRECTIONBITMAP) != 0) { //probably a store app
						switch (w.ClassNameIs("Windows.UI.Core.CoreWindow", "ApplicationFrameWindow")) {
						case 1: return false; //Windows search, experience host, etc. Also app windows that normally would sit on ApplicationFrameWindow windows.
						case 2: if (_WindowsStoreAppFrameChild(w).Is0) return false; break; //skip hosts
						}
					}
				}
			} else if (osVersion.minWin8) {
				if ((exStyle & WSE.NOREDIRECTIONBITMAP) != 0 && !w.HasStyle(WS.CAPTION)) {
					if (!allDesktops && (exStyle & WSE.TOPMOST) != 0) return false; //skip store apps
					if (shellWindow.GetThreadProcessId(out var pidShell) != 0 && w.GetThreadProcessId(out var pid) != 0 && pid == pidShell) return false; //skip captionless shell windows
				}
				//On Win8 impossible to get next window like Alt+Tab.
				//	All store apps are topmost, covering non-topmost desktop windows.
				//	DwmGetWindowAttribute makes no sense here.
				//	Desktop windows are never cloaked, inactive store windows are cloaked, etc.
			}

			return true;
		}

		/// <summary>
		/// Gets main windows, ie those that probably are in the Windows taskbar.
		/// </summary>
		/// <returns>Array containing zero or more <b>wnd</b>.</returns>
		/// <param name="allDesktops">On Windows 10 include windows on all virtual desktops. On Windows 8 include Windows Store apps if possible; read more: <see cref="allWindows(bool, bool)"/>.</param>
		/// <remarks>
		/// Uses <see cref="isMainWindow"/>.
		/// Does not match the order of buttons in the Windows taskbar.
		/// </remarks>
		public static wnd[] mainWindows(bool allDesktops = false) {
			var a = new List<wnd>();
			foreach (var w in allWindows(onlyVisible: true)) {
				if (isMainWindow(w, allDesktops: allDesktops)) a.Add(w);
			}
			return a.ToArray();

			//Another way - UI Automation:
			//	var x = new CUIAutomation();
			//	var cond = x.CreatePropertyCondition(30003, 0xC370); //UIA_ControlTypePropertyId, UIA_WindowControlTypeId
			//	var a = x.GetRootElement().FindAll(TreeScope.TreeScope_Children, cond);
			//	for(int i = 0; i < a.Length; i++) print.it((wnd)a.GetElement(i).CurrentNativeWindowHandle);
			//Advantages: 1. Maybe can filter unwanted windows more reliably, although I did't notice a difference.
			//Disadvantages: 1. Skips windows of higher integrity level (UAC). 2. Cannot include cloaked windows, eg those in inactive Win10 virtual desktops. 3. About 1000 times slower, eg 70 ms vs 70 mcs; cold 140 ms.
		}

		/// <summary>
		/// Gets next window in the Z order, skipping invisible and other windows that probably are not in the Windows taskbar.
		/// </summary>
		/// <returns><c>default(wnd)</c> if there are no such windows.</returns>
		/// <param name="w">Start from this window. If <c>default(wnd)</c>, starts from the top of the Z order.</param>
		/// <param name="allDesktops">On Windows 10 include windows on all virtual desktops. On Windows 8 include Windows Store apps if possible; read more: <see cref="allWindows(bool, bool)"/>.</param>
		/// <param name="skipMinimized">Skip minimized windows.</param>
		/// <param name="retryFromTop">If <i>w</i> is not <c>default(wnd)</c> and there are no matching windows after it, retry from the top of the Z order. Then can return <i>w</i>.</param>
		/// <remarks>
		/// Uses <see cref="isMainWindow"/>.
		/// This function is quite slow. Does not match the order of buttons in the Windows taskbar.
		/// </remarks>
		public static wnd nextMain(wnd w = default, bool allDesktops = false, bool skipMinimized = false, bool retryFromTop = false) {
			if (w.Is0) retryFromTop = false;

			for (; ; ) {
				w = w.Is0 ? getwnd.top : w.Get.Next();
				if (w.Is0) {
					if (retryFromTop) { retryFromTop = false; continue; }
					return default;
				}
				if (isMainWindow(w, allDesktops: allDesktops, skipMinimized: skipMinimized)) return w;
			}
		}
	}

	/// <summary>
	/// Activates next non-minimized main window, like with Alt+Tab.
	/// </summary>
	/// <returns><c>true</c> if activated; <c>false</c> if there is no such window or if failed to activate.</returns>
	/// <remarks>
	/// Uses <see cref="getwnd.nextMain"/>, <see cref="getwnd.LastActiveOwnedOrThis"/>, <see cref="Activate"/>.
	/// An alternative way - send Alt+Tab keys, but it works not everywhere.
	/// </remarks>
	public static bool switchActiveWindow() {
		try {
			wnd wActive = active, wRO = wActive.Get.RootOwnerOrThis();
			wnd wMain = getwnd.nextMain(wRO, skipMinimized: true, retryFromTop: true);
			if (!wMain.Is0 && wMain != wActive && wMain != wRO) {
				var wMainOrOwned = wMain.Get.LastActiveOwnedOrThis();
				if (!wMainOrOwned.Is0) {
					//print.it(wMainOrOwned);
					wMainOrOwned.Activate();
					return true;
				}
			}
		}
		catch (AuWndException) { }
		return false;

		//notes:
		//This function ignores minimized windows, because:
		//	Impossible to get exactly the same window as Alt+Tab, which on Win10 is the most recently minimized.
		//	Activating a random minimized window is not very useful. Maybe in the future.
		//The order of windows used by Alt+Tab is not the same as the Z order, especially when there are minimized windows.
		//	We cannot get that order and have to use the Z order. It seems that the order is different on Win10 but not on Win7.
		//After minimizing a window its position in the Z order is undefined.
		//	Most windows then are at the bottom when used the Minimize button or ShowWindow etc.
		//	But some are at the top, just after the active window, for example MS Document Explorer (Win7 SDK).
		//	Also at the top when minimized with the taskbar button.
	}

	#endregion

	/// <summary>
	/// Gets the top-level parent window of this control.
	/// If this is a top-level window, returns this.
	/// </summary>
	/// <returns><c>default(wnd)</c> if this window is invalid.</returns>
	/// <remarks>Supports <see cref="lastError"/>.</remarks>
	/// <seealso cref="Get"/>
	public wnd Window {
		get {
			var w = Api.GetAncestor(this, Api.GA_ROOT);
			if (w.Is0 && this == getwnd.root) w = this;
			return w;
		}
	}
	//This is in getwnd and here, because frequently used.

	/// <summary>
	/// Returns <c>true</c> if this is a child window (control), <c>false</c> if top-level window.
	/// </summary>
	/// <remarks>
	/// Supports <see cref="lastError"/>.
	/// Another way is <c>w.HasStyle(WS.CHILD)</c>. It is faster but less reliable, because some top-level windows have <b>WS_CHILD</b> style and some child windows don't.
	/// </remarks>
	/// <seealso cref="getwnd.DirectParent"/>
	public bool IsChild => !Get.DirectParent.Is0;

	/// <summary>
	/// Returns <c>true</c> if this is a child or descendant of window <i>w</i>.
	/// </summary>
	/// <remarks>
	/// Calls API <msdn>IsChild</msdn>.
	/// Supports <see cref="lastError"/>.
	/// </remarks>
	public bool IsChildOf(wnd w) { return Api.IsChild(w, this); }

	/// <summary>
	/// Returns <c>(wnd)GetWindowLong(GWL.HWNDPARENT)</c>.
	/// </summary>
	internal wnd ParentGWL_ => (wnd)GetWindowLong(GWL.HWNDPARENT);

	/// <summary>
	/// Gets the active (foreground) window.
	/// Calls API <msdn>GetForegroundWindow</msdn>.
	/// </summary>
	/// <value>Returns <c>default(wnd)</c> if there is no active window; more info: <see cref="WndUtil.WaitForAnActiveWindow"/>.</value>
	public static wnd active => Api.GetForegroundWindow();

	/// <summary>
	/// Returns <c>true</c> if this window is the active (foreground) window.
	/// </summary>
	public bool IsActive => !Is0 && this == Api.GetForegroundWindow();

	//FUTURE: static bool IsActiveAny(list of wnd or wndFinder).

	/// <summary>
	/// Returns <c>true</c> if this window is the active (foreground) window.
	/// If this is <see cref="getwnd.root"/>, returns <c>true</c> if there is no active window.
	/// </summary>
	internal bool IsActiveOrNoActiveAndThisIsWndRoot_ {
		get {
			if (Is0) return false;
			var f = Api.GetForegroundWindow();
			return this == (f.Is0 ? getwnd.root : f);
		}
	}

	/// <summary>
	/// Returns <c>true</c> if this window is directly or indirectly owned by window <i>w</i>.
	/// </summary>
	/// <param name="w"></param>
	/// <param name="level">
	/// Return <c>true</c> if:
	/// <br/>• 0 - <i>w</i> is direct owner. See <see cref="getwnd.Owner"/>.
	/// <br/>• 1 - <i>w</i> is direct or indirect owner (eg owner's owner).
	/// <br/>• 2 - <i>w</i> is direct or indirect owner, or this is a menu-like window that looks like owned by <i>w</i> (same thread, topmost, etc).
	/// </param>
	/// <remarks>
	/// Many popup menus and similar temporary windows don't have an owner window. Instead they have topmost style. If <i>level</i> is 2, this function tries to detect this case. In most cases it works, but not always (depends on styles).
	/// </remarks>
	public bool IsOwnedBy(wnd w, int level) {
		if ((uint)level > 2) throw new ArgumentOutOfRangeException(nameof(level));
		int tid = 0;
		return IsOwnedBy2_(w, level, ref tid);
	}

	/// <summary>
	/// This version is used by "find window" functions.
	/// Uses tid to avoid getting w thread id (slow) for each window; let it be 0 initially.
	/// </summary>
	internal bool IsOwnedBy2_(wnd w, int level, ref int tid) {
		for (var o = Get.Owner; !o.Is0; o = o.Get.Owner) {
			if (o == w) return true;
			if (level == 0) break;
		}

		if (level > 1) {
			//print.it("level>1");
			var es = ExStyle;
			if (es.Has(WSE.TOPMOST) && es.HasAny(WSE.TOOLWINDOW | WSE.NOACTIVATE) && !es.Has(WSE.APPWINDOW)) {
				var s = Style;
				if (!s.Has(WS.CAPTION) && !s.Has(WS.THICKFRAME))
					switch (s & (WS.CHILD | WS.POPUP)) {
					case WS.POPUP:
					case WS.CHILD when !w.IsChild: //ComboLBox
						if (tid == 0) tid = w.ThreadId; //much slower than style/exstyle
						if (ThreadId == tid)
							return !w.IsTopmost || ZorderIsAbove(w);
						break;
					}
			}
		}

		return false;
	}
}
