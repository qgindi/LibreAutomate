extern alias CAW;

//note: the Roslyn project has been modified. Eg added Symbols property to the CompletionItem class.

using System.Collections.Immutable;
using Au.Controls;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;

//PROBLEM: Roslyn bug: no popup list if first parameter of indexer setter is enum. Same in VS.
//	Even on Ctrl+Space does not select the enum in list. And does not add enum members like "Enum.Member".

//CONSIDER: completion in keys string. But probably really useful only for keys like MediaNextTrack.

//CONSIDER: if string starts with <>, on < show list of output tags. User suggestion. And useful for me. Maybe even show a list of standard colors and the color control.
//	Also need a tool for wildcard expression.
//	Now users can in "string" press F1 or Ctrl+Space to open the help page.

//Roslyn bug: in code like below, after `is C.` the list contains only types. VS OK.
/*
int i = 0;
if (i is C.) ; //bad
if (i == C.) ; //OK
if (i is 4 or C.) ; //OK

static class C {
public const int CONST = 10;
public struct STRUCT {  }
}
*/

partial class CiCompletion {
	CiPopupList _popupList;
	_Data _data; //not null while the popup list window is visible
	CancellationTokenSource _cancelTS;
	
	class _Data {
		public CompletionService completionService;
		public Document document;
		public SemanticModel model;
		public List<CiComplItem> items;
		public int codeLength;
		public string filterText;
		public SciCode.ITempRange tempRange;
		public bool forced, noAutoSelect, isDot, unimported;
		public CiWinapi winapi;
	}
	
	public bool IsVisibleUI => _data != null;
	
	public void Cancel() {
		_cancelTS?.Cancel();
		_cancelTS = null;
		_CancelUI();
	}
	
	void _CancelUI(bool popupListHidden = false, bool tempRangeRemoved = false) {
		//print.it("_CancelUI", _data != null);
		if (_data == null) return;
		if (!tempRangeRemoved) _data.tempRange.Remove();
		_data = null;
		if (!popupListHidden) _popupList.Hide();
	}
	
	/// <summary>
	/// Called before <see cref="CiAutocorrect.SciCharAdded"/> and before passing the character to Scintilla.
	/// If showing popup list, synchronously commits the selected item if need (inserts text).
	/// Else inserts text at caret position and now caret is after the text.
	/// </summary>
	public CiComplResult SciCharAdding_Commit(SciCode doc, char ch) {
		CiComplResult R = CiComplResult.None;
		if (_data != null) {
			if (!SyntaxFacts.IsIdentifierPartCharacter(ch)) {
				var ci = _popupList.SelectedItem;
				if (ci != null && _data.filterText.Length == 0 && ch != '.') ci = null;
				if (ci != null) R = _Commit(doc, ci, ch, default);
				_CancelUI();
				//return;
			}
		}
		return R;
	}
	
	/// <summary>
	/// Asynchronously shows popup list if need.
	/// </summary>
	public void SciCharAdded_ShowList(CodeInfo.CharContext c) {
		if (_data == null) {
			_ShowList(c.ch);
		}
	}
	
	/// <summary>
	/// If showing popup list, synchronously filters/selects items.
	/// </summary>
	public void SciModified(SciCode doc, in Sci.SCNotification n) {
		if (_data != null) {
			bool trValid = _data.tempRange.GetCurrentFromTo(out int from, out int to, utf8: true);
			Debug.Assert(trValid); if (!trValid) { Cancel(); return; }
			string s = doc.aaaRangeText(false, from, to);
			
			//cancel if typed nonalpha if auto-showed by typing nonalpha (eg space in parameters or '(' after method name)
			if (!_data.forced && _data.filterText.NE() && s.Length == 1 && !SyntaxFacts.IsIdentifierStartCharacter(s[0])) {
				Cancel(); return;
			}
			
			foreach (var v in s) if (!SyntaxFacts.IsIdentifierPartCharacter(v)) return; //mostly because now is before SciCharAddedCommit, which commits (or cancels) if added invalid char
			_data.filterText = s;
			_FilterItems(_data);
			_popupList.UpdateVisibleItems(); //and calls SelectBestMatch
		}
	}
	
	public void ShowList(char ch = default) {
		_ShowList(ch);
	}
	
	//static bool s_workaround1;
	
