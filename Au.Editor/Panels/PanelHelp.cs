using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using Au.Controls;
using System.Security.Authentication;
using System.Text.Json.Nodes;
using System.Net;
using System.Windows.Media;
using ToolLand;

//CONSIDER: Add a menu-button. Menu:
//	Item "Request a recipe for this search query (uses internet)".

namespace LA;

class PanelHelp {
	KTreeView _tv, _tvFavorites;
	KTextBox _search;
	_Item _root;
	_Item _selectedItem;
	List<_Item> _aiResults;
	bool _showingResults;
	bool _openingItem;
	internal (Button aiSearch, Button copyResults, ToolBarTray toolbar, Button back, Button forward, Button openInBrowser, Button toggleReadPanel) buttons_;
	
	public PanelHelp() {
		P = new();
		P.UiaSetName("Help panel");
		
		var b = new wpfBuilder(P).Columns(-1, 0, 2).Brush(SystemColors.ControlBrush);
		
		b.R.Add(out _search).Tooltip("Find documentation.\n\nMiddle-click to clear.").UiaName("Find documentation"); //rejected: watermark
		_search.TextChanged += (_, _) => _SearchInNameOnSearchTextChanged();
		_search.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) _AiSearch(); };
		
		b.Options(modifyPadding: false, margin: new());
		
		b.xAddButtonIcon(out buttons_.aiSearch, EdIcons.AiSearch, _ => _AiSearch(), "AI search\n\nEnter");
		b.And(0).xAddButtonIcon(out buttons_.copyResults, "*Material.ContentCopy" + EdIcons.black, _CopyResultsForAiChat, "Copy results for AI chat\n\nYou can paste it in ChatGPT, Gemini, etc.\nThen the AI can answer your question better.\nPaste it anywhere in your message.").Hidden(null);
		
		_tv = new() { Name = "Help_TOC", SingleClickActivate = true, HotTrack = true, BackgroundColor = 0xf0f8e8, SmallIndent = true };
		b.Row(-1).Add(_tv).Span(-1);
		_tvFavorites = new() { Name = "Help_Favorites", SingleClickActivate = true, HotTrack = true, BackgroundColor = 0xf0f8e8, SmallIndent = true };
		b.And(0).Add(_tvFavorites).Hidden();
		
		var tb = b.R.xAddToolBar(hideOverflow: true);
		buttons_.toolbar = tb.Parent as ToolBarTray;
		buttons_.toolbar.Visibility = Visibility.Collapsed; //will add buttons and show when creating web browser control
		tb.UiaSetName("Help_navigation_toolbar");
		
		b.End();
		
		Panels.PanelManager["Help"].DontActivateFloating = e => e is KTreeView;
		
		P.IsVisibleChanged += (_, e) => {
			if ((bool)e.NewValue && _root == null) {
				_Load();
				_MouseRM(_tv);
				_MouseRM(_tvFavorites);
				
				void _MouseRM(KTreeView tv) {
					tv.ItemActivated += e => _OpenItem(e.Item as _Item, false);
					tv.ItemClick += e => {
						if (e.Button == MouseButton.Right) _ContextMenu(e.Item as _Item);
						if (e.Button == MouseButton.Middle) _FavoritesToggle();
					};
					tv.RightClickInEmptySpace += () => _ContextMenu(null);
					tv.MiddleClickInEmptySpace += () => _FavoritesToggle();
				}
			}
		};
	}
	
	public UserControl P { get; }
	
	void _Load() {
		try {
			_root = new _Item(true, null, _DocKind.Folder);
			_TOC(folders.ThisAppBS + "toc.json", _root);
			
			var cookbookRoot = _root.FirstChild;
			var first = cookbookRoot.FirstChild;
			first.Remove();
			_root.AddChild(first, first: true);
			
			cookbookRoot.isExpanded = true;
			cookbookRoot.Next.isExpanded = true; //API
			
			static void _TOC(string jsonFile, _Item root) {
				var json = filesystem.loadText(jsonFile);
				var jRoot = JsonNode.Parse(json).AsArray();
				foreach (JsonObject j in jRoot) {
					var docKind = (string)j["name"] switch { "Cookbook" => _DocKind.Cookbook, "Articles" => _DocKind.Article, "Editor" => _DocKind.Editor, _ => _DocKind.Api };
					_Add(j, root, docKind);
				}
				
				static void _Add(JsonNode j, _Item ip, _DocKind docKind) {
					string name = (string)j["name"], href = (string)j["href"], symKind = null;
					if (docKind == _DocKind.Api) symKind = (string)j["kind"];
					if (j["items"] is JsonArray { Count: > 0 } ja) {
						var i = new _Item(true, name, docKind == _DocKind.Api && href != null ? docKind : _DocKind.Folder, symKind, href);
						ip.AddChild(i);
						foreach (var v in ja) {
							_Add(v, i, docKind);
						}
					} else {
						ip.AddChild(new _Item(false, name, docKind, symKind, href));
					}
				}
			}
			
			_tv.SetItems(_root.Children());
		}
		catch (Exception e1) { print.it(e1); }
		
		//_Test();
	}
	
	void _OpenItem(_Item item, bool select) {
		if (item == null) return;
		if (select && _favoritesView) _FavoritesToggle();
		
		if (!_favoritesView) {
			if (item.dir) {
				if (item.href == null) {
					_tv.Expand(item, null);
					return;
				} else if (!item.isExpanded) {
					_tv.Expand(item, true);
				}
			}
			
			if (_showingResults) {
				_selectedItem = item.clonedFrom;
			} else {
				if (select) {
					_openingItem = true;
					_search.Text = "";
					_openingItem = false;
					_tv.Select(item);
				}
				
				_selectedItem = item;
			}
		}
		
		var s1 = item.docKind switch { _DocKind.Cookbook => "cookbook/", _DocKind.Editor => "editor/", _DocKind.Article => "articles/", _ => "api/" };
		var href = item.docKind == _DocKind.Cookbook ? Uri.EscapeDataString(item.name) + ".html" : item.href;
		HelpUtil.AuHelp($"{s1}{href}"); //opens in the Read panel or in the web browser, depending on user settings
	}
	
	void _SearchInNameOnSearchTextChanged() {
		if (_favoritesView) _FavoritesToggle();
		
		if (_aiResults != null) {
			_aiResults = null;
			buttons_.aiSearch.Visibility = Visibility.Visible;
			buttons_.copyResults.Visibility = Visibility.Collapsed;
		}
		
		var s = _search.Text.Trim();
		if (s.Length < 2) {
			_tv.SetItems(_root.Children());
			_showingResults = false;
			if (!_openingItem && _selectedItem != null) {
				_tv.Select(_selectedItem);
			}
			return;
		}
		
		//print.clear();
		
		//CONSIDER: use Lucene.
		//rejected: use SQLite FTS5. Tried but didn't like. Lucene is much better.
		
		var root = _SearchContains(_root);
		_Item _SearchContains(_Item parent) {
			_Item R = null;
			for (var n = parent.FirstChild; n != null; n = n.Next) {
				_Item r = null;
				if (n.dir) r = _SearchContains(n);
				if ((!n.dir || n.href != null) && r == null) {
					if (n.name.Contains(s, StringComparison.OrdinalIgnoreCase)) r = n.Clone();
				}
				if (r != null) {
					if (R == null) {
						R = parent.Clone();
						R.isExpanded = true;
					}
					R.AddChild(r);
				}
			}
			return R;
		}
		
		//try stemmed fuzzy. Max Levenshtein distance 1 for a word.
		//	rejected: use FuzzySharp. For max distance 1 don't need it.
		bool fuzzy = root == null && s.Length >= 3;
		if (fuzzy) {
			string[] a1 = _Stem(s);
			root = _SearchFuzzy(_root);
			_Item _SearchFuzzy(_Item parent) {
				_Item R = null;
				for (var n = parent.FirstChild; n != null; n = n.Next) {
					_Item r = null;
					if (n.dir) r = _SearchFuzzy(n);
					if (!n.dir || n.href != null) {
						n.stemmedName ??= _Stem(n.name);
						bool allFound = true;
						foreach (var v1 in a1) {
							bool found = false;
							foreach (var v2 in n.stemmedName) {
								if (found = _Match(v1, v2)) break;
							}
							if (!(allFound &= found)) break;
						}
						if (allFound) r = n.Clone();
					}
					if (r != null) {
						if (R == null) {
							R = parent.Clone();
							R.isExpanded = true;
						}
						R.AddChild(r);
					}
				}
				return R;
			}
		}
		
		_tv.SetItems(root?.Children());
		_showingResults = true;
		
		static bool _Match(string s1, string s2) {
			if (s1[0] != s2[0] || Math.Abs(s1.Length - s2.Length) > 1) return false; //the first char must match
			if (s1.Length > s2.Length) Math2.Swap(ref s1, ref s2); //let s1 be the shorter
			
			int ib = 0, ie1 = s1.Length, ie2 = s2.Length;
			while (ib < s1.Length && s1[ib] == s2[ib]) ib++; //skip common prefix
			while (ie1 > ib && s1[ie1 - 1] == s2[--ie2]) ie1--; //skip common suffix
			
			int n = ie1 - ib;
			if (n == 1) return s1.Length == s2.Length || ib == ie1;
			return n == 0;
		}
	}
	
	string[] _Stem(string s) {
		if (_stem.stemmer == null) _stem = (new(), new(), new regexp(@"(*UCP)[^\W_]+"));
		_stem.a.Clear();
		foreach (var v in _stem.rx.FindAll(s.Lower(), 0)) {
			_stem.a.Add(_stem.stemmer.Stem(v));
		}
		return _stem.a.ToArray();
	}
	(Libs.Porter2Stemmer.EnglishPorter2Stemmer stemmer, List<string> a, regexp rx) _stem;
	
	async void _AiSearch() {
		if (_favoritesView) _FavoritesToggle();
		
		var query = _search.Text.Trim();
		if (query.Length < 2) return;
		
		AI.AiModel.ApiKeys = App.Settings.ai_ak;
		var emModel = AI.AiModel.GetModel<AI.AiEmbeddingModel>(App.Settings.ai_modelEmbed, displayName: true);
		if (emModel == null) {
			_AiSettingsError($"Please go to Options > AI and select models for documentation search.");
			return;
		}
		var rrModel = AI.AiModel.GetModel<AI.AiRerankModel>(App.Settings.ai_modelRerank, displayName: true);
		if (rrModel == null) AI.AiModel.RerankerModelWarning();
		
		try {
			_ctsAiSearch?.Cancel();
			_ctsAiSearch?.Dispose();
			_ctsAiSearch = new();
			var cancel = _ctsAiSearch.Token;
			
			var em = new AI.Embeddings(emModel);
			var ems = await Task.Run(() => em.GetDocsEmbeddings(cancel));
			
			var r1 = _search.RectInScreen();
			using var osd = osdText.showText("Searching.\nClick to cancel.", -1, new(r1.right, r1.bottom), showMode: OsdMode.ThisThread);
			osd.Clicked += (_, _) => { _ctsAiSearch?.Cancel(); };
			
			int takePlus = Math.Min(40, query.Count(c => c is <= ' ' or ',' or '.' or ';' or '?'));
			int take = 15 + takePlus;
			
			var queryVector = await Task.Run(() => em.CreateEmbedding(query, cancel));
			var topAll = em.GetTopMatches(queryVector, ems, rrModel == null ? 50 : 150);
			if (topAll.Count == 0) return;
			
			Dictionary<string, (float score, bool summary)> dTop = [];
			foreach (var v in topAll) {
				var name = v.f.name;
				bool isSum = name[0] == '+';
				if (isSum) name = name[1..];
				dTop.TryAdd(name, (v.score, isSum));
			}
			var aTop = dTop.Select(o => (name: o.Key, v: o.Value)).OrderByDescending(o => o.v.score).ToArray();
			
			List<_Item> a = [];
			if (rrModel != null) {
				osd.Text = "Reranking.\nClick to cancel.";
				await Task.Run(() => {
					var names = aTop.Select(o => o.name).ToArray();
					var texts = em.GetDocsTexts(names);
					var headers = rrModel.GetHeaders();
					var post = rrModel.GetPostData(query, texts);
					var j = rrModel.Post(post, headers, cancel).Json();
					//print.it(j.ToJsonString(new() { WriteIndented = true }));
					var ar = rrModel.GetResults(j);
					int i = 0;
					float firstScore = 0;
					foreach (var v in ar) {
						if (i == 0) firstScore = v.score;
						if (i++ > take || firstScore - v.score > .3f || v.score < .4f) break;
						_FindAdd(names[v.index]);
					}
				});
			} else {
				float firstScore = aTop[0].v.score;
				foreach (var v in aTop) {
					if (v.v.score < firstScore - .2f) break;
					_FindAdd(v.name);
				}
			}
			
			void _FindAdd(string s) {
				if (s.Starts("[cookbook]")) {
					s = s[11..];
					if (_FindRecipe(s, true) is { } r) {
						a.Add(r.Clone(notDir: true));
					} else {
						Debug_.Print(s);
					}
				} else {
					bool isAPI = s[0] != '[';
					var r = _root.Children().ElementAt(isAPI ? 2 : s[1] == 'a' ? 3 : 4);
					if (isAPI) {
						int i = s.Starts("Au.More") ? 7
							: s.Starts("Au.Types") ? 8
							: s.Starts("Au.Triggers") ? 11
							: 2;
						var ns = s[..i];
						r = r.Children().First(o => o.name == ns);
						r = r.Descendants().First(o => o.ApiFullNameEquals(s));
						s = s[(i + 1)..];
						r = r.Clone(s, notDir: true);
					} else {
						string name = s = s[(s.IndexOf(' ') + 1)..], section = null;
						int i = s.Find(" | ");
						if (i > 0) (section, name) = (name[(i + 3)..], name[..i]);
						if (section == "other") section = null;
						
						r = r.Children().FirstOrDefault(o => o.name == name);
						if (r == null) {
							Debug_.Print(name);
							return;
						}
						
						string href = r.href;
						if (section != null) {
							s = $"{r.name} | {section}";
							section = section.Lower().Replace(' ', '-').RxReplace(@"[^[:alnum:]\-]", "");
							href = $"{href}#{section}";
						} else {
							s = r.name;
						}
						
						r = new(false, s, r.docKind, null, href, r);
					}
					a.Add(r);
				}
			}
			
			_tv.SetItems(a);
			_aiResults = a;
			_showingResults = true;
			buttons_.aiSearch.Visibility = Visibility.Collapsed;
			buttons_.copyResults.Visibility = Visibility.Visible;
		}
		catch (OperationCanceledException etc) { if (etc.InnerException is TimeoutException) print.it(etc.Message); }
		catch (InvalidCredentialException) {
			var api = emModel.api;
			_AiSettingsError($"Please go to Options > AI and set the API key for {api}.\nYou can create an API key in your account on the {api} website.");
		}
		catch (Exception e1) { print.it(e1); }
		
		void _AiSettingsError(string text) {
			if (!dialog.showOkCancel("AI search error", text, owner: P)) return;
			DOptions.AaShow(DOptions.EPage.AI);
		}
	}
	CancellationTokenSource _ctsAiSearch;
	
	void _CopyResultsForAiChat(WBButtonClickArgs ba) {
		if (!(_aiResults?.Count > 0)) return;
		using var db = new sqlite(folders.ThisAppBS + "doc-ai.db", SLFlags.SQLITE_OPEN_READONLY);
		var b = new StringBuilder($$"""


--- Context ---

Below are LibreAutomate documentation articles that likely contain information to answer the user question.
They are retrieved and ordered using AI embedding.
The search phrase was: {{_search.Text}}
The user question is above or below this context data. If it's missing, assume the search phrase is the question.

Articles are separated by `--- Article kind: KIND ---` lines, where KIND is one of:
- API reference - library API member reference (method, class, etc)
- library conceptual article - a library documentation article other than API member reference
- cookbook - a how-to article with code examples
- application help - LibreAutomate IDE documentation

""");
		foreach (var v_ in _aiResults) {
			var v = v_.clonedFrom;
			var name = v.name;
			var dbName = v.docKind switch {
				_DocKind.Cookbook => "[cookbook] " + name,
				_DocKind.Editor => "[editor] " + name,
				_DocKind.Article => "[articles] " + name,
				_ => v.FullName
			};
			
			if (!db.Get(out string text, "SELECT text FROM doc WHERE name=?", dbName)) { Debug_.Print(dbName); continue; }
			var docKind = v.docKind switch { _DocKind.Cookbook => "cookbook", _DocKind.Editor => "application help", _DocKind.Article => "library conceptual article", _ => "API reference" };
			b.AppendFormat("""


--- Article kind: {0} ---

{1}
""", docKind, text);
		}
		b.Append("""


--- End of context ---


""");
		
		//print.it(b.ToString());
		clipboard.text = b.ToString();
	}
	
	//#if DEBUG
	//	void _Test() {
	//		print.clear();
	//		using var db = new sqlite(folders.ThisAppBS + "doc-ai.db", SLFlags.SQLITE_OPEN_READONLY);
	//		//foreach (var v in _root.Children().ElementAt(2).Descendants().Where(o => o.Level > 2)) {
	//		//	var s = v.Level == 3 ? $"{v.Parent.name}.{v.name}" : $"{v.Parent.Parent.name}.{v.Parent.name}.{v.name}";
	//		//	//print.it(s);
	//		//	if (!db.Get(out string text, "SELECT text FROM doc WHERE name=?", s)) { Debug_.Print(s); } //fields are not included in the DB for AI
	//		//}
	//		//foreach (var v in _root.Children().ElementAt(1).Descendants().Where(o => o.Level > 2 && !o.dir)) {
	//		//	var s = "[cookbook] " + v.name;
	//		//	//print.it(s);
	//		//	if (!db.Get(out string text, "SELECT text FROM doc WHERE name=?", s)) { Debug_.Print(s); } //very small articles are not included in the DB for AI
	//		//}
	//		//foreach (var v in _root.Children().ElementAt(3).Descendants().Where(o => o.Level > 2 && !o.dir)) {
	//		//	var s = "[articles] " + v.name;
	//		//	//print.it(s);
	//		//	if (!db.Get(out string text, "SELECT text FROM doc WHERE name=?", s)) { Debug_.Print(s); }
	//		//}
	//		//foreach (var v in _root.Children().ElementAt(4).Descendants().Where(o => o.Level > 2 && !o.dir)) {
	//		//	var s = "[editor] " + v.name;
	//		//	//print.it(s);
	//		//	if (!db.Get(out string text, "SELECT text FROM doc WHERE name=?", s)) { Debug_.Print(s); }
	//		//}
	//	}
	//#endif
	
	/// <summary>
	/// Finds and opens a recipe.
	/// <para>
	/// If called in LA tools process or in a non-main LA thread, runs async in the main process/thread.
	/// </para>
	/// </summary>
	/// <param name="name">Wildcard or start or any substring of recipe name.</param>
	public static void OpenRecipe(string name) {
		if (process.IsLaMainThread_) Panels.Help._OpenRecipe(name);
		else if (process.IsLaProcess_) App.Dispatcher.InvokeAsync(() => OpenRecipe(name));
		else WndCopyData.Send<char>(ScriptEditor.WndMsg_, 18, name);
	}
	
	void _OpenRecipe(string name) {
		Panels.PanelManager[P].Visible = true;
		_OpenItem(_FindRecipe(name), true);
	}
	
	_Item _FindRecipe(string s, bool exact = false) {
		var d = _root.Descendants().Where(o => o.docKind is _DocKind.Cookbook);
		if (exact) return d.FirstOrDefault(o => !o.dir && o.name == s);
		return d.FirstOrDefault(o => !o.dir && o.name.Like(s, true))
			?? d.FirstOrDefault(o => !o.dir && o.name.Starts(s, true))
			?? d.FirstOrDefault(o => !o.dir && o.name.Find(s, true) >= 0);
	}
	
	public void MenuCommand() {
		Panels.PanelManager[P].Visible = true;
		_search.Focus();
		
		if (_selectedItem == null && !App.Settings.doc_web) {
			_OpenItem(_root.FirstChild, true);
		}
	}
	
	void _ContextMenu(_Item item) {
		var m = new popupMenu();
		
		if (item is { docKind: not _DocKind.Folder }) {
			_FavoritesLoad(out var csv);
			var item0 = item; if (item.clonedFrom is { } orig) item = orig;
			
			string kind = item.docKind switch { _DocKind.Cookbook => "C", _DocKind.Article => "A", _DocKind.Editor => "E", _ => "L" };
			string name = item.FullName;
			int iFavorite = csv?.Rows.FindIndex(o => o[1] == name && o[0] == kind) ?? -1;
			m.AddCheck("Favorite", iFavorite >= 0, k => {
				if (iFavorite < 0) {
					csv ??= new();
					csv.AddRow(kind, name);
				} else {
					csv.RemoveRow(iFavorite);
					if (_favoritesView) {
						_favorites.Remove(item0);
						_tvFavorites.SetItems(_favorites);
					}
				}
				try { csv.Save(_favoritesFile); } catch (Exception ex) { print.it(ex); return; }
				Panels.Editor.SyncEditorTextIfFileIs(_favoritesFile, false);
			});
		}
		//bool fileExists = filesystem.exists(_favoritesFile, true).File;
		//m["Edit favorites", disable: !fileExists] = o => { App.Model.ImportLinkOrOpen(_favoritesFile); }; //probably don't need. Not fully implemented: _FavoritesToggle should update the UI favorites list if the file was modified.
		m.AddCheck("Show favorites\tM-click", _favoritesView, o => _FavoritesToggle());
		
		m.Show();
	}
	
	void _FavoritesToggle() {
		if (_favoritesView ^= true) {
			if (_FavoritesLoad(out var csv)) {
				List<_Item> a = new(csv.Rows.Count);
				foreach (var n in _root.Descendants()) {
					if (n.docKind == _DocKind.Folder) continue;
					char kind = n.docKind switch { _DocKind.Cookbook => 'C', _DocKind.Article => 'A', _DocKind.Editor => 'E', _ => 'L' };
					foreach (var v in csv.Rows) {
						string s0 = v[0]; if (s0.Length != 1 || s0[0] != kind) continue;
						if (n.docKind == _DocKind.Api) {
							if (!n.ApiFullNameEquals(v[1])) continue;
						} else {
							if (n.name != v[1]) continue;
						}
						a.Add(n);
						break;
					}
				}
				bool same = _favorites != null && a.SequenceEqual(_favorites.Select(o => o.clonedFrom));
				if (!same) {
					_favorites = new(a.Count);
					foreach (var n in a) {
						var name = n.name; if (n.docKind == _DocKind.Api && n.Level == 4) name = n.Parent.name + "." + name;
						_favorites.Add(n.Clone(name, notDir: true));
					}
					_tvFavorites.SetItems(_favorites, true);
				}
			} else {
				_tvFavorites.SetItems(null);
			}
			_tv.Visibility = Visibility.Hidden;
			_tvFavorites.Visibility = Visibility.Visible;
		} else {
			_tvFavorites.Visibility = Visibility.Hidden;
			_tv.Visibility = Visibility.Visible;
		}
	}
	
	bool _favoritesView;
	List<_Item> _favorites;
	static readonly string _favoritesFile = AppSettings.DirBS + "Help favorites.csv";
	
	static bool _FavoritesLoad(out csvTable csv) {
		csv = null;
		Panels.Editor.SyncEditorTextIfFileIs(_favoritesFile, true);
		if (!filesystem.exists(_favoritesFile, true).File) return false;
		try { csv = csvTable.load(_favoritesFile); }
		catch (Exception ex) { print.warning(ex); return false; }
		if (csv.ColumnCount < 2) { csv = null; return false; }
		return true;
	}
	
	//#if DEBUG
	//	void _DebugGetWords() {
	//		print.clear();
	//		var hs = new HashSet<string>();
	//		foreach (var recipe in _root.Descendants().Where(o => o.docKind is _DocKind.Cookbook)) {
	//			var a = _Stem(recipe.name);
	//			foreach (var s in a)
	//				if (s.Length > 2 && !s[0].IsAsciiDigit()) hs.Add(s);
	//		}
	//		print.it(hs.OrderBy(o => o, StringComparer.OrdinalIgnoreCase));
	//	}
	//#endif
	
	enum _DocKind : byte { Folder, Cookbook, Api, Editor, Article }
	
	class _Item : TreeBase<_Item>, ITreeViewItem {
		internal readonly string name;
		internal readonly _DocKind docKind;
		internal readonly string symKind;
		internal readonly string href;
		internal readonly _Item clonedFrom;
		internal readonly bool dir;
		internal bool isExpanded;
		internal string[] stemmedName;
		
		public _Item(bool dir, string name, _DocKind docKind, string symKind = null, string href = null, _Item clonedFrom = null) {
			this.dir = dir;
			this.name = name;
			this.docKind = docKind;
			this.symKind = symKind;
			this.href = href;
			this.clonedFrom = clonedFrom;
		}
		
		public _Item Clone(string newName = null, bool notDir = false) => new(dir & !notDir, newName ?? name, docKind, symKind, href, this);
		
		#region ITreeViewItem
		
		string ITreeViewItem.DisplayText => name;
		
		object ITreeViewItem.Image
			=> docKind switch {
				_DocKind.Cookbook => name == "Documentation" ? EdIcons.Help : "*BoxIcons.RegularCookie" + EdIcons.darkYellow,
				_DocKind.Editor => "*Material.ApplicationOutline" + EdIcons.blue,
				_DocKind.Article => "*PhosphorIcons.Article" + EdIcons.black,
				_ => symKind != null ? _KindIcon : EdIcons.FolderArrow(isExpanded),
			};
		
		string _KindIcon => symKind switch {
			"Namespace" => "resources/ci/namespace.xaml",
			"Class" => "resources/ci/class.xaml",
			"Struct" => "resources/ci/structure.xaml",
			"Enum" => "resources/ci/enum.xaml",
			"Interface" => "resources/ci/interface.xaml",
			"Delegate" => "resources/ci/delegate.xaml",
			"Method" or "Constructor" => "resources/ci/method.xaml",
			"Property" or "Indexer" => "resources/ci/property.xaml",
			"Event" => "resources/ci/event.xaml",
			"Field" => "resources/ci/field.xaml",
			"Operator" => "resources/ci/operator.xaml",
			_ => null
		};
		
		void ITreeViewItem.SetIsExpanded(bool yes) { isExpanded = yes; }
		
		bool ITreeViewItem.IsExpanded => isExpanded;
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		
		bool ITreeViewItem.IsFolder => dir;
		
		#endregion
		
		/// <summary>
		/// If Api, returns <c>"Namespace.Type.name"</c> or <c>"Namespace.name"</c>, else <c>"name"</c>.
		/// </summary>
		public string FullName => docKind != _DocKind.Api ? name : Level == 4 ? $"{Parent.Parent.name}.{Parent.name}.{name}" : $"{Parent.name}.{name}";
		
		public bool ApiFullNameEquals(string s) {
			if (!s.Ends(name)) return false;
			int i1 = s.Length - name.Length - 1;
			if (s[i1] != '.') return false;
			int level = Level; if (!(level is 3 or 4)) return false;
			var t = this;
			if (level == 4) { //member of a type
				t = Parent; //type
				int i2 = i1 - t.name.Length;
				if (!s.Eq(i2, t.name) || !s.Eq(--i2, '.')) return false;
				i1 = i2;
			}
			t = t.Parent; //namespace
			if (i1 != t.name.Length || !s.Starts(t.name)) return false;
			return true;
		}
	}
}
