
namespace Au.Controls
{
	using static Sci;

	public unsafe partial class KScintilla
	{
		#region styles

		public void zStyleFont(int style, string name) {
			zSetString(SCI_STYLESETFONT, style, name);
		}

		//public string zStyleFont(int style)
		//{
		//	return zGetString(SCI_STYLEGETFONT, style, 100);
		//}

		public void zStyleFont(int style, string name, int size) {
			zStyleFont(style, name);
			zStyleFontSize(style, size);
		}

		/// <remarks>Uses only font name and size. Not style etc.</remarks>
		public void zStyleFont(int style, System.Windows.Controls.Control c) {
			zStyleFont(style, c.FontFamily.ToString(), c.FontSize.ToInt() * 72 / 96);
		}

		/// <remarks>Segoe UI, 9.</remarks>
		public void zStyleFont(int style) {
			zStyleFont(style, "Segoe UI", 9);
		}

		public void zStyleFontSize(int style, int value) {
			Call(SCI_STYLESETSIZE, style, value);
		}

		//public int zStyleFontSize(int style)
		//{
		//	return Call(SCI_STYLEGETSIZE, style);
		//}

		public void zStyleHidden(int style, bool value) {
			Call(SCI_STYLESETVISIBLE, style, !value);
		}

		//public bool zStyleHidden(int style)
		//{
		//	return 0 == Call(SCI_STYLEGETVISIBLE, style);
		//}

		public void zStyleBold(int style, bool value) {
			Call(SCI_STYLESETBOLD, style, value);
		}

		public void zStyleItalic(int style, bool value) {
			Call(SCI_STYLESETITALIC, style, value);
		}

		public void zStyleUnderline(int style, bool value) {
			Call(SCI_STYLESETUNDERLINE, style, value);
		}

		public void zStyleEolFilled(int style, bool value) {
			Call(SCI_STYLESETEOLFILLED, style, value);
		}

		public void zStyleHotspot(int style, bool value) {
			Call(SCI_STYLESETHOTSPOT, style, value);
		}

		public bool zStyleHotspot(int style) {
			return 0 != Call(SCI_STYLEGETHOTSPOT, style);
		}

		public void zStyleForeColor(int style, ColorInt color) {
			Call(SCI_STYLESETFORE, style, color.ToBGR());
		}

		public void zStyleBackColor(int style, ColorInt color) {
			Call(SCI_STYLESETBACK, style, color.ToBGR());
		}

		/// <summary>
		/// Measures string width.
		/// </summary>
		public int zStyleMeasureStringWidth(int style, string s) {
			return zSetString(SCI_TEXTWIDTH, style, s);
		}

		/// <summary>
		/// Calls SCI_STYLECLEARALL, which sets all styles to be the same as STYLE_DEFAULT.
		/// Then also sets some special styles, eg STYLE_HIDDEN and hotspot color.
		/// </summary>
		/// <param name="belowDefault">Clear only styles 0..STYLE_DEFAULT.</param>
		public void zStyleClearAll(bool belowDefault = false) {
			if (belowDefault) zStyleClearRange(0, STYLE_DEFAULT);
			else Call(SCI_STYLECLEARALL);
			zStyleHidden(STYLE_HIDDEN, true);
			Call(SCI_SETHOTSPOTACTIVEFORE, true, 0xFF0080); //inactive 0x0080FF

			//STYLE_HOTSPOT currently unused
			//zStyleHotspot(STYLE_HOTSPOT, true);
			//zStyleForeColor(STYLE_HOTSPOT, 0xFF8000);
		}

		/// <summary>
		/// Calls SCI_STYLECLEARALL(styleFrom, styleToNotIncluding), which sets range of styles to be the same as STYLE_DEFAULT.
		/// If styleToNotIncluding is 0, clears all starting from styleFrom.
		/// </summary>
		public void zStyleClearRange(int styleFrom, int styleToNotIncluding = 0) {
			Call(SCI_STYLECLEARALL, styleFrom, styleToNotIncluding);
		}

		/// <summary>
		/// Gets style at position.
		/// Uses SCI_GETSTYLEAT.
		/// Returns 0 if pos is invalid.
		/// </summary>
		public int zGetStyleAt(int pos) {
			return Call(SCI_GETSTYLEAT, pos);
		}

		#endregion

		#region spec styles

		//these SCI_ are deprecated. Instead use 
		//public void zSelectionForeColor(bool use, ColorInt color) {
		//	Call(SCI_SETSELFORE, use, color.ToBGR());
		//}

		//public void zSelectionBackColor(bool use, ColorInt color, int alpha = 256) {
		//	Call(SCI_SETSELBACK, use, color.ToBGR());
		//	Call(SCI_SETSELALPHA, alpha);
		//}

