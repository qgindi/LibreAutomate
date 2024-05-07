extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Rename;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Au.Controls;
using System.Xml.Linq;
using System.Windows.Documents;
using System.Text.Json.Nodes;

class DSnippets : KDialogWindow {
	public static void ShowSingle() {
		ShowSingle(() => new DSnippets());
	}
	
	List<_Item> _files;
	
	Panel _panelSnippet, _panelContext, _panelFile;
	StackPanel _panelContextEnum;
	KTreeView _tv;
	KSciCodeBox _code;
	TextBox _tName, _tInfo, _tMore, _tPrint, _tUsing, _tMeta, _tVar;
	TextBlock _tbFile, _tbDefaultFileInfo;
	
	[Flags] enum CX { Function = 1, Type = 2, Namespace = 4, Attributes = 16, Line = 32, Any = 64 }
	
	_Item _ti; //current item
	_Item _clip; //cut/copy item
	bool _cut;
	bool _readonly;
	bool _ignoreEvents;
	
	DSnippets() {
		InitWinProp("Snippets", App.Wmain);
		
		var b = new wpfBuilder(this).WinSize(800, 600).Columns(250, 0, -1);
		b.Row(-1);
		
		b.xAddInBorder(_tv = new());
		b.Add<GridSplitter>().Splitter(vertical: true);
		
		b.StartGrid().Columns(-1); //right side
		b.Row(-1);
		
		//snippet
		
		b.StartGrid().Hidden(null);
		_panelSnippet = b.Panel;
		b.R.Add("Name", out _tName).Tooltip("Snippet name. Single word.\nIf ends with \"Surround\", the snippet can be used only for surround.");
		_tName.TextChanged += (_, _) => { if (!_ignoreEvents && _ti.Level == 2) _TvSetText(_tName.TextOrNull()); };
		b.R.Add("Info", out _tInfo);
		_tInfo.TextChanged += (_, _) => { if (!_ignoreEvents) { _ti.info = _tInfo.TextOrNull(); if (_ti.Level == 3) _TvSetText(_ti.info); } };
		b.R.Add("Info+", out _tMore).Multiline(40);
		_tMore.TextChanged += (_, _) => { if (!_ignoreEvents) _ti.more = _tMore.TextOrNull(); };
		b.R.Add("Print", out _tPrint).Multiline(40).Tooltip("Print this text when inserting the snippet.\nCan contain output tags if starts with <>.");
		_tPrint.TextChanged += (_, _) => { if (!_ignoreEvents) _ti.print_ = _tPrint.TextOrNull(); };
		b.R.Add("using", out _tUsing).Tooltip("If the snippet code requires using directives, add the namespace names here.\nExample: System.Windows;System.Windows.Controls");
		_tUsing.TextChanged += (_, _) => { if (!_ignoreEvents) _ti.using_ = _tUsing.TextOrNull(); };
		b.R.Add("Meta", out _tMeta).Tooltip("If the snippet code requires /*/ meta comments /*/, add them here.\nExample: c Example.cs; nuget -\\Example");
		_tMeta.TextChanged += (_, _) => { if (!_ignoreEvents) _ti.meta_ = _tMeta.TextOrNull(); };
		b.R.Add("$var$", out _tVar).Tooltip("$var$ variable type and name. Example: Au.toolbar,t");
		_tVar.TextChanged += (_, _) => { if (!_ignoreEvents) _ti.var_ = _tVar.TextOrNull(); };
		foreach (var v in _panelSnippet.Children) if (v is TextBox t1) t1.IsReadOnlyCaretVisible = true;
		
		b.R.Add<Label>("Code");
		b.StartGrid().Columns(70, 70, 70, 70, 40).Align("R");
		b.AddButton("Paste", _ => _PasteCode(clipboard.text)).Tooltip("Replaces code with the clipboard text without indentation tabs");
		b.AddButton("${n:text}", _ => _InsertVar("${1:}")).Tooltip("Tab stop with text");
		b.AddButton("$n", _ => _InsertVar("$1")).Tooltip("Tab stop without text or with text of the first ${n:text}");
		b.AddButton("$0", _ => _InsertVar("$0")).Tooltip("Final text cursor position");
		b.AddButton("...", _ => _InsertVar(null));
		b.End();
		b.Row(-1).xAddInBorder(out _code);
		_code.AaTextChanged += (_, _) => { if (!_ignoreEvents) _ti.code = _code.aaaText; };
		_code.AaNotify += (KScintilla c, ref Sci.SCNotification n) => {
			if (n.code == Sci.NOTIF.SCN_MODIFYATTEMPTRO && _readonly) dialog.showInfo(null, "Default snippets are read-only, but you can clone a default snippet (right click, copy, paste) and edit the clone. Uncheck the default snippet.", owner: c);
		};
		b.End();
		
		//context
		
		b.And(0).StartStack(true).Hidden(null);
		_panelContext = b.Panel;
		b.StartStack<KGroupBox>("Context", true);
		_panelContextEnum = b.Panel as StackPanel;
		b.End();
		b.End();
		
		//file
		
		b.And(0).StartStack(true).Hidden(null);
		_panelFile = b.Panel;
		b.Add(out _tbFile);
		b.Add(out _tbDefaultFileInfo, "Don't edit this file. The setup program replaces it. Only uncheck some snippets if need.").Wrap();
		b.End();
		
		b.R.AddSeparator(vertical: false);
		b.R.StartOkCancel().AddOkCancel(out var bOK, out var bCancel, out _, apply: "Apply");
		bOK.IsDefault = false; bCancel.IsCancel = false;
		b.AddButton("Import", _ => _ImportMenu()).Width(70);
		b.AddButton("Help", _ => HelpUtil.AuHelp("editor/Snippets")).Width(70);
		b.End();
		
		b.End(); //right side
		b.End();
		
		_tv.HasCheckboxes = true;
		_tv.SingleClickActivate = true;
		_tv.ItemActivated += _tv_ItemActivated;
		_tv.ItemClick += _tv_ItemClick;
		_tv.KeyDown += _tv_KeyDown;
		_FillTree();
		
		//b.Loaded += () => {
		//};
		
		b.OkApply += e => {
			_Save();
		};
	}
	
