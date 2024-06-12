extern alias CAW;

using System.Windows.Documents;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.DocumentationComments;
using Microsoft.CodeAnalysis.SignatureHelp;

class CiText {
	readonly Stack<TextElement> _stack = new();
	TextElement _container = new Section();
	TextElement _last;
	
	public Section Result => (Section)_container;
	
	/// <summary>
	/// Starts Block or Span. Or adds empty.
	/// </summary>
	/// <param name="e"></param>
	/// <param name="start">Start and don't end now. If false, adds and ends.</param>
	void _Start(TextElement e, bool start) {
		switch (e) {
		case Block b: ((Section)_container).Blocks.Add(b); break;
		case Span s: Append(s); break;
		default: throw new NotSupportedException();
		}
		_last = e;
		if (start) {
			_stack.Push(_container);
			_container = e;
		}
	}
	
	/// <summary>
	/// Ends Block or Span.
	/// </summary>
	void _End() { _container = _stack.Pop(); }
	
	public Run Append(string text) {
		var r = new Run(text);
		Append(r);
		return r;
	}
	
	public void Append(Inline i) {
		_Inlines().Add(i);
		_last = i;
	}
	
	public void Append(UIElement e) {
		_Inlines().Add(e);
	}
	
	InlineCollection _Inlines() => _container switch { Paragraph p => p.Inlines, Span s => s.Inlines, _ => throw new InvalidOperationException() };
	
	public Run Run(string style, string text, string append = null) {
		var r = Append(text);
		Style(style);
		if (append != null) Append(append);
		return r;
	}
	
	public void Style(string style) {
		//_last.Style = (Style)Application.Current.Resources[style]; //finds slower
		_last.Style = (Style)Application.Current.FindResource(style);
	}
	
	public Paragraph StartParagraph(string style = null) {
		var p = new Paragraph();
		_Start(p, true);
		if (style != null) Style(style);
		return p;
	}
	
	public void EndParagraph() => _End();
	
	/// <summary>
	/// <c>StartParagraph("div");</c>
	/// </summary>
	public Paragraph StartDiv() => StartParagraph("div");
	
	public void EndDiv() => _End();
	
	public (Paragraph, Run) Header(string text) {
		var p = StartParagraph("header");
		var r = Append(text);
		EndParagraph();
		return (p, r);
	}
	
	public Separator Separator() {
		var r = new Separator { Margin = new(4) };
		var v = new BlockUIContainer(r);
		_Start(v, false);
		return r;
	}
	
	public void LineBreak(string append = null, bool notIfFirstInParagraph = false) {
		if (!(notIfFirstInParagraph && _last is Paragraph)) Append(new LineBreak());
		if (append != null) Append(append);
	}
	
	public void StartOverload(bool selected, int index) {
		StartParagraph(selected ? "overloadSelected" : "overload");
		if (!selected) { StartHyperlink($"^{index}"); Style("divLink"); }
		Run(selected ? "dotSelected" : "dot", "● ");
	}
	
	public void EndOverload(bool selected) {
		if (!selected) EndHyperlink();
		EndParagraph();
	}
	
	/// <summary>
	/// Appends codeBlock paragraph with text.
	/// </summary>
	/// <param name="code">Non-escaped text.</param>
	public void CodeBlock(string code) {
		StartParagraph("codeBlock");
		Append(code);
		EndParagraph();
	}
	
	/// <summary>
	/// Appends Span with a defined style (like HTML class).
	/// </summary>
	/// <param name="style">A style defined in app resources, like "keyword".</param>
	public Span StartSpan(string style = null) {
		var r = new Span();
		_Start(r, true);
		if (style != null) Style(style);
		return r;
	}
	
	public void EndSpan() => _End();
	
	public void StartBold() => _Start(new Bold(), true);
	
	public void EndBold() => _End();
	
	public void Bold(string text) => _Start(new Bold(new Run(text)), false);
	
	public void StartItalic() => _Start(new Italic(), true);
	
	public void EndItalic() => _End();
	
	public void Italic(string text) => _Start(new Italic(new Run(text)), false);
	
