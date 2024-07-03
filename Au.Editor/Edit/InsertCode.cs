extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Rename;
using acc = Microsoft.CodeAnalysis.Accessibility;

using Au.Controls;
using System.Windows;
using System.Windows.Controls;

/// <summary>Flags for <see cref="InsertCode.Statements"/>.</summary>
[Flags]
enum ICSFlags {
	/// <summary>If text contains '%', remove it and finally move caret there.</summary>
	GoToPercent = 1,
	
	/// <summary>Activate editor window.</summary>
	ActivateEditor = 4,
	
	/// <summary>Don't focus the editor control. Without this flag focuses it if window is active or activated.</summary>
	NoFocus = 8,
	
	GoToStart = 16,
	
	SelectNewCode = 32,
	
	MakeVarName1 = 64,
}

/// <summary>
/// Inserts various code in code editor. With correct indent etc.
/// Some functions can insert in other controls too.
/// </summary>
static class InsertCode {
	/// <summary>
	/// Inserts one or more statements at current line. With correct position, indent, etc.
	/// If editor is null or readonly, prints in output.
	/// Async if called from non-main thread.
	/// </summary>
	/// <param name="s">Text. The function ignores "\r\n" at the end. Does nothing if null.</param>
	/// <param name="separate">Prepend/append empty line to separate from surrounding code if need. If null, does it if <i>s</i> contains '\n'.</param>
	/// <param name="renameVars">Variable names to rename in s.</param>
	public static void Statements(string s, ICSFlags flags = 0, bool? separate = null, (string oldName, string newName)[] renameVars = null) {
		if (s == null) return;
		bool sep = separate ?? s.Contains('\n');
		
		if (Environment.CurrentManagedThreadId == 1) _Statements(s, flags, sep, renameVars);
		else App.Dispatcher.InvokeAsync(() => _Statements(s, flags, sep, renameVars));
	}
	
