using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows;
using System.Collections;

/// <summary>
/// Misc util functions.
/// </summary>
static class EdUtil {
	
	//public static object oTest;
}

/// <summary>
/// Can be used to return bool and error text. Has implicit conversions from/to bool.
/// </summary>
record struct BoolError(bool ok, string error) {
	public static implicit operator bool(BoolError x) => x.ok;
	public static implicit operator BoolError(bool ok) => new(ok, ok ? null : "Failed.");
}

#if DEBUG

static class EdDebug {
}

#endif

static class EdResources {
	public const string
		c_iconScript = "*Material.ScriptOutline #73BF00|#87E100",
		c_iconClass = "*Codicons.SymbolClass #4080FF|#84ACFF",
		c_iconFolder = "*Material.Folder #EABB00",
		c_iconFolderOpen = "*Material.FolderOpen #EABB00"
	;
	
	public static string FolderIcon(bool open) => open ? c_iconFolderOpen : c_iconFolder;
	
	public static string FolderArrow(bool open) => open ? @"resources/images/expanddown_16x.xaml" : @"resources/images/expandright_16x.xaml";
}

/// <summary>
/// Opens databases ref.db, doc.db (created by project DatabasesEtc) or winapi.db (created by script "SDK create database").
/// </summary>
static class EdDatabases {
	public static sqlite OpenRef() => _Open("ref.db");
	
	public static sqlite OpenDoc() => _Open("doc.db");
	
	public static sqlite OpenWinapi() => _Open("winapi.db");
	
	static sqlite _Open(string name) {
		var path = folders.ThisAppBS + name;
		//if (App.IsAuHomePC) { //no. Instead exit editor before running DatabasesEtc project. And it does not lock winapi.db.
		//	var pathNew = path + ".new";
		//	if (filesystem.exists(pathNew)) filesystem.move(pathNew, path, FIfExists.Delete);
		//}
		return new sqlite(path, SLFlags.SQLITE_OPEN_READONLY);
	}
	
	public static string WinapiFile => folders.ThisAppBS + "winapi.db";
}

/// <summary>
/// Temporarily disables window redrawing.
/// Ctor sends WM_SETREDRAW(0) if visible.
/// If was visible, Dispose sends WM_SETREDRAW(1) and calls RedrawWindow.
/// </summary>
struct WndSetRedraw : IDisposable {
	wnd _w;
	
	public WndSetRedraw(wnd w) {
		_w = w;
		if (_w.IsVisible) _w.Send(Api.WM_SETREDRAW, 0); else _w = default;
	}
	
	public unsafe void Dispose() {
		if (_w.Is0) return;
		_w.Send(Api.WM_SETREDRAW, 1);
		Api.RedrawWindow(_w, flags: Api.RDW_ERASE | Api.RDW_FRAME | Api.RDW_INVALIDATE | Api.RDW_ALLCHILDREN);
		_w = default;
	}
}

record struct StartEndText(int start, int end, string text) {
	public int Length => end - start;
	public Range Range => start..end;
	
	/// <inheritdoc cref="ReplaceAll(string, List{StartEndText}, ref StringBuilder)"/>
	public static string ReplaceAll(string s, List<StartEndText> a) {
		StringBuilder b = null;
		ReplaceAll(s, a, ref b);
		return b.ToString();
	}
	
	/// <summary>
	/// Replaces all text ranges specified in <i>a</i> with strings specified in <i>a</i>.
	/// </summary>
	/// <param name="a">Text ranges and replacement texts. Must be sorted by range. Ranges must not overlap.</param>
	/// <param name="b">Receives new text. If null, the function creates new, else at first calls <c>b.Clear()</c>.</param>
	/// <exception cref="ArgumentException">Ranges are overlapped or not sorted. Only #if DEBUG.</exception>
	public static void ReplaceAll(string s, List<StartEndText> a, ref StringBuilder b) {
		ThrowIfNotSorted(a);
		int cap = s.Length - a.Sum(o => o.Length) + a.Sum(o => o.text.Length);
		if (b == null) b = new(cap);
		else {
			b.Clear();
			b.EnsureCapacity(cap);
		}
		
		int i = 0;
		foreach (var v in a) {
			b.Append(s, i, v.start - i).Append(v.text);
			i = v.end;
		}
		b.Append(s, i, s.Length - i);
	}
	
	/// <exception cref="ArgumentException">Ranges are overlapped or not sorted. [Conditional("DEBUG")].</exception>
	[Conditional("DEBUG")]
	internal static void ThrowIfNotSorted(List<StartEndText> a) {
		for (int i = 1; i < a.Count; i++) if (a[i].start < a[i - 1].end) throw new ArgumentException("ranges must be sorted and not overlapped");
	}
}

static class EdComUtil {
	
