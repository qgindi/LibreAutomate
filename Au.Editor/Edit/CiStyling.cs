//#define PRINT

extern alias CAW;

//Code colors. Also calls functions of folding, images, errors.

using Au.Controls;
using static Au.Controls.Sci;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using CAW::Microsoft.CodeAnalysis.Classification;

partial class CiStyling {
	public void DocHandleDestroyed(SciCode doc) {
		if (doc == _doc) {
			_doc = null; //GC. Not important, but helps when trying to detect memory leaks.
		}
	}
	
	/// <summary>
	/// Called after setting editor control text when a document opened (not just switched active document).
	/// </summary>
	public static void DocTextAdded() => CodeInfo._styling._DocTextAdded();
	void _DocTextAdded() {
		if (CodeInfo.IsReadyForStyling) {
			_timerOpened ??= new(_ => _DocChanged());
			_timerOpened.After(10); //not Dispatcher.InvokeAsync
		} else { //at program startup
			CodeInfo.ReadyForStyling += () => _DocChanged();
		}
	}
	
	/// <summary>
	/// Sets timer to updates styling and folding from 0 to the end of the visible area.
	/// </summary>
	public void Update() => _update = true;
	
	SciCode _doc; //to detect when the active document changed
	bool _update;
	bool _folded;
	Sci_VisibleRange _visibleLines;
	timer _timerModified, _timerOpened;
	int _modStart;
	int _modFromEnd; //like SCI_GETENDSTYLED, but from end
	int _diagCounter;
	CancellationTokenSource _cancelTS;
	
	void _DocChanged(SciCode doc = null) {
		//bool opened = doc == null;
		doc ??= Panels.Editor.ActiveDoc;
		if (doc == null) return;
		_doc = doc;
		_update = false;
		_folded = false;
		_visibleLines = default;
		_timerModified?.Stop();
		_modStart = _modFromEnd = int.MaxValue;
		_diagCounter = 0;
		_Work(doc, cancel: true);
		//if (opened) {
		//}
	}
	
	/// <summary>
	/// Called every 250 ms while editor is visible.
	/// </summary>
	public void Timer250msWhenVisibleAndWarm(SciCode doc) {
		//We can't use Scintilla styling notifications, mostly because of Roslyn slowness.
		//To detect when need styling and folding we use 'opened' and 'modified' events and 250 ms timer.
		//When modified, we do styling for the modified line(s). Redraws faster, but unreliable, eg does not update new/deleted identifiers.
		//The timer does styling and folding for all visible lines. Redraws with a bigger delay, but updates everything after modified, scrolled, resized, folded, etc.
		
		if (_cancelTS != null || (_timerModified?.IsRunning ?? false)) return;
		if (doc != _doc || _update) {
			_update = false;
			if (doc != _doc) _DocChanged(doc);
			else _Work(doc, cancel: true);
		} else {
			Sci_GetVisibleRange(doc.AaSciPtr, out var vr); //fast
			if (vr != _visibleLines) {
				_Work(doc);
			} else if (_diagCounter > 0 && --_diagCounter == 0) {
				CodeInfo._diag.Indicators(doc.aaaPos16(vr.posFrom), doc.aaaPos16(vr.posTo));
			}
		}
	}
	
	/// <summary>
	/// Called when editor text modified.
	/// </summary>
	public void SciModified(SciCode doc, in SCNotification n) {
		//Delay to avoid multiple styling/folding/cancelling on multistep actions (replace text range, find/replace all, autocorrection) and fast automated text input.
		_cancelTS?.Cancel(); _cancelTS = null;
		_modStart = Math.Min(_modStart, n.position);
		_modFromEnd = Math.Min(_modFromEnd, doc.aaaLen8 - n.FinalPosition);
		_folded = false;
		//using var p1 = perf.local();
#if true
		_timerModified ??= new timer(_ModifiedTimer);
		if (!_timerModified.IsRunning) { _timerModified.Tag = doc; _timerModified.After(25); }
#else
		_StylingAndFolding(doc, doc.aaaLineEndFromPos(false, doc.aaaLen8 - _modFromEnd, withRN: true));
#endif
		//workaround for:
		//	On Undo, if the undo text contains hidden text, Scintilla it seems tries to show that unstyled text before styleneeded notification.
		//	If the hidden text is long, it adds horz scrollbar and scrolls.
		//	Not if the undo text ends with newline.
		if (n.modificationType.Has(MOD.SC_LASTSTEPINUNDOREDO | MOD.SC_MOD_INSERTTEXT)) {
			doc.EHideImages_(n.position, doc.aaaLineEndFromPos(false, n.position + n.length));
			doc.aaaSetStyled();
		}
	}
	
