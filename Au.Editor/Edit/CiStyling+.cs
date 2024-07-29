using Au.Controls;
using static Au.Controls.Sci;

partial class CiStyling {
	/// <summary>
	/// Scintilla style indices of token kinds.
	/// </summary>
	public enum EStyle : byte {
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
		Event,
		LocalVariable,
		Field,
		Constant,
		Label,
		Preprocessor,
		Excluded,
		XmlDocText,
		XmlDocTag, //tags, CDATA, ///, etc
		RxText,
		RxMeta,
		RxChars,
		RxOption,
		RxEscape,
		RxCallout,
		RxComment,
		
		countUserDefined,
		
		Image = countUserDefined,
		
		//STYLE_HIDDEN=31,
		//STYLE_DEFAULT=32,
		
		LineNumber = 33, //STYLE_LINENUMBER
	}
	
	public record struct TStyle(int color, bool back = false, bool bold = false, bool italic = false, bool underline = false) {
		public static implicit operator TStyle(int color) => new(color);
	}
	
	public record struct TIndicator(int color, int alpha);
	
	public record class TTheme { //info: record class because need `with` and synthesized ==
		public string FontName = "Consolas";
		public double FontSize = 9.75;
		public int Background = 0xffffff;
		
		public TStyle None; //black
		public TStyle Comment = 0x60A000; //green-yellow
		public TStyle String = 0xA07040; //brown-green
		public TStyle StringEscape = 0xFF60FF; //pink
		public TStyle Number = 0x804000; //brown-red
		public TStyle Punctuation; //black
		public TStyle Operator = 0x0000ff; //blue
		public TStyle Keyword = 0x0000ff; //blue
		public TStyle Namespace = 0x808000; //dark yellow
		public TStyle Type = 0x0080c0; //teal-blue
		public TStyle Function = new(0, bold: true); //black bold
		public TStyle Event = new(0, bold: true);
		public TStyle LocalVariable = 0x204020; //dark gray-green
		public TStyle Field = 0x204020;
		public TStyle Constant = 0x204020;
		public TStyle Label = 0xff00ff; //magenta
		public TStyle Preprocessor = 0xff3300; //red
		public TStyle Excluded = 0x808080; //gray
		public TStyle XmlDocText = 0x408000; //green
		public TStyle XmlDocTag = 0x808080; //gray
		public TStyle RxText = 0xA07040;
		public TStyle RxMeta = new(0xBDD8FF, true);
		public TStyle RxChars = new(0xCBFF7D, true);
		public TStyle RxOption = new(0xFFE47B, true);
		public TStyle RxEscape = 0xFF60FF;
		public TStyle RxCallout = new(0xFF8060, true);
		public TStyle RxComment = 0x808080;
		
		public TStyle LineNumber = 0x808080;
		public int LineNumberMargin = 0xE0E0E0;
		public int MarkerMargin = 0xFFFFFF;
		
		public TIndicator IndicRefs = new(0x80C000, 40);
		public TIndicator IndicBraces = new(0x80C000, 255);
		public TIndicator IndicDebug = new(0xFFF181, 255);
		public TIndicator IndicFound = new(0xffff00, 255);
		public TIndicator IndicSnippetField = new(0xe0a000, 60);
		public TIndicator IndicSnippetFieldActive = new(0x33ADFF, 60);
		
		public int SelColor = unchecked((int)0xA0A0A0A0);
		public int SelNofocusColor = 0x60A0A0A0;
		public int Caret = 0x2000000; //alpha = thickness
		public int CaretLine = 0x1E0E0E0; //alpha = frame thickness
		
		//note: these must be before the static properties, else would be null when the properties use them.
		static readonly string s_themesDirCustomizedBS = AppSettings.DirBS + @"Themes\";
		static readonly string s_themesDirDefaultBS = folders.ThisAppBS + @"Default\Themes\";
		
		/// <summary>
		/// Default theme without customizations.
		/// Use as immutable. If need theme with some values changed, use code `CiStyling.TTheme.Default with { ... }`.
		/// </summary>
		public static TTheme Default { get; } = new();
		
