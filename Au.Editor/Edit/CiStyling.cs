//Code colors. Also calls functions of folding, images, errors.

//#if TRACE
//#define PRINT
//#endif

//#define HIDE_IMAGE_STRING

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
		doc.zSetStyled();
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
		doc.zSetStyled();
		//using var p1 = perf.local();
#if true
		_modTimer ??= new timer(_ModifiedTimer);
		if (!_modTimer.IsRunning) { _modTimer.Tag = doc; _modTimer.After(25); }
#else
		_StylingAndFolding(doc, doc.zLineEndFromPos(false, doc.zLen8 - _modFromEnd, withRN: true));
#endif
	}

	void _ModifiedTimer(timer t) {
		//var p1 = perf.local();
		var doc = t.Tag as SciCode;
		if (doc != Panels.Editor.ZActiveDoc) return;
		if (_cancelTS != null) return;
		_Work(doc, _modStart, doc.zLineEndFromPos(false, doc.zLen8 - _modFromEnd, withRN: true));
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
				if (needFolding) af = CiFolding.GetFoldPoints(root, cd.code, cancelToken);
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
					//print.it(v.ClassificationType, cd.code[v.TextSpan.Start..v.TextSpan.End]);
					EToken style = v.ClassificationType switch {
						#region
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
						#endregion
					};

					EToken _TryResolveMethod() { //ClassificationTypeNames.Identifier. Possibly method name when there are errors in arguments.
						var node = semo.Root.FindNode(v.TextSpan);
						if (node?.Parent is InvocationExpressionSyntax && !semo.GetMemberGroup(node).IsDefaultOrEmpty) return EToken.Function; //not too slow
						return EToken.None;
					}

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

					//hide image Base64. Actually currently only changes color. Can't hide because of scintilla bugs.
					if (v.TextSpan.Length > 10) {
						//note: hide only @"image:string" and /*image:comment*/, but not "image:string" and //... /*image:comment*/.
						//	It simplifies code and allows user to unhide if wants.
						//	Where need some other comment before /*image:comment*/, use /*other comment*/ /*image:comment*/.
						switch (style) {
						case EToken.Comment when cd.code.AsSpan(v.TextSpan.Start).StartsWith("/*image:"):
						case EToken.String when cd.code.AsSpan(v.TextSpan.Start).StartsWith("@\"image:"):
#if HIDE_IMAGE_STRING //if hidden style would work
							spanStart8 += 7; spanEnd8 -= style == EToken.String ? 1 : 2;
#else
							if (style == EToken.String) { spanStart8 += 2; spanEnd8 -= 1; }
#endif
							_SetStyleRange((byte)EToken.Image);
							break;
						}
					}
				}
			}
			_PN();
			doc.Call(SCI_STARTSTYLING, start8);
			unsafe { fixed (byte* bp = b) doc.Call(SCI_SETSTYLINGEX, b.Length, bp); }
			doc.zSetStyled();

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

#pragma warning disable CS0660, CS0661 // Type defines operator == or operator != but does not override Object.Equals(object o)
	public struct TStyle
#pragma warning restore
	{
		public int color;
		public bool bold;
		public bool hidden;
		//public bool small;

		public TStyle(int color, bool bold, bool hidden = false) {
			this.color = color;
			this.bold = bold;
			this.hidden = hidden;
			//this.small = small;
		}

		public static implicit operator TStyle(int color) => new(color, false);

		public static bool operator ==(TStyle a, TStyle b) => a.color == b.color && a.bold == b.bold;
		public static bool operator !=(TStyle a, TStyle b) => !(a == b);
	}

	public record TStyles //note: must be record, because uses synthesized ==
	{
		public string FontName = "Consolas";
		public int FontSize = 10;
		public int BackgroundColor = 0xffffff;

		public TStyle None; //black
							//public TStyle Comment = 0x408000; //green like in VS but towards yellow
		public TStyle Comment = 0x60B000; //light green, towards yellow
		public TStyle String = 0xA07040; //brown, more green
										 //0xc0c0c0; //good contrast with 0xA07040, but maybe not with white background
										 //0xc0e000; //light yellow-green. Too vivid.
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

		//public TStyle Image = new(0xf0f0f0, false, true); //hidden
		//public TStyle Image = 0xe0e0e0;
		public TStyle Image = 0xffffff; //visible only when selected or if dark theme
		//public TStyle Image = 0xf8f8f8; //barely visible, unless selected or if dark theme

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

		public void ToScintilla(KScintilla sci) {
			sci.zStyleFont(STYLE_DEFAULT, FontName, FontSize);
			sci.zStyleBackColor(STYLE_DEFAULT, BackgroundColor);
			//if(None.color != 0) sci.zStyleForeColor(STYLE_DEFAULT, None.color); //also would need bold and in ctor above
			sci.zStyleClearAll();

			void _Set(EToken tok, TStyle sty) {
				sci.zStyleForeColor((int)tok, sty.color);
				if (sty.bold) sci.zStyleBold((int)tok, true);
#if HIDE_IMAGE_STRING
				//cannot use hidden style or small font because of scintilla bug:
				//	1. In wrap mode draws as many lines as with big font. Even caret is large and spans all lines.
				//		Plus other anomalies, eg when scrolling.
				//		I could not find a workaround. Tried SCI_SETLAYOUTCACHE, SCI_SETPOSITIONCACHE, SCI_SETHOTSPOTSINGLELINE, etc.
				//		//TODO: update scintilla. Fixed several bugs similar to this.
				//	2. User cannot delete text containing hidden text.
				//		Need to modify scintilla source; maybe just simply modify IsProtected() in Style.h.
				//if (sty.hidden) sci.zStyleHidden((int)tok, true);
				//if (sty.small) { sci.zStyleFont((int)tok, "Gabriola", 1); sci.Call(SCI_STYLESETCASE, (int)tok, 2); } //smallest font available on Win7 too

				if (sty.hidden) { sci.zStyleHidden((int)tok, true); }
				//if (sty.hidden) { /*sci.zStyleHidden((int)tok, true);*/ sci.zStyleHotspot((int)tok, true); }
#endif
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

			_Set(EToken.Image, Image);

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
}