	public Hyperlink StartHyperlink(string uri) {
		var h = new Hyperlink { NavigateUri = new(uri, UriKind.RelativeOrAbsolute) };
		h.RequestNavigate += (o, e) => FlowDocumentControl.OnLinkClicked_(o as Hyperlink, e);
		_Start(h, true);
		return h;
	}
	
	public void EndHyperlink() => _End();
	
	public Hyperlink Hyperlink(string uri, string text, string append = null) {
		var h = StartHyperlink(uri);
		if (WpfUtil_.IsHighContrastDark) h.Foreground = Brushes.RoyalBlue;
		Append(text);
		EndHyperlink();
		if (append != null) Append(append);
		return h;
	}
	
	public void Image(CiItemKind i) => Image(CiComplItem.ImageResource(i));
	
	public void Image(CiItemAccess i) => Image(CiComplItem.AccessImageResource(i));
	
	/// <summary>
	/// Adds xaml or png etc image from app resources.
	/// </summary>
	/// <param name="source">Image resource, like "resources/a.xaml" or png etc.</param>
	public void Image(string source) {
		if (source.Ends(".xaml")) {
			var c = new InlineUIContainer(ResourceUtil.GetWpfImageElement(source)) { BaselineAlignment = BaselineAlignment.Center };
			Append(c);
		} else { //not used, not tested
			var c = new Image { Source = ResourceUtil.GetWpfImage(source), Stretch = Stretch.None };
			Append(c);
		}
	}
	
	public void AppendTaggedParts(IEnumerable<TaggedText> tags, bool? isParameters = null) {
		if (tags == null) return;
		//need IReadOnlyList to easier replace something. Usually it is ImmutableArray or List.
		Debug_.PrintIf(tags is not IReadOnlyList<TaggedText>, "not IReadOnlyList");
		var a = tags as IReadOnlyList<TaggedText> ?? tags.ToArray();
		//print.it(a.Count);
		bool inParameters = isParameters == true;
		for (int i = 0; i < a.Count; i++) {
			var v = a[i];
			//print.it($"{v.Tag}, '{v.Text}', {v.Style}");
			//if(lessNewlines>1) print.it($"{v.Tag}, '{v.Text}', {v.Style}");
			//print.it($"{v.Tag}, '{v.Text}', {v.Style}, navHint='{v.NavigationHint}', navTarget='{v.NavigationTarget}'");
			string s = v.Text, c = null;
			switch (v.Tag) {
			case TextTags.Struct:
				c = "type";
				
				//rejected: replace parameter type ReadOnlySpan<char> with Strinĝ, and in global.cs add: global using Strinĝ = System.ReadOnlySpan<char>;
				//	Better in XML doc tell it's a string.
				//if (inParameters) {
				//	if (s == "ReadOnlySpan" && i < a.Count - 3 && a[i + 2].Text == "char" && a[i + 1].Text == "<" && a[i + 3].Text == ">") {
				//		s = "Strinĝ";
				//		i += 3;
				//	}
				//}
				break;
			case TextTags.Class or TextTags.Enum or TextTags.Interface or TextTags.Delegate or TextTags.TypeParameter or TextTags.Record or TextTags.RecordStruct:
				c = "type";
				break;
			case TextTags.Keyword:
				c = "keyword";
				break;
			case TextTags.StringLiteral:
				c = "string";
				break;
			case TextTags.NumericLiteral:
				c = "number";
				break;
			case TextTags.Namespace:
				c = "namespace";
				break;
			case TextTags.LineBreak:
				LineBreak();
				continue;
			case TextTags.Punctuation:
				if (isParameters == null && i > 0 && s.Length == 1) { //auto-detect
					char ch = s[0];
					if (!inParameters) {
						if (ch == '(') inParameters = a[i - 1].Tag is TextTags.Method or TextTags.ExtensionMethod;
						if (ch == '[') inParameters = a[i - 1].Tag is TextTags.Keyword && a[i - 1].Text == "this";
						//note: does not detect ctor.
						//	Then a[i - 1] is type. But for cast operators it is type too; they must not be detected.
						//	Probably it's better if ctor and cast are displayed with same types.
					} else if (ch == ')') inParameters = false;
				}
				break;
			case TextTags.Method: //eg operator <
			case TextTags.Operator:
				break;
			case TextTags.Text:
				if (v.Style == 0) _ProcessText();
				break;
			case TextTags.CodeBlockStart or TextTags.CodeBlockEnd: continue;
#if DEBUG
			case TextTags.Space:
			case TextTags.Constant:
			case TextTags.EnumMember:
			case TextTags.Event:
			case TextTags.ExtensionMethod:
			case TextTags.Field:
			case TextTags.Local:
			case TextTags.Parameter:
			case TextTags.Property:
			case TextTags.RangeVariable:
			case TextTags.Alias:
			case TextTags.Label:
			case TextTags.ContainerStart:
			case TextTags.ContainerEnd:
			case TextTags.AnonymousTypeIndicator:
			case TextTags.ErrorType:
				break;
			default:
				Debug_.Print($"{v.Tag}, '{v.Text}', {v.Style}");
				break;
#endif
			}
			
			switch (v.Style) {
			case TaggedTextStyle.Strong: StartBold(); break;
			case TaggedTextStyle.Emphasis: StartItalic(); break;
			case TaggedTextStyle.Code: StartSpan("codeSpan"); break;
			}
			
			if (c != null) Run(c, s); else Append(s);
			
			switch (v.Style) {
			case TaggedTextStyle.Code: EndSpan(); break;
			case TaggedTextStyle.Emphasis: EndItalic(); break;
			case TaggedTextStyle.Strong: EndBold(); break;
			}
			
			void _ProcessText() {
				//replace [](xref:topic_id) with Hyperlink
				if (!s.Contains("](xref:")) return;
				if (s_xrefmap == null) {
					s_xrefmap = new();
					try {
						var s1 = filesystem.loadText(folders.ThisApp + "xrefmap.yml"); //generated by DocFX project
						foreach (var k in s1.RxFindAll(@"(?m)^- uid: (.+)\R  name: (.+)\R  href: (.+)$")) {
							s_xrefmap.Add(k[1].Value, (k[3].Value, k[2].Value));
						}
					}
					catch { }
				}
				int i = 0;
				foreach (var m in s.RxFindAll(@"\[(.*?)\]\(xref:(.+?)\)")) {
					if (!s_xrefmap.TryGetValue(m[2].Value, out var v)) continue;
					if (m.Start > i) Append(s[i..m.Start]);
					Hyperlink(HelpUtil.AuHelpUrl(v.url), m[1].Length > 0 ? m[1].Value : v.text);
					i = m.End;
				}
				if (i > 0) s = s[i..];
			}
		}
	}
	
