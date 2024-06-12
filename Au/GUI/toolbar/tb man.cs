namespace Au;

public partial class toolbar {
	class _OwnerWindow {
		public readonly List<toolbar> a;
		public readonly wnd w;
		public bool visible;
		bool _updatedOnce;
		RECT _rect, _clientRect;
		SIZE _prevSize, _prevClientSize;
		//public readonly int thread;
		
		public _OwnerWindow(wnd w) {
			this.w = w;
			//thread = w.ThreadId;
			a = new List<toolbar>();
		}
		
		public void AddTB(toolbar tb) {
			a.Add(tb);
			tb._ow = this;
		}
		
		public (bool visible, bool dead) IsVisible() {
			lastError.clear();
			if (!w.IsVisible) return (false, lastError.code != 0);
			return (!w.IsMinimized && !w.IsCloaked, false);
			//speed: IsCloaked now on Win10 quite fast, faster than GetRect
		}
		
		public bool UpdateRect(out bool changed) {
			changed = false;
			int have = 0;
			foreach (var tb in a) {
				if (tb._oc != null || tb._os != null) continue;
				if (tb._followClientArea) {
					if (0 != (have & 2)) continue;
					if (!w.GetClientRect(out var r, inScreen: true)) return false;
					if (r != _clientRect) {
						_prevClientSize = (_clientRect.Width, _clientRect.Height);
						_clientRect = r;
						changed = true;
					}
					have |= 2;
				} else {
					if (0 != (have & 1)) continue;
					if (!w.GetRect(out var r)) return false;
					if (r != _rect) {
						_prevSize = (_rect.Width, _rect.Height);
						_rect = r;
						changed = true;
					}
					have |= 1;
				}
				if (have == 3) break;
			}
			if (!_updatedOnce) _updatedOnce = changed = true;
			return true;
		}
		
		public RECT GetCachedRect(toolbar tb) => tb._followClientArea ? _clientRect : _rect;
		
		public SIZE GetPrevSize(toolbar tb) => tb._followClientArea ? _prevClientSize : _prevSize;
	}
	
	class _OwnerControl {
		public readonly wnd c;
		public readonly ITBOwnerObject oo;
		public RECT cachedRect;
		public SIZE prevSize;
		bool _updatedOnce;
		
		public _OwnerControl(wnd control, ITBOwnerObject ioo) {
			c = control;
			oo = ioo;
		}
		
		public (bool visible, bool dead) IsVisible(bool parentVisible = true) {
			if (!c.Is0) {
				lastError.clear();
				if (!c.IsVisible) return (false, lastError.code != 0);
				if (!parentVisible || c.IsMinimized) return default; //never mind: ancestors controls may be minimized
			}
			if (oo != null) {
				if (!oo.IsAlive) return (false, true);
				if (!oo.IsVisible) return default;
			}
			return (true, false);
		}
		
		public bool UpdateRect(out bool changed) {
			bool ok = oo != null ? oo.GetRect(out RECT r) : c.GetRect(out r);
			if (changed = ok && r != cachedRect) {
				prevSize = (cachedRect.Width, cachedRect.Height);
				cachedRect = r;
			}
			if (!_updatedOnce) _updatedOnce = changed = true;
			return ok;
		}
	}
	
	class _OwnerScreen {
		public _OwnerScreen(toolbar tb, screen scrn) {
			_tb = tb;
			_scrn = (_isAuto = scrn.IsEmpty) ? screen.of(_tb._sett.screenx, _tb._sett.screeny) : scrn.Now;
			UpdateRect(out _);
		}
		
		toolbar _tb;
		screen _scrn;
		bool _isAuto;
		public RECT cachedRect;
		public SIZE prevSize;
		
#if DEBUG
		public screen Screen {
			get {
				Debug_.PrintIf(!_scrn.IsAlive, "screen not alive");
				return _scrn;
			}
		}
#else
		public screen Screen => _scrn;
#endif
		
		//public bool IsAuto => _isAuto;
		
