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
	KScintilla_ _c;
	string _usings;
	string _currentRecipeName;
	
	public KScintilla_ Scintilla => _c;
	
	public PanelRecipe() {
		//P.UiaSetName("Recipe panel"); //no UIA element for Panel
		
		_c = new KScintilla_(this) {
			Name = "Recipe_text",
			AaInitReadOnlyAlways = true,
			AaInitTagsStyle = KScintilla.AaTagsStyle.User
		};
		P.Children.Add(_c);
	}
	
	public DockPanel P { get; } = new();
	
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
			s = s.RxReplace(@"<see cref=['""](.+?)['""]/>", static m => {
				var v = m[1].Value;
				var t = v; if (t.Contains('{')) t = "<_>" + t.Replace('{', '<').Replace('}', '>') + "</_>";
				return $"<+see '{v}'>{t}<>";
			});
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
	
	public class OpeningRecipeArgs {
		/// <summary>
		/// Recipe name.
		/// </summary>
		public string name;
		
		/// <summary>
		/// Recipe text, split into text and code parts.
		/// Text parts contain output tags. Standard tags are documented in LA help. Custom tag examples: <see href="https://github.com/qgindi/LibreAutomate/tree/master/_/Cookbook/files"/>
		/// When translating text, don't change tags. For example, at first find and save tags, replace them with unique indexed untranslatable strings, and finally replace these strings with the saved tags.
		/// </summary>
		public List<(bool isText, string s)> parts;
	}
	
	/// <summary>
	/// Editor extensions can use this to modify cookbook recipe text, for example translate to another language.
	/// The callback function is called when opening a recipe in this panel. It receives recipe name and text, and can change them.
	/// </summary>
	public Action<OpeningRecipeArgs> OpeningRecipe { get; set; }
	
	void _SetText(string name, string code) {
		_currentRecipeName = name;
		_c.aaaClearText();
		
		var parts = ParseRecipe(code, out _usings);
		
		if (OpeningRecipe != null) {
			var k = new OpeningRecipeArgs() { name = name, parts = parts };
			try {
				OpeningRecipe(k);
				name = k.name;
				parts = k.parts;
			}
			catch (Exception e1) { print.it(e1); }
		}
		
		if (!name.NE() && !code.Starts("/// <lc")) _c.AaTags.AddText($"<lc YellowGreen><b>{name}</b><>\r\n\r\n", false, false, false);
		
		var ac = new List<(string code, int offset8, int len8)>();
		int ipart = -1;
		foreach (var (isText, s) in parts) {
			ipart++;
			if (isText) {
				_c.AaTags.AddText((ipart == 0 ? null : "\r\n") + s, true, false, false);
			} else {
				int n1 = _c.aaaLineCount, offset8 = _c.aaaLen8 + 2;
				var s8 = Encoding.UTF8.GetBytes("\r\n" + s + "\r\n");
				_c.aaaAppendText8(s8, scroll: false);
				int n2 = _c.aaaLineCount - 1;
				for (int i = n1; i < n2; i++) _c.Call(SCI_MARKERADDSET, i, 3);
				ac.Add((s, offset8, s8.Length - 4));
			}
		}
		
		//code styling
		if (ac != null) {
			code = string.Join("\r\n", ac.Select(o => o.code));
			var styles8 = CiUtil.GetScintillaStylingBytes8(code);
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
		var syn = document.GetSyntaxRootSynchronously(default);
		var node = syn.FindToken(code.Length - 3 - s.Length, true).Parent.FirstAncestorOrSelf<CrefSyntax>();
		if (node != null) {
			var semo = document.GetSemanticModelAsync().Result;
			if (semo.GetSymbolInfo(node).GetAnySymbol() is ISymbol sym)
				return CiUtil.GetSymbolHelpUrl(sym);
		}
		return null;
	}
	
	internal class KScintilla_ : KScintilla {
		PanelRecipe _panel;
		bool _zoomMenu;
		
		internal KScintilla_(PanelRecipe panel) {
			_panel = panel;
		}
		
		protected override void AaOnHandleCreated() {
			Call(SCI_SETWRAPMODE, SC_WRAP_WORD);
			aaaMarginSetWidth(1, 14);
			aaaMarkerDefine(0, SC_MARK_FULLRECT, backColor: 0xB0E0A0);
			aaaMarkerDefine(1, SC_MARK_BACKGROUND, backColor: 0xF0F0F0);
			_SetFont(false);
			Call(SCI_SETZOOM, App.Settings.recipe_zoom);
			
			AaTags.AddLinkTag("+see", s => { s = GetSeeUrl(s, _panel._usings); if (s != null) run.itSafe(s); });
			//aaTags.AddLinkTag("+lang", s => run.itSafe("https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/" + s)); //unreliable, the URLs may change
			AaTags.AddLinkTag("+lang", s => run.itSafe(App.Settings.internetSearchUrl + System.Net.WebUtility.UrlEncode(s + ", C# reference")));
			//aaTags.AddLinkTag("+guide", s => run.itSafe("https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/" + s)); //rejected. Use <google>.
			AaTags.AddLinkTag("+ms", s => run.itSafe(App.Settings.internetSearchUrl + System.Net.WebUtility.UrlEncode(s + " site:microsoft.com")));
			AaTags.AddStyleTag(".c", new() { backColor = 0xF0F0F0, monospace = true }); //inline code
			AaTags.AddStyleTag(".x", new() { backColor = 0xF0F0F0, monospace = true, textColor = 0xE06060 }); //API name
			//AaTags.AddStyleTag(".x", new() { monospace = true, textColor = 0x808080, bold = true }); //API name
			AaTags.AddStyleTag(".k", new() { backColor = 0xF0F0F0, monospace = true, textColor = 0x0000FF }); //keyword
			
#if DEBUG
			_panel._AutoRenderCurrentRecipeScript();
#endif
			base.AaOnHandleCreated();
		}
		
		void _SetFont(bool change) {
			aaaStyleFont(STYLE_DEFAULT, App.Settings.font_recipeText.name, App.Settings.font_recipeText.size); //no-tags text font
			AaTags.FontForMonoTags = (App.Settings.font_recipeCode.name, App.Settings.font_recipeCode.size); //<mono> and custom tags with Mono style that don't set name/size
			
			if (change) {
				//code fonts
				for (int i = 0; i < Sci.STYLE_HIDDEN; i++) {
					aaaStyleFont(i, App.Settings.font_recipeCode.name, App.Settings.font_recipeCode.size);
				}
				//text tag fonts
				var ts = AaTags.TagStyles;
				for (int i = ts.first, j = 0; j < ts.a.Count; j++, i++) {
					var font = ts.a[j].Mono ? App.Settings.font_recipeCode : App.Settings.font_recipeText;
					aaaStyleFont(i, font.name, font.size);
				}
			} else {
				//aaaStyleFont(STYLE_DEFAULT); //Segoe UI, 9. Too narrow and looks too small when compared with the code font.
				//aaaStyleFont(STYLE_DEFAULT, "Segoe UI", 10); //too tall
				//aaaStyleFont(STYLE_DEFAULT, "Verdana", 9); //too wide
				//aaaStyleFont(STYLE_DEFAULT, "Tahoma", 9); //good
				//aaaStyleFont(STYLE_DEFAULT, "Calibri", 10.5); //perfect
				CiStyling.TTheme.Default.ToScintilla(this, multiFont: true, fontName: App.Settings.font_recipeCode.name, fontSize: App.Settings.font_recipeCode.size);
			}
			
			//workaround for: the default font Calibri looks best, but need size 10.5 instead of normal 9, and it increases line height
			if (App.Settings.font_recipeText.name.Eqi("Calibri") && App.Settings.font_recipeText.size >= 10) {
				Call(SCI_SETEXTRAASCENT, -1);
				Call(SCI_SETEXTRADESCENT, -1);
			} //and don't unset. After setting Calibri font, Scintilla later will count its height even if no styles with that font exist.
		}
		
		public void AaChangedFontSettings() {
			if (!AaWnd.Is0) _SetFont(true);
		}
		
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
			case Api.WM_CONTEXTMENU:
				_ContextMenu();
				break;
			}
			
			var R = base.WndProc(hwnd, msg, wParam, lParam, ref handled);
			
			return R;
		}
		
		void _ContextMenu() {
			var m = new popupMenu();
			m["Copy\tCtrl+C", disable: !aaaHasSelection] = o => { Call(Sci.SCI_COPY); };
			m["Copy this code", disable: !_CanGetCode] = o => { if (_GetCode() is string s) clipboard.text = s; };
			m["New script", disable: !_CanGetCode] = o => { if (_GetCode() is string s) App.Model.NewItem("Script.cs", null, _panel._currentRecipeName + ".cs", true, new(true, s)); };
			m.Separator();
			m["Open in web browser"] = o => { Panels.Cookbook.OpenRecipeInWebBrowser(_panel._currentRecipeName); };
			m.Show(owner: Handle);
		}
		
		string _GetCode() {
			if (!_CanGetCode) return null;
			int line = aaaLineFromPos(), firstLine = line, lastLine = line, nLines = aaaLineCount;
			while (firstLine > 0 && 0 != (1 & Call(SCI_MARKERGET, firstLine - 1))) firstLine--;
			while (lastLine < nLines - 1 && 0 != (1 & Call(SCI_MARKERGET, lastLine + 1))) lastLine++;
			return aaaRangeText(false, aaaLineStart(false, firstLine), aaaLineEnd(false, lastLine, withRN: true));
		}
		
		bool _CanGetCode => !aaaHasSelection && 0 != (1 & Call(SCI_MARKERGET, aaaLineFromPos()));
	}
	
#if DEBUG
	unsafe void _AutoRenderCurrentRecipeScript() {
		string prevText = null;
		SciCode prevDoc = null;
		
		App.Timer1sWhenVisible += () => {
			if (!P.IsVisible) return;
			if (App.Model.WorkspaceName != "Cookbook") {
				if (!(App.Model.WorkspaceName == "ok" && App.Model.CurrentFile is { IsExternal: true, IsScript: true } cf && cf.Ancestors().Any(o => o.Name is "cookbook_files"))) return;
			}
			var doc = Panels.Editor.ActiveDoc;
			if (doc == null || !doc.EFile.IsScript || doc.EFile.Parent.Name == "-") return;
			_Render(doc);
		};
		
		void _Render(SciCode doc) {
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
		}
	}
#endif
}
