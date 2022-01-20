using System.Linq;
using Au.Controls;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using acc = Microsoft.CodeAnalysis.Accessibility;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis.Shared.Extensions;

/// <summary>
/// Inserts various code in code editor. With correct indentation etc.
/// Some functions can insert in other controls too.
/// </summary>
static class InsertCode
{
	/// <summary>
	/// Inserts one or more statements at current line.
	/// If editor is null or readonly, prints in output.
	/// Async if called from non-main thread.
	/// </summary>
	/// <param name="s">Text without "\r\n" at the end. Does nothing if null.</param>
	/// <param name="goToPercent">If contains '%', removes it and moves caret there.</param>
	/// <param name="fold">Collapse folding points in inserted code.</param>
	/// <param name="activate">Activate editor window.</param>
	public static void Statements(string s, bool goToPercent = false, bool fold = false, bool activate = false) {
		if (s == null) return;

		if (Environment.CurrentManagedThreadId == 1) _Action(s, goToPercent, fold, activate);
		else App.Dispatcher.InvokeAsync(() => _Action(s, goToPercent, fold, activate));

		static void _Action(string s, bool goToPercent, bool fold, bool activate) {
			if (!App.Hmain.IsVisible) App.ShowWindow();
			var d = Panels.Editor.ZActiveDoc;
			if (d == null || d.zIsReadonly) {
				print.it(s);
			} else {
				if (activate) d.Hwnd.Window.ActivateL();
				if (d.Hwnd.Window.IsActive) d.Focus();
				int start = d.zLineStartFromPos(false, d.zCurrentPos8);
				int indent = d.zLineIndentationFromPos(false, start);
				if (indent == 0) {
					s += "\r\n";
				} else {
					var b = new StringBuilder();
					foreach (var v in s.Lines()) b.Append('\t', indent).AppendLine(v);
					s = b.ToString();
				}

				int i = -1;
				if (goToPercent) {
					i = s.IndexOf('%');
					if (i >= 0) s = s.Remove(i, 1);
				}

				CodeInfo.Pasting(d, silent: true);
				d.zReplaceSel(false, start, s);
				if (i >= 0) d.zGoToPos(true, start + i);

				if (fold) _FoldInsertedCode(d, start, s.LineCount());
			}
		}

		static void _FoldInsertedCode(SciCode doc, int start8, int nLines) {
			string text = doc.zText;
			timer.after(400, _ => { //because fold points are added async, 250 ms timer + async/await
				var d = Panels.Editor.ZActiveDoc; if (d != doc || d.zText != text) return;
				for (int line = d.zLineFromPos(false, start8), i = line + nLines - 1; --i >= line;) {
					if (0 != (d.Call(Sci.SCI_GETFOLDLEVEL, i) & Sci.SC_FOLDLEVELHEADERFLAG)) d.Call(Sci.SCI_FOLDLINE, i);
				}
			});
		}
	}

	/// <summary>
	/// Inserts text in code editor at current position, not as new line, replaces selection.
	/// If editor is null or readonly, does nothing.
	/// </summary>
	/// <param name="s">If contains '%', removes it and moves caret there.</param>
	public static void TextSimply(string s) {
		Debug.Assert(Environment.CurrentManagedThreadId == 1);
		var d = Panels.Editor.ZActiveDoc;
		if (d == null || d.zIsReadonly) return;
		TextSimplyInControl(d, s);
	}

	/// <summary>
	/// Inserts text in specified or focused control.
	/// At current position, not as new line, replaces selection.
	/// </summary>
	/// <param name="c">If null, uses the focused control, else sets focus.</param>
	/// <param name="s">If contains '%', removes it and moves caret there. Alternatively use '\b', then does not touch '%'.</param>
	public static void TextSimplyInControl(FrameworkElement c, string s) {
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
			if (sci.zIsReadonly) return;
			sci.zReplaceSel(s);
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
				k.SendIt();
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

		k.sciDoc.zInsertText(true, i, b.ToString(), addUndoPointAfter: true, restoreFolding: true);

		return true;

		//tested: CompilationUnitSyntax.AddUsings isn't better than this code. Does not skip existing. Does not add newlines. Does not skip comments etc. Did't test #directives.
	}

	/// <summary>
	/// Finds where new using directives can be inserted: end of existing using directives or 0 or end of extern aliases or #directives or meta or doc comments or 1 comments line.
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
		var semo = k.document.GetSemanticModelAsync().Result;
		if (namespaces != null && _ClearExistingUsings(CiUtil.GetAllGlobalUsings(semo))) return -1;

		int end = -1;
		var cu = k.document.GetSyntaxRootAsync().Result as CompilationUnitSyntax;

		//then look in current namespace, ancestor namespaces, compilation unit
		int pos = k.sciDoc.zCurrentPos16;
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

