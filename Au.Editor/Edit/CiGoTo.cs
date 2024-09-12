//#define SG_SYMBOL //sourcegraph type:symbol. Almost unusable (2023-07-04). Does not find many types, cannot search for "member X in class Y", cannot combine with nonsymbol search, incorrect and possibly unstable select:symbol.

extern alias CAW;

using System.Net;
using System.Windows;
using System.Windows.Controls;
using Au.Controls;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using System.Text.RegularExpressions;

//FUTURE: try source link. Like now VS.

class CiGoTo {
	struct _SourceLocation {
		public _SourceLocation(string file, int line, int column) {
			this.file = file; this.line = line; this.column = column;
		}
		public string file;
		public int line, column;
	}
	
	bool _canGoTo, _inSource;
	//if in source
	List<_SourceLocation> _sourceLocations;
	//if in metadata
	string _assembly, _repo, _type, _kind, _prefix, _member, _namespace, _alt;
	int _flags;
	//1 with github can use symbol: with member; eg github supports only methods.
	//2 can use "public ... Member"; eg cannot for interface and enum.
	//4 with github can use symbol: with type; eg github supports only class and struct.
	
	/// <summary>
	/// true if can go to the symbol source. Then caller eg can add link to HTML.
	/// May return true even if can't go to. Then on link click nothing happens. Next time will return false.
	/// </summary>
	public bool CanGoTo => _canGoTo;
	
	CiGoTo(bool inSource) { _canGoTo = true; _inSource = inSource; }
	
	/// <summary>
	/// Gets info required to go to symbol source file/line/position or website.
	/// This function is fast. The slower code is in the <b>GoTo</b> functions.
	/// </summary>
	public CiGoTo(ISymbol sym) {
		if (_inSource = sym.IsFromSource()) {
			_sourceLocations = new();
			foreach (var loc in sym.Locations) {
				Debug_.PrintIf(!loc.IsVisibleSourceLocation());
				//if (!loc.IsVisibleSourceLocation()) continue;
				
				var v = loc.GetLineSpan();
				_sourceLocations.Add(new _SourceLocation(v.Path, v.StartLinePosition.Line, v.StartLinePosition.Character));
			}
			_canGoTo = _sourceLocations.Count > 0;
		} else {
			var asm = sym.ContainingAssembly; if (asm == null) return;
			if (sym is INamespaceSymbol) return; //not useful
			_canGoTo = true;
			_assembly = asm.Name;
			
			if (asm.GetAttributes().FirstOrDefault(o => o.AttributeClass.Name == "AssemblyMetadataAttribute" && "RepositoryUrl" == o.ConstructorArguments[0].Value as string)?.ConstructorArguments[1].Value is string s && s.Length > 0) {
				if (s.Starts("git:")) s = s.ReplaceAt(0, 3, "https"); //eg .NET
				if (s.Starts("https://github.com/")) _repo = s.Ends(".git") ? s[19..^4] : s[19..];
				Debug_.PrintIf(_repo.NE(), s);
			}
			
			//Unfortunately the github search engine is so bad. Gives lots of garbage. Randomly returns not all results.
			//To remove some garbage, can include namespace, filename, path (can be partial, without filename).
			//There is no best way for all casses. GoTo() will show UI, and users can try several alternatives.
			//Also tried github API. Can get search results, but not always can find the match in the garbage.
			//	The returned code snippets are small and often don't contain the search words. Can download entire file, but it's too slow and dirty.
			//	The test code is in some script. Tested octokit too, but don't need it, it's just wraps the REST API, which is easy to use.
			//At first this class used referencesource, not github. Can jump directly to the class or method etc.
			//	But it seems it is now almost dead. Many API either don't exist or are obsolete (I guess) or only Unix version.
			//	And it was only for .NET and Roslyn.
			//2023-07-02: GitHub search improved. Supports regex and partially symbol: (symbol definitions). It seems always shows all results.
			//	Tested: Supports symbol: for: class, interface, method.
			//		Not for: property, event, delegate, enum, struct. For namespace only if NS{} but not if NS.NS{} or NS;.
			
			if (sym is not INamedTypeSymbol ts) {
				ts = sym.ContainingType;
				_member = sym.Name;
				if (_member.Starts('.')) _member = null; //".ctor"
				else {
					if (sym is IMethodSymbol ims && ims.MethodKind is MethodKind.Ordinary or MethodKind.ReducedExtension) _flags |= 1;
					if (ts.TypeKind is TypeKind.Class or TypeKind.Struct) _flags |= 2;
				}
			}
			ts = ts.OriginalDefinition; //eg List<int> -> List<T>
			
			_type = ts.Name; //get name like Int32 or List. GetShortName gets eg int, but we need Int32.
			if (ts.TypeKind is TypeKind.Class or TypeKind.Interface && !ts.IsRecord) _flags |= 4;
			
#if SG_SYMBOL
			if (sym is INamedTypeSymbol) {
				_kind = ts.TypeKind switch {
					TypeKind.Class => "class",
					TypeKind.Struct => "struct",
					TypeKind.Enum => "enum",
					TypeKind.Interface => "interface",
					TypeKind.Delegate => "method",
					_ => null
				};
			} else {
				_kind = sym.Kind switch {
					SymbolKind.Event => "event",
					SymbolKind.Field => "variable",
					SymbolKind.Method => "method",
					SymbolKind.Property => "property",
					_ => null
				};
				Debug_.PrintIf(_kind == null, sym.Kind);
			}
#endif
			
			_prefix = ts.TypeKind switch {
				TypeKind.Class => ts.IsRecord ? "(record class|record)" : "class",
				TypeKind.Struct => "struct",
				TypeKind.Enum => "enum",
				TypeKind.Interface => "interface",
				//TypeKind.Delegate => "delegate " + ts.DelegateInvokeMethod?.ReturnType.GetShortName(), //never mind: can be generic or qualified. Rare.
				TypeKind.Delegate => "delegate .+?",
				_ => null
			};
			
			_namespace = ts.ContainingNamespace.QualifiedName();
			
			//for source.dot.net need exact generic, preferably fully qualified, like Namespace.List<T>
			_alt = ts.ToDisplayString(s_sdnSymbolFormat);
		}
	}
	
