extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;

using Au.Controls;
using System.Windows.Input;

partial class PanelDebug {
	KTreeView _tvVariables;
	_VariablesViewItem[] _aVar;
	_VariablesViewItem _watch;
	
	void _VariablesViewInit() {
		_tvVariables = new() { Name = "Variables_list", SingleClickActivate = true };
		_tvVariables.ItemClick += _tvVariables_ItemClick;
	}
	
	void _ListVariables() {
		_d.Send($"-stack-list-variables --thread {_s.threadId} --frame {_s.frame.level}");
	}
	
	//Called when the user selects a frame or thread.
	//Calls -var-delete for all _aVar items.
	//Not really necessary, just frees some memory in the debugger process now. The debugger itself deletes all variables on step/continue.
	void _VariablesViewChangedFrameOrThread() {
		if (_aVar == null) return;
		foreach (var v in _aVar) v.VarDelete();
	}
	
	void _VariablesViewSetItems(_VARIABLE[] a) {
		if (a.NE_()) {
			_aVar = a != null ? new _VariablesViewItem[2] : null;
		} else {
			_aVar = new _VariablesViewItem[a.Length + 2];
			for (int i = 0; i < a.Length; i++) {
				_aVar[i + 2] = new(this, a[i]);
			}
		}
		
		if (_aVar != null) {
			_aVar[0] = new _VariablesViewItem(this, _VVItemType.Mouse);
			if (_watch == null) {
				_aVar[1] = _watch = new(this, _VVItemType.Watch);
			} else {
				_aVar[1] = _watch;
				foreach (var v in _watch.Children) v.UpdateWatch();
			}
		}
		
		_tvVariables.SetItems(_aVar);
	}
	
	//called by CiQuickInfo when mouse hovers on a non-literal token
	internal void SciMouseHover_(CodeInfo.Context cd) {
		if (!IsStopped || _aVar.NE_()) return;
		
		//get expression string
		//It can be a variable/field/property name, x.y, x[y] or similar.
		
		string exp = null;
		int pos = cd.pos;
		var tok = cd.syntaxRoot.FindToken(pos);
		var node = tok.Parent;
		//CiUtil.PrintNode(tok);
		//CiUtil.PrintNode(node);
		var tk = tok.Kind();
		if (tk == SyntaxKind.IdentifierToken) {
			if (node is VariableDeclaratorSyntax vds) {
				if (vds.Identifier.Span.ContainsOrTouches(pos)) exp = tok.Text;
			} else if (node is ParameterSyntax ps) {
				if (ps.Identifier.Span.ContainsOrTouches(pos)) exp = tok.Text;
			} else if (node is PropertyDeclarationSyntax pds) {
				if (pds.Identifier.Span.ContainsOrTouches(pos)) exp = tok.Text;
			} else if (node is SingleVariableDesignationSyntax svds) { //`Call(out var tok)` or `if (x is Y tok)` or tuple etc
				if (svds.Identifier.Span.ContainsOrTouches(pos)) exp = tok.Text;
			} else if (node is IdentifierNameSyntax) {
				if (cd.semanticModel.GetSymbolInfo(node).Symbol is ISymbol sym) {
					if (sym.Kind is SymbolKind.Field or SymbolKind.Local or SymbolKind.Parameter or SymbolKind.Property) {
						if (sym is IPropertySymbol ips && ips.IsWriteOnly) {
						} else if (tok.GetPreviousToken().Kind() is SyntaxKind.DotToken or SyntaxKind.MinusGreaterThanToken) {
							exp = cd.code[_FindStartOfMemberAccessOrElementAccess(node)..tok.Span.End];
						} else {
							exp = tok.Text;
						}
					}
				}
			}
		} else if (node is BracketedArgumentListSyntax && node.Parent is ElementAccessExpressionSyntax or ElementBindingExpressionSyntax) {
			if (App.Settings.debug.noFuncEval) return; //debugger bug: crashes if it's an indexer other than of string or array. And with this flag would fail anyway. And does not support string/array. //FUTURE: test, maybe the bug fixed.
			exp = cd.code[_FindStartOfMemberAccessOrElementAccess(node)..node.Span.End];
		} else return;
		
		int _FindStartOfMemberAccessOrElementAccess(SyntaxNode node) {
			var r = node.Parent;
			for (var n = r; n is MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax or ElementAccessExpressionSyntax or ElementBindingExpressionSyntax or MemberBindingExpressionSyntax; n = n.Parent) {
				if (n is MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax) r = n;
			}
			return r.SpanStart;
		}
		
		if (exp != null) {
			exp = _EscapeExpression(exp);
			//print.it(expr);
			//note: can be `MethodCall().member', `x[MethodCall()]` etc, not only variables, properties, indexers and operators.
			//info: netcoredbg does not support many expressions.
			//	Eg `x->y`, `n::x.y`, `stringOrArray.Length`, `string[0]`, `x[true ? 0 : 1]`, `x[Index]`, `x[a..b]`, `((C2)cc).P`, `x++`, typeof, as, is.
			//	See StackMachine.cs in ManagedPart project.
			//	Issue: https://github.com/Samsung/netcoredbg/issues/132
			
			//get frame
			
			SyntaxNode _GetEnclosingFunction(SyntaxNode n) {
				for (; n != null; n = n.Parent)
					if (n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or BasePropertyDeclarationSyntax or EventDeclarationSyntax or AnonymousFunctionExpressionSyntax) break;
				return n;
			}
			
			int frame = 0;
			if (_aStack.Length > 1) {
				var path = cd.sci.EFile.FilePath;
				var ef = _GetEnclosingFunction(node);
				if (ef == null) { //TLS
					if (node.HasAncestor<GlobalStatementSyntax>()) frame = _aStack.Length - 1; //else pos is in a class but not in a func
				} else {
					for (int i = 0; i < _aStack.Length; i++) {
						var f = _aStack[i].f;
						if (f.fullname != path) continue;
						int p = cd.sci.aaaLineStart(true, f.line - 1) + f.col - 1; //position of current statement in frame i
						var ef2 = _GetEnclosingFunction(cd.syntaxRoot.FindToken(p).Parent);
						if (ef2 == ef) { frame = i; break; }
					}
				}
			}
			//print.it(frame);
			
			//create MI variable, and display it as the first item in _tvVariables
			
			if (_VarCreate(exp, frame) is _VAR r) {
				_aVar[0].VarDelete();
				_aVar[0] = new(this, null, r, _VVItemType.Mouse);
				_tvVariables.SetItems(_aVar, true);
				_tvVariables.EnsureVisible(0);
				return;
			}
		}
		
		_aVar[0].VarDelete();
		_aVar[0] = new(this, _VVItemType.Mouse);
		_tvVariables.SetItems(_aVar, true);
	}
	