	static Dictionary<string, (string url, string text)> s_xrefmap;
	
	public void AppendTaggedParts(ImmutableArray<SymbolDisplayPart> parts, bool? isParameters = null)
		=> AppendTaggedParts(parts.ToTaggedText(), isParameters);
	
	public static Section FromTaggedParts(IEnumerable<TaggedText> tags) {
		var x = new CiText();
		x.StartParagraph();
		x.AppendTaggedParts(tags, false);
		x.EndParagraph();
		return x.Result;
	}
	
	public static Section FromSymbols(IEnumerable<ISymbol> symbols, int iSelect, SemanticModel model, int position) {
		//perf.first();
		var x = new CiText();
		x.AppendSymbols(symbols, iSelect, model, position);
		return x.Result;
	}
	
	public void AppendSymbols(IEnumerable<ISymbol> symbols, int iSelect, SemanticModel model, int position) {
		//perf.first();
		ISymbol sym = null;
		int i = -1;
		foreach (var v in symbols) {
			if (++i == iSelect) sym = v;
			StartOverload(i == iSelect, i);
			var parts = v.ToDisplayParts(s_symbolFullFormat);
			AppendTaggedParts(parts);
			if (v.Kind == SymbolKind.Parameter) _AppendComment("parameter");
			EndOverload(i == iSelect);
		}
		//perf.next();
		
		if (sym != null) {
			//print.it($"<><c blue>{sym}<>");
			//print.it(sym.GetType().GetInterfaces());
			
			var parts = GetSymbolDescription(sym, model, position);
			//print.it(parts);
			if (parts.Any()) {
				StartParagraph();
				AppendTaggedParts(parts, false);
				EndParagraph();
			}
			
			//print.it(sym.GetDocumentationCommentXml());
			
			ISymbol enclosing = null;
			switch (sym.Kind) {
			case SymbolKind.NamedType:
			case SymbolKind.Event:
			case SymbolKind.Field:
			case SymbolKind.Method:
			case SymbolKind.Property:
				Separator();
				
				//HELP AND SOURCE LINKS
				StartDiv();
				AppendSymbolLinks(sym);
				EndDiv();
				
				//CONTAINER namespace/type and assembly
				StartDiv();
				Append("In ");
				var ctn = sym.ContainingType ?? sym.ContainingNamespace as INamespaceOrTypeSymbol;
				if (!(ctn is INamespaceSymbol ins && ins.IsGlobalNamespace)) {
					Append((ctn is ITypeSymbol its) ? its.TypeKind.ToString().Lower() : "namespace"); Append(" ");
					AppendTaggedParts(ctn.ToDisplayParts(s_qualifiedTypeFormat), false);
					Append(", ");
				}
				Append(sym.IsFromSource() ? "file " : "assembly "); Append(sym.ContainingAssembly.Name);
				EndDiv();
				
				//MODIFIERS
				bool isReadonly = false, isConst = false, isVolatile = false, isAsync = false, isEnumMember = false;
				var tKind = TypeKind.Unknown; //0
				INamedTypeSymbol baseType = null; string enumBaseType = null; ImmutableArray<INamedTypeSymbol> interfaces = default;
				switch (sym) {
				case IFieldSymbol ifs:
					if (isEnumMember = ifs.IsEnumMember()) {
						enumBaseType = ifs.ContainingType.EnumUnderlyingType.ToNameDisplayString();
					} else {
						isReadonly = ifs.IsReadOnly;
						isConst = ifs.IsConst;
						isVolatile = ifs.IsVolatile;
					}
					break;
				case INamedTypeSymbol ints:
					isReadonly = ints.IsReadOnly;
					tKind = ints.TypeKind;
					switch (tKind) {
					case TypeKind.Class:
						var bt = ints.BaseType;
						if (bt != null && bt.BaseType != null) baseType = bt; //not object
						goto case TypeKind.Interface;
					case TypeKind.Interface:
					case TypeKind.Struct:
						interfaces = ints.AllInterfaces;
						break;
					case TypeKind.Enum:
						enumBaseType = ints.EnumUnderlyingType.ToNameDisplayString();
						break;
					}
					break;
				case IMethodSymbol ims:
					isAsync = ims.IsAsync;
					break;
				}
				if (!isEnumMember) {
					StartDiv();
					string s1 = sym.DeclaredAccessibility switch {
						Microsoft.CodeAnalysis.Accessibility.Public => "public",
						Microsoft.CodeAnalysis.Accessibility.Private => "private",
						Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
						Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
						Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private protected",
						Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected internal",
						_ => null
					};
					if (s1 != null) _AppendKeyword(s1);
					
					if (isConst) _AppendSpaceKeyword("const"); else if (sym.IsStatic) _AppendSpaceKeyword("static");
					if (sym.IsAbstract && tKind != TypeKind.Interface) _AppendSpaceKeyword("abstract");
					if (sym.IsSealed && (tKind == TypeKind.Class || tKind == TypeKind.Unknown)) _AppendSpaceKeyword("sealed");
					if (sym.IsVirtual) _AppendSpaceKeyword("virtual");
					if (sym.IsOverride) _AppendSpaceKeyword("override");
					if (sym.IsExtern) _AppendSpaceKeyword("extern");
					if (isReadonly) _AppendSpaceKeyword("readonly");
					if (isAsync) _AppendSpaceKeyword("async");
					if (isVolatile) _AppendSpaceKeyword("volatile");
					EndDiv();
					void _AppendSpaceKeyword(string name) {
						if (_last is not Paragraph) Append(" ");
						Run("keyword", name);
					}
				}
				
				//BASE TYPES AND IMPLEMENTED INTERFACES
				if (baseType != null || enumBaseType != null || !interfaces.IsDefaultOrEmpty) {
					StartDiv();
					Append(": ");
					if (enumBaseType != null) {
						_AppendTypeName(enumBaseType, "keyword");
					} else if (baseType != null) {
						_AppendType(baseType);
						for (baseType = baseType.BaseType; baseType.BaseType != null; baseType = baseType.BaseType) {
							Append(" : ");
							_AppendType(baseType);
						}
					}
					if (!interfaces.IsDefaultOrEmpty) {
						bool comma = baseType != null || enumBaseType != null;
						foreach (var v in interfaces) {
							_AppendComma(ref comma);
							bool hilite = v.Name == "IDisposable";
							if (hilite) StartSpan("hilite");
							_AppendType(v);
							if (hilite) EndSpan();
						}
					}
					EndDiv();
				}
				
				//ATTRIBUTES
				_AppendAttributes(sym.GetAttributes());
				switch (sym) {
				case IMethodSymbol ims:
					_AppendAttributes(ims.GetReturnTypeAttributes(), "return");
					foreach (var v in ims.Parameters) _AppendAttributes(v.GetAttributes(), v.Name, isParameter: true);
					break;
				case IPropertySymbol ips:
					var ipgs = ips.GetMethod;
					if (ipgs != null) {
						_AppendAttributes(ipgs.GetAttributes(), "get");
						_AppendAttributes(ipgs.GetReturnTypeAttributes(), "return");
					}
					var ipss = ips.SetMethod;
					if (ipss != null) _AppendAttributes(ipss.GetAttributes(), "set");
					var ipfs = ips.GetBackingFieldIfAny();
					if (ipfs != null) _AppendAttributes(ipfs.GetAttributes(), "field");
					break;
				}
				
				//TYPE MEMBERS NOT SHOWN IN THE COMPLETION LIST: constructors, finalizers, operators
				if ((tKind == TypeKind.Class || tKind == TypeKind.Struct /*|| tKind == TypeKind.Interface*/) && sym is INamespaceOrTypeSymbol t) {
					List<ISymbol> a = null;
					foreach (var v in t.GetMembers()) {
						switch (v) {
						case IMethodSymbol m:
							switch (m.MethodKind) {
							case MethodKind.Ordinary:
							case MethodKind.PropertyGet:
							case MethodKind.PropertySet:
							case MethodKind.EventAdd:
							case MethodKind.EventRemove:
							case MethodKind.ExplicitInterfaceImplementation:
							case MethodKind.Destructor:
								continue;
								//case MethodKind.Destructor: goto g1; //don't check is accessible etc
							}
							if (m.IsImplicitlyDeclared) continue; //eg default ctor of struct
							//print.it(m.MethodKind, m);
							break;
						//case IPropertySymbol p when p.IsIndexer: //now indexers are in completion list
						//	break;
						default: continue;
						}
						if (!_IsAccessible(v) || v.IsObsolete()) continue;
						//g1:
						(a ??= new List<ISymbol>()).Add(v);
					}
					if (a != null) {
						Separator();
						//string header = tKind switch { TypeKind.Class => "Constructors, finalizers, operators, indexers", TypeKind.Struct => "Constructors, operators, indexers", _ => "Indexers" };
						//string header = tKind == TypeKind.Interface ? "Indexers" : "Constructors, operators, indexers";
						string header = "Constructors, operators";
						Header(header);
						a.Sort((v1, v2) => {
							//var diff = v1.Kind - v2.Kind; //indexer is property, others are methods
							//if (diff != 0) return diff;
							if (v1 is IMethodSymbol m1 && v2 is IMethodSymbol m2) return _Sort(m1) - _Sort(m2);
							static int _Sort(IMethodSymbol sy) => sy.MethodKind switch { MethodKind.StaticConstructor => 0, MethodKind.Constructor => 1, /*MethodKind.Destructor => 2,*/ MethodKind.Conversion => 3, MethodKind.UserDefinedOperator => 4, _ => 0 };
							return 0;
						});
						foreach (var v in a) {
							StartDiv();
							Run("dot", "• ");
							if (v is IMethodSymbol m && m.MethodKind == MethodKind.StaticConstructor) _AppendKeyword("static ");
							AppendTaggedParts(v.ToDisplayParts(s_symbolFullFormat));
							EndDiv();
						}
					}
					//never mind: also should add those of base types.
				}
				break;
			}
			//perf.nw();
			
			bool _IsAccessible(ISymbol symbol) {
				enclosing ??= model.GetEnclosingNamedTypeOrAssembly(position, default);
				return enclosing != null && symbol.IsAccessibleWithin(enclosing);
			}
			void _AppendComma(ref bool isComma) { if (!isComma) isComma = true; else Append(", "); }
			void _AppendKeyword(string name) { Run("keyword", name); }
			void _AppendTypeName(string name, string cssClass = "type") => Run(cssClass, name);
			void _AppendType(INamedTypeSymbol t) {
				if (t.IsGenericType) AppendTaggedParts(t.ToDisplayParts(s_unqualifiedTypeFormat), false);
				else _AppendTypeName(t.Name);
			}
			void _AppendAttributes(ImmutableArray<AttributeData> a, string target = null, bool isParameter = false) {
				if (a.IsDefaultOrEmpty) return;
				bool attrComma = false, paramAdded = false;
				foreach (var att in a) {
					var ac = att.AttributeClass; if (ac == null) continue;
					//if(!_IsAccessible(ac)) continue; //no, would not show attributes if code does not have 'using' for the attribute
					string aname = ac.Name.RemoveSuffix("Attribute");
					if (aname == "CompilerGenerated" || aname == "IteratorStateMachine") continue;
					//att.ToString(); //similar, but too much noise
					if (isParameter) {
						if (!paramAdded) {
							paramAdded = true;
							StartDiv();
							Append(target + ": [");
						}
						_AppendComma(ref attrComma);
					} else {
						StartDiv();
						Append("[");
						if (target != null) Run("keyword", target, ": ");
					}
					bool hilite = aname == "Obsolete";
					if (hilite) StartSpan("hilite");
					_AppendTypeName(aname);
					if (hilite) EndSpan();
					var ca = att.CommonConstructorArguments;
					var na = att.CommonNamedArguments;
					if (ca.Length + na.Length > 0) {
						Append("(");
						bool paramComma = false;
						foreach (var v in ca) {
							_AppendComma(ref paramComma);
							Append(v.ToCSharpString()); //never mind: enum with namespace
						}
						foreach (var v in na) {
							_AppendComma(ref paramComma);
							Append(v.Key + " = ");
							Append(v.Value.ToCSharpString());
						}
						Append(")");
					}
					if (!isParameter) { Append("]"); EndDiv(); }
				}
				if (isParameter && paramAdded) { Append("]"); EndDiv(); }
			}
		}
		void _AppendComment(string s) {
			Run("comment", "   //" + s);
		}
	}
	
