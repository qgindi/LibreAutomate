extern alias CAW;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;

using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;

using Au.Controls;
using System.Windows;
using System.Windows.Controls;

namespace LA;

partial class TriggersAndToolbars {
#if DEBUG
	public static void Test() {
		//var tt = _CodeAnalysis.GetTriggersType();
		//print.it(tt);
		//if (tt == 0) return;
		//print.it("-----");
		//print.it(_CodeAnalysis.GetTriggersVar(tt));
		
		//print.it(_CodeAnalysis.GetActionTriggersVar());
	}
#endif
	
	static class _CodeAnalysis {
		/// <summary>
		/// Called either to get triggers type (with tt 0) or triggers variable (with tt not 0). But always gets both, because not always can get just one.
		/// In current document looks for a variable of a triggers type. Or in current class looks for a field/property. Also can detect type if this is a standard triggers file.
		/// </summary>
		/// <returns>
		/// tType: if tt 0 - the detected type, or 0 if cannot detect; else tt.
		/// tVar: variable name (or like `triggers.Hotkey`), or null if tVar 0.
		/// onlyActionTriggers: true if tt 0 and found only an ActionTriggers variable.
		/// </returns>
		static (TriggersType tType, string tVar, bool onlyActionTriggers) _GetTriggersTypeOrVar(TriggersType tt) {
			if (!CodeInfo.GetContextAndDocument(out var cd, metaToo: true)) return default;
			var semo = cd.semanticModel;
			
			//Find a local variable of a triggers type (like HotkeyTriggers).
			//	Either nearest declaration, or a trigger like `variable[...]...`.
			//	Search upwards, enumerating sibling statements of current statement and its ancestors.
			//	If tt==0, find any type. Else find tt type.
			//	If not found, find an ActionTriggers variable declaration (for tVar like `triggers.Hotkey`).
			
			var tok = cd.syntaxRoot.FindToken(cd.pos);
			if (tok.IsKind(SyntaxKind.EndOfFileToken)) tok = tok.GetPreviousToken();
			var node = tok.Parent;
			if (node is MemberDeclarationSyntax and not GlobalStatementSyntax && cd.pos <= tok.SpanStart) node = (tok = tok.GetPreviousToken()).Parent;
			bool? andThis = null;
			string typeName = tt == 0 ? null : $"Au.Triggers.{tt}Triggers", actionTriggersVar = null;
			if (node is BlockSyntax) {
				SyntaxNode n1 = null;
				if (node.Span.ContainsInside(cd.pos)) n1 = tok.IsKind(SyntaxKind.CloseBraceToken) ? node.ChildNodes().LastOrDefault() : node.ChildNodes().FirstOrDefault();
				node = n1 ?? node.GetAncestor<StatementSyntax>();
			} else {
				node = node?.GetAncestorOrThis<StatementSyntax>();
				andThis = node is ExpressionStatementSyntax && (node.SpanStart <= cd.pos || tok.GetPreviousToken().IsKind(SyntaxKind.OpenBraceToken));
			}
			while (node != null) {
				foreach (var n in node.PreviousSiblings(andThis: andThis ?? node is ExpressionStatementSyntax)) {
					//CiUtil.PrintNode(n);
					if (n is LocalDeclarationStatementSyntax lds) {
						if (lds.Declaration.Variables.Count > 0 && _IsTriggersTypeVarNode(lds.Declaration.Type, out var varType)) {
							var s = lds.Declaration.Variables[0].Identifier.Text;
							if (varType != 0) return (varType, s, false);
							actionTriggersVar ??= s;
						}
					} else if (n is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } aes }) {
						if (aes.Left is ElementAccessExpressionSyntax ea && _IsTriggersTypeVarNode(ea.Expression, out var varType)) {
							if (varType != 0) return (varType, ea.Expression.ToString(), false);
						}
					}
				}
				
				node = node.Parent;
				if (node is BlockSyntax || node is not StatementSyntax) node = node?.GetAncestor<StatementSyntax>();
				andThis = null;
				
				//never mind: can be static func.
				//never mind: other kinds of local var declaration, eg out.
				
