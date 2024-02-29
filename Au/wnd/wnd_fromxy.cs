namespace Au
{
	public partial struct wnd
	{
		/// <summary>
		/// Gets visible top-level window or control from point.
		/// </summary>
		/// <param name="p">
		/// Coordinates.
		/// Tip: To specify coordinates relative to the right, bottom, work area or a non-primary screen, use <see cref="Coord.Normalize"/>, like in the example.
		/// </param>
		/// <param name="flags"></param>
		/// <remarks>
		/// Unlike API <msdn>WindowFromPhysicalPoint</msdn> etc, this function: does not skip disabled controls; always skips transparent control like group box if a smaller sibling is there. All this is not true with flag <b>Raw</b>.
		/// </remarks>
		/// <example>
		/// Find window at 100 200.
		/// <code><![CDATA[
		/// var w = wnd.FromXY((100, 200), WXYFlags.NeedWindow);
		/// print.it(w);
		/// ]]></code>
		/// 
		/// Find window or control at 50 from left and 100 from bottom of the work area.
		/// <code><![CDATA[
		/// var w = wnd.FromXY(Coord.Normalize(50, ^100, workArea: true));
		/// print.it(w);
		/// ]]></code>
		/// </example>
		public static wnd fromXY(POINT p, WXYFlags flags = 0) {
			bool needW = flags.Has(WXYFlags.NeedWindow);
			bool needC = flags.Has(WXYFlags.NeedControl);
			if (needW && needC) throw new ArgumentException("", "flags");

			if (flags.HasAny(WXYFlags.Raw | WXYFlags.NeedWindow)) {
				var w = Api.WindowFromPoint(p);
				if (needW) return w.Window;
				return !needC || w.IsChild ? w : default;
			} else {
				var w = _FromXY(p, out bool isChild);
				return !needC || isChild ? w : default;
			}

			//info:
			//WindowFromPoint is the most reliable. It skips really transparent top-level windows (TL). Unfortunately it skips disabled controls (but not TL).
			//ChildWindowFromPointEx with CWP_SKIPINVISIBLE|CWP_SKIPTRANSPARENT skips all with WS_EX_TRANSPARENT, although without WS_EX_LAYERED they aren't actually transparent.
			//RealChildWindowFromPoint does not skip transparent TL. It works like ChildWindowFromPointEx(CWP_SKIPINVISIBLE).
			//None of the above API prefers a really visible control that is under a transparent part of a sibling control. RealChildWindowFromPoint does it only for group buttons, but not for tab controls etc.
			//All API skip windows that have a hole etc in window region at that point.
			//If same thread, WindowFromPoint uses WM_NCHITTEST+HTTRANSPARENT. Other API tested only with TL of other processes and don't use.
			//AccessibleObjectFromPoint too dirty, unreliable and often very slow, because sends WM_GETOBJECT etc.
			//IUIAutomation.FromPoint very slow. Getting window handle from it is not easy, > 0.5 ms.
		}

		/// <inheritdoc cref="fromXY(POINT, WXYFlags)"/>
		public static wnd fromXY(int x, int y, WXYFlags flags = 0) => fromXY(new POINT(x, y), flags);

		//rejected: FromXY(Coord, Coord, ...). Coord makes no sense.

		/// <summary>
		/// Gets visible top-level window or control from mouse cursor position.
		/// More info: <see cref="fromXY"/>.
		/// </summary>
		public static wnd fromMouse(WXYFlags flags = 0) => fromXY(mouse.xy, flags);

		/// <summary>
		/// Gets descendant control from point.
		/// </summary>
		/// <returns>By default returns <c>default(wnd)</c> if the point is not in a child control; it depends on <i>flags</i>.</returns>
		/// <param name="x">X coordinate in client area or screen (if flag <b>ScreenXY</b>). Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate.</param>
		/// <param name="flags"></param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		public wnd ChildFromXY(Coord x, Coord y, WXYCFlags flags = 0) {
			ThrowIfInvalid();
			POINT p = flags.Has(WXYCFlags.ScreenXY) ? Coord.Normalize(x, y) : Coord.NormalizeInWindow(x, y, this);
			return _ChildFromXY(p, flags);
		}

		/// <summary>
		/// Gets descendant control from point.
		/// </summary>
		/// <returns>By default returns <c>default(wnd)</c> if the point is not in a child control; it depends on <i>flags</i>.</returns>
		/// <param name="p">Coordinates in client area or screen (if flag <b>ScreenXY</b>).</param>
		/// <param name="flags"></param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		public wnd ChildFromXY(POINT p, WXYCFlags flags = 0) {
			ThrowIfInvalid();
			return _ChildFromXY(p, flags);
		}

		//Gets real control or window from point in screen.
		//The input control can be eg from WindowFromPhysicalPoint, which skips disabled and transparent controls and does not get controls under transparent siblings such as groupbox button.
		//In such cases this struct replaces the input control (hwnd field) with the real control (descendant or sibling).
		//Fully DPI-aware on Win8.1+.
		struct _WindowFromPoint : IDisposable
		{
			POINT _p;
			IntPtr _hr;
			public wnd hwnd;
			public RECT r;

			public _WindowFromPoint(POINT p, wnd hwndStart) {
				r = default;
				_hr = default;
				hwnd = hwndStart;
				_p = p;

				//rejected. This library does not support DPI-scaled windows on Win7-8; too expensive.
				//if (!osVersion.minWin8_1 && !Api.PhysicalToLogicalPoint(hwnd, ref _p)) _p = p;
				////print.it(p, _p);

				//tested: on Win10 WM_NCHITTEST and GetWindowRgn use physical coords with DPI-scaled windows.
			}

			public void Dispose() { if (_hr != default) Api.DeleteObject(_hr); }

			public (bool inWindow, bool inClient) IsInWindow() {
				if (hwnd.GetWindowInfo_(out var k)) return (k.rcWindow.Contains(_p), k.rcClient.Contains(_p));
				return default;
			}

			bool _IsPointVisibleIn_1(wnd c) {
				return c.HasStyle(WS.VISIBLE) && Api.GetWindowRect(c, out r) && r.Contains(_p);
			}

			bool _IsPointVisibleIn_2(wnd c) {
				if (_hr == default) _hr = Api.CreateRectRgn(0, 0, 0, 0);
				if (Api.GetWindowRgn(c, _hr) > 1 && !Api.PtInRegion(_hr, _p.x - r.left, _p.y - r.top)) return false;
				if (c.HasExStyle(WSE.LAYERED) && c.IsCloaked) return false;
				return true;
				//if(osVersion.minWin8 && c.HasExStyle(WSE.LAYERED) && Api.GetLayeredWindowAttributes(c, ... //never mind. We can get alpha 0 (probably rare), but difficult or impossible to detect whether the point is transparent because of color key (probably not so rare).
				//tested: WindowFromPoint skips all: region, cloaked, transparent (by alpha or colorkey).
				//tested: RealChildWindowFromPoint skips region and cloaked, but not transparent.
				//tested: UI Automation skips region and cloaked, but not transparent.
				//tested: AccessibleObjectFromPoint skips only cloaked.
				//tested: Spy++ skips none.
				//tested: NtUserWindowFromPoint is the same as WindowFromPoint.
			}

			public bool FindSibling() {
				bool found = false;
				if (Api.GetWindowRect(hwnd, out RECT r1)) {
					for (wnd c2 = hwnd; !(c2 = Api.GetWindow(c2, Api.GW_HWNDNEXT)).Is0;) { //GetWindow often is the slowest part. EnumChildWindows slower.
						if (_IsPointVisibleIn_1(c2)) {
							if (r.left >= r1.left && r.top >= r1.top && r.right <= r1.right && r.bottom <= r1.bottom && (r.right - r.left < r1.right - r1.left || r.bottom - r.top < r1.bottom - r1.top)) {
								if (_IsPointVisibleIn_2(c2)) {
									if (hwnd.SendTimeout(1000, out var ht, Api.WM_NCHITTEST, 0, Math2.MakeLparam(_p), SMTFlags.ABORTIFHUNG | SMTFlags.BLOCK) && ht != Api.HTTRANSPARENT) break;
									hwnd = c2;
									r1 = r;
									found = true;
								}
							}
						}
					}
				}
				return found;
			}

			public bool FindDescendant(bool directChild = false) {
				bool found = false;
				gNextGeneration:
				for (wnd c2 = Api.GetWindow(hwnd, Api.GW_CHILD); !c2.Is0; c2 = Api.GetWindow(c2, Api.GW_HWNDNEXT)) {
					if (_IsPointVisibleIn_1(c2) && _IsPointVisibleIn_2(c2)) {
						hwnd = c2;
						found = true;
						if (directChild) break;
						goto gNextGeneration;
					}
				}
				return found;
			}
		}

		/// <summary>
		/// Gets window or control from point.
		/// Returns <c>default(wnd)</c> if failed (unlikely).
		/// </summary>
		/// <param name="p">Point in screen.</param>
		/// <param name="isChild">Receives <c>true</c> if control, false if top-level or failed.</param>
		static wnd _FromXY(POINT p, out bool isChild) {
			wnd w = Api.WindowFromPoint(p);
			if (w.Is0) { isChild = false; return default; }
			using var k = new _WindowFromPoint(p, w);
			bool findSibling = w.IsChild, findDescendant = true;
			for (; ; ) {
				//find a smaller sibling fully covered by c
				if (findSibling) findDescendant |= k.FindSibling();

				//find descendant, because: 1. WindowFromPoint does not find disabled controls. 2. The above code may change k.hwnd.
				if (!findDescendant) break;
				if (!k.FindDescendant()) break;
				findSibling = true; findDescendant = false;
			}
			isChild = findSibling;
			return k.hwnd;

			//note: don't use API [Real]ChildWindowFromPoint[Ex]. Bugs:
			//	1. Returns wrong control in DPI-scaled windows in non-primary screen with different DPI.
			//	2. Returns wrong control in RTL windows.
		}

		/// <summary>
		/// Gets descendant control from point. This can be top-level window or control.
		/// If there is no descendant, the return value depends on <i>flags</i>.
		/// </summary>
		/// <param name="p">Point in client area or screen (if flag <b>ScreenXY</b>).</param>
		/// <param name="flags"></param>
		wnd _ChildFromXY(POINT p, WXYCFlags flags = 0) {
			if (!flags.Has(WXYCFlags.ScreenXY)) Api.ClientToScreen(this, ref p);

			bool directChild = flags.Has(WXYCFlags.DirectChild),
				orThis = flags.Has(WXYCFlags.OrThis),
				inside = flags.Has(WXYCFlags.Inside);

			using var k = new _WindowFromPoint(p, this);
			if (inside) {
				var v = k.IsInWindow();
				if (!v.inWindow) return default;
				if (!v.inClient) return orThis ? this : default;
			}

			for (; ; ) {
				//find descendant
				if (!k.FindDescendant(directChild)) break;

				//find a smaller sibling fully covered by c
				if (!k.FindSibling()) break;
				if (directChild) break;
			}

			var R = k.hwnd;
			if (R == this) {
				if (!orThis || (!inside && !k.IsInWindow().inWindow)) R = default;
			}
			return R;
		}

		/// <summary>
		/// Gets sibling control in space: left, right, above or below.
		/// Returns <c>default(wnd)</c> if there is no sibling.
		/// </summary>
		/// <param name="direction"></param>
		/// <param name="distance">Distance from this control (from its edge) in the specified direction.</param>
		/// <param name="edgeOffset">
		/// Distance in perpendicular direction, along the specified edge. Default 5.
		/// If <i>direction</i> is <b>Left</b> or <b>Right</b>, 0 is the top edge, 1 is 1 pixel down, -1 is 1 pixel up, and so on.
		/// If <i>direction</i> is <b>Above</b> or <b>Below</b>, 0 is the left edge, 1 is 1 pixel to the right, -1 is 1 pixel to the left, and so on.
		/// </param>
		/// <param name="topChild">If at that point is a visible child or descendant of the sibling, get that child/descendant. Default <c>false</c>.</param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <remarks>
		/// This function is used mostly with controls, but supports top-level windows too.
		/// </remarks>
		wnd _SiblingXY(_SibXY direction, int distance, int edgeOffset = 5, bool topChild = false) {
			ThrowIfInvalid();
			wnd w = Get.DirectParent;
			if (!(w.Is0 ? GetRect(out RECT r) : GetRectIn(w, out r))) ThrowUseNative(); //note: most other wnd 'get' functions don't throw, but here it's better to throw.
			POINT p = default;
			switch (direction) {
			case _SibXY.Left: p = (r.left - distance, r.top + edgeOffset); break;
			case _SibXY.Right: p = (r.right + distance, r.top + edgeOffset); break;
			case _SibXY.Above: p = (r.left + edgeOffset, r.top - distance); break;
			case _SibXY.Below: p = (r.left + edgeOffset, r.bottom + distance); break;
			}
			//print.it(p); if(w.Is0) mouse.move(p); else mouse.move(w, p.x, p.y);
			wnd R = w.Is0
				? fromXY(p, topChild ? 0 : WXYFlags.NeedWindow)
				: w._ChildFromXY(p, topChild ? 0 : WXYCFlags.DirectChild);
			return R == this ? default : R; //cannot return self, but can return child if topChild, it's ok
		}

		enum _SibXY { Left, Right, Above, Below }

		wnd _SiblingXY(_SibXY direction) {
			ThrowIfInvalid();
			wnd desktop = default; if (!this.IsChild) getwnd.desktop(out desktop, out _);
			var r = Rect;
			wnd nearest = default; int nearestDist = int.MaxValue;
			for (var c = Api.GetWindow(this, Api.GW_HWNDFIRST); !c.Is0; c = Api.GetWindow(c, Api.GW_HWNDNEXT)) {
				if (c == this) continue;
				if (!c.IsVisible) continue;
				var k = c.Rect;
				int dist = 0;
				switch (direction) {
				case _SibXY.Left: if (k.left >= r.left || k.bottom < r.top || k.top >= r.bottom) continue; dist = r.left - k.right; break;
				case _SibXY.Above: if (k.top >= r.top || k.right < r.left || k.left >= r.right) continue; dist = r.top - k.bottom; break;
				case _SibXY.Right: if (k.right <= r.right || k.bottom < r.top || k.top >= r.bottom) continue; dist = k.left - r.right; break;
				case _SibXY.Below: if (k.bottom <= r.bottom || k.right < r.left || k.left >= r.right) continue; dist = k.top - r.bottom; break;
				}
				dist = Math.Max(dist, 0);
				if (dist < nearestDist) {
					if (c.IsCloaked || c.IsMinimized || c.IsMaximized) continue;
					if (!desktop.Is0) if (c == desktop || c.ThreadId == desktop.ThreadId) continue;
					nearestDist = dist;
					nearest = c;
				}
			}
			return nearest;
		}
	}
}

