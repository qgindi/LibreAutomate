/// Creates doc-ai.db from files created by script `Au docs`. It will be used in semantic search using AI embedding.
/// How: converts HTML to markdown optimized for AI embeddings.
/// Afterwards also run script `AI summaries` to fill the `summary` column of the DB.

/*/ nuget html\HtmlAgilityPack; nuget -\YamlDotNet; /*/
using ReverseMarkdown;
using HtmlAgilityPack;
using System.Net;
using YamlDotNet.RepresentationModel;
using System.Text.Json.Nodes;

if (script.testing) print.clear();

string startFrom = null;
//startFrom = "Au.clipboard";
//startFrom = "Au.clipboardData.AddImage";
//startFrom = "Au.clipboardData.getFiles";
//startFrom = "Au.clipboardData.contains";
//startFrom = "Au.clipboardData.getBinary";
//startFrom = "Au.clipboard.paste";
//startFrom = "Au.clipboard.text"; //property
//startFrom = "Au.computer.suspend";
//startFrom = "Au.consoleProcess.-ctor";
//startFrom = "Au.csvTable";
//startFrom = "Au.csvTable.Item";
//startFrom = "Au.Types.TreeBase-1.Count";
//startFrom = "Au.More.CaptureScreen.Pixels";
//startFrom = "Au.toolbar";
//startFrom = "Au.Types.HSContentPart"; //record
//startFrom = "Au.Types.POINT"; //struct
//startFrom = "Au.run.it";
//startFrom = "Au.Types.ExtString";
//startFrom = "Au.Types.CSLink";
//startFrom = "Au";
//startFrom = "Au.elm.ComboSelect";
//startFrom = "Output tags";
//startFrom = "Au.dialog.-ctor";
//startFrom = "Au.elm";
//startFrom = "Au.Types.RFlags";
//startFrom = "Au.Triggers.TAFlags";
//startFrom = "Au.Triggers.ActionTrigger";
//startFrom = "Au.Triggers.AutotextTrigger";
//startFrom = "Au.osdRect";
//startFrom = "Au.Types.JSettings";
//startFrom = "Au.dialog";
//startFrom = "Wildcard expression";
//startFrom = "Au.dialog.SetExpandControl";
//startFrom = "Au.More.RegisteredHotkey.Register";
//startFrom = "Au.wnd";
//startFrom = "Au.clipboardData.getBinary";
//startFrom = "Au.folders";
//startFrom = "Au.More.MemoryUtil.FreeAlloc";
//startFrom = "Au.elm.Invoke";
//startFrom = "Au.keys.send";
//startFrom = "Au.Types.IFImage";
//startFrom = "Au.dialog.Controls";

HtmlToMarkdown h2m = new(step: !true || startFrom != null, startFrom);
h2m.Convert();

class HtmlToMarkdown(bool step, string startFrom) {
	
	const string c_rootDir = @"C:\Temp\Au\DocFX\site\";
	string[] _subDirs = ["api", "articles", "editor", "cookbook",];
	string _subDir;
	bool _pause;
	bool _isApiDir, _isApi;
	string _name;
	Dictionary<string, string> _dName;
	
	public void Convert() {
		if (step || startFrom != null) {
			_Convert();
		} else {
			//using var p1 = perf.local();
			
			_Toc();
			
			var dbFile = folders.ThisAppBS + "doc-ai.db";
			filesystem.delete(dbFile);
			using (var db = new sqlite(dbFile)) {
				db.Execute("CREATE TABLE doc (name TEXT PRIMARY KEY, summary TEXT, text TEXT)");
				using var dbInsert = db.Statement("INSERT INTO doc VALUES (?, ?, ?)");
				using var dbTrans = db.Transaction();
				_Convert(dbInsert);
				dbTrans.Commit();
				db.Execute("VACUUM");
			}
			filesystem.copyTo(dbFile, @"C:\code\Au.Editor", FIfExists.Delete);
			
#if true
			print.it("<>DONE. Created doc-ai.db.");
			script.run(@"AI summaries.cs");
#else
			print.it("""
<>DONE. Created doc-ai.db.
	Now <script AI summaries.cs>add summaries<>
""");
#endif
			//print.scrollToTop();
		}
	}
	
