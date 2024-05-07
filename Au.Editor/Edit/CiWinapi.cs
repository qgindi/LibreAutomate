extern alias CAW;

using Au.Controls;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;

class CiWinapi {
	int _typenameStart;
	bool _canInsert;

	public static bool IsWinapiClassSymbol(INamedTypeSymbol typeSym) => typeSym?.BaseType?.Name == "NativeApi";

	public static CiWinapi AddWinapi(INamedTypeSymbol typeSym, List<CiComplItem> items, TextSpan span, int typenameStart, bool onlyTypes) {
		Debug.Assert(IsWinapiClassSymbol(typeSym));

		//In 'new' context show only types, except interfaces.
		//Also should do the same in 'as' context, and partially in 'is' context. But Roslyn doesn't, so we too.
		var sql = "SELECT name, kind FROM api";
		if (onlyTypes) sql += " WHERE kind<=4"; //see CiItemKind

		items.Capacity = items.Count + 60000;
		using var db = EdDatabases.OpenWinapi();
		using var stat = db.Statement(sql);
		while (stat.Step()) {
			string name = stat.GetText(0);
			var kind = (CiItemKind)stat.GetInt(1);
			var ci = new CiComplItem(CiComplProvider.Winapi, span, name, kind/*, CiItemAccess.Internal*/);
			items.Add(ci);
		}

		return new() { _typenameStart = typenameStart, _canInsert = typeSym.IsFromSource() };

		//Now (May 2021) there are ~51000 items, and uses ~16 MB of memory while showing the popup list.
		//	Without CompletionItem would be ~8 MB. But now it is used everywhere and need some work to remove. Never mind.
	}

	public static System.Windows.Documents.Section GetDescription(CiComplItem item) {
		var m = new CiText();
		m.StartParagraph();
		m.Append(item.kind.ToString() + " "); m.Bold(item.Text); m.Append(".");
		m.EndParagraph();
		if (_GetText(item, out string s)) m.CodeBlock(s);
		return m.Result;
	}

	static bool _GetText(CiComplItem item, out string text) {
		using var db = EdDatabases.OpenWinapi();
		return db.Get(out text, $"SELECT code FROM api WHERE name='{item.Text}'");
	}

	public void OnCommitInsertDeclaration(CiComplItem item) {
		if (!_GetText(item, out string text)) return;
		if (_InsertDeclaration(item, text)) return;
		clipboard.text = text;
		print.it("<>Clipboard:\r\n<code>" + text + "</code>");
	}

