namespace Au.Types;

/// <summary>
/// Adds extension methods for <see cref="String"/>.
/// </summary>
/// <remarks>
/// Some .NET <see cref="String"/> methods use <see cref="StringComparison.CurrentCulture"/> by default, while others use ordinal or invariant comparison. It is confusing (difficult to remember), dangerous (easy to make bugs), slower and rarely useful.
/// Microsoft recommends to specify <b>StringComparison.Ordinal[IgnoreCase]</b> explicitly. See https://msdn.microsoft.com/en-us/library/ms973919.aspx.
/// This class adds ordinal comparison versions of these methods. Same or similar name, for example <b>Ends</b> for <b>EndsWith</b>.
/// See also <see cref="process.thisProcessCultureIsInvariant"/>.
/// 
/// This class also adds more methods.
/// You also can find string functions in other classes of this library, including <see cref="StringUtil"/>, <see cref="regexp"/>, <see cref="pathname"/>, <see cref="csvTable"/>, <see cref="keys.more"/>, <see cref="Convert2"/>, <see cref="Hash"/>.
/// </remarks>
public static unsafe partial class ExtString {
	/// <summary>
	/// Compares this and other string. Returns true if equal.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="s">Other string. Can be null.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	/// <seealso cref="Eq(RStr, RStr)"/>
	/// <seealso cref="Eqi(RStr, RStr)"/>
	/// <seealso cref="string.Compare"/>
	/// <seealso cref="string.CompareOrdinal"/>
	public static bool Eq(this string t, string s, bool ignoreCase = false) {
		return ignoreCase ? string.Equals(t, s, StringComparison.OrdinalIgnoreCase) : string.Equals(t, s);
	}