	void _Convert(sqliteStatement dbInsert = null) {
		_InitConverter();
		foreach (var subDir in _subDirs) {
			_subDir = subDir;
			_isApiDir = _subDir is "api";
			foreach (var f in filesystem.enumFiles(c_rootDir + subDir, "*.html")) {
				_name = f.Name[..^5];
				if (_name is "toc" or "index") continue;
				if (_isApiDir) {
					if (_name is "Au" or "Au.Types" or "Au.More" or "Au.Triggers") continue;
				}
				
				if (startFrom != null) { if (_name == startFrom) startFrom = null; else continue; }
				
				if (step) {
					print.clear();
					print.it($"<><lc yellowgreen>{_name}<>");
				}
				
				_pause = false;
				_isApi = _isApiDir && _name.Starts("Au.");
				
				string html = _LoadHtmlArticle(f.FullPath), html1 = html;
				
				html = _PreprocessHtml(html, f.FullPath, out HtmlNode doc);
				if (html is null) continue;
				
				string markdown;
				try {
					markdown = _converter.Convert(doc);
				}
				catch (UnknownTagException ex) {
					print.it(ex);
					OpenInBrowser(f.FullPath);
					return;
				}
				
				_PostprocessMarkdown(ref markdown);
				
				//find long articles (maybe need to split)
				var tok = markdown.Length / 3;
				if (tok > 4000 && !(_name is "Code editor" or "File properties" or "toc")) {
					//pause = true; print.clear();
					print.it($"{tok}  {_name}");
				} else { //skip some small articles without useful info
					if (_SkipSmall(markdown)) continue;
				}
				
				if (step || _pause) {
					//print.it(markdown);
					print.scrollToTop();
					SaveAndOpenInEditor(markdown, "4.md");
					
					dialog d = new(_name, "<a href=\"1\">HTML 1</a>\n<a href=\"2\">HTML 2</a>\n<a href=\"3\">In browser</a>\n<a href=\"4\">Markdown</a>", "1 Continue|0 Stop", y: 1);
					d.HyperlinkClicked += _Link;
					d.Created += o => {
						Task.Run(() => {
							if (o.hwnd.WaitFor(-1, k => !o.hwnd.IsActive)) { 100.ms(); o.hwnd.Activate(); }
						});
					};
					if (1 != d.ShowDialog()) return;
					void _Link(DEventArgs e) {
						int link = e.LinkHref.ToInt();
						if (link == 3) {
							OpenInBrowser(f.FullPath);
						} else {
							SaveAndOpenInEditor(link == 4 ? markdown : link == 1 ? html1 : html, e.LinkHref + (link == 4 ? ".md" : ".html"));
						}
					}
				} else if (dbInsert != null) {
					//filesystem.saveText($@"C:\Temp\Au\markdown\{subDir}\{_name}.md", markdown); //makes slower: 3 -> 9 s; (30-60 s if the folder not excluded in WD)
					
					if (_isApiDir) {
						dbInsert.BindAll($"{_dName[f.Name]}", null, markdown).Step();
						dbInsert.Reset();
					} else {
						//split some long articles
						if (_name is "Code editor" or "File properties") {
							//print.it(markdown);
							StringBuilder other = null; string otherName = null;
							foreach (var (i, v) in markdown.RxSplit("\n\n###? ").Index()) {
								int j = v.IndexOf('\n');
								if (j < 0) continue; //## Header\n### Header
								var heading = v[0..j];
								if (heading.Ends("(group of properties)")) continue;
								if (i > 1 && v.Length < 500 && v.LineCount() <= 5) {
									other ??= new($"# {otherName = $"{_name} | other"}\r\n");
									other.AppendLine($"\r\n## {v}");
								} else {
									string name2 = i == 0 ? _name : _name + " | " + heading;
									string text = i == 0 ? v : $"# {_name} | {v}";
									//print.it($"<><lc yellowgreen>{name2}<>"); print.it(text);
									_DbInsert(name2, text);
								}
							}
							if (other != null) _DbInsert(otherName, other.ToString());
							continue;
						}
						
						_DbInsert(_name, markdown);
						
						void _DbInsert(string name, string text) {
							dbInsert.BindAll($"[{subDir}] {name}", null, text).Step();
							dbInsert.Reset();
						}
					}
				}
			}
		}
	}
	