	void _tv_ItemActivated(TVItemEventArgs e) => _Open(e.Item as _Item);
	
	void _Open(_Item t) {
		_ignoreEvents = true;
		_ti = t;
		_readonly = t.IsReadonly;
		int level = t.Level;
		_panelFile.Visibility = level == 0 ? Visibility.Visible : Visibility.Collapsed;
		_panelContext.Visibility = level == 1 ? Visibility.Visible : Visibility.Collapsed;
		_panelSnippet.Visibility = level >= 2 ? Visibility.Visible : Visibility.Collapsed;
		
		if (level == 0) {
			_tbFile.Inlines.Clear();
			var h = new Hyperlink(new Run(t.filePath));
			h.Click += (_, _) => run.selectInExplorer(t.filePath);
			_tbFile.Inlines.Add(h);
			_tbDefaultFileInfo.Visibility = _readonly ? Visibility.Visible : Visibility.Collapsed;
		} else if (level == 1) {
			CX cx = 0;
			foreach (var v in t.text.Split('|', StringSplitOptions.TrimEntries)) {
				cx |= v switch { "Function" => CX.Function, "Type" => CX.Type, "Namespace" => CX.Namespace, "Attributes" => CX.Attributes, "Line" => CX.Line, "Any" => CX.Any, _ => 0 };
			}
			_panelContextEnum.Children.Clear();
			new EnumUI<CX>(_panelContextEnum, cx);
			_panelContext.IsEnabled = !_readonly;
			if (!_readonly) {
				foreach (CheckBox c in _panelContextEnum.Children) {
					c.Checked += _c_Checked;
					c.Unchecked += _c_Checked;
				}
				void _c_Checked(object sender, RoutedEventArgs e) {
					var s = string.Join('|', _panelContextEnum.Children.OfType<CheckBox>().Where(o => o.IsChecked == true).Select(o => o.Content as string));
					_TvSetText(s); //note: allow empty
				}
			}
		} else {
			bool isMenu = level == 2 && t.IsFolder;
			if (!isMenu) {
				var s = t.code;
				_code.AaSetText(s, _readonly ? 0 : -1);
			}
			
			_tName.Text = level == 2 ? t.text : t.Parent.text;
			_tInfo.Text = level == 2 ? t.info : t.text;
			_tMore.Text = level == 2 ? t.more : t.Parent.more;
			_tPrint.Text = t.print_;
			_tUsing.Text = t.using_;
			_tMeta.Text = t.meta_;
			_tVar.Text = t.var_;
			
			_code.Visibility = isMenu ? Visibility.Hidden : Visibility.Visible;
			//don't disable textboxes. Instead make read-only, to allow scroll/select/copy text.
			_tName.IsReadOnly = level == 3 || _readonly;
			_tInfo.IsReadOnly = _readonly;
			_tMore.IsReadOnly = level == 3 || _readonly;
			_tPrint.IsReadOnly = _readonly;
			_tUsing.IsReadOnly = _readonly;
			_tMeta.IsReadOnly = _readonly;
			_tVar.IsReadOnly = _readonly;
			foreach (var v in _panelSnippet.Children) {
				switch (v) {
				case TextBox t1: //if readonly, display like disabled
					if (t1.IsReadOnly) t1.Background = SystemColors.ControlBrush; else t1.ClearValue(TextBox.BackgroundProperty);
					break;
				case Panel p1:
					p1.IsEnabled = !(_readonly || isMenu);
					break;
				}
			}
		}
		_ignoreEvents = false;
	}
	