		/// <summary>
		/// Current theme (with or without customizations).
		/// Use as immutable. If need theme with some values changed, use code `CiStyling.TTheme.Current with { ... }`.
		/// </summary>
		public static TTheme Current => s_current ??= new("");
		static TTheme s_current;
		
		class _ThemeInfo {
			public string
				name, //not null if can load file; without suffix
				defPath, //not null if can load a default file
				custPath; //not null, even if file does not exist
			public bool
				customized, //true if using a customized file
				custExists; //true if customized file exists, even if not using it
			
			/// <param name="theme">"" - current. null - default ("LA").</param>
			public _ThemeInfo(string theme) {
				if (theme is "") theme = App.Settings.edit_theme.NullIfEmpty_();
				if (theme is null or "LA") {
					_Cust();
					
					//In the past LA did not support multiple themes...
					if (!custExists && theme is null) {
						var sOld = AppSettings.DirBS + "Font.csv";
						if (filesystem.exists(sOld, true).File) {
							try {
								var s = filesystem.loadText(sOld);
								s = s.RxReplace(@"(?m)^Function\b(.+)", "Function$1\r\nEvent$1");
								s = s.RxReplace(@"(?m)^Variable\b(.+)", "LocalVariable$1\r\nField$1");
								filesystem.saveText(custPath, s);
								App.Settings.edit_theme = "LA [customized]";
								name = "LA";
								customized = custExists = true;
								filesystem.rename(sOld, "Font.csv.bak", FIfExists.RenameNew);
							}
							catch (Exception e1) { Debug_.Print(e1); }
						}
					}
				} else {
					bool cust = theme.Ends(" [customized]");
					if (cust) theme = theme[..^13];
					if (cust && theme is "LA") {
						_Cust();
						if (customized = custExists) name = theme;
					} else {
						var dp = s_themesDirDefaultBS + theme + ".csv";
						if (filesystem.exists(dp, true).File) {
							name = theme;
							defPath = dp;
							custPath = s_themesDirCustomizedBS + theme + ".csv";
							custExists = filesystem.exists(custPath, true).File;
							customized = cust && custExists;
						} else {
							_Cust();
						}
					}
				}
				
				void _Cust() {
					custPath = s_themesDirCustomizedBS + "LA.csv";
					custExists = filesystem.exists(custPath, true).File;
				}
			}
		}
		
		/// <summary>
		/// Gets theme name like <c>"Name"</c> or <c>"Name [customized]"</c>.
		/// </summary>
		public string Name { get; private set; } = "LA";
		
		TTheme() { }
		
		/// <summary>
		/// Loads current or specified theme (default or customized).
		/// </summary>
		/// <param name="theme">Theme name, or <c>""</c> to load current theme. If ends with <c>" [customized]"</c>, loads customized file if exists.</param>
		public TTheme(string theme) {
			var t = new _ThemeInfo(theme);
			if (t.name != null) {
				if (t.defPath != null && _Load(t.defPath)) Name = t.name; //need even if will load customized, because it may not have some items or may fail to load
				if (t.customized && _Load(t.custPath)) Name = t.name + " [customized]";
			}
		}
		
