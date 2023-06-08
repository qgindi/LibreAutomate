extern alias CAW;

using System.Windows.Controls;
using Au.Controls;
using static Au.Controls.Sci;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;

class PanelRecipe {
	_KScintilla _c;
	string _usings;

	//public KScintilla Scintilla => _c;

	public PanelRecipe() {
		//P.UiaSetName("Recipe panel"); //no UIA element for Panel

		_c = new _KScintilla {
			Name = "Recipe_text",
			AaInitReadOnlyAlways = true,
			AaInitTagsStyle = KScintilla.AaTagsStyle.User
		};
		_c.AaHandleCreated += _c_aaHandleCreated;

		P.Children.Add(_c);
	}

	public DockPanel P { get; } = new();

	private void _c_aaHandleCreated() {
		_c.Call(SCI_SETWRAPMODE, SC_WRAP_WORD);

		_c.aaaMarginSetWidth(1, 14);
		_c.Call(SCI_MARKERDEFINE, 0, SC_MARK_FULLRECT);
		_c.Call(SCI_MARKERSETBACK, 0, 0xA0E0B0);

		//_c.aaaStyleFont(STYLE_DEFAULT); //Segoe UI, 9. Too narrow and looks too small when compared with the code font.
		//_c.aaaStyleFont(STYLE_DEFAULT, "Segoe UI", 10); //too tall
		//_c.aaaStyleFont(STYLE_DEFAULT, "Verdana", 9); //too wide
		//_c.aaaStyleFont(STYLE_DEFAULT, "Calibri", 9); //too small
		_c.aaaStyleFont(STYLE_DEFAULT, "Tahoma", 9);
		var styles = new CiStyling.TStyles(customized: false) { FontSize = 9 };
		styles.ToScintilla(_c, multiFont: true);
		_c.Call(SCI_SETZOOM, App.Settings.recipe_zoom);

		_c.AaTags.AddLinkTag("+recipe", Panels.Cookbook.OpenRecipe);
		_c.AaTags.AddLinkTag("+see", s => { s = GetSeeUrl(s, _usings); if (s != null) run.itSafe(s); });
		//_c.aaTags.AddLinkTag("+lang", s => run.itSafe("https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/" + s)); //unreliable, the URLs may change
		_c.AaTags.AddLinkTag("+lang", s => run.itSafe("https://www.google.com/search?q=" + System.Net.WebUtility.UrlEncode(s + ", C# reference")));
		//_c.aaTags.AddLinkTag("+guide", s => run.itSafe("https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/" + s)); //rejected. Use <google>.
		_c.AaTags.AddLinkTag("+ms", s => run.itSafe("https://www.google.com/search?q=" + System.Net.WebUtility.UrlEncode(s + " site:microsoft.com")));
		_c.AaTags.AddLinkTag("+nuget", s => DNuget.ShowSingle(s));
		_c.AaTags.AddStyleTag(".k", new SciTags.UserDefinedStyle { textColor = 0x0000FF, bold = true }); //keyword

#if DEBUG
		_AutoRenderCurrentRecipeScript();
#endif
	}

	public void Display(string name, string code) {
		Panels.PanelManager[P].Visible = true;
		_SetText(name, code);
	}

	/// <summary>
	/// Splits a recipe source code into text and code parts.
	/// From text parts removes /// and replaces 'see' with '+see'.
	/// </summary>
	/// <param name="code">C# code.</param>
	/// <param name="usings">null or all using directives found in all codes.</param>
	public static List<(bool isText, string s)> ParseRecipe(string code, out string usings) {
		//rejected:
		//	1. Ignore code before the first ///. Not really useful, just forces to always start with ///.
		//	2. Use {  } for scopes of variables. Its' better to use unique names.
		//	3. Use if(...) {  } to enclose code examples to select which code to test.
		//		Can accidentally damage real 'if' code. I didn't use it; it's better to test codes in other script.

		List<(bool isText, string s)> r = new();
		StringBuilder sbUsings = null;
		var ac = new List<(string code, int offset8, int len8)>();
		int iCode = 0;
		foreach (var m in code.RxFindAll(@"(?ms)^(?:///(?!=/)\N*\R*)+|^/\*\*.+?\*/\R*")) {
			//print.it("--------");
			//print.it(m);
			if (code.Eq(m.Start, "/// <summary>")) continue;
			int textTo = m.End, i = code.Find("\n/// <summary>", m.Start..m.End); if (i >= 0) textTo = i + 1;

			_Code(iCode, m.Start);
			_Text(m.Start, iCode = textTo);
		}
		_Code(iCode, code.Length);
		usings = sbUsings?.ToString();
		return r;

		void _Text(int start, int end) {
			while (code[end - 1] <= ' ') end--;
			bool ml = code[start + 1] == '*';
			if (ml) {
				start += 3; while (code[start] <= ' ') start++;
				end -= 2; while (end > start && code[end - 1] <= ' ') end--;
			}
			var s = code[start..end];
			if (!ml) s = s.RxReplace(@"(?m)^/// ?", "");
			s = s.RxReplace(@"<see cref=['""](.+?)['""]/>", static m => { var v = m[1].Value; return $"<+see '{v}'>{v.Replace('{', '<').Replace('}', '>')}<>"; });
			//print.it("TEXT"); print.it(s);
			r.Add((true, s));
		}

		void _Code(int start, int end) {
			while (end > start && code[end - 1] <= ' ') end--;
			if (end == start) return;
			var s = code[start..end];
			//print.it("CODE"); print.it(s);
			r.Add((false, s));

			foreach (var m in s.RxFindAll(@"(?m)^using [\w\.]+;")) {
				(sbUsings ??= new()).AppendLine(m.Value);
			}
		}
	}