	bool _InsertDeclaration(CiComplItem item, string text) {
		if (!_canInsert) return false;
		if (!CodeInfo.GetDocumentAndFindNode(out var cd, out var typenameNode, _typenameStart)) return false;
		var semo = cd.semanticModel;
		var sym = semo.GetSymbolInfo(typenameNode).Symbol;
		if (sym is not INamedTypeSymbol t || !t.IsFromSource()) return false;
		var sr = t.DeclaringSyntaxReferences[0];

		SciCode doc = cd.sci;
		FileNode fSelect = null;
		if (sr.SyntaxTree != semo.SyntaxTree) {
			var f = App.Model.Find(sr.SyntaxTree.FilePath, FNFind.CodeFile);
			if (!App.Model.SetCurrentFile(f, dontChangeTreeSelection: true)) return false;
			doc = Panels.Editor.ActiveDoc;
			fSelect = cd.sci.FN;
		}

		var hs = new HashSet<string>();
		using (new KScintilla.aaaUndoAction(doc)) {
			_Insert(0, sr.GetSyntax(), text, item.Text, item.kind);
		}

		if (fSelect != null) App.Model.SetCurrentFile(fSelect);
		return true;

		void _Insert(int level, SyntaxNode node, string text, string name, CiItemKind kind) {
			if (node is not ClassDeclarationSyntax nodeCD) return;
			int posClass = nodeCD.Keyword.SpanStart, posInsert = nodeCD.CloseBraceToken.SpanStart;
			string emptyLine = "\r\n";

			//if constant, try to insert below the last existing constant with same prefix
			if (kind == CiItemKind.Constant) {
				int u = name.IndexOf('_') + 1;
				if (u > 1) {
					string prefix = " " + name[..u], code = doc.aaaText;
					foreach (var v in nodeCD.ChildNodes().OfType<FieldDeclarationSyntax>()) {
						var span = v.Span;
						int j = code.Find(prefix, span.Start..span.End);
						if (j > 0 && code.Find("const ", span.Start..j) > 0) {
							posInsert = v.FullSpan.End;
							emptyLine = null;
							//break; //no, need the last
						} else if (emptyLine == null) break;
					}
				}
			}

			//if (level == 0) { //insert missing usings first. Now in global.cs. Or CiErrors will add.
			//	int len = doc.aaaLen16;
			//	InsertCode.UsingDirective("Au;Au.Types;System;System.Runtime.InteropServices"); //Au: wnd; Au.Types: RECT etc; System: IntPtr, Guid etc
			//	int add = doc.aaaLen16 - len;
			//	posInsert += add; posClass += add;
			//}

			text = emptyLine + text + "\r\n";
			doc.aaaInsertText(true, posInsert, text, addUndoPointAfter: true, restoreFolding: true);

			//recursively add declarations for unknown names found in now added declaration
			if (kind is CiItemKind.Constant or CiItemKind.Field or CiItemKind.Enum or CiItemKind.Class) return;
			//print.it(level, name);
			if (level > 30) return; //max seen 10. Tested: at level 10 uses ~40 KB of stack.
			if (!CodeInfo.GetDocumentAndFindNode(out var cd, out node, posClass)) return;
			if (node is not ClassDeclarationSyntax) return;
			var semo = cd.semanticModel;
			var newSpan = new TextSpan(posInsert, text.Length);
			var da = semo.GetDiagnostics(newSpan); //the slowest part
			foreach (var d in da) {
				var ec = (ErrorCode)d.Code;
				if (ec
					is ErrorCode.ERR_SingleTypeNameNotFound
					or ErrorCode.ERR_NameNotInContext //never seen
					or ErrorCode.ERR_BadAccess //eg "'VARIANT' is inaccessible due to its protection level", because defined as internal in some assembly
					) {
					var loc = d.Location;
					if (!loc.IsInSource) {
						Debug_.Print("!insource");
						continue;
					}
					var span = loc.SourceSpan;
					if (!newSpan.Contains(span)) {
						Debug_.Print("!contains");
						continue;
					}
					name = cd.code[span.Start..span.End];
					//print.it(name);

					if (!hs.Add(name)) { //same name again
										 //print.it("same", name);
						continue;
					}

					//some parameter types add much garbage, but the parameters are rarely used.
					//	If parameter, it is usually null. If interface member parameter, the member is rarely used.
					//	Let's add empty definition. It's easy to replace it with full definition when need.
					if (name == "IBindCtx") {
						text = """
[ComImport, Guid("0000000e-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IBindCtx {}
""";
						kind = CiItemKind.Interface;
					} else if (name == "PROPVARIANT") {
						text = "internal struct PROPVARIANT { int a, b; nint c, d; }";
						kind = CiItemKind.Structure;
					} else {
						using var db = EdDatabases.OpenWinapi(); //fast, if compared with GetDiagnostics
						using var stat = db.Statement("SELECT code, kind FROM api WHERE name=?", name);
						if (!stat.Step()) {
							Debug_.Print("not in DB: " + name);
							continue;
						}
						text = stat.GetText(0);
						kind = (CiItemKind)stat.GetInt(1);
						//print.it(kind, text);
					}

					_Insert(level + 1, node, text, name, kind);
				} else {
					if (ec == ErrorCode.ERR_ManagedAddr) continue; //possibly same name is an internal managed type in some assembly, but in our DB it may be unmanaged. This error is for for field; we'll catch its type later.
					if (ec == ErrorCode.WRN_NewNotRequired) continue; //when 'new' used with a repeated member of a base interface, the base is still not declared, therefore this warning
					Debug_.Print(d);
				}
			}
		}
	}
}