	async void _ShowList(char ch) {
		//print.clear();
		
		//using
		//var p1 = perf.local();
		
		//print.it(_cancelTS);
		//bool busy = _cancelTS != null;
		bool isCommand = ch == default;
		
		if (!CodeInfo.GetContextWithoutDocument(out var cd)) { //returns false if position is in meta comments
			Cancel();
			return;
		}
		SciCode doc = cd.sci;
		int position = cd.pos;
		string code = cd.code;
		
		if (ch != default && position > 1 && SyntaxFacts.IsIdentifierPartCharacter(ch) && SyntaxFacts.IsIdentifierPartCharacter(code[position - 2])) { //in word
			return;
			//never mind: does not hide Regex completions. Same in VS.
		}
		
		bool unimported = isCommand && IsVisibleUI;
		
		Cancel();
		
		//CodeInfo.HideTextPopupAndTempWindows(); //no
		CodeInfo.HideTextPopup();
		
		//using var nogcr = keys.isScrollLock ? new Debug_.NoGcRegion(50_000_000) : default;
		
		if (!cd.GetDocument()) return; //returns false if fails (unlikely)
		var document = cd.document;
		//p1.Next('d');
		
		if (ch == '/') {
			GenerateCode.DocComment(cd);
			return;
		}
		
		bool isDot = false, canGroup = false;
		PSFormat stringFormat = PSFormat.None;
		CiStringInfo stringInfo = default;
		CompletionService completionService = null;
		SemanticModel model = null;
		CompilationUnitSyntax root = null;
		//ISymbol symL = null; //symbol at left of . etc
		CSharpSyntaxContext syncon = null;
		ITypeSymbol typeL = null; //not null if X. where X is not type/namespace/unknown
		ISymbol symL = null;
		int typenameStart = -1;
		
		var cancelTS = _cancelTS = new CancellationTokenSource();
		var cancelToken = cancelTS.Token;
#if DEBUG
		if (Debugger.IsAttached) { cancelToken = default; _cancelTS = null; }
#endif
		
		try {
			CompletionList r = await Task.Run(async () => { //info: usually GetCompletionsAsync etc are not async
				model = await document.GetSemanticModelAsync(cancelToken).ConfigureAwait(false); //speed: does not make slower, because GetCompletionsAsync calls it too. Same speed if only GetSyntaxRootAsync.
				root = model.Root as CompilationUnitSyntax;
				var tok = root.FindToken(position, findInsideTrivia: true);
				//CiUtil.PrintNode(tok);
				
				//return if in trivia, except if space in non-comments
				if (!isCommand && !tok.Span.ContainsOrTouches(position)) {
					bool good = false;
					if (ch == ' ') good = tok.FindTrivia(position - 1).IsKind(SyntaxKind.WhitespaceTrivia);
					if (ch == '<') good = tok.FindTrivia(position - 1).RawKind == 0; //in XML doc
					if (!good) return null;
				}
				
				try { syncon = CSharpSyntaxContext.CreateContext(document, model, position, cancelToken); }
				catch (ArgumentException) { return null; } //may happen in invalid code, where probably don't need intellisense
				
				var node = tok.Parent;
				//p1.Next('s');
				
				//in some cases show list when typing a character where GetCompletionsAsync works only on command
				if (ch == '[' && syncon.IsAttributeNameContext) ch = default;
				if (ch == ' ' && syncon.IsObjectCreationTypeContext) ch = default;
				//rejected. Then, if typed space, shows list 2 times. Not nice.
				//if (ch == '|') { //show on 'Enum.Member|'. Roslyn shows only on 'Enum.Member| ' (need space after |).
				//	var t2 = tok.GetPreviousToken();
				//	if (t2.IsKind(SyntaxKind.BarToken) && t2.SpanStart == position - 1 && t2.Parent.IsKind(SyntaxKind.BitwiseOrExpression)) {
				//		var n2 = t2.GetPreviousToken().Parent;
				//		if (n2 is IdentifierNameSyntax && (model.GetTypeInfo(n2).Type?.IsEnumType() ?? false)) ch = default;
				//	}
				//}
				
				completionService = CompletionService.GetService(document);
				if (cancelToken.IsCancellationRequested) return null;
				
				var options = CompletionOptions.Default with {
					TriggerInArgumentLists = false,
					ShowNameSuggestions = false,
					SnippetsBehavior = SnippetsRule.NeverInclude,
					PerformSort = false,
					ShowItemsFromUnimportedNamespaces = unimported,
					ForceExpandedCompletionIndexCreation = unimported,
					ExpandedCompletionBehavior = unimported ? ExpandedCompletionMode.ExpandedItemsOnly : ExpandedCompletionMode.AllItems,
					//TargetTypedCompletionFilter = true, //?
				};
				//print.it(options);
				var trigger = ch == default ? default : CompletionTrigger.CreateInsertionTrigger(ch);
				
				CompletionList r1 = await completionService.GetCompletionsAsync(document, position, options, null, trigger, cancellationToken: cancelToken).ConfigureAwait(false);
				if (r1 != null && r1.ItemsList.Count == 0) r1 = null;
				if (unimported) return r1; //don't support grouping etc. The completion items don't have Symbol. It's OK.
				if (r1 != null) {
					canGroup = true;
					//is it member access?
					if (node is not InitializerExpressionSyntax && position <= tok.SpanStart && tok.GetPreviousToken() is var t1 && t1.Kind() is SyntaxKind.CommaToken or SyntaxKind.OpenBraceToken && t1.Parent is InitializerExpressionSyntax ies1) node = ies1;
					if (node is InitializerExpressionSyntax) {
						//if only properties and/or fields, group by inheritance. Else group by namespace; it's a collection initializer list and contains everything.
						isDot = !r1.ItemsList.Any(o => o.Symbols?[0] is not (IPropertySymbol or IFieldSymbol));
						if (!isDot && ch == '{') return null; //eg 'new int[] {'
					} else {
						isDot = syncon.IsRightOfNameSeparator;
						if (isDot) { //set canGroup = false if Namespace.X or alias::X
							if (syncon.IsInImportsDirective) {
								canGroup = false;
							} else {
								var token = syncon.TargetToken; //not LeftToken, it seems they are swapped
								node = token.Parent;
								//CiUtil.PrintNode(token);
								//CiUtil.PrintNode(node);
								
								switch (node) {
								case MemberAccessExpressionSyntax s1: // . or ->
									node = s1.Expression;
									break;
								case MemberBindingExpressionSyntax s1: // ?.
									if (s1.Parent.Parent is ConditionalAccessExpressionSyntax caes1) node = caes1.Expression; else node = s1;
									break;
								case QualifiedNameSyntax s1: // eg . outside functions
									node = s1.Left;
									break;
								case AliasQualifiedNameSyntax: // ::
								case ExplicitInterfaceSpecifierSyntax: //Interface.X
								case QualifiedCrefSyntax: //does not include base members
									canGroup = false;
									break;
								case RangeExpressionSyntax: //x..y (user may want to make x.Method().y)
									break;
								default:
									Debug_.Print(node.GetType());
									isDot = canGroup = false;
									break;
								}
								
								if (canGroup) {
#if true //need typeL
									var ti = model.GetTypeInfo(node).Type;
									if (ti == null) {
										Debug_.PrintIf(model.GetSymbolInfo(node).Symbol is not INamespaceSymbol, node);
										canGroup = false;
									} else {
										symL = model.GetSymbolInfo(node).Symbol;
										Debug_.PrintIf(symL is INamespaceSymbol, node);
										//print.it(symL, symL is INamedTypeSymbol);
										if (symL is INamedTypeSymbol) typenameStart = node.SpanStart;
										else typeL = ti;
									}
#else //need just canGroup
									if (model.GetSymbolInfo(node).Symbol is INamespaceSymbol) canGroup = false;
#endif
								}
								//print.it(canGroup);
							}
						}
					}
					//p1.Next('M');
				} else if (isCommand) {
					if (tok.IsInString(position, code, out stringInfo) == true) {
						var tspan = stringInfo.textSpan;
						stringFormat = CiUtil.GetParameterStringFormat(stringInfo.stringNode, model, true);
						if (stringFormat == PSFormat.Wildex) { //is regex in wildex?
							if (code.RxMatch(@"\G(?:\*\*\*\w+ )?\*\*c?rc? ", 0, out RXGroup rg, 0, tspan.ToRange())
								&& position >= tspan.Start + rg.Length) stringFormat = PSFormat.Regexp;
						} else if (stringFormat == PSFormat.None) stringFormat = (PSFormat)100;
					}
				}
				return r1;
			}); //await Task.Run
			
			if (cancelToken.IsCancellationRequested) return;
			
			if (r == null) {
				if (stringFormat == (PSFormat)100) {
					var m = new popupMenu();
					m["Regex"] = o => CodeInfo._tools.ShowForStringParameter(PSFormat.Regexp, cd, stringInfo);
					m["Keys"] = o => CodeInfo._tools.ShowForStringParameter(PSFormat.Keys, cd, stringInfo);
					m.Separator();
					m.Submenu("Help\tF1", m => {
						m["C# strings"] = o => run.itSafe(CiUtil.GoogleURL("C# strings"));
						m["String formatting"] = o => run.itSafe(CiUtil.GoogleURL("C# string formatting"));
						m["Wildcard expression"] = o => HelpUtil.AuHelp("articles/Wildcard expression");
						m["Output tags"] = o => HelpUtil.AuHelp("articles/Output tags");
					});
					m.Show(PMFlags.ByCaret, owner: doc.AaWnd);
				} else if (stringFormat != 0) {
					CodeInfo._tools.ShowForStringParameter(stringFormat, cd, stringInfo);
				}
				return;
			}
			
			Debug.Assert(doc == Panels.Editor.ActiveDoc); //when active doc changed, cancellation must be requested
			if (doc.aaaText != code) { //changed while awaiting
				timer.after(55, _ => { if (doc == Panels.Editor.ActiveDoc) ShowList(); });
				return;
				//TODO3: instead: when text changed, cancel and set timer.
			} else if (doc.aaaCurrentPos16 != position) return;
			//p1.Next('T');
			
			var provider = _GetProvider(r.ItemsList[0]); //currently used only in cases when all completion items are from same provider
			if (!isDot) isDot = provider == CiComplProvider.Override;
			//print.it(provider, isDot, canGroup, r.Items[0].DisplayText);
			
			var span = r.Span;
			
			var d = new _Data {
				completionService = completionService,
				document = document,
				model = model,
				codeLength = code.Length,
				filterText = code.Substring(span.Start, span.Length),
				items = new List<CiComplItem>(r.ItemsList.Count),
				forced = isCommand,
				noAutoSelect = r.SuggestionModeItem != null,
				isDot = isDot,
				unimported = unimported
			};
			//if (r.SuggestionModeItem is {  } l) print.it(l.DisplayText, l.InlineDescription, l.Properties, l.ProviderName, l.Tags); //the item is not useful
			
			//ISymbol enclosing = null;
			//bool _IsAccessible(ISymbol symbol) {
			//	enclosing ??= model.GetEnclosingNamedTypeOrAssembly(position, default);
			//	return enclosing != null && symbol.IsAccessibleWithin(enclosing);
			//}
			
			//info: some members of enum UnmanagedType are missing. Hidden with EditorBrowsableState.Never, don't know why.
			
			//var testInternal = CodeInfo.Meta.TestInternal;
			Dictionary<INamespaceOrTypeSymbol, List<int>> groups = canGroup ? new(new CiNamespaceOrTypeSymbolEqualityComparer()) : null;
			List<int> keywordsGroup = null, etcGroup = null, snippetsGroup = null;
			uint kinds = 0; bool hasNamespaces = false;
			foreach (var ci_ in r.ItemsList) {
				//FUTURE: test with new Roslyn, maybe this fixed (in VS works well):
				//	The list contains only types if after `is`. Example: `bool ok = m is C.`. Here C is a class that contains inner types and int constants.
				
				var ci = ci_;
				if (unimported) {
					//if (ci.Flags.Has(CompletionItemFlags.Expanded)) { //now instead used CompletionOptions.ExpandedCompletionBehavior
					d.items.Add(new CiComplItem(provider, ci));
					//}
					continue;
				}
				
				Debug.Assert(ci.Symbols == null || ci.Symbols.Count > 0); //we may use code ci?.Symbols[0]. Roslyn uses this code in CompletionItem ctor: var firstSymbol = symbols[0];
				var sym = ci.Symbols?[0];
				
				//if (unimported && (ci.DisplayText == "Activator" || ci.DisplayText.Starts("Post"))) {
				//	print.it(ci.Flags, ci.InlineDescription, ci.Properties, ci.Tags);
				//}
				
				if (sym != null) {
					if (sym is IAliasSymbol ia) ci.Symbols = ImmutableArray.Create(sym = ia.Target);
					
					if (provider == CiComplProvider.Cref) { //why it adds internals from other assemblies?
						switch (sym.Kind) {
						case SymbolKind.NamedType when sym.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public && !sym.IsInSource() && !model.IsAccessible(0, sym):
							//ci.DebugPrint();
							continue;
						case SymbolKind.Namespace:
							//print.it(sym, sym.ContainingAssembly?.Name, sym.IsInSource());
							switch (sym.Name) {
							case "Internal" when sym.ContainingAssembly?.Name == "System.Core":
							case "Windows" when sym.ContainingAssembly?.Name == "mscorlib":
							case "MS" when sym.ContainingAssembly?.Name == null:
							case "FxResources" when sym.ContainingAssembly?.Name == "System.Resources.Extensions":
								continue;
							}
							break;
						}
					}
				}
				
				var v = new CiComplItem(provider, ci);
				kinds |= 1u << ((int)v.kind);
				//print.it(ci.DisplayText, sym);
				//if(ci.SortText != ci.DisplayText) print.it($"<>{ci.DisplayText}, sort={ci.SortText}");
				//if(ci.FilterText != ci.DisplayText) print.it($"<>{ci.DisplayText}, filter={ci.FilterText}");
				//if(!ci.DisplayTextSuffix.NE()) print.it($"<>{ci.DisplayText}, suf={ci.DisplayTextSuffix}");
				//if(!ci.DisplayTextPrefix.NE()) print.it($"<>{ci.DisplayText}, pre={ci.DisplayTextPrefix}");
				//print.it(ci.Flags); //a new internal property. Always None.
				
				switch (v.kind) {
				case CiItemKind.Method:
					if (sym != null) {
						if (sym.IsStatic) {
							switch (ci.DisplayText) {
							case "Equals":
							case "ReferenceEquals":
								//hide static members inherited from Object
								if (sym.ContainingType.BaseType == null) { //Object
									if (isDot && !(symL is INamedTypeSymbol ints1 && ints1.BaseType == null)) continue; //if not object
									v.moveDown = CiComplItemMoveDownBy.Name;
								}
								break;
							case "Main" when _IsOurScriptClass(sym.ContainingType):
								v.moveDown = CiComplItemMoveDownBy.Name;
								break;
							}
						} else {
							switch (ci.DisplayText) {
							case "Equals" or "GetHashCode" or "GetType" or "ToString":
							case "MemberwiseClone":
							case "Deconstruct": //record
							case "GetEnumerator": //IEnumerable
							case "CompareTo": //IComparable
							case "GetTypeCode": //IConvertible
							case "Clone" when sym.ContainingType.Name == "String": //this useless method would be the first in the list
								v.moveDown = CiComplItemMoveDownBy.Name;
								break;
							}
							//var ct = sym.ContainingType;
							//print.it(ct.ToString(), ct.Name, ct.ContainingNamespace.ToString(), ct.BaseType);
						}
					}
					break;
				case CiItemKind.Namespace when ci.Symbols != null: //null if extern alias
					hasNamespaces = true;
					switch (ci.DisplayText) {
					case "Accessibility":
					case "UIAutomationClientsideProviders":
						v.moveDown = CiComplItemMoveDownBy.Name;
						break;
					case "XamlGeneratedNamespace": continue;
					}
					break;
				case CiItemKind.TypeParameter:
					if (sym == null && ci.DisplayText == "T") continue;
					break;
				case CiItemKind.Class:
					if (!isDot && sym is INamedTypeSymbol ins && _IsOurScriptClass(ins)) v.moveDown = CiComplItemMoveDownBy.Name;
					if (typenameStart >= 0 && ci.DisplayText == "l" && CiWinapi.IsWinapiClassSymbol(symL as INamedTypeSymbol)) v.hidden = CiComplItemHiddenBy.Always; //not continue;, then no groups
					break;
				case CiItemKind.EnumMember when !isDot:
					//workaround for: if Enum.Member, members are sorted by value, not by name. Same in VS.
					if (ci.SortText != ci.DisplayText) {
						string ss = ci.SortText, se = sym.ContainingType.Name;
						if (ss.Length == se.Length + 5 && ss.Starts(se) && ss[se.Length] == '_') //like "EnumName_0001"
							v.ci = ci = ci.WithSortText(se + "." + sym.Name);
					}
					break;
				case CiItemKind.EnumMember when isDot:
				case CiItemKind.Label:
					canGroup = false;
					break;
				case CiItemKind.LocalVariable:
					if (isDot) continue; //see the bug comment below
					break;
				}
				
				static bool _IsOurScriptClass(INamedTypeSymbol t) => t.Name is "Program" or "Script";
				
				if (sym != null && v.kind is not (CiItemKind.LocalVariable or CiItemKind.Namespace or CiItemKind.TypeParameter)) {
					if (ci.Symbols.All(sy => sy.IsObsolete())) v.moveDown = CiComplItemMoveDownBy.Obsolete; //can be several overloads, some obsolete but others not
				}
				
				d.items.Add(v);
			}
			//p1.Next('i');
			
			if (canGroup) {
				for (int i = 0; i < d.items.Count; i++) {
					var v = d.items[i];
					var sym = v.FirstSymbol;
					if (sym == null) {
						if (v.kind == CiItemKind.Keyword) (keywordsGroup ??= new List<int>()).Add(i);
						else (etcGroup ??= new List<int>()).Add(i);
					} else {
						INamespaceOrTypeSymbol nts;
						if (!isDot) {
							nts = sym.ContainingNamespace;
							
							//put locals at the top
							if (nts?.ContainingNamespace != null && sym.ContainingSymbol is not INamespaceOrTypeSymbol) {
								while (nts.ContainingNamespace is INamespaceSymbol n1) nts = n1; //global namespace
							}
							//rejected: also put members of enclosing type[s] (and the type itself) at the top.
							//	Not so easy to implement without problems like "Enum.Member" in a different group than the "Enum".
							//	In some cases model.GetEnclosingNamedType returns other object although logically it is the same as sym.
							//	Also then may be more confusion. Now everything is clear: locals are at the top, as well as everything namespaceless.
							//	Simple code that does not work well:
							//let only types be in namespace groups. Put locals and non-type members of enclosing type(s) at the top.
							//if (nts.ContainingNamespace != null && sym is not INamespaceOrTypeSymbol) {
							//	while (nts.ContainingNamespace is INamespaceSymbol n1) nts = n1; //global namespace
							//}
							
							//CONSIDER: put in different groups: namespaces, locals, members of enclosing type(s), other namespaceless symbols. Now grouped by kind.
						}
						//else if(sym is ReducedExtensionMethodSymbol em) nts = em.ReceiverType; //rejected. Didn't work well, eg with linq.
						else nts = sym.ContainingType;
						
						//Roslyn bug: sometimes adds some garbage items.
						//To reproduce: invoke global list. Then invoke list for a string variable. Adds String, Object, all local string variables, etc. Next time works well. After Enum dot adds the enum type, even in VS; in VS sometimes adds enum methods and extmethods.
						//Debug_.PrintIf(nts == null, sym.Name);
						if (nts == null) continue;
						
						if (groups.TryGetValue(nts, out var list)) list.Add(i); else groups.Add(nts, new List<int> { i });
					}
				}
				
				//snippets, winapi
				if (isDot) {
					if (typeL != null) { //eg variable.x
					} else if (symL is INamedTypeSymbol nts && CiWinapi.IsWinapiClassSymbol(nts)) { //type.x
						int i = d.items.Count;
						bool newExpr = syncon.TargetToken.Parent.Parent is ObjectCreationExpressionSyntax or StackAllocArrayCreationExpressionSyntax; //info: syncon.IsObjectCreationTypeContext false
						d.winapi = CiWinapi.AddWinapi(nts, d.items, span, typenameStart, newExpr);
						int n = d.items.Count - i;
						if (n > 0) {
							snippetsGroup = new List<int>(n);
							for (; i < d.items.Count; i++) snippetsGroup.Add(i);
						}
					}
				} else if (!d.noAutoSelect && (hasNamespaces || _OnlyCSharpKeywordsOrDirectives())) {
					//add snippets
					if (provider is not (CiComplProvider.Cref or CiComplProvider.XmlDoc)) {
						int i = d.items.Count;
						CiSnippets.AddSnippets(d.items, span, root, code, false, syncon);
						for (; i < d.items.Count; i++) (snippetsGroup ??= new List<int>()).Add(i);
					}
				}
				
				bool _OnlyCSharpKeywordsOrDirectives()
					=> kinds == 1u << (int)CiItemKind.Keyword && provider == CiComplProvider.Keyword;
			}
			//p1.Next('+');
			
			if (d.items.Count == 0) return;
			
			List<string> groupsList = null;
			if (canGroup && groups.Count + (keywordsGroup == null ? 0 : 1) + (etcGroup == null ? 0 : 1) + (snippetsGroup == null ? 0 : 1) > 1) {
				List<(string, List<int>)> g = null;
				if (isDot) {
					var gs = groups.ToList();
					gs.Sort((k1, k2) => {
						//let extension methods be at bottom, sorted by type name
						int em1 = d.items[k1.Value[0]].kind == CiItemKind.ExtensionMethod ? 1 : 0;
						int em2 = d.items[k2.Value[0]].kind == CiItemKind.ExtensionMethod ? 1 : 0;
						int diff = em1 - em2;
						if (diff != 0) return diff;
						if (em1 == 1) return string.Compare(k1.Key.Name, k2.Key.Name, StringComparison.OrdinalIgnoreCase);
#if true
						//sort non-extension members by inheritance or base interface
						var t1 = k1.Key as INamedTypeSymbol; var t2 = k2.Key as INamedTypeSymbol;
						if (t1.InheritsFromOrImplementsOrEqualsIgnoringConstruction(t2)) return -1;
						if (t2.InheritsFromOrImplementsOrEqualsIgnoringConstruction(t1)) return 1;
						//interface and object? For both, BaseType returns null and InheritsFromOrImplementsOrEqualsIgnoringConstruction returns false.
						var tk1 = t1.TypeKind; var tk2 = t2.TypeKind;
						if (tk1 == TypeKind.Class && t1.BaseType == null) return 1; //t1 is object
						if (tk2 == TypeKind.Class && t2.BaseType == null) return -1; //t2 is object
						//Debug_.Print($"{t1}, {t2},    {t1.BaseType}, {t2.BaseType},    {tk1}, {tk2}");
#else
						//sort non-extension members by inheritance
						var t1 = k1.Key as INamedTypeSymbol; var t2 = k2.Key as INamedTypeSymbol;
						if (_IsBase(t1, t2)) return -1;
						if (_IsBase(t2, t1)) return 1;
						static bool _IsBase(INamedTypeSymbol t, INamedTypeSymbol tBase) {
							for (t = t.BaseType; t != null; t = t.BaseType) if (t == tBase) return true;
							return false;
						}
						//can be both interfaces, or interface and object. For object and interfaces BaseType returns null.
						var tk1 = t1.TypeKind; var tk2 = t2.TypeKind;
						if (tk1 == TypeKind.Class && t1.BaseType == null) return 1; //t1 is object
						if (tk2 == TypeKind.Class && t2.BaseType == null) return -1; //t2 is object
						if (tk1 == TypeKind.Interface && tk2 == TypeKind.Interface) {
							if (t2.AllInterfaces.Contains(t1)) return 1;
							if (t1.AllInterfaces.Contains(t2)) return -1;
						}
						//fails for eg ObservableCollection<>. Uses 2 variables for t2 and t1.BaseType although it is the same type.
						Debug_.Print($"{t1}, {t2}, {k1.Value.Count}, {k2.Value.Count}, {tk1}, {tk2}, {t1.BaseType}, {t2.BaseType}"); //usually because of Roslyn bugs
#endif
						
						return 0;
					});
					//print.it(gs);
					
#if true
					if (gs[0].Key.Name == "String") { //don't group Au extension methods
						for (int i = gs.Count; --i > 0;) {
							if (d.items[gs[i].Value[0]].kind != CiItemKind.ExtensionMethod) continue;
							var ns = gs[i].Key.ContainingNamespace;
							if (ns.Name == "Types" && ns.ContainingNamespace.Name == "Au") {
								gs[0].Value.AddRange(gs[i].Value);
								gs.RemoveAt(i);
								//break; //no. If testInternal Au, we have 2 Au.Types.
							}
						}
					}
#else
					if(!App.Settings.ci_complGroupEM) { //don't group non-Linq extension methods
						for(int i = 1; i < gs.Count; i++) {
							if(d.items[gs[i].Value[0]].kind != CiItemKind.ExtensionMethod) continue;
							var ns = gs[i].Key.ContainingNamespace;
							if(ns.Name != "Linq") {
								gs[0].Value.AddRange(gs[i].Value);
								gs.RemoveAt(i--);
							}
						}
					}
#endif
					
					g = gs.Select(o => (o.Key.Name, o.Value)).ToList(); //list<(itype, list)> -> list<typeName, list>
				} else {
					g = groups.Select(o => (o.Key.QualifiedName(), o.Value)).ToList(); //dictionary<inamespace, list> -> list<namespaceName, list>
					g.Sort((e1, e2) => {
						//order: global, Au, my, others by name, Microsoft.*
						string s1 = e1.Item1, s2 = e2.Item1;
						int k1 = s1.Length <= 2 ? (s1 switch { "" => 3, "Au" => 2, "my" => 1, _ => 0 }) : s1.Starts("Microsoft.") ? -1 : 0;
						int k2 = s2.Length <= 2 ? (s2 switch { "" => 3, "Au" => 2, "my" => 1, _ => 0 }) : s2.Starts("Microsoft.") ? -1 : 0;
						int kd = k2 - k1; if (kd != 0) return kd;
						return string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);
					});
					//print.it("----");
					//foreach(var v in g) print.it(v.Item1, v.Item2.Count);
					
					if (hasNamespaces && _GetFilters(model, out var filters)) {
						foreach (var (ns, a) in g) { //for each namespace in completion list
							if (ns.NE() || !filters.TryGetValue(ns, out var k)) continue;
							foreach (var i in a) { //for each type in that namespace in completion list
								var sym = d.items[i].FirstSymbol;
								if (sym is not INamedTypeSymbol nt) {
									if (sym is IFieldSymbol fs) nt = fs.ContainingType; //enum member
									else continue;
								}
								var s = nt.Name;
								string opt = k[0];
								bool found = k.Length == 1 || (k.Length == 2 && k[1] == "*");
								if (!found) {
									for (int j = 1; j < k.Length; j++) { //for each type in filter, including additional options, like [-~ T1 T2 - T3 T4]
										var t = k[j];
										if (t[0] is '+' or '-') { opt = t; continue; }
										//if (s.Like(u)) { found = true; break; }
										if (t[0] == '*') found = s.Ends(t.AsSpan(1..));
										else if (t[^1] == '*') found = s.Starts(t.AsSpan(..^1));
										else found = s == t;
										if (found) break;
									}
								}
								if (found == (opt[0] == '-')) {
									var ci = d.items[i];
									if (opt.Eq(1, '~')) ci.moveDown |= CiComplItemMoveDownBy.Name;
									else ci.hidden |= CiComplItemHiddenBy.Always;
								}
							}
						}
					}
				}
				if (keywordsGroup != null) g.Add(("keywords", keywordsGroup));
				if (snippetsGroup != null) g.Add((isDot ? "" : "snippets", snippetsGroup));
				if (etcGroup != null) g.Add(("etc", etcGroup));
				for (int i = 0; i < g.Count; i++) {
					foreach (var v in g[i].Item2) d.items[v].group = i;
				}
				groupsList = g.Select(o => o.Item1).ToList();
			}
			//p1.Next('g');
			
			if (!span.IsEmpty) _FilterItems(d);
			//p1.Next('F');
			
			d.tempRange = doc.ETempRanges_Add(this, span.Start, span.End, () => {
				//print.it("leave", _data==d);
				if (_data == d) _CancelUI(tempRangeRemoved: true);
			}, position == span.End ? SciCode.TempRangeFlags.LeaveIfPosNotAtEndOfRange : 0);
			
			_data = d;
			if (_popupList == null) {
				_popupList = new CiPopupList(this);
				_popupList.PopupWindow.Hidden += (_, _) => _CancelUI(popupListHidden: true);
			}
			_popupList.Show(doc, span.Start, _data.items, groupsList); //and calls SelectBestMatch
		}
		catch (OperationCanceledException) { /*Debug_.Print("canceled");*/ return; }
		//catch (AggregateException e1) when (e1.InnerException is TaskCanceledException) { return; }
		finally {
			if (_data == null) {
				//p1.Next('z');
				//print.it($"{p1.ToString()}  |  ch='{(ch == default ? "" : ch.ToString())}', canceled={cancelTS.IsCancellationRequested}");
			}
			cancelTS.Dispose();
			if (cancelTS == _cancelTS) _cancelTS = null;
		}
		
