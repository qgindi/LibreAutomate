extern alias CAW;

using Au.Controls;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;

namespace LA;

class CiErrors {
	record struct _Diagnostic(Diagnostic d, int start, int end) {
		public ErrorCode Code => (ErrorCode)d.Code;
	}
	
	SemanticModel _semo;
	List<_Diagnostic> _codeDiag;
	readonly List<StartEndText> _stringErrors = new();
	readonly List<StartEndText> _metaErrors = new();
	StartEnd _metaRange;
	
	/// <param name="start16"></param>
	/// <param name="end16"></param>
	/// <param name="pasting">true on paste, drop or `InsertCode.Statements`</param>
	/// <param name="pastingSilent">true if `InsertCode.Statements`</param>
	public void Indicators(int start16, int end16, bool pasting = false, bool pastingSilent = false) {
		bool pastedWithUsings = false;
		gStart:
		_semo = null;
		
		if (!CodeInfo.GetContextAndDocument(out var cd, 0, metaToo: true)) return;
		var doc = cd.sci;
		var code = cd.code;
		if (end16 < code.Length && code.Eq(end16 - 1, '\n')) end16--; //don't include error from next line
		if (end16 <= start16) return;
		bool has = false;
		var semo = cd.semanticModel;
		
		var aDiag = semo.GetDiagnostics(TextSpan.FromBounds(start16, end16));
		
		if (!aDiag.IsDefaultOrEmpty) {
			if (pasting && !pastingSilent && !pastedWithUsings && _PastedWithUsings(cd, aDiag, ref start16, ref end16)) {
				pastedWithUsings = true;
				goto gStart;
			}
			
			_codeDiag = new(aDiag.Length);
			foreach (var d_ in aDiag) {
				var d = d_;
				if (d.IsSuppressed) continue;
				var loc = d.Location; if (!loc.IsInSource) continue;
				var span = loc.SourceSpan;
				//print.it(d.Severity, span, d.Id);
				int start = Math.Min(span.Start, code.Length), end = Math.Clamp(span.End, start, code.Length);
				if (end == start) {
					if (end < code.Length && code[end] is not ('\r' or '\n')) end++;
					else if (start > 0) start--;
				}
				var ec = (ErrorCode)d.Code;
				if (start == 0 && ec == ErrorCode.WRN_UnprocessedXMLComment) continue; //XML comment at start
				
				//workaround for: when starting to type the last top-level statement, if it's a word (a C# type or non-keyword), error
				//	"A namespace cannot directly contain members such as fields or methods". Users would think it's a bug of this program.
				//	Another workaround would be to add ';' after. But it does not work if it's a C# type eg 'int'.
				//	Let's ignore it in editor. It's a lesser evil. And nobody will compile such unfinished code.
				//if (ec == ErrorCode.ERR_NamespaceUnexpected) continue;
				if (ec == ErrorCode.ERR_NamespaceUnexpected) {
					//If unknown name, convert to ERR_NameNotInContext. Then on mouse hover will display tooltip with links to add using directive etc.
					var d2 = cd.document.WithText(SourceText.From(code.Insert(end, ";")));
					var m2 = d2.GetSemanticModelAsync().Result_();
					//print.it(m2.GetDiagnostics(span));
					d = m2.GetDiagnostics(span).FirstOrDefault(o => (ErrorCode)o.Code == ErrorCode.ERR_NameNotInContext);
					if (d == null) continue;
					ec = ErrorCode.ERR_NameNotInContext;
				}
				
				if (!has) doc.EInicatorsDiag_(has = true);
				var indic = d.Severity switch { DiagnosticSeverity.Error => SciTheme.Indic.Error, DiagnosticSeverity.Warning => SciTheme.Indic.Warning, DiagnosticSeverity.Info => SciTheme.Indic.Info, _ => SciTheme.Indic.DiagHidden };
				doc.aaaIndicatorAdd(indic, true, start..end);
				_codeDiag.Add(new(d, start, end));
				
				if (d.Severity == DiagnosticSeverity.Error) {
					switch (ec) {
					case ErrorCode.ERR_NameNotInContext
					or ErrorCode.ERR_SingleTypeNameNotFound
					or ErrorCode.ERR_NoSuchMemberOrExtension
					or ErrorCode.ERR_NoSuchMemberOrExtensionNeedUsing
					or ErrorCode.ERR_UnimplementedInterfaceMember
					or ErrorCode.ERR_UnimplementedAbstractMethod
					or ErrorCode.ERR_BadBinaryOps:
						_semo = semo;
						break;
					}
				}
			}
		}
		if (_metaErrors.Count > 0) {
			int offs = 0; //!=0 if modified text before metacomments
			if (cd.meta.end - cd.meta.start == _metaRange.end - _metaRange.start) offs = cd.meta.start - _metaRange.start;
			
			foreach (var v in _metaErrors) {
				int from = v.start + offs, to = v.end + offs;
				if (to <= start16 || from >= end16) continue;
				if (!has) doc.EInicatorsDiag_(has = true);
				doc.aaaIndicatorAdd(SciTheme.Indic.Error, true, from..to);
			}
		}
		_Strings(semo, cd, start16, end16);
		if (_stringErrors.Count > 0) {
			if (!has) doc.EInicatorsDiag_(has = true);
			foreach (var v in _stringErrors) {
				doc.aaaIndicatorAdd(SciTheme.Indic.Warning, true, v.start..v.end);
			}
		}
		if (!has) {
			doc.EInicatorsDiag_(false);
			_codeDiag = null;
		} else if (pasting && _semo != null) {
			//insert missing using directives
			List<_MissingUsingError> aMissingUsing = null;
			foreach (var v in _codeDiag) {
				if (_MissingUsingError.IsMissingUsingError(v.Code, out bool extMethod)) {
					var mu = new _MissingUsingError(code, v.start, v.end, extMethod, _semo);
					_MissingUsingError.AddToList(ref aMissingUsing, mu);
				}
			}
			//var e1 = _codeDiag.Where(o => o.d.Severity == DiagnosticSeverity.Error); print.it(e1.Count(), e1);
			//print.it(amu.Lenn_(), amu);
			
			List<string> usings = _GetMissingUsings(aMissingUsing);
			if (usings != null) {
				//uncheck some usings that are rarely used and cause errors
				bool uncheckForms = usings.Count > 1 && usings.Any(o => o is "System.Windows" or "System.Windows.Controls" or "System.Windows.Controls.Primitives" or "System.Windows.Input");
				bool uncheckDrawing = usings.Count > 1 && usings.Contains("System.Windows.Media");
				if (pastingSilent && uncheckForms) usings.Remove("System.Windows.Forms");
				if (pastingSilent && uncheckDrawing) usings.Remove("System.Drawing");
				
				if (!pastingSilent) {
					var d = new CheckListDialog("Add missing using directives?");
					foreach (var u in usings) {
						bool check = true;
						if (uncheckForms && u is "System.Windows.Forms") check = false;
						if (uncheckDrawing && u is "System.Drawing") check = false;
						d.Add(u, check);
					}
					if (!d.ShowDialog(doc) || !d.ResultItems.Any()) return;
					usings = d.ResultItems.ToList();
				}
				if (usings.Count > 0) InsertCode.UsingDirective(string.Join(';', usings), true);
				if (!pastingSilent && usings.Count > 1) print.it("Info: multiple using directives have been added. If it causes 'ambiguous reference' errors, remove one of usings displayed in the error tooltip. If that does not work, undo and remove other using.");
			}
		}
	}
	