		public bool UpdateRect(out bool changed) {
			RECT r = _scrn.Rect;
			if (changed = r != cachedRect && !r.Is0) {
				prevSize = (cachedRect.Width, cachedRect.Height);
				cachedRect = r;
			}
			return true;
		}
		
		//called from _WmWindowPosChanged
		public void UpdateIfAutoScreen() {
			if (!_isAuto) return;
			var r = screen.of(_tb._w).Rect;
			int x = 0, y = 0; if (r.left != 0 || r.top != 0) { x = r.CenterX; y = r.CenterY; }
			if (x != _tb._sett.screenx || y != _tb._sett.screeny) {
				_scrn = screen.of(_tb._sett.screenx = x, _tb._sett.screeny = y);
				UpdateRect(out _);
			}
		}
		
		//Called on WM_DISPLAYCHANGE. If screen detached, sets _scrn = 0. When reattached, sets _scrn = new screen handle.
		public bool IsScreenInvalid() {
			if (!_scrn.IsAlive) {
				//Debug_.Print($"CloseIfScreenInvalid, {_tb.Name}, {screen.of(_tb._w, SODefault.Zero)}");
				
				_scrn = screen.of(new POINT(cachedRect.CenterX, cachedRect.CenterY), SODefault.Zero);
				return _scrn.Handle == default;
			}
			return false;
		}
	}
	
	_OwnerWindow _ow; //not null if owned
	_OwnerControl _oc; //not null if owned by a control or other object (ITBOwnerObject)
	_OwnerScreen _os; //not null if not owned or if anchor has flag Screen
	bool _followClientArea;
	
	[ThreadStatic] static _TBManager t_man;
	static _TBManager _Manager => t_man ??= new();
	
	class _TBManager {
		internal readonly List<toolbar> _atb = new();
		readonly List<_OwnerWindow> _aow = new();
		timer _timer;
		int _timerPeriod;
		WinEventHook _hook;
		int _tempHook;
		bool _inHook;
		
		public void Add(toolbar tb, wnd w, wnd c, ITBOwnerObject ioo) {
			bool isOwned = !w.Is0;
			if (isOwned) {
				if (!_FindOW(w, out var ow)) _aow.Add(ow = new _OwnerWindow(w));
				ow.AddTB(tb);
				if (!c.Is0 || ioo != null) tb._oc = new _OwnerControl(c, ioo);
			}
			
			_atb.Add(tb);
			
			if (_hook == null) {
				_hook = new WinEventHook([
					0, EEvent.OBJECT_REORDER,
					EEvent.OBJECT_CLOAKED, EEvent.OBJECT_UNCLOAKED,
					EEvent.SYSTEM_MOVESIZESTART, EEvent.SYSTEM_MOVESIZEEND,
					EEvent.SYSTEM_MINIMIZESTART, EEvent.SYSTEM_MINIMIZEEND,
					],
					_Hook,
					flags: EHookFlags.SKIPOWNTHREAD);
				_timer = new timer(_Timer);
			}
			
			tb._hide = TBHide.Owner;
			if (isOwned) {
				_SetTimer(250);
			} else {
				if (!_timer.IsRunning) _SetTimer(250);
				tb._FollowRect();
				//tb._Zorder();
				tb._SetVisible(true, TBHide.Owner);
			}
		}
		
		public void Remove(toolbar tb) {
			_atb.Remove(tb);
			var ow = tb._ow;
			if (ow != null) {
				ow.a.Remove(tb);
				if (ow.a.Count == 0) _aow.Remove(ow);
			}
		}
		
		void _SetTimer(int period) {
			_timer.Every(_timerPeriod = period);
		}
		