		bool _Load(string path) {
			csvTable csv;
			try { csv = csvTable.load(path); }
			catch (Exception e1) { print.it(e1); return false; }
			if (csv.ColumnCount < 2) return false;
			
			foreach (var a in csv.Rows) {
				switch (a[0]) {
				case "Font":
					if (!a[1].NE()) FontName = a[1];
					if (a.Length > 2) { var fs = a[2].ToNumber(); if (fs >= 5 && fs <= 100) FontSize = fs; }
					break;
				case nameof(Background): _Int6(ref Background); break;
				case nameof(None): _Style(ref None); break;
				case nameof(Comment): _Style(ref Comment); break;
				case nameof(String): _Style(ref String); break;
				case nameof(StringEscape): _Style(ref StringEscape); break;
				case nameof(Number): _Style(ref Number); break;
				case nameof(Punctuation): _Style(ref Punctuation); break;
				case nameof(Operator): _Style(ref Operator); break;
				case nameof(Keyword): _Style(ref Keyword); break;
				case nameof(Namespace): _Style(ref Namespace); break;
				case nameof(Type): _Style(ref Type); break;
				case nameof(Function): _Style(ref Function); break;
				case nameof(Event): _Style(ref Event); break;
				case nameof(LocalVariable): _Style(ref LocalVariable); break;
				case nameof(Field): _Style(ref Field); break;
				case nameof(Constant): _Style(ref Constant); break;
				case nameof(Label): _Style(ref Label); break;
				case nameof(Preprocessor): _Style(ref Preprocessor); break;
				case nameof(Excluded): _Style(ref Excluded); break;
				case nameof(XmlDocText): _Style(ref XmlDocText); break;
				case nameof(XmlDocTag): _Style(ref XmlDocTag); break;
				case nameof(RxText): _Style(ref RxText, true); break;
				case nameof(RxMeta): _Style(ref RxMeta, true); break;
				case nameof(RxChars): _Style(ref RxChars, true); break;
				case nameof(RxOption): _Style(ref RxOption, true); break;
				case nameof(RxEscape): _Style(ref RxEscape, true); break;
				case nameof(RxCallout): _Style(ref RxCallout, true); break;
				case nameof(RxComment): _Style(ref RxComment, true); break;
				case nameof(LineNumber): _Style(ref LineNumber); break;
				case nameof(LineNumberMargin): _Int6(ref LineNumberMargin); break;
				case nameof(MarkerMargin): _Int6(ref MarkerMargin); break;
				case nameof(IndicRefs): _Indic(ref IndicRefs); break;
				case nameof(IndicBraces): _Indic(ref IndicBraces); break;
				case nameof(IndicDebug): _Indic(ref IndicDebug); break;
				case nameof(IndicFound): _Indic(ref IndicFound); break;
				case nameof(IndicSnippetField): _Indic(ref IndicSnippetField); break;
				case nameof(IndicSnippetFieldActive): _Indic(ref IndicSnippetFieldActive); break;
				case nameof(SelColor): _Int6Int(ref SelColor); break;
				case nameof(SelNofocusColor): _Int6Int(ref SelNofocusColor); break;
				case nameof(Caret): _Int6Int(ref Caret); break;
				case nameof(CaretLine): _Int6Int(ref CaretLine); break;
				}
				
				void _Style(ref TStyle r, bool hasBack = false) {
					if (!a[1].NE() && a[1].ToInt(out int i)) r.color = i;
					if (a.Length > 2 && !a[2].NE() && a[2].ToInt(out int i2)) {
						r.bold = 0 != (i2 & 1);
						r.italic = 0 != (i2 & 2);
						r.underline = 0 != (i2 & 4);
						if (hasBack) r.back = 0 != (i2 & 8);
					} else r.bold = r.italic = r.underline = r.back = false;
				}
				
				void _Indic(ref TIndicator r) {
					int i = r.color; _Int6(ref i); r.color = i;
					i = r.alpha; _Alpha(ref i); r.alpha = i;
				}
				
				void _Int6(ref int value) {
					if (!a[1].NE() && a[1].ToInt(out int i)) value = i;
					value &= 0xffffff;
				}
				
				void _Alpha(ref int value) {
					if (a.Length > 2 && !a[2].NE() && a[2].ToInt(out int i)) value = Math.Clamp(i, 0, 255);
				}
				
				void _Int6Int(ref int r) {
					int i = r; _Int6(ref i);
					int j = r >>> 24; _Alpha(ref j);
					r = i | (j << 24);
				}
			}
			
			return true;
		}
		