				bool _IsTriggersTypeVarNode(SyntaxNode varOrTypeExpr, out TriggersType varType) {
					if (semo.GetTypeInfo(varOrTypeExpr).Type is { } ty) {
						//print.it(ty, n);
						return _IsTriggersType(ty, out varType);
					}
					varType = 0;
					return false;
				}
			}
			
			//If standard triggers file, use the filename, and skip the "find field/property" code.
			if (tt == 0 || actionTriggersVar == null) {
				if (_IsStdTriggersFile(cd.sci.EFile) is var tts && tts != 0) {
					if (tt == 0) tt = tts;
					actionTriggersVar ??= "Triggers";
				}
			}
			
			//Find a field or property.
			actionTriggersVar ??= _FindActionTriggersPropertyOrField(semo, cd.pos);
			
			return (
				tt,
				tt == 0 ? null : (actionTriggersVar ?? "Triggers") + "." + tt,
				tt == 0 && actionTriggersVar != null
				);
			
			bool _IsTriggersType(ITypeSymbol ty, out TriggersType varType) {
				varType = 0;
				var s = ty.ToString();
				if (s.Like("Au.Triggers.*Triggers")) {
					if (tt == 0) {
						switch (s[12..^8]) {
						case "Hotkey": varType = TriggersType.Hotkey; return true;
						case "Autotext": varType = TriggersType.Autotext; return true;
						case "Mouse": varType = TriggersType.Mouse; return true;
						case "Window": varType = TriggersType.Window; return true;
						case "Action": return true;
						}
					} else {
						if (s == typeName) { varType = tt; return true; }
						if (s.Eq(12..^8, "Action")) return true;
					}
				} else if (s == "Au.Triggers.TASimpleReplace") {
					if (tt == 0) { varType = TriggersType.Autotext; return true; } //note: the variable will be incorrect, but we need only type
				}
				return false;
			}
		}
		
		static TriggersType _IsStdTriggersFile(FileNode fn) {
			if (fn?.IsClass == true) {
				int i = fn.Name.Eq(true, "Hotkey triggers.cs", "Autotext triggers.cs", "Mouse triggers.cs", "Window triggers.cs");
				if (i > 0 && fn.ItemPath.Eqi(@"\@Triggers and toolbars\Triggers\" + fn.Name)) return (TriggersType)i;
			}
			return 0;
		}
		
		static string _FindActionTriggersPropertyOrField(SemanticModel semo, int pos) {
			return CiUtil.EnumPropertiesAndFields(semo, pos).FirstOrDefault(o => o.GetMemberType().ToString() == "Au.Triggers.ActionTriggers")?.Name;
		}
		
		public static TriggersType GetTriggersType() {
			return _GetTriggersTypeOrVar(0).tType;
		}
		
		public static string GetTriggersVar(TriggersType tt) {
			Debug.Assert(tt != 0);
			return _GetTriggersTypeOrVar(tt).tVar ?? "Triggers." + tt;
		}
		
		public static string GetActionTriggersVar() {
			if (CodeInfo.GetContextAndDocument(out var cd)) {
				var semo = cd.semanticModel;
				if (CiUtil.GetLocalVariablesAt(semo, cd.pos, o => o.ToString() == "Au.Triggers.ActionTriggers").FirstOrDefault() is { } localSym) return localSym.Name;
				if (_FindActionTriggersPropertyOrField(semo, cd.pos) is { } pf) return pf;
			}
			return "Triggers";
		}
		
		public static bool IsNonStandardFileWithTriggers() {
			if (App.Model.CurrentFile is not { } f) return false;
			if (_IsStdTriggersFile(f) != 0) return false;
			var v = _GetTriggersTypeOrVar(0);
			return v.tType != 0 || v.onlyActionTriggers;
		}
		
		/// <summary>
		/// If current document is a standard triggers file, moves caret to a place where a new trigger can be inserted (if need).
		/// </summary>
		/// <returns>true if caret was in a correct place or if moved. false if not a standard triggers file.</returns>
		public static bool MoveCaretForNewTriggerOrScope() {
			if (!CodeInfo.GetContextAndDocument(out var cd, metaToo: true)) return false;
			var semo = cd.semanticModel;
			
			var programSym = semo.Compilation.GlobalNamespace.GetTypeMembers("Program").FirstOrDefault();
			var attrSym = programSym?.GetTypeMembers("TriggersAttribute").FirstOrDefault();
			if (attrSym == null) return false;
			
			var m = semo.GetEnclosingSymbol<IMethodSymbol>(cd.pos, default);
			while (m != null && !m.IsOrdinaryMethod()) m = m.ContainingSymbol as IMethodSymbol;
			
			if (m != null && _HasTriggersAttribute(m)) return true;
			m = programSym.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(o => o.Locations[0].SourceTree == semo.SyntaxTree && _HasTriggersAttribute(o));
			if (m == null) return false; //this file has no methods with [Triggers]
			
			if (m.Locations[0].FindNode(default) is MethodDeclarationSyntax md) {
				var last = md.Body.Statements.OfType<ExpressionStatementSyntax>().LastOrDefault(o => o.Expression is AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax eae });
				if (last != null) cd.pos = last.FullSpan.End;
				else if (md.Body.Statements.OfType<IfStatementSyntax>().FirstOrDefault() is { } firstIf) cd.pos = firstIf.FullSpan.Start;
				else cd.pos = md.Body.CloseBraceToken.SpanStart;
				
				cd.sci.aaaGoToPos(true, cd.pos);
			}
			
			return true;
			
			bool _HasTriggersAttribute(IMethodSymbol m) => m.GetAttributes().Any(o => o.AttributeClass == attrSym);
		}
		
	}
	
	public record struct FoundTrigger(FileNode file, int pos, string type, string trigger, string scope);
	
	public static async Task<List<FoundTrigger>> FindTriggersInCodeAsync(FileNode thisFile) {
		if (thisFile?.IsCodeFile != true) return null;
		
		var ttFolder = GetProject(create: false);
		if (ttFolder == null) return null;
		
		List<FoundTrigger> aResults = new();
		string nameCs = thisFile.Name, nameNoCs = thisFile.DisplayName, parentDir = thisFile.ItemPath[..^nameCs.Length];
		
		using var ws = new CiWorkspace(ttFolder, CiWorkspace.Caller.OtherPR);
		await Task.Run(_Work);
		return aResults;
		
		void _Work() {
			var comp = ws.GetCompilation();
			
			INamedTypeSymbol
				ntHotkey = comp.GetTypeByMetadataName("Au.Triggers.HotkeyTriggers"),
				ntAutotext = comp.GetTypeByMetadataName("Au.Triggers.AutotextTriggers"),
				ntMouse = comp.GetTypeByMetadataName("Au.Triggers.MouseTriggers"),
				ntWindow = comp.GetTypeByMetadataName("Au.Triggers.WindowTriggers"),
				ntToolbar = comp.GetTypeByMetadataName("Au.toolbar"),
				ntMenu = comp.GetTypeByMetadataName("Au.popupMenu"),
				ntTriggerArgs = comp.GetTypeByMetadataName("Au.Triggers.TriggerArgs"),
				ntMTItem = comp.GetTypeByMetadataName("Au.Types.MTItem"),
				ntScopes = comp.GetTypeByMetadataName("Au.Triggers.TriggerScopes");
			
			INamedTypeSymbol ntTriggersAttribute = null, ntToolbarsAttribute = null;
			if (comp.GlobalNamespace.GetTypeMembers("Program").FirstOrDefault() is { } symProgram) {
				ntTriggersAttribute = symProgram.GetTypeMembers("TriggersAttribute").FirstOrDefault();
				ntToolbarsAttribute = symProgram.GetTypeMembers("ToolbarsAttribute").FirstOrDefault();
			}
			
			//var files = ws.Meta.CodeFiles.Zip(comp.SyntaxTrees, (cf, tree) => (cf: cf, tree: tree, semo: comp.GetSemanticModel(tree))).ToArray();
			var files = ws.Meta.CodeFiles.Zip(comp.SyntaxTrees, (f, tree) => (cf: f, tree: tree, semo: f.isC ? null : comp.GetSemanticModel(tree))).Where(o => !o.cf.isC).ToArray(); //skip meta c and global.cs
			
			foreach (var (cf, tree, semo) in files) {
				//print.it($"<><lc greenyellow>{cf.f}<>");
				var root = tree.GetCompilationUnitRoot();
				
				//look for strings equal to the script name or path
				foreach (var tok in root.DescendantTokens()) {
					var tk = tok.Kind();
					if (tk is SyntaxKind.StringLiteralToken or SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.InterpolatedStringTextToken) {
						var s = tok.ValueText;
						string sn;
						if (s.Ends(sn = nameCs, true) || s.Ends(sn = nameNoCs, true)) {
							if (s.Length - sn.Length is int j && j > 0) if (j != parentDir.Length || !s.Starts(parentDir, true)) continue;
							
							int found = 0;
							_GetTrigger(tok.Parent, semo, cf, 0, false, ref found);
							
							//if trigger not found, add as string
							if (found == 0) {
								var n = tok.Parent; if (tk == SyntaxKind.InterpolatedStringTextToken) n = n.Parent;
								if (n.GetAncestors().FirstOrDefault(o => o is StatementSyntax or ArrowExpressionClauseSyntax) is { } ss) {
									//var scope = semo.GetEnclosingSymbol(ss.SpanStart)?.Name; //empty if lambda
									var scope = ss.GetAncestor<MemberDeclarationSyntax>() switch { MethodDeclarationSyntax k => k.Identifier.Text, PropertyDeclarationSyntax k => k.Identifier.Text, EventDeclarationSyntax k => k.Identifier.Text, _ => null };
									aResults.Add(new(cf.f, n.SpanStart, null, ss.ToString(), scope));
								}
							}
						}
					}
				}
			}
			
			//If node is in a trigger action, finds the trigger statement and gets the triggers type.
			//If found, adds to the _FindTriggerResult list.
			void _GetTrigger(SyntaxNode node, SemanticModel semo, MCCodeFile cf, int level, bool inFuncOfTriggersActionType, ref int result) {
				for (var n = node; n != null; n = n.Parent) {
					if (n is LocalFunctionStatementSyntax) {
						if (level < 5 && semo.GetDeclaredSymbol(n) is IMethodSymbol ims && n.GetAncestor<BlockSyntax>() is { } scope) {
							_FuncReferences(ims, scope, semo, cf, level, ref result);
						}
						break;
					} else if (n is MethodDeclarationSyntax) {
						if (level < 5 && semo.GetDeclaredSymbol(n) is IMethodSymbol ims) {
							if (ntTriggersAttribute != null && ims.GetAttributes().Any(o => o.AttributeClass == ntTriggersAttribute || o.AttributeClass == ntToolbarsAttribute)) break;
							foreach (var v in files) {
								_FuncReferences(ims, v.tree.GetCompilationUnitRoot(), v.semo, v.cf, level, ref result);
							}
						}
						break;
					} else if (n is AnonymousFunctionExpressionSyntax) {
						inFuncOfTriggersActionType = _IsFuncOfTriggersActionType(semo.GetSymbolInfo(n).Symbol as IMethodSymbol);
					} else if (!inFuncOfTriggersActionType) {
						//is it a toolbar/menu label?
						bool isLabel = n is BracketedArgumentListSyntax;
						if (!isLabel && n is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax maes }) {
							//probably `script.run("theString")`, but can be eg `toolbarOrMenu.Add("theString")`
							var ty = semo.GetTypeInfo(maes.Expression).Type;
							isLabel = ty == ntToolbar || ty == ntMenu || ty == ntHotkey || ty == ntAutotext || ty == ntMouse || ty == ntWindow;
						}
						if (isLabel) { result |= 2; break; }
					} else if (n is InvocationExpressionSyntax or AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax }) {
						ExpressionSyntax expr = null;
						BaseArgumentListSyntax arglist = null;
						if (n is AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax eaes }) { //`t[...] = ...`
							expr = eaes.Expression;
							arglist = eaes.ArgumentList;
						} else if (n is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax maes } ies) { //maybe `t.Add(...)` or `t.Menu(...)`
							expr = maes.Expression;
							arglist = ies.ArgumentList;
						} else continue;
						
						if (semo.GetTypeInfo(expr).Type is { } ty) {
							string triggersType = null;
							if (ty == ntHotkey || ty == ntAutotext || ty == ntMouse || ty == ntWindow) {
								triggersType = ty.Name[..^8];
							} else if (ty == ntToolbar) {
								triggersType = "Toolbar";
							} else if (ty == ntMenu) { //a submenu of a toolbar?
								foreach (var v in n.GetAncestors<InvocationExpressionSyntax>()) {
									if (v.Expression is MemberAccessExpressionSyntax maes && semo.GetTypeInfo(maes.Expression).Type is { } ty2) {
										if (ty2 == ntToolbar) { triggersType = "Toolbar"; break; }
									}
								}
							}
							
							if (triggersType != null) {
								var args = arglist.Arguments;
								string arguments = null, scope = null;
								if (ty == ntToolbar || ty == ntMenu) {
									if (n.GetAncestor<MethodDeclarationSyntax>() is { } mds) arguments = mds.Identifier.Text;
									else arguments = args[0].ToString();
								} else {
									arguments = args.ToString();
									if (ty != ntWindow) scope = _GetScope(n, semo);
									//funcOf = _GetFuncOf(n, semo);
								}
								
								aResults.Add(new(cf.f, args[0].SpanStart, triggersType, arguments, scope));
								result |= 1;
								break;
							}
						}
					}
				}
			}
			
			void _FuncReferences(IMethodSymbol ims, SyntaxNode scope, SemanticModel semo, MCCodeFile cf, int level, ref int found) {
				var range = scope.Span;
				string name = ims.Name, code = cf.code;
				bool? isOfTriggersActionType = null;
				for (int i = range.Start; ; i += name.Length) {
					i = code.Find(name, i..range.End);
					if (i < 0) break;
					if (!SyntaxFacts.IsIdentifierPartCharacter(code.At_(i - 1)) && !SyntaxFacts.IsIdentifierPartCharacter(code.At_(i + name.Length))) {
						var tok = scope.FindToken(i);
						if (tok.SpanStart != i || tok.Parent is not IdentifierNameSyntax) continue; //info: also skips the declaration, because it's not IdentifierNameSyntax
						var sym2 = semo.GetSymbolInfo(tok.Parent).Symbol;
						if (sym2 != ims) continue;
						var node = tok.Parent.Parent;
						if (node is InvocationExpressionSyntax or AssignmentExpressionSyntax or ArgumentSyntax) {
							bool tat = node is InvocationExpressionSyntax ? false : (isOfTriggersActionType ??= _IsFuncOfTriggersActionType(ims));
							_GetTrigger(node, semo, cf, level + 1, tat, ref found);
						}
					}
				}
			}
			
			bool _IsFuncOfTriggersActionType(IMethodSymbol ims) {
				if (ims != null && ims.ReturnsVoid && ims.Parameters.Length == 1 && !ims.IsGenericMethod && ims.Parameters[0] is { RefKind: RefKind.None, Type: { BaseType: var t2 } t1 }) {
					if (t2 == ntTriggerArgs || t1 == ntTriggerArgs || t2 == ntMTItem || t1 == ntMTItem) return true;
				}
				return false;
			}
			
			string _GetScope(SyntaxNode node, SemanticModel semo) {
				foreach (var stat in _EnumStatementsUp(node)) {
					if (stat is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax maes } ies }) {
						if (maes.Name.Identifier.Text is string s1 && s1 is "Window" or "Windows" or "Again" or "AllWindows") {
							if (semo.GetTypeInfo(maes.Expression).Type == ntScopes) {
								if (s1 == "AllWindows") return null;
								return s1 + ies.ArgumentList;
							}
						}
					}
				}
				return null;
			}
			
			//rejected. Need much more code to detect whether NextTrigger would be applied to this trigger. Can be unreliable.
			//string _GetFuncOf(SyntaxNode node, SemanticModel semo) {
			//	foreach (var stat in _EnumStatementsUp(node)) {
			//		if (stat is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax maes } aes }) {
			//			if (maes.Name.Identifier.Text is string s1 && s1 is "FollowingTriggers" or "FollowingTriggersBeforeWindow" or "NextTrigger" or "NextTriggerBeforeWindow") {
			//				if (semo.GetTypeInfo(maes.Expression).Type == ntFuncs) {
			//					if (aes.Right is LiteralExpressionSyntax) return null; // `= null`
			//					return aes.Right.ToString();
			//				}
			//			}
			//		}
			//	}
			//	return null;
			//}
			
			IEnumerable<StatementSyntax> _EnumStatementsUp(SyntaxNode n) {
				while (n.GetAncestorOrThis<StatementSyntax>() is { } stat) {
					foreach (var v in stat.PreviousSiblings()) {
						if (v is StatementSyntax ss) {
							yield return ss;
						} else {
							Debug_.Print(v);
							break;
						}
					}
					n = stat.Parent;
					if (n is BlockSyntax { Parent: not BlockSyntax }) n = n.Parent;
				}
			}
		}
	}
	
	/// <summary>
	/// Returns true if <i>f</i> is <c>@"\@Triggers and toolbars\Triggers and toolbars.cs"</c>.
	/// </summary>
	public static bool IsTtScipt(FileNode f) {
		return f != null && f.IsCodeFile && f.Name.Eqi("Triggers and toolbars.cs") && f.Parent.Name.Eqi("@Triggers and toolbars") && f.Parent.Parent.Parent == null;
	}
	
	public static async Task<List<FoundTrigger>> FindAllTriggersAsync(FileNode thisFile) {
		if (!thisFile.IsExecutableDirectly()) return null;
		
		//action triggers
		var ar = await FindTriggersInCodeAsync(thisFile) ?? new();
		
		//scheduler
		if (await WinScheduler.GetScriptTriggersAsync(thisFile) is { } ast) {
			foreach (var v in ast) {
				ar.Add(new(null, -1, "Scheduled", v.trigger, v.task));
			}
		}
		
		//startup script
		foreach (var v in App.Model.GetStartupScriptsExceptDisabled()) {
			if (v.f == thisFile) {
				ar.Add(new(null, -1, "Startup", "When this workspace loaded", null));
				break;
			}
		}
		
		//test script
		foreach (var v in App.Model.Root.Descendants()) {
			if (v.TestScript == thisFile) {
				ar.Add(new(v, -1, "Test script", v.Name, null));
			}
		}
		
		//rejected: preBuid/postBuild, shortcut
		
		return ar;
	}
	
	public static async void AllTriggersMenu(FileNode thisFile) {
		if (thisFile == null) return;
		//if (thisFile.IsClass) thisFile = thisFile.GetProjectMainOrThis();
		var a = await FindAllTriggersAsync(thisFile);
		
		var p = new KPopupListBox { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse, PlacementTarget = App.Wmain };
		p.OK += o => {
			if (o is ListBoxItem { Tag: Action click }) click();
		};
		
		void _Add(string s, Action click = null) {
			var li = new ListBoxItem { Content = s };
			if (click != null) li.Tag = click; else li.IsEnabled = false;
			p.Control.Items.Add(li);
		}
		
		if (a == null) {
			_Add("This file isn't runnable as a script");
		} else if (a.Count == 0) {
			_Add("No triggers found");
		} else {
			int schedTrigger = 0;
			foreach (var v in a) {
				static string _Limit(string s) {
					if (s != null) {
						s = s.Replace("\t", "    ");
						//s = string.Join('\n', s.Lines(noEmpty: true).Select(o => o.Limit(200))); //don't need. WPF limits the window width to the work area and adds hscrollbar.
					}
					return s;
				}
				string trigger = _Limit(v.trigger), scope = _Limit(v.scope);
				if (v.file != null) {
					if (v.type == "Test script") {
						_Add($"{v.type} of {trigger}", () => App.Model.OpenAndGoTo(v.file));
					} else if (v.type == null) {
						_Add($"Found in {scope}:\n    {trigger}", () => App.Model.OpenAndGoTo(v.file, columnOrPos: v.pos));
					} else {
						var s = $"{v.type} {trigger}";
						if (scope != null) s += "\n    Scope: " + scope;
						_Add(s, () => App.Model.OpenAndGoTo(v.file, columnOrPos: v.pos));
					}
				} else if (v.type == "Scheduled") {
					int i = ++schedTrigger;
					_Add(trigger, () => DSchedule.ShowFor(thisFile, i));
				} else if (v.type == "Startup") {
					_Add(trigger, () => DOptions.AaShow(DOptions.EPage.Workspace));
				}
			}
		}
		
		p.IsOpen = true;
	}
}
