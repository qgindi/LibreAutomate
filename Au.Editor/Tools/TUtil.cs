using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace Au.Tools;

static class TUtil {
	#region text
	
	/// <summary>
	/// Appends ', ' and string argument to this StringBuilder.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="s">Argument value. If null, appends 'null'. If verbatim (like '@"text"'), appends 's'. Else appends '"escaped s"'; can make verbatim.</param>
	/// <param name="param">If not null, appends 'param: s'. By default appends only 's'. If "null", appends 'null, s'.</param>
	/// <param name="noComma">Don't append ', '. If false, does not append if b.Length is less than 2 or if b ends with one of: <c>,([{&lt;</c>.</param>
	public static StringBuilder AppendStringArg(this StringBuilder t, string s, string param = null, bool noComma = false) {
		_AppendArgPrefix(t, param, noComma);
		if (s == null) t.Append("null");
		else if (IsVerbatim(s, out _) || MakeVerbatim(ref s)) t.Append(s);
		else t.Append(s.Escape(quote: true));
		return t;
	}
	
	/// <summary>
	/// Appends ', ' and non-string argument to this StringBuilder.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="s">Argument value. Must not be empty.</param>
	/// <param name="param">If not null, appends 'param: s'. By default appends only 's'. If "null", appends 'null, s'.</param>
	/// <param name="noComma">Don't append ', '. Use for the first parameter. If false, does not append only if b.Length is less than 2.</param>
	public static StringBuilder AppendOtherArg(this StringBuilder t, string s, string param = null, bool noComma = false) {
		Debug.Assert(!s.NE());
		_AppendArgPrefix(t, param, noComma);
		t.Append(s);
		return t;
	}
	
	static void _AppendArgPrefix(StringBuilder t, string param, bool noComma) {
		if (!noComma && t.Length > 1 && t[^1] is not ('(' or '[' or '{' or '<' or ',')) t.Append(", ");
		if (param != null) t.Append(param).Append(param == "null" ? ", " : ": ");
	}
	
	/// <summary>
	/// Appends waitTime. If !orThrow, appends "-" if need.
	/// If !orThrow and !appendAlways and waitTime == "0", appends nothing and returns false.
	/// </summary>
	public static bool AppendWaitTime(this StringBuilder t, string waitTime, bool orThrow, bool appendAlways = false) {
		if (waitTime.NE()) waitTime = "8e88";
		if (!orThrow) {
			if (!appendAlways && waitTime == "0") return false;
			if (!waitTime.Starts('-') && waitTime != "0") t.Append('-');
		}
		t.Append(waitTime);
		return true;
	}
	
	/// <summary>
	/// If some 'use' are true, formats a flags argument like "Enum.Flag1 | Enum.Flag2" and returns true.
	/// </summary>
	public static bool FormatFlags<T>(out string s, params (bool use, T flag)[] af) where T : unmanaged, Enum {
		if (!af.Any(o => o.use)) { s = null; return false; }
		s = string.Join(" | ", af.Where(o => o.use).Select(o => typeof(T).Name + "." + o.flag));
		return true;
	}
	
	/// <summary>
	/// If some 'flags' checkbox controls are checked or indeterminate, formats a flags argument like "Enum.Flag1 | Enum.Flag2" and returns true. Ignores null controls.
	/// </summary>
	public static bool FormatFlags<T>(out string s, params (CheckBox use, T flag)[] af) where T : unmanaged, Enum {
		return FormatFlags(out s, af.Select(o => (o.use != null && o.use.IsChecked != false, o.flag)).ToArray());
	}
	
	/// <summary>
	/// Returns true if s is like '@"*"' or '$"*"' or '$@"*"' or '@$"*"'.
	/// s can be null.
	/// </summary>
	public static bool IsVerbatim(string s, out int prefixLength) {
		prefixLength = (s.Like(false, "@\"*\"", "$\"*\"", "$@\"*\"", "@$\"*\"") + 1) / 2;
		return prefixLength > 0;
	}
	
	/// <summary>
	/// If s contains \ and no newlines/controlchars: replaces all " with "", prepends @", appends " and returns true.
	/// </summary>
	/// <param name="s"></param>
	/// <returns></returns>
	public static bool MakeVerbatim(ref string s) {
		if (!s.Contains('\\') || s.RxIsMatch(@"[\x00-\x1F\x85\x{2028}\x{2029}]")) return false;
		s = "@\"" + s.Replace("\"", "\"\"") + "\"";
		return true;
	}
	
	/// <summary>
	/// If s has *? characters, prepends "**t ".
	/// s can be null.
	/// </summary>
	public static string EscapeWildex(string s) {
		if (wildex.hasWildcardChars(s)) s = "**t " + s;
		return s;
	}
	
	/// <summary>
	/// If s has *? characters, prepends "**t ".
	/// If <i>canMakeVerbatim</i>, finally calls <see cref="MakeVerbatim"/>.
	/// s can be null.
	/// </summary>
	public static string EscapeWindowName(string s, bool canMakeVerbatim) {
		if (s != null) {
			if (wildex.hasWildcardChars(s)) s = "**t " + s;
			if (canMakeVerbatim) MakeVerbatim(ref s);
		}
		return s;
	}
	//rejected: if name has * at the start or end, make regex like @"**r \*?\QUntitled - Notepad\E", so that would find with or without *.
	//	Ugly. Most users will not know what it is. Anyway in most cases need to replace the document part with *.
	///// <summary>
	///// If s has *? characters, prepends "**t ".
	///// But if s has single * character, converts to "**r regex" that ignores it. Because single * often is used to indicate unsaved state.
	///// If canMakeVerbatim, finally calls <see cref="MakeVerbatim"/>.
	///// s can be null.
	///// </summary>
	//public static string EscapeWindowName(string s, bool canMakeVerbatim) {
	//	if (s == null) return s;
	//	if (wildex.hasWildcardChars(s)) {
	//		int i = s.IndexOf('*');
	//		if (i >= 0 && s.IndexOf('*', i + 1) < 0) {
	//			s = "**r " + regexp.escapeQE(s[..i]) + @"\*?" + regexp.escapeQE(s[++i..]);
	//		} else s = "**t " + s;
	//	}
	//	if (canMakeVerbatim) MakeVerbatim(ref s);
	//	return s;
	//}
	
	/// <summary>
	/// Returns true if newRawValue does not match wildex tbValue, unless contains is like $"..." or $@"...".
	/// </summary>
	/// <param name="tbValue">A wildex string, usually from a TextBox control. Can be raw or verbatim. Can be null.</param>
	/// <param name="newRawValue">New raw string, not wildex. Can be null.</param>
	public static bool ShouldChangeTextBoxWildex(string tbValue, string newRawValue) {
		tbValue ??= "";
		if (newRawValue == null) newRawValue = "";
		if (IsVerbatim(tbValue, out _)) {
			if (tbValue[0] == '$') return false;
			tbValue = tbValue[2..^1].Replace("\"\"", "\"");
		}
		var x = new wildex(tbValue, noException: true);
		return !x.Match(newRawValue);
	}
	