	static void _Statements(string s, ICSFlags flags, bool separate, (string oldName, string newName)[] renameVars) {
		if (!App.Hmain.IsVisible) App.ShowWindow();
		if (!CodeInfo.GetContextAndDocument(out var k, metaToo: true)) {
			print.it(s);
			return;
		}
		var root = k.syntaxRoot;
		var code = k.code;
		var pos = k.pos;
		var token = root.FindToken(pos);
		var node = token.Parent;
		
		//get the best valid insertion place
		
		bool havePos = false;
		var last = root.AttributeLists.LastOrDefault() as SyntaxNode
			?? root.Usings.LastOrDefault() as SyntaxNode
			?? root.Externs.LastOrDefault() as SyntaxNode
			?? root.GetDirectives(o => o is DefineDirectiveTriviaSyntax).LastOrDefault();
		if (last != null) {
			int e1 = last.FullSpan.End;
			if (havePos = pos <= e1) pos = e1;
		}
		
		if (!havePos) {
			var members = root.Members;
			if (members.Any()) {
				var g = members.LastOrDefault(o => o is GlobalStatementSyntax);
				int posAfterTLS = g?.FullSpan.End ?? members.First().FullSpan.Start;
				
				bool done1 = false;
				if (node is BlockSyntax) {
					done1 = node.Span.ContainsInside(pos);
				} else if (node is MemberDeclarationSyntax) {
					done1 = true;
					//don't use posAfterTLS if before the first type
					bool here = node == members.FirstOrDefault(o => o is not GlobalStatementSyntax) && pos <= node.SpanStart;
					pos = Math.Min(pos, here ? node.SpanStart : posAfterTLS);
				} else if (node is CompilationUnitSyntax && g != members[^1]) { //after types
					done1 = true;
					pos = Math.Min(pos, posAfterTLS);
				}
				if (!done1) {
					for (; node is not CompilationUnitSyntax; node = node.Parent) {
						//CiUtil.PrintNode(node);
						if (node is StatementSyntax) {
							var pa = node.Parent;
							if (node is BlockSyntax && pa is not (BlockSyntax or GlobalStatementSyntax)) continue;
							var span = node.Span;
							if (havePos = pos >= span.End && token.IsKind(SyntaxKind.CloseBraceToken)) pos = node.FullSpan.End;
							else if (havePos = pos >= span.Start) pos = span.Start;
							break;
						}
						if (node is MemberDeclarationSyntax) {
							pos = posAfterTLS;
							break;
						}
					}
				}
				
				havePos |= pos == posAfterTLS;
			}
		}
		
		if (k.meta.end > 0 && pos <= k.meta.end) {
			havePos = true;
			pos = k.meta.end;
			if (code.Eq(pos, "\r\n")) pos += 2; else if (code.Eq(pos, '\n')) pos++;
		}
		
		if (!havePos) { //if in comments or directive or disabled code, move to the start of trivia or line
			var trivia = root.FindTrivia(pos);
			var tk = trivia.Kind();
			if (tk is not (SyntaxKind.EndOfLineTrivia or SyntaxKind.None)) {
				if (tk == SyntaxKind.DisabledTextTrivia) {
					while (pos > 0 && code[pos - 1] != '\n') pos--;
				} else {
					pos = trivia.FullSpan.Start;
				}
				//rejected: move to the start of entire #if ... block.
				//	Rare, not easy, may be far, and maybe user wants to insert into disabled code.
			}
		}
		
		//rename symbols in s if need
		
		try { Util.RenameNewSymbols(ref s, k, node, pos, flags.Has(ICSFlags.MakeVarName1), renameVars); }
		catch (Exception e1) { Debug_.Print(e1); }
		
		//indent, newlines
		
		string breakLine = null;
		for (; pos > 0 && code[pos - 1] != '\n'; pos--) if (code[pos - 1] is not (' ' or '\t')) { breakLine = "\r\n"; break; }
		int replTo = CiUtil.SkipSpace(code, pos);
		
		var d = k.sci;
		
		var t2 = root.FindToken(pos); if (t2.SpanStart >= pos) t2 = t2.GetPreviousToken();
		bool afterOpenBrace = t2.IsKind(SyntaxKind.OpenBraceToken);
		bool beforeCloseBrace = replTo < code.Length && code[replTo] == '}';
		
		int indent = d.aaaLineIndentFromPos(true, pos);
		if (afterOpenBrace && breakLine != null && !(t2.Parent is BlockSyntax bs1 && bs1.Parent is GlobalStatementSyntax)) indent++;
		else if (beforeCloseBrace) indent++;
		
		var b = new StringBuilder(breakLine);
		if (separate && !afterOpenBrace && !s.Starts("{\r\n") && pos > 0) {
			int nn = 0; for (int i = pos; --i >= 0 && code[i] <= ' ';) if (code[i] == '\n' && ++nn == 2) break;
			if (nn < 2) b.AppendIndent(indent).AppendLine();
		}
		
		b.AppendCodeWithIndent(s, indent, andNewline: true);
		
		if (separate && !s.Ends("\n}") && replTo < code.Length && code[replTo] is not ('\r' or '}')) {
			b.AppendIndent(indent).AppendLine();
		}
		if (indent > 0) b.AppendIndent(beforeCloseBrace ? indent - 1 : indent);
		s = b.ToString();
		
		//insert
		
		int go = -1;
		if (flags.Has(ICSFlags.GoToPercent)) {
			go = s.IndexOf('%');
			if (go >= 0) s = s.Remove(go, 1);
		}
		
		d.aaaSelect(true, pos, replTo);
		using (new CodeInfo.Pasting(d, silent: true)) {
			d.aaaReplaceSel(s);
			
			if (go >= 0) d.aaaGoToPos(true, pos + go);
			else if (flags.Has(ICSFlags.SelectNewCode)) d.aaaSelect(true, pos + s.TrimEnd('\t').Length, pos, true);
			else if (flags.Has(ICSFlags.GoToStart)) d.aaaGoToPos(true, pos);
		}
		
		var w = d.AaWnd.Window;
		if (flags.Has(ICSFlags.ActivateEditor)) w.ActivateL();
		if (!flags.Has(ICSFlags.NoFocus) && w.IsActive) d.Focus();
	}
	
	/// <summary>
	/// Inserts text in code editor at current position, not as new line, replaces selection.
	/// If editor is null or readonly, does nothing.
	/// </summary>
	/// <param name="s">If contains '%', removes it and moves caret there.</param>
	public static void TextSimply(string s) {
		Debug.Assert(Environment.CurrentManagedThreadId == 1);
		var d = Panels.Editor.ActiveDoc;
		if (d == null || d.aaaIsReadonly) return;
		TextSimplyInControl(d, s);
	}
	
