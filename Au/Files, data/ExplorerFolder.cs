namespace Au.More;

/// <summary>
/// File Explorer folder window functions.
/// </summary>
public class ExplorerFolder {
	api.IWebBrowser2 _b;
	wnd _w, _cTab;
	
	/// <summary>
	/// Creates <b>ExplorerFolder</b> for a folder window.
	/// </summary>
	/// <returns><c>null</c> if failed.</returns>
	/// <param name="w">A folder window.</param>
	/// <param name="tab">Tab name (wildcard expression). If null (default), uses the current tab.</param>
	/// <exception cref="AuWndException"><i>w</i> invalid.</exception>
	public static ExplorerFolder Of(wnd w, string tab = null) {
		w.ThrowIfInvalid();
		wnd cTab = w.Child(tab, "ShellTabWindowClass");
		if (tab != null && cTab.Is0) return null;
		var b = _GetIWebBrowser(w, cTab);
		return b == null ? null : new() { _b = b, _w = w, _cTab = cTab };
	}
	
	static api.IWebBrowser2 _GetIWebBrowser(wnd w, wnd cTab) {
		foreach (var b in _EnumShellWindows()) {
			try {
				if ((wnd)b.HWND == w) {
					if (!cTab.Is0 && _GetIShellBrowser(b, out var sb)) {
						bool ok = 0 == sb.GetWindow(out var c) && c == cTab;
						Marshal.ReleaseComObject(sb);
						if (!ok) continue;
					}
					return b;
				}
				Marshal.ReleaseComObject(b);
			}
			catch (COMException) { /*print.warning(b.LocationURL);*/ } //about:blank
		}
		return null;
	}
	