	/// <summary>
	/// Replaces known non-constant window class names with wildcard. Eg "WindowsForms10.EDIT..." with "*.EDIT.*".
	/// </summary>
	/// <param name="s">Can be null.</param>
	/// <param name="escapeWildex">If didn't replace, call <see cref="EscapeWildex"/>.</param>
	public static string StripWndClassName(string s, bool escapeWildex) {
		if (!s.NE()) {
			int n = s.RxReplace(@"^WindowsForms\d+(\..+?\.).+", "*$1*", out s);
			if (n == 0) n = s.RxReplace(@"^(HwndWrapper\[.+?;|Afx:).+", "$1*", out s);
			if (escapeWildex && n == 0) s = EscapeWildex(s);
		}
		return s;
	}
	
	public static string ArgsFromWndFindCode(string wndFind) {
		if (wndFind.RxMatch(@"\bwnd.find\((?:-?\d+, )?(.+)\);", 1, out RXGroup g)) return g.Value;
		return null;
	}
	
	#endregion
	
	#region formatters
	
	public record WindowFindCodeFormatter {
		public string nameW, classW, programW, containsW, alsoW, waitW, orRunW, andRunW;
		public bool hiddenTooW, cloakedTooW, programNotStringW;
		public string idC, nameC, classC, alsoC, skipC, nameC_comments, classC_comments;
		public bool hiddenTooC;
		public bool NeedWindow = true, NeedControl, Throw, Activate, Test, Finder;
		public string CodeBefore, VarWindow = "w", VarControl = "c";
		
		public string Format() {
			if (!(NeedWindow || NeedControl)) return CodeBefore;
			var b = new StringBuilder(CodeBefore);
			if (CodeBefore != null && !CodeBefore.Ends('\n')) b.AppendLine();
			
			bool orThrow = false, orRun = false, andRun = false, activate = false, needWndFinder = false, needChildFinder = false;
			if (!(Test || Finder)) {
				orThrow = Throw;
				orRun = orRunW != null;
				andRun = andRunW != null;
				activate = Activate;
			}
			if (Finder && !Test) {
				needChildFinder = NeedControl;
				needWndFinder = !needChildFinder;
			}
			
			if (NeedWindow && !needChildFinder) {
				bool orThrowW = orThrow || NeedControl;
				
				if (needWndFinder) {
					b.Append("wndFinder f = new(");
				} else {
					b.Append(Test ? "wnd " : "var ").Append(VarWindow);
					if (Test) b.AppendLine(";").Append(VarWindow);
					
					if (orRun) {
						b.Append(" = wnd.findOrRun(");
					} else if (andRun) {
						b.Append(" = wnd.runAndFind(() => { ").Append(andRunW).Append(" }, ");
						b.AppendWaitTime(waitW, orThrowW);
					} else {
						b.Append(" = wnd.find(");
						if (waitW != null && !Test) b.AppendWaitTime(waitW, orThrowW);
						else if (orThrowW) b.Append('0');
					}
				}
				
				b.AppendStringArg(nameW);
				int m = 0;
				if (classW != null) m |= 1;
				if (programW != null) m |= 2;
				if (m != 0) b.AppendStringArg(classW);
				if (programW != null) {
					if (!(programNotStringW || programW.Starts("WOwner."))) b.AppendStringArg(programW);
					else if (!Test) b.AppendOtherArg(programW);
					else m &= ~2;
				}
				if (FormatFlags(out var s1, (hiddenTooW, WFlags.HiddenToo), (cloakedTooW, WFlags.CloakedToo))) b.AppendOtherArg(s1, m < 2 ? "flags" : null);
				if (alsoW != null) b.AppendOtherArg(alsoW, "also");
				if (containsW != null) b.AppendStringArg(containsW, "contains");
				
				if (orRun) {
					b.Append(", run: () => { ").Append(orRunW).Append(" }");
					if (!orThrowW) b.Append(", wait: -60");
				}
				if (orRun || andRun) {
					if (!activate) b.Append(", activate: !true");
					activate = false;
				}
				b.Append(')');
				if (activate && orThrowW) { b.Append(".Activate()"); activate = false; }
				b.Append(';');
			}
			
			if (NeedControl) {
				if (needChildFinder) {
					b.Append("wndChildFinder cf = new(");
				} else {
					if (NeedWindow) b.AppendLine();
					if (!Test) b.Append("var ").Append(VarControl).Append(" = ");
					b.Append(VarWindow).Append(".Child(");
					if (!Test) {
						if (waitW is not (null or "0") || orRun || andRun) b.Append(orThrow ? "1" : "-1");
						else if (orThrow) b.Append('0');
					}
				}
				if (nameC != null) b.AppendStringArg(nameC);
				if (classC != null) b.AppendStringArg(classC, nameC == null ? "cn" : null);
				if (FormatFlags(out var s1, (hiddenTooC, WCFlags.HiddenToo))) b.AppendOtherArg(s1, nameC == null || classC == null ? "flags" : null);
				if (idC != null) b.AppendOtherArg(idC, "id");
				if (alsoC != null) b.AppendOtherArg(alsoC, "also");
				if (skipC != null) b.AppendOtherArg(skipC, "skip");
				b.Append(");");
				
				if (!Test && nameC == null) { //if no control name, append // classC_comments nameC_comments
					string sn = nameC == null ? nameC_comments : null, sc = classC == null ? classC_comments : null;
					int m = 0; if (!sn.NE()) m |= 1; if (!sc.NE()) m |= 2;
					if (m != 0) {
						b.Append(" // ");
						if (0 != (m & 2)) b.Append(sc.Limit(70));
						if (0 != (m & 1)) {
							if (0 != (m & 2)) b.Append(' ');
							b.AppendStringArg(sn.Limit(100).RxReplace(@"^\*\*\*\w+ (.+)", "$1"), noComma: true);
						}
					}
				}
			}
			
			if (!orThrow && !Test && !Finder) {
				b.Append("\r\nif(").Append(NeedControl ? VarControl : VarWindow).Append(".Is0) { print.it(\"not found\"); }");
				if (activate) b.Append(" else { ").Append(VarWindow).Append(".Activate(); }");
			}
			
			return b.ToString();
		}
		
		/// <summary>
		/// Sets <b>skipC</b> if <i>c</i> is not the first found <i>w</i> child control with <b>nameC</b>/<b>classC</b>/<b>hiddenTooC</b>.
		/// </summary>
		public void SetSkipC(wnd w, wnd c) {
			skipC = GetControlSkip(w, c, nameC, classC, hiddenTooC);
		}
		
