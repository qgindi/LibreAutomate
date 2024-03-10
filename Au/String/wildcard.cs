using System.Text.RegularExpressions;

namespace Au {
	/// <summary>
	/// Parses and compares [wildcard expression](xref:wildcard_expression).
	/// </summary>
	/// <remarks>
	/// Used in "find" functions. For example in <see cref="wnd.find"/> to compare window name, class name and program.
	/// The "find" function creates a <b>wildex</b> instance (which parses the wildcard expression), then calls <see cref="Match"/> for each item (eg window) to compare some its property text.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// //This version does not support wildcard expressions.
	/// Document Find1(string name, string date) {
	/// 	return Documents.Find(x => x.Name.Eqi(name) && x.Date.Eqi(date));
	/// }
	/// 
	/// //This version supports wildcard expressions.
	/// //null-string arguments are not compared.
	/// Document Find2(string name, string date) {
	/// 	wildex n = name, d = date; //null if the string is null
	/// 	return Documents.Find(x => (n == null || n.Match(x.Name)) && (d == null || d.Match(x.Date)));
	/// }
	/// 
	/// //Example of calling such function.
	/// //Find item whose name is "example" (case-insensitive) and date starts with "2017-".
	/// var item = x.Find2("example", "2017-*");
	/// ]]></code>
	/// </example>
	public class wildex {
		//note: could be struct, but somehow then slower. Slower instance creation, calling methods, in all cases.

		readonly object _o; //string, regexp, Regex or wildex[]. Tested: getting string etc with '_o as string' is fast.
		readonly WXType _type;
		readonly bool _ignoreCase;
		readonly bool _not;

		/// <param name="wildcardExpression">
		/// [Wildcard expression](xref:wildcard_expression).
		/// Cannot be <c>null</c> (throws exception).
		/// <c>""</c> will match <c>""</c>.
		/// </param>
		/// <param name="matchCase">Case-sensitive even if there is no <c>**c</c>.</param>
		/// <param name="noException">If <i>wildcardExpression</i> is invalid, don't throw exception; let <see cref="Match(string)"/> always return <c>false</c>.</param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentException">Invalid <c>"**options "</c> or regular expression.</exception>
		public wildex([ParamString(PSFormat.Wildex)] string wildcardExpression, bool matchCase = false, bool noException = false) {
			Not_.Null(wildcardExpression);
			var w = wildcardExpression;
			try {
				_type = WXType.Wildcard;
				_ignoreCase = !matchCase;
				string[] split = null;

				if (w.Length >= 3 && w[0] == '*' && w[1] == '*') {
					for (int i = 2, j; i < w.Length; i++) {
						switch (w[i]) {
						case 't': _type = WXType.Text; break;
						case 'r': _type = WXType.RegexPcre; break;
						case 'R': _type = WXType.RegexNet; break;
						case 'm': _type = WXType.Multi; break;
						case 'c': _ignoreCase = false; break;
						case 'n': _not = true; break;
						case ' ': w = w[(i + 1)..]; goto g1;
						case '(':
							if (w[i - 1] != 'm') goto ge;
							for (j = ++i; j < w.Length; j++) if (w[j] == ')') break;
							if (j >= w.Length || j == i) goto ge;
							split = new string[] { w[i..j] };
							i = j;
							break;
						default: goto ge;
						}
					}
				ge:
					throw new ArgumentException("Invalid \"**options \" in wildcard expression.");
				g1:
					switch (_type) {
					case WXType.RegexNet:
						var ro = _ignoreCase ? (RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) : RegexOptions.CultureInvariant;
						_o = new Regex(w, ro);
						return;
					case WXType.RegexPcre:
						_o = new regexp(w, _ignoreCase ? RXFlags.CASELESS : 0);
						return;
					case WXType.Multi:
						var a = w.Split(split ?? _splitMulti, StringSplitOptions.None);
						var multi = new wildex[a.Length];
						for (int i = 0; i < a.Length; i++) multi[i] = new wildex(a[i]);
						_o = multi;
						return;
					}
				}

				if (_type == WXType.Wildcard && !hasWildcardChars(w)) _type = WXType.Text;
				_o = w;
			}
			catch when (noException) { _type = WXType.Error; }
		}
		static readonly string[] _splitMulti = { "||" };