		void _Timer(timer t) {
			if (_timerPeriod != 250) _SetTimer(250);
			
			//remove closed toolbars and their owners if need. Now don't need because toolbars call Remove when closing.
			//for(int i = _atb.Count; --i >= 0;) {
			//	var tb = _atb[i];
			//	if(tb._closed) Remove(tb);
			//}
			
			//move/close/hide/show owned toolbars together with their owners
			for (int i = _aow.Count; --i >= 0;) {
				var ow = _aow[i];
				if (!_FollowOwner(ow)) {
					for (int j = ow.a.Count; --j >= 0;) {
						var tb = ow.a[j];
						tb.Close();
						bool rem1 = _atb.Remove(tb); Debug_.PrintIf(rem1, "");
					}
					bool rem2 = _aow.Remove(ow); Debug_.PrintIf(rem2, "");
					//actually don't need these two Remove, because tb.Close calls Remove. Just don't use RemoveAt and foreach.
				}
			}
			
			//occasionally may fail to zorder a toolbar. Retry several times.
			for (int i = _atb.Count; --i >= 0;) {
				var v = _atb[i];
				if (v._zorderRetry > 0) v._ZorderOwned();
			}
			
			_ZorderTimer();
			
			_ManageFullScreen();
		}
		
		//long _reorderTime;
		void _Hook(HookData.WinEvent d) {
			//print.it(d.event_, d.idObject, d.idChild, d.thread, d.w);
			if (d.w.Is0 || d.idObject != (d.event_ == EEvent.OBJECT_REORDER ? EObjid.CLIENT : EObjid.WINDOW) || d.idChild != 0) return;
			switch (d.event_) {
			case EEvent.OBJECT_REORDER when d.w == wnd.getwnd.root: //the hook does not give the window, only its thread id
				_Zorder();
				break;
			case EEvent.SYSTEM_MOVESIZESTART when _tempHook == 0:
				if (_FindOW(d.w, out _)) _tempHook = _hook.Add(EEvent.OBJECT_LOCATIONCHANGE, flags: EHookFlags.SKIPOWNTHREAD);
				break;
			case EEvent.SYSTEM_MOVESIZEEND when _tempHook != 0:
				_hook.Remove(_tempHook);
				_tempHook = 0;
				break;
			case EEvent.OBJECT_LOCATIONCHANGE:
			case EEvent.OBJECT_CLOAKED:
			case EEvent.OBJECT_UNCLOAKED:
			case EEvent.SYSTEM_MINIMIZESTART:
				//Debug_.PrintIf(_inHook, "_inHook"); //it's ok
				if (!_inHook && _FindOW(d.w, out _OwnerWindow ow)) {
					//prevent reenter.
					//	The ITBOwnerObject may retrieve sent messages, eg when getting acc rect.
					//	It's ok if hook missed. We'll call it on timer or next OBJECT_LOCATIONCHANGE.
					_inHook = true;
					try { _FollowOwner(ow); }
					finally { _inHook = false; }
				}
				break;
			case EEvent.SYSTEM_MINIMIZEEND:
				if (_FindOW(d.w, out _)) _SetTimer(150);
				break;
			}
			
			//SYSTEM_MOVESIZESTART and SYSTEM_MOVESIZEEND temporarily add/remove OBJECT_LOCATIONCHANGE to move toolbars with the owner window.
			//	Cannot make OBJECT_LOCATIONCHANGE always active, because it is called frequently, on each cursor position change etc.
			//	There are no other not-in-process hooks to detect moved windows. For CBT hook need 2 processes - 64bit and 32bit.
			
			//OBJECT_REORDER keeps toolbars above their owner windows in the Z order.
			//	Easier would be to make the owner natively owner. But then problems:
			//	1. If this process is admin, the owner's process cannot receive drag&drop from other non-admin processes. Don't know why, probably it is a Windows bug.
			//	2. Fails if owner's process is a Store app. Also probaby if higher UAC IL.
			//	3. In some cases possible various anomalies, for example wrong Z order of windows after closing the owner window.
			//	4. All unknown and future things like those.
			//	In QM some of these problems were solved by adding a child window to the owner window and making it the native owner of the toolbar. But then other problems, eg DPI-scaling.
			
			//PROBLEM: OBJECT_REORDER makes creating windows slower.
			//	For example, combobox controls send OBJECT_REORDER when adding items. Two for each item that would be visible in the drop-down list.
			//	Tested: standard dialog box with 12 comboboxes, each with 30 such items. We receive ~720 OBJECT_REORDER.
			//		If there are 4 processes with 1 OBJECT_REORDER hook, dialog startup time increases 50%, from 360 to 540 ms.
			//Other used hooks aren't called frequently. Except OBJECT_LOCATIONCHANGE, but it is temporary.
		}
		