	/// <summary>
	/// Inserts text in specified or focused control.
	/// At current position, not as new line, replaces selection.
	/// </summary>
	/// <param name="c">If null, uses the focused control, else sets focus.</param>
	/// <param name="s">
	/// If contains '%', removes it and moves caret there.
	/// Alternatively use '\b', then does not touch '%'.
	/// If contains '%' or \b, must be single line.
	/// </param>
	public static void TextSimplyInControl(FrameworkElement c, string s) { //TODO3: flags for processing % etc
		if (c == null) {
			c = App.FocusedElement;
			if (c == null) return;
		} else {
			Debug.Assert(Environment.CurrentManagedThreadId == c.Dispatcher.Thread.ManagedThreadId);
			if (c != App.FocusedElement) //be careful with HwndHost
				c.Focus();
		}
		
		int i = s.IndexOf('\b');
		if (i < 0) i = s.IndexOf('%');
		if (i >= 0) {
			Debug.Assert(!s.Contains('\r'));
			s = s.Remove(i, 1);
			i = s.Length - i;
		}
		
		if (c is KScintilla sci) {
			if (sci.aaaIsReadonly) return;
			sci.aaaReplaceSel(s);
			while (i-- > 0) sci.Call(Sci.SCI_CHARLEFT);
		} else if (c is TextBox tb) {
			if (tb.IsReadOnly) return;
			tb.SelectedText = s;
			tb.CaretIndex = tb.SelectionStart + tb.SelectionLength - Math.Max(i, 0);
		} else {
			Debug_.Print(c);
			if (!c.Hwnd().Window.ActivateL()) return;
			Task.Run(() => {
				var k = new keys(null);
				k.AddText(s);
				if (i > 0) k.AddKey(KKey.Left).AddRepeat(i);
				k.SendNow();
			});
		}
	}
	
	/// <summary>
	/// Inserts code 'using ns;\r\n' in correct place in editor text, unless it is already exists.
	/// Returns true if inserted.
	/// </summary>
	/// <param name="ns">Namespace, eg "System.Diagnostics". Can be multiple, separated with semicolon (can be whitespce around).</param>
	/// <param name="missing">Don't check whether the usings exist. Caller knows they don't exist.</param>
	public static bool UsingDirective(string ns, bool missing = false) {
		Debug.Assert(Environment.CurrentManagedThreadId == 1);
		if (!CodeInfo.GetContextAndDocument(out var k, 0, metaToo: true)) return false;
		var namespaces = ns.Split(';', StringSplitOptions.TrimEntries);
		int i = _FindUsingsInsertPos(k, missing ? null : namespaces);
		if (i < 0) return false;
		
		var b = new StringBuilder();
		if (i > 0 && k.code[i - 1] != '\n') b.AppendLine();
		foreach (var v in namespaces) {
			if (v != null) b.Append("using ").Append(v).AppendLine(";");
		}
		if (i == k.code.Length || k.code[i] is not ('\r' or '\n')) b.AppendLine();
		
		k.sci.aaaInsertText(true, i, b.ToString(), addUndoPointAfter: true, restoreFolding: true);
		
		return true;
		
		//tested: CompilationUnitSyntax.AddUsings isn't better than this code. Does not skip existing. Does not add newlines. Does not skip comments etc. Did't test #directives.
	}
	
