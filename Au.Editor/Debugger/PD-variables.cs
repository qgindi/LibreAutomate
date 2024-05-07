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

//CONSIDER: save watches.
//CONSIDER: add separate "Watch" treeview. Also move <mouse> there.

partial class PanelDebug {
	KTreeView _tvVariables;
	_VariablesViewItem[] _aVar;
	_VariablesViewItem _watch;
	
	void _VariablesViewInit() {
		_tvVariables = new() { Name = "Variables_list", SingleClickActivate = true };
		_tvVariables.ItemClick += _tvVariables_ItemClick;
		_tvVariables.ItemActivated += e => { ((_VariablesViewItem)e.Item).Print(); };
	}
	
	void _ListVariables() {
		if (_s.frame.clr_addr.module_id.NE()) return; //f.func "[Native Frames]". Would be ^error.
		_d.Send($"-stack-list-variables --thread {_s.threadId} --frame {_s.frame.level}");
	}
	
	//Rejected. See comment in VarDelete.
	//Called when the user selects a frame or thread.
	//Calls -var-delete for all _aVar items.
	//Not really necessary, just frees some memory in the debugger process now. Debugger itself deletes all variables on step/continue.
	//void _VariablesViewChangedFrameOrThread() {
	//	if (_aVar == null) return;
	//	foreach (var v in _aVar) v.VarDelete();
	//}
	
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
			_aVar[0] = new _VariablesViewItem(this, _VVItemKind.Mouse);
			if (_watch == null) {
				_aVar[1] = _watch = new(this, _VVItemKind.Watch);
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
			} else if (node is IdentifierNameSyntax /*or GenericNameSyntax*/) {
				if (cd.semanticModel.GetSymbolInfo(node).Symbol is ISymbol sym) {
					if (sym.Kind is SymbolKind.Field or SymbolKind.Local or SymbolKind.Parameter or SymbolKind.Property) {
						if (sym is IPropertySymbol ips && ips.IsWriteOnly) {
						} else if (tok.GetPreviousToken().Kind() is SyntaxKind.DotToken or SyntaxKind.MinusGreaterThanToken) {
							exp = cd.code[_FindStartOfMemberAccessOrElementAccess(node)..tok.Span.End];
						} else {
							exp = tok.Text;
						}
					//} else if (sym.Kind is SymbolKind.Method && node.GetAncestor<InvocationExpressionSyntax>() is { } ies) { //for testing only
					//	var span = ies.Span;
					//	int i = span.Start;
					//	if (cd.code[i] == '.') i = _FindStartOfMemberAccessOrElementAccess(ies);
					//	exp = cd.code[i..span.End];
					}
				}
			}
		} else if (node is BracketedArgumentListSyntax && node.Parent is ElementAccessExpressionSyntax or ElementBindingExpressionSyntax) {
			//if (App.Settings.debug.noFuncEval) return; //debugger crashes if it's an indexer other than of string or array. And with this flag would fail anyway.
			exp = cd.code[_FindStartOfMemberAccessOrElementAccess(node)..node.Span.End];
		} else return;
		
		int _FindStartOfMemberAccessOrElementAccess(SyntaxNode node) {
			var r = node.Parent;
			for (var n = r; n is MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax or ElementAccessExpressionSyntax or ElementBindingExpressionSyntax or MemberBindingExpressionSyntax or InvocationExpressionSyntax; n = n.Parent) {
				if (n is MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax) r = n;
			}
			return r.SpanStart;
		}
		
		if (exp != null) {
			//print.it(exp);
			//note: can be `MethodCall().member', `x[MethodCall()]` etc, not only variables, properties, indexers and operators.
			//info: netcoredbg does not support many expressions.
			//	Eg `x->y`, `n::x.y`, `stringOrArray.Length`, `string[0]`, `x[true ? 0 : 1]`, `x[Index]`, `x[a..b]`, `((C2)cc).P`, `x++`, typeof, as, is, Method<T>.
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
				var path = cd.sci.FN.FilePath;
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
				//_aVar[0].VarDelete();
				_aVar[0] = new(this, null, r, _VVItemKind.Mouse);
				_tvVariables.SetItems(_aVar, true);
				_tvVariables.EnsureVisible(0);
				return;
			}
		}
		
		//_aVar[0].VarDelete();
		_aVar[0] = new(this, _VVItemKind.Mouse);
		_tvVariables.SetItems(_aVar, true);
	}
	
	_VAR _VarCreate(string exp, int frame = -1) {
		exp = _EscapeExpression(exp);
		if (frame < 0) frame = _s.frame.level;
		int evalFlags = 0; //enum_EVALFLAGS, https://learn.microsoft.com/en-us/visualstudio/extensibility/debugger/reference/evalflags
		//if (App.Settings.debug.noFuncEval) evalFlags |= 0x80; //EVAL_NOFUNCEVAL. Rejected. Not useful, just makes code more complex. Also tested EVAL_NOSIDEEFFECTS and EVAL_ALLOWERRORREPORT; it seems they do nothing.
		if (_d.SendSync(100, $"-var-create - {exp} --thread {_s.threadId} --frame {frame} --evalFlags {evalFlags}") is string s) {
			//print.it("VAR", s);
			if (s.Starts("^done,name=")) return new _MiRecord(s).Data<_VAR>();
			Debug_.Print($"<c orange>{s}<>");
		}
		return null;
		//tested: debugger has a 5-10 s timeout for expression evaluation.
	}
	
	_VAR _VarCreateL(string exp) {
		if (_d.SendSync(100, $"-var-create - {exp}") is string s) {
			if (s.Starts("^done,name=")) return new _MiRecord(s).Data<_VAR>();
			Debug_.Print($"<c orange>{s}<>");
		}
		return null;
	}
	
	void _WatchAdd(string exp, int frame = -1) {
		if (_VarCreate(exp, frame) is _VAR r) {
			_VariablesViewItem v = new(this, _watch, r, _VVItemKind.Watch);
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
			
			if (v.itemKind == _VVItemKind.Watch) {
				if (v.Parent == null) m["Clear"] = o => _WatchRemove(null);
				else m["Remove"] = o => _WatchRemove(v);
			} else {
				if (v.Exp == null) return; //<mouse>, <static>
				m["Watch"] = o => _WatchAdd(v.ExpPath());
			}
			
			m.Show(owner: _tvVariables);
		}
	}
	static Stack<string> s_pathStack;
	
	class _VariablesViewItem : ITreeViewItem {
		readonly PanelDebug _panel;
		_VAR _v;
		string _text, _exp;
		bool _isFolder;
		bool _isExpanded;
		public readonly _VVItemKind itemKind;
		_VariablesViewItem[] _children;
		
		public _VariablesViewItem(PanelDebug panel, _VARIABLE v) {
			_panel = panel;
			_exp = v.name;
			_SetTextAndIsFolder(v.value);
		}
		
		void _SetTextAndIsFolder(string value) {
			if (value != null) {
				_text = $"{_exp}={value.Limit(8000)}";
				_isFolder = value.Starts('{') && (_v != null ? _v.numchild > 0 : !value.Ends("[0]}"));
			} else _text = _exp;
		}
		
		public _VariablesViewItem(PanelDebug panel, _VariablesViewItem parent, _VAR v, _VVItemKind itemKind) {
			_panel = panel;
			Parent = parent;
			this.itemKind = itemKind;
			_v = v;
			if (v.value.NE() && v.exp == "Static members") {
				_text = "<static>";
				_isFolder = true;
			} else {
				var s = v.exp; if (s.Ends('.')) s = "*" + s[..^1];
				_exp = s;
				_SetTextAndIsFolder(v.value);
			}
		}
		
		//Adds <mouse> or <watch>.
		public _VariablesViewItem(PanelDebug panel, _VVItemKind itemKind) {
			_panel = panel;
			this.itemKind = itemKind;
			bool isWatch = itemKind == _VVItemKind.Watch;
			_text = isWatch ? "<watch>" : "<mouse>";
			if (isWatch) _children = Array.Empty<_VariablesViewItem>();
		}
		
		//public void VarDelete() {
		//	if (_v == null) return;
		//	//_panel._d.SendSync(102, $"-var-delete {v.name}"); //no. Somehow then every other -var-list-children fails.
		//	_v = null;
		//	_isFolder = _isExpanded = false;
		//	_children = null;
		//}
		
		public _VariablesViewItem Parent { get; }
		
		public _VariablesViewItem[] Children => _children;
		
		public string Exp => _exp;
		
		public string ExpPath() {
			var p = this;
			int n = 0; for (; p?._exp != null; p = p.Parent) n++;
			string r;
			if (n == 1) r = _exp;
			else {
				var s = s_pathStack ??= new();
				s.Clear();
				for (p = this; p?._exp != null; p = p.Parent) s.Push(p._exp);
				r = string.Join('.', s).Replace(".[", "[");
			}
			if (p?.Parent?._v is { } u) r = u.type + "." + r; //if in <static>, prepend type
			return r;
		}
		
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
				_v = v;
				_SetTextAndIsFolder(v.value);
			} else {
				_v = null;
				_isFolder = false;
				_text = $"{_exp}=";
			}
			_children = null;
		}
		
		public void Print() {
			if (_exp == null) return;
			if (!_EnsureHaveVar()) return;
			if (_v.value?.Ends('}') != false) {
				var s1 = App.Settings.debug.printVarCompact ? "Compact" : "";
				string t = _v.type, e = ExpPath();
				if (t is "IntPtr" or "UIntPtr" or "int" or "uint" or "long" or "ulong" or "short" or "ushort" or "byte" or "sbyte" or "double" or "float" or "char" or "bool") {
					//actually object or dynamic. Would fail or get garbage.
					e = $"{e}.ToString()";
					t = "string";
					//} else if (t.Ends("[]")) { //array. Fails everything I tried.
					//t="System.Array";
					//s1 = "Array";
					//t = t[..^2];
				}
				var k = $"Au.More.LaDebugger_<{t}>.Print{s1}({e})";
				//print.it(k);
				//print.it(_v);
				if (_panel._VarCreate(k) is { } v) {
					if (v.value.Starts('"')) {
						var s = Encoding.UTF8.GetString(Convert.FromBase64String(v.value[1..^1]));
						print.it(_AndHex(s));
					} else {
						print.it(v.value); //{Some.Exception}
					}
				}
				
			} else if (_v.value.Ends('"')) {
				print.it(_v.value[1..^1].Unescape());
			} else if (_v.value.Starts('<')) {
				//"<error>". Eg because of option "Don't call functions to get values" (rejected).
			} else {
				print.it(_AndHex(_v.value));
			}
			
			string _AndHex(string s) {
				var t = _v.type.TrimEnd('*');
				if (t is "IntPtr" or "UIntPtr" or "int" or "uint" or "long" or "ulong" or "short" or "ushort" or "byte" or "sbyte") {
					if (s.ToInt(out long x)) s = $"{s}  0x{x:X}";
				} else if (t is "char" && s.Length == 1) {
					var v = s == "'" ? @"\'" : s == "\"" ? s : s.Escape();
					s = $"{(int)s[0]} '{v}'";
				}
				return s;
			}
		}
		
		bool _EnsureHaveVar() {
			if (_v == null && _panel._VarCreate(_exp) is { } v) {
				_v = v;
			}
			return _v != null;
		}
		
		//ITreeViewItem
		
		string ITreeViewItem.DisplayText => _text;
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items {
			get {
				if (!_panel.IsStopped) return Array.Empty<_VariablesViewItem>();
				if (_children == null) {
					if (_EnsureHaveVar() && _v.numchild > 0) {
						//note: if without `--all-values`, values will be null, but calls properties anyway (same speed).
						//	In any case, does not call properties if the parent variable was created with evalFlags EVAL_NOFUNCEVAL. Then fast.
						
						//SHOULDDO: slow if many children. Eg WPF Window has almost 400, and the time is ~550 ms.
						//	If > 100 children, should get child properties when/if displaying them first time.
						//	Now we need just names for sorting. To make it fast, call -var-create with flag EVAL_NOFUNCEVAL (-var-list-children inherits its flags).
						//		This inheritance can be a problem. Unless I'll modify netcoredbg to pass flags to -var-list-children.
						//	Tested: if -var-list-children called for each child separately, the total time for a WPF Window is ~850 ms (instead of ~550 ms).
						//		Because need to sort, we cannot call -var-list-children to get just currently displayed children in single call.
						
						//print.it(_v);
						var max = _v.type.Ends(']') ? 100 : 5000;
						if (_panel._d.SendSync(101, $"-var-list-children --all-values {_v.name} 0 {max}") is string s) {
							if (s.Starts("^done,numchild=")) {
								var r = new _MiRecord(s).Data<_DONE_CHILDREN>();
								if (r.numchild > 0) {
									_children = new _VariablesViewItem[r.children.Length];
									for (int i = 0; i < _children.Length; i++) {
										_children[i] = new(_panel, this, r.children[i], itemKind);
									}
									Array.Sort(_children, (x, y) => string.Compare(x._exp ?? "\xffff", y._exp ?? "\xffff", StringComparison.OrdinalIgnoreCase));
									
									//FUTURE: show non-raw children of List, Dictionary, IEnumerable etc. Like other debuggers.
									//	Issue: https://github.com/Samsung/netcoredbg/issues/85
									//	Issue: https://github.com/Samsung/netcoredbg/issues/132#issuecomment-1868233713
									//	Now, as a workaround, users can click the variable to print items. But it works not with all ienumerables.
									
									//CONSIDER: put private members into a folder, except if parent is 'this'. But can be slow to correctly detect private members.
								}
							} else {
								//tested: debugger has a timeout for expression evaluation.
								//	Eg if the executed code contains code `12.s();`, waits 5 s, and that value is "<error>".
								//	But if contains code `dialog.show("");`, waits 10 s and returns error.
								//	Debugger respects [DebuggerBrowsable(DebuggerBrowsableState.Never)] but ignores other [DebuggerX].
								Debug_.Print($"<c orange>{s}<>");
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
		
		int ITreeViewItem.TextColor(TVColorInfo ci) => itemKind switch { _VVItemKind.Mouse => 0xE08000, _VVItemKind.Watch => 0x40A000, _ => -1 };
	}
	
	enum _VVItemKind : byte { Default, Mouse, Watch }
	
#if DEBUG
	void _Test() {
		//if (_VarCreate($"type", 0) is _VAR r) {
		//	print.it(r);
		//	print.it(r.value);
		//	_d.Send($"-var-assign {r.name} list.GetType()");
		//}
		
		//var s = "Au.print.util.toString(dict)";
		////s = "_ToString(i)";
		//s = "C.Print(i)";
		//s = "e1.GetEnumerator().MoveNext()";
		//if (_VarCreate($"{s}", 0) is _VAR r) {
		//	print.it(r);
		//	print.it(r.value);
		//}
		
		if (_d.SendSync(7, "-test") is string s1) {
			print.it(s1);
		}
	}
#endif
}