		bool _FindOW(wnd owner, out _OwnerWindow ow) {
			foreach (var v in _aow) if (v.w == owner) { ow = v; return true; }
			ow = null; return false;
		}
		
		bool _FollowOwner(_OwnerWindow ow) {
			var (visibleW, dead) = ow.IsVisible();
			if (dead) return false;
			
			bool changedRectW = false;
			if (visibleW) visibleW = ow.UpdateRect(out changedRectW);
			ow.visible = visibleW;
			
			for (int i = ow.a.Count; --i >= 0;) {
				bool visible, changedRect;
				var tb = ow.a[i];
				var oc = tb._oc;
				if (oc == null) {
					visible = visibleW;
					changedRect = changedRectW;
				} else {
					(visible, dead) = oc.IsVisible(visibleW);
					if (dead) {
						tb.Close();
						ow.a.RemoveAt(i);
						continue;
					}
					if (visible) visible = oc.UpdateRect(out changedRect); else changedRect = false;
				}
				bool changedVisible = visible == tb._hide.Has(TBHide.Owner);
				if (visible && (changedRect || changedVisible)) { // || changedVisible is for new toolbars, but it's ok to call for old too
					tb._FollowRect(true);
				}
				if (changedVisible) {
					tb._SetVisible(visible, TBHide.Owner);
					if (visible) tb._ZorderOwned();
				}
			}
			
			return true;
		}
		
		void _ManageFullScreen(toolbar tb = null) {
			if (tb?.MiscFlags.Has(TBFlags.HideWhenFullScreen) ?? _atb.Any(o => o.MiscFlags.Has(TBFlags.HideWhenFullScreen))) {
				var w = wnd.active;
				bool isFS = w.IsFullScreen_(out var scrn);
				if (tb != null) tb._ManageFullScreen(isFS, w, scrn);
				else foreach (var v in _atb) if (v.MiscFlags.Has(TBFlags.HideWhenFullScreen)) v._ManageFullScreen(isFS, w, scrn);
			}
		}
		
		//EEvent.OBJECT_REORDER
		void _Zorder() {
			for (int i = _atb.Count; --i >= 0;) {
				var tb = _atb[i];
				if (tb.IsOwned) tb._ZorderOwned();
				else if (_zorderDelay < 1) _zorderDelay = 2;
			}
#if false
			//This version of zordering owned toolbars is faster but unreliable.
			//	1. For console windows getwindowthreadprocessid gives wrong thread id. Hook receives the correct id.
			//	2. When clicked client area of a Store app, hook receives thread id of the child control. It is different than that of the main host window.
			//	3. All unknown and future things like those.
			foreach(var v in _aow) {
				if(v.thread != d.idThread) continue;
				foreach(var tb in v.a) tb._ZorderOwned();
			}
#endif
		}
		int _zorderDelay;
		