		/// <summary>
		/// Creates new <b>wildex</b> from wildcard expression string.
		/// If the string is <c>null</c>, returns <c>null</c>.
		/// </summary>
		/// <param name="wildcardExpression">[Wildcard expression](xref:wildcard_expression). </param>
		/// <exception cref="ArgumentException">Invalid <c>"**options "</c> or regular expression.</exception>
		public static implicit operator wildex([ParamString(PSFormat.Wildex)] string wildcardExpression) {
			if (wildcardExpression == null) return null;
			return new wildex(wildcardExpression);
		}

		//rejected: ReadOnlySpan<char>. Then cannot use eg .NET Regex.

		/// <summary>
		/// Compares a string with the [wildcard expression](xref:wildcard_expression) used to create this <see cref="wildex"/>. Returns <c>true</c> if they match.
		/// </summary>
		/// <param name="s">String. If <c>null</c>, returns <c>false</c>. If <c>""</c>, returns <c>true</c> if it was <c>""</c> or <c>"*"</c> or a regular expression that matches <c>""</c>.</param>
		public bool Match(string s) {
			if (s == null) return false;

			bool R = false;
			switch (_type) {
			case WXType.Wildcard:
				R = s.Like(_o as string, _ignoreCase);
				break;
			case WXType.Text:
				R = s.Eq(_o as string, _ignoreCase);
				break;
			case WXType.RegexPcre:
				R = (_o as regexp).IsMatch(s);
				break;
			case WXType.RegexNet:
				R = (_o as Regex).IsMatch(s);
				break;
			case WXType.Multi:
				var multi = _o as wildex[];
				//[n] parts: all must match (with their option n applied)
				int nNot = 0;
				for (int i = 0; i < multi.Length; i++) {
					var v = multi[i];
					if (v.Not) {
						if (!v.Match(s)) return _not; //!v.Match(s) means 'matches if without option n applied'
						nNot++;
					}
				}
				if (nNot == multi.Length) return !_not; //there are no parts without option n

				//non-[n] parts: at least one must match
				for (int i = 0; i < multi.Length; i++) {
					var v = multi[i];
					if (!v.Not && v.Match(s)) return !_not;
				}
				break;
			default: //Error
				return false;
			}
			return R ^ _not;
		}

		/// <summary>
		/// Returns the text or wildcard string.
		/// <c>null</c> if <b>TextType</b> is not <b>Text</b> or <b>Wildcard</b>.
		/// </summary>
		public string Text => _o as string;

		/// <summary>
		/// Returns the <b>regexp</b> object created from regular expression string.
		/// <c>null</c> if <b>TextType</b> is not <b>RegexPcre</b> (no option <c>r</c>).
		/// </summary>
		public regexp RegexPcre => _o as regexp;

		/// <summary>
		/// Gets the <b>Regex</b> object created from regular expression string.
		/// <c>null</c> if <b>TextType</b> is not <b>RegexNet</b> (no option <c>R</c>).
		/// </summary>
		public Regex RegexNet => _o as Regex;

		/// <summary>
		/// Array of <b>wildex</b> variables, one for each part in multi-part text.
		/// <c>null</c> if <b>TextType</b> is not <b>Multi</b> (no option <c>m</c>).
		/// </summary>
		public wildex[] MultiArray => _o as wildex[];

		/// <summary>
		/// Gets the type of text (wildcard, regex, etc).
		/// </summary>
		public WXType TextType => _type;

		/// <summary>
		/// Is case-insensitive?
		/// </summary>
		public bool IgnoreCase => _ignoreCase;

		/// <summary>
		/// Has option <c>n</c>?
		/// </summary>
		public bool Not => _not;

		///
		public override string ToString() {
			return _o?.ToString();
		}

		/// <summary>
		/// Returns <c>true</c> if string contains wildcard characters: <c>'*'</c>, <c>'?'</c>.
		/// </summary>
		/// <param name="s">Can be <c>null</c>.</param>
		public static bool hasWildcardChars(RStr s) {
			foreach (var c in s) if (c is '*' or '?') return true;
			return false;
		}
	}
}

namespace Au.Types {
	public static unsafe partial class ExtString {
		#region Like