	string _PreprocessHtml(string html, string file, out HtmlNode doc) {
		//print.it(html);
		
		var hdoc = new HtmlAgilityPack.HtmlDocument();
		hdoc.LoadHtml(html);
		doc = hdoc.DocumentNode;
		
		//remove comments
		foreach (var v in doc.Descendants().OfType<HtmlCommentNode>().ToArray()) v.Remove();
		
		//_IsBlockElement and _IsInlineElement must know names of all elements used in our docs
		foreach (var v in doc.Descendants()) {
			if (v is HtmlTextNode) continue;
			if (_IsBlockElement(v)) continue;
			if (_IsInlineElement(v)) continue;
			throw new _Exception(file, v);
		}
		
		static bool _IsBlockElement(HtmlNode n) => n != null && n.Name is "blockquote" or "dd" or "details" or "div" or "dl" or "dt" or "footer" or "form" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "header" or "hr" or "li" or "nav" or "noscript" or "ol" or "p" or "pre" or "section" or "summary" or "table" or "tbody" or "td" or "tfoot" or "th" or "thead" or "tr" or "ul" or "video";
		
		static bool _IsInlineElement(HtmlNode n) => n != null && n.Name is "a" or "abbr" or "acronym" or "b" or "bdo" or "big" or "br" or "button" or "cite" or "code" or "dfn" or "em" or "i" or "img" or "input" or "kbd" or "label" or "map" or "object" or "output" or "q" or "samp" or "script" or "select" or "small" or "span" or "strong" or "sub" or "sup" or "textarea" or "time" or "tt" or "u" or "var";
		
		//print.it(doc.InnerHtml);
		//print.it("----");
		_RemoveInsignificantWhitespace(doc, file);
		//print.it(doc.InnerHtml);
		if (doc.InnerHtml is var sih && sih.RxReplace(@"(?s)<pre.+?</pre>", "").Contains('\n')) { print.it("----", pathname.getNameNoExt(file)); print.it(sih); }
		//Environment.Exit(0);
		
		void _RemoveInsignificantWhitespace(HtmlNode node, string file) {
			if (node.Name == "pre") {
				if (node.Ancestors("td").FirstOrDefault() is { } td)
					if (!(_isApi && td.ParentNode is { Name: "tr" } tr && tr.Elements("td").Count() == 1)) //we'll convert it to li
						throw new _Exception(file, node);
				return;
			}
			for (int i = node.ChildNodes.Count - 1; i >= 0; i--) {
				var child = node.ChildNodes[i];
				if (child is HtmlTextNode t) {
					var prev = t.PreviousSibling;
					var next = t.NextSibling;
					var s = t.Text;
					if (string.IsNullOrWhiteSpace(s)) {
						bool remove = false;
						if (prev != null && next != null) remove = _IsBlockElement(prev) || _IsBlockElement(next);
						else remove = _IsBlockElement(prev) || _IsBlockElement(next) || _IsBlockElement(node);
						if (remove) t.Remove(); else t.Text = " ";
					} else {
						if (_IsBlockElement(prev ?? node)) s = s.TrimStart();
						if (_IsBlockElement(next ?? node)) s = s.TrimEnd();
						
						if (s.Contains("\n")) {
							var lines = s.Lines(noEmpty: true);
							for (int ii = lines.Length; --ii >= 0;) {
								node.InsertAfter(hdoc.CreateTextNode(lines[ii].Trim()), t);
								if (ii > 0) node.InsertAfter(hdoc.CreateElement("br"), t);
							}
							t.Remove();
						} else {
							t.Text = s;
						}
					}
				} else if (child.HasChildNodes) {
					_RemoveInsignificantWhitespace(child, file);
				}
			}
		}
		
		//find tables with > 2 columns
		//if (doc.SelectNodes("//table") is { } tables2) {
		//	foreach (var table in tables2) {
		//		if (table.SelectSingleNode(".//tr") is { } r1) {
		//			int n = r1.SelectNodes("td|th").Count;
		//			if (n > 2) print.it(n, file);
		//		}
		//	}
		//}
		//return;
		
		bool isType = false;
		
		//headers
		if (_isApi) {
			var h1 = doc.Element("h1");
			var uid = h1.GetAttributeValue("data-uid", null);
			string h1Text = h1.InnerText;
			bool hasOverloads = h1Text.Ends(')');
			
			//skip fields without documentation
			if (h1Text.Starts("Field") && h1.NextSibling is { Name: "div", HasChildNodes: false }) {
				//print.it("-----"); for(var sib=h1.NextSibling; sib!=null; sib=sib.NextSibling) print.it(sib.OuterHtml);
				return null;
			}
			
			//<a>Class</a>.Method -> <b>Au.X.Class.Method</b>, or Class -> <b>Au.X.Class</b>
			if (!h1Text.RxMatch(@"^(\w+ (?:of )?)(\w+)([^\(]*)(?:\(.+?\))?$", out var mh)) throw new _Exception(file, $"{h1Text}, {uid}, {h1.OuterHtml}");
			string h1TextStart = mh[1].Value, classWord = mh[2].Value;
			int icw = uid.FindWord(classWord);
			if (icw < 0) throw new _Exception(file, $"{h1Text}, {uid}, {h1.OuterHtml}");
			h1.InnerHtml = $"{h1TextStart}<code>{uid[..icw]}{classWord}{mh[3].Value}</code>";
			
			if (hasOverloads) {
				foreach (var (i, h2) in doc.Elements("h2").Index()) h2.InnerHtml = "Overload " + (i + 1);
			} else {
				if (doc.Element("h2") is { InnerHtml: "Overload" } h2) h2.Remove();
			}
			
			if (doc.Element("hr") is { } hr1 && hr1.HasClass("overload")) hr1.Remove(); //remove <hr> before the first overload
			
			//remove namespace, assembly
			foreach (var h6 in doc.Elements("h6").ToArray()) {
				if (h6.FirstChild.Name == "strong") {
					var s1 = h6.FirstChild.InnerHtml;
					if (s1 is "Namespace" && h6.PreviousSibling.PreviousSibling is { Name: "hr" } hr2) hr2.Remove();
					if (s1 is "Assembly" or "Namespace") {
						isType = true;
						h6.Remove();
					}
				}
			}
			
			if (isType) {
				//remove Extension Methods section
				if (doc.SelectSingleNode("h3[@id='extensionmethods']") is { } em) {
					while (em.NextSibling?.Name is "div") em.NextSibling.Remove();
					em.Remove();
				}
				
				if (h1Text.Starts("Enum")) {
					//fields table -> headings. Because some tables contain multiple lines (list, paragraphs).
					if (doc.SelectSingleNode("h3[@id='fields']") is not { } hFields) throw null;
					if (hFields.NextSibling is not { Name: "table" } table) throw null;
					var parent = hFields.ParentNode;
					hFields.Remove(); hFields.Name = "h2"; parent.AppendChild(hFields);
					foreach (HtmlNode row in table.FirstChild.NextSibling.ChildNodes) {
						parent.AppendChild(HtmlNode.CreateNode($"<h3><code>{row.FirstChild.InnerText}</code></h3>"));
						var description = row.LastChild;
						if (description.HasChildNodes) {
							description.Remove();
							description.Name = "div";
							parent.AppendChild(description);
						}
					}
					table.Remove();
				} else {
					//compact inheritance
					if (doc.SelectSingleNode("div[@class='inheritance']") is { } inh) {
						StringBuilder bInh = new(), bDerived = null, b = bInh;
						var a = inh.Elements("div").Select(o => o.FirstChild).ToArray();
						bool haveInhFromThisLib = false;
						foreach (var (i, v) in a.Index()) {
							if (b.Length == 0) b.Append(bDerived != null ? "<p>Derived: " : "<p>"); else b.Append(bDerived != null ? ", " : " → ");
							if (i > 0 && v.Name == "a" && v.GetAttributeValue("href", null) is { } href && href.Starts("Au.")) {
								b.Append(v.OuterHtml);
								if (bDerived is null) haveInhFromThisLib = true;
							} else {
								b.Append("<code>").Append(v.InnerText).Append("</code>");
								//the list contains inheritance classes and classes derived from this. Split it into 2 lists.
								if (bDerived != null) throw new _Exception(file);
								if (i > 0 && v.Name != "a" && v.InnerHtml is var s1 && s1 != "Attribute") {
									if (!h1Text.Eq(h1Text.IndexOf(' ') + 1, s1)) throw new _Exception(file, $"uid={uid}, inh={s1}");
									if (i < a.Length - 1) b = bDerived = new();
								}
							}
							v.ParentNode.Remove();
						}
						inh.RemoveAllChildren();
						inh.AppendChild(_NewElem("h5", "Inheritance"));
						inh.AppendChild(HtmlNode.CreateNode(bInh.Append("</p>").ToString()));
						if (bDerived != null) inh.AppendChild(HtmlNode.CreateNode(bDerived.Append("</p>").ToString()));
						
						//remove inherited members except of this library
						if (inh.NextSibling is { Name: "div" } inh2 && inh2.HasClass("inheritedMembers")) {
							inh2.Remove();
							if (haveInhFromThisLib) {
								doc.AppendChild(_NewElem("h3", "Members inherited from other types of this library"));
								bool once = false;
								foreach (var v in inh2.Elements("div").ToArray()) {
									var link = v.FirstChild;
									if (link.GetAttributeValue("href", null).Starts("Au.")) {
										if (!once) once = true; else doc.AppendChild(hdoc.CreateTextNode(", "));
										link.Name = "code";
										doc.AppendChild(link);
										if (link.InnerHtml is [.., ')'] s1 && s1.IndexOf('(') is var i1 && i1 > 0) link.InnerHtml = s1.ReplaceAt(++i1..^1, ""); //remove params from method signatures
									}
								}
							}
						}
					}
					
					foreach (var h3 in doc.Elements("h3").ToArray()) {
						var id = h3.Id;
						//remove summaries from members. For AI they make more bad than good (article too big and diluted).
						if (id is "methods" or "properties" or "constructors" or "operators" or "events" or "fields") {
							if (h3.NextSibling is not { Name: "table", LastChild: { Name: "tbody" } tbody } table) throw null;
							bool isMethod = id is "methods";
							var b = new StringBuilder("<p>");
							string sep = null;
							foreach (var m in tbody.SelectNodes("tr/td[1]/a[1]")) {
								var s1 = m.InnerHtml;
								if (isMethod && s1[^1] == ')' && s1.IndexOf('(') is var i1 && i1 > 0) s1 = s1[..i1]; //remove params from method signatures
								b.Append(sep).Append("<code>").Append(s1).Append("</code>");
								sep = ", ";
							}
							b.Append("</p>");
							doc.InsertAfter(HtmlNode.CreateNode(b.ToString()), h3);
							table.Remove();
						}
					}
				}
				
				//somehow Remarks of types is <strong>
				foreach (var h5 in doc.Elements("h5").ToArray()) {
					if (h5.FirstChild.Name == "strong") h5.FirstChild.Name = "span";
				}
			}
			
			//DocFX converts <note> to <div><h5>. Bad in markdown.
			if (doc.SelectNodes("//h2|//h3|//h4|//h5|//h6") is { } headers) {
				foreach (var h in headers) {
					var pa = h.ParentNode;
					if (pa == doc) continue;
					var t = (h.FirstChild as HtmlTextNode);
					if (t?.Text is null or "Inheritance") continue;
					if (pa.Name != "div") throw null;
					if (pa.HasClass("alert")) {
						//print.it($"{pathname.getNameNoExt(file),-40}  {h.OuterHtml}  {h.ParentNode.OuterHtml}");
						pa.Name = "blockquote";
						h.Name = "b";
						t.Text = t.Text.Upper(SUpper.FirstChar) + ":";
					}
				}
			}
		}
		
		//tables
		int cellSectionIndex = 0;
		if (doc.SelectNodes("//table") is { } tables) {
			foreach (var table in tables) {
				if (_isApi) {
					//replace single-column tables with <ul>
					if (table.SelectNodes("tr") is { } rows && rows.All(r => r.Elements("td").Count() == 1)) {
						string section = null;
						if (table.ParentNode is { Name: "div", PreviousSibling: { Name: "h5" } h5 }) {
							section = h5.GetClasses().FirstOrDefault();
							//print.it(section);
						}
						
						if (section is "returns" or "propertyValue") {
							if (rows.Count != 1) throw null;
							var cell = rows[0].Element("td");
							cell.Name = "div";
							table.ParentNode.ReplaceChild(cell, table);
							_UnTuple(false, cell.FirstChild);
							if (cell.Element("div") is { FirstChild: not null } div1) cell.InsertBefore(hdoc.CreateElement("br"), div1);
						} else {
							var ul = hdoc.CreateElement("ul");
							
							foreach (var row in rows) {
								var cell = row.Element("td");
								
								//replace with <li>
								var li = hdoc.CreateElement("li");
								
								if (section is "parameters" or "exceptions" or "typeParameters") {
									var e1 = cell.FirstChild; if (e1.NodeType != HtmlNodeType.Element) throw null;
									//replace `param (type)\nDescription` with `param (type): Description` etc
									if (section is "parameters") {
										if (!(e1.Name == "span" && e1.HasClass("parametername"))) throw null;
										e1.Name = "i";
										e1.RemoveClass("parametername");
										var t1 = (HtmlTextNode)e1.NextSibling;
										_UnTuple(true, t1);
										if (cell.Element("div") is { FirstChild: not null } div1 && div1.PreviousSibling is HtmlTextNode t2 && t2.Text.Ends(')')) t2.Text += ":";
									} else {
										if (cell.Element("div") is { FirstChild: not null } div1) cell.InsertBefore(hdoc.CreateTextNode(":"), div1);
										if (section is "typeParameters") e1.Name = "code";
									}
								}
								
								li.AppendChildren(cell.ChildNodes);
								ul.AppendChild(li);
							}
							
							table.ParentNode.ReplaceChild(ul, table);
						}
						
						continue;
					}
				}
				
				foreach (var cell in table.SelectNodes(".//td")) {
					//move multiline cell contents to a new section below the table
					if (cell.ChildNodes is { Count: > 1 } cn && cn.Any(o => _IsBlockElement(o))) {
						if (cellSectionIndex++ == 0) {
							doc.AppendChild(_NewElem("h2", "Footnotes")); //tested: all AI understand everything without explanation.
						}
						var div = hdoc.CreateElement("div");
						while (cell.FirstChild is { } fc) { cell.RemoveChild(fc); div.AppendChild(fc); }
						doc.AppendChild(_NewElem("h6", "details" + cellSectionIndex));
						doc.AppendChild(div);
						cell.AppendChild(HtmlNode.CreateNode($"<p>(see section <code>details{cellSectionIndex}</code> in Footnotes)</p>"));
						//_pause = true;
					}
					
					//escape pipes in code: `one\|two`
					foreach (var v in cell.Descendants("code")) {
						if (v.FirstChild is not HtmlTextNode { NextSibling: null } t) throw new _Exception(file, v);
						t.Text = t.Text.Replace("|", "[[pipe]]"); //later will restore and escape. Don't escape now, because something will replace single backslash with two.
						
						//About pipe characters in markdown tables in `code`:
						//	Some markdown converters/renderers don't support it, unless escaped like `a\|b`. Eg GitHub, VSCode preview.
						//	Others don't support escaped. Eg VS, Markdig.
						//	I write docs with unescaped pipes. Because of Markdig etc. Therefore have to use VS and not VSCode, at least when editing articles with such tables.
						//	For AI it's safer to escape. Also need it for preview in VSCode when debugging this script.
					}
				}
			}
		}
		
		//replace API links with inline code. Remove URL from other links. Make Au API fully qualified.
		if (doc.SelectNodes("//a") is { } links) {
			foreach (var link in links) {
				string href = link.GetAttributeValue("href", null); if (href.NE()) throw null;
				string text = link.InnerText;
				
				if (href.Starts("Au.")) {
					_AuApi();
				} else if (href.Starts("../") || href[0] is '/') {
					href = href[(href[0] is '/' ? 1 : 3)..];
					if (href.Starts("api/")) {
						href = href[4..];
						_AuApi();
					} else {
						int i = href.IndexOf('/');
						if (i > 0) href = href[..i];
						if (href is not ("articles" or "editor" or "cookbook" or "index.html")) throw new _Exception(file, href);
						href = "";
					}
				} else if (href.Starts("https://learn.microsoft.com/dotnet/api/")) {
					href = "net_api";
				} else if (href.Starts("https://www.google.com/search?q=")) {
					if (_PreviousSiblingTexts(link).Any(o => o.Ends("API "))) href = "win_api";
					else if (text.Starts("sqlite3_")) href = "sqlite_api";
					else if (text.RxIsMatch(@"^[A-Z][\w\.]*?[A-Z]\w*$")) {
						href = href[32..];
						//print.it(_isApi, text, href);
						if (_isApi) href = "win_api";
						else if (text.Contains('.') || href.RxIsMatch(@"\+(?:class|struct|interface|enum)\b") || href.Starts("site:microsoft.com")) href = "net_api";
						else href = "";
					} else {
						href = "";
					}
				} else if (href.Starts("https://www.pcre.org/")) {
					href = "";
				} else if (href.Starts("https://www.libreautomate.com/forum")) {
					continue;
				} else if (href.Starts("https://www.libreautomate.com/")) {
					throw new _Exception(file, link.OuterHtml); //must be relative link
				} else if (href.Starts("http")) {
					href = "";
				} else if (href.Starts("#")) {
					continue;
				} else {
					if (_isApi) throw new _Exception(file, link.OuterHtml);
					//print.it(_subDir, text, href, link.ParentNode.InnerText);_pause=true;
					href = "";
				}
				
				if (href is "api" or "net_api") {
					_LinkGenericNullableArrayPointer(href.Length > 3, link, ref text);
					if (link.Name != "a") continue;
				}
				
				if (href.Ends("api")) {
					link.Name = "code";
					link.InnerHtml = text;
				} else if (href is "") {
					link.SetAttributeValue("href", "\U0001F517"); //link symbol
				} else {
					throw new _Exception(file, href);
				}
				
				void _AuApi() {
					if (!text.Starts("Au.")) {
						//make fully-qualified
						var filename = href[..href.Find(".html")];
						int i = text.IndexOfAny(['.', '(', '&']);
						var classWord = i < 0 ? text : text[..i];
						i = filename.Find(classWord);
						if (i < 1) {
							href = "";
						} else {
							text = filename[..i] + text;
							href = "api";
						}
					} else href = "api";
				}
			}
		}
		
		//join adjacant <code> (invalid markdown `code1``code2`)
		if (doc.SelectNodes("//code")?.ToArray() is { } codes) {
			for (int i = codes.Length; --i > 0;) {
				var c = codes[i];
				if (c.ParentNode.Name is "pre") continue;
				if (c.PreviousSibling == codes[i - 1]) {
					c.PreviousSibling.InnerHtml += c.InnerText;
					c.Remove();
				}
			}
		}
		
		_EscapeLtGtEtc(doc);
		
		void _EscapeLtGtEtc(HtmlNode doc) {
			foreach (var t in doc.Descendants().OfType<HtmlTextNode>()) {
				var s = t.Text;
				var p = t.ParentNode;
				if (p.Name == "code" || t.Ancestors("pre").Any()) continue;
				
				if (s.Contains('*')) print.it("Error in docs: * not in <c>", s, p.OuterHtml, _name);
				
				if (s.Contains('_')) {
					if (s.RxIsMatch(@"(?<!\w)_(?!KNOWNFLAGS|NOCLIENTMOVE|NOCLIENTSIZE|STATECHANGED)")) {
						print.it("Error in docs: _TEXT not in <c>", s, p.OuterHtml, _name);
					} else {
						//print.it($"{s}    |    {p.OuterHtml}    |    {_name}");
					}
				}
				
				if (s.Contains("&lt;") || s.Contains("&gt;")) {
					s = s.Replace("&lt;", "[[lt]]");
					s = s.Replace("&gt;", "[[gt]]");
					t.Text = s;
				}
			}
		}
		
		//catch <b> where should be <code>
		//foreach (var v in doc.Descendants("b").Concat(doc.Descendants("strong"))) {
		//	var t = v.FirstChild as HtmlTextNode;
		//	var s = t.Text;
		//	if (t is null || v.FirstChild.NextSibling != null) {
		//		print.it(v.OuterHtml);
		//	} else if (s.Contains(' ') && !(s.Ends("&gt;") && s.Contains("&lt;"))) {
		//		//print.it($"{s}        {_name}");
		//	} else if (!s.Ends(':')) {
		//		print.it($"{s}        {_name}");
		//	}
		//}
		
		HtmlNode _NewElem(string name, string text) {
			if (text.Contains('<')) throw new ArgumentException();
			var n = hdoc.CreateElement(name);
			n.AppendChild(hdoc.CreateTextNode(text));
			return n;
		}
		
		static IEnumerable<string> _PreviousSiblingTexts(HtmlNode n) {
			while ((n = n.PreviousSibling) != null) {
				if (n is HtmlTextNode t) yield return t.Text;
			}
		}
		
		void _LinkGenericNullableArrayPointer(bool isNet, HtmlNode link, ref string text) {
			if (!(link.NextSibling is HtmlTextNode { Text: ['&' or '?' or '[' or '*', ..] } t1)) return;
			var s = t1.Text;
			if (s[0] is '*') {
				_Simple("*");
				return;
			}
			
			if (s.Starts('&')) {
				if (!s.Starts("&lt;")) return;
				int depth = 0;
				for (HtmlNode n = t1; ; n = n.NextSibling) {
					if (n is not HtmlTextNode t2) continue;
					s = t2.Text;
					int i = s.Find("&gt;") + 4;
					if (i >= 4) depth--;
					if (s.Contains("&lt;")) depth++;
					if (depth > 0) {
						n = t2.ParentNode.ReplaceChild(_NewElem("code", s), t2);
					} else {
						t2.ParentNode.InsertBefore(_NewElem("code", s[..i]), t2);
						t2.Text = s = s[i..];
						t1 = t2;
						break;
					}
				}
			}
			if (s.Starts('?')) _Simple("?");
			if (s.Starts("[]")) _Simple("[]");
			
			void _Simple(string op) {
				t1.Text = s = s[op.Length..];
				if (link.Name == "code") { //byte etc
					link.InnerHtml = link.InnerText + op;
				} else {
					link.ParentNode.InsertAfter(_NewElem("code", op), link);
				}
			}
		}
		
		//If the type of the parameter/return/value contains tuples, makes it <code>.
		//Also makes T -> <code>T<code>.
		void _UnTuple(bool param, HtmlNode first) {
			var t1 = first as HtmlTextNode;
			if (param && !t1.Text.Starts("&nbsp;&nbsp;(")) throw null;
			int indexStart = param ? 13 : 0;
			for (var n = first; n.Name != "div"; n = n.NextSibling, indexStart = 0) if (n is HtmlTextNode t2 && t2.Text.IndexOf('(', indexStart) >= 0) goto tuple;
			
			//T -> <code>T<code>
			var typeNode = param ? first.NextSibling : first;
			if (typeNode.Name != "a") {
				bool isText = param ? typeNode.Name is "div" : t1 != null;
				if (!isText) {
					typeNode.Name = "code";
				} else if (param) {
					var code1 = t1.ParentNode.InsertAfter(_NewElem("code", t1.Text[13..^1]), t1);
					t1.ParentNode.InsertAfter(hdoc.CreateTextNode(")"), code1);
					t1.Text = "&nbsp;&nbsp;(";
				} else {
					t1.ParentNode.ReplaceChild(_NewElem("code", t1.Text), t1); //T[], T*
				}
			}
			return;
			
			tuple:
			StringBuilder b = new();
			if (param) {
				b.Append(((HtmlTextNode)first).Text[13..]);
				((HtmlTextNode)first).Text = "&nbsp;&nbsp;(";
				first = first.NextSibling;
			}
			while (first.Name != "div") {
				b.Append(first.InnerText);
				var nn = first.NextSibling;
				first.Remove();
				first = nn;
			}
			if (param) b.Remove(b.Length - 1, 1);
			var pa = first.ParentNode;
			pa.InsertBefore(_NewElem("code", b.ToString()), first);
			if (param) pa.InsertBefore(hdoc.CreateTextNode(")"), first);
		}
		
		return doc.OuterHtml;
	}
	
