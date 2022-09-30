//#define DUPLICATE_TEXT
//#define MISSING_PARAM
//#define PARAM_WITH_JUST_SEE
//#define RETURNS_IN_SUMMARY
//#define LIST_NOT_ENDED
//#define STRING_NOT_IN_C
//#define PARAM_NOT_IN_I
//#define SYMBOL_NOT_BOLD
//#define SPELL_CHECK

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using System.Collections.Immutable;
using System.Xml.Linq;
using Markdig;
using System.Xml;

class _Analyze {
	//AuDocs _main;
	int _currentFileIndex, _currentFileIndexPrev;
	CSharpSemanticModel _semo;
	ISymbol _currentSym;
#if SPELL_CHECK
	WeCantSpell.Hunspell.WordList _spellCheck = WeCantSpell.Hunspell.WordList.CreateFromFiles(@"C:\Program Files\LibreOffice\share\extensions\dict-en\en_US.dic");
#endif
	regexp _rxTriple = new(@"(?m)^\h*///\h?");
	regexp _rxListNotEnded;
	
	//public _Analyze(AuDocs main) {
	//	_main = main;
	//}
	
	public void AddUsings(ref string s) {
		//s=s+"\nusing System.Drawing;\n";
		//s=s+"\nusing System.Windows.Forms;\n";
		//s=s+"\nusing System.Windows;\n";
		//s=s+"\nusing System.Windows.Controls;\n";
	}
	
	public void StartingFile(CSharpSemanticModel semo, int currentFileIndex) {
		_semo = semo;
		_currentFileIndex = currentFileIndex;
	}
	