	void _SetText(string name, string code) {
		_c.aaaClearText();
		if (!name.NE() && !code.Starts("/// <lc")) code = $"/// <lc YellowGreen><b>{name}</b><>\r\n\r\n{code}";

		var ac = new List<(string code, int offset8, int len8)>();
		foreach (var (isText, s) in ParseRecipe(code, out _usings)) {
			if (isText) {
				_c.AaTags.AddText(s, true, false, false);
			} else {
				int n1 = _c.aaaLineCount, offset8 = _c.aaaLen8 + 2;
				var s8 = Encoding.UTF8.GetBytes("\r\n" + s + "\r\n\r\n");
				_c.aaaAppendText8(s8, scroll: false);
				int n2 = _c.aaaLineCount - 2;
				for (int i = n1; i < n2; i++) _c.Call(SCI_MARKERADD, i, 0);
				ac.Add((s, offset8, s8.Length - 6));
			}
		}

		//code styling
		if (ac != null) {
			code = string.Join("\r\n", ac.Select(o => o.code));
			var styles8 = CiUtil.GetScintillaStylingBytes(code);
			unsafe {
				fixed (byte* bp = styles8) {
					int bOffset = 0;
					foreach (var v in ac) {
						_c.Call(SCI_STARTSTYLING, v.offset8);
						_c.Call(SCI_SETSTYLINGEX, v.len8, bp + bOffset);
						bOffset += v.len8 + 2; //+2 for string.Join("\r\n"
					}
				}
			}
			_c.aaaSetStyled();
		}
	}

	public static string GetSeeUrl(string s, string usings) {
		//add same namespaces as in default global.cs. Don't include global.cs because it may be modified.
		string code = usings + $"///<see cref='{s}'/>";
		using var ws = new AdhocWorkspace();
		var document = CiUtil.CreateDocumentFromCode(ws, code, needSemantic: true);
		var syn = document.GetSyntaxRootAsync().Result;
		var node = syn.FindToken(code.Length - 3 - s.Length, true).Parent.FirstAncestorOrSelf<CrefSyntax>();
		if (node != null) {
			var semo = document.GetSemanticModelAsync().Result;
			if (semo.GetSymbolInfo(node).GetAnySymbol() is ISymbol sym)
				return CiUtil.GetSymbolHelpUrl(sym);
		}
		return null;
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
			if (!P.IsVisible) return;
			var doc = Panels.Editor.ActiveDoc;
			if (doc == null || !doc.EFile.IsScript || doc.EFile.Parent.Name == "-") return;
			string text = doc.aaaText;
			if (text == prevText) return;
			prevText = text;
			//print.it("update");

			int n1 = doc == prevDoc ? _c.Call(SCI_GETFIRSTVISIBLELINE) : 0;
			if (n1 > 0) _c.AaWnd.Send(Api.WM_SETREDRAW);
			_SetText(doc.EFile.DisplayName, text);
			if (doc == prevDoc) {
				if (n1 > 0)
					//_c.Call(SCI_SETFIRSTVISIBLELINE, n1);
					timer.after(1, _ => {
						_c.Call(SCI_SETFIRSTVISIBLELINE, n1);
						_c.AaWnd.Send(Api.WM_SETREDRAW, 1);
						Api.RedrawWindow(_c.AaWnd, flags: Api.RDW_ERASE | Api.RDW_FRAME | Api.RDW_INVALIDATE);
					});
			} else {
				prevDoc = doc;
				Panels.Cookbook.AddToHistory_(doc.EFile.DisplayName);
			}
			//rejected: autoscroll. Even if works perfectly, often it is more annoying than useful.
		};
	}
#endif
}