		void _ZorderTimer() {
			//250-500 ms after EEvent.OBJECT_REORDER
			if (_zorderDelay < 1 || --_zorderDelay > 0) return;
			
			//Ensure the toolbar is on top of the primary taskbar. Or ontop of the active window if the primary taskbar is behind it.
			//	Not on top of all topmost windows. Would cover tooltips etc, fight with sibling toolbars, etc.
			var taskbar = s_taskbar.FindFast(null, "Shell_TrayWnd", false); //of the primary screen
			var active = wnd.active;
			var w = !active.Is0 && active != taskbar && (taskbar.Is0 || !taskbar.ZorderIsAbove(active) && !active.IsOfThisProcess) ? active : taskbar;
			if (w.Is0) return;
			for (int i = _atb.Count; --i >= 0;) {
				var v = _atb[i];
				if (v.IsOwned) continue;
				var tb = v.Hwnd;
				if (!tb.ZorderIsAbove(w) && w.IsVisible) {
					if (w.IsTopmost) {
						tb.ZorderL_(w, before: true);
					} else if (!tb.IsTopmost) {
						tb.ZorderL_(SpecHWND.TOPMOST);
					} else {
						//Windows 11 bug: sometimes, when activated a non-topmost window, it becomes on top of topmost windows, eg of taskbars and unowned toolbars.
						//	Afterwards activating other normal windows makes them on top of topmost windows too.
						//	Impossible to reproduce, it happens randomly, once in several days or weeks.
						Debug_.Print($"toolbar behind the active non-topmost window: {w}");
						
						//workaround 1. Sometimes works. Fails if w is "Caret Listener Shim Window"; then 2 fails too.
						tb.ZorderL_(SpecHWND.NOTOPMOST);
						tb.ZorderL_(SpecHWND.TOPMOST);
						if (!tb.ZorderIsAbove(w)) {
							Debug_.Print("Workaround 1 failed.");
							
							//workaround 2. Not much tested here, but `w1.ZorderL_(w2); w2.ZorderL_(w1);` works elsewhere.
							tb.ZorderL_(w, before: true); //may not work, although returns true. ZorderL_ gets previous window, and it may be HWND_TOP (0). SWP(HWND_TOP) does nothing if tb is behind the active non-topmost window (w).
							if (!tb.ZorderIsAbove(w)) { //workaround
								tb.ZorderL_(w);
								w.ZorderL_(tb);
								Debug_.PrintIf(!tb.ZorderIsAbove(w), $"Workaround 2 failed.");
							}
							tb.ZorderL_(SpecHWND.TOPMOST);
						}
					}
				}
			}
		}
		static wnd.Cached_ s_taskbar;
	}
	
	void _ZorderOwned() {
		Debug.Assert(IsOwned);
		if (IsOwned && _ow.visible) {
			wnd wt = _w, wo = _ow.w;
			
			//Some windows, eg UiPath, have an owned "shadow frame" window that may cover toolbar parts that aren't entirely inside the owner window's rect.
			//	Toolbars should detect such windows and zorder above them. Can't just blindly zorder above the topmost owned.
			//	But detecting can be difficult/slow/unreliable. Need to get all owned windows or all thread windows, etc.
			//	Never mind. Let users don't move toolbars to such places where they may be covered.
			//if (_zorderedOnce && wo.IsActive) {
			//	var w2 = wo.Get.EnabledOwned(); //may be not that window, eg a tooltip
			//	//print.it("eo", w2);
			//	if (!w2.Is0 && w2.Rect.Contains(wo.Rect)) wo = w2;
			//}
			//print.it(wo);
			
			if (!_zordered || !wt.ZorderIsAbove(wo)) {
				_zordered = wt.ZorderAbove(wo) || !wo.IsAlive;
				if (!_zordered) {
					var ec = lastError.code;
					if (wt.ZorderIsAbove(wo)) {
						_zordered = true;
					} else if (
#if !DEBUG
						_zorderRetry == 1 //the last retry
#else
						_zorderRetry > 0 //any retry. It's OK if sometimes fails first time, brobably then it's a bad time to zorder.
#endif
						) {
						var es = ec == Api.ERROR_ACCESS_DENIED && wo.UacAccessDenied ? "This process should run as admin, or owner's process not as admin." : lastError.messageFor(ec);
						print.warning($"Failed to Z-order toolbar '{_name}' above owner window. {es}");
					}
				}
			}
			
			if (_zordered) _zorderRetry = 0; else if (_zorderRetry == 0) _zorderRetry = 5; else _zorderRetry--;
			
			//never mind: when clicked owner's caption, we receive 2 hook events and need to ZorderAbove 2 times. Speed is OK, but flickers more often.
			//	When we ZorderAbove on mouse down, Windows also zorders the window on mouse up, and then we receive second event.
			//	Possible workarounds:
			//	1. Temporarily make wt nativaly owned by _ow.w. Restore after 500 ms. But fails with higher UAC IL windows and appstore windows.
			//	2. Temporarily make wt topmost. Restore after 500 ms. But Windows makes it difficult and possibly unreliable.
		}
	}
	bool _zordered;
	byte _zorderRetry;
	