	/// <summary>
	/// Compares this strings with multiple strings.
	/// Returns 1-based index of matching string, or 0 if none.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="strings">Other strings. Strings can be null.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Eq(this string t, bool ignoreCase, params string[] strings) {
		for (int i = 0; i < strings.Length; i++) if (Eq(t, strings[i], ignoreCase)) return i + 1;
		return 0;
	}

	/// <summary>
	/// Compares part of this string with other string. Returns true if equal.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="startIndex">Offset in this string. If invalid, returns false.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	/// <seealso cref="Eq(RStr, int, RStr, bool)"/>
	/// <seealso cref="string.Compare"/>
	/// <seealso cref="string.CompareOrdinal"/>
	public static bool Eq(this string t, int startIndex, RStr s, bool ignoreCase = false) {
		int nt = t.Length, ns = s.LengthThrowIfNull_();
		if ((uint)startIndex > nt || ns > nt - startIndex) return false;
		var span = t.AsSpan(startIndex, ns);
		if (!ignoreCase) return span.SequenceEqual(s);
		return span.Equals(s, StringComparison.OrdinalIgnoreCase);
		//Faster than string.Compare[Ordinal].
		//With Tables_.LowerCase similar speed. Depends on whether match. 
	}

	/// <summary>
	/// Compares part of this string with multiple strings.
	/// Returns 1-based index of matching string, or 0 if none.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="startIndex">Offset in this string. If invalid, returns false.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="strings">Other strings.</param>
	/// <exception cref="ArgumentNullException">A string in <i>strings</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Eq(this string t, int startIndex, bool ignoreCase = false, params string[] strings) {
		for (int i = 0; i < strings.Length; i++) if (t.Eq(startIndex, strings[i], ignoreCase)) return i + 1;
		return 0;
	}

	/// <summary>
	/// Compares part of this string with other string. Returns true if equal.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="range">Range of this string. Can return true only if its length == s.Length. If invalid, returns false.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	/// <seealso cref="Eq(RStr, RStr)"/>
	/// <seealso cref="Eqi(RStr, RStr)"/>
	/// <seealso cref="string.Compare"/>
	/// <seealso cref="string.CompareOrdinal"/>
	public static bool Eq(this string t, Range range, RStr s, bool ignoreCase = false) {
		int nt = t.Length, ns = s.LengthThrowIfNull_();
		int i = range.Start.GetOffset(nt), len = range.End.GetOffset(nt) - i;
		return ns == len && t.Eq(i, s, ignoreCase);
	}

	/// <summary>
	/// Returns true if the specified character is at the specified position in this string.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="index">Offset in this string. If invalid, returns false.</param>
	/// <param name="c">Character.</param>
	public static bool Eq(this string t, int index, char c) {
		if ((uint)index >= t.Length) return false;
		return t[index] == c;
	}

	/// <summary>
	/// Compares this and other string ignoring case (case-insensitive). Returns true if equal.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="s">Other string. Can be null.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Eqi(this string t, string s) => string.Equals(t, s, StringComparison.OrdinalIgnoreCase);

	//rejected. Not so often used.
	//public static bool Eqi(this string t, int startIndex, string s) => Eq(t, startIndex, s, true);

	/// <summary>
	/// Compares end of this string with other string. Returns true if equal.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Ends(this string t, RStr s, bool ignoreCase = false) {
		int nt = t.Length, ns = s.LengthThrowIfNull_();
		if (ns > nt) return false;
		var span = t.AsSpan(nt - ns);
		if (!ignoreCase) return span.SequenceEqual(s);
		return span.Equals(s, StringComparison.OrdinalIgnoreCase);
		//faster than EndsWith
	}

	/// <summary>
	/// Compares end of this string with multiple strings.
	/// Returns 1-based index of matching string, or 0 if none.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="strings">Other strings.</param>
	/// <exception cref="ArgumentNullException">A string in <i>strings</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Ends(this string t, bool ignoreCase, params string[] strings) {
		for (int i = 0; i < strings.Length; i++) if (Ends(t, strings[i], ignoreCase)) return i + 1;
		return 0;
	}

	/// <summary>
	/// Returns true if this string ends with the specified character.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="c">Character.</param>
	public static bool Ends(this string t, char c) {
		int i = t.Length - 1;
		return i >= 0 && t[i] == c;
	}

	/// <summary>
	/// Compares beginning of this string with other string. Returns true if equal.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Starts(this string t, RStr s, bool ignoreCase = false) {
		int nt = t.Length, ns = s.LengthThrowIfNull_();
		if (ns > nt) return false;
		var span = t.AsSpan(0, ns);
		if (!ignoreCase) return span.SequenceEqual(s);
		return span.Equals(s, StringComparison.OrdinalIgnoreCase);
		//faster than StartsWith
	}

	/// <summary>
	/// Compares beginning of this string with multiple strings.
	/// Returns 1-based index of matching string, or 0 if none.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="strings">Other strings.</param>
	/// <exception cref="ArgumentNullException">A string in <i>strings</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Starts(this string t, bool ignoreCase, params string[] strings) {
		for (int i = 0; i < strings.Length; i++) if (Starts(t, strings[i], ignoreCase)) return i + 1;
		return 0;
	}

	/// <summary>
	/// Returns true if this string starts with the specified character.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="c">Character.</param>
	public static bool Starts(this string t, char c) {
		return t.Length > 0 && t[0] == c;
	}

	//Speed test results with text of length 5_260_070 and 'find' text "inheritdoc":
	//IndexOf(Ordinal)							6 ms (depends on 'find' text; can be much faster if starts with a rare character)
	//IndexOf(OrdinalIgnoreCase)				32 ms
	//FindStringOrdinal()						8 ms
	//FindStringOrdinal(true)					32 ms
	//Like("*" + x + "*")						10 ms
	//Like("*" + x + "*", true)					12 ms
	//RxIsMatch(LITERAL)						13 ms
	//RxIsMatch(LITERAL|CASELESS)			19 ms
	//Regex.Match(CultureInvariant)				4 ms (when no regex-special characters or if escaped)
	//Regex.Match(CultureInvariant|IgnoreCase)	9 ms
	//Find2(true)								10 ms

	//Could optimize the case-insensitive Find.
	//	Either use table (like Like), or for very long strings use Regex.
	//	But maybe then result would be different in some cases, not sure.
	//	How if contains Unicode surrogates?
	//	Bad: slower startup, because need to create table or JIT Regex.
	//	Never mind.

	//public static int Find2(this string t, string s, bool ignoreCase = false) {
	//	if (!ignoreCase) return t.IndexOf(s, StringComparison.Ordinal);
	//	int n = t.Length - s.Length;
	//	if (n >= 0) {
	//		if (s.Length == 0) return 0;
	//		var m = Tables_.LowerCase;
	//		char first = m[s[0]];
	//		for (int i = 0; i <= n; i++) {
	//			if (m[t[i]] == first) {
	//				int j = 1; while (j < s.Length && m[t[i + j]] == m[s[j]]) j++;
	//				if (j == s.Length) return i;
	//			}
	//		}
	//	}
	//	return -1;
	//}

	/// <summary>
	/// Finds substring in this string. Returns its 0-based index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Subtring to find.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Find(this string t, string s, bool ignoreCase = false) {
		return t.IndexOf(s, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
	}

	/// <summary>
	/// Finds substring in part of this string. Returns its 0-based index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Subtring to find.</param>
	/// <param name="startIndex">The search start index.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>startIndex</i>.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Find(this string t, string s, int startIndex, bool ignoreCase = false) {
		return t.IndexOf(s, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
	}

	/// <summary>
	/// Finds substring in part of this string. Returns its 0-based index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Subtring to find.</param>
	/// <param name="range">The search range.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Find(this string t, string s, Range range, bool ignoreCase = false) {
		var (start, count) = range.GetOffsetAndLength(t.Length);
		return t.IndexOf(s, start, count, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
	}

	/// <summary>
	/// Finds the first character specified in <i>chars</i>. Returns its index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters.</param>
	/// <param name="range">The search range.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)] //functions with Range parameter are very slow until fully optimized
	public static int FindAny(this string t, string chars, Range? range = null) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		int r = t.AsSpan(start, len).IndexOfAny(chars);
		return r < 0 ? r : r + start;
	}

	/// <summary>
	/// Finds the first character not specified in <i>chars</i>. Returns its index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters.</param>
	/// <param name="range">The search range.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	public static int FindNot(this string t, string chars, Range? range = null) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		int r = t.AsSpan(start, len).IndexOfNot(chars);
		return r < 0 ? r : r + start;
	}

	/// <summary>
	/// Finds the first character not specified in <i>chars</i>. Returns its index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	public static int IndexOfNot(this RStr t, string chars!!) {
		for (int i = 0; i < t.Length; i++) {
			char c = t[i];
			for (int j = 0; j < chars.Length; j++) if (chars[j] == c) goto g1;
			return i;
		g1:;
		}
		return -1;
	}

	/// <summary>
	/// Finds the last character not specified in <i>chars</i>. Returns its index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	public static int LastIndexOfNot(this RStr t, string chars!!) {
		for (int i = t.Length; --i >= 0;) {
			char c = t[i];
			for (int j = 0; j < chars.Length; j++) if (chars[j] == c) goto g1;
			return i;
		g1:;
		}
		return -1;
	}

	/// <summary>
	/// Finds the last character specified in <i>chars</i> (searches right to left). Returns its index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters.</param>
	/// <param name="range">The search range.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	public static int FindLastAny(this string t, string chars, Range? range = null) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		int r = t.AsSpan(start, len).LastIndexOfAny(chars);
		return r < 0 ? r : r + start;
	}

	/// <summary>
	/// Finds the last character not specified in <i>chars</i> (searches right to left). Returns its index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters.</param>
	/// <param name="range">The search range.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	public static int FindLastNot(this string t, string chars, Range? range = null) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		int r = t.AsSpan(start, len).LastIndexOfNot(chars);
		return r < 0 ? r : r + start;
	}

	/// <summary>
	/// Removes specified characters from the start and end of this string.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters to remove.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	public static string Trim(this string t, string chars) {
		var span = t.AsSpan().Trim(chars);
		return span.Length == t.Length ? t : new string(span);
	}

	/// <summary>
	/// Removes specified characters from the start of this string.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters to remove.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	public static string TrimStart(this string t, string chars) {
		var span = t.AsSpan().TrimStart(chars);
		return span.Length == t.Length ? t : new string(span);
	}

	/// <summary>
	/// Removes specified characters from the end of this string.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters to remove.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is null.</exception>
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	public static string TrimEnd(this string t, string chars) {
		var span = t.AsSpan().TrimEnd(chars);
		return span.Length == t.Length ? t : new string(span);
	}

	/// <summary>
	/// Finds whole word. Returns its 0-based index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Subtring to find.</param>
	/// <param name="range">The search range.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="otherWordChars">Additional word characters, for which <see cref="char.IsLetterOrDigit"/> returns false. For example "_".</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <remarks>
	/// If <i>s</i> starts with a word character finds substring that is not preceded by a word character.
	/// If <i>s</i> ends with a word character, finds substring that is not followed by a word character.
	/// Word characters are those for which <see cref="char.IsLetterOrDigit"/> returns true plus those specified in <i>otherWordChars</i>.
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int FindWord(this string t, string s!!, Range? range = null, bool ignoreCase = false, string otherWordChars = null) {
		var (start, end) = range.GetStartEnd(t.Length);
		int lens = s.Length;
		if (lens == 0) return 0; //like IndexOf and Find

		bool wordStart = _IsWordChar(s, 0, false, otherWordChars),
			wordEnd = _IsWordChar(s, lens - 1, true, otherWordChars);

		for (int i = start, iMax = end - lens; i <= iMax; i++) {
			i = t.IndexOf(s, i, end - i, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
			if (i < 0) break;
			if (wordStart && i > 0 && _IsWordChar(t, i - 1, true, otherWordChars)) continue;
			if (wordEnd && i < iMax && _IsWordChar(t, i + lens, false, otherWordChars)) continue;
			return i;
		}
		return -1;

		static bool _IsWordChar(string s, int i, bool expandLeft, string otherWordChars) {
			//SHOULDDO: use Rune
			char c = s[i];
			if (c >= '\uD800' && c <= '\uDFFF') { //Unicode surrogates
				if (expandLeft) {
					if (char.IsLowSurrogate(s[i])) return i > 0 && char.IsHighSurrogate(s[i - 1]) && char.IsLetterOrDigit(s, i - 1);
				} else {
					if (char.IsHighSurrogate(s[i])) return i < s.Length - 1 && char.IsLowSurrogate(s[i + 1]) && char.IsLetterOrDigit(s, i);
				}
			} else {
				if (char.IsLetterOrDigit(c)) return true;
				if (otherWordChars?.Contains(c) ?? false) return true;
			}
			return false;
		}
	}

	/// <summary>
	/// Returns <see cref="string.Length"/>. Returns 0 if this string is null.
	/// </summary>
	/// <param name="t">This string.</param>
	[DebuggerStepThrough]
	public static int Lenn(this string t) => t?.Length ?? 0;

	/// <summary>
	/// Returns true if this string is null or empty ("").
	/// </summary>
	/// <param name="t">This string.</param>
	[DebuggerStepThrough]
	public static bool NE(this string t) => t == null || t.Length == 0;

	/// <summary>
	/// Returns this string, or null if it is "" or null.
	/// </summary>
	/// <param name="t">This string.</param>
	[DebuggerStepThrough]
	internal static string NullIfEmpty_(this string t) => t.NE() ? null : t;
	//not public because probably too rarely used.

	/// <summary>
	/// This function can be used with foreach to split this string into substrings as start/end offsets.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="separators">Characters that delimit the substrings. Or one of <see cref="SegSep"/> constants.</param>
	/// <param name="flags"></param>
	/// <param name="range">Part of this string to split.</param>
	/// <example>
	/// <code><![CDATA[
	/// string s = "one * two three ";
	/// foreach(var t in s.Segments(" ")) print.it(s[t.start..t.end]);
	/// foreach(var t in s.Segments(SegSep.Word, SegFlags.NoEmpty)) print.it(s[t.start..t.end]);
	/// ]]></code>
	/// </example>
	/// <seealso cref="Lines(string, Range, bool)"/>
	public static SegParser Segments(this string t, string separators, SegFlags flags = 0, Range? range = null) {
		return new SegParser(t, separators, flags, range);
	}

	//rejected. Usually Split is good. In .NET Core+ it also has option to trim spaces and in most cases is faster.
	///// <summary>
	///// Splits this string into substrings using the specified separators.
	///// </summary>
	///// <param name="t">This string.</param>
	///// <param name="separators">A string containing characters that delimit substrings. Or one of <see cref="SegSep"/> constants.</param>
	///// <param name="maxCount">The maximal number of substrings to get. If negative, gets all. Else if there are more substrings, the last element will contain single substring, unlike with <see cref="String.Split"/>.</param>
	///// <param name="flags"></param>
	///// <param name="range">Part of this string to split.</param>
	///// <seealso cref="Segments"/>
	///// <seealso cref="Lines"/>
	///// <seealso cref="SegParser"/>
	///// <seealso cref="string.Split"/>
	///// <seealso cref="string.Join"/>
	//public static string[] SegSplit(this string t, string separators, SegFlags flags = 0, int maxCount = -1, Range? range = null) {
	//	var x = new SegParser(t, separators, flags, range);
	//	return x.ToStringArray(maxCount);
	//}

	//rejected. 30% slower than the fast Lines() overload. The slow overload rarely used. Range rarely used; can use SegSplit instead.
	///// <summary>
	///// Splits this string into lines using separators "\r\n", "\n", "\r".
	///// </summary>
	///// <param name="t">This string.</param>
	///// <param name="noEmptyLines">Don't need empty lines.</param>
	///// <param name="maxCount">The maximal number of substrings to get. If negative, gets all. Else if there are more lines, the last element will contain single line, unlike with <see cref="String.Split"/></param>
	///// <param name="range">Part of this string to split.</param>
	///// <seealso cref="Segments"/>
	///// <seealso cref="SegSep.Line"/>
	//public static string[] SegLines(this string t, bool noEmptyLines = false, int maxCount = -1, Range? range = null) {
	//	return SegSplit(t, SegSep.Line, noEmptyLines ? SegFlags.NoEmpty : 0, maxCount, range);
	//}

	/// <summary>
	/// Splits this string using newline separators "\r\n", "\n", "\r".
	/// </summary>
	/// <returns>Array containing lines as strings. Does not include the last empty line.</returns>
	/// <param name="t">This string.</param>
	/// <param name="noEmpty">Don't need empty lines.</param>
	/// <seealso cref="StringReader.ReadLine"/>
	[SkipLocalsInit]
	public static string[] Lines(this string t, bool noEmpty = false) {
		//this code is similar to StringReader.ReadLine, but much faster. Faster than string.Split(char).
		using var f = new FastBuffer<StartEnd>();
		int n = 0;
		for (int pos = 0; pos < t.Length;) {
			if (n == f.n) f.More(preserve: true);
			var span = t.AsSpan(pos);
			int len = span.IndexOfAny('\r', '\n');
			if (len < 0) {
				f[n++] = new(pos, t.Length);
				break;
			} else {
				if (!(noEmpty && len == 0)) f[n++] = new(pos, pos + len);
				pos += len + 1;
				if (span[len] == '\r' && pos < t.Length && t[pos] == '\n') pos++;
			}
		}
		var a = new string[n];
		for (int i = 0; i < n; i++) {
			var v = f[i];
			a[i] = t[v.start..v.end];
		}
		return a;
	}

	/// <summary>
	/// Splits this string or its range using newline separators "\r\n", "\n", "\r". Gets start/end offsets of lines.
	/// </summary>
	/// <returns>Array containing start/end offsets of lines in the string (not in the range). Does not include the last empty line.</returns>
	/// <param name="t">This string.</param>
	/// <param name="range">Range of this string. Example: <c>var a = s.Lines(..); //split entire string</c>.</param>
	/// <param name="noEmpty">Don't need empty lines.</param>
	/// <seealso cref="Lines(ReadOnlySpan{char}, bool)"/>
	public static StartEnd[] Lines(this string t, Range range, bool noEmpty = false) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		var a = t.AsSpan(start, len).Lines(noEmpty);
		if (start != 0) {
			for (int i = 0; i < a.Length; i++) {
				a[i].start += start;
				a[i].end += start;
			}
		}
		return a;
	}

	/// <summary>
	/// Splits this string using newline separators "\r\n", "\n", "\r". Gets start/end offsets of lines.
	/// </summary>
	/// <returns>Array containing start/end offsets of lines. Does not include the last empty line.</returns>
	/// <param name="t">This string.</param>
	/// <param name="noEmpty">Don't need empty lines.</param>
	[SkipLocalsInit]
	public static StartEnd[] Lines(this RStr t, bool noEmpty = false) {
		using var f = new FastBuffer<StartEnd>();

		//this code is like in Lines. Tried to move it to a function, but then somehow very slow.
		int n = 0;
		for (int pos = 0; pos < t.Length;) {
			if (n == f.n) f.More(preserve: true);
			var span = t[pos..];
			int len = span.IndexOfAny('\r', '\n');
			if (len < 0) {
				f[n++] = new(pos, t.Length);
				break;
			} else {
				if (!(noEmpty && len == 0)) f[n++] = new(pos, pos + len);
				pos += len + 1;
				if (span[len] == '\r' && pos < t.Length && t[pos] == '\n') pos++;
			}
		}

		var a = new StartEnd[n];
		new Span<StartEnd>(f.p, n).CopyTo(a);
		return a;
	}

	/// <summary>
	/// Returns the number of lines.
	/// Counts line separators "\r\n", "\n", "\r".
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="preferMore">Add 1 if the string ends with a line separator or its length is 0.</param>
	/// <param name="range">Part of this string or null (default).</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <seealso cref="StringUtil.LineAndColumn"/>
	public static int LineCount(this string t, bool preferMore = false, Range? range = null) {
		var (i, to) = range.GetStartEnd(t.Length);
		if (to - i == 0) return preferMore ? 1 : 0;
		int n = 1;
		for (; i < to; i++) {
			char c = t[i];
			if (c > '\r') continue;
			if (c == '\r') {
				if (++i == to || t[i] != '\n') i--; //single \r ?
				n++;
			} else if (c == '\n') n++;
		}
		if (!preferMore) switch (t[i - 1]) { case '\n': case '\r': n--; break; }
		return n;
	}

	/// <summary>
	/// Converts this string to lower case.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <remarks>
	/// Calls <see cref="string.ToLowerInvariant"/>.
	/// </remarks>
	public static string Lower(this string t) => t.ToLowerInvariant();

	/// <summary>
	/// Converts this string to upper case.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <remarks>
	/// Calls <see cref="string.ToUpperInvariant"/>.
	/// </remarks>
	public static string Upper(this string t) => t.ToUpperInvariant();

	/// <summary>
	/// Converts this string or only the first character to upper case or all words to title case.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <param name="how"></param>
	/// <param name="culture">Culture, for example <c>CultureInfo.CurrentCulture</c>. If null (default) uses invariant culture.</param>
	public static unsafe string Upper(this string t, SUpper how, CultureInfo culture = null) {
		if (how == SUpper.FirstChar) {
			if (t.Length == 0 || !char.IsLower(t, 0)) return t;
			var r = Rune.GetRuneAt(t, 0);
			r = culture != null ? Rune.ToUpper(r, culture) : Rune.ToUpperInvariant(r);
			int n = r.IsBmp ? 1 : 2;
			var m = new Span<char>(&r, n);
			if (n == 2) r.EncodeToUtf16(m);
			return string.Concat(m, t.AsSpan(n));
		}
		var ti = (culture ?? CultureInfo.InvariantCulture).TextInfo;
		t = t ?? throw new NullReferenceException();
		if (how == SUpper.TitleCase) return ti.ToTitleCase(t);
		return ti.ToUpper(t);
	}

	#region ToNumber

	static long _ToInt(string t, int startIndex, out int numberEndIndex, bool toLong, STIFlags flags) {
		numberEndIndex = 0;

		int len = t.Lenn();
		if ((uint)startIndex > len) throw new ArgumentOutOfRangeException("startIndex");
		int i = startIndex;
		char c;

		//skip spaces
		for (; ; i++) {
			if (i == len) return 0;
			c = t[i];
			if (c > ' ') break;
			if (c == ' ') continue;
			if (c < '\t' || c > '\r') break; //\t \n \v \f \r
		}
		if (i > startIndex && 0 != (flags & STIFlags.DontSkipSpaces)) return 0;

		//skip arabic letter mark etc
		if (c >= '\x61C' && (c == '\x61C' || c == '\x200E' || c == '\x200F')) {
			if (++i == len) return 0;
			c = t[i];
		}

		//skip -+
		bool minus = false;
		if (c == '-' || c == '−' || c == '+') {
			if (++i == len) return 0;
			if (c != '+') minus = true;
			c = t[i];
		}

		//is hex?
		bool isHex = false;
		switch (flags & (STIFlags.NoHex | STIFlags.IsHexWithout0x)) {
		case 0:
			if (c == '0' && i <= len - 3) {
				c = t[++i];
				if (isHex = (c == 'x' || c == 'X')) i++; else i--;
			}
			break;
		case STIFlags.IsHexWithout0x:
			isHex = true;
			break;
		}

		//skip '0'
		int i0 = i;
		while (i < len && t[i] == '0') i++;

		long R = 0; //result

		int nDigits = 0, nMaxDigits;
		if (isHex) {
			nMaxDigits = toLong ? 16 : 8;
			for (; i < len; i++) {
				int k = _CharHexToDec(t[i]); if (k < 0) break;
				if (++nDigits > nMaxDigits) return 0;
				R = (R << 4) + k;
			}
			if (i == i0) i--; //0xNotHex (decimal 0)

		} else { //decimal or not a number
			nMaxDigits = toLong ? 20 : 10;
			for (; i < len; i++) {
				int k = t[i] - '0'; if (k < 0 || k > 9) break;
				R = R * 10 + k;
				//is too long?
				if (++nDigits >= nMaxDigits) {
					if (nDigits > nMaxDigits) return 0;
					if (toLong) {
						if (string.CompareOrdinal(t, i + 1 - nDigits, "18446744073709551615", 0, nDigits) > 0) return 0;
					} else {
						if (R > uint.MaxValue) return 0;
					}
				}
			}
			if (i == i0) return 0; //not a number
		}

		if (minus) R = -R;
		numberEndIndex = i;
		return R;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static int _CharHexToDec(char c) {
		if (c >= '0' && c <= '9') return c - '0';
		if (c >= 'A' && c <= 'F') return c - ('A' - 10);
		if (c >= 'a' && c <= 'f') return c - ('a' - 10);
		return -1;
	}

	/// <summary>
	/// Converts part of this string to int number and gets the number end index.
	/// Returns the number, or 0 if fails to convert.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="startIndex">Offset in this string where to start parsing.</param>
	/// <param name="numberEndIndex">Receives offset in this string where the number part ends. If fails to convert, receives 0.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	/// <remarks>
	/// Fails to convert when string is null, "", does not begin with a number or the number is too big.
	/// 
	/// Unlike <b>int.Parse</b> and <b>Convert.ToInt32</b>:
	///		The number in string can be followed by more text, like <c>"123text"</c>.
	///		Has <i>startIndex</i> parameter that allows to get number from middle, like <c>"text123text"</c>.
	///		Gets the end of the number part.
	///		No exception when cannot convert.
	///		The number can be decimal (like <c>"123"</c>) or hexadecimal (like <c>"0x1A"</c>); don't need separate flags for each style.
	///		Does not depend on current culture. As minus sign recognizes '-' and '−'.
	///		Faster.
	///	
	/// The number in string can start with ASCII whitespace (spaces, newlines, etc), like <c>" 5"</c>.
	/// The number in string can be with <c>"-"</c> or <c>"+"</c>, like <c>"-5"</c>, but not like <c>"- 5"</c>.
	/// Fails if the number is greater than +- <b>uint.MaxValue</b> (0xffffffff).
	/// The return value becomes negative if the number is greater than <b>int.MaxValue</b>, for example <c>"0xffffffff"</c> is -1, but it becomes correct if assigned to uint (need cast).
	/// Does not support non-integer numbers; for example, for <c>"3.5E4"</c> returns 3 and sets <c>numberEndIndex=startIndex+1</c>.
	/// </remarks>
	public static int ToInt(this string t, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		return (int)_ToInt(t, startIndex, out numberEndIndex, false, flags);
	}

	/// <summary>
	/// Converts part of this string to int number.
	/// Returns the number, or 0 if fails to convert.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static int ToInt(this string t, int startIndex = 0, STIFlags flags = 0) {
		return (int)_ToInt(t, startIndex, out _, false, flags);
	}

	/// <summary>
	/// Converts part of this string to int number and gets the number end index.
	/// Returns false if fails.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="result">Receives the result number.</param>
	/// <param name="startIndex">Offset in this string where to start parsing.</param>
	/// <param name="numberEndIndex">Receives offset in this string where the number part ends. If fails to convert, receives 0.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static bool ToInt(this string t, out int result, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		result = (int)_ToInt(t, startIndex, out numberEndIndex, false, flags);
		return numberEndIndex != 0;
	}