	/// <summary>
	/// Finds where new using directives can be inserted:
	/// <br/>• after existing using directives
	/// <br/>• or at 0
	/// <br/>• or after extern aliases
	/// <br/>• or after #directives
	/// <br/>• or after meta
	/// <br/>• or after doc comments
	/// <br/>• or after 1 comments line.
	/// If namespaces!=null, clears existing namespaces in it (sets =null); if all cleared, returns -1.
	/// </summary>
	static int _FindUsingsInsertPos(CodeInfo.Context k, string[] namespaces = null) {
		//In namespaces clears elements that exist in e. If all cleared, sets namespaces=null and returns true.
		bool _ClearExistingUsings(IEnumerable<UsingDirectiveSyntax> e) {
			int n = namespaces.Count(o => o != null); //if (n == 0) return;
			foreach (var u in e) {
				int i = Array.IndexOf(namespaces, u.Name.ToString());
				if (i >= 0) {
					namespaces[i] = null;
					if (--n == 0) { namespaces = null; return true; }
				}
			}
			return false;
		}
		
		//at first look for "global using"
		var semo = k.semanticModel;
		if (namespaces != null && _ClearExistingUsings(CiUtil.GetAllGlobalUsings(semo))) return -1;
		
		int end = -1;
		var cu = k.syntaxRoot;
		
		//then look in current namespace, ancestor namespaces, compilation unit
		int pos = k.sci.aaaCurrentPos16;
		for (var node = cu.FindToken(pos).Parent; node != null; node = node.Parent) {
			SyntaxList<UsingDirectiveSyntax> usings; SyntaxList<ExternAliasDirectiveSyntax> externs;
			if (node is NamespaceDeclarationSyntax ns) {
				if (pos <= ns.OpenBraceToken.SpanStart || pos > ns.CloseBraceToken.SpanStart) continue;
				usings = ns.Usings; externs = ns.Externs;
			} else if (node is CompilationUnitSyntax) {
				usings = cu.Usings; externs = cu.Externs;
			} else continue;
			
			if (usings.Any()) {
				if (namespaces != null && _ClearExistingUsings(usings)) return -1;
				if (end < 0) end = usings[^1].FullSpan.End;
			} else if (externs.Any()) {
				if (end < 0) end = externs[^1].FullSpan.End;
			}
			if (end >= 0 && namespaces == null) break;
		}
		
		if (end < 0) { //insert at the start but after #directives and certain comments
			int end2 = -1;
			foreach (var v in cu.GetLeadingTrivia()) if (v.IsDirective) end2 = v.FullSpan.End; //skip directives
			if (end2 < 0) {
				end2 = k.meta.end; //skip meta
				if (end2 == 0) if (k.code.RxMatch(@"^(///.*\R)+(?=\R|$)", 0, out RXGroup g1)) end2 = g1.End; //skip ///comments
				if (k.code.RxMatch(@"\s*//\.(?: .*)?\R", 0, out RXGroup g2, RXFlags.ANCHORED, end2..)) end2 = g2.End; //skip //. used for folding
			}
			end = end2;
		}
		return end;
	}
	
	/// <summary>
	/// Inserts meta comments.
	/// </summary>
	/// <param name="s">One or more meta options, like <c>"c A; r B;"</c>.</param>
	/// <returns>true if changed documnt text.</returns>
	public static bool MetaComment(string s) {
		Debug.Assert(Environment.CurrentManagedThreadId == 1);
		if (Panels.Editor.ActiveDoc is not { EFile.IsCodeFile: true } doc) return false;
		var meta = new MetaCommentsParser(doc.aaaText);
		var meta2 = new MetaCommentsParser($"/*/ {s} /*/");
		meta.Merge(meta2);
		return meta.Apply();
	}
	
	//rejected
	//public static void AddFileDescription() {
	//	var doc = Panels.Editor.ActiveDoc;
	//	if (!doc.EFile.IsCodeFile) return;
	//	doc.aaaInsertText(false, 0, "/// Description\r\n\r\n");
	//	doc.aaaSelect(false, 4, 15, makeVisible: true);
	//}
	
	public static void SurroundPragmaWarningFormat() {
		CiSnippets.Surround("""
#pragma warning disable format
${SELECTED_TEXT}$0
#pragma warning restore format
""");
	}
	
	public static void AddClassProgram() {
		if (!CodeInfo.GetContextAndDocument(out var cd) /*|| !cd.sci.EFile.IsScript*/) return;
		int start, end = cd.code.Length;
		var members = cd.syntaxRoot.Members;
		if (members.Any()) {
			start = _FindRealStart(false, members[0]);
			if (members[0] is not GlobalStatementSyntax) end = start;
			else if (members.FirstOrDefault(v => v is not GlobalStatementSyntax) is SyntaxNode sn) end = _FindRealStart(true, sn);
			
			int _FindRealStart(bool needEnd, SyntaxNode sn) {
				int start = sn.SpanStart;
				//find first empty line in comments before
				var t = sn.GetLeadingTrivia();
				for (int i = t.Count; --i >= 0;) {
					var v = t[i];
					int ss = v.SpanStart;
					if (ss < cd.meta.end) break;
					//if (needEnd) { print.it($"{v.Kind()}, '{v}'"); continue; }
					var k = v.Kind();
					if (k == SyntaxKind.EndOfLineTrivia) {
						while (i > 0 && t[i - 1].IsKind(SyntaxKind.WhitespaceTrivia)) i--;
						if (i == 0 || t[i - 1].IsKind(k)) return needEnd ? ss : v.Span.End;
					} else if (k == SyntaxKind.SingleLineCommentTrivia) {
						if (cd.code.Eq(ss, "//.") && char.IsWhiteSpace(cd.code[ss + 3])) break;
					} else if (k is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia)) {
						break; //eg #directive
					}
				}
				return start;
			}
		} else start = end;
		//CiUtil.HiliteRange(start, end); return;
		