	void _ManageFullScreen(bool isFS, wnd wFore, screen scrn) {
		if (_inMoveSize) return;
		bool hide;
		if (!isFS) hide = false;
		else if (IsOwned) hide = OwnerWindow == wFore;
		else hide = screen.of(_w, SODefault.Zero) == scrn;
		
		_SetVisible(!hide, TBHide.FullScreen);
	}
	
	void _FollowRect(bool onFollowOwner = false) {
		if (_inMoveSize) return;
		if (onFollowOwner && Anchor.OfScreen() && _followedOnce) return;
		if (!onFollowOwner && _hide.Has(TBHide.Owner) && IsOwned && OwnerWindow.IsMinimized) return;
		
		bool dpiChanged = _os == null && _SetDpi();
		//print.it(dpiChanged, OwnerWindow);
		
		var (r, prevSize) = _GetCachedOwnerRect();
		//print.it(r, Anchor, _xy, Size);
		
		var swp = SWPFlags.NOZORDER | SWPFlags.NOOWNERZORDER | SWPFlags.NOACTIVATE;
		var bounds = _w.Rect;
		int x, y, cx = bounds.Width, cy = bounds.Height;
		
		if (Anchor.HasLeft()) {
			x = (Anchor.OppositeX() ? r.right : r.left) + _Scale(_offsets.Left, true);
			if (Anchor.HasRight() && (!_followedOnce || r.Width != prevSize.width)) {
				if (_preferSize) _offsets.Right = _Unscale(r.right - x - cx, true);
				else cx = Math.Max(r.right - _Scale(_offsets.Right, true) - x, 2); //_WmWindowPosChanging will limit min max if need
			}
		} else {
			Debug.Assert(Anchor.HasRight());
			x = (Anchor.OppositeX() ? r.left : r.right) - _Scale(_offsets.Right, true) - cx;
		}
		if (Anchor.HasTop()) {
			y = (Anchor.OppositeY() ? r.bottom : r.top) + _Scale(_offsets.Top, true);
			if (Anchor.HasBottom() && (!_followedOnce || r.Height != prevSize.height)) {
				if (_preferSize) _offsets.Bottom = _Unscale(r.bottom - y - cy, true);
				else cy = Math.Max(r.bottom - _Scale(_offsets.Bottom, true) - y, 2);
			}
		} else {
			Debug.Assert(Anchor.HasBottom());
			y = (Anchor.OppositeY() ? r.top : r.bottom) - _Scale(_offsets.Bottom, true) - cy;
		}
		
		if (_preferSize) {
			_preferSize = false;
			_sett.offsets = _offsets;
		}
		
		if (x == bounds.left && y == bounds.top) swp |= SWPFlags.NOMOVE;
		if (cx == bounds.Width && cy == bounds.Height) swp |= SWPFlags.NOSIZE;
		if (!swp.Has(SWPFlags.NOMOVE | SWPFlags.NOSIZE)) {
			_ignorePosChanged = (byte)(dpiChanged ? 2 : 1);
			_w.SetWindowPos(swp, x, y, cx, cy);
			_ignorePosChanged = 0;
		}
		
		bool followedOnce = _followedOnce;
		_followedOnce = true;
		
		if (dpiChanged) {
			_MeasureText();
			_AutoSizeNow();
		}
		
		if (!followedOnce && _os != null) {
			var sc = screen.of(_w, SODefault.Zero);
			if (sc != _os.Screen) {
				_w.EnsureInScreen(_os.Screen, workArea: !_w.IsTopmost);
			}
		}
	}
	bool _followedOnce;
	byte _ignorePosChanged;
	bool _preferSize;
	