	void _tv_ItemClick(TVItemEventArgs e) {
		var t = e.Item as _Item;
		int level = t.Level;
		if (e.Button == MouseButton.Left) {
			//print.it(e.Part);
			if (e.Part == TVParts.Checkbox) {
				t.isChecked ^= true;
				_tv.Redraw(t);
			} else if (e.Part == TVParts.Text) {
				if (level == 2 && t.IsFolder) _tv.Expand(t, true);
			}
		} else if (e.Button == MouseButton.Right) {
			var m = new popupMenu();
			
			if (!t.IsReadonly) {
				int clipLevel = _clip?.Level ?? -1;
				if (level == 0) {
					m["Add new context"] = o => _New(true);
					if (clipLevel == 1) _MiPaste(true);
				} else if (level == 1) {
					m["Add new snippet"] = o => _New(true);
					if (clipLevel == 2) _MiPaste(true);
					m.Separator();
					m["Add new context"] = o => _New(false);
					m["Cut"] = o => _Copy(t, true);
					if (clipLevel == 1) _MiPaste(false);
					_MiDelete();
				} else {
					if (level == 2) {
						m["Add new snippet"] = o => _New(false);
						m["Add new menu item"] = o => _New(true);
					} else {
						m["Add new menu item"] = o => _New(false);
					}
					m.Separator();
					m["Cut"] = o => _Copy(t, true);
					m["Copy"] = o => _Copy(t);
					if (clipLevel >= 2) {
						_MiPaste(false);
						if (level == 2) _MiPaste(true);
					}
					_MiDelete();
				}
				
				void _New(bool into) { _AddNewOrPaste(false, t, into); }
				
				void _MiPaste(bool into) {
					if (!_CanPaste(t, into)) return;
					int le = level; if (into) le++;
					m[le == 3 ? (into ? "Paste as menu item" : "Paste menu item") : le == 2 ? "Paste snippet" : "Paste context"] = o => _AddNewOrPaste(true, t, into);
				}
				
				void _MiDelete() {
					m.Submenu("Delete", m => {
						m[level == 1 ? $"Delete context '{t.text}' and all its snippets" : $"Delete {(level == 2 ? "snippet" : "menu item")} '{t.text}'"] = o => _Remove(t);
					});
				}
				
				if (level <= 1) {
					m.Separator();
					m["Sort snippets"] = o => _Sort(t);
				}
			} else if (level > 1) {
				m["Copy"] = o => _Copy(t);
			}
			
			m.Show(owner: this);
		}
	}
	
	void _Copy(_Item t, bool cut = false) {
		if (_clip != null) _tv.Redraw(_clip);
		_clip = t;
		_cut = cut;
		_tv.Redraw(t); //red etc
	}
	
	bool _CanPaste(_Item t, bool into) {
		int level = t.Level + (into ? 1 : 0), clipLevel = _clip?.Level ?? -1;
		if (clipLevel != level && !(level == 3 && clipLevel == 2 && !_clip.IsFolder)) return false;
		if (_clip == t.Parent || (_cut && _clip == t)) return false;
		return true;
	}
	
	//rejected: select/copy multiple. Rarely need. Can edit file text.
	