	void _PostprocessMarkdown(ref string md) {
		md = md.Trim().Replace("\r\n", "\n");
		md = md.Replace('\u00A0', ' ');
		md = md.Replace("[[pipe]]", @"\|");
		md = md.Replace("[[lt]]", @"\<").Replace("[[gt]]", ">");
		if (_isApi) {
			md = md.RxReplace(@"(?m)\R\* \* \*(?!\R\R## Overload)$\R*", "\n");
		}
	}
	
	bool _SkipSmall(string markdown) {
		if (markdown.Length < 300) {
			if (_isApi) {
				if (markdown.RxIsMatch(@"^.+\R\R```")) { //no summary
					if (_name.Starts("Au.folders.")) return false;
					int i = markdown.Find("\n```\n") + 5;
					var s = markdown[i..];
					var a = s.Split(["\n", "```"], StringSplitOptions.RemoveEmptyEntries);
					if (a is ["public void Dispose()", "##### Implements", _]) return true;
					if (a is ["public override string ToString()" or "public override int GetHashCode()", "##### Returns", _, "##### Overrides", _]) return true;
					if (a is ["protected override void Dispose(bool disposing)", "##### Parameters", _, "##### Overrides", _]) return true;
					if (a is ["", "", "", "", _]) return true;
					if (a is ["", "", "", "", _]) return true;
					
					//print.it($"<><lc LightBlue>{markdown.Length}  {_name}<>");
					//print.it(a);
				}
			} else {
				if (_subDir is "cookbook") {
					if (_name is "Create exe program") return true; //just a link to the editor article
				}
				
				//print.it($"<><lc LightBlue>{markdown.Length}  {_name}<>");
				//print.it(markdown);
			}
		}
		return false;
	}
	