		void _Save() {
			var b = new StringBuilder(); //don't need csvTable for such simple values
			b.AppendLine($"Font, {FontName}, {FontSize.ToS()}");
			_Int6(Background);
			_Style(None);
			_Style(Comment);
			_Style(String);
			_Style(StringEscape);
			_Style(Number);
			_Style(Punctuation);
			_Style(Operator);
			_Style(Keyword);
			_Style(Namespace);
			_Style(Type);
			_Style(Function);
			_Style(Event);
			_Style(LocalVariable);
			_Style(Field);
			_Style(Constant);
			_Style(Label);
			_Style(Preprocessor);
			_Style(Excluded);
			_Style(XmlDocText);
			_Style(XmlDocTag);
			_Style(RxText);
			_Style(RxMeta);
			_Style(RxChars);
			_Style(RxOption);
			_Style(RxEscape);
			_Style(RxCallout);
			_Style(RxComment);
			_Style(LineNumber);
			_Int6(LineNumberMargin);
			_Int6(MarkerMargin);
			_Indic(IndicRefs);
			_Indic(IndicBraces);
			_Indic(IndicDebug);
			_Indic(IndicFound);
			_Indic(IndicSnippetField);
			_Indic(IndicSnippetFieldActive);
			_Int6Int(SelColor);
			_Int6Int(SelNofocusColor);
			_Int6Int(Caret);
			_Int6Int(CaretLine);
			
			void _Style(TStyle r, [CallerArgumentExpression("r")] string name = null) {
				b.Append($"{name}, 0x{r.color:X6}");
				if (((r.bold ? 1 : 0) | (r.italic ? 2 : 0) | (r.underline ? 4 : 0) | (r.back ? 8 : 0)) is int i && i > 0) b.Append($", {i.ToS()}");
				b.AppendLine();
			}
			
			void _Indic(TIndicator i, [CallerArgumentExpression("i")] string name = null)
				=> b.AppendLine($"{name}, 0x{i.color:X6}, {i.alpha.ToS()}");
			
			void _Int6(int i, [CallerArgumentExpression("i")] string name = null)
				=> b.AppendLine($"{name}, 0x{i:X6}");
			
			void _Int6Int(int i, [CallerArgumentExpression("i")] string name = null)
				=> b.AppendLine($"{name}, 0x{i & 0xffffff:X6}, {(i >>> 24).ToS()}");
			
			var t = new _ThemeInfo("");
			filesystem.saveText(t.custPath, b.ToString());
		}
		
