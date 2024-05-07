using Au.Tools;
using System.Windows;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Au.Controls;

//TODO: everywhere generate code "var w1 ..." instead of "var w ...". The same with elm.
//	Or maybe only with elm, because often the script works with single window, or with one main window (let it be w) and several others.

/// <summary>
/// Scintilla-based control that shows colored C# code. Based on <see cref="KSciCodeBox"/> and adds methods to get code for wnd.find.
/// </summary>
class KSciCodeBoxWnd : KSciCodeBox {
	/// <summary>
	/// Returns code to find window w and optionally control con in it. Without newline at the end.
	/// If w/con is same as previous and code of this control is modified and valid, gets code from this code control, from the start to aaReadonlyStart.
	/// Else creates code "var w = wnd.find(...);". If w is invalid, creates code "wnd w = default;".
	/// The returned wndVar is final wnd variable name (of window or control).
	/// </summary>
	public (string code, string wndVar) AaGetWndFindCode(bool test, wnd w, wnd con = default, bool private1 = false) {
		if (test) { //remove 'wait' and 'activate' from wnd.find and wnd.Child. If no 'wait', insert 0 to throw notfoundexception.
			var k = AaGetWndFindCode(false, w, con, private1: true);
			var s = k.code;
			var p = _ParseWndFind(s, test: true);
			if (p?.wVar != null) {
				void _Replace(int end, int argsStart, int argsEnd, int nameStart, bool orRun = false, bool orRunReplace = false, int funcNameEnd = 0) {
					if (orRun && !orRunReplace) return;
					s = s.ReplaceAt(argsEnd..end, ");"); //remove '.Activate()' etc. If orRun, removes run etc arguments.
					s = s.ReplaceAt(argsStart..nameStart, nameStart < argsEnd ? "0, " : "0"); //remove 'wait, ' and add '0, ' (to throw NotFoundException)
					if (orRun) s = s.Remove(funcNameEnd - 5, 5); //findOrRun -> find
				}
				if (p.cVar != null) _Replace(p.cEnd, p.cArgsStart, p.cArgsEnd, p.cNameStart);
				_Replace(p.wEnd, p.wArgsStart, p.wArgsEnd, p.wNameStart, p.orRun, p.orRunReplace, p.wFuncNameEnd);
			}
			//print.it(s);
			k.code = s;
			return k;
		}

		string R = null, sCode = null, wndVar = "w", conVar = "c";

		if (w != _wnd) _userModified = false; else if (!_userModified) _userModified = 0 != Call(Sci.SCI_GETMODIFY);
		if (!w.Is0) {
			if (_userModified) {
				sCode = aaaRangeText(false, 0, _ReadonlyStartUtf8);
				var p = _ParseWndFind(sCode, test: false);
				if (p?.wVar != null) {
					bool isConCode = p.cVar != null;
					//print.it(isConCode);
					if (con == _con && !con.Is0 == isConCode) {
						//print.it(isConCode ? "same control" : "no control");
						if (!private1 && p.wName != null) {
							//if window name changed and does not match the name in code, change it in code
							var name = w.NameTL_;
							if (name != null && !new wildex(p.wName, noException: true).Match(name)) {
								var s = TUtil.EscapeWindowName(name, true);
								if (!(TUtil.IsVerbatim(s, out _) || TUtil.MakeVerbatim(ref s))) s = s.Escape(quote: true);
								sCode = sCode.ReplaceAt(p.wNameStart..p.wNameEnd, s);
							}
						}
						return (sCode, p.cVar ?? p.wVar);
					}
					wndVar = p.wVar;
					if (isConCode) sCode = sCode[..p.cStart];
					if (con.Is0) {
						//print.it("remove control");
						_con = default;
						return (sCode, wndVar);
					}
					if (isConCode) conVar = p.cVar;
					//print.it(isConCode ? "replace control" : "add control");
				} else sCode = null;
			}

			var f = new TUtil.WindowFindCodeFormatter {
				Throw = true,
				waitW = "1",
				VarWindow = wndVar,
				VarControl = conVar,
			};

			if (sCode != null) {
				f.CodeBefore = sCode;
				f.NeedWindow = false;
			} else if (w.ClassName is string cls) {
				f.nameW = TUtil.EscapeWindowName(w.NameTL_, true);
				f.classW = TUtil.StripWndClassName(cls, true);
				if (f.nameW.NE()) f.programW = w.ProgramName;
				f.hiddenTooW = !w.IsVisible;
				f.cloakedTooW = w.IsCloaked;
			} else {
				con = default;
				f.NeedWindow = false;
			}

			if (!con.Is0) {
				bool isId = TUtil.GetUsefulControlId(con, w, out int id);
				string cls = con.ClassName;
				if (isId || cls != null) {
					f.NeedControl = true;
					wndVar = conVar;
					if (isId) {
						f.idC = id.ToS();
						f.classC_comments = cls;
						f.nameC_comments = con.Name;
					} else {
						f.classC = TUtil.StripWndClassName(cls, true);
						string name = con.Name, prefix = null;
						if (name.NE()) {
							name = con.NameWinforms;
							if (!name.NE()) prefix = "***wfName ";
							else {
								var nameElm = con.NameElm;
								//var nameLabel = con.NameLabel;
								if (!nameElm.NE()/* || !nameLabel.NE()*/) {
									//if(nameAcc.NE() || nameLabel == nameAcc) {
									//	name = nameLabel; prefix = "***label ";
									//} else {
									name = nameElm; prefix = "***elmName ";
									//}
								}
							}
						}
						if (wildex.hasWildcardChars(name)) name = "**t " + name;

						f.nameC = prefix + name;
						f.hiddenTooC = !con.IsVisible;
						f.SetSkipC(w, con);
					}
				} else con = default;
			}

			R = f.Format();
		}

		if (R == null) {
			_wnd = default; _con = default;
			return ("wnd w = default;", "w");
		}
		_wnd = w; _con = con;
		return (R, wndVar);
	}
	wnd _wnd, _con;
	bool _userModified;