	void _Strings(SemanticModel semo, CodeInfo.Context cd, int start16, int end16) {
		_stringErrors.Clear();
		var code = cd.code;
		ArgumentListSyntax keysArgs = null;
		foreach (var node in semo.Root.DescendantNodes(TextSpan.FromBounds(start16, end16))) {
			var format = CiUtil.GetParameterStringFormat(node, semo, false, ignoreInterpolatedString: true);
			if (format is PSFormat.None or PSFormat.RegexpReplacement) continue;
			var tok1 = node.GetFirstToken();
			var s = tok1.ValueText; //replaced escape sequences
			if (s.Length == 0) continue;
			string es = null;
			try {
				switch (format) {
				case PSFormat.NetRegex:
					new System.Text.RegularExpressions.Regex(s); //never mind: may have 'options' argument, eg ECMAScript or Compiled
					break;
				case PSFormat.Regexp:
					new regexp(s);
					break;
				case PSFormat.Wildex:
					if (s.Starts("***")) s = s[(s.IndexOf(' ') + 1)..]; //eg wnd.Child("***elmName ...")
					if (s.Starts("**")) new wildex(s);
					break;
				case PSFormat.Keys:
					if (s[0] is '!' or '%') break;
					
					//if keys.send("arg1", "arg2"), use single keys instance to validate all args.
					//	Else possible false positives such as if second arg is ")".
					var args = node.GetAncestor<ArgumentListSyntax>();
					if (args == null || args == keysArgs) break;
					keysArgs = args;
					
					var k = new keys(null);
					foreach (var nk in args.DescendantNodes()) {
						if (nk is ArgumentSyntax) continue;
						if (nk is LiteralExpressionSyntax les && nk.Kind() == SyntaxKind.CharacterLiteralExpression) {
							if (les.Token.Value is char c1) k.AddChar(c1);
						} else {
							if (CiUtil.GetParameterStringFormat(nk, semo, false) != PSFormat.Keys) continue;
							s = nk.GetFirstToken().ValueText;
							if (s.Length == 0 || s[0] is '!' or '%') continue; //never mind: can be "keys"+"!keys"
							try { k.AddKeys(s); }
							catch (ArgumentException ex) {
								var e = ex.Message;
								//print.it(e); CiUtil.PrintNode(nk); CiUtil.PrintNode(nk.Parent);
								//detect some common valid cases like "keys*"+x or $"keys*{x}"
								if (((s.Ends('*') || s.Starts('*')) && e.Contains("<<<*")) || (s.Ends('_') && e.Contains("<<<_>>>"))) {
									if (nk.Parent is not ArgumentSyntax) continue; //eg like "Tab*"+5 or $"Tab*{5}"
								}
								_AddError(nk, e);
							}
						}
					}
					break;
				case PSFormat.Hotkey:
					if (!keys.more.parseHotkeyString(s, out _, out _))
						_AddError(node, "Invalid hotkey string.");
					break;
				case PSFormat.HotkeyTrigger:
					if (!keys.more.parseTriggerString(s, out _, out _, out _, false))
						_AddError(node, "Invalid hotkey string.");
					break;
				case PSFormat.TriggerMod:
					if (!keys.more.parseTriggerString(s, out _, out _, out _, true))
						_AddError(node, "Invalid modifiers string.");
					break;
				case PSFormat.CodeFile or PSFormat.FileInWorkspace:
					if (null == App.Model.Find(s, format == PSFormat.CodeFile ? FNFind.CodeFile : FNFind.Any, silent: true)) {
						var ae = App.Model.FoundMultiple;
						if (ae != null) {
							var paths = string.Join('\n', ae.Select(o => o.ItemPath));
							_AddError(node, "Multiple found. Use path, or rename some.\nPaths:\n" + paths);
							//never mind: should add links.
						} else _AddError(node, "Not found.");
					}
					break;
				}
			}
			catch (ArgumentException ex) { es = ex.Message; }
			if (es != null) _AddError(node, es);
			
			void _AddError(SyntaxNode node, string es) {
				var span = node.Span;
				_stringErrors.Add(new(span.Start, span.End, es));
			}
		}
	}
	