	void _AddNewOrPaste(bool paste, _Item anchor, bool into) {
		int level = anchor.Level + (into ? 1 : 0);
		_Item t;
		if (paste) {
			if (_cut) {
				t = _clip;
				if (level == 3 && t.Level == 2) {
					if (!t.more.NE()) print.it($"{t.text} Info+ was:\r\n{t.more}");
					if (!t.info.NE()) t.text = t.info;
					t.info = t.more = null;
				}
				_Remove(t, cut: true);
			} else {
				Debug.Assert(level > 1);
				t = new _Item(this, level, _clip.text);
				t.code = _clip.code;
				if (level == 2) {
					t.info = _clip.info;
					t.more = _clip.more;
				} else {
					if (_clip.Level == 2 && !_clip.info.NE()) t.text = _clip.info;
				}
				t.print_ = _clip.print_;
				t.using_ = _clip.using_;
				t.meta_ = _clip.meta_;
				t.var_ = _clip.var_;
				t.isChecked = _clip.isChecked;
				foreach (var v in _clip.Children()) {
					var c = new _Item(this, 3, v.text) { code = v.code, print_ = v.print_, using_ = v.using_, meta_ = v.meta_, var_ = v.var_ };
					t.AddChild(c);
				}
			}
		} else {
			t = new _Item(this, level, level == 1 ? "Any" : level == 2 ? "Snippet" : "");
			if (level == 2) t.isChecked = true;
		}
		
		if (level == 3 && into && !anchor.IsFolder) { //convert anchor to menu
			var u = new _Item(this, level, anchor.info?.TrimEnd('.')) { code = anchor.code };
			anchor.code = null;
			anchor.AddChild(u);
		}
		
		if (into) anchor.AddChild(t); else anchor.AddSibling(t, false);
		
		_tv.SetItems(_files, true);
		
		if (paste && _cut) {
			
		} else {
			if (into) _tv.Expand(anchor, true);
			_SelectAndOpen(t);
			var c = level == 2 ? _tName : level == 3 ? _tInfo : null;
			if (c != null) {
				c.Focus();
				if (paste) c.SelectAll();
			}
		}
	}
	
	void _SelectAndOpen(_Item t) {
		_tv.SelectSingle(t, andFocus: true);
		_tv.EnsureVisible(t);
		if (t != _ti) _Open(t);
	}
	
	void _Remove(_Item t, bool cut = false) {
		_clip = null;
		var p = t.Parent;
		t.Remove();
		
		if (p.Level == 2 && p.Count == 1) { //convert p to non-menu
			var u = p.FirstChild;
			p.code = u.code;
			p.info ??= u.text;
			p.more ??= u.more;
			p.print_ ??= u.print_;
			p.using_ ??= u.using_;
			p.meta_ ??= u.meta_;
			p.var_ ??= u.var_;
			u.Remove();
		}
		
		if (!cut) _tv.SetItems(_files, true);
		
		if (p.Level == 2 && !cut) {
			_SelectAndOpen(p);
		} else if (_ti == t) {
			_SelectNone();
		}
	}
	
	void _SelectNone() {
		_ti = null;
		_clip = null;
		_panelFile.Visibility = Visibility.Collapsed;
		_panelContext.Visibility = Visibility.Collapsed;
		_panelSnippet.Visibility = Visibility.Collapsed;
	}
	
	//rejected: a main menu command or hotkey here to quickly add new snippet from selected text or clipboard text.
	//	Cannot know where and what snippet type (snippet or menu item) to add.
	//void _QuickAddSnippet() {
	
	//}
	
	void _Sort(_Item t) {
		if (t.Level == 1) _Context(t); else if (t.Level == 0) foreach (var v in t.Children()) _Context(v);
		_tv.SetItems(_files, modified: true);
		
		void _Context(_Item t) {
			var a = t.Children().OrderBy(o => o.text, StringComparer.OrdinalIgnoreCase).ToArray();
			foreach (var v in a) v.Remove();
			foreach (var v in a) t.AddChild(v);
		}
	}
	
	void _InsertVar(string s) {
		var text = _code.aaaText;
		int selStart = _code.aaaSelectionStart16, selEnd = _code.aaaSelectionEnd16;
		bool inPlaceholder = selStart >= 4 && selEnd < text.Length && text[selEnd] == '}' && text[selStart - 1] == ':' && text.LastIndexOf('$', selStart - 2) is int i1 && i1 >= 0 && text.RxIsMatch(@"\G\$\{\d+:\z", range: i1..selStart);
		
		if (s != null) {
			if (inPlaceholder) return;
			if (s == "${1:}") {
				int n = 1; while (text.RxIsMatch($@"\$(?:{{{n}[:}}]|{n}(?!\d))")) n++;
				s = $"${{{n}:{_code.aaaSelectedText()}}}";
			}
			_code.aaaReplaceSel(s);
			if (s.RxIsMatch(@"^\$\{\d+:\}$")) _code.Call(Sci.SCI_CHARLEFT);
			else if (s == "$1") _code.Call(Sci.SCI_CHARLEFTEXTEND);
			_code.Focus();
		} else {
			var m = new popupMenu();
			Action<PMItem> aIns = o => {
				var s = o.Text;
				if (inPlaceholder) s = "$" + s[2..^1];
				_code.aaaReplaceSel(s);
				_code.Focus();
			};
			m["${VAR}"] = aIns;
			m["${UUID}"] = aIns;
			m["${RANDOM}"] = aIns;
			m["${RANDOM_HEX}"] = aIns;
			m["${TM_FILENAME_BASE}"] = aIns;
			m["${TM_SELECTED_TEXT}"] = aIns;
			m.Show();
		}
	}
	