	record _WndFindParsing {
		//var w = wnd.find...
		public string wVar, wName;
		public int wEnd, wArgsStart, wArgsEnd, wNameStart, wNameEnd, wFuncNameEnd;
		public bool orRun, orRunReplace;
		//var c = w.Child...
		public string cVar;
		public int cStart, cEnd, cArgsStart, cArgsEnd, cNameStart;
	}

	//Parses var w = wnd.find(...)..., and var c = w.Child(...)... if exists.
	//Gets only strings and offsets needed for replacements.
	static _WndFindParsing _ParseWndFind(string code, bool test) {
		if (code.NE()) return null;
		var p = new _WndFindParsing();
		var cu = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Preview)).GetCompilationUnitRoot();
		foreach (var g1 in cu.Members) {
			if (g1 is not GlobalStatementSyntax g) break;
			if (g.Statement is LocalDeclarationStatementSyntax lds && lds.Declaration.Type.ToString() is "var" or "wnd") {
				var v = lds.Declaration.Variables[0];
				if (v.ArgumentList != null) continue; //array
				if (v.Initializer.Value is InvocationExpressionSyntax ies && ies.Expression is MemberAccessExpressionSyntax mae) {
					while (mae.Expression is InvocationExpressionSyntax ies2 && ies2.Expression is MemberAccessExpressionSyntax mae2) { ies = ies2; mae = mae2; } //eg when with '.Activate()'
					string s1 = mae.Expression.ToString(), s2 = mae.Name.ToString();
					bool isW = p.wVar == null;
					if (isW ? (s1 == "wnd" && s2 == "find" || (p.orRun = s2 == "findOrRun")) : (s1 == p.wVar && s2 == "Child")) {
						var al = ies.ArgumentList;
						int argsStart = al.OpenParenToken.Span.End, argsEnd = al.CloseParenToken.SpanStart, nameStart = argsStart;
						var a = al.Arguments;
						if (a.Count > 0) {
							int iName = 0;
							//wait. Never mind: also can be +, ~, (cast), double.Constant, etc.
							if (!p.orRun && a[0].Expression.Kind() is SyntaxKind.NumericLiteralExpression or SyntaxKind.UnaryMinusExpression) {
								nameStart = a.Count > 1 ? a[1].SpanStart : argsEnd;
								iName = a.Count > 1 ? 1 : -1;
							}
							if (isW && iName >= 0 && a[iName].Expression is LiteralExpressionSyntax les && les.IsKind(SyntaxKind.StringLiteralExpression)) {
								p.wName = les.Token.ValueText;
								p.wNameEnd = les.Span.End;
							}
						}
						var varName = v.Identifier.ToString();
						int end = lds.Span.End;
						if (isW) { //wnd.find
							p.wVar = varName;
							p.wEnd = end;
							p.wArgsStart = argsStart;
							p.wArgsEnd = argsEnd;
							p.wNameStart = nameStart;
							p.wFuncNameEnd = mae.Name.Span.End;
							if (p.orRun && test) {
								for (int i = 1, n = a.Count; i < n; i++) {
									if (a[i].NameColon?.Name.Identifier.Text == "run") {
										p.orRunReplace = true;
										p.wArgsEnd = a[i - 1].Span.End;
										break;
									}
								}
							}
						} else { //w.Child
							p.cVar = varName;
							p.cEnd = end;
							p.cArgsStart = argsStart;
							p.cArgsEnd = argsEnd;
							p.cNameStart = nameStart;
							p.cStart = lds.FullSpan.Start; while (code[p.cStart - 1] <= ' ') p.cStart--;
							break;
						}
					}
				}
			}
		}
		//print.it(p);
		return p;
	}

	/// <summary>
	/// Shows <see cref="Dwnd"/> and updates text.
	/// </summary>
	public (bool ok, wnd w, wnd con, bool useCon) AaShowWndTool(Window owner, wnd w, wnd con, bool checkControl) {
		var flags = DwndFlags.DontInsert; if (checkControl) flags |= DwndFlags.CheckControl;
		var d = new Dwnd(con.Is0 ? w : con, flags);
		d.ShowAndWait(owner, hideOwner: true);
		var code = d.AaResultCode; if (code == null) return default;
		_wnd = d.AaResultWindow;
		_con = d.AaResultUseControl ? d.AaResultControl : default;
		int i = _ReadonlyStartUtf8;
		var code2 = aaaRangeText(false, i, i + _readonlyLenUtf8);
		aaaIsReadonly = false;
		aaaSetText(code + code2);
		return (true, d.AaResultWindow, d.AaResultControl, d.AaResultUseControl);
	}
}