	public void ClearMetaErrors() => _metaErrors.Clear();
	
	public void AddMetaError(StartEnd metaRange, int from, int to, string s) {
		_metaRange = metaRange;
		_metaErrors.Add(new(from, to > from ? to : from + 1, s));
	}
	
	public void EraseIndicatorsInLine(SciCode doc, int pos8) {
		var (_, start, end) = doc.aaaLineStartEndFromPos(false, pos8, withRN: true);
		doc.aaaIndicatorClear(SciTheme.Indic.DiagHidden, false, start..end);
		doc.aaaIndicatorClear(SciTheme.Indic.Info, false, start..end);
		doc.aaaIndicatorClear(SciTheme.Indic.Warning, false, start..end);
		doc.aaaIndicatorClear(SciTheme.Indic.Error, false, start..end);
	}
	
	public void SciModified(SciCode doc, in Sci.SCNotification n) {
		//clear arrays to prevent showing tooltip because positions changed. But don't clear indicators because we'll update them soon.
		_codeDiag = null;
		_stringErrors.Clear();
		
		if (_pasting.doc != null && n.modificationType.Has(Sci.MOD.SC_MOD_INSERTTEXT | Sci.MOD.SC_PERFORMED_USER)) {
			var p = _pasting; _pasting = default;
			if (doc == p.doc && n.length > 3) {
				int start = doc.aaaPos16(n.position), end = doc.aaaPos16(n.position + n.length);
				string text = doc.aaaText;
				doc.Dispatcher.InvokeAsync(() => { //may need to modify code (can't do it in a scintilla modified notification)
					if (Panels.Editor.ActiveDoc != doc || doc.aaaText != text) return;
					try {
						Indicators(start, end, pasting: true, pastingSilent: p.silent);
					}
					catch (Exception ex) { Debug_.Print(ex); }
				});
			}
		}
	}
	(SciCode doc, bool silent) _pasting;
	
