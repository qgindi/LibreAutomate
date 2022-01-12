//Code colors. Also calls functions of folding, images, errors.

using System.Linq;
using Au.Controls;
using static Au.Controls.Sci;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

partial class CiStyling
{
	/// <summary>
	/// Called when opening a document, when handle created but text still not loaded.
	/// </summary>
	public static void DocHandleCreated(SciCode doc) {
		TStyles.Settings.ToScintilla(doc);
		CiFolding.InitFolding(doc);
	}

	/// <summary>
	/// Called after setting editor control text when a document opened (not just switched active document).
	/// </summary>
	public static void DocTextAdded(SciCode doc, bool newFile) => CodeInfo._styling._DocTextAdded(doc, newFile);
	void _DocTextAdded(SciCode doc, bool newFile) {
		if (CodeInfo.IsReadyForStyling) {
			doc.Dispatcher.InvokeAsync(() => _DocChanged(doc, true), System.Windows.Threading.DispatcherPriority.Loaded);
			//info: if Normal priority, scintilla says there is 0 visible lines, and will need to do styling again. Timer would be OK too.
		} else { //at program startup
			CodeInfo.ReadyForStyling += () => { if (!doc.Hwnd.Is0) _DocChanged(doc, true); };
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
	timer _modTimer;
	int _modStart;
	int _modFromEnd; //like SCI_GETENDSTYLED, but from end
	int _diagCounter;
	CancellationTokenSource _cancelTS;

	void _DocChanged(SciCode doc, bool opened) {
		_doc = doc;
		_update = false;
		_folded = false;
		_visibleLines = default;
		_modTimer?.Stop();
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

		if (_cancelTS != null || (_modTimer?.IsRunning ?? false)) return;
		if (doc != _doc || _update) {
			_update = false;
			if (doc != _doc) _DocChanged(doc, false);
			else _Work(doc, cancel: true);
		} else {
			//using var p1 = perf.local();
			Sci_GetVisibleRange(doc.ZSciPtr, out var vr); //fast
			if (vr != _visibleLines) {
				_Work(doc);
			} else if (_diagCounter > 0 && --_diagCounter == 0) {
				CodeInfo._diag.Indicators(doc.zPos16(vr.posFrom), doc.zPos16(vr.posTo));
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
		_modFromEnd = Math.Min(_modFromEnd, doc.zLen8 - n.FinalPosition);
		_folded = false;
		//using var p1 = perf.local();
#if true
		_modTimer ??= new timer(_ModifiedTimer);
		if (!_modTimer.IsRunning) { _modTimer.Tag = doc; _modTimer.After(25); }
#else
		_StylingAndFolding(doc, doc.zLineEndFromPos(false, doc.zLen8 - _modFromEnd, withRN: true));
#endif
		//workaround for:
		//	On Undo, if the undo text contains hidden text, Scintilla it seems tries to show that unstyled text before styleneeded notification.
		//	If the hidden text is long, it adds horz scrollbar and scrolls.
		//	Not if the undo text ends with newline.
		if (n.modificationType.Has(MOD.SC_LASTSTEPINUNDOREDO | MOD.SC_MOD_INSERTTEXT)) {
			doc.HideImages_(n.position, doc.zLineEndFromPos(false, n.position + n.length));
			doc.zSetStyled();
		}
	}

	void _ModifiedTimer(timer t) {
		//var p1 = perf.local();
		var doc = t.Tag as SciCode;
		if (doc != Panels.Editor.ZActiveDoc) return;
		if (_cancelTS != null) return;
		_Work(doc, doc.zLineStartFromPos(false, _modStart), doc.zLineEndFromPos(false, doc.zLen8 - _modFromEnd, withRN: true));
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
		_cancelTS = new CancellationTokenSource();
		var cancelTS = _cancelTS;
		var cancelToken = cancelTS.Token;

		var cd = new CodeInfo.Context(0);
		if (!cd.GetDocument()) return;
		var document = cd.document;
		var code = cd.code;
		_PN('d');
		try {
			Sci_GetVisibleRange(doc.ZSciPtr, out var vr);
			//print.it(vr);

			bool minimal = end8 >= 0;
			bool needFolding = !minimal && !_folded;
			SyntaxNode root = null;
			List<CiFolding.FoldPoint> af = null;

			await Task.Run(async () => {
				root = await document.GetSyntaxRootAsync(cancelToken).ConfigureAwait(false);
				_PN('s');
				if (needFolding) af = CiFolding.GetFoldPoints(root, code, cancelToken);
			});
			if (_Cancelled()) return;
			_PN('p');

			if (minimal) {
				start8 = Math.Max(start8, vr.posFrom);
				end8 = Math.Min(end8, vr.posTo);
			} else {
				if (needFolding) {
					CiFolding.Fold(doc, af);
					_folded = true;
				}
				Sci_GetVisibleRange(doc.ZSciPtr, out vr);
				_PN('F');
				start8 = vr.posFrom;
				end8 = vr.posTo;
			}
			//if (end8 == vr.posTo) _modFromEnd = doc.zLen8 - end8; //old code, now don't know its purpose. If need, then maybe do the same for _modStart.
			if (end8 <= start8) return;

#if PRINT
			//print.it($"<><c green>lines {doc.zLineFromPos(false, start8) + 1}-{doc.zLineFromPos(false, end8)}, range {start8}-{end8}, {vr}<>");
#endif

			var ar8 = _GetVisibleRanges();
			List<StartEnd> _GetVisibleRanges() {
				//print.it(vr);
				List<StartEnd> a = new();
				StartEnd r = new(start8, end8);
				for (int dline = doc.zLineFromPos(false, start8), dlinePrev = dline - 1, vline = doc.Call(SCI_VISIBLEFROMDOCLINE, dline); ; dline = doc.Call(SCI_DOCLINEFROMVISIBLE, ++vline)) {
					int i = doc.zLineStart(false, dline); if (i >= end8) break;
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

			var ar = new (IEnumerable<ClassifiedSpan> a, StartEnd r)[ar8.Count];
			for (int i = 0; i < ar8.Count; i++) ar[i].r = new StartEnd(doc.zPos16(ar8[i].start), doc.zPos16(ar8[i].end));
			SemanticModel semo = null;

			await Task.Run(async () => {
				semo = await document.GetSemanticModelAsync(cancelToken).ConfigureAwait(false);
				_PN('m');
				for (int i = 0; i < ar8.Count; i++) {
					var r = ar[i].r;
					ar[i].a = Classifier.GetClassifiedSpans(semo, TextSpan.FromBounds(r.start, r.end), document.Project.Solution.Workspace, cancelToken);
				}
				//info: GetClassifiedSpansAsync calls GetSemanticModelAsync and GetClassifiedSpans, like here.
				//GetSemanticModelAsync+GetClassifiedSpans are slow, ~ 90% of total time.
				//Tried to implement own "GetClassifiedSpans", but slow too, often slower, because GetSymbolInfo is slow.
			});
			if (_Cancelled()) return;
			_PN('c');

			var b = new byte[end8 - start8];

			foreach (var (a, r) in ar) {
				foreach (var v in a) {
					//print.it(v.ClassificationType, code[v.TextSpan.Start..v.TextSpan.End]);
					EToken style = StyleFromClassifiedSpan(v, semo);

					if (style == EToken.None) {
#if DEBUG
						switch (v.ClassificationType) {
						case ClassificationTypeNames.Identifier: break;
						case ClassificationTypeNames.LabelName: break;
						case ClassificationTypeNames.PreprocessorText: break;
						case ClassificationTypeNames.StaticSymbol: break;
						default: Debug_.PrintIf(!v.ClassificationType.Starts("regex"), $"<><c gray>{v.ClassificationType}, {v.TextSpan}<>"); break;
						}
#endif
						continue;
					}

					//int spanStart16 = v.TextSpan.Start, spanEnd16 = v.TextSpan.End;
					int spanStart16 = Math.Max(v.TextSpan.Start, r.start), spanEnd16 = Math.Min(v.TextSpan.End, r.end);
					int spanStart8 = doc.zPos8(spanStart16), spanEnd8 = doc.zPos8(spanEnd16);
					_SetStyleRange((byte)style);

					void _SetStyleRange(byte style) {
						for (int i = spanStart8; i < spanEnd8; i++) b[i - start8] = style;
					}
				}
			}
			doc.HideImages_(start8, end8, b);
			_PN();
			doc.Call(SCI_STARTSTYLING, start8);
			unsafe { fixed (byte* bp = b) doc.Call(SCI_SETSTYLINGEX, b.Length, bp); }
			doc.zSetStyled(minimal ? int.MaxValue : end8);

			_modStart = _modFromEnd = int.MaxValue;
			_visibleLines = minimal ? default : vr;
			_PN('S');
			if (!minimal) {
				doc._ImagesGet(cd, ar.SelectMany(o => o.a), vr);
				_diagCounter = 4; //update diagnostics after 1 s
			} else {
				CodeInfo._diag.EraseIndicatorsInLine(doc, doc.zCurrentPos8);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception e1) { Debug_.Print(e1); return; } //InvalidOperationException when this code: wpfBuilder ... .Also(b=>b.Panel.for)
		finally {
			cancelTS.Dispose();
			if (cancelTS == _cancelTS) _cancelTS = null;
		}

		bool _Cancelled() {
			if (cancelToken.IsCancellationRequested) {
				_PN();
#if PRINT
				print.it($"<><c orange>canceled.  {p1.ToString()}<>");
#endif
				return true;
			}
			if (doc != Panels.Editor.ZActiveDoc) {
#if PRINT
				print.it("<><c red>switched doc<>");
#endif
				return true;
			}
			return false;
		}
#if TRACE
		//if(!s_debugPerf) { s_debugPerf = true; perf.nw('s'); }
#endif
	}
#if TRACE
	//static bool s_debugPerf;
#endif

	public static EToken StyleFromClassifiedSpan(ClassifiedSpan cs, SemanticModel semo) {
		return cs.ClassificationType switch {
			ClassificationTypeNames.ClassName => EToken.Type,
			ClassificationTypeNames.Comment => EToken.Comment,
			ClassificationTypeNames.ConstantName => EToken.Constant,
			ClassificationTypeNames.ControlKeyword => EToken.Keyword,
			ClassificationTypeNames.DelegateName => EToken.Type,
			ClassificationTypeNames.EnumMemberName => EToken.Constant,
			ClassificationTypeNames.EnumName => EToken.Type,
			ClassificationTypeNames.EventName => EToken.Function,
			ClassificationTypeNames.ExcludedCode => EToken.Excluded,
			ClassificationTypeNames.ExtensionMethodName => EToken.Function,
			ClassificationTypeNames.FieldName => EToken.Variable,
			ClassificationTypeNames.Identifier => _TryResolveMethod(),
			ClassificationTypeNames.InterfaceName => EToken.Type,
			ClassificationTypeNames.Keyword => EToken.Keyword,
			ClassificationTypeNames.LabelName => EToken.Label,
			ClassificationTypeNames.LocalName => EToken.Variable,
			ClassificationTypeNames.MethodName => EToken.Function,
			ClassificationTypeNames.NamespaceName => EToken.Namespace,
			ClassificationTypeNames.NumericLiteral => EToken.Number,
			ClassificationTypeNames.Operator => EToken.Operator,
			ClassificationTypeNames.OperatorOverloaded => EToken.Function,
			ClassificationTypeNames.ParameterName => EToken.Variable,
			ClassificationTypeNames.PreprocessorKeyword => EToken.Preprocessor,
			//ClassificationTypeNames.PreprocessorText => EStyle.None,
			ClassificationTypeNames.PropertyName => EToken.Function,
			ClassificationTypeNames.Punctuation => EToken.Punctuation,
			ClassificationTypeNames.RecordClassName or ClassificationTypeNames.RecordStructName => EToken.Type,
			ClassificationTypeNames.StringEscapeCharacter => EToken.StringEscape,
			ClassificationTypeNames.StringLiteral => EToken.String,
			ClassificationTypeNames.StructName => EToken.Type,
			//ClassificationTypeNames.Text => EStyle.None,
			ClassificationTypeNames.VerbatimStringLiteral => EToken.String,
			ClassificationTypeNames.TypeParameterName => EToken.Type,
			//ClassificationTypeNames.WhiteSpace => EStyle.None,

			ClassificationTypeNames.XmlDocCommentText => EToken.XmlDocText,
			ClassificationTypeNames.XmlDocCommentAttributeName => EToken.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentAttributeQuotes => EToken.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentAttributeValue => EToken.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentCDataSection => EToken.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentComment => EToken.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentDelimiter => EToken.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentEntityReference => EToken.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentName => EToken.XmlDocTag,
			ClassificationTypeNames.XmlDocCommentProcessingInstruction => EToken.XmlDocTag,

			//FUTURE: Regex. But how to apply it to regexp?
			//ClassificationTypeNames. => EStyle.,
			_ => EToken.None
		};

		EToken _TryResolveMethod() { //ClassificationTypeNames.Identifier. Possibly method name when there are errors in arguments.
			var node = semo.Root.FindNode(cs.TextSpan);
			if (node?.Parent is InvocationExpressionSyntax && !semo.GetMemberGroup(node).IsDefaultOrEmpty) return EToken.Function; //not too slow
			return EToken.None;
		}
	}

	/// <summary>
	/// Scintilla style indices of token types.
	/// </summary>
	public enum EToken : byte
	{
		None,
		Comment,
		String,
		StringEscape,
		Number,
		Punctuation,
		Operator,
		Keyword,
		Namespace,
		Type,
		Function,
		Variable,
		Constant,
		Label,
		Preprocessor,
		Excluded,
		XmlDocText,
		XmlDocTag, //tags, CDATA, ///, etc

		countUserDefined,

		Image = countUserDefined,

		//STYLE_HIDDEN=31,
		//STYLE_DEFAULT=32,

		LineNumber = 33, //STYLE_LINENUMBER
	}

	public struct TStyle
	{
		public int color;
		public bool bold;

		public TStyle(int color, bool bold) {
			this.color = color;
			this.bold = bold;
		}

		public static implicit operator TStyle(int color) => new(color, false);
	}

	public record TStyles //note: must be record, because uses synthesized ==
	{
		public string FontName = "Consolas";
		public int FontSize = 10;
		public int BackgroundColor = 0xffffff;

		public TStyle None; //black
		public TStyle Comment = 0x60A000; //light green, towards yellow
		public TStyle String = 0xA07040; //brown, more green
		public TStyle StringEscape = 0xB776FB; //pink-purple like in VS
		public TStyle Number = 0x804000; //brown, more red
		public TStyle Punctuation; //black
		public TStyle Operator = 0x0000ff; //blue like keyword
		public TStyle Keyword = 0x0000ff; //blue like in VS
		public TStyle Namespace = 0x808000; //dark yellow
		public TStyle Type = 0x0080c0; //like in VS but more blue
		public TStyle Function = new(0, true);
		public TStyle Variable = 0x204020; //dark green gray
		public TStyle Constant = 0x204020; //like variable
		public TStyle Label = 0xff00ff; //magenta
		public TStyle Preprocessor = 0xff8000; //orange
		public TStyle Excluded = 0x808080; //gray
		public TStyle XmlDocText = 0x408000; //green
		public TStyle XmlDocTag = 0x808080; //gray

		public TStyle LineNumber = 0x808080;

		public static TStyles Settings {
			get => s_styles ??= new TStyles();
			set {
				s_styles = value;
				if (value != null) value._Save();
				else filesystem.delete(s_settingsFile);
			}
		}
		static TStyles s_styles;
		internal static readonly string s_settingsFile = AppSettings.DirBS + "Font.csv";

		public TStyles() {
			csvTable csv;
			if (!filesystem.exists(s_settingsFile).isFile) return;
			try { csv = csvTable.load(s_settingsFile); }
			catch (Exception e1) { print.it(e1.ToStringWithoutStack()); return; }
			if (csv.ColumnCount < 2) return;

			foreach (var a in csv.Rows) {
				switch (a[0]) {
				case "Font":
					if (!a[1].NE()) FontName = a[1];
					if (a.Length > 2) { int fs = a[2].ToInt(); if (fs >= 5 && fs <= 100) FontSize = fs; }
					break;
				case "Background":
					if (!a[1].NE()) BackgroundColor = a[1].ToInt();
					break;
				case nameof(None): _Style(ref None, a); break;
				case nameof(Comment): _Style(ref Comment, a); break;
				case nameof(String): _Style(ref String, a); break;
				case nameof(StringEscape): _Style(ref StringEscape, a); break;
				case nameof(Number): _Style(ref Number, a); break;
				case nameof(Punctuation): _Style(ref Punctuation, a); break;
				case nameof(Operator): _Style(ref Operator, a); break;
				case nameof(Keyword): _Style(ref Keyword, a); break;
				case nameof(Namespace): _Style(ref Namespace, a); break;
				case nameof(Type): _Style(ref Type, a); break;
				case nameof(Function): _Style(ref Function, a); break;
				case nameof(Variable): _Style(ref Variable, a); break;
				case nameof(Constant): _Style(ref Constant, a); break;
				case nameof(Label): _Style(ref Label, a); break;
				case nameof(Preprocessor): _Style(ref Preprocessor, a); break;
				case nameof(Excluded): _Style(ref Excluded, a); break;
				case nameof(XmlDocText): _Style(ref XmlDocText, a); break;
				case nameof(XmlDocTag): _Style(ref XmlDocTag, a); break;
				case nameof(LineNumber): _Style(ref LineNumber, a); break;
				}
			}

			static void _Style(ref TStyle r, string[] a) {
				if (!a[1].NE()) r.color = a[1].ToInt();
				if (a.Length > 2 && !a[2].NE()) r.bold = 0 != (1 & a[2].ToInt()); else r.bold = false;
			}
		}

		void _Save() {
			var b = new StringBuilder(); //don't need csvTable for such simple values
			b.AppendFormat("Font, {0}, {1}\r\n", FontName, FontSize);
			b.Append("Background, 0x").AppendLine(BackgroundColor.ToString("X6"));
			_Style(nameof(None), None);
			_Style(nameof(Comment), Comment);
			_Style(nameof(String), String);
			_Style(nameof(StringEscape), StringEscape);
			_Style(nameof(Number), Number);
			_Style(nameof(Punctuation), Punctuation);
			_Style(nameof(Operator), Operator);
			_Style(nameof(Keyword), Keyword);
			_Style(nameof(Namespace), Namespace);
			_Style(nameof(Type), Type);
			_Style(nameof(Function), Function);
			_Style(nameof(Variable), Variable);
			_Style(nameof(Constant), Constant);
			_Style(nameof(Label), Label);
			_Style(nameof(Preprocessor), Preprocessor);
			_Style(nameof(Excluded), Excluded);
			_Style(nameof(XmlDocText), XmlDocText);
			_Style(nameof(XmlDocTag), XmlDocTag);
			_Style(nameof(LineNumber), LineNumber);

			void _Style(string name, TStyle r) {
				b.Append(name).Append(", 0x").Append(r.color.ToString("X6"));
				if (r.bold) b.Append(", 1");
				b.AppendLine();
			}

			filesystem.saveText(s_settingsFile, b.ToString());
		}

		/// <summary>
		/// Gets colors, bold, but not font properties.
		/// </summary>
		public TStyles(KScintilla sci) {
			BackgroundColor = ColorInt.SwapRB(sci.Call(SCI_STYLEGETBACK));

			TStyle _Get(EToken tok) {
				int color = ColorInt.SwapRB(sci.Call(SCI_STYLEGETFORE, (int)tok));
				bool bold = 0 != sci.Call(SCI_STYLEGETBOLD, (int)tok);
				return new TStyle(color, bold);
			}

			None = _Get(EToken.None);
			Comment = _Get(EToken.Comment);
			String = _Get(EToken.String);
			StringEscape = _Get(EToken.StringEscape);
			Number = _Get(EToken.Number);
			Punctuation = _Get(EToken.Punctuation);
			Operator = _Get(EToken.Operator);
			Keyword = _Get(EToken.Keyword);
			Namespace = _Get(EToken.Namespace);
			Type = _Get(EToken.Type);
			Function = _Get(EToken.Function);
			Variable = _Get(EToken.Variable);
			Constant = _Get(EToken.Constant);
			Label = _Get(EToken.Label);
			Preprocessor = _Get(EToken.Preprocessor);
			Excluded = _Get(EToken.Excluded);
			XmlDocText = _Get(EToken.XmlDocText);
			XmlDocTag = _Get(EToken.XmlDocTag);

			LineNumber = _Get(EToken.LineNumber);
		}

		/// <param name="multiFont">Set font only for code styles.</param>
		public void ToScintilla(KScintilla sci, bool multiFont = false) {
			if (!multiFont) sci.zStyleFont(STYLE_DEFAULT, FontName, FontSize);
			sci.zStyleBackColor(STYLE_DEFAULT, BackgroundColor);
			//if(None.color != 0) sci.zStyleForeColor(STYLE_DEFAULT, None.color); //also would need bold and in ctor above
			sci.zStyleClearAll(); //belowDefault could be true, but currently don't need it and would need to test everywhere

			void _Set(EToken tok, TStyle sty) {
				sci.zStyleForeColor((int)tok, sty.color);
				if (sty.bold) sci.zStyleBold((int)tok, true);
				if (multiFont) sci.zStyleFont((int)tok, FontName, FontSize);
			}

			_Set(EToken.None, None);
			_Set(EToken.Comment, Comment);
			_Set(EToken.String, String);
			_Set(EToken.StringEscape, StringEscape);
			_Set(EToken.Number, Number);
			_Set(EToken.Punctuation, Punctuation);
			_Set(EToken.Operator, Operator);
			_Set(EToken.Keyword, Keyword);
			_Set(EToken.Namespace, Namespace);
			_Set(EToken.Type, Type);
			_Set(EToken.Function, Function);
			_Set(EToken.Variable, Variable);
			_Set(EToken.Constant, Constant);
			_Set(EToken.Label, Label);
			_Set(EToken.Preprocessor, Preprocessor);
			_Set(EToken.Excluded, Excluded);
			_Set(EToken.XmlDocText, XmlDocText);
			_Set(EToken.XmlDocTag, XmlDocTag);

			_Set((EToken)STYLE_LINENUMBER, LineNumber);
		}

		//not used
		//public TStyle GetStyle(EToken token) {
		//	return token switch {
		//		EToken.None => None,
		//		EToken.Comment => Comment,
		//		EToken.String => String,
		//		EToken.StringEscape => StringEscape,
		//		EToken.Number => Number,
		//		EToken.Punctuation => Punctuation,
		//		EToken.Operator => Operator,
		//		EToken.Keyword => Keyword,
		//		EToken.Namespace => Namespace,
		//		EToken.Type => Type,
		//		EToken.Function => Function,
		//		EToken.Variable => Variable,
		//		EToken.Constant => Constant,
		//		EToken.Label => Label,
		//		EToken.Preprocessor => Preprocessor,
		//		EToken.Excluded => Excluded,
		//		EToken.XmlDocText => XmlDocText,
		//		EToken.XmlDocTag => XmlDocTag,
		//		EToken.LineNumber => LineNumber,
		//		_ => default,
		//	};
		//}

		//public void SetStyle(EToken token, TStyle style) {
		//	switch(token) {
		//	case EToken.None: None = style; break;
		//	case EToken.Comment: Comment = style; break;
		//	case EToken.String: String = style; break;
		//	case EToken.StringEscape: StringEscape = style; break;
		//	case EToken.Number: Number = style; break;
		//	case EToken.Punctuation: Punctuation = style; break;
		//	case EToken.Operator: Operator = style; break;
		//	case EToken.Keyword: Keyword = style; break;
		//	case EToken.Namespace: Namespace = style; break;
		//	case EToken.Type: Type = style; break;
		//	case EToken.Function: Function = style; break;
		//	case EToken.Variable: Variable = style; break;
		//	case EToken.Constant: Constant = style; break;
		//	case EToken.Label: Label = style; break;
		//	case EToken.Preprocessor: Preprocessor = style; break;
		//	case EToken.Excluded: Excluded = style; break;
		//	case EToken.XmlDocText: XmlDocText = style; break;
		//	case EToken.XmlDocTag: XmlDocTag = style; break;
		//	case EToken.LineNumber: LineNumber = style; break;
		//	}
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