	void _Toc() {
		var stream = new YamlStream();
		
		//api
		
		_dName = [];
		
		var yApi = new YamlMappingNode();
		stream.Add(new YamlDocument(yApi));
		
		var json = filesystem.loadText(c_rootDir + @"api\toc.json");
		var jRoot = JsonNode.Parse(json);
		foreach (var jNS in jRoot["items"].AsArray()) {
			var nsName = (string)jNS["name"];
			//print.it(nsName);
			
			var yNS = new YamlMappingNode();
			yApi.Add(nsName, yNS);
			
			foreach (var jType in jNS["items"].AsArray()) {
				var typeName = (string)jType["name"];
				var typeHref = (string)jType["href"];
				var typeFullName = $"{nsName}.{typeName}";
				//print.it("\t" + typeName);
				_dName.Add(typeHref, typeFullName);
				
				var yType = new YamlMappingNode();
				yNS.Add(typeName, yType);
				
				foreach (var jKind in jType["items"].AsArray()) {
					var kindName = (string)jKind["name"];
					//print.it("\t\t" + kindName);
					
					var yKind = new YamlSequenceNode { Style = YamlDotNet.Core.Events.SequenceStyle.Flow };
					yType.Add(kindName, yKind);
					
					bool isOp = kindName[0] == 'O';
					foreach (var jMember in jKind["items"].AsArray()) {
						var mName = (string)jMember["name"];
						if (isOp) {
							if (mName.Ends(" operator")) mName = mName[..^9]; else if (mName.Starts("operator ")) mName = mName[9..]; else throw null;
						}
						var mFullName = $"{typeFullName}.{mName}";
						var mHref = (string)jMember["href"];
						//print.it("\t\t\t" + mName);
						_dName.Add(mHref, mFullName);
						
						yKind.Add(mName);
					}
				}
			}
		}
		
		//conceptual
		
		var yConcept = new YamlMappingNode();
		stream.Add(new YamlDocument(yConcept));
		foreach (var subDir in _subDirs.Skip(1)) {
			var yList = new YamlSequenceNode();
			foreach (var f in filesystem.enumFiles(c_rootDir + subDir, "*.html")) {
				var name = f.Name[..^5];
				if (name is "index" or "toc") continue;
				//print.it(name);
				yList.Add(name);
			}
			yConcept.Add(subDir, yList);
		}
		
		using var writer = new StringWriter();
		stream.Save(writer, assignAnchors: false);
		var yaml = writer.ToString();
		yaml = yaml.Replace("\r\n...", "");
		
		//instructions for AI
		
		var instructions = """
Instructions for AI:
  This entire text contains three YAML documents separated by a `---` line:
    - 1. Instructions for AI (this document).
    - 2. TOC of LibreAutomate API documentation:
      - YAML structure: namespace > type > member kind > list of members.
      - Notes: Each type (class, enum etc) and member (method, event etc) has its own article. Articles for classes, structs and interfaces don't include member descriptions. Articles for enums include member descriptions. 
    - 3. TOC of other documentation, which has three parts:
      - articles: additional info for API documentation. Text formats, key names etc.
      - editor: documentation of the script editor app.
      - cookbook: how-to guides with code examples.
---

""";
		yaml = instructions + yaml;
		
		//print.it(yaml);
		filesystem.saveText(folders.ThisAppBS + "toc-ai.yml", yaml);
	}
	