	//To convert a COM type library we use TypeLibConverter class. However .NET Core+ does not have it.
	//Workaround: the code is in Au.Net4.exe. It uses .NET Framework 4.8. We call it through run.console.
	//We don't use tlbimp.exe:
	//	1. If some used interop assemblies are in GAC (eg MS Office PIA), does not create files for them. But we cannot use GAC in a Core+ app.
	//	2. Does not tell what files created.
	//	3. My PC somehow has MS Office PIA installed and there is no uninstaller. After deleting the GAC files tlbimp.exe created all files, but it took several minutes.
	//Tested: impossible to convert .NET Framework TypeLibConverter code. Part of it is in extern methods.
	//Tested: cannot use .NET Framework dll for it. Fails at run time because uses Core+ assemblies, and they don't have the class. Need exe.
	public static async Task<List<string>> ConvertTypeLibrary(object tlDef, Window owner) {
		string comDll = null;
		switch (tlDef) {
		case string path:
			comDll = path;
			break;
		case RegTypelib r:
			//can be several locales
			var aloc = new List<string>(); //registry keys like "0" or "409"
			var aloc2 = new List<string>(); //locale names for display in the list dialog
			using (var verKey = Registry.ClassesRoot.OpenSubKey($@"TypeLib\{r.guid}\{r.version}")) {
				foreach (var s1 in verKey.GetSubKeyNames()) {
					int lcid = s1.ToInt(0, out int iEnd, STIFlags.IsHexWithout0x);
					if (iEnd != s1.Length) continue; //"FLAGS" etc; must be hex number without 0x
					aloc.Add(s1);
					var s2 = "Neutral";
					if (lcid > 0) {
						try { s2 = new System.Globalization.CultureInfo(lcid).DisplayName; } catch { s2 = s1; }
					}
					aloc2.Add(s2);
				}
			}
			string locale;
			if (aloc.Count == 1) locale = aloc[0];
			else {
				int i = dialog.showList(aloc2, "COM type library locale", owner: owner);
				if (i == 0) return null;
				locale = aloc[i - 1];
			}
			comDll = r.GetPath(locale);
			if (comDll == null /*|| !filesystem.exists(comDll).File*/) { //can be "filepath/resource"
				print.it("Failed to get file path.");
				return null;
			}
			break;
		}
		
		print.it($"Converting COM type library to .NET assembly.");
		List<string> converted = new();
		int rr = -1;
		owner.IsEnabled = false;
		try {
			await Task.Run(() => {
				var dir = folders.Workspace + @".interop\";
				filesystem.createDirectory(dir);
				void _Callback(string s) {
					//skip some useless warnings, eg "can't convert some internal type"
					bool mute = s.RxIsMatch(@"Warning: .+ (?:could not convert the signature for the member '(?:\w*_|tag|[A-Z]+\.)|as a pointer and may require unsafe code to manipulate.|to import this property as a method instead.)");
					if (!mute) print.it(s);
					if (s.Starts("Converted: ")) {
						s.RxMatch(@"""(.+?)"".$", 1, out s);
						converted.Add(s);
					}
				}
				rr = run.console(_Callback, folders.ThisAppBS + "Au.Net4.exe", $"/typelib \"{dir}|{comDll}\"");
			});
		}
		catch (Exception ex) { print.it("Failed to convert type library", ex.ToStringWithoutStack()); }
		owner.IsEnabled = true;
		if (rr != 0) return null;
		print.it(@"<>Converted and saved in <link>%folders.Workspace%\.interop<>.");
		return converted;
	}
	
	public record class RegTypelib(string text, string guid, string version) {
		public override string ToString() => text;
		
		public string GetPath(string locale) {
			var k0 = $@"TypeLib\{guid}\{version}\{locale}\win";
			for (int i = 0; i < 2; i++) {
				var bits = osVersion.is32BitProcess == (i == 1) ? "32" : "64";
				using var hk = Registry.ClassesRoot.OpenSubKey(k0 + bits);
				if (hk?.GetValue("") is string path) return path.Trim('"');
			}
			return null;
		}
	}
	
}

/// <summary>
/// Gets relative path from full path when recursively enumerating a directory. Optionally with a prefix. Almost no garbage.
/// </summary>
class RelativePath {
	char[] _a;
	int _fullPathBaseLen;
	string _prefix;
	
	public RelativePath(string prefix) {
		_prefix = prefix ?? "";
		_a = new char[_prefix.Length + 300];
		prefix.CopyTo(_a);
	}
	
	public RStr GetRelativePath(string fullPath) {
		if (_fullPathBaseLen == 0) _fullPathBaseLen = fullPath.LastIndexOf('\\');
		int len1 = fullPath.Length - _fullPathBaseLen, len2 = len1 + _prefix.Length;
		if (_a.Length < len2) Array.Resize(ref _a, len2 + 300);
		fullPath.CopyTo(_fullPathBaseLen, _a, _prefix.Length, len1);
		return _a.AsSpan(0, len2);
	}
}

/// <summary>
/// Wildcard-compares paths with a list of strings.
/// </summary>
class WildcardList {
	readonly string[] _a;
	
	/// <summary>
	/// Creates a list of strings from a multiline string.
	/// </summary>
	/// <param name="list">Multiline string. Use <c>//</c> to comment out a line.</param>
	/// <param name="replaceSlash">Replace <c>'/'</c> with <c>'\\'</c> (to match file paths).</param>
	public WildcardList(string list, bool replaceSlash = true) {
		if (list.NE()) {
			_a = Array.Empty<string>();
		} else {
			List<string> a = new();
			foreach (var s in list.Lines(noEmpty: true)) {
				if (s.Starts("//")) continue;
				if (s.FindNot(@"*/\") < 0) continue; //wildcard like "*" or "\*" or "\" etc has no sense. Either matches always or never. Prevent accidentally excluding all files etc.
				a.Add(replaceSlash ? s.Replace('/', '\\') : s);
			}
			_a = a.ToArray();
		}
	}
	
	/// <summary>
	/// Wildcard-compares <i>s</i> with the list of wildcard strings.
	/// </summary>
	/// <returns>true if a <i>s</i> matches a wildcard string.</returns>
	public bool IsMatch(RStr s, bool ignoreCase) {
		foreach (var v in _a) if (s.Like(v, ignoreCase)) return true;
		return false;
	}
}

//class TempEnvVar : IDisposable {
//	string[] _vars;
//	public TempEnvVar(params (string name, string value)[] vars) {
//		_vars = vars.Select(o => o.name).ToArray();
//		foreach (var v in vars) Environment.SetEnvironmentVariable(v.name, v.value);
//	}

//	public void Dispose() {
//		foreach (var v in _vars) Environment.SetEnvironmentVariable(v, null);
//	}
//}