		CiSnippets.Surround("""
class Program {
	static void Main(string[] a) => new Program(a);
	Program(string[] args) {
${SELECTED_TEXT}$0
	}
}
""", start..end);
	}
	
	/// <summary>
	/// Called from Dicons.
	/// </summary>
	/// <param name="icon">Like "*Pack.Name #color".</param>
	public static void SetMenuToolbarItemIcon(string icon) {
		if (!CodeInfo.GetDocumentAndFindNode(out var cd, out var node, -2)) return;
		var semo = cd.semanticModel;
		
		//find nearest argumentlist and its method symbol
		BaseArgumentListSyntax arglist = null; IMethodSymbol method = null;
		var es = node.FirstAncestorOrSelf<ExpressionStatementSyntax>()?.Expression;
		if (es is InvocationExpressionSyntax ies) { //m.Add("name", "image"); or m.Submenu("name", , "image"); etc
			arglist = ies.ArgumentList;
			method = semo.GetSymbolInfo(ies.Expression).Symbol as IMethodSymbol;
		} else if (es is AssignmentExpressionSyntax aes && aes.Left is ElementAccessExpressionSyntax eaes) { //m["name", "image"] = ;
			arglist = eaes.ArgumentList;
			if (semo.GetSymbolInfo(eaes).Symbol is IPropertySymbol ips && ips.IsIndexer) method = ips.SetMethod;
		} else return;
		if (method == null) return;
		
		//get index of parameter of type MTImage
		var timage = semo.Compilation.GetTypeByMetadataName("Au.Types." + nameof(MTImage)); if (timage == null) return;
		int paramIndex = 0; string paramName = null;
		foreach (var p in method.Parameters) {
			if (p.Type == timage) { paramName = p.Name; break; }
			paramIndex++;
		}
		if (paramIndex == method.Parameters.Length) return;
		
		//find image argument
		ArgumentSyntax arg = null; int i = 0;
		foreach (var a in arglist.Arguments) {
			var nc = a.NameColon;
			if (nc != null) {
				if (nc.Name.Identifier.Text == paramName) arg = a;
			} else {
				if (i == paramIndex) arg = a;
			}
			if (arg != null) break;
			i++;
		}
		
		//replace or insert
		int replFrom, replTo; string prefix = null, suffix = null;
		if (arg != null) {
			var span = arg.Span;
			replFrom = span.Start;
			replTo = span.End;
		} else if (paramIndex < arglist.Arguments.Count) {
			replFrom = replTo = arglist.Arguments[paramIndex].SpanStart;
			suffix = ", ";
		} else {
			replFrom = replTo = arglist.Span.End - 1;
			if (arglist.Arguments.Count > 0) prefix = ", ";
		}
		icon = $"{prefix}{paramName}: \"{icon}\"{suffix}";
		//print.it(cd.pos, replFrom, replTo, icon);
		
		cd.sci.aaaReplaceRange(true, replFrom, replTo, icon);
	}
	
	public static class Util {
		/// <summary>
		/// If <i>s</i> contains local variable declarations, replaces the variable names if they exist in current document in scope at caret position.
		/// </summary>
		public static void RenameNewSymbols(ref string s, int pos, bool makeVarName1 = false) {
			if (!CodeInfo.GetContextAndDocument(out var k)) return;
			var token = k.syntaxRoot.FindToken(k.pos);
			var node = token.Parent;
			try { RenameNewSymbols(ref s, k, node, pos, makeVarName1); }
			catch (Exception e1) { Debug_.Print(e1); }
		}
		
		/// <summary>
		/// If <i>s</i> contains local variable declarations, replaces the variable names if they exist in <i>k.document</i> in scope at <i>node</i>/<i>pos</i>.
		/// </summary>
		/// <param name="makeVarName1">Also rename local variables declared like `var v` with `var v1` at root level.</param>
		/// <param name="rename">Explicit renamings.</param>
		public static void RenameNewSymbols(ref string s, CodeInfo.Context k, SyntaxNode node, int pos, bool makeVarName1, (string oldName, string newName)[] rename = null) {
			//find the function or TLS where to look for declared symbols in editor code
			var scope = node;
			for (; scope is not CompilationUnitSyntax; scope = scope.Parent) {
				if (scope is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) {
					if (scope.Span.ContainsInside(pos)) break;
				}
			}
			
			//modified Roslyn's GetAllDeclaredSymbols. Would be difficult to use it.
			static void _GetDeclaredSymbols(SemanticModel semo, SyntaxNode node, List<ISymbol> symbols, HashSet<string> names, HashSet<string> hVar1, int level = 0) {
				if (node is not CompilationUnitSyntax && semo.GetDeclaredSymbol(node) is ISymbol sym) {
					symbols?.Add(sym);
					names?.Add(sym.Name);
					if (hVar1 != null) if (level == 4 && node.Parent.Parent is LocalDeclarationStatementSyntax && sym.Name is var s && !s.Ends('1')) hVar1.Add(s);
				}
				if (level > 0 && node is LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax) return;
				foreach (var n in node.ChildNodes()) {
					if (n is AnonymousFunctionExpressionSyntax or AnonymousObjectCreationExpressionSyntax or TupleExpressionSyntax) continue; //lambda etc
					_GetDeclaredSymbols(semo, n, symbols, names, hVar1, level + 1);
				}
			}
			
			HashSet<string> h1 = new(); //names of symbols declared in scope. If makeVarName1, also names of local variables declared in s like `var v` at root level.
			
			//get symbols declared in s
			List<ISymbol> a2 = new();
			HashSet<string> h2 = new();
			using var ws2 = new AdhocWorkspace();
			var doc2 = CiUtil.CreateDocumentFromCode(ws2, s, false);
			var semo2 = doc2.GetSemanticModelAsync().Result;
			_GetDeclaredSymbols(semo2, semo2.Root, a2, h2, makeVarName1 ? h1 : null);
			//print.it("---- h2 ----"); foreach (var v in h2) print.it(v);
			if (h2.Count == 0) return;
			
			//get names of symbols declared in scope (editor code)
			var semo1 = k.document.GetSemanticModelAsync().Result;
			_GetDeclaredSymbols(semo1, scope, null, h1, null);
			//print.it("---- h1 ----"); foreach (var v in h1) print.it(v);
			if (h1.Count == 0) return;
			
			bool renamed = false;
			var sol = doc2.Project.Solution;
			
			//in s rename symbols that exist in scope (editor code) or renameVars
			foreach (var sym in a2) {
				string name = sym.Name, name2 = null;
				
				if (rename != null) {
					foreach (var v in rename)
						if (v.oldName == name) {
							name = name2 = v.newName;
							if (!makeVarName1 && !h1.Contains(name)) goto g2;
							goto g1;
						}
				}
				
				if (!h1.Contains(name)) continue;
				g1:
				//create unique name and add to hs1
				int i = 1;
				if (name.RxMatch(@"\d+$", 0, out RXGroup g)) {
					i = Math.Max(i, name.ToInt(g.Start) + 1);
					name = name[..g.Start];
				}
				while (h2.Contains(name2 = name + i) || !h1.Add(name2)) i++;
				//print.it(sym.Name, name2);
				g2:
				var opt1 = new SymbolRenameOptions();
				sol = Renamer.RenameSymbolAsync(sol, sym, opt1, name2).Result;
				h2.Remove(sym.Name);
				renamed = true;
			}
			if (renamed) s = sol.GetDocument(doc2.Id).GetTextAsync().Result.ToString();
			
			//rejected: don't rename if variables in both codes are in unrelated { blocks }.
			//	Tested: in most cases better like now.
			
			//rejected: also replace names that match reserved keywords.
			//	This program will not create such names.
		}
	}
}