	static string _LoadHtmlArticle(string path) {
		var s = filesystem.loadText(path);
		int i1 = s.Find("<h1"), i2 = s.LastIndexOf("</article>", StringComparison.OrdinalIgnoreCase);
		return s.AsSpan(i1..i2).Trim().ToString();
	}
	
	void _InitConverter() {
		var config = new ReverseMarkdown.Config {
			UnknownTags = Config.UnknownTagsOption.Raise,
			SmartHrefHandling = true,
			//DefaultCodeBlockLanguage = "csharp",
			TableWithoutHeaderRowHandling = Config.TableWithoutHeaderRowHandlingOption.EmptyRow,
		};
		_converter = new Converter(config);
	}
	Converter _converter;
	
	public static void OpenInEditor(string file) {
		run.it(@"C:\Program Files\Microsoft VS Code\Code.exe", $"\"{file}\"");
	}
	
	public static void SaveAndOpenInEditor(string text, string filename) {
		string file = folders.Temp + filename;
		filesystem.saveText(file, text);
		OpenInEditor(file);
	}
	
	public static void OpenInBrowser(string file) {
		//run.it(file); //somehow browser uses much CPU, even Firefox
		run.it("http://localhost:8080/" + file[c_rootDir.Length..].Replace('\\', '/'));
	}
}

class _Exception : Exception {
	public _Exception(string file, string message = "Failed") : base(message) {
		HtmlToMarkdown.OpenInBrowser(file);
		HtmlToMarkdown.OpenInEditor(file);
	}
	
	public _Exception(string file, HtmlNode node) : this(file, node.OuterHtml) { }
}