	public void Pasting(SciCode doc, bool silent) { _pasting = (doc, silent); }
	
	public System.Windows.Documents.Section GetPopupTextAt(SciCode doc, int pos8, int pos16, out Action<CiPopupText, string> onLinkClick) {
		onLinkClick = null;
		if (_codeDiag == null && _metaErrors.Count == 0 && _stringErrors.Count == 0) return null;
		if (pos8 < 0) return null;
		int all = doc.Call(Sci.SCI_INDICATORALLONFOR, pos8);
		//print.it(all);
		if (0 == (all & ((1 << SciTheme.Indic.Error) | (1 << SciTheme.Indic.Warning) | (1 << SciTheme.Indic.Info) | (1 << SciTheme.Indic.DiagHidden)))) return null;
		
		var x = new CiText();
		x.StartParagraph();
		
		ErrorCode ecPrev = 0;
		int implPos = -1;
		for (int i = 0, n = _codeDiag?.Count ?? 0; i < n; i++) {
			var v = _codeDiag[i];
			if (pos16 < v.start || pos16 > v.end) continue;
			var d = v.d;
			var s1 = d.Severity switch { DiagnosticSeverity.Error => "Error", DiagnosticSeverity.Warning => "Warning", _ => "Info" };
			x.LineBreak(s1, notIfFirstInParagraph: true);
			x.Append(": " + d.GetMessage());
			
			var ec = (ErrorCode)d.Code;
			if (d.Severity == DiagnosticSeverity.Error) {
				//print.it(ec, d.Id);
				if (_semo == null) continue;
				if (_MissingUsingError.IsMissingUsingError(ec, out bool extMethod) || ec == ErrorCode.ERR_DottedTypeNameNotFoundInNS /*Namespace.NotFound*/) {
					if (ec == ecPrev && !extMethod) continue; //probably "not found 'AbcAttribute'" followed by "not found 'Abc'"
					ecPrev = ec;
					_UsingsEtc(x, v, doc, extMethod);
				} else {
					switch (ec) {
					case ErrorCode.ERR_UnimplementedInterfaceMember or ErrorCode.ERR_UnimplementedAbstractMethod:
						Debug.Assert(implPos == -1 || implPos == v.start);
						implPos = v.start;
						break;
					case ErrorCode.ERR_BadBinaryOps:
						//New users may not know how to use multiple flags, and intuitively try operator +. Let's add more info.
						if (d.GetMessage().RxIsMatch(@"^Operator '\+' cannot be applied to operands of type '(\w+)' and '\1'$")) {
							var n1 = _semo.Root.FindToken(v.start).Parent;
							if (_semo.GetTypeInfo(n1).Type?.IsEnumType() ?? false) {
								x.Append(". Use operator '|'.");
							}
						}
						break;
					}
				}
			} else if (d.Severity == DiagnosticSeverity.Warning) {
				x.Append("\nCopy: ");
				x.Hyperlink($"^wi {d.Id}", d.Id, ", ");
				x.Hyperlink($"^wp {d.Id} {d.Descriptor.Title}", $"#pragma warning disable");
				switch (ec) {
				case ErrorCode.WRN_MissingXMLComment:
					x.Append("\nTo add XML comment, type /// above.");
					break;
				}
			}
		}
		if (implPos >= 0) x.Hyperlink("^ii" + implPos, "\nImplement");
		
		_Also(_metaErrors, "Error: ");
		_Also(_stringErrors, null);
		void _Also(List<StartEndText> a, string prefix) {
			foreach (var v in a) {
				if (pos16 < v.start || pos16 > v.end) continue;
				x.LineBreak(prefix, notIfFirstInParagraph: true);
				if (v.text.Starts("<>") && v.text.RxFindAll(@"(<\+?\w+ .+?)>(.+?)<>", out var am)) {
					int i = 2;
					foreach (var m in am) {
						if (m.Start > i) x.Append(v.text[i..m.Start]);
						x.Hyperlink("^" + m[1].Value, m[2].Value);
						i = m.End;
					}
					if (i < v.text.Length) x.Append(v.text[i..v.text.Length]);
				} else {
					x.Append(v.text);
				}
			}
		}
		
		x.EndParagraph();
		onLinkClick = (ph, e) => _LinkClicked(e);
		return x.Result;
	}
	