	public static ImmutableArray<TaggedText> GetSymbolDescription(ISymbol sym, SemanticModel model, int position) {
		return sym.GetDocumentationParts(model, position, _Formatter, default);
	}
	
	//public static IEnumerable<TaggedText> GetTaggedTextForXml(string xml, SemanticModel model, int position)
	//{
	//	if(xml == null) return Enumerable.Empty<TaggedText>();
	//	return _Formatter.Format(xml, model, position, ISymbolExtensions2.CrefFormat); //error in new Roslyn
	//}
	
	static IDocumentationCommentFormattingService _Formatter => s_formatter ??= CodeInfo.CurrentWorkspace.Services.GetLanguageServices("C#").GetService<IDocumentationCommentFormattingService>();
	static IDocumentationCommentFormattingService s_formatter;
	
	const SymbolDisplayMiscellaneousOptions s_miscDisplayOptions =
		SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral
		| SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
		//| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier //?
		//| SymbolDisplayMiscellaneousOptions.IncludeNotNullableReferenceTypeModifier //!
		| SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix
		| SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName
		| SymbolDisplayMiscellaneousOptions.UseSpecialTypes;
	const SymbolDisplayParameterOptions s_parameterDisplayOptions =
		SymbolDisplayParameterOptions.IncludeType
		| SymbolDisplayParameterOptions.IncludeName
		| SymbolDisplayParameterOptions.IncludeParamsRefOut
		| SymbolDisplayParameterOptions.IncludeOptionalBrackets
		| SymbolDisplayParameterOptions.IncludeDefaultValue
		| SymbolDisplayParameterOptions.IncludeExtensionThis;
	
