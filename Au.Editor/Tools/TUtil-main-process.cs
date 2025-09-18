using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

using UnsafeTools;

namespace LA;

static class TUtil2 {
	//Some tool dialogs normally run in new thread. If started from another such dialog - in its thread.
	//Currently not using this. Tools like Delm run in separate process instead.
	public static void ShowDialogInNonmainThread(Func<KDialogWindow> newDialog) {
		if (!App.IsMainThread) {
			_Show(false);
		} else {
			run.thread(() => { _Show(true); }).Name = "Au.Tool";
		}
		
		bool _Show(bool dialog) {
			try { //unhandled exception kills process if in nonmain thread
				var d = newDialog();
				if (dialog) {
					bool ok = true == d.ShowDialog();
					d.Dispatcher.InvokeShutdown(); //without this would be huge memory leaks, random crashes etc. From Dispatcher class docs: If you create a Dispatcher on a background thread, be sure to shut down the dispatcher before exiting the thread.
					return ok;
				}
				d.Show();
			}
			catch (Exception e1) { print.it(e1); }
			return false;
		}
	}
	
	public static void CloseDialogsInNonmainThreads() {
		//close tool windows running in other threads. Elso they would not save their rect etc.
		var aw = wnd.findAll(null, "HwndWrapper[*", WOwner.Process(process.thisProcessId), WFlags.CloakedToo | WFlags.HiddenToo,
			also: o => o.Prop["close me on exit"] == 1);
		foreach (var v in aw) v.SendTimeout(1000, out _, Api.WM_CLOSE);
	}
	