	_VAR _VarCreate(string exp, int frame = -1) {
		if (frame < 0) frame = _s.frame.level;
		int evalFlags = 0; //enum_EVALFLAGS, https://learn.microsoft.com/en-us/visualstudio/extensibility/debugger/reference/evalflags
		if (App.Settings.debug.noFuncEval) evalFlags |= 0x80; //EVAL_NOFUNCEVAL. Also tested EVAL_NOSIDEEFFECTS and EVAL_ALLOWERRORREPORT; it seems they do nothing.
		if (_d.SendSync(100, $"-var-create - {exp} --thread {_s.threadId} --frame {frame} --evalFlags {evalFlags}") is string s) {
			//print.it("VAR", s);
			if (s.Starts("^done,name=")) return new _MiRecord(s).Data<_VAR>();
			Debug_.Print($"<><c orange>{s}<>");
		}
		return null;
		//tested: the debugger has a 5-10 s timeout for expression evaluation.
	}
	
	void _WatchAdd(string exp, int frame = -1) {
		if (_VarCreate(exp, frame) is _VAR r) {
			_VariablesViewItem v = new(this, _watch, r, _VVItemType.Watch);
			_watch.AddWatch(v);
			_tvVariables.SetItems(_aVar, true);
			_tvVariables.Expand(1, true);
			_tvVariables.SelectSingle(v, true);
		}
	}
	
	void _WatchRemove(_VariablesViewItem v) {
		_watch.RemoveWatch(v);
		_tvVariables.SetItems(_aVar, true);
	}
	
	static string _EscapeExpression(string s) {
		s = s.RxReplace(@"[\r\n\t]+", " ").Escape();
		s = '"' + s + '"';
		return s;
	}
	
	void _tvVariables_ItemClick(TVItemEventArgs e) {
		var v = e.Item as _VariablesViewItem;
		if (e.Button == MouseButton.Right && e.ClickCount == 1 && e.Mod == 0) {
			var m = new popupMenu();
			
			if (v.itemType == _VVItemType.Watch) {
				if (v.Parent == null) m["Remove all watches"] = o => _WatchRemove(null);
				else m["Remove watch"] = o => _WatchRemove(v);
			} else {
				if (v.Exp == null) return; //<mouse>
				m["Add watch"] = o => {
					var s = _ExprPath(v);
					_WatchAdd(s);
				};
			}
			
			m.Show(owner: _tvVariables);
		}
		
		static string _ExprPath(_VariablesViewItem v) {
			int n = 0; for (var p = v; p != null; p = p.Parent) n++;
			if (n == 1) return v.Exp;
			var s = s_pathStack ??= new();
			s.Clear();
			for (var p = v; p != null; p = p.Parent) s.Push(p.Exp);
			return string.Join('.', s).Replace(".[", "[");
		}
	}
	static Stack<string> s_pathStack;
	
	class _VariablesViewItem : ITreeViewItem {
		readonly PanelDebug _panel;
		string _text;
		string _id, _exp;
		bool _isFolder;
		bool _isExpanded;
		public readonly _VVItemType itemType;
		_VariablesViewItem[] _children;
		
		public _VariablesViewItem(PanelDebug panel, _VARIABLE v) {
			_panel = panel;
			_exp = v.name;
			_SetTextAndIsFolder(v.value);
		}
		