		static bool _GetFilters(SemanticModel model, out Dictionary<string, string[]> filters) {
			//using var p2 = perf.local(); //~50 mcs
			var stCurrent = model.SyntaxTree;
			filters = null;
			foreach (var st in model.Compilation.SyntaxTrees) {
				//print.it(st.FilePath);
				foreach (var u in st.GetCompilationUnitRoot().Usings) {
					if (u.GlobalKeyword.RawKind == 0 && st != stCurrent) break;
					if (u.Alias != null || u.StaticKeyword.RawKind != 0) continue;
					//print.it(u);
					var tt = u.GetTrailingTrivia().FirstOrDefault(o => o.IsKind(SyntaxKind.SingleLineCommentTrivia));
					if (tt.RawKind != 0) {
						var text = tt.ToString(); //print.it((object)text==tt.ToString()); //true, fast
						if (text[^1] == ']') {
							int i = text.LastIndexOf('[') + 1;
							if (i > 0 && text[i] is '+' or '-') {
								var a = text.AsSpan(i..^1).SplitS(' ', StringSplitOptions.RemoveEmptyEntries);
								filters ??= new();
								filters[u.Name.ToString()] = a;
								continue;
							}
						}
					}
					filters?.Remove(u.Name.ToString());
				}
			}
			return filters != null;
		}
	}
	
	static void _FilterItems(_Data d) {
		var filterText = d.filterText;
		foreach (var v in d.items) {
			v.hidden &= ~CiComplItemHiddenBy.FilterText;
			v.hilite = 0;
			v.moveDown &= ~CiComplItemMoveDownBy.FilterText;
		}
		if (!filterText.NE()) {
			string textLower = filterText.Lower(), textUpper = filterText.Upper();
			char c0Lower = textLower[0], c0Upper = textUpper[0];
			foreach (var v in d.items) {
				if (v.kind == CiItemKind.None) continue; //eg regex completion
				var s = v.ci.FilterText;
				//Debug_.PrintIf(v.ci.FilterText != v.Text, $"{v.ci.FilterText}, {v.Text}");
				//print.it(v.Text, v.ci.FilterText, v.ci.SortText, v.ci.ToString());
				bool found = false;
				int iFirst = _FilterFindChar(s, 0, c0Lower, c0Upper), iFirstFirst = iFirst;
				if (iFirst >= 0) {
					if (filterText.Length == 1) {
						_HiliteChar(iFirst);
					} else {
						while (!s.Eq(iFirst, filterText, true)) {
							iFirst = _FilterFindChar(s, iFirst + 1, c0Lower, c0Upper);
							if (iFirst < 0) break;
						}
						if (iFirst >= 0) {
							_HiliteSubstring(iFirst);
						} else { //has all uppercase chars? Eg add OneTwoThree if text is "ott" or "ot" or "tt".
							_HiliteChar(iFirstFirst);
							for (int i = 1, j = iFirstFirst + 1; i < filterText.Length; i++, j++) {
								j = _FilterFindChar(s, j, textLower[i], textUpper[i], camel: true);
								if (j < 0) { found = false; break; }
								_HiliteChar(j);
							}
						}
					}
				}
				
				void _HiliteChar(int i) {
					found = true;
					if (i < 64) v.hilite |= 1UL << i;
				}
				
				void _HiliteSubstring(int i) {
					found = true;
					int to = Math.Min(i + filterText.Length, 64);
					while (i < to) v.hilite |= 1UL << i++;
				}
				
				if (found) {
					v.hilite <<= v.ci.DisplayTextPrefix.Lenn();
					
					//if DisplayText != FilterText, correct or clear hilites. Eg cref.
					if (s != v.Text) {
						iFirst = v.Text.Find(s);
						if (iFirst < 0) v.hilite = 0; else v.hilite <<= iFirst;
					}
				} else {
					if (filterText.Length > 1 && (iFirst = s.Find(filterText, true)) >= 0) {
						v.moveDown |= CiComplItemMoveDownBy.FilterText; //sort bottom
						_HiliteSubstring(iFirst);
					} else v.hidden |= CiComplItemHiddenBy.FilterText;
				}
			}
		}
	}
	
	/// <summary>
	/// Finds character in s where it is one of: uppercase; lowercase after '_'/'@'; not uppercase/lowercase; any at i=0.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="i">Start index.</param>
	/// <param name="cLower">Lowercase version of character to find.</param>
	/// <param name="cUpper">Uppercase version of character to find.</param>
	/// <param name="camel">Uppercase must not be preceded and followed by uppercase.</param>
	static int _FilterFindChar(string s, int i, char cLower, char cUpper, bool camel = false) {
		for (; i < s.Length; i++) {
			char c = s[i];
			if (c == cUpper) { //any not lowercase
				if (!camel) return i;
				if (i == 0 || !char.IsUpper(c)) return i;
				if (!char.IsUpper(s[i - 1])) return i;
				if (i + 1 < s.Length && char.IsLower(s[i + 1])) return i;
			}
			if (c == cLower) { //lowercase
				if (i == 0) return i;
				switch (s[i - 1]) { case '_': case '@': return i; }
			}
		}
		return -1;
	}
	
	public void SelectBestMatch(IEnumerable<CompletionItem> listItems, bool grouped) {
		CiComplItem ci = null;
		var filterText = _data.filterText;
		if (!(/*_data.noAutoSelect ||*/ filterText == "_")) { //noAutoSelect when lambda argument
			
			//rejected. Need FilterItems anyway, eg to select enum type or 'new' type.
			//if(filterText.NE()) {
			//	_popupList.SelectFirstVisible();
			//	return;
			//}
			
			//perf.first();
			var visible = listItems.ToImmutableArray();
			if (!visible.IsEmpty) {
				//perf.next();
				var fi = _data.completionService.FilterItems(_data.document, visible, filterText);
				//perf.next();
				//if (filterText.Length > 0) print.it("-", fi);
				//print.it(visible.Length, fi.Length);
				if (!fi.IsDefaultOrEmpty) {
					if (fi.Length < visible.Length || filterText.Length > 0 || visible.Length == 1) {
						var v = fi[0];
						if (!v.DisplayTextPrefix.NE() && fi.Length > 1 && fi.FirstOrDefault(o => o.DisplayTextPrefix.NE()) is { } vv) v = vv; //eg "(nint)" -> "Name"
						ci = v.Attach as CiComplItem;
						
						//rejected. Not sure it's better.
						//For normal priority items should ignore group and select by alphabet.
						//	Else often selects eg an Au or System namespace item instead of keyword. Probably it is not what users like.
						//if (grouped && fi.Length > 1 && filterText.Length > 1 && ci.ci.SortText.Starts(filterText[0])) {
						//	for(int i = 1; i < fi.Length; i++) {
						//		var v = fi[i].Attach as CiComplItem;
						//		if (v.moveDown != 0) break;
						//		if (v.group == ci.group) continue;
						//		string s1 = v.ci.SortText, s2 = ci.ci.SortText;
						//		if (s1.NE() || s2.NE() || s1[0] != s2[0]) continue;
						//		if (string.CompareOrdinal(s1, s2) < 0) ci = v;
						//	}
						//	//foreach(var v in fi) print.it(v, v.Rules.MatchPriority, v.Rules.SelectionBehavior); //all 0, Default
						//}
					}
				} else if (filterText == "" && !_data.isDot && !_data.unimported) {
					//Workaround for bug in new Roslyn: does not select enum when the target type is enum.
					//	The same after keyword 'new'.
					//	Never mind: does not prefer the target type when typed eg single letter and the list also contains static fields or props of that type like "Type.Member". It never worked.
					//	VS works well in all cases. Maybe it does not use this Roslyn API, or uses different Roslyn version.
					//	FUTURE: test after updating Roslyn, maybe fixed.
					foreach (var v in _data.items) {
						//if (v.ci is var j && j.DisplayText.Starts("DEdit")) print.it(j.DisplayText, j.Flags, j.Span, j.Tags, j.Properties, j.IsPreferredItem());
						//if (j.ci is var j && j.Properties.ContainsKey("Symbols")) print.it(j.DisplayText, j.Flags, j.Span, j.Tags, j.Properties, j.IsPreferredItem());
						if (v.kind is CiItemKind.Enum or CiItemKind.Class or CiItemKind.Structure or CiItemKind.Delegate && v.ci.Properties.ContainsKey("Symbols")) { //TODO3: unreliable
							ci = v;
							break;
						}
					}
				}
			}
			//perf.nw('b');
		}
		if (_data.noAutoSelect && ci != null) _popupList.SuggestedItem = ci;
		else _popupList.SelectedItem = ci;
	}
	//CONSIDER: when typed 1-2 lowercase chars, select keyword instead of type.
	//	Now these types are selected first (but none when typed 3 chars):
	/*
	elm else
	folders for
	inputBlocker int
	regexp return/ref/readonly/record    //ref/record are rare and before return, but readonly...
	trayIcon true/try
	uiimage uint

	RARE
	clipboard class
	filesystem finally/fixed
	pathname params
	print / process private/protected

	*/
	
	public System.Windows.Documents.Section GetDescriptionDoc(CiComplItem ci, int iSelect) {
		if (_data == null) return null;
		switch (ci.Provider) {
		case CiComplProvider.Snippet: return CiSnippets.GetDescription(ci);
		case CiComplProvider.Winapi: return CiWinapi.GetDescription(ci);
		}
		switch (ci.kind) {
		case CiItemKind.Keyword: return CiText.FromKeyword(ci.Text);
		case CiItemKind.Label: return CiText.FromLabel(ci.Text);
		}
		var symbols = ci.Symbols;
		if (symbols != null) return CiText.FromSymbols(symbols, iSelect, _data.model, _data.tempRange.CurrentFrom);
		if (ci.kind == CiItemKind.Namespace) return null; //extern alias
		Debug_.PrintIf(!(ci.kind == CiItemKind.None || ci.ci.Flags.Has(CompletionItemFlags.Expanded)), ci.kind); //None if Regex
		var r = _data.completionService.GetDescriptionAsync(_data.document, ci.ci).Result; //fast if Regex, else not tested
		return r == null ? null : CiText.FromTaggedParts(r.TaggedParts);
	}
	
	/// <summary>
	/// Inserts the replacement text of the completion item.
	/// ch == default if clicked or pressed Enter or Tab or a hotkey eg Ctrl+Enter.
	/// key == default if clicked or typed a character (except Tab and Enter). Does not include hotkey modifiers.
	/// </summary>
	CiComplResult _Commit(SciCode doc, CiComplItem item, char ch, KKey key) {
		//At first hide UI and set _data=null. Else modifying document text may cause bugs.
		var data = _data;
		int currentFrom = data.tempRange.CurrentFrom, currentTo = data.tempRange.CurrentTo;
		_CancelUI();
		
		if (item.Provider == CiComplProvider.EmbeddedLanguage) { //can complete only on click or Tab
			if (ch != default || !(key == default || key == KKey.Tab)) return CiComplResult.None;
		} else if (ch == ':') { //don't complete if `label:`
			if (data.model.SyntaxTree.FindTokenOrEndToken(currentFrom, default).Parent is IdentifierNameSyntax node) {
				if ((node.Parent is ExpressionStatementSyntax ess && ess.Parent is BlockSyntax) || (node.Parent is VariableDeclarationSyntax vds && vds.Parent is LocalDeclarationStatementSyntax)) {
					if (!(item.Text == "global" || (item.FirstSymbol is INamespaceSymbol ns && ns.IsGlobalNamespace))) //eg `global::`
						return CiComplResult.None;
				}
			}
		}
		
		bool isSpace; if (isSpace = ch == ' ') ch = default;
		int codeLenDiff = doc.aaaLen16 - data.codeLength;
		
		if (item.Provider == CiComplProvider.Snippet) {
#if true
			if (ch is not ('\0' or '(')) return CiComplResult.None;
			CiSnippets.Commit(doc, item, codeLenDiff);
			return CiComplResult.Complex;
#else //rejected: support eg identifierSnippet.<new completion list>. Better use type alias.
			if (ch is not ('\0' or '(' or '.')) return CiComplResult.None;
			var snippet = CiSnippets.Commit(doc, item, codeLenDiff);
			return ch != default && snippet != null && snippet.Split('.').All(o => SyntaxFacts.IsValidIdentifier(o)) ? CiComplResult.Simple //support eg identifierSnippet.<new completion list>
				: CiComplResult.Complex; //eat the char
			//rejected: if the snippet inserted code like `Type.Method` and completed with space, append `()`. Instead let use snippet `Type.Method($end$)`.
#endif
		}
		
		var ci = item.ci;
		string s; int startPos, len;
		bool isComplex = false;
		bool ourProvider = item.Provider is CiComplProvider.Winapi;
		if (ourProvider) {
			s = item.Text;
			startPos = currentFrom;
			len = currentTo - startPos;
		} else {
			var change = data.completionService.GetChangeAsync(data.document, ci).Result;
			//note: don't use the commitCharacter parameter. Some providers, eg XML doc, always set IncludesCommitCharacter=true, even when commitCharacter==null, but may include or not, and may include inside text or at the end.
			
			var changes = change.TextChanges;
			var provider = _GetProvider(ci);
			isComplex = changes.Length > 1 || change.NewPosition.HasValue || provider is CiComplProvider.Override or CiComplProvider.XmlDoc;
			
			var lastChange = changes.Last();
			s = lastChange.NewText;
			if (s.NE()) return CiComplResult.None; //Roslyn bug: fails if there are parameters of type nint. Same in VS. Tried a workaround (modify ci.Properties["Symbols"]), but unsuccessfully. Tried to find/modify the Roslyn code, but it's too deep.
			var span = lastChange.Span;
			startPos = span.Start;
			len = span.Length + codeLenDiff;
			if (isComplex) { //xml doc, override, regex
				int newPos = change.NewPosition ?? -1;
				if (ch != default && newPos >= 0) return CiComplResult.None;
				switch (provider) {
				case CiComplProvider.Override:
					newPos = -1;
					if (App.Settings.ci_formatTabIndent) {
						//Replace 4 spaces with tab. Make { in same line.
						s = s.Replace("    ", "\t").RxReplace(@"\R\t*\{", " {", 1);
						//Correct indent. 
						int indent = s.FindNot("\t"), indent2 = doc.aaaLineIndentFromPos(true, currentFrom);
						if (indent > indent2) s = s.RxReplace("(?m)^" + new string('\t', indent - indent2), "");
					}
					break;
				case CiComplProvider.XmlDoc:
					if (!s.Ends('>') && s.RxMatch(@"^<?(\w+)($| )", 1, out string tag)) {
						if (CodeInfo.GetDocumentAndFindNode(out _, out var n1, span.Start, findInsideTrivia: true) && null == n1.GetAncestorOrThis<XmlNameAttributeSyntax>()) { //not in <tag attr="|">
							string lt = s.Starts('<') || doc.aaaText.Eq(span.Start - 1, '<') ? "" : "<";
							if (s == tag || (ci.Properties.TryGetValue("AfterCaretText", out var s1) && s1.NE()) && newPos > 0) newPos += 1 + lt.Length;
							s = $"{lt}{s}></{tag}>";
						}
					}
					break;
				}
				using var undo = doc.aaaNewUndoAction();
				bool last = true;
				for (int j = changes.Length; --j >= 0; last = false) {
					var v = changes[j];
					if (last) doc.aaaReplaceRange(true, v.Span.Start, v.Span.End + codeLenDiff, s, moveCurrentPos: newPos < 0);
					else doc.aaaReplaceRange(true, v.Span.Start, v.Span.End, v.NewText);
				}
				if (newPos >= 0) doc.aaaSelect(true, newPos, newPos, makeVisible: true);
				
				return CiComplResult.Complex;
			}
		}
		Debug_.PrintIf(startPos != currentFrom && item.Provider != CiComplProvider.EmbeddedLanguage, $"{currentFrom}, {startPos}");
		
		//if typed space after method or keyword 'if' etc, replace the space with '(' etc. Also add if pressed Tab or Enter.
		CiAutocorrect.EBrackets bracketsOperation = default;
		int caretBack = 0, bracketsFrom = 0, bracketsLen = 0;
		//bool isEnter = key == KKey.Enter;
		string sAppend = null;
		
		if (s.FindAny("({[<") < 0) {
			if (ch == default) { //completed with Enter, Tab, Space or click
				switch (item.kind) {
				case CiItemKind.Method or CiItemKind.ExtensionMethod:
					ch = '(';
					break;
				case CiItemKind.Keyword:
					string name = item.Text;
					switch (name) {
					case "for" or "foreach" or "while" or "lock" or "catch":
					case "if" when !_IsDirective():
					case "fixed" when _IsStartOfStatement(): //else in struct
					//case "using" when _IsStartOfStatement(): //else directive. But can be with or without `()`.
					case "when" when _IsInAncestorNode(startPos, n => (n is CatchClauseSyntax, n is SwitchSectionSyntax)): //`catch(...) when`
						(ch, sAppend) = ('(', " ()");
						break;
					//rejected: append ` {  }`. Too many confusing features isn't good. Probably rarely somebody uses or likes it. Also, would need to delete `;`.
					//case "try" or "finally":
					//case "get" or "set" or "add" or "remove" or "do" or "unsafe" or "checked" or "unchecked" when isEnter:
					//case "else" when isEnter && !_IsDirective():
					//	ch = '{';
					//	break;
					case "switch":
						if (_IsStartOfStatement()) (ch, sAppend) = ('(', " ()"); //else ch = '{';
						break;
					case "nameof" or "sizeof" or "typeof":
					case "checked" or "unchecked" when !_IsStartOfStatement():
						ch = '(';
						break;
					default:
						if (_NeedParentheses()) ch = '(';
						break;
					}
					break;
				case CiItemKind.Class or CiItemKind.Structure or CiItemKind.Interface or CiItemKind.Enum or CiItemKind.Delegate:
					if (ci.DisplayTextSuffix == "<>") ch = '<';
					else if (_NeedParentheses()) ch = '(';
					break;
				}
				
				bool _IsDirective() => doc.aaaText.Eq(startPos - 1, "#"); //info: CompletionItem of 'if' and '#if' are identical. Nevermind: this code does not detect '# if'.
				
				if (isComplex = ch != default) {
					//if (ch == '{') {
					//	if (isEnter) {
					//		int indent = doc.aaaLineIndentFromPos(true, startPos);
					//		var b = new StringBuilder(" {\r\n");
					//		b.AppendIndent(indent + 1);
					//		b.AppendLine().AppendIndent(indent).Append('}');
					//		sAppend = b.ToString();
					//		caretBack = indent + 3;
					//	} else {
					//		sAppend = " {  }";
					//		caretBack = 2;
					//	}
					//	bracketsFrom = startPos + s.Length + 2;
					//	bracketsLen = sAppend.Length - 3;
					//} else
					if (App.Settings.ci_complParen switch { 0 => isSpace, 1 => true, _ => false } && !data.noAutoSelect && !doc.aaaText.Eq(startPos + len, ch)) { //info: noAutoSelect when lambda argument
						sAppend ??= ch == '(' ? "()" : "<>";
						caretBack = 1;
						bracketsFrom = startPos + s.Length + sAppend.Length - 1;
					} else {
						ch = default;
						sAppend = null;
						isComplex = false;
					}
				}
			} else if (!(ch is '(' or '<' or '[' or '{' || data.noAutoSelect)) { //completed with ;,.?- etc
				if (_NeedParentheses()) sAppend = "()";
			}
			
			bool _NeedParentheses() {
				if (item.kind is CiItemKind.Method or CiItemKind.ExtensionMethod) return true;
				if (ch == '.') return false; //if 'new Word.', often can be eg 'new Word.Word()' but rarely 'new Word().'
				switch (item.kind) {
				case CiItemKind.Class or CiItemKind.Structure or CiItemKind.Enum or CiItemKind.Delegate:
				//if (ci.Properties.TryGetValue("ShouldProvideParenthesisCompletion", out var v1) && v1.Eqi("True")) goto g1; //missing when eg 'new Namespace.Type'
				//break;
				case CiItemKind.Keyword when item.Text is "string" or "object" or "int" or "uint" or "nint" or "nuint" or "long" or "ulong" or "byte" or "sbyte" or "short" or "ushort" or "char" or "bool" or "double" or "float" or "decimal":
					if (CodeInfo.GetDocumentAndFindNode(out _, out var node, startPos)) {
						node = node.Parent;
						if (node is QualifiedNameSyntax) node = node.Parent;
						if (node is ObjectCreationExpressionSyntax) goto g1;
						if (node is AttributeSyntax && item.kind is CiItemKind.Class) return true;
					}
					break;
				}
				return false;
				g1:
				bracketsOperation = CiAutocorrect.EBrackets.NewExpression;
				return true;
				//If 'new Type', adds '()'.
				//If then coder types '[' for 'new Type[]' or '{' for 'new Type { initializers }', autocorrection will replace the '()' with '[]' or '{  }'.
			}
			
			//bool _IsGeneric()
			//	=> ci.Properties.TryGetValue("IsGeneric", out var v1) && v1.Eqi("True");
		}
		
		try {
			if (sAppend == null && s == data.filterText) return CiComplResult.None;
			
			if (!doc.aaaText.Eq(startPos..(startPos + len), s)) doc.aaaSetAndReplaceSel(true, startPos, startPos + len, s); else doc.aaaCurrentPos16 = startPos + len;
			if (sAppend != null) {
				doc.aaaReplaceSel(sAppend);
				if (ch == ';' && caretBack == 0 && doc.aaaCharAt8(doc.aaaCurrentPos8) == ';') { caretBack = -1; isComplex = true; } //skip `;`
			}
			if (caretBack != 0) doc.aaaCurrentPos8 -= caretBack;
			if (bracketsFrom > 0) {
				CodeInfo._correct.BracketsAdded(doc, bracketsFrom, bracketsFrom + bracketsLen, bracketsOperation);
			}
			
			return isComplex ? CiComplResult.Complex : CiComplResult.Simple;
		}
		finally {
			if (ourProvider) {
				switch (item.Provider) {
				case CiComplProvider.Winapi: data.winapi.OnCommitInsertDeclaration(item); break;
				}
			}
			if (isComplex && ch is '(' or '<') {
				CodeInfo._signature.SciCharAdded(doc, ch, methodCompletion: ch == '(' && item.kind is CiItemKind.Method or CiItemKind.ExtensionMethod);
				bool methodCompletion = ch == '(' && item.kind is CiItemKind.Method or CiItemKind.ExtensionMethod; //may need to show enum list for the first method parameter, like when typed '('
				CodeInfo._signature.SciCharAdded(doc, ch, methodCompletion);
			}
		}
		
		//static bool _IsInAncestorNodeOfType<T>(int pos) where T : SyntaxNode
		//	=> CodeInfo.GetDocumentAndFindNode(out _, out var node, pos) && null != node.GetAncestor<T>();
		
		static bool _IsInAncestorNode(int pos, Func<SyntaxNode, (bool yes, bool no)> f) {
			if (!CodeInfo.GetDocumentAndFindNode(out _, out var node, pos)) return false;
			while ((node = node.Parent) != null) {
				//CiUtil.PrintNode(node);
				var (yes, no) = f(node);
				if (yes) return true;
				if (no) return false;
			}
			return false;
		}
		
		bool _IsStartOfStatement() {
			if (!CodeInfo.GetDocumentAndFindToken(out _, out var tok, startPos, metaToo: true)) return false;
			for (var node = tok.Parent; node?.SpanStart == startPos; node = node.Parent) {
				if (node is StatementSyntax or IncompleteMemberSyntax { Parent: CompilationUnitSyntax }) {
					//still can be an expression. Eg if `int j = i sw`, Roslyn assumes `int j = i` is a statement with missing `;`, and `sw` is another statement.
					tok = tok.GetPreviousToken();
					return tok.Kind() is 0 or SyntaxKind.SemicolonToken or SyntaxKind.OpenBraceToken or SyntaxKind.CloseBraceToken or SyntaxKind.ColonToken;
				}
			}
			return false;
		}
	}
	
	/// <summary>
	/// Double-clicked item in list.
	/// </summary>
	public void Commit(SciCode doc, CiComplItem item) => _Commit(doc, item, default, default);
	
	/// <summary>
	/// Tab, Enter, Ctrl/Shift+Enter, Ctrl+;.
	/// </summary>
	public CiComplResult OnCmdKey_Commit(SciCode doc, KKey key) {
		var R = CiComplResult.None;
		if (_data != null) {
			var ci = _popupList.SelectedItem;
			if (key is KKey.Tab) ci ??= _popupList.SuggestedItem;
			if (ci != null) {
				R = _Commit(doc, ci, default, key);
				if (R == CiComplResult.None && key is KKey.Tab or KKey.Enter) R = CiComplResult.Simple; //always suppress Tab and Enter
			}
			_CancelUI();
		}
		return R;
	}
	
	/// <summary>
	/// Esc, Arrow, Page, Home, End.
	/// </summary>
	public bool OnCmdKey_SelectOrHide(KKey key) => _data != null && _popupList.OnCmdKey(key);
	
	static CiComplProvider _GetProvider(CompletionItem ci) {
		var s = ci.ProviderName;
		Debug_.PrintIf(s == null, "ProviderName null");
		if (s == null) return CiComplProvider.Other;
		int i = s.LastIndexOf('.') + 1;
		Debug.Assert(i > 0);
		//print.it(s[i..]);
		if (s.Eq(i, "Symbol")) return CiComplProvider.Symbol;
		if (s.Eq(i, "Keyword")) return CiComplProvider.Keyword;
		if (s.Eq(i, "Cref")) return CiComplProvider.Cref;
		if (s.Eq(i, "XmlDoc")) return CiComplProvider.XmlDoc;
		if (s.Find("EmbeddedLanguage", i) >= 0) return CiComplProvider.EmbeddedLanguage;
		if (s.Eq(i, "Override")) return CiComplProvider.Override;
		//if (s.Eq(i, "ExternAlias")) return CiComplProvider.ExternAlias;
		//if (s.Eq(i, "ObjectAndWith")) return CiComplProvider.ObjectAndWithInitializer;
		//if (s.Eq(i, "AttributeNamedParameter")) return CiComplProvider.AttributeNamedParameter; //don't use because can be mixed with other symbols
		return CiComplProvider.Other;
	}
}
