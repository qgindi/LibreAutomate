using System.Linq;
using System.Windows.Controls;
using Au.Controls;
using static Au.Controls.Sci;
using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Classification;
//using Microsoft.CodeAnalysis.Shared.Extensions;
//using Microsoft.CodeAnalysis.CSharp.Extensions;
using EToken = CiStyling.EToken;

class PanelRecipe : DockPanel
{
	KScintilla _c;

	//public KScintilla ZControl => _c;

	public PanelRecipe() {
		//this.UiaSetName("Recipe panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

		_c = new KScintilla {
			Name = "Recipe_text",
			ZInitReadOnlyAlways = true,
			ZInitTagsStyle = KScintilla.ZTagsStyle.User
		};
		_c.ZHandleCreated += _c_ZHandleCreated;

		this.Children.Add(_c);
	}

	private void _c_ZHandleCreated() {
		_c.Call(SCI_SETWRAPMODE, SC_WRAP_WORD);

		_c.zSetMarginWidth(1, 8);
		_c.Call(SCI_MARKERDEFINE, 0, SC_MARK_FULLRECT);
		_c.Call(SCI_MARKERSETBACK, 0, 0xA0E0B0);

		var styles = new CiStyling.TStyles { FontSize = 8 };
		styles.ToScintilla(_c, multiFont: true);

		_c.ZTags.AddLinkTag("+see", _SeeLinkClicked);

		_AutoRenderCurrentRecipeScript();
	}

	public void SetText(string code) {
		Panels.PanelManager[this].Visible = true;
		_SetText(code);
	}

	void _SetText(string code) {
		_c.zClearText();

		var ac = new List<(string code, int offset8)>();
		int iCode = 0;
		foreach (var m in code.RxFindAll(@"(?ms)^(?:///(?!=/)\N*\R*)+|^/\*\*.+?\*/\R*")) {
			//print.it(m);
			if (iCode > 0) _Code(iCode, m.Start);
			iCode = m.End;
			_Text(m.Start, m.End);
		}
		if (iCode < code.Length) _Code(iCode, code.Length);

		void _Text(int start, int end) {
			while (code[end - 1] <= ' ') end--;
			bool ml = code[start + 1] == '*';
			if (ml) {
				start += 3; while (code[start] <= ' ') start++;
				end -= 2; while (end > start && code[end - 1] <= ' ') end--;
			}
			var s = code[start..end];
			if (!ml) s = s.RxReplace(@"(?m)^/// ?", "");
			s = s.RxReplace(@"<see cref=['""](.+?)['""]/>", "<+see '$1'>$1<>");
			//print.it("TEXT"); print.it(s);
			_c.ZTags.AddText(s, true, false, false);
		}

		void _Code(int start, int end) {
			while (end > start && code[end - 1] <= ' ') end--;
			if (end == start) return;
			bool isIf = false;
			if (code[end - 1] == '}' && (code[start] == '{' || (isIf = code.Eq(start, "if ") || code.Eq(start, "if(")))) {
				if (isIf) start = code.IndexOf('{', start);
				while (++start < end) if (code[start] > ' ') break;
				while (--end > start) if (code[end - 1] > ' ') break;
			}
			var s = code[start..end];
			if (isIf) s = s.Replace("\n\t", "\n");
			//print.it("CODE"); print.it(s);

			int n1 = _c.zLineCount, offset8 = _c.zLen8 + 2;
			_c.zAppendText("\r\n" + s + "\r\n", andRN: true, scroll: false, ignoreTags: true);
			int n2 = _c.zLineCount - 2;
			for (int i = n1; i < n2; i++) _c.Call(SCI_MARKERADD, i, 0);
			ac.Add((s, offset8));
		}

		//code styling
		if (ac != null) {
			code = string.Join("\r\n", ac.Select(o => o.code));
			Debug.Assert(code.IsAscii()); //never mind: does not support non-ASCII
			var b = new byte[code.Length];
			var document = CiUtil.CreateRoslynDocument(code, needSemantic: true);
			var semo = document.GetSemanticModelAsync().Result;
			var a = Classifier.GetClassifiedSpans(semo, TextSpan.FromBounds(0, code.Length), document.Project.Solution.Workspace);
			foreach (var v in a) {
				//print.it(v.ClassificationType, code[v.TextSpan.Start..v.TextSpan.End]);
				EToken style = CiStyling.StyleFromClassifiedSpan(v, semo);
				if (style == EToken.None) continue;
				for (int i = v.TextSpan.Start; i < v.TextSpan.End; i++) b[i] = (byte)style;
			}
			unsafe {
				fixed (byte* bp = b) {
					int bOffset = 0;
					foreach (var v in ac) {
						_c.Call(SCI_STARTSTYLING, v.offset8);
						_c.Call(SCI_SETSTYLINGEX, v.code.Length, bp + bOffset);
						bOffset += v.code.Length + 2; //+2 for string.Join("\r\n"
					}
				}
			}
			_c.zSetStyled();
		}
	}

	void _SeeLinkClicked(string s) {
		//add same namespaces as in default global.cs. Don't include global.cs because it may be modified.
		string code = $"///<see cref='{s}'/>";
		var document = CiUtil.CreateRoslynDocument(code, needSemantic: true);
		var syn = document.GetSyntaxRootAsync().Result;
		var node = syn.FindToken(code.Length - 3 - s.Length, true).Parent.FirstAncestorOrSelf<CrefSyntax>();
		if (node == null) return;
		var semo = document.GetSemanticModelAsync().Result;
		var si = semo.GetSymbolInfo(node);
		var sym = si.Symbol;
		//print.it(sym, si.CandidateSymbols);
		if (sym == null) {
			if (si.CandidateSymbols.IsDefaultOrEmpty) return;
			sym = si.CandidateSymbols[0];
		}
		var url = CiUtil.GetSymbolHelpUrl(sym);
		if (url != null) run.itSafe(url);
	}

	//class _KScintilla : KScintilla
	//{
	//	//protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
	//	//	switch (msg) {
	//	//	}
	//	//	return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
	//	//}
	//}

	[Conditional("DEBUG")]
	void _AutoRenderCurrentRecipeScript() {
		string prevText = null;
		SciCode prevDoc = null;
		App.Timer1sWhenVisible += () => {
			if (App.Model.WorkspaceName != "Cookbook") return;
			if (!this.IsVisible) return;
			var doc = Panels.Editor.ZActiveDoc;
			if (doc == null || !doc.ZFile.IsScript) return;
			string text = doc.zText;
			if (text == prevText) return;
			prevText = text;
			//print.it("update");

			int n1 = _c.Call(SCI_GETFIRSTVISIBLELINE);
			_SetText(text);
			if (doc == prevDoc) _c.Call(SCI_SETFIRSTVISIBLELINE, n1); else prevDoc = doc;
			//rejected: autoscroll. Even if works perfectly, often it is more annoying than useful.
		};
	}
}
