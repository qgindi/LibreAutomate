using System.Windows.Controls;
using Au.Controls;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Extensions;

class PanelOutline : DockPanel
{
	KTreeView _tv;
	SciCode _activeDoc;
	bool _modified;
	bool _once;
	_Item _oldTree;

	public PanelOutline() {
		//this.UiaSetName("Outline panel");

		_tv = new KTreeView { Name = "Outline_list", SingleClickActivate = true, HotTrack = true };
		_tv.ItemClick += (_, e) => { if (e.MouseButton == System.Windows.Input.MouseButton.Right) _ContextMenu(); };
		_tv.ContextMenuOpening += (_, _) => _ContextMenu(); //right-click in empty space
		this.Children.Add(_tv);
	}

	public void Timer025sWhenVisible() {
		if (_activeDoc == null || _modified) Update(); else if (!this.IsVisible) Clear();
		//if (_oldTree != null) if (0 != (4 & App.Settings.outline_flags)) _Sync();
	}

	public void SciModified() {
		if (_activeDoc != null) _modified = true;
	}

	public void Update() {
		if (this.IsVisible) {
			try { if (_Update()) return; }
			catch (Exception e1) { print.it(e1); }
		}
		Clear();
	}

	bool _Update() {
		//print.it("update");
		//using var p1 = perf.local();
		if (!CodeInfo.GetContextAndDocument(out var cd, 0, metaToo: true)) return false;
		var cu = cd.syntaxRoot;

		var root = new _Item();

		//at first get regions
		if (cu.ContainsDirectives) {
			_Item regions = null;
			for (var d = cu.GetFirstDirective(); d != null; d = d.GetNextDirective()) {
				if (!d.IsKind(SyntaxKind.RegionDirectiveTrivia) || !d.IsActive) continue;
				var s = d.EndOfDirectiveToken.LeadingTrivia.ToString(); if (s.NE()) continue;
				if (regions == null) root.AddChild(regions = new("#region", CiItemKind.Region, 0));
				regions.AddChild(new(s, CiItemKind.Region, d.SpanStart));
			}
		}

		//then get member declarations
		var members = cu.Members;
		while (members.Count == 1) {
			var f = members.First();
			if (f is BaseNamespaceDeclarationSyntax nd) members = nd.Members;
			else if (f is TypeDeclarationSyntax td) members = td.Members;
			else break;
		}
		//p1.Next('d');
		_Members(root, members, 0);
		//p1.Next('m');

		void _Members(_Item parent, SyntaxList<MemberDeclarationSyntax> members, int level) {
			int sort = App.Settings.outline_flags & 3;
			List<_Item> a = sort != 0 ? new() : null;
			_Item locals = null;
			foreach (var m in members) {
				if (level == 0 && m is GlobalStatementSyntax g) {
					if (g.Statement is not LocalFunctionStatementSyntax d) continue;
					if (locals == null) parent.AddChild(locals = new("Local functions", CiItemKind.LocalMethod, 0));
					var k = new _Item(d);
					locals.AddChild(k);
				} else {
					var k = new _Item(m);
					if (a != null) a.Add(k); else parent.AddChild(k);
					switch (m) {
					case BaseNamespaceDeclarationSyntax d:
						_Members(k, d.Members, level + 1);
						break;
					case TypeDeclarationSyntax d:
						_Members(k, d.Members, level + 1);
						break;
					}
				}
			}
			if (a != null) {
				a.Sort((i1, i2) => {
					if (sort >= 2) {
						int k1 = (int)i1._kind, k2 = (int)i2._kind;
						if (k1 != k2) return k1 - k2;
						//if (sort == 2) return i1._pos - i2._pos;
					}
					string s1 = i1._text, s2 = i2._text;
					bool a1 = s1[0].IsAsciiAlpha(), a2 = s2[0].IsAsciiAlpha();
					if (a1 != a2) return a1 ? -1 : 1;
					return string.CompareOrdinal(s1, s2);
				});
				foreach (var v in a) parent.AddChild(v);
			}
		}

		_modified = false;
		_activeDoc = cd.sci;
		//_oldDocument = cd.document;

		int changed = 4;
		if (_oldTree != null) {
			changed = _TreeChanged(_oldTree, root);
			//print.it(changed);
			if (changed == 0) {
				_UpdatePos(_oldTree, root);
				return true;
			}
		}
		_oldTree = root;
		//p1.Next('c');

		if (!_once) {
			_once = true;
			_tv.ItemActivated += (_, e) => {
				_activeDoc.Focus();
				var v = e.Item as _Item;
				_activeDoc.zGoToPos(true, v._pos);
			};
			Panels.Editor.ZActiveDocChanged += () => Clear();
		}

		_tv.SetItems(root.Children(), modified: changed != 4);
		return true;

		//0 same, 1 changed
		int _TreeChanged(_Item old, _Item now) {
			int R = 0;
			if (old.HasChildren || now.HasChildren) {
				for (_Item o = old.FirstChild, n = now.FirstChild; ; o = o.Next, n = n.Next) {
					if ((o == null) != (n == null)) return 1; //added or removed at the end
					if (o == null) break;
					if (!n.Eq(o)) {
						//is just changed text of this, or something added or removed in the moddle? If added/removed, need to update _isExpanded.
						R |= 1;
						_Item o1 = o.Next, n1 = n.Next;
						if (o1 != null && n1 != null) { //else added or removed at the end
							if (!n1.Eq(o1)) { //else just changed text
								for (; n1 != null; n1 = n1.Next) if (n1.Eq(o)) { n = n1; goto g1; } //inserted 1 or more
								for (; o1 != null; o1 = o1.Next) if (o1.Eq(n)) { o = o1; goto g1; } //removed 1 or more
								return 1;
								g1:;
							}
						}
					}
					if (o.HasChildren || n.HasChildren) {
						R |= _TreeChanged(o, n);
					}
				}
				now._isExpanded = old._isExpanded;
			}
			return R;
		}

		void _UpdatePos(_Item old, _Item now) {
			for (_Item o = old.FirstChild, n = now.FirstChild; o != null; o = o.Next, n = n.Next) {
				o._pos = n._pos;
				if (o.HasChildren) _UpdatePos(o, n);
			}
		}
	}