	record _MissingUsingError {
		public static bool IsMissingUsingError(ErrorCode ec, out bool extMethod) {
			extMethod = false;
			return ec switch {
				ErrorCode.ERR_NoSuchMemberOrExtension or ErrorCode.ERR_NoSuchMemberOrExtensionNeedUsing => extMethod = true, //these end with (are you missing a using directive...
				ErrorCode.ERR_NameNotInContext or ErrorCode.ERR_SingleTypeNameNotFound => true,
				_ => false,
			};
			//not tested: ERR_GlobalSingleTypeNameNotFound, ERR_DottedTypeNameNotFoundInAgg, ERR_AliasNotFound, ERR_TypeNotFound
		}
		
		public static void AddToList(ref List<_MissingUsingError> a, _MissingUsingError mu) {
			if (mu.isEM && mu.emReceiverType == null) return; //unlikely
			a ??= new();
			foreach (var v in a) if (v.name == mu.name && v.emReceiverType == mu.emReceiverType && v.isGeneric == mu.isGeneric && v.isAttribute == mu.isAttribute) return;
			a.Add(mu);
		}
		
		public _MissingUsingError(string code, int start, int end, bool extMethod, SemanticModel semo) {
			int end2 = code.IndexOf('<', start, end - start);
			if (end2 < 0) end2 = end; else isGeneric = true;
			name = code[start..end2];
			isAttribute = !extMethod && _IsAttributeNameWithoutSuffix(name, start, semo);
			if (isEM = extMethod) emReceiverType = _GetExtensionMethodReceiverType(semo, start);
			Debug_.PrintIf(extMethod && emReceiverType == null, "failed to get extension method receiver type"); //unlikely
		}
		
		public readonly string name;
		public readonly bool isEM, isGeneric, isAttribute;
		public readonly ITypeSymbol emReceiverType;
	}
	
