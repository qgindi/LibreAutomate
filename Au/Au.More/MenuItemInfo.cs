namespace Au.More
{
	/// <summary>
	/// Gets item id, text and other info of a classic menu.
	/// </summary>
	public class MenuItemInfo
	{
		IntPtr _hm;
		int _id;
		bool _isSystem;
		wnd _ow;

		private MenuItemInfo() { }

		/// <summary>
		/// Gets info of a menu item from point.
		/// </summary>
		/// <returns><c>null</c> if failed, eg the point is not in the menu or the window is hung.</returns>
		/// <param name="pScreen">Point in screen coordinates.</param>
		/// <param name="w">Popup menu window, class name <c>"#32768"</c>.</param>
		/// <param name="msTimeout">Timeout (ms) to use when the window is busy or hung.</param>
		public static MenuItemInfo FromXY(POINT pScreen, wnd w, int msTimeout = 5000) {
			if (!w.SendTimeout(msTimeout, out var hm, Api.MN_GETHMENU)) return null;
			int i = Api.MenuItemFromPoint(default, hm, pScreen); if (i == -1) return null;
			i = Api.GetMenuItemID(hm, i); if (i == -1 || i == 0) return null;
			return new MenuItemInfo { _hm = hm, _id = i };
		}

		/// <summary>
		/// Gets info of a menu item from mouse.
		/// </summary>
		/// <returns><c>null</c> if failed, eg the point is not in a menu or the window is hung.</returns>
		/// <param name="msTimeout">Timeout (ms) to use when the window is busy or hung.</param>
		public static MenuItemInfo FromXY(int msTimeout = 5000) {
			var p = mouse.xy;
			var w = wnd.fromXY(p, WXYFlags.Raw);
			if (!w.ClassNameIs("#32768")) return null;
			return FromXY(p, w, msTimeout);
		}

		/// <summary>
		/// Gets the popup menu handle.
		/// </summary>
		public IntPtr MenuHandle => _hm;

		/// <summary>
		/// Gets menu item id.
		/// </summary>
		public int ItemId => _id;

		/// <summary>
		/// Gets the owner window of the popup menu.
		/// </summary>
		public wnd OwnerWindow => _OwnerSystem().ow;

		/// <summary>
		/// <c>true</c> if it is a system menu, eg when right-clicked the title bar of a window.
		/// </summary>
		public bool IsSystem => _OwnerSystem().sys;

		(wnd ow, bool sys) _OwnerSystem() {
			if (_ow.Is0 && miscInfo.getGUIThreadInfo(out var g)) {
				_ow = g.hwndMenuOwner;
				_isSystem = g.flags.Has(GTIFlags.SYSTEMMENUMODE);
			}
			return (_ow, _isSystem);
		}

		/// <summary>
		/// Gets menu item text.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="removeHotkey">If contains <c>'\t'</c> character, get substring before it.</param>
		/// <param name="removeAmp">Call <see cref="StringUtil.RemoveUnderlineChar"/>.</param>
		public string GetText(bool removeHotkey, bool removeAmp) => GetText(_hm, _id, false, removeHotkey, removeAmp);

		/// <summary>
		/// Gets menu item text.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="menuHandle"></param>
		/// <param name="id"></param>
		/// <param name="byIndex">id is 0-based index. For example you can use it to get text of a submenu-item, because such items usually don't have id.</param>
		/// <param name="removeHotkey">If contains <c>'\t'</c> character, get substring before it.</param>
		/// <param name="removeAmp">Call <see cref="StringUtil.RemoveUnderlineChar"/>.</param>
		[SkipLocalsInit]
		public static unsafe string GetText(IntPtr menuHandle, int id, bool byIndex, bool removeHotkey, bool removeAmp) {
			var mi = new Api.MENUITEMINFO(Api.MIIM_STRING);
			if (!Api.GetMenuItemInfo(menuHandle, id, byIndex, ref mi)) return null; //get required buffer size
			if (mi.cch == 0) return "";
			using FastBuffer<char> b = new(mi.cch + 1);
			mi.cch = b.n;
			mi.dwTypeData = b.p;
			if (!Api.GetMenuItemInfo(menuHandle, id, byIndex, ref mi)) return null;
			var s = b.GetStringFindLength();
			if (removeHotkey) { int i = s.IndexOf('\t'); if (i >= 0) s = s[..i]; }
			if (removeAmp) s = StringUtil.RemoveUnderlineChar(s);
			return s;
		}
	}
}