	void _WmWindowPosChanging(ref Api.WINDOWPOS wp) {
		//uncomment if using properties MinimumSize and MaximumSize.
		
		if (!_created) return;
		////print.it(this, wp.flags);
		
		//if(!wp.flags.Has(SWPFlags.NOSIZE)) {
		//	SIZE min = _GetMinSize();
		//	if(wp.cx < min.width) wp.cx = min.width;
		//	if(wp.cy < min.height) wp.cy = min.height;
		//	SIZE max = _Scale(MaximumSize);
		//	if(max.width > 0) wp.cx = Math.Min(wp.cx, Math.Max(max.width, min.width));
		//	if(max.height > 0) wp.cy = Math.Min(wp.cy, Math.Max(max.height, min.height));
		//}
		//
		//SIZE _GetMinSize()
		//{
		//	int k = Border < TBBorder.ThreeD ? 1 : WndUtil.BorderWidth(_w);//?
		//	k *= 2;
		//	var ms = _Scale(MinimumSize);
		//	return (Math.Max(k, ms.Width), Math.Max(k, ms.Height));
		//}
		
		//don't allow to move the satellite away from the planet.
		//	Only when _inMoveSize. In other cases can create problems, eg when DPI changes.
		if (!wp.flags.Has(SWPFlags.NOMOVE) && _IsSatellite && _inMoveSize) {
			RECT r = _satPlanet._w.Rect, rs = _w.Rect;
			if (wp.cx == rs.Width && wp.cy == rs.Height) { //only when moving. When resizing, it could collapse the toolbar; will snap finally.
				if (wp.x > r.right) wp.x = r.right; else wp.x = Math.Max(wp.x, r.left - wp.cx);
				if (wp.y > r.bottom) wp.y = r.bottom; else wp.y = Math.Max(wp.y, r.top - wp.cy);
			}
		}
	}
	
	void _WmWindowPosChanged(in Api.WINDOWPOS wp) {
		if (!_created) return;
		if (!wp.flags.Has(SWPFlags.NOMOVE | SWPFlags.NOSIZE) && _ignorePosChanged < 2) {
			bool resized = !wp.flags.Has(SWPFlags.NOSIZE);
			if (_ignorePosChanged == 0) {
				if (_os != null) {
					if (_os.IsScreenInvalid()) return;
					_os.UpdateIfAutoScreen();
				}
				_UpdateOffsets(wp.x, wp.y, wp.cx, wp.cy); //tested: if SWP_NOMOVE or SWP_NOSIZE, wp contains current values
			} else {
				if (resized && !_inMoveSize) _sett.size = _Unscale(_w.ClientRect.Size);
			}
			if (resized) {
				/*SIZE z=*/
				_Measure(_w.ClientRect.Width); //rewrap buttons etc
				
				//if(AutoSize && _ignorePosChanged && z.height!=wp.cy && Anchor is TBAnchor.TopLR or TBAnchor.BottomLR && Layout==TBLayout.HorizontalWrap) _Resize(z);//rejected
			}
			_SatFollow();
		}
		if (wp.flags.Has(SWPFlags.HIDEWINDOW)) {
			_SatHide();
		}
	}
	
	void _UpdateOffsets(int x, int y, int cx, int cy) {
		var (r, _) = _GetCachedOwnerRect();
		//print.it(x, y, cx, cy, r);
		if (Anchor.HasLeft()) _offsets.Left = _Unscale(x - (Anchor.OppositeX() ? r.right : r.left), true);
		if (Anchor.HasRight()) _offsets.Right = _Unscale((Anchor.OppositeX() ? r.left : r.right) - x - cx, true);
		if (Anchor.HasTop()) _offsets.Top = _Unscale(y - (Anchor.OppositeY() ? r.bottom : r.top), true);
		if (Anchor.HasBottom()) _offsets.Bottom = _Unscale((Anchor.OppositeY() ? r.top : r.bottom) - y - cy, true);
		if (!_inMoveSize) {
			_sett.offsets = _offsets;
			_sett.size = _Unscale(_w.ClientRect.Size);
		}
	}
	