namespace Au.Types
{
	/// <summary>
	/// Flags for <see cref="wnd.fromXY"/> and <see cref="wnd.fromMouse"/>.
	/// </summary>
	[Flags]
	public enum WXYFlags
	{
		/// <summary>
		/// Need top-level window. If at that point is a control, gets its top-level parent.
		/// Don't use together with <b>NeedControl</b>.
		/// </summary>
		NeedWindow = 1,

		/// <summary>
		/// Need a control (child window). Returns <c>default(wnd)</c> if there is no control at that point.
		/// Don't use together with <b>NeedWindow</b>.
		/// Without flags <b>NeedWindow</b> and <b>NeedControl</b> the function gets control or top-level window.
		/// </summary>
		NeedControl = 2,

		/// <summary>
		/// Just call API <msdn>WindowFromPhysicalPoint</msdn>.
		/// Faster, but skips disabled controls and in some cases gets transparent control like group box although a smaller visible sibling is there.
		/// Not used with flag <b>NeedWindow</b>.
		/// </summary>
		Raw = 4,
	}

	/// <summary>
	/// Flags for <see cref="wnd.ChildFromXY"/>.
	/// </summary>
	[Flags]
	public enum WXYCFlags
	{
		/// <summary>
		/// If the point is in this window but not in a descendant control, return this. Default - return <c>default(wnd)</c>.
		/// </summary>
		OrThis = 1,

		/// <summary>
		/// The point is in screen coordinates. Default - client area.
		/// </summary>
		ScreenXY = 2,

		/// <summary>
		/// Must be direct child of this. Default - any descendant.
		/// </summary>
		DirectChild = 4,

		/// <summary>
		/// If the point is not in client area, don't look for descendants; if with flag <b>OrThis</b> and the point is in this (non-client area), return this, else return <c>default(wnd)</c>.
		/// </summary>
		Inside = 8,
	}
}