	List<string> _GetMissingUsings(List<_MissingUsingError> a) {
		if (a.NE_()) return null;
		List<string> usings = null;
		var compilation = _semo.Compilation;
		var stack = new List<string>();
		int need = 0; foreach (var v in a) if (v.isEM) need |= 2; else need |= 1;
		//var p1 = perf.local();
		//var p1 = new perf.Instance { Incremental = true };
		_EnumNamespace(compilation.GlobalNamespace);
		//p1.Write();
		//p1.NW();
		
		//CONSIDER: async, because slow. Or use AssemblyMetadata.CachedSymbols (internal), it's faster except first time.
		void _EnumNamespace(INamespaceSymbol ns) {
			bool found = false;
			foreach (var nt in ns.GetMembers()) {
				string sn = nt.Name;
				//print.it("<>" + new string(' ', stack.Count) + (nt is INamespaceSymbol ? "<c blue>" + nt.ToString() + "<>" : nt.ToString())/*, nt.ContainingAssembly?.Name*/);
				if (sn.NE() || sn[0] == '<') continue;
				if (nt is INamespaceSymbol ins) {
					stack.Add(sn);
					_EnumNamespace(ins);
					stack.RemoveAt(stack.Count - 1);
				} else if (!found) { //else continue to search in nested namespaces
					var its = nt as INamedTypeSymbol;
					if (0 != (need & 1)) {
						foreach (var v in a) {
							if (v.isEM) continue;
							if (sn != v.name) {
								if (!(v.isAttribute && sn.Length == v.name.Length + 9 && sn.Starts(v.name) && sn.Ends("Attribute"))) continue;
							}
							if (v.isGeneric && !its.IsGenericType) continue;
							if (_AddNamespace(nt)) goto gNext;
						}
					}
					if (0 != (need & 2)) {
						//p1.First();
						if (its.IsStatic && its.MightContainExtensionMethods) { //fast, but without IsStatic slow first time
							foreach (var m in nt.GetMembers().OfType<IMethodSymbol>()) { //fast; slightly slower than nt.MemberNames.Contains(errName) which gets member types etc too
								foreach (var v in a) {
									if (!v.isEM) continue;
									if (m.Name == v.name && m.IsExtensionMethod) {
										if (null == m.ReduceExtensionMethod(v.emReceiverType)) { /*Debug_.Print(emReceiverType);*/ continue; }
										if (_AddNamespace(m)) goto gNext;
									}
								}
							}
						}
						//p1.Next();
					}
					gNext:;
					
					bool _AddNamespace(ISymbol sym) {
						if (!sym.IsAccessibleWithin(compilation.Assembly)) return false;
						(usings ??= new()).Add(string.Join('.', stack));
						return found = true;
					}
				}
			}
		}
		return usings;
	}
	
	void _UsingsEtc(CiText x, in _Diagnostic v, SciCode doc, bool extMethod) {
		var d = v.d;
		var ec = (ErrorCode)d.Code;
		var mu = new _MissingUsingError(doc.aaaText, v.start, v.end, extMethod, _semo);
		List<_MissingUsingError> amu = null;
		_MissingUsingError.AddToList(ref amu, mu);
		List<string> usings = _GetMissingUsings(amu);
		if (usings != null) {
			var sstart = doc.aaaPos8(v.start).ToString();
			x.Append("\nAdd using ");
			for (int i = 0; i < usings.Count; i++) {
				var u = usings[i];
				if (i > 0) x.Append(" or ");
				x.Hyperlink("^u" + sstart + u, u);
			}
			if (!extMethod) {
				x.Append("\nOr prefix ");
				for (int i = 0; i < usings.Count; i++) {
					var u = usings[i];
					if (i > 0) x.Append(" or ");
					x.Hyperlink("^p" + sstart + u, u);
				}
			}
		} else {
			x.Hyperlink("^r", "\nAdd assembly reference or class file...");
			x.Hyperlink("^n", "\nAdd NuGet package...");
			if (!(mu.isEM | mu.isGeneric | mu.isAttribute || ec == ErrorCode.ERR_DottedTypeNameNotFoundInNS || _semo.GetEnclosingSymbol(v.start) is null or INamespaceSymbol))
				x.Hyperlink("^A" + mu.name, "\nFind Windows API...");
		}
	}
	