		/// <summary>
		/// Compares this string with a string that possibly contains wildcard characters.
		/// Returns <c>true</c> if the strings match.
		/// </summary>
		/// <param name="t">This string. If <c>null</c>, returns <c>false</c>. If <c>""</c>, returns <c>true</c> if <i>pattern</i> is <c>""</c> or <c>"*"</c>.</param>
		/// <param name="pattern">String that possibly contains wildcard characters. Cannot be <c>null</c>. If <c>""</c>, returns <c>true</c> if this string is <c>""</c>. If <c>"*"</c>, always returns <c>true</c> except when this string is <c>null</c>.</param>
		/// <param name="ignoreCase">Case-insensitive.</param>
		/// <exception cref="ArgumentNullException"><i>pattern</i> is <c>null</c>.</exception>
		/// <remarks>
		/// Wildcard characters:
		/// 
		/// | Character | Will match | Examples
		/// | -
		/// | <c>*</c> | Zero or more of any characters. | <c>"start*"</c>, <c>"*end"</c>, <c>"*middle*"</c>
		/// | <c>?</c> | Any single character. | <c>"date ????-??-??"</c>
		/// 
		/// There are no escape sequences for <c>*</c> and <c>?</c> characters.
		/// 
		/// Uses ordinal comparison, ie does not depend on current culture.
		/// 
		/// See also: [wildcard expression](xref:wildcard_expression).
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// string s = @"C:\abc\mno.xyz";
		/// if(s.Like(@"C:\abc\mno.xyz")) print.it("matches whole text (no wildcard characters)");
		/// if(s.Like(@"C:\abc\*")) print.it("starts with");
		/// if(s.Like(@"*.xyz")) print.it("ends with");
		/// if(s.Like(@"*mno*")) print.it("contains");
		/// if(s.Like(@"C:\*.xyz")) print.it("starts and ends with");
		/// if(s.Like(@"?:*")) print.it("any character, : and possibly more text");
		/// ]]></code>
		/// </example>
		/// <seealso cref="wildex"/>
#if false //somehow speed depends on dll version. With some versions same as C# code, with some slower. Also depends on string. With shortest strings 50% slower.
		public static bool Like(this string t, string pattern, bool ignoreCase = false)
		{
			if(t == null) return false;
			fixed (char* pt = t, pw = pattern)
				return Cpp.Cpp_StringLike(pt, t.Length, pw, pattern.Length, ignoreCase);
		}
#else
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public static bool Like(this string t, string pattern, bool ignoreCase = false) {
			Not_.Null(pattern);
			int patLen = pattern.Length;
			if (t == null) return false;
			if (patLen == 0) return t.Length == 0;
			if (patLen == 1 && pattern[0] == '*') return true;
			if (t.Length == 0) return false;

			fixed (char* str = t, pat = pattern) {
				return _WildcardCmp(str, pat, t.Length, patLen, ignoreCase ? Tables_.LowerCase : null);
			}

			//Microsoft.VisualBasic.CompilerServices.Operators.LikeString() supports more wildcard characters etc. Depends on current culture, has bugs, slower 6-250 times.
			//System.IO.Enumeration.FileSystemName.MatchesSimpleExpression supports \escaping. Slower 2 - 100 times.
		}