#pragma warning disable CS3006 // Overloaded method differing only in ref or out, or in array rank, is not CLS-compliant
	/// <summary>
	/// Converts part of this string to int number.
	/// Returns false if fails.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static bool ToInt(this string t, out int result, int startIndex = 0, STIFlags flags = 0)
		=> ToInt(t, out result, startIndex, out _, flags);
#pragma warning restore CS3006 // Overloaded method differing only in ref or out, or in array rank, is not CLS-compliant

	/// <summary>
	/// Converts part of this string to uint number and gets the number end index.
	/// Returns false if fails.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static bool ToInt(this string t, out uint result, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		result = (uint)_ToInt(t, startIndex, out numberEndIndex, false, flags);
		return numberEndIndex != 0;
	}

	/// <summary>
	/// Converts part of this string to uint number.
	/// Returns false if fails.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static bool ToInt(this string t, out uint result, int startIndex = 0, STIFlags flags = 0)
		=> ToInt(t, out result, startIndex, out _, flags);

	/// <summary>
	/// Converts part of this string to long number and gets the number end index.
	/// Returns false if fails.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static bool ToInt(this string t, out long result, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		result = _ToInt(t, startIndex, out numberEndIndex, true, flags);
		return numberEndIndex != 0;
	}

	/// <summary>
	/// Converts part of this string to long number.
	/// Returns false if fails.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static bool ToInt(this string t, out long result, int startIndex = 0, STIFlags flags = 0)
		=> ToInt(t, out result, startIndex, out _, flags);

	/// <summary>
	/// Converts part of this string to ulong number and gets the number end index.
	/// Returns false if fails.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static bool ToInt(this string t, out ulong result, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		result = (ulong)_ToInt(t, startIndex, out numberEndIndex, true, flags);
		return numberEndIndex != 0;
	}

	/// <summary>
	/// Converts part of this string to ulong number.
	/// Returns false if fails.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	public static bool ToInt(this string t, out ulong result, int startIndex = 0, STIFlags flags = 0)
		=> ToInt(t, out result, startIndex, out _, flags);

	/// <summary>
	/// Converts this string or its part to double number.
	/// Returns the number, or 0 if fails to convert.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="range">Part of this string or null (default).</param>
	/// <param name="style">The permitted number format in the string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid <i>style</i>.</exception>
	/// <remarks>
	/// Calls <see cref="double.TryParse(RStr, NumberStyles, IFormatProvider, out double)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// Fails if the string is null or "" or isn't a valid floating-point number.
	/// Examples of valid numbers: "12", " -12.3 ", ".12", "12.", "12E3", "12.3e-45", "1,234.5" (with style <c>NumberStyles.Float | NumberStyles.AllowThousands</c>). String like "2text" is invalid, unless range is <c>0..1</c>.
	/// </remarks>
	public static double ToNumber(this string t, Range? range = null, NumberStyles style = NumberStyles.Float) {
		ToNumber(t, out double r, range, style);
		return r;
	}

	/// <summary>
	/// Converts this string or its part to double number.
	/// Returns false if fails.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="result">Receives the result number.</param>
	/// <param name="range">Part of this string or null (default).</param>
	/// <param name="style">The permitted number format in the string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid <i>style</i>.</exception>
	/// <remarks>
	/// Calls <see cref="double.TryParse(RStr, NumberStyles, IFormatProvider, out double)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	public static bool ToNumber(this string t, out double result, Range? range = null, NumberStyles style = NumberStyles.Float) {
		return double.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	/// <summary>
	/// Converts this string or its part to float number.
	/// Returns false if fails.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="result">Receives the result number.</param>
	/// <param name="range">Part of this string or null (default).</param>
	/// <param name="style">The permitted number format in the string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid <i>style</i>.</exception>
	/// <remarks>
	/// Calls <see cref="float.TryParse(RStr, NumberStyles, IFormatProvider, out float)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	public static bool ToNumber(this string t, out float result, Range? range = null, NumberStyles style = NumberStyles.Float) {
		return float.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	/// <summary>
	/// Converts this string or its part to int number.
	/// Returns false if fails.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="result">Receives the result number.</param>
	/// <param name="range">Part of this string or null (default).</param>
	/// <param name="style">The permitted number format in the string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid <i>style</i>.</exception>
	/// <remarks>
	/// Calls <see cref="int.TryParse(RStr, NumberStyles, IFormatProvider, out int)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	public static bool ToNumber(this string t, out int result, Range? range = null, NumberStyles style = NumberStyles.Integer) {
		return int.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);

		//note: exception if NumberStyles.Integer | NumberStyles.AllowHexSpecifier.
		//	Can parse either decimal or hex, not any.
		//	Does not support "0x". With AllowHexSpecifier eg "11" is 17, but "0x11" is invalid.
	}

	/// <summary>
	/// Converts this string or its part to uint number.
	/// Returns false if fails.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="result">Receives the result number.</param>
	/// <param name="range">Part of this string or null (default).</param>
	/// <param name="style">The permitted number format in the string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid <i>style</i>.</exception>
	/// <remarks>
	/// Calls <see cref="uint.TryParse(RStr, NumberStyles, IFormatProvider, out uint)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	public static bool ToNumber(this string t, out uint result, Range? range = null, NumberStyles style = NumberStyles.Integer) {
		return uint.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	/// <summary>
	/// Converts this string or its part to long number.
	/// Returns false if fails.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="result">Receives the result number.</param>
	/// <param name="range">Part of this string or null (default).</param>
	/// <param name="style">The permitted number format in the string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid <i>style</i>.</exception>
	/// <remarks>
	/// Calls <see cref="long.TryParse(RStr, NumberStyles, IFormatProvider, out long)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	public static bool ToNumber(this string t, out long result, Range? range = null, NumberStyles style = NumberStyles.Integer) {
		return long.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	/// <summary>
	/// Converts this string or its part to ulong number.
	/// Returns false if fails.
	/// </summary>
	/// <param name="t">This string. Can be null.</param>
	/// <param name="result">Receives the result number.</param>
	/// <param name="range">Part of this string or null (default).</param>
	/// <param name="style">The permitted number format in the string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid <i>style</i>.</exception>
	/// <remarks>
	/// Calls <see cref="ulong.TryParse(RStr, NumberStyles, IFormatProvider, out ulong)"/>. Uses <see cref="CultureInfo.InvariantCulture"/> if the string range contains only ASCII characters, else uses current culture.
	/// </remarks>
	public static bool ToNumber(this string t, out ulong result, Range? range = null, NumberStyles style = NumberStyles.Integer) {
		return ulong.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	static RStr _NumSpan(string t, Range? range, out CultureInfo ci) {
		ci = CultureInfo.InvariantCulture;
		if (t == null) return default;
		var (start, len) = range.GetOffsetAndLength(t.Length);

		//Workaround for .NET 5 preview 7 bug: if current user culture is eg Norvegian or Lithuanian,
		//	'number to/from string' functions use '−' (Unicode minus), not '-' (ASCII hyphen), even if in Control Panel is ASCII hyphen.
		//	Tested: no bug in .NET Core 3.1.
		//Also, in some cultures eg Arabic there are more chars.
		if (!t.AsSpan(start, len).IsAscii()) ci = CultureInfo.CurrentCulture;

		return t.AsSpan(start, len);
	}

	#endregion

	/// <summary>
	/// Inserts other string.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <param name="startIndex">Offset in this string. Can be from end, like <c>^4</c>.</param>
	/// <param name="s">String to insert.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>startIndex</i>.</exception>
	public static string Insert(this string t, Index startIndex, string s) {
		return t.Insert(startIndex.GetOffset(t.Length), s);
	}

	/// <summary>
	/// Replaces part of this string with other string.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <param name="startIndex">Offset in this string.</param>
	/// <param name="count">Count of characters to replace.</param>
	/// <param name="s">The replacement string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>startIndex</i> or <i>count</i>.</exception>
	public static string ReplaceAt(this string t, int startIndex, int count, string s) {
		int i = startIndex;
		if (count == 0) return t.Insert(i, s);
		return string.Concat(t.AsSpan(0, i), s, t.AsSpan(i + count));
	}

	/// <summary>
	/// Replaces part of this string with other string.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <param name="range">Part of this string to replace.</param>
	/// <param name="s">The replacement string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	public static string ReplaceAt(this string t, Range range, string s) {
		var (i, count) = range.GetOffsetAndLength(t.Length);
		return ReplaceAt(t, i, count, s);
	}

	/// <summary>
	/// Removes part of this string.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t">This string.</param>
	/// <param name="range">Part of this string to remove.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>ranget</i>.</exception>
	public static string Remove(this string t, Range range) {
		var (i, count) = range.GetOffsetAndLength(t.Length);
		return t.Remove(i, count);
	}

	//rejected. Use [..^count].
	///// <summary>
	///// Removes <i>count</i> characters from the end of this string.
	///// </summary>
	///// <returns>The result string.</returns>
	///// <param name="t">This string.</param>
	///// <param name="count">Count of characters to remove.</param>
	///// <exception cref="ArgumentOutOfRangeException"></exception>
	//public static string RemoveSuffix(this string t, int count) => t[^count];

	/// <summary>
	/// Removes <i>suffix</i> string from the end.
	/// </summary>
	/// <returns>The result string. Returns this string if does not end with <i>suffix</i>.</returns>
	/// <param name="t">This string.</param>
	/// <param name="suffix">Substring to remove.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>suffix</i> is null.</exception>
	public static string RemoveSuffix(this string t, string suffix, bool ignoreCase = false) {
		if (!t.Ends(suffix, ignoreCase)) return t;
		return t[..^suffix.Length];
	}

	/// <summary>
	/// Removes <i>suffix</i> character from the end.
	/// </summary>
	/// <returns>The result string. Returns this string if does not end with <i>suffix</i>.</returns>
	/// <param name="t">This string.</param>
	/// <param name="suffix">Character to remove.</param>
	/// <exception cref="ArgumentNullException"><i>suffix</i> is null.</exception>
	public static string RemoveSuffix(this string t, char suffix) {
		if (!t.Ends(suffix)) return t;
		return t[..^1];
	}

	/// <summary>
	/// If this string is longer than <i>limit</i>, returns its substring 0 to <i>limit</i>-1 with appended '…' character.
	/// Else returns this string.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="limit">Maximal length of the result string. If less than 1, uses 1.</param>
	/// <param name="middle">Let "…" be in the middle. For example it is useful when the string is a file path, to avoid removing the filename.</param>
	/// <param name="lines"><i>limit</i> is lines, not characters.</param>
	public static string Limit(this string t, int limit, bool middle = false, bool lines = false) {
		if (limit < 1) limit = 1;
		if (lines) {
			var a = t.AsSpan().Lines();
			int k = a.Length;
			if (k > limit) {
				limit--; //for "…" line
				if (limit == 0) return t[a[0].Range] + "…";
				if (middle) {
					if (limit == 1) return t[a[0].Range] + "\r\n…";
					int half = limit - limit / 2; //if limit is odd number, prefer more lines at the start
					int half2 = a.Length - (limit - half);
					//if (half2 == a.Length - 1 && a[half2].Length == 0) return t[..a[half].end] + "\r\n…"; //rejected: if ends with newline, prefer more lines at the start than "\r\n…\r\n" at the end
					return t.ReplaceAt(a[half - 1].end..a[half2].start, "\r\n…\r\n");
				} else {
					return t[..a[limit - 1].end] + "\r\n…";
				}
			}
		} else if (t.Length > limit) {
			limit--; //for "…"
			if (middle) {
				int i = _Correct(t, limit / 2);
				int j = _Correct(t, t.Length - (limit - i), 1);
				return t.ReplaceAt(i..j, "…");
			} else {
				limit = _Correct(t, limit);
				return t[..limit] + "…";
			}

			//ensure not in the middle of a surrogate pair or \r\n
			static int _Correct(string s, int i, int d = -1) {
				if (i > 0 && i < s.Length) {
					char c = s[i - 1];
					if ((c == '\r' && s[i] == '\n') || char.IsSurrogatePair(c, s[i])) i += d;
				}
				return i;
			}
		}
		return t;
	}

	/// <summary>
	/// Replaces unsafe characters with C# escape sequences.
	/// If the string contains these characters, replaces and returns new string. Else returns this string.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="limit">If the final string is longer than <i>limit</i>, get its substring 0 to <i>limit</i>-1 with appended '…' character. The enclosing "" are not counted.</param>
	/// <param name="quote">Enclose in "".</param>
	/// <remarks>
	/// Replaces these characters: <c>'\\'</c>, <c>'\"'</c>, <c>'\t'</c>, <c>'\n'</c>, <c>'\r'</c> and all in range 0-31.
	/// </remarks>
	public static string Escape(this string t, int limit = 0, bool quote = false) {
		int i, len = t.Length;
		if (len == 0) return quote ? "\"\"" : t;

		if (limit > 0) {
			if (len > limit) len = limit - 1; else limit = 0;
		}

		for (i = 0; i < len; i++) {
			var c = t[i];
			if (c < ' ' || c == '\\' || c == '\"') goto g1;
			//tested: Unicode line-break chars in most controls don't break lines, therefore don't need to escape
		}
		if (limit > 0) t = Limit(t, limit);
		if (quote) t = "\"" + t + "\"";
		return t;
	g1:
		using (new StringBuilder_(out var b, len + len / 16 + 100)) {
			if (quote) b.Append('\"');
			for (i = 0; i < len; i++) {
				var c = t[i];
				if (c < ' ') {
					switch (c) {
					case '\t': b.Append("\\t"); break;
					case '\n': b.Append("\\n"); break;
					case '\r': b.Append("\\r"); break;
					case '\0': b.Append("\\0"); break;
					default: b.Append("\\u").Append(((ushort)c).ToString("x4")); break;
					}
				} else if (c == '\\') b.Append("\\\\");
				else if (c == '\"') b.Append("\\\"");
				else b.Append(c);

				if (limit > 0 && b.Length - (quote ? 1 : 0) >= len) break;
			}

			if (limit > 0) b.Append('…');
			if (quote) b.Append('\"');
			return b.ToString();
		}
	}

	/// <summary>
	/// Replaces C# escape sequences to characters in this string.
	/// Returns true if successful. Returns false if the string contains an invalid or unsupported escape sequence.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="result">Receives the result string. It is this string if there are no escape sequences or if fails.</param>
	/// <remarks>
	/// Supports all escape sequences of <see cref="Escape"/>: \\ \" \t \n \r \0 \uXXXX.
	/// Does not support \a \b \f \v \' \xXXXX \UXXXXXXXX.
	/// </remarks>
	public static bool Unescape(this string t, out string result) {
		result = t;
		int i = t.IndexOf('\\');
		if (i < 0) return true;

		using (new StringBuilder_(out var b, t.Length)) {
			b.Append(t, 0, i);

			for (; i < t.Length; i++) {
				char c = t[i];
				if (c == '\\') {
					if (++i == t.Length) return false;
					switch (c = t[i]) {
					case '\\': case '\"': break;
					case 't': c = '\t'; break;
					case 'n': c = '\n'; break;
					case 'r': c = '\r'; break;
					case '0': c = '\0'; break;
					//case 'a': c = '\a'; break;
					//case 'b': c = '\b'; break;
					//case 'f': c = '\f'; break;
					//case 'v': c = '\v'; break;
					//case '\'': break;
					//also we don't support U and x
					case 'u':
						if (!_Uni(t, ++i, 4, out int u)) return false;
						c = (char)u;
						i += 3;
						break;
					default: return false;
					}
				}
				b.Append(c);
			}

			result = b.ToString();
			return true;
		}

		static bool _Uni(string t, int i, int maxLen, out int R) {
			R = 0;
			int to = i + maxLen; if (to > t.Length) return false;
			for (; i < to; i++) {
				int k = _CharHexToDec(t[i]); if (k < 0) return false;
				R = (R << 4) + k;
			}
			return true;
		}
	}

	//rejected
	///// <summary>
	///// Replaces all "'" with "''".
	///// </summary>
	//public static string SqlEscape(this string t) => t.Replace("'", "''");

	//rejected. Rarely used.
	///// <summary>
	///// Converts this string to '\0'-terminated char[].
	///// </summary>
	//public static char[] ToArrayAnd0(this string t)
	//{
	//	var c = new char[t.Length + 1];
	//	for(int i = 0; i < t.Length; i++) c[i] = t[i];
	//	return c;
	//}

	//rejected. Better call Convert2.Utf8FromString directly.
	///// <summary>
	///// Converts this string to '\0'-terminated UTF8 string as byte[].
	///// </summary>
	///// <remarks>
	///// Calls <see cref="Convert2.Utf8FromString"/>.
	///// </remarks>
	///// <seealso cref="Convert2.Utf8ToString"/>
	///// <seealso cref="Encoding.UTF8"/>
	//public static byte[] ToUtf8And0(this string t) => Convert2.Utf8FromString(t);


	/// <summary>
	/// Reverses this string, like "Abc" -> "cbA".
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t"></param>
	/// <param name="raw">Ignore char sequences such as Unicode surrogates and grapheme clusters. Faster, but if the string contains these sequences, the result string is incorrect.</param>
	public static unsafe string ReverseString(this string t, bool raw) {
		if (t.Length < 2) return t;
		var r = new string('\0', t.Length);
		fixed (char* p = r) {
			if (raw || t.IsAscii()) {
				for (int i = 0, j = t.Length; i < t.Length; i++) {
					p[--j] = t[i];
				}
			} else {
				var a = StringInfo.ParseCombiningCharacters(t); //speed: same as StringInfo.GetTextElementEnumerator+MoveNext+ElementIndex
				for (int gTo = t.Length, j = 0, i = a.Length; --i >= 0; gTo = a[i]) {
					for (int g = a[i]; g < gTo; g++) p[j++] = t[g];
				}
			}
		}
		return r;

		//tested: string.Create slower.
	}

	/// <summary>
	/// Returns true if does not contain non-ASCII characters.
	/// </summary>
	/// <seealso cref="IsAscii(RStr)"/>
	public static bool IsAscii(this string t) => t.AsSpan().IsAscii();

	/// <summary>
	/// Returns true if does not contain non-ASCII characters.
	/// </summary>
	public static bool IsAscii(this RStr t) {
		foreach (char c in t) if (c > 0x7f) return false;
		return true;
	}

	///// <summary>
	///// Returns true if does not contain non-ASCII characters.
	///// </summary>
	//public static bool IsAscii(this ReadOnlySpan<byte> t) {
	//	foreach (char c in t) if (c > 0x7f) return false;
	//	return true;
	//}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int LengthThrowIfNull_(this RStr t) {
		int n = t.Length;
		if (n == 0 && t == default) throw new ArgumentNullException();
		return n;
	}

	/// <summary>
	/// Returns true if equals to string <i>s</i>, case-sensitive.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="s">Other string. Can be null.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Eq(this RStr t, RStr s) => t.Equals(s, StringComparison.Ordinal);

	/// <summary>
	/// Returns true if equals to string <i>s</i>, case-insensitive.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="s">Other string. Can be null.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Eqi(this RStr t, RStr s) => t.Equals(s, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Compares part of this span with string <i>s</i>. Returns true if equal.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="startIndex">Offset in this span. If invalid, returns false.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Eq(this RStr t, int startIndex, RStr s, bool ignoreCase = false) {
		int ns = s.LengthThrowIfNull_();
		int to = startIndex + ns, tlen = t.Length;
		if (to > tlen || (uint)startIndex > tlen) return false;
		t = t[startIndex..to];
		if (!ignoreCase) return t.SequenceEqual(s);
		return t.Equals(s, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Returns true if the specified character is at the specified position in this span.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="index">Offset in this span. If invalid, returns false.</param>
	/// <param name="c">Character.</param>
	public static bool Eq(this RStr t, int index, char c) {
		if ((uint)index >= t.Length) return false;
		return t[index] == c;
	}

	/// <summary>
	/// Returns true if starts with string <i>s</i>.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Starts(this RStr t, RStr s, bool ignoreCase = false) => t.StartsWith(s, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

	/// <summary>
	/// Returns true if starts with string <i>s</i>.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Ends(this RStr t, RStr s, bool ignoreCase = false) => t.EndsWith(s, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

	/// <summary>
	/// Finds character <i>c</i> in this span, starting from <i>index</i>.
	/// Returns its index in this span, or -1 if not found.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static int IndexOf(this RStr t, int index, char c) {
		int i = t[index..].IndexOf(c);
		return i < 0 ? i : i + index;
	}

	/// <summary>
	/// Finds character <i>c</i> in <i>range</i> of this span.
	/// Returns its index in this span, or -1 if not found.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static int IndexOf(this RStr t, Range range, char c) {
		int i = t[range].IndexOf(c);
		if (i < 0) return i;
		return i + range.Start.GetOffset(t.Length);
	}

	/// <summary>
	/// Finds string <i>s</i> in this span, starting from <i>index</i>.
	/// Returns its index in this span, or -1 if not found.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	public static int IndexOf(this RStr t, int index, RStr s, bool ignoreCase = false) {
		if (s == default) throw new ArgumentNullException();
		int i = t[index..].IndexOf(s, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		return i < 0 ? i : i + index;
	}

	/// <summary>
	/// Finds string <i>s</i> in <i>range</i> of this span.
	/// Returns its index in this span, or -1 if not found.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="ArgumentNullException"><i>s</i> is null.</exception>
	public static int IndexOf(this RStr t, Range range, RStr s, bool ignoreCase = false) {
		if (s == default) throw new ArgumentNullException();
		int i = t[range].IndexOf(s, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		if (i < 0) return i;
		return i + range.Start.GetOffset(t.Length);
	}

	/// <summary>
	/// Creates read-only span from range of this string.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"/>
	public static RStr AsSpan(this string t, Range r) {
		var (i, len) = r.GetOffsetAndLength(t.Length);
		return t.AsSpan(i, len);
	}

	internal static void CopyTo_(this string t, char* p) => t.AsSpan().CopyTo(new Span<char>(p, t.Length));
}

/// <summary>
/// Flags for <see cref="ExtString.ToInt"/> and similar functions.
/// </summary>
[Flags]
public enum STIFlags {
	/// <summary>
	/// Don't support hexadecimal numbers (numbers with prefix "0x").
	/// </summary>
	NoHex = 1,

	/// <summary>
	/// The number in string is hexadecimal without a prefix, like "1A".
	/// </summary>
	IsHexWithout0x = 2,

	/// <summary>
	/// Fail if string starts with a whitespace character.
	/// </summary>
	DontSkipSpaces = 4,
}

/// <summary>
/// Used with <see cref="ExtString.Upper(string, SUpper, CultureInfo)"/>
/// </summary>
public enum SUpper {
	/// <summary>
	/// Convert all characters to upper case.
	/// </summary>
	AllChars,

	/// <summary>
	/// Convert only the first character to upper case.
	/// </summary>
	FirstChar,

	/// <summary>
	/// Convert the first character of each word to upper case and other characters to lower case.
	/// Calls <see cref="TextInfo.ToTitleCase"/>.
	/// </summary>
	TitleCase,
}