		void _SetTextAndIsFolder(string value) {
			_text = $"{_exp}={value.Limit(8000)}";
			_isFolder = value.Starts('{') && !value.Ends("[0]}");
		}
		
		public _VariablesViewItem(PanelDebug panel, _VariablesViewItem parent, _VAR v, _VVItemType itemType) {
			_panel = panel;
			Parent = parent;
			this.itemType = itemType;
			_id = v.name;
			if (itemType == 0 && v.value.NE() && v.exp == "Static members") {
				_text = "<static>";
				_isFolder = true;
			} else {
				var s = v.exp; if (s.Ends('.')) s = "*" + s[..^1];
				_exp = s;
				_SetTextAndIsFolder(v.value);
			}
		}
		
		//Adds <mouse> or <watch>.
		public _VariablesViewItem(PanelDebug panel, _VVItemType itemType) {
			_panel = panel;
			this.itemType = itemType;
			bool isWatch = itemType == _VVItemType.Watch;
			_text = isWatch ? "<watch>" : "<mouse>";
			if (isWatch) _children = Array.Empty<_VariablesViewItem>();
		}
		
		public void VarDelete() {
			if (_id == null) return;
			_panel._d.SendSync(102, $"-var-delete {_id}");
			_id = null;
			_isFolder = _isExpanded = false;
			_children = null;
		}
		
		public _VariablesViewItem Parent { get; }
		
		public _VariablesViewItem[] Children => _children;
		
		public string Exp => _exp;
		
		public void AddWatch(_VariablesViewItem v) {
			if (_children.NE_()) _children = [v]; else _children = _children.InsertAt(-1, v);
			_isFolder = true;
		}
		
		//Deletes all if v null.
		public void RemoveWatch(_VariablesViewItem v) {
			if (v == null) {
				_children = Array.Empty<_VariablesViewItem>();
			} else {
				var i = Array.IndexOf(_children, v);
				if (i < 0) return;
				_children = _children.RemoveAt(i);
			}
			_isFolder = _children.Length > 0;
		}
		
		public void UpdateWatch() {
			if (_panel._VarCreate(_exp) is _VAR v) {
				_id = v.name;
				_SetTextAndIsFolder(v.value);
			} else {
				_id = null;
				_isFolder = false;
				_text = $"{_exp}=";
			}
			_children = null;
		}
		
		//ITreeViewItem
		
		string ITreeViewItem.DisplayText => _text;
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items {
			get {
				if (!_panel.IsStopped) return Array.Empty<_VariablesViewItem>();
				if (_children == null) {
					if (_id == null && _panel._VarCreate(_exp) is { } v) _id = v.name;
					if (_id != null) {
						if (_panel._d.SendSync(101, $"-var-list-children --all-values {_id} 0 1000") is string s) {
							if (s.Starts("^done,numchild=")) {
								var r = new _MiRecord(s).Data<_DONE_CHILDREN>();
								if (r.numchild > 0) {
									_children = new _VariablesViewItem[r.children.Length];
									for (int i = 0; i < _children.Length; i++) {
										_children[i] = new(_panel, this, r.children[i], itemType);
									}
									//SHOULDDO: show non-raw children of List, Dictionary, IEnumerable etc. Like other debuggers.
									//	Issue: https://github.com/Samsung/netcoredbg/issues/85
									//	Also $exception.
								}
							} else {
								//tested: the debugger has a timeout for expression evaluation.
								//	Eg if the executed code contains code `12.s();`, waits 5 s, and that value is "<error>".
								//	But if contains code `dialog.show("");`, waits 10 s and returns error.
								//	The debugger respects [DebuggerBrowsable(DebuggerBrowsableState.Never)] but ignores other [DebuggerX].
								Debug_.Print($"<><c orange>{s}<>");
							}
						}
					}
					if (_children == null) _isFolder = false;
					_children ??= Array.Empty<_VariablesViewItem>();
				}
				return _children;
			}
		}
		
		public bool IsFolder => _isFolder;
		
		bool ITreeViewItem.IsExpanded => _isExpanded;
		
		void ITreeViewItem.SetIsExpanded(bool yes) { _isExpanded = yes; }
		
		object ITreeViewItem.Image
			=> _isFolder ? EdResources.FolderArrow(_isExpanded)
			: null;
		
		void ITreeViewItem.SetNewText(string text) {
			
		}
		
		int ITreeViewItem.TextColor(TVColorInfo ci) => itemType switch { _VVItemType.Mouse => 0xE08000, _VVItemType.Watch => 0x40A000, _ => -1 };
	}
	
	enum _VVItemType : byte { Default, Mouse, Watch }
	
#if DEBUG
	void _Test() {
		//_d.Send("-var-assign i 100");
		
		var s = "Au.print.util.toString(dict)";
		//s = "_ToString(i)";
		s = "C.Print(i)";
		if (_VarCreate($"\"{s}\"", 0) is _VAR r) {
			print.it(r);
			print.it(r.value);
		}
	}
#endif
}