		/// <param name="multiFont">Set font only for code styles, not for STYLE_DEFAULT.</param>
		public void ToScintilla(KScintilla sci, bool multiFont = false, string fontName = null, double? fontSize = null) {
			//print.it(sci.GetType(), sci.Name);
			bool sciCode = sci is SciCode;
			
			if (!multiFont) sci.aaaStyleFont(STYLE_DEFAULT, fontName ?? FontName, fontSize ?? FontSize);
			sci.aaaStyleBackColor(STYLE_DEFAULT, Background);
			//if(None.color != 0) sci.aaaStyleForeColor(STYLE_DEFAULT, None.color); //also would need bold and in ctor above
			sci.aaaStyleClearAll(); //belowDefault could be true, but currently don't need it and would need to test everywhere
			
			void _Set(EStyle k, TStyle sty) {
				if (sty.back) {
					sci.aaaStyleBackColor((int)k, sty.color);
					sci.aaaStyleForeColor((int)k, None.color);
				} else sci.aaaStyleForeColor((int)k, sty.color);
				
				if (sty.bold) sci.aaaStyleBold((int)k, true);
				if (sty.italic) sci.aaaStyleItalic((int)k, true);
				if (sty.underline) sci.aaaStyleUnderline((int)k, true);
				if (multiFont) sci.aaaStyleFont((int)k, fontName ?? FontName, fontSize ?? FontSize);
			}
			
			_Set(EStyle.None, None);
			_Set(EStyle.Comment, Comment);
			_Set(EStyle.String, String);
			_Set(EStyle.StringEscape, StringEscape);
			_Set(EStyle.Number, Number);
			_Set(EStyle.Punctuation, Punctuation);
			_Set(EStyle.Operator, Operator);
			_Set(EStyle.Keyword, Keyword);
			_Set(EStyle.Namespace, Namespace);
			_Set(EStyle.Type, Type);
			_Set(EStyle.Function, Function);
			_Set(EStyle.Event, Event);
			_Set(EStyle.LocalVariable, LocalVariable);
			_Set(EStyle.Field, Field);
			_Set(EStyle.Constant, Constant);
			_Set(EStyle.Label, Label);
			_Set(EStyle.Preprocessor, Preprocessor);
			_Set(EStyle.Excluded, Excluded);
			_Set(EStyle.XmlDocText, XmlDocText);
			_Set(EStyle.XmlDocTag, XmlDocTag);
			_Set(EStyle.RxText, RxText);
			_Set(EStyle.RxMeta, RxMeta);
			_Set(EStyle.RxChars, RxChars);
			_Set(EStyle.RxOption, RxOption);
			_Set(EStyle.RxEscape, RxEscape);
			_Set(EStyle.RxCallout, RxCallout);
			_Set(EStyle.RxComment, RxComment);
			
			_Set((EStyle)STYLE_LINENUMBER, LineNumber);
			sci.aaaStyleBackColor(STYLE_LINENUMBER, LineNumberMargin); //documented: sets color of all margins except of type SC_MARGIN_COLOUR and the folding margin
			
			sci.aaaStyleForeColor(STYLE_INDENTGUIDE, 0xcccccc);
			
			if (sci is SciCode || sci.Name == "styles") {
				_Indic(SciCode.c_indicRefs, IndicRefs.color, IndicRefs.alpha, INDIC_FULLBOX);
				_Indic(SciCode.c_indicBraces, IndicBraces.color, IndicBraces.alpha, INDIC_GRADIENT);
				_Indic(SciCode.c_indicDebug, IndicDebug.color, IndicDebug.alpha, INDIC_FULLBOX);
				_Indic(SciCode.c_indicDebug2, IndicDebug.color, 128 + IndicDebug.alpha / 2, INDIC_GRADIENTCENTRE);
				_Indic(SciCode.c_indicFound, IndicFound.color, IndicFound.alpha, INDIC_FULLBOX);
				_Indic(SciCode.c_indicSnippetField, IndicSnippetField.color, IndicSnippetField.alpha, INDIC_FULLBOX);
				_Indic(SciCode.c_indicSnippetFieldActive, IndicSnippetFieldActive.color, IndicSnippetFieldActive.alpha, INDIC_FULLBOX);
#if DEBUG
				_Indic(SciCode.c_indicTestBox, 0xff0000, 60, INDIC_FULLBOX);
				sci.aaaIndicatorDefine(SciCode.c_indicTestStrike, INDIC_STRIKE, 0xff0000);
				sci.aaaIndicatorDefine(SciCode.c_indicTestPoint, INDIC_POINT, 0xff00ff);
#endif
				
				void _Indic(int indic, int color, int alpha, int style) {
					sci.aaaIndicatorDefine(indic, style, color, alpha, 255, underText: true);
				}
				//void _Indic(int indic, int color, int alpha, bool gradient) {
				//	sci.aaaIndicatorDefine(indic, gradient ? INDIC_GRADIENT : INDIC_FULLBOX, color, alpha, 255, underText: true);
				//}
			}
			
			sci.aaaSetElementColor(SC_ELEMENT_SELECTION_BACK, SelColor);
			sci.aaaSetElementColor(SC_ELEMENT_SELECTION_INACTIVE_BACK, SelNofocusColor);
			sci.aaaSetElementColor(SC_ELEMENT_CARET, Caret & 0xffffff);
			sci.Call(SCI_SETCARETWIDTH, Math.Clamp(Caret >>> 24, 1, 4)); //not DPI-scaled
			if (sci is SciCode) {
				sci.aaaSetElementColor(SC_ELEMENT_CARET_LINE_BACK, CaretLine & 0xffffff);
				sci.Call(SCI_SETCARETLINEFRAME, Math.Clamp(CaretLine >>> 24, 1, 4)); //not DPI-scaled
				sci.Call(SCI_SETCARETLINEVISIBLEALWAYS, 1);
				
				sci.Call(SCI_SETMARGINBACKN, SciCode.c_marginMarkers, ColorInt.SwapRB(MarkerMargin));
			}
			sci.aaaSetElementColor(SC_ELEMENT_WHITE_SPACE, 0x808080); //space/tab visuals and wrap visuals
		}
		