		//public void zCaretLineColor(bool use, ColorInt color, int alpha = 256) {
		//	Call(SCI_SETCARETLINEBACK, color.ToBGR());
		//	Call(SCI_SETCARETLINEBACKALPHA, alpha);
		//	Call(SCI_SETCARETLINEVISIBLE, use);
		//}

		//we set it in ctor and don't need to change
		//public void zCaretWidth(int value)
		//{
		//	Call(SCI_SETCARETWIDTH, value);
		//}

		#endregion

		#region margins

		public void zSetMarginType(int margin, int SC_MARGIN_) {
			Call(SCI_SETMARGINTYPEN, margin, SC_MARGIN_);
		}

		internal int[] _marginDpi;

		public void zSetMarginWidth(int margin, int value, bool dpiScale = true, bool chars = false) {
			if (dpiScale && value > 0) {
				var a = _marginDpi ??= new int[Call(SCI_GETMARGINS)];
				if (chars) {
					value *= zStyleMeasureStringWidth(STYLE_LINENUMBER, "8");
					a[margin] = Dpi.Unscale(value, _dpi).ToInt();
				} else {
					a[margin] = value;
					value = Dpi.Scale(value, _dpi);
				}
			} else {
				var a = _marginDpi;
				if (a != null) a[margin] = 0;
			}
			Call(SCI_SETMARGINWIDTHN, margin, value);
		}

		//public void zSetMarginWidth(int margin, string textToMeasureWidth) {
		//	int n = zStyleMeasureStringWidth(STYLE_LINENUMBER, textToMeasureWidth);
		//	Call(SCI_SETMARGINWIDTHN, margin, n + 4);
		//}

		//not used
		//public int zGetMarginWidth(int margin, bool dpiUnscale) {
		//	int R = Call(SCI_GETMARGINWIDTHN, margin);
		//	if (dpiUnscale && R > 0) {
		//		var a = _marginDpi;
		//		var v = a?[margin] ?? 0;
		//		if (v > 0) R = v;
		//	}
		//	return R;
		//}

		internal void zMarginWidthsDpiChanged_() {
			var a = _marginDpi; if (a == null) return;
			for (int i = a.Length; --i >= 0;) {
				if (a[i] > 0) Call(SCI_SETMARGINWIDTHN, i, Dpi.Scale(a[i], _dpi));
			}
		}

		public int zMarginFromPoint(POINT p, bool screenCoord) {
			if (screenCoord) _w.MapScreenToClient(ref p);
			if (_w.ClientRect.Contains(p)) {
				for (int i = 0, n = Call(SCI_GETMARGINS), w = 0; i < n; i++) { w += Call(SCI_GETMARGINWIDTHN, i); if (w >= p.x) return i; }
			}
			return -1;
		}

		/// <summary>
		/// SCI_GETMARGINWIDTHN. Not DPI-scaled.
		/// </summary>
		public (int left, int right) zGetMarginX(int margin) {
			int x = 0;
			for (int i = 0; i < margin; i++) x += Call(SCI_GETMARGINWIDTHN, i);
			return (x, x + Call(SCI_GETMARGINWIDTHN, margin));
		}

		#endregion

		#region lexer

		/// <summary>
		/// Sets lexer for a language.
		/// Does not set keywords, options etc. Just creates (or gets cached) and sets lexer object, and clears styles 0-31.
		/// For C# instead use <see cref="zSetLexerCsharp"/> (it sets keywords etc).
		/// Does not check whether already using that lexer.
		/// </summary>
		/// <param name="lang">Language. See SCI_GETLEXERLANGUAGE doc. Use null for no lexer.</param>
		/// <returns>false if <i>lang</i> unknown.</returns>
		public bool zSetLexer(string lang) {
			nint il = 0;
			if (lang != null) {
				//note: cannot cache lexer objects. It seems Scintilla deletes old object, eg when sets new; with old object crashes. Not too slow.
				il = Sci_CreateLexer(Convert2.Utf8Encode(lang)); //FUTURE: test other languages
				if (il == 0) { print.it("There is no lexer for " + lang); return false; }
			}
			zStyleClearRange(0, STYLE_HIDDEN); //STYLE_DEFAULT - 1
			Call(SCI_SETILEXER, 0, il);
			return true;
		}