	static IEnumerable<api.IWebBrowser2> _EnumShellWindows() {
		//var sw = new _Api.ShellWindows() as _Api.IShellWindows; //4 times slower than in C++. CoCreateInstance 3 times slower.
		if (0 != Api.CoCreateInstance(new("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), 0, 4, typeof(api.IShellWindows).GUID, out var o)) throw new AuException();
		var sw = o as api.IShellWindows;
		for (int i = 0, n = sw.Count(); i < n; i++) {
			yield return sw.Item(i) as api.IWebBrowser2;
		}
		Marshal.ReleaseComObject(sw);
	}
	
	static bool _GetIShellBrowser(api.IWebBrowser2 b, out api.IShellBrowser sb) => Api.QueryService(b, api.SID_STopLevelBrowser, out sb);
	
	/// <summary>
	/// Creates <b>ExplorerFolder</b> for all folder windows.
	/// </summary>
	/// <param name="onlyFilesystem">Skip folders that don't have a filesystem path, such as Control Panel and Recycle Bin.</param>
	/// <param name="tabsOf">Need only tabs of this window.</param>
	public static ExplorerFolder[] All(bool onlyFilesystem = false, wnd tabsOf = default) {
		var a = new List<ExplorerFolder>();
		foreach (var b in _EnumShellWindows()) {
			try {
				var s = b.LocationURL;
				if (s.NE()) {
					if (onlyFilesystem) continue;
				} else {
					if (!s.Starts("file:///")) continue; //skip IE etc
				}
				
				wnd w = (wnd)b.HWND, c = default;
				if (!tabsOf.Is0 && w != tabsOf) continue;
				if (_GetIShellBrowser(b, out var sb)) sb.GetWindow(out c);
				
				a.Add(new() { _b = b, _w = w, _cTab = c });
			}
			catch (COMException) { /*print.warning(b.LocationURL);*/ } //about:blank
		}
		return a.ToArray();
	}
	
	/// <summary>
	/// Calls <see cref="GetFolderPath"/>.
	/// </summary>
	public override string ToString() => GetFolderPath();
	
	/// <summary>
	/// Gets window handle.
	/// </summary>
	public wnd Hwnd => _w;
	
	/// <summary>
	/// Gets tab control handle.
	/// </summary>
	public wnd HwndTab => _cTab;
	
	/// <summary>
	/// Gets folder path.
	/// For non-filesystem folder gets string like <c>":: ITEMIDLIST"</c>; see <see cref="Pidl"/>.
	/// </summary>
	/// <returns><c>null</c> if failed.</returns>
	public string GetFolderPath() {
		var s = _b.LocationURL;
		if (!s.NE()) {
			if (!s.Starts("file:///")) return null;
			if (0 != Api.PathCreateFromUrlAlloc(s, out var r)) return null; //note: .NET urldecoding functions produce invalid string if there are urlencoded non-ASCII characters
			return r;
		} else { //non-filesystem?
			if (_GetIShellBrowser(_b, out var sb)) {
				if (0 == sb.QueryActiveShellView(out var v1) && v1 is api.IFolderView2 f) {
					var p = new Pidl(f.GetFolder(typeof(api.IPersistFolder2).GUID).GetCurFolder());
					return p.ToString();
				}
			}
			return null;
		}
	}
	
	/// <summary>
	/// Gets paths of selected items.
	/// </summary>
	/// <returns>Array containing 0 or more items.</returns>
	/// <remarks>
	/// For non-file-system items gets <c>":: ITEMIDLIST"</c>; see <see cref="Pidl"/>.
	/// </remarks>
	public string[] GetSelectedItems() {
		var d = _b.Document as api.IShellFolderView;
		var items = d?.SelectedItems();
		if (items == null) return Array.Empty<string>();
		int n = items.Count;
		var a = new List<string>(n);
		for (int i = 0; i < n; i++) {
			try {
				var s = items.Item(i)?.Path;
				if (!s.NE()) a.Add(Pidl.ClsidToItemidlist_(s));
			}
			catch { }
			//once: no selection, but items.Count returned 1, and items.Item(i) returned null. Could not reproduce after select-unselect.
		}
		return a.ToArray();
	}
	
	/// <summary>
	/// Opens a folder in this window/tab.
	/// </summary>
	/// <param name="folder">Folder path or <c>":: ITEMIDLIST"</c>. Or <c>":back"</c>, <c>":forward"</c>, <c>":up"</c>.</param>
	/// <exception cref="Exception">Failed.</exception>
	public void Open(string folder) {
		if (!_GetIShellBrowser(_b, out var sb)) throw new AuException();
		var flag = folder switch { ":back" => api.SBSP_NAVIGATEBACK, ":forward" => api.SBSP_NAVIGATEFORWARD, ":up" => api.SBSP_PARENT, _ => 0u };
		if (flag != 0) {
			sb.BrowseObject(0, flag | api.SBSP_SAMEBROWSER);
		} else {
			using var pidl = Pidl.FromString(folder, throwIfFailed: true);
			sb.BrowseObject(pidl.UnsafePtr, api.SBSP_SAMEBROWSER);
		}
		30.ms();
		while (_b.Busy != 0) 10.ms();
	}
	
	/// <summary>
	/// Adds new tab in a folder window.
	/// </summary>
	/// <param name="w">A folder window.</param>
	/// <param name="folder">If not null, calls <see cref="Open"/>.</param>
	/// <returns><b>ExplorerFolder</b> for the new tab.</returns>
	/// <exception cref="Exception">Failed.</exception>
	/// <remarks>
	/// To create new tab, activates the window and sends keys <c>Ctrl+T</c>.
	/// </remarks>
	public static ExplorerFolder NewTab(wnd w, string folder = null) {
		_ThrowIfNoMultipleTabs();
		w.Activate();
		var c1 = w.Child(cn: "ShellTabWindowClass");
		keys.send("Ctrl+T");
		30.ms();
		wait.until(5, () => w.Child(cn: "ShellTabWindowClass") != c1);
		
		var r = ExplorerFolder.Of(w);
		if (folder != null) r.Open(@"C:\Test");
		return r;
		
		//Alternative.
		//	Bad: undocumented, found only in https://stackoverflow.com/a/78502949/26641797
		//	Good: don't need to activate window.
		//	Same: async too. Need to wait.
		//c1.Send(api.WM_COMMAND, 0xA21B);
	}
	
	/// <summary>
	/// Makes this tab visible.
	/// </summary>
	/// <exception cref="Exception">Failed.</exception>
	public void SwitchToTab() {
		_ThrowIfNoMultipleTabs();
		var s = _cTab.Name;
		var c = _w.ChildFast("", "Microsoft.UI.Content.DesktopChildSiteBridge");
		Debug_.PrintIf(c.Is0);
		if (c.Is0) c = _w;
		var a = c.Elm["PAGETAB", s].FindAll();
		if (a.Length == 0) throw new AuException();
		if (a.Length == 1) {
			a[0].Invoke();
			Debug_.PrintIf(_w.ChildFast(null, "ShellTabWindowClass") != _cTab);
		} else {
			foreach (var e in a) {
				e.Invoke();
				if (_w.ChildFast(null, "ShellTabWindowClass") == _cTab) break;
			}
		}
	}
	
	static void _ThrowIfNoMultipleTabs() {
		if (!osVersion.minWin11_22H2) throw new InvalidOperationException("Multiple tabs not supported in this Windows version.");
	}
	
	//rejected: InvokeVerbOnSelection. Too limited. Verbs unknown. Many commands can be invoked with hotkeys.
	
	class api : NativeApi {
		[ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
		internal interface IShellWindows {
			int Count();
			[return: MarshalAs(UnmanagedType.IDispatch)] object Item(object index);
		}
		
		[ComImport, Guid("d30c1661-cdaf-11d0-8a3e-00c04fc9e26e"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
		internal interface IWebBrowser2 {
			object Document { get; }
			//string Path { get; } //always C:\WINDOWS\
			string LocationURL { get; }
			long HWND { get; }
			short Busy { get; }
			short Visible { get; }
		}
		
		[ComImport, Guid("29EC8E6C-46D3-411f-BAAA-611A6C9CAC66"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
		internal interface IShellFolderView {
			FolderItems SelectedItems();
		}
		
		[ComImport, Guid("744129E0-CBE5-11CE-8350-444553540000"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
		internal interface FolderItems {
			int Count { get; }
			FolderItem Item(object index);
		}
		
		[ComImport, Guid("FAC32C80-CBE4-11CE-8350-444553540000"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
		internal interface FolderItem {
			string Path { get; }
		}
		
		internal static readonly Guid SID_STopLevelBrowser = new(0x4C96BE40, 0x915C, 0x11CF, 0x99, 0xD3, 0x00, 0xAA, 0x00, 0x4A, 0xE8, 0x37);
		
		[ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IShellBrowser {
			[PreserveSig] int GetWindow(out wnd phwnd);
			void _2();
			void _3();
			void _4();
			void _5();
			void _6();
			void _7();
			void _8();
			void BrowseObject(nint pidl, uint wFlags);
			void _a();
			void _b();
			void _c();
			[PreserveSig] int QueryActiveShellView([MarshalAs(UnmanagedType.IUnknown)] out object ppshv);
		}
		
		[ComImport, Guid("1AC3D9F0-175C-11d1-95BE-00609797EA4F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IPersistFolder2 {
			void _1();
			void _2();
			IntPtr GetCurFolder();
		}
		
		[ComImport, Guid("1af3a467-214f-4298-908e-06b03e0b39f9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IFolderView2 {
			void _1();
			void _2();
			IPersistFolder2 GetFolder(in Guid riid);
		}
		
		internal const uint SBSP_SAMEBROWSER = 0x1;
		internal const uint SBSP_NAVIGATEBACK = 0x4000;
		internal const uint SBSP_NAVIGATEFORWARD = 0x8000;
		internal const uint SBSP_PARENT = 0x2000;
	}
}
