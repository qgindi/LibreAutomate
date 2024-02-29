
namespace Au.More;

/// <summary>
/// Gets some info of a folder window: path, selected items.
/// </summary>
public class ExplorerFolder {
	_Api.IWebBrowserApp _b;
	wnd _w;
	
	/// <summary>
	/// Creates <b>ExplorerFolder</b> for a folder window.
	/// </summary>
	/// <returns><c>null</c> if failed.</returns>
	/// <param name="w">A folder window.</param>
	public static ExplorerFolder Of(wnd w) {
		var b = _GetIWebBrowserApp(w);
		return b == null ? null : new() { _b = b, _w = w };
	}
	
	static _Api.IWebBrowserApp _GetIWebBrowserApp(wnd w) {
		var sw = new _Api.ShellWindows() as _Api.IShellWindows;
		for (int i = 0, n = sw.Count(); i < n; i++) {
			var b = sw.Item(i) as _Api.IWebBrowserApp;
			try { if ((wnd)b.HWND == w) return b; }
			catch (COMException) { /*print.warning(b.LocationURL);*/ } //about:blank
		}
		return null;
	}
	//Speed 22 ms. With dynamic less code, but first time 250 ms.
	
	/// <summary>
	/// Creates <b>ExplorerFolder</b> for all folder windows.
	/// </summary>
	/// <param name="onlyFilesystem">Skip folders that don't have a filesystem path, such as Control Panel and Recycle Bin.</param>
	public static ExplorerFolder[] All(bool onlyFilesystem = false) {
		var a = new List<ExplorerFolder>();
		var sw = new _Api.ShellWindows() as _Api.IShellWindows;
		for (int i = 0, n = sw.Count(); i < n; i++) {
			var b = sw.Item(i) as _Api.IWebBrowserApp;
			try {
				var s = b.LocationURL;
				if (s.NE()) {
					if (onlyFilesystem) continue;
				} else {
					if (!s.Starts("file:///")) continue; //skip IE etc
				}
				var w = (wnd)b.HWND;
				a.Add(new() { _b = b, _w = w });
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
	/// Gets folder path.
	/// For non-filesystem folder gets string like <c>":: ITEMIDLIST"</c>; see <see cref="Pidl"/>.
	/// </summary>
	/// <returns><c>null</c> if failed.</returns>
	public string GetFolderPath() {
		var s = _b.LocationURL;
		if (!s.NE()) {
			if (!s.Starts("file:///")) return null;
			s = s[8..].Replace('/', '\\');
			return System.Net.WebUtility.UrlDecode(s);
		} else { //non-filesystem?
			if (Api.QueryService(_b, _Api.SID_STopLevelBrowser, out _Api.IShellBrowser sb)) {
				if (0 == sb.QueryActiveShellView(out var v1) && v1 is _Api.IFolderView f) {
					var p = new Pidl(f.GetFolder(typeof(_Api.IPersistFolder2).GUID).GetCurFolder());
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
		var d = _b.Document as _Api.IShellFolderView;
		var items = d?.SelectedItems();
		if (items == null) return Array.Empty<string>();
		int n = items.Count;
		var a = new List<string>(n);
		for (int i = 0; i < n; i++) {
			try {
				var s = items.Item(i)?.Path;
				if (!s.NE()) a.Add(Pidl.ClsidToItemidlist_(s));
			}
			catch {  }
			//once: no selection, but items.Count returned 1, and items.Item(i) returned null. Could not reproduce after select-unselect.
		}
		return a.ToArray();
	}
	
	class _Api {
		[ComImport, Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), ClassInterface(ClassInterfaceType.None)]
		internal class ShellWindows { }
		
		[ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
		internal interface IShellWindows {
			int Count();
			[return: MarshalAs(UnmanagedType.IDispatch)] object Item(object index);
		}
		
		[ComImport, Guid("0002DF05-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
		internal interface IWebBrowserApp {
			object Document { get; }
			//string Path { get; } //always C:\WINDOWS\
			string LocationURL { get; }
			long HWND { get; }
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
			void _1();
			void _2();
			void _3();
			void _4();
			void _5();
			void _6();
			void _7();
			void _8();
			void _9();
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
		
		[ComImport, Guid("cde725b0-ccc9-4519-917e-325d72fab4ce"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IFolderView {
			void _1();
			void _2();
			IPersistFolder2 GetFolder(in Guid riid);
		}
	}
}