		/// <inheritdoc cref="Like(string, string, bool)" path="//summary|//param|//exception"/>
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public static bool Like(this RStr t, string pattern, bool ignoreCase = false) {
			Not_.Null(pattern);
			int patLen = pattern.Length;
			if (patLen == 0) return t.Length == 0;
			if (patLen == 1 && pattern[0] == '*') return true;
			if (t.Length == 0) return false;

			fixed (char* str = t, pat = pattern) {
				return _WildcardCmp(str, pat, t.Length, patLen, ignoreCase ? Tables_.LowerCase : null);
			}

			//Microsoft.VisualBasic.CompilerServices.Operators.LikeString() supports more wildcard characters etc. Depends on current culture, has bugs, slower 6-250 times.
			//System.IO.Enumeration.FileSystemName.MatchesSimpleExpression supports \escaping. Slower 2 - 100 times.
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		static bool _WildcardCmp(char* s, char* w, int lenS, int lenW, char* table) {
			char* se = s + lenS, we = w + lenW;

			//find '*' from start. Makes faster in some cases.
			for (; w < we && s < se; w++, s++) {
				char cS = s[0], cW = w[0];
				if (cW == '*') goto g1;
				if (cW == cS || cW == '?') continue;
				if ((table == null) || (table[cW] != table[cS])) return false;
			}
			if (w == we) return s == se; //w ended?
			goto gr; //s ended
		g1:

			//find '*' from end. Makes "*text" much faster.
			for (; we > w && se > s; we--, se--) {
				char cS = se[-1], cW = we[-1];
				if (cW == '*') break;
				if (cW == cS || cW == '?') continue;
				if ((table == null) || (table[cW] != table[cS])) return false;
			}

			//Algorithm by Alessandro Felice Cantatore, http://xoomer.virgilio.it/acantato/dev/wildcard/wildmatch.html
			//Changes: supports '\0' in string; case-sensitive or not; restructured, in many cases faster.

			int i = 0;
		gStar: //info: goto used because C# compiler makes the loop faster when it contains less code
			w += i + 1;
			if (w == we) return true;
			s += i;

			for (i = 0; s + i < se; i++) {
				char sW = w[i];
				if (sW == '*') goto gStar;
				if (sW == s[i] || sW == '?') continue;
				if ((table != null) && (table[sW] == table[s[i]])) continue;
				s++; i = -1;
			}

			w += i;
		gr:
			while (w < we && *w == '*') w++;
			return w == we;

			//info: Could implement escape sequence ** for * and maybe *? for ?.
			//	But it makes code slower etc.
			//	Not so important.
			//	Most users would not know about it.
			//	Usually can use ? for literal * and ?.
			//	Usually can use regular expression if need such precision.
			//	Then cannot use "**options " for wildcard expressions.
			//	Could use other escape sequences, eg [*], [?] and [[], but it makes slower and is more harmful than useful.

			//The first two loops are fast, but Eq much faster when !ignoreCase. We cannot use such optimizations that it can.
			//The slowest case is "*substring*", because then the first two loops don't help.
			//	Then similar speed as string.IndexOf(ordinal) and API <msdn>FindStringOrdinal</msdn>.
			//	Possible optimization, but need to add much code, and makes not much faster, and makes other cases slower, difficult to avoid it.
		}
#endif

		/// <summary>
		/// Calls <see cref="Like(string, string, bool)"/> for each wildcard pattern specified in the argument list until it returns <c>true</c>.
		/// Returns 1-based index of the matching pattern, or 0 if none.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="ignoreCase">Case-insensitive.</param>
		/// <param name="patterns">One or more wildcard strings. The array and strings cannot be <c>null</c>.</param>
		/// <exception cref="ArgumentNullException">A string in <i>patterns</i> is <c>null</c>.</exception>
		public static int Like(this string t, bool ignoreCase = false, params string[] patterns) {
			for (int i = 0; i < patterns.Length; i++) if (t.Like(patterns[i], ignoreCase)) return i + 1;
			return 0;
		}

		#endregion Like
	}

	//rejected: struct WildexStruct - struct version of wildex class. Moved to the Unused project.
	//	Does not make faster, although in most cases creates less garbage.

	/// <summary>
	/// The type of text (wildcard expression) of a <see cref="wildex"/> variable.
	/// </summary>
	public enum WXType : byte {
		/// <summary>
		/// Simple text (option <c>t</c>, or no <c>*?</c> characters and no <c>t</c> <c>r</c> <c>R</c> options).
		/// </summary>
		Text,

		/// <summary>
		/// Wildcard (has <c>*?</c> characters and no <c>t</c> <c>r</c> <c>R</c> options).
		/// <b>Match</b> calls <see cref="ExtString.Like(string, string, bool)"/>.
		/// </summary>
		Wildcard,

		/// <summary>
		/// PCRE regular expression (option <c>r</c>).
		/// <b>Match</b> calls <see cref="regexp.IsMatch"/>.
		/// </summary>
		RegexPcre,

		/// <summary>
		/// .NET regular expression (option <c>R</c>).
		/// <b>Match</b> calls <see cref="Regex.IsMatch(string)"/>.
		/// </summary>
		RegexNet,

		/// <summary>
		/// Multiple parts (option <c>m</c>).
		/// <b>Match</b> calls <b>Match</b> for each part (see <see cref="wildex.MultiArray"/>) and returns <c>true</c> if all negative (option <c>n</c>) parts return <c>true</c> (or there are no such parts) and some positive (no option <c>n</c>) part returns <c>true</c> (or there are no such parts).
		/// If you want to implement a different logic, call <b>Match</b> for each <see cref="wildex.MultiArray"/> element (instead of calling <b>Match</b> for this variable).
		/// </summary>
		Multi,

		/// <summary>
		/// The regular expression was invalid, and parameter <i>noException</i> <c>true</c>.
		/// </summary>
		Error,
	}
}