	void _ModifiedTimer(timer t) {
		//var p1 = perf.local();
		var doc = t.Tag as SciCode;
		if (doc != Panels.Editor.ActiveDoc) return;
		if (_cancelTS != null) return;
		_Work(doc, doc.aaaLineStartFromPos(false, _modStart), doc.aaaLineEndFromPos(false, doc.aaaLen8 - _modFromEnd, withRN: true));
		//p1.NW('a'); //we return without waiting for the async task to complete
	}
	
	async void _Work(SciCode doc, int start8 = 0, int end8 = -1, bool cancel = false) {
#if PRINT
		using var p1 = perf.local();
#endif
		void _PN(char ch = default) {
#if PRINT
			p1.Next(ch);
#endif
		}
		
		if (cancel) { _cancelTS?.Cancel(); _cancelTS = null; }
		Debug.Assert(_cancelTS == null);
		var cancelTS = _cancelTS = new CancellationTokenSource();
		var cancelToken = cancelTS.Token;
		
		var cd = new CodeInfo.Context(0);
		Debug.Assert(doc == cd.sci);
		if (!cd.GetDocument()) return;
		var document = cd.document;
		var code = cd.code;
		_PN('d');
		try {
			Sci_GetVisibleRange(doc.AaSciPtr, out var vr);
			//print.it(vr);
			
			bool minimal = end8 >= 0;
			bool needFolding = !minimal && !_folded;
			List<SciFoldPoint> af = null;
			
			if (needFolding) {
				await Task.Run(() => {
					_PN('s');
					af = CiFolding.GetFoldPoints(cd.syntaxRoot, code, cancelToken);
				});
				if (_Canceled()) return;
			}
			_PN('p');
			
			if (minimal) {
				start8 = Math.Max(start8, vr.posFrom);
				end8 = Math.Min(end8, vr.posTo);
			} else {
				if (needFolding) {
					CiFolding.Fold(doc, af);
					_folded = true;
				}
				Sci_GetVisibleRange(doc.AaSciPtr, out vr);
				_PN('F');
				start8 = vr.posFrom;
				end8 = vr.posTo;
			}
			//if (end8 == vr.posTo) _modFromEnd = doc.aaaLen8 - end8; //old code, now don't know its purpose. If need, then maybe do the same for _modStart.
			if (end8 <= start8) return;
			
#if PRINT
			//print.it($"<><c green>lines {doc.aaaLineFromPos(false, start8) + 1}-{doc.aaaLineFromPos(false, end8)}, range {start8}-{end8}, {vr}<>");
#endif
			
			var ar8 = _GetVisibleRanges();
			List<StartEnd> _GetVisibleRanges() {
				//print.it(vr);
				List<StartEnd> a = new();
				StartEnd r = new(start8, end8);
				for (int dline = doc.aaaLineFromPos(false, start8), dlinePrev = dline - 1, vline = doc.Call(SCI_VISIBLEFROMDOCLINE, dline); ; dline = doc.Call(SCI_DOCLINEFROMVISIBLE, ++vline)) {
					int i = doc.aaaLineStart(false, dline); if (i >= end8) break;
					//print.it(dline + 1);
					if (dline > dlinePrev + 1) {
						a.Add(r);
						r.start = i;
					}
					r.end = i + doc.Call(SCI_LINELENGTH, dline);
					dlinePrev = dline;
				}
				a.Add(r);
				//print.it("a", a);
				return a;
			}
			
			var ar = new (List<ClassifiedSpan> a, StartEnd r)[ar8.Count];
			for (int i = 0; i < ar8.Count; i++) ar[i].r = new StartEnd(doc.aaaPos16(ar8[i].start), doc.aaaPos16(ar8[i].end));
			SemanticModel semo = null;
			
			await Task.Run(async () => {
				semo = await document.GetSemanticModelAsync(cancelToken).ConfigureAwait(false);
				_PN('m'); //BAD: slow when [re]opening a file in a large project
				for (int i = 0; i < ar8.Count; i++) {
					var r = ar[i].r;
					ar[i].a = CiUtil.GetClassifiedSpans(semo, document, r.start, r.end, cancelToken);
				}
				//info: GetClassifiedSpansAsync calls GetSemanticModelAsync and GetClassifiedSpans, like here.
				//GetSemanticModelAsync+GetClassifiedSpans are slow, ~ 90% of total time.
				//Tried to implement own "GetClassifiedSpans", but slow too, often slower, because GetSymbolInfo is slow.
			});
			if (_Canceled()) return;
			_PN('c');
			
			var b = new byte[end8 - start8];
			
			char prevPunctuation = default;
			foreach (var (a, r) in ar) {
				foreach (var v in a) {
					EStyle style = StyleFromClassifiedSpan(v, semo);
					//print.it($"<><c green>{v.ClassificationType}<> '{code[v.TextSpan.Start..v.TextSpan.End]}' style={style}");
					
					if (style == EStyle.None) {
#if DEBUG
						switch (v.ClassificationType) {
						case ClassificationTypeNames.Identifier or ClassificationTypeNames.PreprocessorText: break;
						default: Debug_.PrintIf(!v.ClassificationType.Starts("regex"), $"<c gray>{v.ClassificationType}, {v.TextSpan}<>"); break;
						}
#endif
					} else {
						//int spanStart16 = v.TextSpan.Start, spanEnd16 = v.TextSpan.End;
						int spanStart16 = Math.Max(v.TextSpan.Start, r.start), spanEnd16 = Math.Min(v.TextSpan.End, r.end);
						int spanStart8 = doc.aaaPos8(spanStart16), spanEnd8 = doc.aaaPos8(spanEnd16);
						_SetStyleRange((byte)style);
						if (style is EStyle.String && prevPunctuation is '(' or ',' or ':') _RegexString();
						prevPunctuation = style is EStyle.Punctuation ? code[v.TextSpan.End - 1] : default;
						
						void _SetStyleRange(byte style) {
							for (int i = spanStart8; i < spanEnd8; i++) b[i - start8] = style;
						}
						
						void _RegexString() {
							//we need only verbatim and raw strings, and not interpolated
							bool verbatim = v.ClassificationType == ClassificationTypeNames.VerbatimStringLiteral;
							if (verbatim) {
								if (v.TextSpan.Length < 4 || !code.Eq(v.TextSpan.Start, "@\"")) return;
							} else {
								if (v.TextSpan.Length < 7 || !code.Eq(v.TextSpan.Start, "\"\"\"")) return;
							}
							
							var tok = cd.syntaxRoot.FindToken(v.TextSpan.Start); //fast here
							if (tok.Parent is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } node) return;
							var format = CiUtil.GetParameterStringFormat(tok.Parent, semo, true, ignoreInterpolatedString: true);
							if (!(format is PSFormat.Regexp or PSFormat.NetRegex or PSFormat.Wildex)) return;
							
							var (from, to) = v.TextSpan;
							if (verbatim) {
								from += 2; if (code[to - 1] is '"') to--;
							} else {
								while (from < to && code[from] is '"') { from++; if (code[to - 1] is '"') to--; }
								from = CiUtil.SkipNewline(code, CiUtil.SkipSpace(code, from));
								to = CiUtil.SkipNewlineBack(code, CiUtil.SkipSpaceBack(code, to));
								if (to <= from) return;
							}
							
							RegexParser.GetScintillaStylingBytes(code.AsSpan(from..to), format, b.AsSpan((spanStart8 - start8 + from - v.TextSpan.Start)..));
						}
					}
				}
			}
			doc.EHideImages_(start8, end8, b);
			_PN();
			doc.Call(SCI_STARTSTYLING, start8);
			unsafe { fixed (byte* bp = b) doc.Call(SCI_SETSTYLINGEX, b.Length, bp); }
			doc.aaaSetStyled(minimal ? int.MaxValue : end8);
			
			_modStart = _modFromEnd = int.MaxValue;
			_visibleLines = minimal ? default : vr;
			_PN('S');
			if (!minimal) {
				if (!App.Settings.edit_noImages) doc.EImagesGet_(cd, ar.SelectMany(o => o.a).ToArray(), vr);
				_diagCounter = 4; //update diagnostics after 1 s
			} else {
				CodeInfo._diag.EraseIndicatorsInLine(doc, doc.aaaCurrentPos8);
			}
		}
		catch (OperationCanceledException) { }
		catch (AggregateException e1) when (e1.InnerException is TaskCanceledException) { }
		catch (Exception e1) { Debug_.Print(e1); } //InvalidOperationException when this code: wpfBuilder ... .Also(b=>b.Panel.for)
		finally {
			cancelTS.Dispose();
			if (cancelTS == _cancelTS) _cancelTS = null;
		}
		