	/// <summary>
	/// Prints or collects data for an XML doc to find what can be improved in it.
	/// </summary>
	/// <param name="sdoc">Raw XML doc (with ///).</param>
	/// <param name="sym"></param>
	public void AnalyzeDoc(string sdoc, ISymbol sym) {
		return;
		if (_currentFileIndex is not (>= 0 and < 200)) return;
		
		if (_currentFileIndex > _currentFileIndexPrev) {
			_currentFileIndexPrev = _currentFileIndex;
			print.it($"<><z yellowgreen>{_currentFileIndex}<>");
		}
		
		_currentSym = sym;
		sdoc = "<x>" + _rxTriple.Replace(sdoc) + "</x>";
		var xdoc = XElement.Parse(sdoc, LoadOptions.PreserveWhitespace);
		
		#region child elements
		foreach (var x in xdoc.Elements()) {
			var s = x.ToString();
#if DUPLICATE_TEXT
			if (s.Length > 200) {
				if (!_dDupText.TryAdd(s, _currentSym)) {
					var o = _dDupText[s];
					if (o is List<ISymbol> a) a.Add(_currentSym);
					else if (o is ISymbol sym) _dDupText[s] = new List<ISymbol> { sym, _currentSym };
				}
			}
#endif
			
#if PARAM_WITH_JUST_SEE
			if(x.Name.LocalName=="param" && s.Contains("<see cref") && s.Ends(false, ">.</param>", "></param>") > 0 && x.Elements().Count() == 1) {
				//if (x.FirstNode is XElement || (x.FirstNode is XText xt && xt.Value.Length < 10)) {
				if (x.FirstNode is XElement || (x.FirstNode is XText xt && xt.Value == "See ")) {
					_Print(s);
				}
			}
#endif
			
#if RETURNS_IN_SUMMARY
			if (x.Name.LocalName == "summary" && s.Contains("\nReturns") && !s.Starts("<summary>\r\nReturns")) {
				_Print(s);
			}
			
			//this hotkey script moves /// Returns ... line from <summary> to <returns>.
			//hk["F8"] = o => {
			//	if (!clipboard.tryCopy(out var s)) return;
			//	if (s != null && 0 != s.RxReplace(@"///\h(?:Return[s]? )?(.+)", "/// <returns>$1</returns>", out s, 1)) {
			//		keys.send("Ctrl+X Down Home*2");
			//		clipboard.paste(s);
			//		keys.send("Up");
			//	}
			//};
#endif
			
#if LIST_NOT_ENDED
			_rxListNotEnded ??= new(@"(?m)^\h*-.+\R\t*(?![\- \r\n]|</|<br\b)");
			if (_rxListNotEnded.IsMatch(s)) {
				_Print(s);
			}
#endif
		}
		#endregion
		
		#region child nodes
		foreach (var n in xdoc.Nodes()) {
			if (n is XElement) continue;
			
			//all xdoc child nodes must be XElement
			if (n is XText t && n is not XCData) {
				var s = t.Value;
				if (!string.IsNullOrWhiteSpace(s)) _Print(s);
			} else {
				_Print(n);
			}
		}
		#endregion
		
#if STRING_NOT_IN_C
		//if text contains " or ', in most cases it is code and should be in <c>
		foreach (var s in _DescendantTexts()) {
			//if (s.Contains('\"')) {
			//	_Print(s);
			//}
			if (s.RxIsMatch(@"'.'")) {
				//if (s.RxIsMatch(@"'\\.'")) {
				//if (s.RxIsMatch(@"'\\x\w+'")) {
				_Print(s);
			}
		}
#endif
		
#if PARAM_NOT_IN_I //parameter references must be in <i>
		ImmutableArray<IParameterSymbol> ap = default;
		if (_currentSym is IMethodSymbol ims) {
			ap = ims.Parameters;
		} else if (_currentSym is IPropertySymbol ips && ips.IsIndexer) {
			ap = ips.Parameters;
		}
		if (!ap.IsDefaultOrEmpty) {
			//print.it(_currentSym);
			var at = _DescendantTexts();
			if (at.Any()) {
				bool sep=false;
				foreach (var p in ap) {
					var rx = new regexp($@"(?<![\.'])\b{p.Name}\b");
					foreach (var s in at) {
						if (rx.Replace(s, "</_><z yellow>$0<><_>", out var s2) > 0) {
							if(!sep) { sep=true; print.it("-------------------"); }
							_Print(s2);
						}
					}
				}
			}
		}
#endif
		
#if SYMBOL_NOT_BOLD //names of types, functions, enum members etc should be in <b> or <see>, <msdn>.
		var hs = _stackSymbols.Peek();
		foreach (var s in _DescendantTexts()) {
			bool found = false;
			_rxWord.Replace(s, m => {
				var v = m.Value;
				
				//found = v is not ("ANSI" or "PCRE" or "NOTE" or "IMPORTANT" or "MSDN" or "XAML" or "HTML" or "ASCII" or "JSON" or "HTTP");
				//if (found) return "</_><z yellow>" + v + "<><_>";
				
				bool skip = true; for (int j = 1; j < v.Length; j++) if (char.IsUpper(v[j])) { skip = false; break; }
				if (skip) {
					skip = false;
					//skip if start of sentence
					if ((m.Start == 0 || m.Subject.Eq(m.Start - 1, '\n') || m.Subject.Eq(m.Start - 2, ". ")) && !m.Subject.Eq(m.End, '<') && !m.Subject.Eq(m.End, '[')) {
						//skip = v is "More" or "System" or "Object" or "Enum" or "Single" or "Double" or "Missing" or "Type" or "Delegate" or "Attribute" or "String" or "Array" or "List" or "Exception" or "Action" or "File" or "Path" or "Icon" or "Image" or "Process" or "Thread" or "Font" or "Timeout" or "Timer" or "Rectangle" or "Group";
						skip = true;
					} else {
						//skip = v is "More" or "System" or "Object" or "Enum" or "Single" or "Double" or "Missing" or "Type" or "Delegate" or "Attribute" or "String" or "Array" or "List" or "Exception" or "Action" or "File" or "Path" or "Process" or "Thread" or "Timeout" or "Timer" or "Task" or "Debug" or "Au";
					}
				} else {
					skip = v is "GC" or "MD5";
				}
				if (skip) return v;
				
				if (hs.Contains(v)) {
					found = true;
					return "</_><z yellow>" + v + "<><_>";
				} else {
					
				}
				return v;
			}, out var s2);
			
			if (found) {
				_Print(s2);
			}
		}
#endif
		
#if SPELL_CHECK
		foreach (var s in _DescendantTexts()) {
			bool found = false;
			_rxWord.Replace(s, m => {
				var v = m.Value;
				if (!_spellCheck.Check(v)) {
					if (v is "eg" or "ie" || v.Starts(true, "substring", "unmanaged") > 0) return v;
					found = true;
					return "</_><z yellow>" + v + "<><_>";
				}
				return v;
			}, out var s2);
			
			if (found) {
				_Print(s2);
			}
		}
#endif
		
		string[] _DescendantTexts()
			=> xdoc.DescendantNodes().OfType<XText>().Where(t => t is not XCData && t.Parent.Name.LocalName is not ("c" or "i" or "b" or "google" or "msdn" or "sqlite" or "code"))
			.Select(t => t.Value.Trim("\r\n")).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
		
		void _Print(object s) {
			if (_currentSym != _prevPrintSym) {
				_prevPrintSym = _currentSym;
				print.it($"<>{AuDocs.VsGoto(_currentSym)}");
			}
			print.it($"<><_>{s}</_>");
		}
	}
	Dictionary<string, object> _dDupText = new();
	ISymbol _prevPrintSym;
	
#if SYMBOL_NOT_BOLD
	Stack<HashSet<string>> _stackSymbols = new();
	public void AnalyzeType(CSharpSyntaxNode sn) {
		if (sn != null) {
			int pos = sn switch { BaseTypeDeclarationSyntax btd => btd.CloseBraceToken.SpanStart, NamespaceDeclarationSyntax nd => nd.CloseBraceToken.SpanStart, FileScopedNamespaceDeclarationSyntax fsnd => fsnd.SemicolonToken.Span.End, _ => 0 };
			_stackSymbols.Push(_semo.LookupSymbols(pos).Select(o => o.Name).ToHashSet());
			//_stackSymbols.Push(_semo.LookupSymbols(pos).Where(o => o is not INamespaceSymbol).Select(o => o.Name).ToHashSet());
			//_stackSymbols.Push(_semo.LookupSymbols(pos).Where(o => o is not INamespaceOrTypeSymbol).Select(o => o.Name).ToHashSet());
		} else _stackSymbols.Pop();
	}
#else
	public void AnalyzeType(CSharpSyntaxNode sn) { }
#endif
	