	void _PasteCode(string s) {
		if (s.NE()) return;
		var a = s.Lines();
		int indent = (int)a.Min(o => (uint)o.FindNot("\t"));
		s = string.Join("\r\n", a.Select(o => o[indent..]));
		_code.aaaReplaceRange(false, 0, -1, s);
	}
	
	void _TvSetText(string s) {
		_ti.text = s;
		_tv.Redraw(_ti, true);
	}
	
	static HashSet<string> _GetHiddenSnippets(string fileName, bool orAdd = false) {
		var files = App.Settings.ci_hiddenSnippets ??= (orAdd ? new() : null);
		if (files == null) return null;
		fileName = fileName.ToLower();
		if (files.TryGetValue(fileName, out var r)) return r;
		if (orAdd) files.Add(fileName, r = new());
		return r;
	}
	
	public static HashSet<string> GetHiddenSnippets(string fileName) => _GetHiddenSnippets(fileName);
	
	static void _SaveHiddenSnippets(string fileName, HashSet<string> hs) {
		if (hs.Count > 0) {
			(App.Settings.ci_hiddenSnippets ??= new())[fileName.ToLower()] = hs;
		} else {
			App.Settings.ci_hiddenSnippets?.Remove(fileName.ToLower());
		}
	}
	
	void _FillTree() {
		string snippetsDir, defSnippets;
#if SCRIPT
		snippetsDir = @"C:\Users\G\Documents\LibreAutomate\.settings";
		defSnippets = @"C:\code\au\_\Default\Snippets.xml";
#else
		snippetsDir = AppSettings.DirBS;
		defSnippets = CiSnippets.DefaultFile;
		if (!filesystem.exists(CiSnippets.CustomFile).File) {
			try { filesystem.saveText(CiSnippets.CustomFile, "<snippets><group context=\"Function\"/></snippets>"); }
			catch { }
		}
#endif
		_files = new();
		
		foreach (var f in filesystem.enumFiles(snippetsDir, "*Snippets.xml")) _File(f.FullPath, f.Name, false);
		_File(defSnippets, "default", true);
		
		_tv.SetItems(_files);
		
		void _File(string path, string name, bool isDefault) {
			try {
				var hidden = _GetHiddenSnippets(name);
				var xf = XmlUtil.LoadElem(path);
				bool oldFormat = xf.Name == "Au.Snippets"; //"snippets" if new format
				var tf = new _Item(this, 0, name) { filePath = path };
				if (hidden == null || !hidden.Contains("")) tf.isChecked = true;
				_files.Add(tf);
				foreach (var xg in xf.Elements("group")) {
					if (!xg.Attr(out string context, "context")) continue;
					if (context.Contains("Arrow")) context = string.Join('|', context.Split('|').Where(o => o != "Arrow")).NullIfEmpty_() ?? "Function"; //fbc
					var tg = new _Item(this, 1, context);
					tf.AddChild(tg);
					var e = xg.Elements("snippet");
					if (isDefault) e = e.OrderBy(o => o.Attr("name")); //rejected: order in all files. Instead there is a context menu command "Sort".
					foreach (var xs in e) {
						if (oldFormat) CiSnippets.ConvertOldFormat_(xs);
						var ts = new _Item(this, 2, xs.Attr("name")) {
							info = xs.Attr("info"),
							more = xs.Attr("more"),
							print_ = xs.Attr("print"),
							using_ = xs.Attr("using"),
							meta_ = xs.Attr("meta"),
							var_ = xs.Attr("var")
						};
						if (hidden == null || !hidden.Contains(ts.text)) ts.isChecked = true;
						tg.AddChild(ts);
						if (xs.HasElements) {
							foreach (var xi in xs.Elements("list")) {
								var ti = new _Item(this, 3, xi.Attr("item")) { code = xi.Value, print_ = xi.Attr("print"), using_ = xi.Attr("using"), meta_ = xi.Attr("meta"), var_ = xi.Attr("var") };
								ts.AddChild(ti);
							}
						} else {
							ts.code = xs.Value;
						}
					}
				}
				tf.fileXml = _ToXml(tf).ToString(); //to detect when need to save
			}
			catch (Exception e1) { print.it(e1.ToStringWithoutStack()); }
		}
	}
	
