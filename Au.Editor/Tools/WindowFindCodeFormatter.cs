namespace ToolLand;

static partial class TUtil {
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
}