		/// <summary>
		/// Fills top-level window fields.
		/// Call once, because does not clear unused fields.
		/// </summary>
		public void RecordWindowFields(wnd w, int waitS, bool activate, string owner = null) {
			NeedWindow = true;
			Throw = true;
			Activate = activate;
			waitW = waitS.ToS();
			string name = w.Name;
			nameW = EscapeWindowName(name, true);
			classW = StripWndClassName(w.ClassName, true);
			if (programNotStringW = owner != null) programW = owner; else if (name.NE()) programW = w.ProgramName;
		}
		
		/// <summary>
		/// Fills control fields.
		/// Call once, because does not clear unused fields.
		/// </summary>
		/// <param name="w">Top-level window.</param>
		/// <param name="c">Control.</param>
		public void RecordControlFields(wnd w, wnd c) {
			NeedControl = true;
			
			string name = null, cn = StripWndClassName(c.ClassName, true);
			if (cn == null) return;
			
			bool _Name(string prefix, string value) {
				if (value.NE()) return false;
				name = prefix + EscapeWildex(value);
				return true;
			}
			
			if (GetUsefulControlId(c, w, out int id)) {
				idC = id.ToS();
				classC_comments = cn;
				_ = _Name(null, c.Name) || _Name(null, c.NameWinforms) || _Name(null, c.NameElm);
				nameC_comments = name;
			} else {
				_ = _Name(null, c.Name) || _Name("***wfName ", c.NameWinforms);
				nameC = name;
				classC = cn;
				
#if true
				SetSkipC(w, c);
				if (name == null && _Name("***elmName ", c.NameElm)) {
					if (skipC == null) nameC = name;
					else nameC_comments = name;
					//SetSkipC(w, c); //can be too slow
				}
#else
				bool setSkip = true;
				if (name == null && _Name("***elmName ", c.NameElm)) {
					nameC = name;
					setSkip = false; //can be too slow
				}
				if (setSkip) SetSkipC(w, c);
#endif
			}
		}
	}
	
	#endregion
	
	#region misc
	