	public void Clear() {
		if (_activeDoc == null) return;
		_activeDoc = null;
		_oldTree = null;
		_modified = false;
		_tv.SetItems(null);
	}

	//unfinished, rejected, maybe in the future. Can't use just _pos. Need to get and update full spans.
	//	Maybe better add "manual sync" button instead of "auto sync" option.
	//void _Sync() {
	//	Debug.Assert(this.IsVisible && _oldTree != null);
	//	//print.it("sync");

	//	int pos = Panels.Editor.ZActiveDoc.zCurrentPos16; //todo: return if didn't change (use the position changed notification).
	//	_Item found = null;
	//	if (0 == (3 & App.Settings.outline_flags)) { //not sorted
	//		found = _Find(_oldTree);

	//		_Item _Find(_Item parent) {
	//			for (var v = parent.FirstChild; v != null; v = v.Next) {
	//				if (v._pos >= pos) return v;
	//				if (v.HasChildren) {
	//					var r = _Find(v);
	//					if (r != null) return r;
	//				}
	//			}
	//			return null;
	//		}
	//	}
	//	if (found != null) {
	//		print.it(found._text);

	//	}
	//}

	void _ContextMenu() {
		var m = new popupMenu();
		int flags = App.Settings.outline_flags, sort = flags & 3;
		m.AddRadio("Don't sort", sort == 0).Id = 1;
		m.AddRadio("Sort by name", sort == 1).Id = 2;
		m.AddRadio("Sort by kind and name", sort == 2).Id = 3;
		//m.AddRadio("Sort by kind and name", sort == 3).Id = 4; //sorting by kind+position probably not useful. Always sort by kind+name.
		//m.Separator();
		//m.AddCheck("Sync", 0 != (flags & 4), _ => App.Settings.outline_flags ^= 4);
		int i = m.Show(owner: this);
		if (i is >= 1 and <= 3) {
			App.Settings.outline_flags = (byte)((App.Settings.outline_flags & ~3) | (i - 1));
			if (_oldTree != null) {
				_oldTree = null;
				Update();
			}
		}
	}