	static SymbolDisplayFormat s_sdnSymbolFormat = new(
		   globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
		   typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		   genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
	
	string _GetLinkData() {
		if (!_canGoTo) return null;
		if (_inSource) {
			var b = new StringBuilder();
			foreach (var v in _sourceLocations) b.AppendFormat("|{0}?{1},{2}", v.file, v.line, v.column);
			return b.ToString();
		} else {
			return $"|\a{_assembly}\a{_repo}\a{_type}\a{_member}\a{_namespace}\a{_kind}\a{_prefix}\a{_alt}\a{_flags}";
		}
	}
	
	/// <summary>
	/// Gets link data string for <see cref="LinkGoTo"/>. Returns null if unavailable.
	/// This function is fast. The slower code is in the <b>GoTo</b> function.
	/// </summary>
	public static string GetLinkData(ISymbol sym) => new CiGoTo(sym)._GetLinkData();
	
	/// <summary>
	/// Opens symbol source file/line/position or shows github search UI. Used on link click.
	/// </summary>
	/// <param name="linkData">String returned by <see cref="GetLinkData"/>.</param>
	public static void LinkGoTo(string linkData) {
		if (linkData == null) return;
		bool inSource = !linkData.Starts("|\a");
		var g = new CiGoTo(inSource);
		if (inSource) {
			var a = linkData.Split('|');
			g._sourceLocations = new List<_SourceLocation>();
			for (int i = 1; i < a.Length; i++) {
				var s = a[i];
				int line = s.LastIndexOf('?'), column = s.LastIndexOf(',');
				g._sourceLocations.Add(new _SourceLocation(s.Remove(line), s.ToInt(line + 1), s.ToInt(column + 1)));
			}
		} else {
			var a = linkData.Split('\a');
			g._assembly = a[1];
			g._repo = a[2];
			g._type = a[3];
			g._member = a[4];
			g._namespace = a[5];
			g._kind = a[6];
			g._prefix = a[7];
			g._alt = a[8];
			g._flags = a[9].ToInt();
		}
		g.GoTo();
	}
	
	/// <summary>
	/// Opens symbol source file/line/position or website.
	/// If need to open website, runs async task.
	/// </summary>
	public void GoTo() {
		if (!_canGoTo) return;
		if (_inSource) {
			if (_sourceLocations.Count == 1) {
				_GoTo(_sourceLocations[0]);
			} else {
				int i = popupMenu.showSimple(_sourceLocations.Select(v => v.file + ", line " + v.line.ToString()).ToArray());
				if (i > 0) _GoTo(_sourceLocations[i - 1]);
			}
			
			static void _GoTo(_SourceLocation v) => App.Model.OpenAndGoTo(v.file, v.line, v.column);
		} else {
			_CodeSearchDialog();
		}
	}
	
	void _CodeSearchDialog() {
		AssemblySett asm = new(), asmSaved = null;
		App.Settings.ci_gotoAsm?.TryGetValue(_assembly, out asmSaved);
		asmSaved ??= new();
		asmSaved.repo ??= _repo;
		
		var bd = new wpfBuilder("Source code search").WinSize(450).Columns(-1);
		bd.Window.UseLayoutRounding = true; //workaround for: text of OK etc buttons not centered vertically
		bd.Row(0).Add(out TabControl tc);
		
		wpfBuilder _Page(string name, WBPanelType panelType = WBPanelType.Grid) {
			var tp = new TabItem { Header = name };
			tc.Items.Add(tp);
			return new wpfBuilder(tp, panelType).Options(bindLabelVisibility: true);
		}
		
		bd.R.StartGrid<KGroupBox>("Repository info of this assembly").Columns(70, -1);
		bd.R.Add("Assembly", out Label _, _assembly).And(50).Add(out KCheckBox cSharp, "C#").Checked(asmSaved.csharp);
		bd.R.Add("Repository", out TextBox tRepo, asmSaved.repo)
			.Tooltip("Repository name, like owner/repo.\nIt's the part of the github URL.");
		bd.R.Add("Path", out TextBox tPath, asmSaved.path).Tooltip("File paths must match this regex.\nExample: src/libraries");
		bd.R.Add("Context", out TextBox tContext, asmSaved.context).Tooltip(@"Let the sourcegraph query string start with this. Not used with github. Default: context:global repo:^github\.com/");
		bd.End();
		
		bd.AddOkCancel(apply: "Search");
		bd.End();
		
		bd.OkApply += _ => {
			asm.repo = tRepo.TextOrNull();
			asm.path = tPath.TextOrNull();
			asm.context = tContext.TextOrNull();
			asm.csharp = cSharp.IsChecked;
			if (asm.repo == _repo && asm.path == null && asm.context == null && asm.csharp) {
				//print.it("remove");
				App.Settings.ci_gotoAsm?.Remove(_assembly);
			} else if (asm != asmSaved) {
				//print.it("save");
				(App.Settings.ci_gotoAsm ??= new())[_assembly] = new() { repo = asm.repo == _repo ? null : asm.repo, path = asm.path, context = asm.context, csharp = asm.csharp };
				asmSaved = asm with { };
			}
			
			App.Settings.ci_gotoTab = tc.SelectedIndex;
		};
		
#if SG_SYMBOL
		const int c_sgSymbol = 0;
		const int c_sgCode = 1;
		const int c_github = 2;
#else
		const int c_sgCode = 0;
		const int c_github = 1;
#endif
		
		if (App.Settings.ci_gotoTab is int itab && itab > 0 && itab <= c_github) tc.SelectedIndex = itab;
		
#if SG_SYMBOL
		_SourcegraphSymbol();
#endif
		_Sourcegraph();
		_Github();
		_Sourcedotnet();
		
		bd.Window.Topmost = true;
		bd.WinSaved(s_wndpos, o => s_wndpos = o);
		bd.Window.Show();
		
#if SG_SYMBOL //note: some parts of this code may be obsolete or little tested
		void _SourcegraphSymbol() {
			var b = _Page("soucegraph symbol");
			
			_AddInfo(b, "Creates and opens a sourcegraph.com symbol search URL.  [", new WBLink("syntax", "https://docs.sourcegraph.com/code_search/reference/queries"), "]");
			
			b.R.Add("Symbol", out TextBox tSymbol, $@"\b{_member.NE() ? _type : _member)}\b");
			TextBox tType = null; KCheckBox cType = null;
			if (!_member.NE()) {
				b.R.Add(out cType, "Type", out tType, $@"\b{_type}\b");
				cType.IsChecked = true;
			}
			b.R.Add(out KCheckBox cKind, "Kind", out TextBox tKind, _kind); cKind.IsChecked = _kind != null;
			b.R.Add(out KCheckBox cText, "Text", out TextBox tText, $@"\bnamespace {Regex.Escape(_namespace)}\b").Tooltip("The file must also contain this text or match this /regex/"); ;
			b.R.Add(out KCheckBox cFile, "File", out TextBox tFile, _type).Tooltip("The file name or path must contain this text or match this /regex/");
			b.R.Add(out KCheckBox cAlso, "Also", out TextBox tAlso, $"NOT file:{_NotPath()}").Tooltip("Append this to the query");
			cAlso.IsChecked = true;
			
			b.End();
			
			bd.OkApply += _ => {
				if (tc.SelectedIndex != c_sgSymbol) return;
				var q = new StringBuilder(@"patterntype:regexp case:yes context:global repo:^github\.com/");
				if (!asm.repo.NE()) q.Append(Regex.Escape(asm.repo)).Append('$');
				if (asm.csharp) q.Append(" lang:C#");
				q.Append(" type:symbol ").Append(tSymbol.Text);
				if (cType?.IsChecked == true && !tType.Text.NE()) q.Append(" AND ").Append(tType.Text);
				if (cText.IsChecked && !tText.Text.NE()) q.Append(" AND ").Append(tText.Text);
				if (cKind.IsChecked && !tKind.Text.NE()) q.Append(" select:symbol." + tKind.Text);
				if (cFile.IsChecked && !tFile.Text.NE()) q.Append(" file:").Append(tFile.Text);
				if (asm.path != null) q.Append(" file:").Append(asm.path);
				if (cAlso.IsChecked && !tAlso.Text.NE()) q.Append(' ').Append(tAlso.Text);
				//print.it(q);
				var url = $"https://sourcegraph.com/search?q={WebUtility.UrlEncode(q.ToString())}";
				run.itSafe(url);
			};
		}
#endif
		
		void _Sourcegraph() {
			var b = _Page("sourcegraph");
			
			b.xAddInfoBlockF($"Creates and opens a sourcegraph.com search URL.  [<a href='https://docs.sourcegraph.com/code_search/reference/queries'>syntax</a>]");
			
			b.R.Add("Type", out TextBox tType, $@"\b{_prefix}\s+{_type}\b"); //note: don't use unescaped space. Then splits into too: "\b{_prefix}" and "{_type}\b"
			b.R.Add("Member", out TextBox tMember);
			if (_member.NE()) b.Hidden(); else tMember.Text = 0 != (_flags & 2) ? $@"\bpublic\s.+?\s{_member}\b" : $@"\b{_member}\b";
			b.R.Add(out KCheckBox cText, "Text").Add(out TextBox tText, $@"\bnamespace\s+{Regex.Escape(_namespace)}\b").LabeledBy().Tooltip("The code must also match this regex"); ;
			//b.R.Add(out KCheckBox cText, "Text", out TextBox tText, $@"\bnamespace\s+{Regex.Escape(_namespace)}\b").Tooltip("The code must also match this regex"); ;
			b.R.Add(out KCheckBox cFile, "File").Add(out TextBox tFile, _type).LabeledBy().Tooltip("The file name or path must match this regex");
			b.R.Add(out KCheckBox cAlso, "Also").Add(out TextBox tAlso, $"NOT file:{_NotPath()}").LabeledBy().Tooltip("Append this to the query");
			cAlso.IsChecked = true;
			
			b.End();
			
			bd.OkApply += _ => {
				if (tc.SelectedIndex != c_sgCode) return;
				var q = new StringBuilder("patterntype:regexp case:yes "); //note: case:yes even if !asm.csharp
				q.Append(asm.context ?? @"context:global repo:^github\.com/");
				if (!asm.repo.NE()) q.Append(Regex.Escape(asm.repo)).Append('$');
				if (asm.csharp) q.Append(" lang:C#");
				q.Append(' ');
				if (!tMember.Text.NE()) {
					q.Append(tMember.Text);
					if (!tType.Text.NE()) q.Append($" file:has.content({tType.Text})");
					if (cText.IsChecked && !tText.Text.NE()) q.Append($" file:has.content({tText.Text})");
				} else {
					q.Append(tType.Text);
				}
				if (cFile.IsChecked && !tFile.Text.NE()) q.Append(" file:").Append(_RxEscapeSpaces(tFile.Text));
				if (asm.path != null) q.Append(" file:").Append(_RxEscapeSpaces(asm.path));
				if (cAlso.IsChecked && !tAlso.Text.NE()) q.Append(' ').Append(tAlso.Text);
				//print.it(q);
				var url = $"https://sourcegraph.com/search?q={WebUtility.UrlEncode(q.ToString())}";
				run.itSafe(url);
				
				static string _RxEscapeSpaces(string s) => s.RxReplace(@"(?<!\\) ", @"\ ");
			};
		}
		
		void _Github() {
			var b = _Page("github");
			
			Action notes = () => dialog.show("GitHub code search notes", "Results may be incomplete. Try to reload the page.\nSkips large source files.", owner: bd.Window);
			b.xAddInfoBlockF($"Creates and opens a github.com search URL.  [<a href='https://docs.github.com/en/search-github/github-code-search/understanding-github-code-search-syntax'>syntax</a>]  [<a {notes}>notes</a>]");
			
			b.R.Add("Type", out TextBox tType, 0 != (_flags & 4) && _member.NE() ? $@"symbol:/(?-i)\b{_type}\b/ /(?-i)\b{_prefix} {_type}\b/" : $@"/(?-i)\b{_prefix} {_type}\b/");
			b.R.Add("Member", out TextBox tMember).Hidden(_member.NE());
			if (!_member.NE()) tMember.Text = 0 != (_flags & 1) ? $@"symbol:/(?-i)\b{_member}\b/"
											: 0 != (_flags & 2) ? $@"/(?-i)\bpublic .+? {_member}\b/" //member of class or struct
											: $@"/(?-i)\b{_member}\b/"; //member of interface or enum
			b.R.Add(out KCheckBox cText, "Text").Add(out TextBox tText, $"\"namespace {_namespace}\"").LabeledBy().Tooltip("Append this to the query");
			b.R.Add(out KCheckBox cFile, "File").Add(out TextBox tFile, _type).LabeledBy().Tooltip("The file name or path must contain this text or match /regex/");
			b.R.Add(out KCheckBox cAlso, "Also").Add(out TextBox tAlso, $"NOT path:/{_NotPath()}/").LabeledBy().Tooltip("Append this to the query");
			cAlso.IsChecked = true;
			
			b.End();
			
			bd.OkApply += _ => {
				if (tc.SelectedIndex != c_github) return;
				var q = new StringBuilder();
				if (!asm.repo.NE()) q.Append($"repo:{asm.repo} ");
				if (asm.csharp) q.Append("lang:C# ");
				if (!tMember.Text.NE()) q.Append(tMember.Text).Append(' '); //must be before type for better results
				q.Append(tType.Text);
				if (cText.IsChecked && !tText.Text.NE()) q.Append(' ').Append(tText.Text);
				if (cFile.IsChecked && !tFile.Text.NE()) q.Append($" path:{tFile.Text}");
				if (asm.path != null) q.Append($" path:/{asm.path.RxReplace(@"(?<!\\)/", @"\/")}/"); //tested: works well when cFile specified too (both use path:)
				if (cAlso.IsChecked && !tAlso.Text.NE()) q.Append(' ').Append(tAlso.Text);
				//print.it(q);
				var url = $"https://github.com/search?q={WebUtility.UrlEncode(q.ToString())}";
				//print.it(url);
				run.itSafe(url);
			};
		}
		
		void _Sourcedotnet() {
			var tp = new TabItem { Header = "source.dot.net" };
			tc.Items.Add(tp);
			tc.SelectionChanged += (_, e) => {
				if (tc.SelectedIndex == tc.Items.Count - 1) {
					var s = "https://source.dot.net/#q=" + _alt;
					if (_member != null) s = s + "." + _member;
					run.itSafe(s);
					bd.Window.Close();
				}
			};
		}
		
		string _NotPath() => @"\b[Tt]est|\b[Gg]enerat|\b[Uu]nix\b|\/ref\/" + (_repo == "dotnet/wpf" ? @"|\/cycle-breakers\/" : null); //note: sourcegraph does not support (?i) and (?-i)
	}
	static string s_wndpos;
	
	internal record AssemblySett {
		public string repo, path, context;
		public bool csharp;
		public AssemblySett() { csharp = true; }
	}
	
	//This was used with referencesource. It seems don't need it now.
	///// <summary>
	///// If _filename or its ancestor type is forwarded to another assembly, replaces _assembly with the name of that assembly.
	///// For example initially we get that String is in System.Runtime. But actually it is in System.Private.CoreLib, and its name must be passed to https://source.dot.net.
	///// Speed: usually < 10 ms.
	///// </summary>
	//void _GetAssemblyNameOfForwardedType() {
	//	var path = folders.NetRuntimeBS + _assembly + ".dll";
	//	if (!(filesystem.exists(path).File || filesystem.exists(path = folders.NetRuntimeDesktopBS + _assembly + ".dll").File)) return;
	
	//	var alc = new System.Runtime.Loader.AssemblyLoadContext(null, true);
	//	try {
	//		var asm = alc.LoadFromAssemblyPath(path);
	//		var ft = asm.GetForwardedTypes()?.FirstOrDefault(ty => ty.FullName == _filename);
	//		if (ft != null) _assembly = ft.Assembly.GetName().Name;
	//	}
	//	catch (Exception ex) { Debug_.Print(ex); }
	//	finally { alc.Unload(); }
	//}
	
	public static void GoToDefinition() {
		if (!CodeInfo.GetContextAndDocument(out var cd, metaToo: true)) return;
		var (sym, _, helpKind, token) = CiUtil.GetSymbolEtcFromPos(cd);
		if (sym != null) {
			if (sym is IParameterSymbol or ITypeParameterSymbol && !sym.IsInSource()) return;
			if (_GetFoldersPath(token, out var fp, sym)) {
				run.itSafe(fp);
			} else {
				var g = new CiGoTo(sym);
				if (g.CanGoTo) g.GoTo();
			}
		} else if (cd.sci.aaaHasSelection) {
			_Open(cd.sci.aaaSelectedText());
		} else if (helpKind == CiUtil.HelpKind.String && token.Parent.IsKind(SyntaxKind.StringLiteralExpression)) {
			_Open(token.ValueText, token);
		} else if (helpKind == CiUtil.HelpKind.None && cd != null) { //maybe path in comments or disabled code
			int pos = cd.pos;
			if (pos < cd.meta.end && pos > cd.meta.start) {
				foreach (var t in Au.Compiler.MetaComments.EnumOptions(cd.code, cd.meta)) {
					if (pos >= t.valueStart && pos <= t.valueEnd) {
						_Open(cd.code[t.valueStart..t.valueEnd]);
						break;
					}
				}
			} else {
				var root = cd.syntaxRoot;
				var trivia = root.FindTrivia(pos); if (trivia.RawKind == 0) return;
				var code = cd.code;
				var span = trivia.Span;
				int from = pos, to = from;
				static bool _IsPathOrUrlChar(char c) => !pathname.isInvalidPathChar(c) || c == '?';
				while (to < span.End && _IsPathOrUrlChar(code[to])) to++;
				while (from > span.Start && _IsPathOrUrlChar(code[from - 1])) from--;
				while (to > pos && code[to - 1] is <= ' ' or '.' or ';' or ',') to--; //trim right
				while (from < pos && code[from] <= ' ') from++; //trim left
				if (to == from) return;
				//rejected: try to find path even if without "". How to know which text part it is? Better let the user select the text.
				if (!(code.Eq(from - 1, '"') && code.Eq(to, '"'))) return;
				_Open(code[from..to]);
			}
		}
		
		//if s is script, opens it. If file path, selects in Explorer. If folder path, opens the folder. If URL, opens the web page.
		static bool _Open(string s, SyntaxToken token = default) {
			if (s.NE()) return false;
			if (pathname.isUrl(s)) {
				if (0 == s.Starts(false, "http:", "https:")) return false;
				run.itSafe(s);
			} else {
				if (!pathname.isFullPathExpand(ref s, strict: false)) {
					var f = App.Model.Find(s);
					if (f != null) { App.Model.SetCurrentFile(f); return true; }
					if (token.RawKind == 0 || !_GetFoldersPath(token, out var fp, null)) return false;
					s = pathname.combine(fp, s);
				}
				var fe = filesystem.exists(s); if (!fe) return false;
				if (fe.Directory) run.itSafe(s);
				else run.selectInExplorer(s);
			}
			return true;
		}
		
		//If t is "string" in 'folders.Folder + "string"' or Folder in 'folders.Folder', gets folder path and return true.
		static bool _GetFoldersPath(SyntaxToken t, out string s, ISymbol sym) {
			s = null;
			bool isString = sym is null;
			if (isString) {
				t = t.GetPreviousToken(); if (!t.IsKind(SyntaxKind.PlusToken)) return false;
				t = t.GetPreviousToken();
			}
			if (!t.IsKind(SyntaxKind.IdentifierToken)) return false;
			var s2 = t.Text;
			t = t.GetPreviousToken(); if (!t.IsKind(SyntaxKind.DotToken)) return false;
			t = t.GetPreviousToken(); if (!t.IsKind(SyntaxKind.IdentifierToken) || t.Text != "folders") return false;
			s = folders.getFolder(s2);
			if (s is null) return false;
			if (true == sym?.IsInSource()) {
				return 2 == popupMenu.showSimple("1 Go to definition|2 Open folder");
			}
			return true;
		}
	}
	
	public static void GoToBase() {
		var (sym, cd) = CiUtil.GetSymbolFromPos(andZeroLength: true);
		if (sym is null) return;
		List<ISymbol> a = new();
		if (sym is INamedTypeSymbol nt && nt.TypeKind is TypeKind.Class or TypeKind.Interface) {
			if (nt.TypeKind == TypeKind.Interface) {
				a.AddRange(nt.AllInterfaces);
			} else if (nt.BaseType != null) { //else object
				a.AddRange(nt.GetBaseTypes().SkipLast(1).Concat(nt.AllInterfaces));
			}
		} else if (sym.Kind is SymbolKind.Method or SymbolKind.Property or SymbolKind.Event) {
			a.AddRange(CAW::Microsoft.CodeAnalysis.FindSymbols.FindReferences.BaseTypeFinder.FindOverriddenAndImplementedMembers(sym, cd.document.Project.Solution, default));
		}
		if (a.Count == 0) return;
		if (a.Count == 1) sym = a[0];
		else {
			int i = popupMenu.showSimple(a.Select(o => o.Name).ToArray());
			if (--i < 0) return;
			sym = a[i];
		}
		var g = new CiGoTo(sym);
		if (g.CanGoTo) g.GoTo();
	}
}