	//Tool dialogs such as Delm normally run in new thread. If started from another such dialog - in its thread.
	//	Else main thread would hang when something is slow or hangs when working with UI elements or executing 'also' code.
	public static void ShowDialogInNonmainThread(Func<KDialogWindow> newDialog) {
		if (Environment.CurrentManagedThreadId != 1) {
			_Show(false);
		} else {
			run.thread(() => {
				_Show(true);
				
				//WPF bug: randomly app crashes when this thread is ending now.
				//	Can reproduce it when using the Windows "Text cursor indicator" feature, especially if here is `wait.doEvents();`.
				//	It happens when a HwndHost'ed control receives WM_GETOBJECT (that indicator feature sends many WM_GETOBJECT).
				//	When closing the dialog, WPF does not unhook/destroy the control. It reparents the control to some hidden window. Even then it receives WM_GETOBJECT. Later GC destroys it.
				//	The control is hooked. The hook proc calls Thread.CurrentThread, which throws NullReferenceException, probably because the runtime already cleared its thread data.
				//	Workaround: here add `wait.doEvents(100);`. Also GC.Collect to call DestroyWindowCore etc.
				try {
					var a = wnd.getwnd.threadWindows(process.thisThreadId).Where(o => o.Name == "SystemResourceNotifyWindow").SelectMany(o => o.ChildAll(flags: WCFlags.HiddenToo)).ToArray();
					for (int i = 0; i < 10; i++) {
						wait.doEvents(100);
						if (i > 0 && !a.Any(o => o.IsAlive)) break;
						GC.Collect();
						GC.WaitForPendingFinalizers();
					}
				}
				catch (Exception e1) { Debug_.Print(e1); }
			}).Name = "Au.Tool";
		}
		
		bool _Show(bool dialog) {
			try { //unhandled exception kills process if in nonmain thread
				var d = newDialog();
				if (dialog) return true == d.ShowDialog();
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
	
	/// <summary>
	/// Gets control id. Returns true if it can be used to identify the control in window wWindow.
	/// </summary>
	public static bool GetUsefulControlId(wnd wControl, wnd wWindow, out int id) {
		id = wControl.ControlId;
		if (id == 0 || id == -1 || id > 0xffff || id < -0xffff) return false;
		//info: some apps use negative ids, eg FileZilla. Usually >= -0xffff. Id -1 is often used for group buttons and separators.
		//if(id == (int)wControl) return false; //.NET forms, delphi. //possible coincidence //rejected, because window handles are > 0xffff
		Debug.Assert((int)wControl > 0xffff);
		
		//if(wWindow.Child(id: id) != wControl) return false; //problem with combobox child Edit that all have id 1001
		if (wWindow.ChildAll(id: id).Length != 1) return false; //note: searching only visible controls; else could find controls with same id in hidden pages of tabbed dialog.
		return true;
	}
	
	/// <summary>
	/// Returns <b>wnd.Child</b> parameter <i>skip</i> if <i>c</i> is not the first found <i>w</i> child control with <i>name</i> and <i>cn</i>.
	/// </summary>
	public static string GetControlSkip(wnd w, wnd c, string name, string cn, bool hiddenToo) {
		if (!c.Is0) {
			var a = w.ChildAll(name, cn, hiddenToo ? WCFlags.HiddenToo : 0);
			if (a.Length > 1 && a[0] != c) {
				int skip = Array.IndexOf(a, c);
				if (skip > 0) return skip.ToS();
			}
		}
		return null;
	}
	
	/// <summary>
	/// Calls EventManager.RegisterClassHandler for CheckBox.CheckedEvent, CheckBox.UncheckedEvent, TextBox.TextChangedEvent and optionally ComboBox.SelectionChangedEvent.
	/// Call from static ctor of KDialogWindow-based classes.
	/// The specified event handler will be called on events of any of these controls in all dialogs of T type.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="changed">Called on event.</param>
	/// <param name="comboToo">Register for ComboBox too.</param>
	public static void OnAnyCheckTextBoxValueChanged<T>(Action<T, object> changed, bool comboToo = false) where T : KDialogWindow {
		var h = new RoutedEventHandler((sender, e) => {
			//print.it(sender, e.Source, e.OriginalSource);
			var source = e.Source;
			if (source == e.OriginalSource) {
				switch (source) {
				case CheckBox:
				case TextBox:
				case ComboBox:
					changed(sender as T, source);
					break;
				}
			}
		});
		EventManager.RegisterClassHandler(typeof(T), ToggleButton.CheckedEvent, h);
		EventManager.RegisterClassHandler(typeof(T), ToggleButton.UncheckedEvent, h);
		EventManager.RegisterClassHandler(typeof(T), TextBoxBase.TextChangedEvent, h);
		if (comboToo) EventManager.RegisterClassHandler(typeof(T), Selector.SelectionChangedEvent, h);
	}
	
	/// <summary>
	/// From path gets name and various path formats (raw, @"string", unexpanded, shortcut) for inserting in code.
	/// If shortcut, also gets arguments.
	/// Supports ":: ITEMIDLIST".
	/// </summary>
	public class PathInfo {
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
			if (!MakeVerbatim(ref s)) s = s.Escape(quote: true);
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
						if (s != null) { path = s; args = m[2].Value; name = w.Name; };
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
		/// <param name="varIndex">If not 0, appends to s in 'var s = path'.</param>
		public string FormatCode(PathCode what, int varIndex = 0) {
			var (path, name, args) = GetStringsForCode();
			string nameComment = (what is PathCode.Var or PathCode.Run && (path.Starts("\":: ") || path.Like("folders.shell.*\"")) && !name.NE()) ? $"/* {name} */ " : null;
			if (what is PathCode.Var) {
				string si = varIndex > 0 ? varIndex.ToS() : null;
				return $"string s{si} = {nameComment}{path};"; //not var s, because may be FolderPath
			} else if (what is PathCode.Run or PathCode.RunMenu) {
				var b = new StringBuilder();
				if (what is PathCode.RunMenu) {
					var t = InsertCodeUtil.GetNearestLocalVariableOfType("Au.toolbar", "Au.popupMenu");
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
				var p = new MetaCommentsParser(App.Model.CurrentFile);
				if (!App.Settings.tools_pathUnexpand) p.r.AddRange(paths);
				else foreach (var v in paths) p.r.Add(folders.unexpandPath(v));
				p.Apply();
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
	
	public enum PathCode { None, Var, Run, RunMenu, EnumDir, MetaR }
	
	/// <summary>
	/// Takes screenshot of standard size to display in editor's margin.
	/// Color-quantizes, compresses and converts to a comment string to embed in code.
	/// Returns null if <b>App.Settings.edit_noImages</b>.
	/// </summary>
	/// <param name="p">Point in center of screenshot rectangle.</param>
	/// <param name="capt">If used, temporarily hides its on-screen rect etc.</param>
	public static string MakeScreenshot(POINT p, CapturingWithHotkey capt = null) {
		if (App.Settings.edit_noImages) return null;
		bool v1 = false, v2 = false;
		if (capt != null) {
			if (v1 = capt._osr.Visible) capt._osr.Hwnd.ShowL(false);
			if (v2 = capt._ost.Visible) capt._ost.Hwnd.ShowL(false);
		}
		const int sh = 30;
		var s = ColorQuantizer.MakeScreenshotComment(new(p.x - sh, p.y - sh / 2, sh * 2, sh));
		if (capt != null) {
			if (v1) capt._osr.Hwnd.ShowL(true);
			if (v2) capt._ost.Hwnd.ShowL(true);
		}
		return s;
	}
	
	#endregion
	
	#region OnScreenRect
	
	/// <summary>
	/// Creates standard <see cref="osdRect"/>.
	/// </summary>
	public static osdRect CreateOsdRect(int thickness = 4) => new() { Color = 0xFF0000, Thickness = thickness, TopmostWorkaround_ = true }; //red
	
	/// <summary>
	/// Briefly shows standard blinking on-screen rectangle.
	/// If disp, shows async in its thread.
	/// </summary>
	public static void ShowOsdRect(RECT r, bool error = false, bool limitToScreen = false, Dispatcher disp = null) {
		if (disp != null) {
			disp.InvokeAsync(() => ShowOsdRect(r, error, limitToScreen));
		} else {
			int thick = error ? 6 : 2;
			var osr = new osdRect { Color = error ? 0xFF0000 : 0x0000FF, Thickness = thick * 2, TopmostWorkaround_ = true };
			r.Inflate(thick, thick); //2 pixels inside, 2 outside
			if (limitToScreen) {
				var k = screen.of(r).Rect;
				r.Intersect(k);
			} else _LimitInsaneRect(ref r);
			osr.Rect = r;
			_OsdRectShow(osr, error);
		}
	}
	
	static void _OsdRectShow(osdRect osr, bool error = false) {
		t_hideCapturingRect = true;
		osr.Show();
		int i = 0;
		timer.every(250, t => {
			if (i++ < 4) {
				osr.Hwnd.ZorderTopRaw_();
				osr.Color = (i & 1) != 0 ? 0xFFFF00 : error ? 0xFF0000 : 0x0000FF;
			} else {
				t.Stop();
				osr.Dispose();
				t_hideCapturingRect = false;
			}
		});
	}
	
	[ThreadStatic] static bool t_hideCapturingRect;
	
	//eg VS Code code editor and output are {W=1000000 H=1000000}. Then the rect drawing code would fail.
	static void _LimitInsaneRect(ref RECT r) {
		if (r.Width > 2000 || r.Height > 1200) {
			var rs = screen.virtualScreen; rs.Inflate(100, 100);
			r.Intersect(rs);
		}
	}
	
	/// <summary>
	/// Briefly shows multiple on-screen rectangles.
	/// If disp, shows async in its thread.
	/// Can modify <i>a</i> elements.
	/// </summary>
	public static void ShowOsdRects(RECT[] a, Dispatcher disp = null) {
		if (disp != null) {
			disp.InvokeAsync(() => ShowOsdRects(a));
		} else {
			var osr = new osdRect { Color = 0x0000FF, Thickness = 2, TopmostWorkaround_ = true };
			for (int i = 0; i < a.Length; i++) _LimitInsaneRect(ref a[i]);
			osr.SetRects(a);
			_OsdRectShow(osr);
		}
	}
	
	#endregion
	
	#region capture
	
	/// <summary>
	/// Common code for tools that capture UI objects with F3.
	/// </summary>
	public class CapturingWithHotkey {
		readonly KCheckBox _captureCheckbox;
		readonly Func<POINT, (RECT? r, string s)> _dGetRect;
		readonly (string hotkey, Action a) _dCapture, _dInsert, _dSmaller;
		HwndSource _hs;
		timer _timer;
		internal osdRect _osr;
		internal osdText _ost; //SHOULDDO: draw rect and text in same OsdWindow
		bool _capturing;
		const string c_propName = "Au.Capture";
		readonly static int s_stopMessage = Api.RegisterWindowMessage(c_propName);
		const int c_hotkeyCapture = 1623031890, c_hotkeyInsert = 1623031891, c_hotkeySmaller = 1623031892;
		
		/// <param name="captureCheckbox">Checkbox that turns on/off capturing.</param>
		/// <param name="getRect">Called to get rectangle of object from mouse. Receives mouse position. Can return default to hide the rectangle.</param>
		public CapturingWithHotkey(KCheckBox captureCheckbox, Func<POINT, (RECT? r, string s)> getRect, (string hotkey, Action a) capture, (string hotkey, Action a) insert = default, (string hotkey, Action a) smaller = default) {
			_captureCheckbox = captureCheckbox;
			_dGetRect = getRect;
			_dCapture = capture;
			_dInsert = insert;
			_dSmaller = smaller;
		}
		
		/// <summary>
		/// Starts or stops capturing.
		/// Does nothing if already in that state.
		/// </summary>
		public bool Capturing {
			get => _capturing;
			set {
				if (value == _capturing) return;
				var wDialog = _captureCheckbox.Hwnd();
				if (value) {
					//let other dialogs stop capturing
					//could instead use a static collection, but this code allows to have such tools in multiple processes, although currently it not used
					wDialog.Prop.Set(c_propName, 1);
					wnd.find(null, "HwndWrapper[*", flags: WFlags.HiddenToo | WFlags.CloakedToo, also: o => {
						if (o != wDialog && o.Prop[c_propName] == 1) o.Send(s_stopMessage);
						return false;
					});
					
					bool _RegisterHotkey(int id, string hotkey) {
						string es = null;
						try {
							var (mod, key) = RegisteredHotkey.Normalize_(hotkey);
							if (Api.RegisterHotKey(wDialog, id, mod, key)) return true;
							es = "Failed to register.";
						}
						catch (Exception e1) { es = e1.Message; }
						dialog.showError("Hotkey " + hotkey, es + "\nClick the hotkey link to set another hotkey.", owner: wDialog);
						return false;
					}
					if (!_RegisterHotkey(c_hotkeyCapture, _dCapture.hotkey)) return;
					if (_dInsert.hotkey != null) _RegisterHotkey(c_hotkeyInsert, _dInsert.hotkey);
					if (_dSmaller.hotkey != null) _RegisterHotkey(c_hotkeySmaller, _dSmaller.hotkey);
					_capturing = true;
					
					if (_hs == null) {
						_hs = PresentationSource.FromDependencyObject(_captureCheckbox) as HwndSource;
						_hs.Disposed += (_, _) => {
							Capturing = false;
							_osr?.Dispose();
							_ost?.Dispose();
						};
					}
					_hs.AddHook(_WndProc);
					
					//set timer to show rectangles of UI element from mouse
					if (_timer == null) {
						_osr = CreateOsdRect(2);
						_timer = new timer(t => {
							int t1 = Environment.TickCount;
							
							POINT p = mouse.xy;
							wnd w = wnd.fromXY(p, WXYFlags.NeedWindow);
							RECT? r = default; string text = null;
							if (!(w.Is0 || w == wDialog || (w.ThreadId == wDialog.ThreadId && w.ZorderIsAbove(wDialog)))) {
								(r, text) = _dGetRect(p);
								
								//F3 does not work if this process has lower UAC IL than the foreground process.
								//	Normally editor is admin, but if portable etc...
								//	Shift+F3 too. But Ctrl+F3 works.
								//if (w!=wPrev && w.IsActive) {
								//	w = wPrev;
								//	if(w.UacAccessDenied)print.it("F3 ");
								//}
							}
							if (r.HasValue && !t_hideCapturingRect) {
								var rr = r.Value;
								rr.Inflate(1, 1); //1 pixel inside, 1 outside
								_LimitInsaneRect(ref rr);
								_osr.Rect = rr;
								_osr.Show();
								if (!text.NE()) {
									_ost ??= new() { Font = new(8), Shadow = false, ShowMode = OsdMode.ThisThread, SecondsTimeout = -1 };
									_ost.Text = text;
									var ro = _ost.Measure();
									var rs = screen.of(rr).Rect;
									int x = rr.left, y = rr.top + 8;
									if (rr.top - rs.top >= ro.Height) y = rr.top - ro.Height; else if (rr.Height < 200) y = rr.bottom; else x += 8;
									_ost.XY = new(x, y, false);
									_ost.Show();
								} else if (_ost != null) _ost.Visible = false;
							} else {
								_osr.Visible = false;
								if (_ost != null) _ost.Visible = false;
							}
							
							_timer.After(Math.Min(Environment.TickCount - t1 + 200, 2000)); //normally the timer priod is ~250 ms
						});
					}
					_timer.After(250);
				} else {
					_capturing = false;
					_hs.RemoveHook(_WndProc);
					Api.UnregisterHotKey(wDialog, c_hotkeyCapture);
					if (_dInsert.hotkey != null) Api.UnregisterHotKey(wDialog, c_hotkeyInsert);
					if (_dSmaller.hotkey != null) Api.UnregisterHotKey(wDialog, c_hotkeySmaller);
					wDialog.Prop.Remove(c_propName);
					_timer.Stop();
					_osr.Hide();
					_ost?.Hide();
				}
			}
		}
		
		nint _WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) {
			if (msg == s_stopMessage) {
				handled = true;
				_captureCheckbox.IsChecked = false;
			} else if (msg == Api.WM_HOTKEY && (wParam is c_hotkeyCapture or c_hotkeyInsert or c_hotkeySmaller)) {
				handled = true;
				if (wParam == c_hotkeyInsert) _dInsert.a();
				else if (wParam == c_hotkeySmaller) _dSmaller.a();
				else _dCapture.a();
			}
			return default;
		}
	}
	
	/// <summary>
	/// Adds link +hotkey that shows dialog "Hotkeys" and updates App.Settings.delm.hk_x.
	/// </summary>
	public static void RegisterLink_DialogHotkey(KSciInfoBox sci) {
		if (sci.AaTags.HasLinkTag("+hotkey")) return;
		sci.AaTags.AddLinkTag("+hotkey", _ => {
			var b = new wpfBuilder("Hotkey");
			b.R.xAddGroupSeparator("In wnd and elm tools");
			b.R.Add("Capture", out TextBox capture, App.Settings.delm.hk_capture).xValidateHotkey(errorIfEmpty: true).Focus();
			b.R.xAddGroupSeparator("In elm tool");
			b.R.Add("Insert code", out TextBox insert, App.Settings.delm.hk_insert).xValidateHotkey();
			b.R.Add("Capturing method", out TextBox smaller, App.Settings.delm.hk_smaller).xValidateHotkey();
			b.R.AddSeparator(false);
			b.R.xAddInfoBlockT("After changing hotkeys please restart the tool window.");
			if (!uacInfo.isAdmin) b.R.xAddInfoBlockT("Hotkeys don't work when the active window is admin,\nbecause this process isn't admin.");
			b.R.AddOkCancel();
			b.End();
			if (b.ShowDialog(Window.GetWindow(sci))) {
				App.Settings.delm.hk_capture = capture.Text;
				App.Settings.delm.hk_insert = insert.TextOrNull();
				App.Settings.delm.hk_smaller = smaller.TextOrNull();
			}
		});
	}
	
	#endregion
	
	#region test
	
	public record RunTestFindResult(object obj, long speed, InfoStrings info);
	
	/// <summary>
	/// Executes test code that finds an object in window.
	/// Returns the found object, speed and info strings to display. On error speed negative.
	/// </summary>
	/// <param name="owner">Owner dialog.</param>
	/// <param name="code">
	/// Must start with one or more lines that find window or control and set wnd variable named <i>wndVar</i>. Can be any code.
	/// The last line must be a 'find object' or 'find all objects' function call, like <c>uiimage.find(...);</c>. No 'var x = ', no 'not found' exception, no wait, no action.
	/// If 'find all objects', will display rectangles of all found objects and return the first found object.
	/// </param>
	/// <param name="wndVar">Name of wnd variable of the window or control in which to search.</param>
	/// <param name="w">Window or control in which to search.</param>
	/// <param name="getRect">Callback function that returns object's rectangle in screen. Called when object has been found.</param>
	/// <param name="activateWindow">Between finding window and object in it, activate the found window and wait 200 ms.</param>
	/// <param name="restoreOwner">If this func minimizes or deactivates the owner window, it sets a timer to restore it after eg ~2 seconds. If <i>restoreOwner</i> not null, the timer will delay restoring until restoreOwner[0] != 0, after restoreOwner[0] ms.</param>
	/// <param name="rectDisp">Use this dispatcher to show rectangles. For example if calling this in a non-UI thread and want to show in UI thread.</param>
	/// <example>
	/// <code><![CDATA[
	/// var rr = TUtil.RunTestFindObject(this, code, wndVar, _wnd, o => (o as elm).Rect);
	/// _info.InfoErrorOrInfo(rr.info);
	/// ]]></code>
	/// </example>
	public static RunTestFindResult RunTestFindObject(
		AnyWnd owner, string code, string wndVar, wnd w,
		Func<object, RECT> getRect = null,
		/*
		/// <param name="invoke">Callback that executes the code. Let it call/return MethodInfo.Invoke(null, null). For example if wants to execute in other thread. If null, the code is executed in this thread.</param>
		Func<MethodInfo, object> invoke = null
		*/
		bool activateWindow = false, int[] restoreOwner = null, Dispatcher rectDisp = null) {
		
		Debug.Assert(!code.NE());
		//print.it(code);
		//perf.first();
		
		var code0 = code;
		var b = new StringBuilder();
		b.AppendLine(@"static object[] __TestFunc__() {");
		if (activateWindow) b.Append("((wnd)").Append(w.Window.Handle).Append(").ActivateL(); 300.ms(); ");
		b.AppendLine("var _p_ = perf.local();");
		b.AppendLine("#line 1");
		var lines = code.Lines(true);
		int lastLine = lines.Length - 1;
		for (int i = 0; i < lastLine; i++) b.AppendLine(lines[i]);
		b.AppendLine("_p_.Next(); var _a_ =");
		b.AppendLine("#line " + (lastLine + 1));
		b.AppendLine(lines[lastLine]);
		b.AppendLine("_p_.Next();");
		b.AppendLine($"return new object[] {{ _p_.ToArray(), _a_, {wndVar} }};");
		b.AppendLine("\r\n}");
		code = b.ToString(); //print.it(code);
		
		(long[] speed, object obj, wnd w) r = default;
		wnd dlg = owner.Hwnd;
		bool dlgWasActive = dlg.IsActive, dlgMinimized = false;
		try {
			try {
				if (!Au.Compiler.Scripting.Compile(code, out var c, addUsings: true, addGlobalCs: true, wrapInClass: true, dll: true)) {
					Debug_.Print("---- CODE ----\r\n" + code + "--------------");
					//shows code too, because it may be different than in the code box
					return new(null, -1, new(true, "Errors:", $"{c.errors}\r\n\r\n<lc #C0C0C0><b>Code:<><>\r\n<code>{code0}</code>"));
				}
				//object ro = invoke?.Invoke(c.method) ?? c.method.Invoke(null, null);
				object ro = c.method.Invoke(null, null);
				var rr = (object[])ro; //use array because fails to cast tuple, probably because in that assembly it is new type
				r = ((long[])rr[0], rr[1], (wnd)rr[2]);
			}
			catch (Exception e) {
				if (e is TargetInvocationException tie) e = tie.InnerException;
				string s1, s2;
				if (e is NotFoundException) { //info: throws only when window not found
					s1 = "Window not found";
					s2 = "Tip: If part of window name changes, replace it with *";
				} else {
					s1 = e.GetType().Name;
					s2 = e.Message.RxReplace(@"^Exception of type '.+?' was thrown. ", "");
					if (e.StackTrace.RxMatch(@"(?m)^\s*( at .+?)\(.+\R\s+\Qat __script__.__TestFunc__()\E", 1, out string s3)) s1 += s3;
				}
				return new(null, -2, new(true, s1, s2));
			}
			
			//perf.nw();
			//print.it(r);
			
			static double _SpeedMcsToMs(long tn) => Math.Round(tn / 1000d, tn < 1000 ? 2 : (tn < 10000 ? 1 : 0));
			double t0 = _SpeedMcsToMs(r.speed[0]), t1 = _SpeedMcsToMs(r.speed[1]); //times of wnd.find and Object.Find
			string sSpeed;
			if (lastLine == 1 && lines[0] == "wnd w;") sSpeed = t1.ToS() + " ms"; //only wnd.find: "wnd w;\r\nw = wnd.find(...);"
			else sSpeed = t0.ToS() + " + " + t1.ToS() + " ms";
			
			if (r.obj is wnd w1 && w1.Is0) r.obj = null;
			
			//FindAll used instead of Find?
			var en = r.obj as IEnumerable<object>;
			if (en != null && !en.Any()) r.obj = null;
			
			if (r.obj != null) {
				RECT re;
				if (en != null) {
					var ar = en.Select(o => getRect(o)).ToArray();
					re = RECT.FromLTRB(ar.Min(o => o.left), ar.Min(o => o.top), ar.Max(o => o.right), ar.Max(o => o.bottom));
					r.obj = en.First();
					ShowOsdRects(ar, disp: rectDisp);
				} else {
					re = getRect(r.obj);
					ShowOsdRect(re, disp: rectDisp);
				}
				
				//if dlg covers the found object, temporarily minimize it (may be always-on-top) and activate object's window. Never mind owners.
				var wTL = r.w.Window;
				if (dlgMinimized = dlg.Rect.IntersectsWith(re) && !r.w.IsOfThisThread && !dlg.IsMinimized) {
					dlg.ShowMinimized(1);
					wTL.ActivateL();
					wait.doEvents(1000);
				}
			}
			
			if (r.w != w && !r.w.Is0) {
				ShowOsdRect(r.w.Rect, error: true, limitToScreen: true, disp: rectDisp);
				//FUTURE: show list of objects inside the wanted window, same as in the Dwnd 'contains' combo. Let user choose. Then update window code quickly.
				//string wndCode = null;
				//wndCode = "wnd w = wnd.find(\"Other\");";
				return new(null, -3, new(true, "Finds another " + (r.w.IsChild ? "control" : "window"), $"<i>Need:<>  {w}\r\n<i>Found:<>  {r.w}"));
			}
			
			return new(r.obj, r.speed[1], new(r.obj == null, r.obj != null ? "Found" : "Not found", null, ",  speed " + sSpeed));
		}
		finally {
			if (dlgWasActive || dlgMinimized) {
				int after = activateWindow && !dlgMinimized && r.w == w ? 1500 : 500;
				timer.after(after, t => {
					if (!dlg.IsAlive) return;
					if (restoreOwner == null) {
						if (dlgMinimized) dlg.ShowNotMinimized(1);
						if (dlgWasActive) dlg.ActivateL();
					} else if (restoreOwner[0] == 0) {
						t.After(100);
					} else {
						t.After(restoreOwner[0]);
						restoreOwner = null;
					}
				});
			}
		}
	}
	
	public record InfoStrings(bool isError, string header, string text, string headerSmall = null) {
		//public string text2 { get; set; }
	}
	
	/// <summary>
	/// Executes action code for a found UI element etc.
	/// </summary>
	/// <returns>Error strings to display, or null if no error.</returns>
	/// <param name="obj">The element. The function passes it to the test script.</param>
	/// <param name="code">Code like "Method(arguments)". The function prepends "obj." and appends ";".</param>
	public static InfoStrings RunTestAction(object obj, string code) {
		var code0 = code;
		code = $@"static void __TestFunc__({obj.GetType()} obj) {{
#line 1
obj.{code};
}}";
		//print.it(code);
		
		try {
			if (!Au.Compiler.Scripting.Compile(code, out var c, addUsings: true, addGlobalCs: true, wrapInClass: true, dll: true))
				return new(true, "Errors:", $"{c.errors}\r\n\r\n<b>Code:<>\r\n<code>obj.{code0};</code>");
			c.method.Invoke(null, new object[] { obj });
			return null;
		}
		catch (Exception e) {
			if (e is TargetInvocationException tie) e = tie.InnerException;
			return new(true, "Action failed", e.GetType().Name + ". " + e.Message.RxReplace(@"^Exception of type '.+?' was thrown. ", ""));
		}
	}
	
	public static async Task<InfoStrings> RunTestCodeAsync(string code, CancellationToken cancel = default) {
		var code0 = code;
		code = $@"static void __TestFunc__(CancellationToken cancel) {{
#line 1
{code}
}}";
		//print.it(code);
		
		try {
			if (!Au.Compiler.Scripting.Compile(code, out var c, addUsings: true, addGlobalCs: true, wrapInClass: true, dll: true)) {
				Debug_.Print("---- CODE ----\r\n" + code + "--------------");
				return new(true, "Errors:", $"{c.errors}\r\n\r\n<b>Code:<>\r\n<code>{code0};</code>");
			}
			await Task.Run(() => { c.method.Invoke(null, new object[] { cancel }); });
			return null;
		}
		catch (Exception e) {
			if (e is TargetInvocationException tie) e = tie.InnerException;
			return new(true, "Failed", e.GetType().Name + ". " + e.Message.RxReplace(@"^Exception of type '.+?' was thrown. ", ""));
		}
	}
	
	#endregion
	
	#region info
	
	public static void InfoError(this KSciInfoBox t, string header, string text, string headerSmall = null) {
		t.aaaText = $"<lc #F0E080><b>{header}<>{headerSmall}<>\r\n{text}";
		t.AaSuspendElems();
	}
	
	public static void InfoInfo(this KSciInfoBox t, string header, string text, string headerSmall = null) {
		t.aaaText = $"<lc #C0E0C0><b>{header}<>{headerSmall}<>\r\n{text}";
		t.AaSuspendElems();
	}
	
	public static void InfoErrorOrInfo(this KSciInfoBox t, InfoStrings info) {
		var text = info.text /*?? info.text2*/;
		if (info.isError) InfoError(t, info.header, text, info.headerSmall);
		else InfoInfo(t, info.header, text, info.headerSmall);
	}
	
	public static void Info(this KSciInfoBox t, FrameworkElement e, string name, string text) {
		text = CommonInfos.PrependName(name, text);
		t.AaAddElem(e, text);
	}
	
	public static void InfoC(this KSciInfoBox t, ContentControl k, string text) => Info(t, k, _ControlName(k), text);
	
	public static void InfoCT(this KSciInfoBox t, KCheckTextBox k, string text, bool isWildex = false, string wildexPart = null) {
		text = CommonInfos.PrependName(_ControlName(k.c), text);
		if (isWildex) text = CommonInfos.AppendWildexInfo(text, wildexPart);
		t.AaAddElem(k.c, text);
		t.AaAddElem(k.t, text);
	}
	
	public static void InfoCO(this KSciInfoBox t, KCheckComboBox k, string text) {
		text = CommonInfos.PrependName(_ControlName(k.c), text);
		t.AaAddElem(k.c, text);
		t.AaAddElem(k.t, text);
	}
	
	/// <summary>
	/// Returns k text without '_' character used for Alt+underline.
	/// </summary>
	static string _ControlName(ContentControl k) => StringUtil.RemoveUnderlineChar(k.Content as string, '_');
	
	/// <summary>
	/// Can be used by tool dialogs to display common info in <see cref="KSciInfoBox"/> control.
	/// </summary>
	public class CommonInfos {
		KSciInfoBox _control;
		RegexWindow _regexWindow;
		
		public CommonInfos(KSciInfoBox control) {
			_control = control;
			_control.AaTags.AddLinkTag("+regex", o => _Regex(o));
		}
		
		void _Regex(string _) {
			_regexWindow ??= new RegexWindow();
			if (_regexWindow.Hwnd.Is0) {
				_regexWindow.ShowByRect(_control.Hwnd().Window, Dock.Bottom);
			} else _regexWindow.Hwnd.ShowL(true);
		}
		
		/// <summary>
		/// Formats "name - text" string, where name is bold: <![CDATA["<b>" + name + "<> - " + text]]>.
		/// </summary>
		public static string PrependName(string name, string text) => "<b>" + name + "<> - " + text;
		
		public static string AppendWildexInfo(string s, string part = null) => s + "\r\n" + (part ?? "The text") +
@" is <help articles/Wildcard expression>wildcard expression<>. Can be <+regex>regex<>, like <mono>**rc regex<>.
Examples:
<mono>whole text
*end
start*
*middle*
time ??:??
**t literal text
**c case-sensitive text
**tc case-sensitive literal
**r regular expression
**rc case-sensitive regex
**n not this
**m this||or this||**r or this regex||**n and not this
**m(^^^) this^^^or this^^^or this
@""C# verbatim string""
<>";
		
		public const string c_alsoParameter = @"<i>also<> lambda.
Can be multiline.
Can use global usings and classes/functions from file ""global.cs"".";
	}
	
	/// <summary>
	/// Auto-creates and shows click-closed system-colored tooltip below element e.
	/// </summary>
	public static void InfoTooltip(ref KPopup p, UIElement e, string text, Dock side = Dock.Bottom) {
		if (p == null) {
			p = new(WS.POPUP | WS.BORDER, shadow: true) { Content = new Label(), ClickClose = KPopup.CC.Anywhere };
			p.Border.Background = SystemColors.InfoBrush;
		}
		(p.Content as Label).Content = text;
		p.ShowByRect(e, side);
	}
	
	///// <summary>
	///// Auto-creates and shows tooltip below element e. The tooltip has system colors, not WPF colors.
	///// </summary>
	//public static void InfoTooltip(ref ToolTip tt, UIElement e, string text) {
	//	tt ??= new ToolTip { StaysOpen = false, Placement = PlacementMode.Bottom, Background = SystemColors.InfoBrush, Foreground = SystemColors.InfoTextBrush };
	//	tt.PlacementTarget = e;
	//	tt.Content = text;
	//	tt.IsOpen = false;
	//	tt.IsOpen = true;
	//}
	
	public static void InfoRectCoord(wnd w, string rect) {
		if (!rect.RxMatch(@"^ *\( *(\d+) *, *(\d+) *, *(\d+) *, *(\d+) *\) *", out var m)) return;
		InfoRectCoord(w.ClientRect, new RECT(m[1].Value.ToInt(), m[2].Value.ToInt(), m[3].Value.ToInt(), m[4].Value.ToInt()));
	}
	
	public static void InfoRectCoord(RECT rOuter, RECT rInner) {
		var b = new StringBuilder();
		b.AppendFormat("<><help Au.Types.IFArea>IFArea<> <help Au.Types.Coord>Coord<> values equivalent to RECT ({0}, {1}, {2}, {3}) for window size ({4}, {5}): <fold>\r\n", rInner.left, rInner.top, rInner.Width, rInner.Height, rOuter.Width, rOuter.Height);
		b.AppendFormat("           {0,-8} {1,-8} {2,-8} {3}\r\n", "left", "top", "right", "bottom");
		for (int i = 0; i < 3; i++) {
			b.AppendFormat("{0,-11}", i switch { 0 => "Simple", 1 => "Reverse", _ => "Fraction" });
			for (int j = 0; j < 4; j++) {
				var (e, s1) = j switch { 0 => (rInner.left, "left"), 1 => (rInner.top, "top"), 2 => (rInner.right, "right"), _ => (rInner.bottom, "bottom") };
				int wh = ((j & 1) == 0 ? rOuter.Width : rOuter.Height);
				b.AppendFormat("{0,-9}", i switch { 0 => e.ToS(), 1 => "^" + (wh - e).ToS(), _ => wh == 0 ? "0" : ((double)e / wh).ToS("0.###") + "f" });
			}
			b.AppendLine();
		}
		b.Append("</fold>");
		print.it(b);
	}
	
	#endregion
}

///// <summary>
///// Starts tasks running always in the same STA thread (each <b>ToolTask</b> instance has own thread).
///// Caller can use async/await.
///// </summary>
//sealed class ToolTask : IDisposable
//{
//	readonly StaTaskScheduler_ _tasks = new(1);
//	int _n;

//	/// <summary>
//	/// Disposes the task scheduler (ends its thread).
//	/// </summary>
//	public void Dispose() {
//		_tasks.Dispose();
//	}

//	/// <summary>
//	/// Don't run the task if it could not start in this time (ms) because other tasks were running. Then the task result is default. Default 2000.
//	/// </summary>
//	public int StartTimeout { get; set; } = 2000;

//	/// <summary>
//	/// Starts task that does not return a value.
//	/// </summary>
//	/// <param name="param">Callback parameters.</param>
//	/// <param name="action">Callback.</param>
//	/// <param name="lowPriority">Don't run (and return null) if a task is running.</param>
//	public Task Run<TParam>(TParam param, Action<TParam> action, bool lowPriority = false) {
//		if (!lowPriority) Interlocked.Increment(ref _n); else if (0 != Interlocked.CompareExchange(ref _n, 1, 0)) return null;
//		long t = Environment.TickCount64;
//		return Task.Factory.StartNew(() => {
//			if (Environment.TickCount64 - t > StartTimeout) return;
//			try { action(param); }
//			catch (Exception e1) { Debug_.Print(e1); return; }
//			finally { Interlocked.Decrement(ref _n); }
//		}, default, 0, _tasks);
//	}

//	/// <summary>
//	/// Starts task that returns a value.
//	/// </summary>
//	/// <param name="param">Callback parameters.</param>
//	/// <param name="action">Callback.</param>
//	/// <param name="lowPriority">Don't run (and return null) if a task is running.</param>
//	public Task<TRet> Run<TRet, TParam>(TParam param, Func<TParam, TRet> f, bool lowPriority = false) {
//		if (!lowPriority) Interlocked.Increment(ref _n); else if (0 != Interlocked.CompareExchange(ref _n, 1, 0)) return null;
//		long t = Environment.TickCount64;
//		return Task.Factory.StartNew(() => {
//			if (Environment.TickCount64 - t > StartTimeout) return default;
//			try { return f(param); }
//			catch (Exception e1) { Debug_.Print(e1); return default; }
//			finally { Interlocked.Decrement(ref _n); }
//		}, default, 0, _tasks);
//	}
//}

///// <summary>
///// All tool dialogs that insert code in editor should inherit from this class.
///// Adds aaaResultCode property; child class sets, app can get, this class inserts text in editor on OK.
///// </summary>
//class CodeToolDialog : KDialogWindow
//{
//	protected wpfBuilder _builder;
//	public virtual string aaaResultCode { get; protected set; }

//	public CodeToolDialog() {
//		_builder.OkApply += o => { InsertCode.Statements(aaaResultCode); };
//	}
//}