	XElement _ToXml(_Item f) {
		var xf = new XElement("snippets");
		foreach (var g in f.Children()) {
			var xg = new XElement("group", new XAttribute("context", g.text)); xf.Add(xg);
			foreach (var s in g.Children()) {
				var xs = new XElement("snippet", new XAttribute("name", s.text ?? ""), new XAttribute("info", s.info ?? "")); xg.Add(xs);
				if (s.more != null) xs.SetAttributeValue("more", s.more);
				_Snippet(s, xs, false);
				foreach (var i in s.Children()) {
					var xi = new XElement("list", new XAttribute("item", i.text ?? "")); xs.Add(xi);
					_Snippet(i, xi, true);
				}
			}
		}
		return xf;
		
		static void _Snippet(_Item t, XElement x, bool li) {
			if (t.code != null) {
				x.Add("\n");
				x.Add(new XCData(t.code));
				x.Add(li ? "\n      " : "\n    ");
			}
			if (t.print_ != null) x.SetAttributeValue("print", t.print_);
			if (t.using_ != null) x.SetAttributeValue("using", t.using_);
			if (t.meta_ != null) x.SetAttributeValue("meta", t.meta_);
			if (t.var_ != null) x.SetAttributeValue("var", t.var_);
		}
	}
	
	void _Save() {
		foreach (var f in _files) {
			if (f.text != "default") {
				var xf = _ToXml(f);
				var xml = xf.ToString();
				if (xml != f.fileXml) {
					f.fileXml = xml;
					filesystem.saveText(f.filePath, xml);
				}
			}
			var hs = f.Descendants().Where(o => o.Level == 2 && !o.isChecked).Select(o => o.text).ToHashSet();
			if (!f.isChecked) hs.Add("");
			_SaveHiddenSnippets(f.text, hs);
		}
		CiSnippets.Reload();
	}
	
	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		if (e.Key == Key.F1 && Keyboard.Modifiers == 0) {
			HelpUtil.AuHelp("editor/Snippets");
			e.Handled = true;
			return;
		}
		base.OnPreviewKeyDown(e);
	}
	
	class _Item : TreeBase<_Item>, ITreeViewItem {
		DSnippets _d;
		public string text; //displayed text. Depends on level: 0 filename or "default", 1 context, 2 snippet name, 3 snippet item info
		public string filePath, fileXml; //level 0
		public string code, info, more, print_, using_, meta_, var_; //level 2 or 3
		public bool isChecked;
		bool _isExpanded;
		
		public _Item(DSnippets d, int level, string text) {
			_d = d;
			this.text = text;
			_isExpanded = level <= 1;
		}
		
		public bool IsReadonly => RootAncestor.text == "default";
		
		#region ITreeViewItem
		
		void ITreeViewItem.SetIsExpanded(bool yes) { _isExpanded = yes; }
		
		public bool IsExpanded => _isExpanded;
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		
		public bool IsFolder => base.HasChildren;
		
		string ITreeViewItem.DisplayText => text;
		
		object ITreeViewItem.Image => Level switch {
			0 => "*Modern.PageXml" + Menus.black,
			1 => EdResources.FolderIcon(_isExpanded),
			2 => IsFolder ? "*Codicons.SymbolSnippet #00A000|#00E000" : "*Codicons.SymbolSnippet #0060F0|#80C0FF",
			_ => "*Material.Asterisk #0060F0|#80C0FF",
		};
		
		TVCheck ITreeViewItem.CheckState => Level is not (0 or 2) ? TVCheck.None : isChecked ? TVCheck.Checked : TVCheck.Unchecked;
		
		int ITreeViewItem.Color(TVColorInfo ci) => (Level == 1 && this != _d._tv.SelectedItem) ? 0xC0E0A0 : -1;
		
		int ITreeViewItem.TextColor(TVColorInfo ci)
			=> this == _d._clip ? (_d._cut ? 0xFF0000 : 0x00A000)
			: Level == 1 ? 0
			: -1;
		
		#endregion
	}
	
	void _tv_KeyDown(object sender, KeyEventArgs e) {
		var k = (e.KeyboardDevice.Modifiers, e.Key);
		switch (k) {
		case (0, Key.Escape):
			if (_clip != null) {
				_clip = null;
				_tv.Redraw();
			}
			goto gh;
		}
		if (_tv.FocusedItem is _Item t) {
			switch (k) {
			case (0, Key.Delete):
				if (!t.IsReadonly && t.Level >= 2 && dialog.showOkCancel("Delete?", t.text, owner: this)) _Remove(t);
				break;
			case (ModifierKeys.Control, Key.X):
				if (!t.IsReadonly && t.Level >= 1) _Copy(t, true);
				break;
			case (ModifierKeys.Control, Key.C):
				if (t.Level >= 2) _Copy(t);
				break;
			case (ModifierKeys.Control, Key.V):
				if (!t.IsReadonly && _clip != null) {
					bool into = _clip.Level > t.Level;
					if (_CanPaste(t, into)) _AddNewOrPaste(true, t, into);
				}
				break;
			default: return;
			}
		}
		gh:
		e.Handled = true;
	}
	
	#region import
	
	void _ImportMenu() {
		var m = new popupMenu();
		m["Import VS snippets..."] = o => _ImportVS();
		m["Import VSCode snippets..."] = o => _ImportJson();
		m.Show(owner: this);
	}
	
	void _SaveImported(XElement x) {
		string dir = AppSettings.DirBS, file = null;
		for (int i = 1; ; i++) {
			file = AppSettings.DirBS + "Imported" + i + " snippets.xml";
			if (!filesystem.exists(file)) {
				x.Save(file);
				break;
			}
		}
		_SelectNone();
		_FillTree();
		if (_files.FirstOrDefault(o => o.filePath == file) is { } f) {
			_tv.SelectSingle(f, andFocus: true, scrollTop: true);
			_Open(f);
		}
		CiSnippets.Reload();
	}
	
	void _ImportVS() {
		var d = new FileOpenSaveDialog() { FileTypes = "VS snippets|*.snippet", Title = "Import snippet files", FileNameLabel = "Selected files:" };
		if (!d.ShowOpen(out string[] files, owner: this)) return;
		try {
			var x = _ImportVS(files);
			_SaveImported(x);
		}
		catch (Exception ex) { dialog.showError("Failed to import snippets.", ex.ToString(), owner: this); }
	}
	
	void _ImportJson() {
		var d = new FileOpenSaveDialog() { FileTypes = "VSCode snippets|*.json", Title = "Import snippets file" };
		if (!d.ShowOpen(out string file, owner: this)) return;
		try {
			var x = _ImportJson(file);
			_SaveImported(x);
		}
		catch (Exception ex) { dialog.showError("Failed to import snippets.", ex.ToString(), owner: this); }
	}
	
	static XElement _ImportVS(IEnumerable<string> files) {
		var xRoot = new XElement("snippets");
		foreach (var f in files) {
			try { _AddFile(f); }
			catch (Exception ex) { print.it($"Failed to convert {pathname.getName(f)}. {ex}"); }
		}
		return xRoot;
		
		void _AddFile(string file) {
			var xSnippets = XElement.Load(file);
			//print.it($"<><bc green>{file}<>\r\n{xSnippets}");
			var ns = xSnippets.Name.Namespace;
			foreach (var xSnippet in xSnippets.Elements(ns + "CodeSnippet")) {
				if (xSnippet.Attr("Format") is string format && !format.Starts("1.")) { print.warning($"Snippet format {format} not supported."); continue; }
				var xs = xSnippet.Element(ns + "Snippet");
				var s = xs.Elem(ns + "Code", "Language", "csharp")?.Value;
				if (s.NE()) continue;
				
				List<(string id, string def)> ad = null;
				if (xs.Element(ns + "Declarations") is { } xDecls) {
					foreach (var xl in xDecls.Elements(ns + "Literal")) {
						var def = xl.Element(ns + "Default")?.Value;
						if (def == null) {
							if (xl.Element(ns + "Function")?.Value is string k && k.Like("SimpleTypeName(*)")) {
								k = k[15..^1];
								if (k.Starts("global::")) k = k[8..];
								if (k.Starts("System.") && k.IndexOf('.', 7) < 0) k = k[7..];
								def = k;
							}
						}
						var id = xl.Element(ns + "ID").Value;
						//if (xl.Attr("Editable") == "false") print.it(xl);
						if (xl.Attr("Editable") == "false" && !def.NE()) s = s.Replace($"${id}$", def);
						else (ad ??= new()).Add((id, def ?? "EDIT"));
					}
				}
				s = s.Replace("$selected$ $end$", "$selected$$end$");
				if (s.Find("$end$") is int i1 && i1 >= 0 && i1 + 5 is int i1end) s = s.ReplaceAt(i1..i1end, s.Length > i1end && s[i1end] is >= '0' and <= '9' ? "${0}" : "$0");
				s = s.Replace("$selected$", "${TM_SELECTED_TEXT}");
				if (ad != null) {
					int n = 1;
					foreach (var v in ad) s = s.Replace($"${v.id}$", $"${{{n++}:{v.def}}}");
				}
				
				var xh = xSnippet.Element(ns + "Header");
				
				var x = _ImportAddXElement(xRoot, s, xh.Element(ns + "Shortcut").Value, xh.Element(ns + "Description")?.Value);
				if (xs.Element(ns + "Imports") is { } xImps) {
					StringBuilder bu = new();
					foreach (var xi in xImps.Elements(ns + "Import")) {
						if (xi.Element(ns + "Namespace") is { } xns) bu.Append(bu.Length > 0 ? "; " : null).Append(xns.Value);
					}
					if (bu.Length > 0) x.SetAttributeValue("using", bu.ToString());
				}
			}
		}
	}
	
	static XElement _ImportJson(string file) {
		var xRoot = new XElement("snippets");
		
		var jRoot = JsonNode.Parse(filesystem.loadText(file));
		//print.it(jRoot.ToJsonString(new() { WriteIndented = true }));
		foreach (var jSnippet in jRoot as JsonObject) {
			var info = jSnippet.Key;
			try {
				var j = jSnippet.Value.AsObject();
				var code = _ToString(j["body"]);
				var more = _ToString(j["description"]);
				var n = j["prefix"];
				if (n is JsonArray ap) {
					foreach (var v in ap) _Add((string)v);
				} else {
					_Add((string)n);
				}
				
				void _Add(string prefix) {
					_ImportAddXElement(xRoot, code, prefix, info, more);
				}
				
				static string _ToString(JsonNode n) {
					if (n is JsonArray a) return string.Join("\r\n", a.GetValues<string>());
					return (string)n;
				}
			}
			catch (Exception ex) { print.it($"Failed to convert {info}. {ex}"); }
		}
		
		return xRoot;
	}
	
	static XElement _ImportAddXElement(XElement xRoot, string code, string namePrefix, string info, string more = null) {
		var context = DetectContext_(code);
		var xContext = xRoot.Elem("group", "context", context);
		if (xContext == null) { xRoot.Add(xContext = new XElement("group", new XAttribute("context", context))); }
		
		var x = new XElement("snippet", "\n", new XCData(code ?? ""), "\n    ");
		x.SetAttributeValue("name", namePrefix.RxReplace(@"[^\p{Xan}_]+", "_") + "Snippet");
		x.SetAttributeValue("info", info);
		if (!more.NE() && more != info) x.SetAttributeValue("more", more);
		xContext.Add(x);
		return x;
	}
	
	internal static string DetectContext_(string code, bool debug = false) {
		try {
			if (0 != code.Starts(false, "<", "Microsoft Visual Studio Solution File", "@")) return "None"; //project or solution file, XML, razor
			
			code = code.RxReplace(@"\$(?|(TM_FILENAME_BASE|RANDOM)\b|\{((?1))\})", "$1");
			code = code.RxReplace(@"\$(?|([1-9]\d*)(?!\d)|\{((?1))\})", "i");
			code = code.RxReplace(@"\$(?:\{[A-Z_]+\}|[A-Z_]+\b|0|\{0\})", "");
			code = code.RxReplace(@"\$\{\d+:(.*?)\}", "$1");
			
			if (debug) print.it(code);//TODO
			
			var cu = CiUtil.GetSyntaxTree(code);
			if (cu.Usings.Any() || cu.Externs.Any() || cu.AttributeLists.Any()) return "Namespace";
			if (!cu.Members.Any()) {
				if (cu.ContainsDirectives) return "Any|Line";
				if (code.Starts("/// ")) return "Namespace|Type|Line";
				return "Any";
			}
			if (cu.Members.Any(SyntaxKind.GlobalStatement)) {
				if (!cu.Members.All(o => o is GlobalStatementSyntax)) {
					if (cu.Members[0] is GlobalStatementSyntax) return "Namespace"; //TLS + types
					return "Function";
				}
				foreach (GlobalStatementSyntax gs in cu.Members) {
					var stat = gs.Statement;
					if (stat is LocalFunctionStatementSyntax) { //can be local or member function
						if (_TryInClass()) return "Type"; //"The modifier 'modifier' is not valid for this item" (eg 'public void...`)
					} else if (stat is LocalDeclarationStatementSyntax lds && !lds.Declaration.Type.IsVar) { //can be local or member variable
					} else {
						if (_TryInClass()) return "Type";
						return "Function";
					}
				}
				return "Type|Function";
			} else {
				if (cu.Members.Any(o => o is BaseNamespaceDeclarationSyntax)) return "Namespace";
				if (cu.Members.All(o => o is BaseTypeDeclarationSyntax)) return "Namespace|Type|Line";
				return "Type";
			}
			
			bool _TryInClass() {
				try {
					int n1 = cu.GetDiagnostics().Count();
					if (n1 == 0) return false;
					var cu2 = CiUtil.GetSyntaxTree("class C{\r\n" + code + "\r\n}");
					int n2 = cu2.GetDiagnostics().Count();
					return n2 < n1;
				}
				catch { return false; }
			}
		}
		catch { return "Any"; }
	}
	
	#endregion
}