	//regexp _rxWord = new(@"\b(?<![\.""])[A-Z]\w+\b(?!</|"")"); //Abc
	//regexp _rxWord = new(@"\b(?<![""])[A-Z]\w+\b(?!</|""|\.)"); //Abc
	//regexp _rxWord = new(@"\b(?<![\.""])[a-z][A-Za-z]+\d?\b(?!</|"")"); //aBc
	//regexp _rxWord = new(@"\b(?<![\.""])[A-Z]+_[A-Z_]+\d?\b(?!</|"")"); //AB_C
	//regexp _rxWord = new(@"\b(?<![\.""])[A-Z_]+_\b(?!</|"")"); //AB_
	//regexp _rxWord = new(@"\b(?<![\.""])[A-Z]{4,}\d?\b(?!</|"")"); //ABC
	//regexp _rxWord = new(@"\b(?<![\.""])[A-Z]+[a-z]*[A-Z]+[a-z]+[A-Za-z]*\d?\b(?!</|"")"); //AbCd
	//regexp _rxWord = new(@"\bflags? [A-Z]\w+\b(?!</|"")"); //flag Abc
	//regexp _rxWord = new(@"\b(?<![\.""])[A-Z]\w+X\b(?!</|"")"); //SymbolX
	//regexp _rxWord = new(@"\b(?<![\.""])[A-Z]\w+x\b(?!</|"")"); //SYMBOLx
	//regexp _rxWord = new(@"\b(?<![\.""])(?i)[A-Z]\w*(\.[A-Z]\w*)+\b(?!</|"")"); //Abc.Def
	//regexp _rxWord = new(@"\b(?<![\.""])[a-z]+[A-Z]+[A-Za-z]*\d?\b(?!</|"")"); //aBc
	//regexp _rxWord = new(@"\b(?<![""])(?i)[a-z]\w+<[a-z]\w+\b(?!</|"")"); //abc<def>
	//regexp _rxWord = new(@"\b(?<![""])(?i)[a-z]\w+\[(?!</|"")"); //abc[] //rejected
	//regexp _rxWord = new(@"\b(?<![\.""])[a-z]+[a-z_\d]*\b(?!</|"")"); //abc
	regexp _rxWord = new(@"\b[A-Za-z][a-z']*\b"); //word for spell check
	
	/// <summary>
	/// Prints data collected by _AnalyzeDoc.
	/// </summary>
	public void Finally() {
		foreach (var (s, o) in _dDupText) {
			if (o is not List<ISymbol> a) continue;
			print.it($"""
<><Z green>Duplicate text:<>
<Z wheat><_>{s}</_><>
{string.Join('\n', a.Select(o => AuDocs.VsGoto(o)))}
""");
		}
	}
	
	/// <summary>
	/// Prints or collects data for an XML doc to find what can be improved in it.
	/// xdoc, sxml - final doc XML (after expanding inheritdoc and modifying).
	/// </summary>
	public void AnalyzeDoc2(XElement xdoc, string sxml) {
		if (_currentFileIndex is not (>= 0 and < 200)) return;
		
		var sym = _currentSym;
		
		if (sxml.RxIsMatch(@"\[.*?\]\(.*?\)")) _Warning("Unprocessed []() link", sxml);
	}
	
	public void MissingParam(IParameterSymbol p, XElement x) {
#if MISSING_PARAM
		if (_currentFileIndex is not (>= 0 and < 200)) return;
		
		var sym = _currentSym;
		if(p.Type.IsEnumType()) return;
		//if(p.IsThis) return; //does not work. Also tested p.OriginalDefinition.IsThis and p.CustomModifiers.
		//if (x==null || x.IsEmpty) return;
		
		//bool hasOverloads = sym.ContainingType.GetMembers(sym.Name).Length > 1;
		//if (!hasOverloads) return;
		
		if (sym != _prevMethod) {
			_prevMethod = sym;
			print.it("<>" + AuDocs.VsGoto(sym));
		}
		print.it(p.Name);
	}
	ISymbol _prevMethod;
#else
	}
#endif
	
	void _Warning(string warning, object text) {
		print.warning($"<_>{warning}</_>\r\n\tMember: {AuDocs.VsGoto(_currentSym)} in {_semo.SyntaxTree.FilePath}\r\n\tText: <_>{text}</_>", 1);
	}
}