		public void zSetLexerCsharp(/*ColorInt? codeBackColor = null*/) {
			//There is no C# lexer, but can use the "cpp" lexer.
			//	Use separate lexer objects for C# and other C-like languages, because keywords and options etc are different.
			zSetLexer("cpp");

			//if (codeBackColor != null) for (int i = 0; i < STYLE_DEFAULT; i++) zStyleBackColor(i, codeBackColor.Value);

			const int colorComment = 0x60A000;
			const int colorString = 0xA07040;
			const int colorNumber = 0x804000;
			const int colorDoc = 0x606060;
			zStyleForeColor((int)LexCppStyles.SCE_C_COMMENT, colorComment); //  /*...*/
			zStyleForeColor((int)LexCppStyles.SCE_C_COMMENTLINE, colorComment); //  //...
			zStyleForeColor((int)LexCppStyles.SCE_C_COMMENTLINEDOC, colorDoc); //  ///...
			zStyleForeColor((int)LexCppStyles.SCE_C_COMMENTDOC, colorDoc); //  /**...*/
			zStyleForeColor((int)LexCppStyles.SCE_C_CHARACTER, colorNumber);
			zStyleForeColor((int)LexCppStyles.SCE_C_NUMBER, colorNumber);
			zStyleForeColor((int)LexCppStyles.SCE_C_STRING, colorString);
			zStyleForeColor((int)LexCppStyles.SCE_C_VERBATIM, colorString); //@"string"
			zStyleForeColor((int)LexCppStyles.SCE_C_ESCAPESEQUENCE, 0xB776FB);
			//zStyleForeColor((int)LexCppStyles.SCE_C_OPERATOR, 0x80); //+,;( etc. Let it be black.
			zStyleForeColor((int)LexCppStyles.SCE_C_PREPROCESSOR, 0xFF8000);
			zStyleForeColor((int)LexCppStyles.SCE_C_WORD, 0xFF); //keywords
			zStyleForeColor((int)LexCppStyles.SCE_C_TASKMARKER, 0x00C000); zStyleBackColor((int)LexCppStyles.SCE_C_TASKMARKER, 0);
			//zStyleForeColor((int)LexCppStyles.SCE_C_WORD2, 0x80F0); //functions. Not using here.
			//zStyleForeColor((int)LexCppStyles.SCE_C_GLOBALCLASS, 0xC000C0); //types. Not using here.

			//zStyleForeColor((int)LexCppStyles.SCE_C_USERLITERAL, ); //C++, like 10_km
			//zStyleForeColor((int)LexCppStyles.SCE_C_STRINGRAW, ); //R"string"
			//zStyleForeColor((int)LexCppStyles.SCE_C_COMMENTDOCKEYWORD, ); //supports only JavaDoc and Doxygen
			//zStyleForeColor((int)LexCppStyles.SCE_C_PREPROCESSORCOMMENT, ); //?
			//zStyleForeColor((int)LexCppStyles.SCE_C_PREPROCESSORCOMMENTDOC, ); //?

			zSetStringString(SCI_SETPROPERTY, "styling.within.preprocessor\0" + "1");
			zSetStringString(SCI_SETPROPERTY, "lexer.cpp.allow.dollars\0" + "0");
			zSetStringString(SCI_SETPROPERTY, "lexer.cpp.track.preprocessor\0" + "0"); //default 1
			zSetStringString(SCI_SETPROPERTY, "lexer.cpp.escape.sequence\0" + "1");
			//zSetStringString(SCI_SETPROPERTY, "lexer.cpp.verbatim.strings.allow.escapes\0" + "1"); //expected to style "", but it does nothing


			//print.it(zGetString(SCI_DESCRIBEKEYWORDSETS, 0, -1));
			//Primary keywords and identifiers
			//Secondary keywords and identifiers
			//Documentation comment keywords
			//Global classes and typedefs
			//Preprocessor definitions
			//Task marker and error marker keywords
			zSetString(SCI_SETKEYWORDS, 0, "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while add alias ascending async await by descending dynamic equals from get global group into join let nameof on orderby partial remove select set value var when where yield unmanaged notnull record init nint nuint not and or");
			//zSetString(SCI_SETKEYWORDS, 1, "Function"); //functions. Not using here.
			//zSetString(SCI_SETKEYWORDS, 2, "summary <summary>"); //supports only JavaDoc and Doxygen
			//zSetString(SCI_SETKEYWORDS, 3, "Au"); //types. Not using here.
			//zSetString(SCI_SETKEYWORDS, 4, "DEBUG TRACE"); //if used with #if, lexer knows which #if/#else branch to style. Not using here (see "lexer.cpp.track.preprocessor").
			zSetString(SCI_SETKEYWORDS, 5, "TO" + "DO SHOULD" + "DO CON" + "SIDER FU" + "TURE B" + "UG HA" + "CK");
		}

		#endregion
	}
}