	void _SetInMoveSize(bool start) {
		_inMoveSize = start;
		if (!start) {
			_sett.offsets = _offsets;
			var z = _Unscale(_w.ClientRect.Size);
			if (z != _sett.size) {
				_sett.size = z;
				_sett.wrapWidth = z.Width - _BorderPadding(unscaled: true) * 2 + 2;
				if (AutoSize) _AutoSizeNow();
			}
		}
	}
	bool _inMoveSize;
	
	/// <summary>
	/// Gets the cached rectangle of the owner window, screen, control, etc.
	/// If is owned and anchor has flag <b>Screen</b>, the rectangle is of toolbar's screen.
	/// Also gets previous size.
	/// The values are cached by <b>UpdateRect</b> of <b>_OwnerWindow</b> etc.
	/// </summary>
	(RECT r, SIZE prevSize) _GetCachedOwnerRect() {
		if (_os != null) return (_os.cachedRect, _os.prevSize);
		if (_oc != null) return (_oc.cachedRect, _oc.prevSize);
		return (_ow.GetCachedRect(this), _ow.GetPrevSize(this));
	}
	
	unsafe nint _WmGetDpiScaledSize(nint wParam, nint lParam) {
		//a quick and not perfect workaround for: on DPI change sometimes incorrect wrap/autosize
		if (_os != null && AutoSize && Layout == TBLayout.HorizontalWrap && _inMoveSize) {
			ref var z = ref *(SIZE*)lParam;
			int dpi = (int)wParam;
			z.width = Math2.MulDiv(z.width, dpi, _dpi);
			z.height = Math2.MulDiv(z.height, dpi, _dpi);
			z.width += dpi / 8;
			return 1;
		}
		return 0;
	}
	
	unsafe void _WmDpiChanged(nint wParam, nint lParam) {
		if (_os != null) {
			//print.it(caller(), _w.Rect, *(RECT*)lParam);
			if (!_SetDpi()) return;
			_Images(true);
			_MeasureText();
			if (_inMoveSize) { //cannot autosize now, or something bad may happen, eg nested wm_dpichanged until stack overflow
				_w.MoveL(*(RECT*)lParam);
			} else {
				if (DpiScaling.offsets == true) _FollowRect(); //update offsets if script wants so
				_AutoSizeNow();
			}
		}
	}
	
	void _WmDisplayChange() {
		if (_os != null) {
			if (_os.IsScreenInvalid()) return;
			
			timer.after(200, _ => {
				if (_os == null || _closed) return;
				_os.UpdateRect(out bool changed);
				if (changed) _FollowRect();
			});
		} else if (osVersion.minWin8_1) {
			//If owner's screen DPI changed, update toolbar's DPI/size/offsets. Need it for pm-dpi-aware windows that don't change rect when DPI changed.
			
			//WM_DISPLAYCHANGE doc: "when the display resolution has changed". Also when changed DPI, although undocumented. Tested on Win8.1 too.
			//	wm_dpichanged isn't good for it. We need DPI of owner, not of toolbar.
			//	Also we receive wm_settingchanged(SPI_SETLOGICALDPIOVERRIDE), but can't trust it. SPI_SETLOGICALDPIOVERRIDE's documentation is "Do not use.". No on Win8.1.
			
			//Wait until owner's DPI updated. If owner is pm-dpi-aware, its DPI is updated at random times, maybe after several s.
			int i = 15, oldDpi = _dpi;
			timer.every(1000, t => {
				if (--i > 0 && _dpi == oldDpi && !_closed && !_hide.Has(TBHide.Owner)) {
					if (screen.of(OwnerWindow).Dpi == oldDpi) return;
					//print.it("DPI changed", _dpi, Dpi.OfWindow(OwnerWindow, true));
					_FollowRect();
				}
				//print.it("stop");
				t.Stop();
				//tested: if pm-dpi-aware window is minimized, its dpi changes when restored. Not if hidden.
			});
		}
	}
}

//TODO3: now toolbars are lost too often.
//	Eg after removing autohide.