		bool _Canceled() {
			if (cancelToken.IsCancellationRequested) {
				_PN();
#if PRINT
				print.it($"<><c orange>canceled.  {p1.ToString()}<>");
#endif
				return true;
			}
			if (doc != Panels.Editor.ActiveDoc) {
#if PRINT
				print.it("<><c red>switched doc<>");
#endif
				return true;
			}
			return false;
		}
#if DEBUG
		//if(!s_debugPerf) { s_debugPerf = true; perf.nw('s'); }
#endif
	}
#if DEBUG
	//static bool s_debugPerf;
#endif
	
	public static EStyle StyleFromClassifiedSpan(ClassifiedSpan cs, SemanticModel semo) {
		return cs.ClassificationType switch {
			ClassificationTypeNames.ClassName => EStyle.Type,
			ClassificationTypeNames.Comment => EStyle.Comment,
			ClassificationTypeNames.ConstantName => EStyle.Constant,
			ClassificationTypeNames.ControlKeyword => EStyle.Keyword,
			ClassificationTypeNames.DelegateName => EStyle.Type,
			ClassificationTypeNames.EnumMemberName => EStyle.Constant,
			ClassificationTypeNames.EnumName => EStyle.Type,
			ClassificationTypeNames.EventName => EStyle.Event,
			ClassificationTypeNames.ExcludedCode => EStyle.Excluded,
			ClassificationTypeNames.ExtensionMethodName => EStyle.Function,
			ClassificationTypeNames.FieldName => EStyle.Field,
			//ClassificationTypeNames.Identifier => _TryResolveMethod(),
			ClassificationTypeNames.InterfaceName => EStyle.Type,
			ClassificationTypeNames.Keyword => EStyle.Keyword,
			ClassificationTypeNames.LabelName => EStyle.Label,
			ClassificationTypeNames.LocalName => EStyle.LocalVariable,
			ClassificationTypeNames.MethodName => EStyle.Function,
			ClassificationTypeNames.NamespaceName => EStyle.Namespace,
			ClassificationTypeNames.NumericLiteral => EStyle.Number,
			ClassificationTypeNames.Operator => EStyle.Operator,
			ClassificationTypeNames.OperatorOverloaded => EStyle.Function,
			ClassificationTypeNames.ParameterName => EStyle.LocalVariable,
			ClassificationTypeNames.PreprocessorKeyword => EStyle.Preprocessor,
			//ClassificationTypeNames.PreprocessorText => EStyle.None,
			ClassificationTypeNames.PropertyName => EStyle.Function,
			ClassificationTypeNames.Punctuation => EStyle.Punctuation,
			ClassificationTypeNames.RecordClassName or ClassificationTypeNames.RecordStructName => EStyle.Type,
			ClassificationTypeNames.StringEscapeCharacter => EStyle.StringEscape,
			ClassificationTypeNames.StringLiteral => EStyle.String,
			ClassificationTypeNames.StructName => EStyle.Type,
			//ClassificationTypeNames.Text => EStyle.None,
			ClassificationTypeNames.VerbatimStringLiteral => EStyle.String,
			ClassificationTypeNames.TypeParameterName => EStyle.Type,
			//ClassificationTypeNames.WhiteSpace => EStyle.None,
			
			ClassificationTypeNames.XmlDocCommentText => EStyle.XmlDocText,
			ClassificationTypeNames.XmlDocCommentAttributeName => EStyle.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentAttributeQuotes => EStyle.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentAttributeValue => EStyle.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentCDataSection => EStyle.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentComment => EStyle.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentDelimiter => EStyle.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentEntityReference => EStyle.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentName => EStyle.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentProcessingInstruction => EStyle.XmlDocTag,
			
			//ClassificationTypeNames. => EStyle.,
			_ => EStyle.None
		};
		
		//it seems don't need this anymore
		//EStyle _TryResolveMethod() { //ClassificationTypeNames.Identifier. Possibly method name when there are errors in arguments.
		//	var node = semo.Root.FindNode(cs.TextSpan);
		//	if (node?.Parent is InvocationExpressionSyntax && node.Span == cs.TextSpan && !semo.GetMemberGroup(node).IsDefaultOrEmpty) return EStyle.Function; //not too slow
		//	return EStyle.None;
		//}
	}
	
	/// <summary>
	/// Returns true if character at pos8 is in a hidden text.
	/// </summary>
	public static bool IsProtected(KScintilla sci, int pos8) => sci.Call(Sci.SCI_GETSTYLEAT, pos8) == STYLE_HIDDEN;
	
	/// <summary>
	/// Returns true if range from8..to8 intersects a hidden text, except when it is greater or equal than the hidden text range.
	/// It means the range should not be selected or modified.
	/// </summary>
	public static bool IsProtected(KScintilla sci, int from8, int to8) {
		bool p1 = IsProtected(sci, from8);
		if (to8 <= from8) return p1 && IsProtected(sci, from8 - 1);
		if (p1) return IsProtected(sci, from8 - 1) || (IsProtected(sci, to8 - 1) && IsProtected(sci, to8));
		if (IsProtected(sci, to8 - 1)) return IsProtected(sci, to8);
		return false;
	}
	
	public static int SkipProtected(KScintilla sci, int pos8) {
		while (IsProtected(sci, pos8)) pos8++;
		return pos8;
	}
}

