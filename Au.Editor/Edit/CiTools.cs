//Show/hide code info tool windows such as Regex and Keys.

using Au.Tools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Windows.Controls;

class CiTools
{
	//#if DEBUG
	//	public static void RegexTest(int position)
	//	{
	//		var node = NodeAt(position);
	//		print.it(IsInString(ref node, position));
	//	}
	//#endif

	public bool HideTempWindows() {
		bool v1 = _regexWindow?.IsVisible ?? false, v2 = _keysWindow?.IsVisible ?? false;
		if (v1) _regexWindow.Close();
		if (v2) _keysWindow.Close();
		return v1 || v2;
	}

	#region regex

	RegexWindow _regexWindow;
	string _regexTopic;

	public void RegexWindowShow(SciCode doc, string code, int pos16, TextSpan stringSpan, bool replace, wnd dontCover = default) {
		int j = stringSpan.Start, vi = _StringPrefixLength(code, j);

		_regexWindow ??= new RegexWindow();
		_ShowWindow(_regexWindow, doc, pos16, dontCover);

		if (!replace && (vi == 0 || !(code[j] == '@' || code[j + 1] == '@')))
			_regexWindow.CurrentTopic = "Note: The string should be like @\"text\", not like \"text\". This tool does not escape \\ characters.";

		var s = _regexWindow.CurrentTopic;
		if (s == "replace") {
			if (!replace) _regexWindow.CurrentTopic = _regexTopic;
		} else if (replace) {
			_regexTopic = s;
			_regexWindow.CurrentTopic = "replace";
		}
		doc.ZTempRanges_Add(this, stringSpan.Start + vi + 1, stringSpan.End - 1, onLeave: () => _regexWindow.Close());
	}

	//public bool RegexWindowIsVisible => _regexWindow?.Window.Visible ?? false;

	#endregion

	#region keys

	KeysWindow _keysWindow;

	public void KeysWindowShow(SciCode doc, string code, int pos16, TextSpan stringSpan, wnd dontCover = default) {
		_keysWindow ??= new KeysWindow();
		_ShowWindow(_keysWindow, doc, pos16, dontCover);
		int vi = _StringPrefixLength(code, stringSpan.Start);
		doc.ZTempRanges_Add(this, stringSpan.Start + vi + 1, stringSpan.End - 1, onLeave: () => _keysWindow.Close());
	}

	#endregion

	static int _StringPrefixLength(string s, int j) {
		int R = 0;
		if (s[j] == '@') R = s[j + 1] == '$' ? 2 : 1; else if (s[j] == '$') R = s[j + 1] == '@' ? 2 : 1;
		return R;
	}

	static void _ShowWindow(InfoWindow w, SciCode doc, int position, wnd dontCover) {
		if (w.IsVisible) w.Hwnd.ZorderTop();
		var r = CiUtil.GetCaretRectFromPos(doc, position, inScreen: true);
		r.left -= Dpi.Scale(80, doc);
		bool above = !dontCover.Is0;
		if (above) r = RECT.Union(r, dontCover.Rect);
		w.ShowByRect(doc, above ? Dock.Top : Dock.Bottom, r, exactSize: true);
		w.InsertInControl = doc;
	}

	public static void CmdShowRegexWindow() => _ShowRegexOrKeysWindow(true);
	public static void CmdShowKeysWindow() => _ShowRegexOrKeysWindow(false);

	static void _ShowRegexOrKeysWindow(bool isRegex) {
		bool retry = false;
		g1:
		if (!CodeInfo.GetDocumentAndFindNode(out var cd, out var token, out var node)) return;
		var pos16 = cd.pos16;
		if (!CiUtil.IsInString(ref node, pos16)) {
			if (isRegex || retry) {
				var s2 = isRegex ? null : "The fastest way to insert 'send keys' code: type kk and press Enter (or Tab, Space, double-click). It shows completion list and selects kkKeysSendSnippet.";
				dialog.showInfo("The text cursor must be in a string.", s2);
				return;
			}

			//is in keys.send argument list?
			if (node is not ArgumentListSyntax && !node.Span.ContainsInside(pos16)) {
				node = node.Parent;
				if(node is ArgumentSyntax) node = node.Parent;
			}
			if (node is ArgumentListSyntax && node.Parent is InvocationExpressionSyntax ie && ie.Expression.ToString() == "keys.send") {
				SyntaxToken t1, t2;
				if (pos16 <= token.SpanStart) { t1 = token.GetPreviousToken(); t2 = token; } else { t1 = token; t2 = token.GetNextToken(); }
				//CiUtil.PrintNode(t1);
				//CiUtil.PrintNode(t2);
				SyntaxKind k1 = t1.Kind(), k2 = t2.Kind();
				bool good1 = k1 is SyntaxKind.OpenParenToken or SyntaxKind.CommaToken, good2 = k2 is SyntaxKind.CloseParenToken or SyntaxKind.CommaToken;
				string s;
				if (good1 && good2) s = "\"%\""; else if(good1) s = "\"%\", "; else if(good2) s = ", \"%\""; else s = ", \"%\", ";
				InsertCode.TextSimply(s);
			} else {
				InsertCode.Statements("keys.send(\"%\");", goToPercent: true); //rejected. Eg could be keys.send("", here).
			}
			retry = true;
			goto g1;
		}
		var doc = cd.sciDoc;
		var stringSpan = node.Span;

		var t = CodeInfo._tools;
		if (isRegex) t.RegexWindowShow(doc, cd.code, pos16, stringSpan, replace: false);
		else t.KeysWindowShow(doc, cd.code, pos16, stringSpan);
	}

	public void ShowForStringParameter(PSFormat stringFormat, CodeInfo.Context cd, TextSpan stringSpan, wnd dontCover = default) {
		switch (stringFormat) {
		case PSFormat.Regexp:
		case PSFormat.RegexpReplacement:
			RegexWindowShow(cd.sciDoc, cd.code, cd.pos16, stringSpan, replace: stringFormat == PSFormat.RegexpReplacement, dontCover);
			break;
		case PSFormat.Keys:
			KeysWindowShow(cd.sciDoc, cd.code, cd.pos16, stringSpan, dontCover);
			break;
		}
	}
}
