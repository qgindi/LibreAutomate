using System.Linq;
using System.Windows.Controls;
using Au.Controls;
using static Au.Controls.Sci;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using EToken = CiStyling.EToken;

class PanelRecipe : DockPanel {
	_KScintilla _c;
	string _usings;

	//public KScintilla ZControl => _c;

	public PanelRecipe() {
		//this.UiaSetName("Recipe panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

		_c = new _KScintilla {
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

		//_c.zStyleFont(STYLE_DEFAULT); //Segoe UI, 9. Too narrow and looks too small when compared with the code font.
		//_c.zStyleFont(STYLE_DEFAULT, "Segoe UI", 10); //too tall
		//_c.zStyleFont(STYLE_DEFAULT, "Verdana", 9); //too wide
		//_c.zStyleFont(STYLE_DEFAULT, "Calibri", 9); //too small
		_c.zStyleFont(STYLE_DEFAULT, "Tahoma", 9);
		var styles = new CiStyling.TStyles { FontSize = 9 };
		styles.ToScintilla(_c, multiFont: true);
		_c.Call(SCI_SETZOOM, App.Settings.recipe_zoom);

		_c.ZTags.AddLinkTag("+recipe", Panels.Cookbook.OpenRecipe);
		_c.ZTags.AddLinkTag("+see", _SeeLinkClicked);
		//_c.ZTags.AddLinkTag("+lang", s => run.itSafe("https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/" + s)); //unreliable, the URLs may change
		_c.ZTags.AddLinkTag("+lang", s => run.itSafe("https://www.google.com/search?q=" + Uri.EscapeDataString(s + ", C# reference")));
		//_c.ZTags.AddLinkTag("+guide", s => run.itSafe("https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/" + s)); //rejected. Use <google>.
		_c.ZTags.AddLinkTag("+ms", s => run.itSafe("https://www.google.com/search?q=" + Uri.EscapeDataString(s + " site:docs.microsoft.com")));
		_c.ZTags.AddLinkTag("+nuget", s => DNuget.ZShow(s));
		_c.ZTags.AddStyleTag(".k", new SciTags.UserDefinedStyle { textColor = 0xFF, bold = true }); //keyword

#if DEBUG
		_AutoRenderCurrentRecipeScript();
#endif
	}

	public void Display(string name, string code) {
		Panels.PanelManager[this].Visible = true;
		_SetText(name, code);
	}

	void _SetText(string name, string code) {
		_c.zClearText();
		if (!name.NE() && !code.Starts("/// <Z")) code = $"/// <Z YellowGreen><b>{name}</b><>\r\n\r\n{code}";

		//rejected:
		//	1. Ignore code before the first ///. Not really useful, just forces to always start with ///.
		//	2. Use {  } for scopes of variables. Its' better to use unique names.
		//	3. Use if(...) {  } to enclose code examples to select which code to test.
		//		Can accidentally damage real 'if' code. I didn't use it; it's better to test codes in other script.

		StringBuilder usings = null;
		var ac = new List<(string code, int offset8)>();
		int iCode = 0;
		foreach (var m in code.RxFindAll(@"(?ms)^(?:///(?!=/)\N*\R*)+|^/\*\*.+?\*/\R*")) {
			//print.it(m);
			_Code(iCode, m.Start);
			iCode = m.End;
			_Text(m.Start, m.End);
		}
		_Code(iCode, code.Length);
		_usings = usings?.ToString();

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
			var s = code[start..end];
			//print.it("CODE"); print.it(s);

			int n1 = _c.zLineCount, offset8 = _c.zLen8 + 2;
			_c.zAppendText("\r\n" + s + "\r\n", andRN: true, scroll: false, ignoreTags: true);
			int n2 = _c.zLineCount - 2;
			for (int i = n1; i < n2; i++) _c.Call(SCI_MARKERADD, i, 0);
			ac.Add((s, offset8));

			foreach (var m in s.RxFindAll(@"(?m)^using [\w\.]+;")) {
				(usings ??= new()).AppendLine(m.Value);
			}
		}

		//code styling
		if (ac != null) {
			code = string.Join("\r\n", ac.Select(o => o.code));
			Debug.Assert(code.IsAscii()); //never mind: does not support non-ASCII
			var b = new byte[code.Length];
			var document = CiUtil.CreateDocumentFromCode(code, needSemantic: true);
			var semo = document.GetSemanticModelAsync().Result;
			var a = Classifier.GetClassifiedSpansAsync(document, TextSpan.FromBounds(0, code.Length)).Result;
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
		string code = _usings + $"///<see cref='{s}'/>";
		var document = CiUtil.CreateDocumentFromCode(code, needSemantic: true);
		var syn = document.GetSyntaxRootAsync().Result;
		var node = syn.FindToken(code.Length - 3 - s.Length, true).Parent.FirstAncestorOrSelf<CrefSyntax>();
		if (node == null) return;
		var semo = document.GetSemanticModelAsync().Result;
		if (semo.GetSymbolInfo(node).GetAnySymbol() is ISymbol sym) {
			var url = CiUtil.GetSymbolHelpUrl(sym);
			if (url != null) run.itSafe(url);
		}
	}

	class _KScintilla : KScintilla {
		bool _zoomMenu;

		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			switch (msg) {
			case Api.WM_MOUSEWHEEL:
				if (keys.gui.getMod() == KMod.Ctrl && !_zoomMenu) {
					int zoom = Call(SCI_GETZOOM);
					timer.after(1, _ => { //after WndProc SCI_GETZOOM returns old value
						if (Call(SCI_GETZOOM) != zoom) {
							_zoomMenu = true;
							int i = popupMenu.showSimple("Save font size");
							_zoomMenu = false;
							if (i == 1) App.Settings.recipe_zoom = (sbyte)Call(SCI_GETZOOM);
						}
					});
				}
				break;
			}

			var R = base.WndProc(hwnd, msg, wParam, lParam, ref handled);

			return R;
		}
	}

#if DEBUG
	unsafe void _AutoRenderCurrentRecipeScript() {
		string prevText = null;
		SciCode prevDoc = null;
		App.Timer1sWhenVisible += () => {
			if (App.Model.WorkspaceName != "Cookbook") return;
			if (!this.IsVisible) return;
			var doc = Panels.Editor.ZActiveDoc;
			if (doc == null || !doc.ZFile.IsScript || doc.ZFile.Parent.Name == "-") return;
			string text = doc.zText;
			if (text == prevText) return;
			prevText = text;
			//print.it("update");

			int n1 = doc == prevDoc ? _c.Call(SCI_GETFIRSTVISIBLELINE) : 0;
			if (n1 > 0) _c.Hwnd.Send(Api.WM_SETREDRAW);
			_SetText(doc.ZFile.DisplayName, text);
			if (doc == prevDoc) {
				if (n1 > 0)
					//_c.Call(SCI_SETFIRSTVISIBLELINE, n1);
					timer.after(1, _ => {
						_c.Call(SCI_SETFIRSTVISIBLELINE, n1);
						_c.Hwnd.Send(Api.WM_SETREDRAW, 1);
						Api.RedrawWindow(_c.Hwnd, flags: Api.RDW_ERASE | Api.RDW_FRAME | Api.RDW_INVALIDATE);
					});
			} else {
				prevDoc = doc;
				Panels.Cookbook.AddToHistory(doc.ZFile.DisplayName);
			}
			//rejected: autoscroll. Even if works perfectly, often it is more annoying than useful.
		};
	}
#endif
}