		public ref TStyle this[EStyle style] {
			get {
				switch (style) {
				case EStyle.None: return ref None;
				case EStyle.Comment: return ref Comment;
				case EStyle.String: return ref String;
				case EStyle.StringEscape: return ref StringEscape;
				case EStyle.Number: return ref Number;
				case EStyle.Punctuation: return ref Punctuation;
				case EStyle.Operator: return ref Operator;
				case EStyle.Keyword: return ref Keyword;
				case EStyle.Namespace: return ref Namespace;
				case EStyle.Type: return ref Type;
				case EStyle.Function: return ref Function;
				case EStyle.Event: return ref Event;
				case EStyle.LocalVariable: return ref LocalVariable;
				case EStyle.Field: return ref Field;
				case EStyle.Constant: return ref Constant;
				case EStyle.Label: return ref Label;
				case EStyle.Preprocessor: return ref Preprocessor;
				case EStyle.Excluded: return ref Excluded;
				case EStyle.XmlDocText: return ref XmlDocText;
				case EStyle.XmlDocTag: return ref XmlDocTag;
				case EStyle.RxText: return ref RxText;
				case EStyle.RxMeta: return ref RxMeta;
				case EStyle.RxChars: return ref RxChars;
				case EStyle.RxOption: return ref RxOption;
				case EStyle.RxEscape: return ref RxEscape;
				case EStyle.RxCallout: return ref RxCallout;
				case EStyle.RxComment: return ref RxComment;
				case EStyle.LineNumber: return ref LineNumber;
				}
				throw new InvalidEnumArgumentException();
			}
		}
		
		/// <param name="indicator">SciCode.c_indicX.</param>
		public ref TIndicator Indicator(int indicator) {
			switch (indicator) {
			case SciCode.c_indicRefs: return ref IndicRefs;
			case SciCode.c_indicBraces: return ref IndicBraces;
			case SciCode.c_indicDebug: return ref IndicDebug;
			case SciCode.c_indicFound: return ref IndicFound;
			case SciCode.c_indicSnippetField: return ref IndicSnippetField;
			case SciCode.c_indicSnippetFieldActive: return ref IndicSnippetFieldActive;
			}
			throw new InvalidEnumArgumentException();
		}
		
		public ref int Element(int element) {
			switch (element) {
			case Sci.SC_ELEMENT_SELECTION_BACK: return ref SelColor;
			case Sci.SC_ELEMENT_SELECTION_INACTIVE_BACK: return ref SelNofocusColor;
			case Sci.SC_ELEMENT_CARET: return ref Caret;
			case Sci.SC_ELEMENT_CARET_LINE_BACK: return ref CaretLine;
			case -1: return ref LineNumberMargin;
			case -2: return ref MarkerMargin;
			}
			throw new InvalidEnumArgumentException();
		}
		
		public static TTheme OptionsMenu(TTheme t, AnyWnd owner) {
			var m = new popupMenu();
			_Add("LA.csv");
			foreach (var v in filesystem.enumFiles(s_themesDirDefaultBS, "*.csv")) _Add(v.Name);
			
			void _Add(string fn) {
				var s = fn[..^4];
				_Add2(s);
				if (filesystem.exists(s_themesDirCustomizedBS + fn)) _Add2(s + " [customized]");
				
				void _Add2(string s) {
					var v = m.AddRadio(s);
					if (s == t.Name) v.IsChecked = true;
				}
			}
			
			m.Separator();
			m.Submenu("Open folder", m => {
				m["Default themes"] = o => { run.itSafe(s_themesDirDefaultBS); };
				m["Customized themes"] = o => { run.itSafe(s_themesDirCustomizedBS); };
			});
			
			m.Show(owner: owner);
			if (m.Result is { IsChecked: true } r) {
				return new(r.Text);
			}
			return null;
		}
		
		public static bool OptionsApply(TTheme t, bool modified) {
			if (!modified && t.Name == Current.Name) return false;
			
			s_current = t with { };
			if (modified && !s_current.Name.Ends(" [customized]")) s_current.Name += " [customized]";
			
			App.Settings.edit_theme = s_current.Name == "LA" ? null : s_current.Name;
			if (modified) s_current._Save();
			
			foreach (var v in Panels.Editor.OpenDocs) {
				Current.ToScintilla(v);
				v.ESetLineNumberMarginWidth_();
			}
			
			return true;
		}
	}
}