	internal static readonly SymbolDisplayFormat s_symbolFullFormat = new(
		SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
		SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeRef | SymbolDisplayMemberOptions.IncludeExplicitInterface,
		SymbolDisplayDelegateStyle.NameAndSignature,
		SymbolDisplayExtensionMethodStyle.Default,
		s_parameterDisplayOptions,
		SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
		SymbolDisplayLocalOptions.IncludeType | SymbolDisplayLocalOptions.IncludeRef | SymbolDisplayLocalOptions.IncludeConstantValue,
		SymbolDisplayKindOptions.IncludeMemberKeyword | SymbolDisplayKindOptions.IncludeNamespaceKeyword | SymbolDisplayKindOptions.IncludeTypeKeyword,
		s_miscDisplayOptions
		);
	static SymbolDisplayFormat s_qualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
		miscellaneousOptions: s_miscDisplayOptions
		);
	static SymbolDisplayFormat s_unqualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Omitted,
		SymbolDisplayTypeQualificationStyle.NameOnly,
		SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
		miscellaneousOptions: s_miscDisplayOptions
		);
	static SymbolDisplayFormat s_parameterFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Omitted,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
		parameterOptions: s_parameterDisplayOptions,
		miscellaneousOptions: s_miscDisplayOptions
		);
	static SymbolDisplayFormat s_symbolWithoutParametersFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Omitted,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
		SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeRef | SymbolDisplayMemberOptions.IncludeExplicitInterface,
		miscellaneousOptions: s_miscDisplayOptions
		);
	static SymbolDisplayFormat s_nameWithoutTypeParametersFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Omitted,
		SymbolDisplayTypeQualificationStyle.NameOnly,
		miscellaneousOptions: s_miscDisplayOptions
		);
	
	public void AppendSymbolWithoutParameters(ISymbol sym) {
		AppendTaggedParts(sym.ToDisplayParts(sym is INamedTypeSymbol ? s_nameWithoutTypeParametersFormat : s_symbolWithoutParametersFormat), false);
	}
	
	public void AppendParameters(ISymbol sym, int iSel = -1, SignatureHelpItem sh = null) {
		ImmutableArray<IParameterSymbol> ap;
		switch (sym) {
		case IMethodSymbol sy:
			ap = sy.Parameters;
			break;
		case IPropertySymbol sy: //indexer
			ap = sy.Parameters;
			break;
		case INamedTypeSymbol sy:
			if (sy.IsTupleType && !sy.IsDefinition) {
				var te = sy.TupleElements;
				for (int i = 0; i < te.Length; i++) {
					_Param(te[i], i, 1);
				}
			} else if (sy.IsGenericType) {
				var tp = sy.TypeParameters;
				for (int i = 0; i < tp.Length; i++) {
					_Param(sy.TypeParameters[i], i, 2);
				}
			} else {
				Debug_.Print(sy);
			}
			return;
		default:
			Debug_.Print(sym);
			return;
		}
		
		for (int i = 0; i < ap.Length; i++) _Param(ap[i], i, 0);
		if (sh != null) { //eg properties in [Attribute(params, properties)]. Can be used for params too, but formats not as I like.
			var ap2 = sh.Parameters;
			for (int i = ap.Length; i < ap2.Length; i++) {
				if (i > 0) Append(", ");
				if (i == iSel) StartBold();
				AppendTaggedParts(ap2[i].DisplayParts);
				if (i == iSel) EndBold();
			}
		}
		
		void _Param(ISymbol p, int i, byte kind) { //kind: 0 param, 1 field, 2 typeparam
			if (i > 0) Append(", ");
			if (i == iSel) StartBold();
			AppendTaggedParts(p.ToDisplayParts(kind == 1 ? s_symbolWithoutParametersFormat : s_parameterFormat), isParameters: kind == 0);
			//info: s_symbolWithoutParametersFormat here maybe is not the best, but it worked well with all tested tuple parameter types.
			if (i == iSel) EndBold();
		}
	}
	
	//public void AppendParameters(SignatureHelpItem sh, int iSel = -1)
	//{
	//	var ap = sh.Parameters;
	//	for(int i = 0; i < ap.Length; i++) {
	//		if(i > 0) Append(", ");
	//		if(i == iSel) StartBold();
	//		AppendTaggedParts(ap[i].DisplayParts);
	//		if(i == iSel) EndBold();
	//	}
	//}
	
	public bool AppendSymbolLinks(ISymbol sym) {
		string helpUrl = CiUtil.GetSymbolHelpUrl(sym);
		string sourceUrl = CiGoTo.GetLinkData(sym);
		if (helpUrl == null && sourceUrl == null) return false;
		AppendSymbolLinks(helpUrl, sourceUrl);
		return true;
	}
	
	public void AppendSymbolLinks(string helpUrl, string sourceUrl) {
		Append(helpUrl != null && sourceUrl != null ? "Links: " : "Link: ");
		if (helpUrl != null) Hyperlink(helpUrl, "more info");
		if (sourceUrl != null) {
			if (helpUrl != null) Append(" , ");
			Hyperlink(sourceUrl, "source code");
		}
	}
	
	public static Section FromKeyword(string name) {
		//var url = "https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/";
		var url = App.Settings.internetSearchUrl + "C%23+keyword";
		var x = new CiText();
		x.StartParagraph();
		x.Append("Keyword ");
		x.Run("keyword", name);
		x.EndParagraph();
		x.Separator();
		x.StartParagraph();
		x.Append("Links: ");
		x.Hyperlink($"{url} {name}", "more info", " , ");
		x.Hyperlink($"{url}s", "C# keywords", ".");
		x.EndParagraph();
		return x.Result;
	}
	
	public static Section FromLabel(string name) {
		var x = new CiText();
		x.StartParagraph();
		x.Append("Label "); x.Bold(name);
		x.EndParagraph();
		return x.Result;
	}
	
	public static FlowDocumentControl CreateControl(int colorRGB = 0xfffff0) {
		var ff = App.Wmain.FontFamily;
		double fs = App.Wmain.FontSize;
		if (ff.Source == "Segoe UI") { ff = new("Calibri"); fs += 2; }
		var d = new FlowDocument {
			FontFamily = ff,
			FontSize = fs,
			Background = ColorInt.WpfBrush_(colorRGB),
			PagePadding = default,
			TextAlignment = TextAlignment.Left,
		};
		TextOptions.SetTextFormattingMode(d, TextFormattingMode.Display);
		var c = new FlowDocumentControl {
			Document = d,
			FocusVisualStyle = null,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			IsInactiveSelectionHighlightEnabled = true,
		};
		return c;
	}
}

class FlowDocumentControl : FlowDocumentScrollViewer {
	public event Action<FlowDocumentControl, string> LinkClicked;
	
	internal static void OnLinkClicked_(Hyperlink h, RequestNavigateEventArgs e) {
		var s = e.Uri.OriginalString;
		//print.it(s);
		DependencyObject d = h;
		while (null != (d = LogicalTreeHelper.GetParent(d))) if (d is FlowDocumentControl c) {
				c.LinkClicked?.Invoke(c, s);
				break;
			}
	}
	
	/// <summary>
	/// Clears text (removes all blocks) and applies a workaround for a WPF bug.
	/// </summary>
	public void Clear() {
		Document.Blocks.Clear();
		_ = Selection?.IsEmpty; //workaround for WPF bug: if some text was selected, would select all new text
	}
	
	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
		if (!Selection.IsEmpty) this.Hwnd().ActivateL(); //user may want to copy text with Ctrl+C
		base.OnPreviewMouseLeftButtonUp(e);
	}
}