	class _Item : TreeBase<_Item>, ITreeViewItem
	{
		internal string _text;
		internal int _pos;
		internal CiItemKind _kind;
		internal bool _isExpanded = true;

		public _Item() { } //root

		public _Item(string text, CiItemKind kind, int pos) {
			_text = text;
			_pos = pos;
			_kind = kind;
		}

		public _Item(MemberDeclarationSyntax m) {
			//CiUtil.PrintNode(m);
			string name;
			if (m is BaseFieldDeclarationSyntax fd) { //field, event
				var vd = fd.Declaration;
				var a = vd.Variables;
				if (a.Count == 1) name = a[0].Identifier.Text + " : " + vd.Type;
				else name = string.Join(", ", a.Select(o => o.Identifier.ToString())) + " : " + vd.Type;
			} else if (m is BaseNamespaceDeclarationSyntax nd) {
				name = nd.Name.ToString();
			} else if (m is TypeDeclarationSyntax td) {
				name = td.Identifier.Text + td.TypeParameterList;
			} else {
				name = m switch {
					DestructorDeclarationSyntax dd => "~" + dd.Identifier.Text,
					ConversionOperatorDeclarationSyntax od => $"{od.ImplicitOrExplicitKeyword.Text} {od.Type}",
					_ => m.GetNameToken().Text
				};

				if (m is MethodDeclarationSyntax md) name = $"{name}{md.TypeParameterList}({_Function(md.ParameterList.Parameters)}) : {md.ReturnType}";
				else if (m is OperatorDeclarationSyntax od) name = $"{name}({_Function(od.ParameterList.Parameters)}) : {od.ReturnType}";
				else if (m is BaseMethodDeclarationSyntax bmd) name = $"{name}({_Function(bmd.ParameterList.Parameters)})";
				else if (m is IndexerDeclarationSyntax id) name = $"{name}[{_Function(id.ParameterList.Parameters)}]";
				else if (m is DelegateDeclarationSyntax odd) name = $"{name}{odd.TypeParameterList}({_Function(odd.ParameterList.Parameters)}) : {odd.ReturnType}";

				if (m is BasePropertyDeclarationSyntax pd) name = $"{name} : {pd.Type}"; //property, indexer, event

				static string _Function(SeparatedSyntaxList<ParameterSyntax> a) {
					int n = a.Count; if (n == 0) return null;
					if (n == 1) return a[0].Type.ToString();
					return string.Join(", ", a.Select(o => o.Type));
				}
			}

			_text = name;
			_kind = CiUtil.MemberDeclarationToKind(m);
			_pos = m.SpanStart;
		}

		public _Item(LocalFunctionStatementSyntax m) {
			_text = m.Identifier.Text;
			_kind = CiItemKind.Method;
			_pos = m.SpanStart;
		}

		public bool Eq(_Item b) => _text == b._text && _kind == b._kind;

		#region ITreeViewItem

		string ITreeViewItem.DisplayText => _text;

		string ITreeViewItem.ImageSource => CiComplItem.ImageResource(_kind);

		void ITreeViewItem.SetIsExpanded(bool yes) { _isExpanded = yes; }

		bool ITreeViewItem.IsExpanded => _isExpanded;

		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();

		bool ITreeViewItem.IsFolder => _IsFolder;
		bool _IsFolder => base.HasChildren;

		//bool ITreeViewItem.IsBold => _IsFolder;

		#endregion
	}
}