		if (end < 0) { //insert at start but after #directives and certain comments
			int end2 = -1;
			foreach (var v in cu.GetLeadingTrivia()) if (v.IsDirective) end2 = v.FullSpan.End; //skip directives
			if (end2 < 0) {
				end2 = k.meta.end; //skip meta
				if (end2 == 0) if (k.code.RxMatch(@"(\s*///.*\R)+", 0, out RXGroup g1, RXFlags.ANCHORED, end2..)) end2 = g1.End; //skip ///comments
				if (k.code.RxMatch(@"\s*//.+\R", 0, out RXGroup g2, RXFlags.ANCHORED, end2..)) end2 = g2.End; //skip 1 line of //comments, because usually it is //. for folding
			}
			end = end2;
		}
		return end;
	}

	/// <summary>
	/// Called from CiCompletion._ShowList on char '/'. If need, inserts XML doc comment with empty summary, param and returns tags.
	/// </summary>
	public static void DocComment(CodeInfo.Context cd) {
		int pos = cd.pos16;
		string code = cd.code;
		SciCode doc = cd.sciDoc;

		if (0 == code.Eq(pos - 3, false, "///\r", "///\n") || !IsLineStart(code, pos - 3)) return;

		var root = cd.document.GetSyntaxRootAsync().Result;
		var node = root.FindToken(pos).Parent;
		var start = node.SpanStart;
		if (start < pos) return;

		if (node is not MemberDeclarationSyntax) { //can be eg func return type (if no public etc) or attribute
			node = node.Parent;
			if (node is not MemberDeclarationSyntax || node.SpanStart != start) return;
		}
		if (node is GlobalStatementSyntax) return;

		//already has doc comment?
		foreach (var v in node.GetLeadingTrivia()) {
			if (v.IsDocumentationCommentTrivia) { //singleline (preceded by ///) or multiline (preceded by /**)
				var span = v.Span;
				if (span.Start != pos || span.Length > 2) return; //when single ///, span includes only newline after ///
			}
		}
		//print.it(pos);
		//CiUtil.PrintNode(node);

		string s = @" <summary>
/// 
/// </summary>";
		var pl = node switch { BaseMethodDeclarationSyntax met => met.ParameterList, RecordDeclarationSyntax rec => rec.ParameterList, _ => null };
		if (pl != null) {
			var b = new StringBuilder(s);
			foreach (var p in pl.Parameters) {
				b.Append("\r\n/// <param name=\"").Append(p.Identifier.Text).Append("\"></param>");
			}
			if (node is MethodDeclarationSyntax mm) {
				var rt = mm.ReturnType;
				if (!code.Eq(rt.Span, "void")) b.Append("\r\n/// <returns></returns>");
			}
			s = b.ToString();
			//rejected: <typeparam name="TT"></typeparam>. Rarely used.
		}

		s = IndentStringForInsert(s, doc, pos);

		doc.zInsertText(true, pos, s, true, true);
		doc.zGoToPos(true, pos + s.Find("/ ") + 2);
	}

	/// <summary>
	/// Returns true if pos in string is at a line start + any number of spaces and tabs.
	/// </summary>
	public static bool IsLineStart(string s, int pos) {
		int i = pos; while (--i >= 0 && (s[i] == ' ' || s[i] == '\t')) { }
		return i < 0 || s[i] == '\n';
	}

	/// <summary>
	/// Returns string with same indentation as of the document line from pos.
	/// </summary>
	public static string IndentStringForInsert(string s, SciCode doc, int pos) {
		int indent = doc.zLineIndentationFromPos(true, pos);
		if (indent > 0) s = s.RxReplace(@"(?<=\n)", new string('\t', indent));
		return s;
	}

	public static void ImplementInterfaceOrAbstractClass(bool explicitly, int position = -1) {
		if (!CodeInfo.GetContextAndDocument(out var cd, position)) return;
		var semo = cd.document.GetSemanticModelAsync().Result;
		var node = semo.Root.FindToken(cd.pos16).Parent;
		//CiUtil.PrintNode(node);

		bool haveBaseType = false;
		for (var n = node; n != null; n = n.Parent) {
			//print.it(n.Kind());
			if (n is BaseTypeSyntax bts) {
				node = bts.Type;
				haveBaseType = true;
			} else if (n is TypeDeclarationSyntax tds) {
				if (!haveBaseType) try { node = tds.BaseList.Types[0].Type; } catch { return; }
				position = tds.CloseBraceToken.Span.Start;
				goto g1;
			}
		}
		return;
		g1:

		var baseType = semo.GetTypeInfo(node).Type as INamedTypeSymbol;
		if (baseType == null) return;
		bool isInterface = false;
		switch (baseType.TypeKind) {
		case TypeKind.Interface: isInterface = true; break;
		case TypeKind.Class when baseType.IsAbstract: break;
		default: return;
		}

		var b = new StringBuilder();
		var format = CiText.s_symbolFullFormat.WithParameterOptions(CiText.s_symbolFullFormat.ParameterOptions & ~SymbolDisplayParameterOptions.IncludeOptionalBrackets);
		var formatExp = format.WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeRef);

		b.Append("\r\n#region ").Append(baseType.ToMinimalDisplayString(semo, position, CiText.s_symbolFullFormat));

		if (isInterface) {
			_Base(baseType, explicitly);
			foreach (var bi in baseType.AllInterfaces) {
				_Base(bi,
					explicitly || (baseType.IsGenericType && !bi.IsGenericType) //eg public IEnumerable<T> and explicit IEnumerable
					);
			}
		} else {
			for (; baseType != null && baseType.IsAbstract; baseType = baseType.BaseType) {
				_Base(baseType, false);
			}
		}

		b.AppendLine("\r\n\r\n#endregion");

		void _Base(INamedTypeSymbol type, bool explicitly) {
			foreach (var v in type.GetMembers()) {
				//print.it(v, v.IsStatic, v.GetType().GetInterfaces());
				bool isAbstract = v.IsAbstract;
				if (!isAbstract && !isInterface) continue;
				if (v.IsStatic) continue;
				bool expl = explicitly || (isInterface && v.DeclaredAccessibility != acc.Public);

				string append = null;
				switch (v) {
				case IMethodSymbol ims when ims.MethodKind == MethodKind.Ordinary:
					append = ims.ReturnsVoid ? @" {
	
}" : @" {
	
	return default;
}";
					break;
				case IPropertySymbol ips:
					if (!expl && isInterface) {
						if (ips.GetMethod != null && ips.GetMethod.DeclaredAccessibility != acc.Public) expl = true;
						if (ips.SetMethod != null && ips.SetMethod.DeclaredAccessibility != acc.Public) expl = true;
					}
					break;
				case IEventSymbol:
					append = !expl ? ";" : @" {
	add {  }
	remove {  }
}";
					break;
				default:
					continue;
				}

				//if(null != classType.FindImplementationForInterfaceMember(v)) continue; //never mind

				b.AppendLine("\r\n");
				if (isInterface) {
					if (!isAbstract) b.AppendLine("//has default implementation");
					if (!expl) b.Append("public ");
				} else {
					b.Append(v.DeclaredAccessibility switch { acc.Public => "public", acc.Internal => "internal", acc.Protected => "protected", acc.ProtectedOrInternal => "protected internal", acc.ProtectedAndInternal => "private protected", _ => "" });
					b.Append(" override ");
				}
				b.Append(v.ToMinimalDisplayString(semo, position, expl ? formatExp : format)).Append(append);
			}
		}

		var text = b.ToString();
		text = text.Replace("] { get; set; }", @"] {
	get { return default; }
	set {  }
}"); //indexers
		text = text.RxReplace(@"[^\]] \{\K set; \}", @"
	set {  }
}"); //write-only properties

		//print.it(text);
		//clipboard.text = text;

		cd.sciDoc.zInsertText(true, position, text, addUndoPointAfter: true);
		cd.sciDoc.zGoToPos(true, position);

		//tested: Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementInterfaceService works but the result is badly formatted (without spaces, etc). Internal, undocumented.
	}

	public static void AddFileDescription() {
		var doc = Panels.Editor.ZActiveDoc; if (doc == null) return;
		doc.zInsertText(false, 0, "/// Description\r\n\r\n");
		doc.zSelect(false, 4, 15, makeVisible: true);
	}

	public static void AddClassProgram() {
		//rejected: menu. Then need to think what to select.
		//	Maybe in the future show input box with string remembered from previous time. Or let specify string in Options.
		//var a = new string[] {
		//	"class Program { static void Main(string[] a) => new Program(a); Program(string[] args) { //...",
		//	"class Program { static void Main(string[] args) { //...",
		//};
		//int pm = popupMenu.showSimple(a) - 1; if (pm < 0) return;

		if (!CodeInfo.GetContextAndDocument(out var cd) /*|| !cd.sciDoc.ZFile.IsScript*/) return;
		var cu = cd.document.GetSyntaxRootAsync().Result as CompilationUnitSyntax;

		int start, end = cd.code.Length;
		var members = cu.Members;
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

		using var undo = new KScintilla.UndoAction(cd.sciDoc);
		cd.sciDoc.zInsertText(true, end, "\r\n}\r\n}\r\n");
		//cd.sciDoc.zInsertText(true, start, a[pm] + "\r\n");
		string s = "class Program { static void Main(string[] a) => new Program(a); Program(string[] args) { //...\r\n";
		cd.sciDoc.zInsertText(true, start, s);
		//if (s.RxMatch(@" =>.+args\)", 0, out RXGroup g)) cd.sciDoc.zSelect(true, start + g.Start, start + g.End); //then can simply press Delete to make it classic. No, it is rather distracting.
	}
}