	void _LinkClicked(string s) {
		CodeInfo.HideTextPopup();
		char action = s[1];
		if (action is 'u' or 'p') { //add 'using', prefix namespace
			int pos8 = s.ToInt(2, out int i);
			s = s[i..];
			var doc = Panels.Editor.ActiveDoc;
			EraseIndicatorsInLine(doc, pos8);
			if (action == 'p') {
				doc.aaaInsertText(false, pos8, s + ".", addUndoPointAfter: true);
			} else {
				InsertCode.UsingDirective(s, true);
			}
		} else if (action == 'A') { //Windows API
			new DWinapi(s[2..]).Show();
		} else if (action == 'r') { //Add reference
			Menus.File.Properties();
		} else if (action == 'n') { //Add NuGet package
			DNuget.ShowSingle();
		} else if (action == 'i') { //implement interface or abstract class
			GenerateCode.ImplementInterfaceOrAbstractClass(s.ToInt(3));
		} else if (action == '<') { //output tag
			int i = s.IndexOf(' ');
			Panels.Output.Scintilla.AaTags.OnLinkClick(s[2..i], s[++i..]);
		} else if (action == 'w') { //copy warning
			if (s[2] == 'i') s = s[4..]; else if (s.Split(' ', 3) is var a) s = $"#pragma warning disable {a[1]} //{a[2]}\r\n#pragma warning restore {a[1]} //{a[2]}\r\n";
			clipboard.text = s;
		}
	}
	
	static bool _IsAttributeNameWithoutSuffix(string name, int pos, SemanticModel semo) {
		if (name.Ends("Attribute")) return false;
		return semo.SyntaxTree.IsAttributeNameContext(pos, default);
	}
	
	static ITypeSymbol _GetExtensionMethodReceiverType(SemanticModel semo, int startOfMethodName) {
		ITypeSymbol t = null;
		if (semo.SyntaxTree.GetRoot().FindToken(startOfMethodName).Parent.Parent is MemberAccessExpressionSyntax ma)
			t = semo.GetTypeInfo(ma.Expression).Type;
		Debug_.PrintIf(t == null, "failed to get extension method receiver type");
		return t;
	}
	
	bool _PastedWithUsings(CodeInfo.Context cd, ImmutableArray<Diagnostic> aDiag, ref int start16, ref int end16) {
		int firstStart = int.MaxValue, lastEnd = 0;
		foreach (var d in aDiag) {
			if ((ErrorCode)d.Code != ErrorCode.ERR_UsingAfterElements) continue;
			firstStart = Math.Min(firstStart, d.Location.SourceSpan.Start);
			lastEnd = Math.Max(lastEnd, d.Location.SourceSpan.End);
		}
		if (firstStart < lastEnd) {
			if (cd.syntaxRoot.FindTokenOnLeftOfPosition(lastEnd) is { RawKind: (int)SyntaxKind.SemicolonToken } t1) lastEnd = t1.FullSpan.End;
			int i = InsertCode.FindUsingsInsertPos(cd);
			if (i >= 0 && i <= firstStart) {
				var sUsings = cd.code[firstStart..lastEnd];
				if (!sUsings.Ends('\n')) sUsings += "\r\n";
				
				//don't add duplicate usings
				var text2 = cd.code.Remove(firstStart..lastEnd).Insert(i, sUsings);
				var document2 = cd.document.WithText(SourceText.From(text2));
				var semo = document2.GetSemanticModelAsync().Result_();
				if (semo.GetDiagnostics(new(i, sUsings.Length)) is { IsDefaultOrEmpty: false } ad2) {
					StringBuilder b = null; int appended = i;
					foreach (var d in ad2) {
						if ((ErrorCode)d.Code is ErrorCode.WRN_DuplicateUsing or ErrorCode.HDN_DuplicateWithGlobalUsing) {
							b ??= new();
							int start = d.Location.SourceSpan.Start, end = d.Location.SourceSpan.End;
							if (semo.SyntaxTree.GetRoot().FindToken(start).Parent.GetAncestorOrThis<UsingDirectiveSyntax>() is { } nUsing) {
								start = nUsing.SpanStart;
								end = nUsing.FullSpan.End;
								b.Append(text2, appended, start - appended);
								appended = end;
							}
						}
					}
					if (b != null) {
						b.Append(text2, appended, i + sUsings.Length - appended);
						sUsings = b.ToString();
					}
				}
				
				var doc = cd.sci;
				using var undo = doc.ENewUndoAction();
				doc.aaaDeleteRange(true, firstStart, lastEnd);
				doc.aaaInsertText(true, i, sUsings);
				
				start16 = firstStart + sUsings.Length;
				end16 += firstStart - lastEnd+ sUsings.Length;
				
				return true;
			}
		}
		return false;
	}
}