	public static bool UnexpandPathsMetaR(string[] a, AnyWnd errDlgOwner) {
		foreach (var v in a) {
			if (CompilerUtil.IsNetAssembly(v)) continue;
			dialog.showError("Not a .NET assembly.", v, owner: errDlgOwner);
			return false;
		}
		
		string appDir = folders.ThisAppBS, dllDir = App.Model.DllDirectoryBS;
		if (a[0].Starts(dllDir, true)) { //note: in portable LA it is in app dir
			for (int i = 0; i < a.Length; i++) a[i] = @"%dll%\" + a[i][dllDir.Length..];
		} else if (a[0].Starts(appDir, true) && !(App.IsPortable && a[0].Starts(appDir + @"data\", true))) {
			for (int i = 0; i < a.Length; i++) a[i] = a[i][appDir.Length..];
		} else if (App.Settings.tools_pathUnexpand) { //unexpand path
			for (int i = 0; i < a.Length; i++) a[i] = folders.unexpandPath(a[i]);
		}
		
		return true;
	}
}

/// <summary>
/// From path gets name and various path formats (raw, @"string", unexpanded, shortcut) for inserting in code.
/// If shortcut, also gets arguments.
/// Supports ":: ITEMIDLIST".
/// </summary>
class PathInfo {
	public readonly string fileRaw, lnkRaw, fileString, lnkString, fileUnexpanded, lnkUnexpanded;
	readonly string _name, _name2, _args;
	readonly bool _elevated, _argsComment;
	
	public PathInfo(string path, string name = null, string args = null, bool elevated = false, bool argsComment = false) {
		fileRaw = path;
		_name = name ?? _Name(path);
		_args = args;
		_elevated = elevated;
		_argsComment = argsComment;
		if (path.Ends(".lnk", true)) {
			try {
				var g = shortcutFile.open(path);
				string target = g.TargetAnyType;
				if (target.Starts("::")) {
					using var pidl = Pidl.FromString(target);
					_name2 = pidl.ToShellString(SIGDN.NORMALDISPLAY);
				} else {
					_args ??= g.Arguments.NullIfEmpty_();
					if (name == null)
						if (!target.Ends(".exe", true) || _name.Contains("Shortcut"))
							_name2 = _Name(target);
				}
				lnkRaw = path;
				fileRaw = target;
			}
			catch { }
		}
		
		_Format(fileRaw, out fileString, out fileUnexpanded);
		if (lnkRaw != null) _Format(lnkRaw, out lnkString, out lnkUnexpanded);
		if (_args != null) _args = _Str(_args);
		
		static void _Format(string raw, out string str, out string unexpanded) {
			str = _Str(raw);
			if (folders.unexpandPath(raw, out unexpanded, out var sn) && !sn.NE()) unexpanded = unexpanded + " + " + _Str(sn);
		}
		
		static string _Name(string path) {
			if (path.Starts("shell:") || path.Starts("::")) return "";
			var s = pathname.getNameNoExt(path);
			if (s.Length == 0) {
				s = pathname.getName(path); //eg some folders are like ".name"
				if (s.Length == 0 && path.Like("?:\\")) s = path[..2]; //eg "C:\"
			}
			return s;
		}
	}
	
	static string _Str(string s) {
		if (s == null) return "null";
		if (!TUtil.MakeVerbatim(ref s)) s = s.Escape(quote: true);
		return s;
	}
	
	/// <summary>
	/// Gets path of window's program for <see cref="run.it"/>. Supports appid, folder, mmc, itemidlist.
	/// Returns null if failed to get path or app id.
	/// </summary>
	public static PathInfo FromWindow(wnd w) {
		var path = WndUtil.GetWindowsStoreAppId(w, true, true);
		if (path == null) return null;
		string name = null, args = null;
		bool elevated = w.Uac.Elevation == UacElevation.Full;
		bool argsComment = false;
		//if folder window, try to get folder path
		if (path.Starts("shell:")) {
			name = w.Name;
		} else if (path.Ends(@"\explorer.exe", true) && w.ClassNameIs("CabinetWClass")) {
			var s1 = ExplorerFolder.Of(w)?.GetFolderPath();
			if (!s1.NE()) path = s1;
		} else {
			var s = process.getCommandLine(w.ProcessId, removeProgram: true);
			if (!s.NE()) {
				if (path.Ends(@"\javaw.exe", true)) {
					args = s;
				} else if (path.Ends(@"\mmc.exe", true)
					&& path[..^8].Eqi(folders.System)
					&& s.RxMatch($@"^("".+?\.msc""|\S+\.msc)(?: (.+))?$", out RXMatch m)) {
					s = m[1].Value.Trim('"');
					if (!pathname.isFullPath(s)) s = filesystem.searchPath(s);
					else if (!filesystem.exists(s)) s = null;
					if (s != null) { path = s; args = m[2].Value; name = w.Name; }
					;
				} else {
					args = s;
					argsComment = args != null;
				}
				
			}
		}
		var r = new PathInfo(path, name, args, elevated, argsComment);
		return r;
	}
	
	/// <summary>
	/// Gets path/name/args code for inserting in editor.
	/// path is escaped/enclosed and may be unexpanded (depends on settings), like <c>@"x:\a\b.c"</c> or <c>folders.Example + @"a\b.c"</c>.
	/// If args not null, it is escaped/enclosed is like ", args" or "/*, args*/".
	/// </summary>
	public (string path, string name, string args) GetStringsForCode() {
		bool lnk = App.Settings.tools_pathLnk && lnkString != null;
		var path = lnk
			? (App.Settings.tools_pathUnexpand && lnkUnexpanded != null ? lnkUnexpanded : lnkString)
			: (App.Settings.tools_pathUnexpand && fileUnexpanded != null ? fileUnexpanded : fileString);
		var name = lnk ? _name : (_name2 ?? _name);
		var args = lnk ? null : _args;
		if (args != null) args = _argsComment ? "/*, " + args + "*/" : ", " + args;
		return (path, name, args);
	}
	
	/// <summary>
	/// Calls <see cref="GetStringsForCode"/> and returns code like <c>run.it(path);</c>.
	/// </summary>
	/// <param name="what">Var, Run or RunMenu.</param>
	/// <param name="varIndex">If not 0, appends to s in `var s = path`.</param>
	public string FormatCode(PathCode what, int varIndex = 0) {
		var (path, name, args) = GetStringsForCode();
		string nameComment = (what is PathCode.Var or PathCode.Run && (path.Starts("\":: ") || path.Like("folders.shell.*\"")) && !name.NE()) ? $"/* {name} */ " : null;
		if (what is PathCode.Var) {
			string si = varIndex > 0 ? varIndex.ToS() : null;
			return $"string s{si} = {nameComment}{path};"; //not var s, because may be FolderPath
		} else if (what is PathCode.Run or PathCode.RunMenu) {
			var b = new StringBuilder();
			if (what is PathCode.RunMenu) {
				var t = CiUtil.GetNearestLocalVariableOfType("Au.toolbar", "Au.popupMenu");
				b.Append($"{t?.Name ?? "t"}[{_Str(name)}] = o => ");
			}
			b.Append("run.it(").Append(nameComment).Append(path).Append(args);
			if (_elevated) b.Append(", flags: RFlags.Admin");
			b.Append(");");
			return b.ToString();
		}
		return null;
	}
	
	/// <summary>
	/// Shows "insert file path code" menu and returns its result.
	/// Called for drag-dropped files or shell items.
	/// </summary>
	public static PathCode InsertCodeMenu(string[] paths, AnyWnd owner) {
		var m = new popupMenu { CheckDontClose = true };
		
		void _Add(PathCode what, string text) { m.Add((int)what, text); }
		
		_Add(PathCode.Var, "string s = path;");
		_Add(PathCode.Run, "run.it(path);");
		_Add(PathCode.RunMenu, "t[name] = o => run.it(path);");
		if (paths.All(o => filesystem.exists(o).Directory)) {
			_Add(PathCode.EnumDir, "Get files in folder...");
		} else if (paths.All(o => filesystem.exists(o).File && o.Ends(".dll", true))) {
			_Add(PathCode.MetaR, "/*/ r path; /*/");
		}
		m.Separator();
		m.AddCheck("Unexpand path (option)", App.Settings.tools_pathUnexpand, o => App.Settings.tools_pathUnexpand ^= true);
		if (paths.Any(o => o.Ends(".lnk", true)))
			m.AddCheck("Shortcut path (option)", App.Settings.tools_pathLnk, o => App.Settings.tools_pathLnk ^= true);
		
		var R = (PathCode)m.Show(owner: owner);
		
		if (R is PathCode.EnumDir) {
			DEnumDir.Dialog(paths[0]);
			return 0;
		}
		
		if (R is PathCode.MetaR) {
			if (TUtil2.UnexpandPathsMetaR(paths, owner)) {
				var p = new MetaCommentsParser(App.Model.CurrentFile);
				p.r.AddRange(paths);
				p.Apply();
			}
			return 0;
		}
		
		return R;
		
		//rejected
		//static bool _IsConsole(string path) {
		//	if (!path.Ends(".exe", true)) return path.Ends(".bat", true);
		//	return 0x4550 == Api.SHGetFileInfo(path, out _, Api.SHGFI_EXETYPE);
		//}
	}
	
	/// <summary>
	/// Adds "insert file path code" items to the quick capture menu.
	/// </summary>
	/// <param name="m">Submenu "Program".</param>
	/// <param name="click"></param>
	public static void QuickCaptureMenu(popupMenu m, Action<PathCode> click) {
		m.CheckDontClose = true;
		
		void _Add(PathCode what, string text) { m[text] = o => click(what); }
		
		_Add(PathCode.Var, "string s = path;");
		_Add(PathCode.Run, "run.it(path);");
		_Add(PathCode.RunMenu, "t[name] = o => run.it(path);");
		m.Separator();
		m.AddCheck("Unexpand path (option)", App.Settings.tools_pathUnexpand, o => App.Settings.tools_pathUnexpand ^= true);
	}
}

enum PathCode { None, Var, Run, RunMenu, EnumDir, MetaR }
