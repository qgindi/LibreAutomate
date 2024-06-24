using System.Buffers;

namespace Au.Types;

/// <summary>
/// Adds extension methods for <see cref="String"/>.
/// </summary>
/// <remarks>
/// Some .NET <see cref="String"/> methods use <see cref="StringComparison.CurrentCulture"/> by default, while others use ordinal or invariant comparison. It is confusing (difficult to remember), dangerous (easy to make bugs), slower and rarely useful.
/// Microsoft recommends to specify <b>StringComparison.Ordinal[IgnoreCase]</b> explicitly. See <see href="https://msdn.microsoft.com/en-us/library/ms973919.aspx"/>.
/// This class adds ordinal comparison versions of these methods. Same or similar name, for example <b>Ends</b> for <b>EndsWith</b>.
/// See also <see cref="process.thisProcessCultureIsInvariant"/>.
/// 
/// This class also adds more methods.
/// You also can find string functions in other classes of this library, including <see cref="StringUtil"/>, <see cref="regexp"/>, <see cref="pathname"/>, <see cref="csvTable"/>, <see cref="keys.more"/>, <see cref="Convert2"/>, <see cref="Hash"/>.
/// </remarks>
public static unsafe partial class ExtString {
	/// <summary>
	/// Compares this and other string. Returns <c>true</c> if equal.
	/// </summary>
	/// <param name="t">This string. Can be <c>null</c>.</param>
	/// <param name="s">Other string. Can be <c>null</c>.</param>
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
	/// Returns 1-based index of the matching string, or 0 if none.
	/// </summary>
	/// <param name="t">This string. Can be <c>null</c>.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="strings">Other strings. Strings can be <c>null</c>.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Eq(this string t, bool ignoreCase, params string[] strings) {
		for (int i = 0; i < strings.Length; i++) if (Eq(t, strings[i], ignoreCase)) return i + 1;
		return 0;
	}

	/// <summary>
	/// Compares part of this string with other string. Returns <c>true</c> if equal.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="startIndex">Offset in this string. If invalid, returns <c>false</c>.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
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
	/// Returns 1-based index of the matching string, or 0 if none.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="startIndex">Offset in this string. If invalid, returns <c>false</c>.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="strings">Other strings.</param>
	/// <exception cref="ArgumentNullException">A string in <i>strings</i> is <c>null</c>.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Eq(this string t, int startIndex, bool ignoreCase = false, params string[] strings) {
		for (int i = 0; i < strings.Length; i++) if (t.Eq(startIndex, strings[i], ignoreCase)) return i + 1;
		return 0;
	}

	/// <summary>
	/// Compares part of this string with other string. Returns <c>true</c> if equal.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="range">Range of this string. Can return <c>true</c> only if its length <c>== s.Length</c>. If invalid, returns <c>false</c>.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
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
	/// Returns <c>true</c> if the specified character is at the specified position in this string.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="index">Offset in this string. If invalid, returns <c>false</c>.</param>
	/// <param name="c">Character.</param>
	public static bool Eq(this string t, int index, char c) {
		if ((uint)index >= t.Length) return false;
		return t[index] == c;
	}

	/// <summary>
	/// Compares this and other string ignoring case (case-insensitive). Returns <c>true</c> if equal.
	/// </summary>
	/// <param name="t">This string. Can be <c>null</c>.</param>
	/// <param name="s">Other string. Can be <c>null</c>.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Eqi(this string t, string s) => string.Equals(t, s, StringComparison.OrdinalIgnoreCase);

	//rejected. Not so often used.
	//public static bool Eqi(this string t, int startIndex, string s) => Eq(t, startIndex, s, true);

	/// <summary>
	/// Compares end of this string with other string. Returns <c>true</c> if equal.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
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
	/// Returns 1-based index of the matching string, or 0 if none.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="strings">Other strings.</param>
	/// <exception cref="ArgumentNullException">A string in <i>strings</i> is <c>null</c>.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Ends(this string t, bool ignoreCase, params string[] strings) {
		for (int i = 0; i < strings.Length; i++) if (Ends(t, strings[i], ignoreCase)) return i + 1;
		return 0;
	}

	/// <summary>
	/// Returns <c>true</c> if this string ends with the specified character.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="c">Character.</param>
	public static bool Ends(this string t, char c) {
		int i = t.Length - 1;
		return i >= 0 && t[i] == c;
	}

	/// <summary>
	/// Compares beginning of this string with other string. Returns <c>true</c> if equal.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
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
	/// Returns 1-based index of the matching string, or 0 if none.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="strings">Other strings.</param>
	/// <exception cref="ArgumentNullException">A string in <i>strings</i> is <c>null</c>.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Starts(this string t, bool ignoreCase, params string[] strings) {
		for (int i = 0; i < strings.Length; i++) if (Starts(t, strings[i], ignoreCase)) return i + 1;
		return 0;
	}

	/// <summary>
	/// Returns <c>true</c> if this string starts with the specified character.
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
	//RxIsMatch(LITERAL|CASELESS)				19 ms
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
	/// <param name="s">Substring to find.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
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
	/// <param name="s">Substring to find.</param>
	/// <param name="startIndex">The search start index.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
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
	/// <param name="s">Substring to find.</param>
	/// <param name="range">The search range.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static int Find(this string t, string s, Range range, bool ignoreCase = false) {
		var (start, count) = range.GetOffsetAndLength(t.Length);
		return t.IndexOf(s, start, count, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
	}

	//CONSIDER: make public.
	/// <summary>
	/// Like <see cref="Find(string, string, bool)"/>, but with a predicate.
	/// </summary>
	/// <param name="also">Called for each found substring until returns <c>true</c>. Receives this string and start and end offsets of the substring.</param>
	internal static int Find_(this string t, string s, Func<string, int, int, bool> also, bool ignoreCase = false) {
		for (int i = 0; ; i++) {
			i = t.Find(s, i, ignoreCase);
			if (i < 0) break;
			if (also(t, i, i + s.Length)) return i;
		}
		return -1;
	}

	/// <summary>
	/// Finds the first character specified in <i>chars</i>. Returns its index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="chars">Characters.</param>
	/// <param name="range">The search range.</param>
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
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
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
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
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static int IndexOfNot(this RStr t, string chars) {
		Not_.Null(chars);
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
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static int LastIndexOfNot(this RStr t, string chars) {
		Not_.Null(chars);
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
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
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
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
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
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
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
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
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
	/// <exception cref="ArgumentNullException"><i>chars</i> is <c>null</c>.</exception>
	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	public static string TrimEnd(this string t, string chars) {
		var span = t.AsSpan().TrimEnd(chars);
		return span.Length == t.Length ? t : new string(span);
	}

	/// <summary>
	/// Finds whole word. Returns its 0-based index, or -1 if not found.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="s">Substring to find.</param>
	/// <param name="range">The search range.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <param name="otherWordChars">Additional word characters. For example <c>"_"</c>.</param>
	/// <param name="isWordChar">Function that returns <c>true</c> for word characters. If <c>null</c>, uses <see cref="char.IsLetterOrDigit"/>.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <remarks>
	/// If <i>s</i> starts with a word character, finds substring that is not preceded by a word character.
	/// If <i>s</i> ends with a word character, finds substring that is not followed by a word character.
	/// Word characters are those for which <i>isWordChar</i> or <see cref="char.IsLetterOrDigit"/> returns <c>true</c> plus those specified in <i>otherWordChars</i>.
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// For Unicode surrogates (2-<b>char</b> characters) calls <see cref="char.IsLetterOrDigit(string, int)"/> and ignores <i>isWordChar</i> and <i>otherWordChars</i>.
	/// </remarks>
	public static int FindWord(this string t, string s, Range? range = null, bool ignoreCase = false, string otherWordChars = null, Func<char, bool> isWordChar = null) {
		Not_.Null(s);
		var (start, end) = range.GetStartEnd(t.Length);
		int lens = s.Length;
		if (lens == 0) return 0; //like IndexOf and Find

		bool wordStart = _IsWordChar(s, 0, false),
			wordEnd = _IsWordChar(s, lens - 1, true);

		for (int i = start, iMax = end - lens; i <= iMax; i++) {
			i = t.IndexOf(s, i, end - i, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
			if (i < 0) break;
			if (wordStart && i > 0 && _IsWordChar(t, i - 1, true)) continue;
			if (wordEnd && i < iMax && _IsWordChar(t, i + lens, false)) continue;
			return i;
		}
		return -1;

		bool _IsWordChar(string s, int i, bool expandLeft) {
			//CONSIDER: use Rune
			char c = s[i];
			if (c >= '\uD800' && c <= '\uDFFF') { //Unicode surrogates
				if (expandLeft) {
					if (char.IsLowSurrogate(s[i])) return i > 0 && char.IsHighSurrogate(s[i - 1]) && char.IsLetterOrDigit(s, i - 1);
				} else {
					if (char.IsHighSurrogate(s[i])) return i < s.Length - 1 && char.IsLowSurrogate(s[i + 1]) && char.IsLetterOrDigit(s, i);
				}
			} else {
				if (isWordChar?.Invoke(c) ?? char.IsLetterOrDigit(c)) return true;
				if (otherWordChars?.Contains(c) ?? false) return true;
			}
			return false;
		}
	}

	/// <summary>
	/// Returns <see cref="string.Length"/>. Returns 0 if this string is <c>null</c>.
	/// </summary>
	/// <param name="t">This string.</param>
	[DebuggerStepThrough]
	public static int Lenn(this string t) => t?.Length ?? 0;

	/// <summary>
	/// Returns <c>true</c> if this string is <c>null</c> or empty (<c>""</c>).
	/// </summary>
	/// <param name="t">This string.</param>
	[DebuggerStepThrough]
	public static bool NE(this string t) => t == null || t.Length == 0;

	/// <summary>
	/// Returns this string, or <c>null</c> if it is <c>""</c> or <c>null</c>.
	/// </summary>
	/// <param name="t">This string.</param>
	[DebuggerStepThrough]
	internal static string NullIfEmpty_(this string t) => t.NE() ? null : t;
	//not public because probably too rarely used.

	/// <summary>
	/// This function can be used with <c>foreach</c> to split this string into substrings as start/end offsets.
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
	/// <seealso cref="Lines(string, Range, bool, bool, bool)"/>
	[EditorBrowsable(EditorBrowsableState.Never)] //obsolete. Use Split or Lines. They are faster, have "trim" option; the returned array is easier to use and not too expensive. For words use regex.
	public static SegParser Segments(this string t, string separators, SegFlags flags = 0, Range? range = null) {
		return new SegParser(t, separators, flags, range);
	}

#if NET8_0_OR_GREATER //else no ReadOnlySpan.Split
	
	/// <summary>
	/// Splits this string into substrings as start/end offsets.
	/// </summary>
	/// <seealso cref="Split"/>
	/// <seealso cref="SplitS"/>
	public static StartEnd[] Split(this string t, Range range, char separator, StringSplitOptions flags = 0) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		var a = t.AsSpan(start, len).Split(separator, flags);
		return _SplitOffset(a, start);
	}
	
	/// <summary>
	/// Splits this string into substrings as start/end offsets.
	/// </summary>
	/// <seealso cref="Split"/>
	/// <seealso cref="SplitS"/>
	public static StartEnd[] Split(this string t, Range range, string separator, StringSplitOptions flags = 0) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		var a = t.AsSpan(start, len).Split(separator, flags);
		return _SplitOffset(a, start);
	}
	
	/// <summary>
	/// Splits this string into substrings as start/end offsets. Can be used multiple separators.
	/// </summary>
	/// <seealso cref="SplitAny"/>
	/// <seealso cref="SplitAnyS"/>
	public static StartEnd[] Split(this string t, Range range, StringSplitOptions flags, ReadOnlySpan<char> separators) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		var a = t.AsSpan(start, len).SplitAny(separators, flags);
		return _SplitOffset(a, start);
	}
	
	/// <summary>
	/// Splits this string into substrings as start/end offsets. Can be used multiple separators.
	/// </summary>
	/// <seealso cref="SplitAny"/>
	/// <seealso cref="SplitAnyS"/>
	public static StartEnd[] Split(this string t, Range range, StringSplitOptions flags, ReadOnlySpan<string> separators) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		var a = t.AsSpan(start, len).SplitAny(separators, flags);
		return _SplitOffset(a, start);
	}
	
	/// <summary>
	/// Splits this string span into substrings as start/end offsets.
	/// </summary>
	public static StartEnd[] Split(this ReadOnlySpan<char> t, char separator, StringSplitOptions flags = 0)
		=> _Split(t, flags, false, 1, separator).a1;
	
	/// <summary>
	/// Splits this string span into substrings as start/end offsets.
	/// </summary>
	public static StartEnd[] Split(this ReadOnlySpan<char> t, string separator, StringSplitOptions flags = 0)
		=> _Split(t, flags, false, 2, sep23: separator).a1;
	
	/// <summary>
	/// Splits this string span into substrings as start/end offsets. Can be used multiple separators.
	/// </summary>
	public static StartEnd[] SplitAny(this ReadOnlySpan<char> t, ReadOnlySpan<char> separators, StringSplitOptions flags = 0)
		=> _Split(t, flags, false, 3, sep23: separators).a1;
	
	/// <summary>
	/// Splits this string span into substrings as start/end offsets. Can be used multiple separators.
	/// </summary>
	public static StartEnd[] SplitAny(this ReadOnlySpan<char> t, ReadOnlySpan<string> separators, StringSplitOptions flags = 0)
		=> _Split(t, flags, false, 4, sep4: separators).a1;
	
	/// <summary>
	/// Splits this string span into substrings.
	/// </summary>
	public static string[] SplitS(this ReadOnlySpan<char> t, char separator, StringSplitOptions flags = 0)
		=> _Split(t, flags, true, 1, separator).a2;
	
	/// <summary>
	/// Splits this string span into substrings.
	/// </summary>
	public static string[] SplitS(this ReadOnlySpan<char> t, string separator, StringSplitOptions flags = 0)
		=> _Split(t, flags, true, 2, sep23: separator).a2;
	
	/// <summary>
	/// Splits this string span into substrings. Can be used multiple separators.
	/// </summary>
	public static string[] SplitAnyS(this ReadOnlySpan<char> t, ReadOnlySpan<char> separators, StringSplitOptions flags = 0)
		=> _Split(t, flags, true, 3, sep23: separators).a2;
	
	/// <summary>
	/// Splits this string span into substrings. Can be used multiple separators.
	/// </summary>
	public static string[] SplitAnyS(this ReadOnlySpan<char> t, ReadOnlySpan<string> separators, StringSplitOptions flags = 0)
		=> _Split(t, flags, true, 4, sep4: separators).a2;
	
	[SkipLocalsInit]
	static (StartEnd[] a1, string[] a2) _Split(ReadOnlySpan<char> t, StringSplitOptions flags, bool retStr, int sep, char sep1 = default, ReadOnlySpan<char> sep23 = default, ReadOnlySpan<string> sep4 = default) {
		const int na = 100;
		Span<StartEnd> a = stackalloc StartEnd[na];
		Span<Range> a2 = MemoryMarshal.Cast<StartEnd, Range>(a);
		List<StartEnd> list = null;
		for (int add = 0; ;) {
			int n = sep switch { 1 => t.Split(a2, sep1, flags), 2 => t.Split(a2, sep23, flags), 3 => t.SplitAny(a2, sep23, flags), _ => t.SplitAny(a2, sep4, flags) };
			//print.it(n, na);
			if (add > 0) _Offset(a, n, add);
			if (n < na) {
				if (retStr) {
					string[] r;
					if (list != null) {
						r = new string[list.Count + n];
						for (int i = 0; i < list.Count; i++) r[i] = t[list[i].Range].ToString();
						for (int i = 0, j = list.Count; i < n; i++) r[j++] = t[a[i].Range].ToString();
					} else {
						r = new string[n];
						for (int i = 0; i < n; i++) r[i] = t[a[i].Range].ToString();
					}
					return (null, r);
				} else {
					return (list == null ? a[..n].ToArray() : [.. list, .. a[..n]], null);
				}
			}
			int last = a[^1].start;
			t = t[last..];
			add += last;
			(list ??= new(na * 2)).AddRange(a[..^1]);
		}
		
		static void _Offset(Span<StartEnd> ar, int n, int add) {
			foreach (ref var v in ar) {
				v.start += add;
				v.end += add;
			}
		}
	}
	
#endif
	
	static StartEnd[] _SplitOffset(StartEnd[] a, int start) {
		if (start != 0) {
			for (int i = 0; i < a.Length; i++) {
				a[i].start += start;
				a[i].end += start;
			}
		}
		return a;
	}

	/// <summary>
	/// Splits this string into lines.
	/// </summary>
	/// <returns>Array containing lines as strings. Does not include the last empty line, unless <i>preferMore</i> true.</returns>
	/// <param name="t">This string.</param>
	/// <param name="noEmpty">Don't need empty lines.</param>
	/// <param name="preferMore">Add 1 array element if the string ends with a line separator or its length is 0.</param>
	/// <param name="rareNewlines">If <c>false</c> (default), recognizes these newlines: <c>"\r\n"</c>, <c>"\n"</c> and <c>"\r"</c>. If <c>true</c>, also recognizes <c>"\f"</c>, <c>"\x0085"</c>, <c>"\x2028"</c> and <c>"\x2029"</c>.</param>
	/// <seealso cref="StringReader.ReadLine"/>
	public static string[] Lines(this string t, bool noEmpty = false, bool preferMore = false, bool rareNewlines = false)
		=> _Lines(t, noEmpty, preferMore, rareNewlines, true).a2;

	/// <summary>
	/// Splits this string or a range in it into lines as start/end offsets.
	/// </summary>
	/// <returns>Array containing start/end offsets of lines in the string (not in the range). Does not include the last empty line, unless <i>preferMore</i> true.</returns>
	/// <param name="t">This string.</param>
	/// <param name="range">Range of this string. Example: <c>var a = s.Lines(..); //split entire string</c>.</param>
	/// <param name="noEmpty">Don't need empty lines.</param>
	/// <param name="preferMore">Add 1 array element if the string range ends with a line separator or its length is 0.</param>
	/// <param name="rareNewlines">If <c>false</c> (default), recognizes these newlines: <c>"\r\n"</c>, <c>"\n"</c> and <c>"\r"</c>. If <c>true</c>, also recognizes <c>"\f"</c>, <c>"\x0085"</c>, <c>"\x2028"</c> and <c>"\x2029"</c>.</param>
	/// <seealso cref="Lines(ReadOnlySpan{char}, bool, bool, bool)"/>
	public static StartEnd[] Lines(this string t, Range range, bool noEmpty = false, bool preferMore = false, bool rareNewlines = false) {
		var (start, len) = range.GetOffsetAndLength(t.Length);
		var a = t.AsSpan(start, len).Lines(noEmpty, preferMore, rareNewlines);
		return _SplitOffset(a, start);
	}

	/// <summary>
	/// Splits this string into lines as start/end offsets.
	/// </summary>
	/// <returns>Array containing start/end offsets of lines. Does not include the last empty line, unless <i>preferMore</i> true.</returns>
	/// <param name="t">This string.</param>
	/// <param name="noEmpty">Don't need empty lines.</param>
	/// <param name="preferMore">Add 1 array element if the string span ends with a line separator or its length is 0.</param>
	/// <param name="rareNewlines">If <c>false</c> (default), recognizes these newlines: <c>"\r\n"</c>, <c>"\n"</c> and <c>"\r"</c>. If <c>true</c>, also recognizes <c>"\f"</c>, <c>"\x0085"</c>, <c>"\x2028"</c> and <c>"\x2029"</c>.</param>
	public static StartEnd[] Lines(this RStr t, bool noEmpty = false, bool preferMore = false, bool rareNewlines = false)
		=> _Lines(t, noEmpty, preferMore, rareNewlines, false).a1;

	[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static (StartEnd[] a1, string[] a2) _Lines(ReadOnlySpan<char> t, bool noEmpty, bool preferMore, bool rareNewlines, bool retStr) {
		using var f = new FastBuffer<StartEnd>();

		var newline = rareNewlines ? s_newlineAll : s_newlineRN;
		int n = 0;
		for (int pos = 0; ;) {
			if (pos == t.Length && !preferMore) break;
			if (n == f.n) f.More(preserve: true);
			var span = t[pos..];
			int len = span.IndexOfAny(newline);
			if (len < 0) {
				f[n++] = new(pos, t.Length);
				break;
			} else {
				if (!(noEmpty && len == 0)) f[n++] = new(pos, pos + len);
				pos += len + 1;
				if (span[len] == '\r' && pos < t.Length && t[pos] == '\n') pos++;
			}
		}

		if (!retStr) return (new Span<StartEnd>(f.p, n).ToArray(), null);

		var a = new string[n];
		for (int i = 0; i < n; i++) a[i] = t[f[i].Range].ToString();
		return (null, a);
	}

	/// <summary>
	/// Returns the number of lines.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="preferMore">Add 1 if the string ends with a line separator or its length is 0.</param>
	/// <param name="range">Part of this string or <c>null</c> (default).</param>
	/// <param name="rareNewlines">If <c>false</c> (default), recognizes these newlines: <c>"\r\n"</c>, <c>"\n"</c> and <c>"\r"</c>. If <c>true</c>, also recognizes <c>"\f"</c>, <c>"\x0085"</c>, <c>"\x2028"</c> and <c>"\x2029"</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <seealso cref="StringUtil.LineAndColumn"/>
	public static int LineCount(this string t, bool preferMore = false, Range? range = null, bool rareNewlines = false)
		=> LineCount(range is null ? t : t.AsSpan(range.Value), preferMore, rareNewlines);

	/// <inheritdoc cref="LineCount(string, bool, Range?, bool)"/>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static int LineCount(this ReadOnlySpan<char> t, bool preferMore = false, bool rareNewlines = false) {
		var newline = rareNewlines ? s_newlineAll : s_newlineRN;
		int n = 0;
		for (int pos = 0; ;) {
			if (pos == t.Length) {
				if (preferMore) n++;
				break;
			}
			n++;
			var span = t[pos..];
			int len = span.IndexOfAny(newline);
			if (len < 0) break;
			pos += len + 1;
			if (span[len] == '\r' && pos < t.Length && t[pos] == '\n') pos++;
		}
		return n;
	}

#if NET8_0_OR_GREATER
	static readonly SearchValues<char> s_newlineRN = SearchValues.Create(['\r', '\n']),
		s_newlineAll = SearchValues.Create(['\r', '\n', '\f', '\x85', '\x2028', '\x2029']);
#else
	static readonly char[] s_newlineRN = ['\r', '\n'],
		s_newlineAll = ['\r', '\n', '\f', '\x85', '\x2028', '\x2029'];
#endif

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
	/// <param name="culture">Culture, for example <c>CultureInfo.CurrentCulture</c>. If <c>null</c> (default) uses invariant culture.</param>
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

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static long _ToInt(RStr t, int startIndex, out int numberEndIndex, bool toLong, STIFlags flags) {
		numberEndIndex = 0;

		int len = t.Length;
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
		if (c >= '\x61C' && c is '\x61C' or '\x200E' or '\x200F') {
			if (++i == len) return 0;
			c = t[i];
		}

		//skip -+
		bool minus = false;
		if (c is '-' or '−' or '+') {
			if (++i == len) return 0;
			if (c != '+') minus = true;
			c = t[i];
		}

		//is hex?
		bool isHex = false;
		switch (flags & (STIFlags.NoHex | STIFlags.IsHexWithout0x)) {
		case 0:
			if (c == '0' && i <= len - 3)
				if (isHex = t[i + 1] is 'x' or 'X' && t[i + 2] is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f')) i += 2;
			break;
		case STIFlags.IsHexWithout0x:
			isHex = true;
			break;
		}

		//skip '0'
		int i0 = i;
		while (i < len && t[i] == '0') i++;

		long R = 0; //result

		int nDigits = 0;
		if (isHex) {
			int nMaxDigits = toLong ? 16 : 8;
			for (; i < len; i++) {
				int k = _CharHexToDec(t[i]); if (k < 0) break;
				if (++nDigits > nMaxDigits) return 0;
				R = (R << 4) + k;
			}
		} else { //decimal or not a number
			int nMaxDigits = toLong ? 20 : 10;
			for (; i < len; i++) {
				int k = t[i] - '0'; if (k < 0 || k > 9) break;
				R = R * 10 + k;
				//is too long?
				if (++nDigits >= nMaxDigits) {
					if (nDigits > nMaxDigits) return 0;
					if (toLong) {
						if (t.Slice(i + 1 - nDigits).CompareTo("18446744073709551615", StringComparison.Ordinal) > 0) return 0;
					} else {
						if (R > uint.MaxValue) return 0;
					}
				}
			}
		}

		if (i == i0) return 0; //not a number
		numberEndIndex = i;
		return minus ? -R : R;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static int _CharHexToDec(char c) {
		if (c >= '0' && c <= '9') return c - '0';
		if (c >= 'A' && c <= 'F') return c - ('A' - 10);
		if (c >= 'a' && c <= 'f') return c - ('a' - 10);
		return -1;
	}

	/// <summary>
	/// Converts part of this string to <b>int</b> number and gets the number end index.
	/// </summary>
	/// <returns>The number, or 0 if failed to convert.</returns>
	/// <param name="t">This string. Can be <c>null</c>.</param>
	/// <param name="startIndex">Offset in this string where to start parsing.</param>
	/// <param name="numberEndIndex">Receives offset in this string where the number part ends. If fails to convert, receives 0.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentOutOfRangeException"><i>startIndex</i> is less than 0 or greater than string length.</exception>
	/// <remarks>
	/// Fails to convert when string is <c>null</c>, <c>""</c>, does not start with a number or the number is too big.
	/// 
	/// Unlike <b>int.Parse</b> and <b>Convert.ToInt32</b>:
	/// - The number in string can be followed by more text, like <c>"123text"</c>.
	/// - Has <i>startIndex</i> parameter that allows to get number from middle, like <c>"text123text"</c>.
	/// - Gets the end of the number part.
	/// - No exception when cannot convert.
	/// - The number can be decimal (like <c>"123"</c>) or hexadecimal (like <c>"0x1A"</c>); don't need separate flags for each style.
	/// - Does not depend on current culture. As minus sign recognizes <c>'-'</c> and <c>'−'</c>.
	/// - Faster.
	/// 
	/// The number in string can start with ASCII whitespace (spaces, newlines, etc), like <c>" 5"</c>.
	/// The number in string can be with <c>"-"</c> or <c>"+"</c>, like <c>"-5"</c>, but not like <c>"- 5"</c>.
	/// Fails if the number is greater than +- <b>uint.MaxValue</b> (0xffffffff).
	/// The return value becomes negative if the number is greater than <b>int.MaxValue</b>, for example <c>"0xffffffff"</c> is -1, but it becomes correct if assigned to <b>uint</b> (need cast).
	/// Does not support non-integer numbers; for example, for <c>"3.5E4"</c> returns 3 and sets <c>numberEndIndex=startIndex+1</c>.
	/// </remarks>
	public static int ToInt(this string t, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		return (int)_ToInt(t, startIndex, out numberEndIndex, false, flags);
	}

	/// <summary>
	/// Converts part of this string to <b>int</b> number.
	/// </summary>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, int, out int, STIFlags)"/>
	public static int ToInt(this string t, int startIndex = 0, STIFlags flags = 0) {
		return (int)_ToInt(t, startIndex, out _, false, flags);
	}

	/// <returns><c>false</c> if failed.</returns>
	/// <param name="result">Receives the result, or 0 if failed.</param>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, int, out int, STIFlags)"/>
	public static bool ToInt(this string t, out int result, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		result = (int)_ToInt(t, startIndex, out numberEndIndex, false, flags);
		return numberEndIndex != 0;
	}

	/// <returns><c>false</c> if failed.</returns>
	/// <param name="result">Receives the result, or 0 if failed.</param>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, int, STIFlags)"/>
	public static bool ToInt(this string t, out int result, int startIndex = 0, STIFlags flags = 0)
		=> ToInt(t, out result, startIndex, out _, flags);

	/// <summary>
	/// Converts part of this string to <b>uint</b> number and gets the number end index.
	/// </summary>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, out int, int, out int, STIFlags)"/>
	public static bool ToInt(this string t, out uint result, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		result = (uint)_ToInt(t, startIndex, out numberEndIndex, false, flags);
		return numberEndIndex != 0;
	}

	/// <summary>
	/// Converts part of this string to <b>uint</b> number.
	/// </summary>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, out int, int, STIFlags)"/>
	public static bool ToInt(this string t, out uint result, int startIndex = 0, STIFlags flags = 0)
		=> ToInt(t, out result, startIndex, out _, flags);

	/// <summary>
	/// Converts part of this string to <b>long</b> number and gets the number end index.
	/// </summary>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, out int, int, out int, STIFlags)"/>
	public static bool ToInt(this string t, out long result, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		result = _ToInt(t, startIndex, out numberEndIndex, true, flags);
		return numberEndIndex != 0;
	}

	/// <summary>
	/// Converts part of this string to <b>long</b> number.
	/// </summary>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, out int, int, STIFlags)"/>
	public static bool ToInt(this string t, out long result, int startIndex = 0, STIFlags flags = 0)
		=> ToInt(t, out result, startIndex, out _, flags);

	/// <summary>
	/// Converts part of this string to <b>ulong</b> number and gets the number end index.
	/// </summary>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, out int, int, out int, STIFlags)"/>
	public static bool ToInt(this string t, out ulong result, int startIndex, out int numberEndIndex, STIFlags flags = 0) {
		result = (ulong)_ToInt(t, startIndex, out numberEndIndex, true, flags);
		return numberEndIndex != 0;
	}

	/// <summary>
	/// Converts part of this string to <b>ulong</b> number.
	/// </summary>
	/// <remarks></remarks>
	/// <inheritdoc cref="ToInt(string, out int, int, STIFlags)"/>
	public static bool ToInt(this string t, out ulong result, int startIndex = 0, STIFlags flags = 0)
		=> ToInt(t, out result, startIndex, out _, flags);

	//FUTURE: make these public and add more overloads.
	internal static int ToInt_(this RStr t, STIFlags flags = 0)
		=> (int)_ToInt(t, 0, out _, false, flags);

	internal static bool ToInt_(this RStr t, out int result, out int numberEndIndex, STIFlags flags = 0) {
		result = (int)_ToInt(t, 0, out numberEndIndex, false, flags);
		return numberEndIndex != 0;
	}

	/// <summary>
	/// Converts this string or its part to double number.
	/// </summary>
	/// <returns>The number, or 0 if failed to convert.</returns>
	/// <param name="t">This string. Can be <c>null</c>.</param>
	/// <param name="range">Part of this string or <c>null</c> (default).</param>
	/// <param name="style">The permitted number format in the string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid <i>style</i>.</exception>
	/// <remarks>
	/// Calls <see cref="double.TryParse(RStr, NumberStyles, IFormatProvider, out double)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// Fails if the string is <c>null</c> or <c>""</c> or isn't a valid floating-point number.
	/// Examples of valid numbers: <c>"12"</c>, <c>" -12.3 "</c>, <c>".12"</c>, <c>"12."</c>, <c>"12E3"</c>, <c>"12.3e-45"</c>, <c>"1,234.5"</c> (with <i>style</i> <c>NumberStyles.Float | NumberStyles.AllowThousands</c>). String like <c>"2text"</c> is invalid, unless <i>range</i> is <c>0..1</c>.
	/// </remarks>
	public static double ToNumber(this string t, Range? range = null, NumberStyles style = NumberStyles.Float) {
		ToNumber(t, out double r, range, style);
		return r;
	}

	/// <returns><c>false</c> if failed.</returns>
	/// <param name="result">Receives the result, or 0 if failed.</param>
	/// <inheritdoc cref="ToNumber(string, Range?, NumberStyles)"/>
	public static bool ToNumber(this string t, out double result, Range? range = null, NumberStyles style = NumberStyles.Float) {
		return double.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	/// <summary>
	/// Converts this string or its part to float number.
	/// </summary>
	/// <remarks>
	/// Calls <see cref="float.TryParse(RStr, NumberStyles, IFormatProvider, out float)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	/// <inheritdoc cref="ToNumber(string, out double, Range?, NumberStyles)"/>
	public static bool ToNumber(this string t, out float result, Range? range = null, NumberStyles style = NumberStyles.Float) {
		return float.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	/// <summary>
	/// Converts this string or its part to <b>int</b> number.
	/// </summary>
	/// <remarks>
	/// Calls <see cref="int.TryParse(RStr, NumberStyles, IFormatProvider, out int)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	/// <inheritdoc cref="ToNumber(string, out double, Range?, NumberStyles)"/>
	public static bool ToNumber(this string t, out int result, Range? range = null, NumberStyles style = NumberStyles.Integer) {
		return int.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);

		//note: exception if NumberStyles.Integer | NumberStyles.AllowHexSpecifier.
		//	Can parse either decimal or hex, not any.
		//	Does not support "0x". With AllowHexSpecifier eg "11" is 17, but "0x11" is invalid.
	}

	/// <summary>
	/// Converts this string or its part to <b>uint</b> number.
	/// </summary>
	/// <remarks>
	/// Calls <see cref="uint.TryParse(RStr, NumberStyles, IFormatProvider, out uint)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	/// <inheritdoc cref="ToNumber(string, out double, Range?, NumberStyles)"/>
	public static bool ToNumber(this string t, out uint result, Range? range = null, NumberStyles style = NumberStyles.Integer) {
		return uint.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	/// <summary>
	/// Converts this string or its part to <b>long</b> number.
	/// </summary>
	/// <remarks>
	/// Calls <see cref="long.TryParse(RStr, NumberStyles, IFormatProvider, out long)"/> with <see cref="CultureInfo"/> <b>InvariantCulture</b>.
	/// </remarks>
	/// <inheritdoc cref="ToNumber(string, out double, Range?, NumberStyles)"/>
	public static bool ToNumber(this string t, out long result, Range? range = null, NumberStyles style = NumberStyles.Integer) {
		return long.TryParse(_NumSpan(t, range, out var ci), style, ci, out result);
	}

	/// <summary>
	/// Converts this string or its part to <b>ulong</b> number.
	/// </summary>
	/// <remarks>
	/// Calls <see cref="ulong.TryParse(RStr, NumberStyles, IFormatProvider, out ulong)"/>. Uses <see cref="CultureInfo.InvariantCulture"/> if the string range contains only ASCII characters, else uses current culture.
	/// </remarks>
	/// <inheritdoc cref="ToNumber(string, out double, Range?, NumberStyles)"/>
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
	/// <exception cref="ArgumentNullException"><i>suffix</i> is <c>null</c>.</exception>
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
	/// <exception cref="ArgumentNullException"><i>suffix</i> is <c>null</c>.</exception>
	public static string RemoveSuffix(this string t, char suffix) {
		if (!t.Ends(suffix)) return t;
		return t[..^1];
	}

	/// <summary>
	/// If this string is longer than <i>limit</i>, returns its substring 0 to <i>limit</i>-1 with appended <c>'…'</c> character.
	/// Else returns this string.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <param name="limit">Maximal length of the result string. If less than 1, uses 1.</param>
	/// <param name="middle">Let <c>"…"</c> be in the middle. For example it is useful when the string is a file path, to avoid removing the filename.</param>
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
	/// <param name="limit">If the final string is longer than <i>limit</i>, get its substring 0 to <i>limit</i>-1 with appended <c>'…'</c> character. The enclosing <c>""</c> are not counted.</param>
	/// <param name="quote">Enclose in <c>""</c>.</param>
	/// <remarks>
	/// Replaces these characters: <c>'\\'</c>, <c>'"'</c>, <c>'\t'</c>, <c>'\n'</c>, <c>'\r'</c> and all in range 0-31.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static string Escape(this string t, int limit = 0, bool quote = false) {
		int i, len = t.Length;
		if (len == 0) return quote ? "\"\"" : t;

		if (limit > 0) {
			if (len > limit) len = limit - 1; else limit = 0;
		}

		for (i = 0; i < len; i++) {
			var c = t[i];
			if (c < ' ' || c == '\\' || c == '"') goto g1;
			//tested: Unicode line-break chars in most controls don't break lines, therefore don't need to escape
		}
		if (limit > 0) t = Limit(t, limit);
		if (quote) t = "\"" + t + "\"";
		return t;
		g1:
		using (new StringBuilder_(out var b, len + len / 16 + 100)) {
			if (quote) b.Append('"');
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
				else if (c == '"') b.Append("\\\"");
				else b.Append(c);

				if (limit > 0 && b.Length - (quote ? 1 : 0) >= len) break;
			}

			if (limit > 0) b.Append('…');
			if (quote) b.Append('"');
			return b.ToString();
		}
	}

	/// <summary>
	/// Replaces C# escape sequences to characters in this string.
	/// </summary>
	/// <returns><c>false</c> if the string contains an invalid or unsupported escape sequence.</returns>
	/// <param name="t">This string.</param>
	/// <param name="result">Receives the result string. It is this string if there are no escape sequences or if failed.</param>
	/// <remarks>
	/// Supports all escape sequences of <see cref="Escape"/>: <c>\\</c> <c>\"</c> <c>\t</c> <c>\n</c> <c>\r</c> <c>\0</c> <c>\uXXXX</c>.
	/// Does not support <c>\a</c> <c>\b</c> <c>\f</c> <c>\v</c> <c>\'</c> <c>\xXXXX</c> <c>\UXXXXXXXX</c>.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
					case '\\': case '"': break;
					case 't': c = '\t'; break;
					case 'n': c = '\n'; break;
					case 'r': c = '\r'; break;
					case '0': c = '\0'; break;
					//case 'a': c = '\a'; break;
					//case 'b': c = '\b'; break;
					//case 'f': c = '\f'; break;
					//case 'v': c = '\v'; break;
					//case 'e': c = '\e'; break;
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

	/// <returns>New string if replaced, else this string. Does not replace if the string does not contain escape sequences or if some escape sequences are invalid or unsupported.</returns>
	/// <inheritdoc cref="Unescape(string, out string)"/>
	public static string Unescape(this string t) {
		Unescape(t, out t);
		return t;
	}

	/// <summary>
	/// Reverses this string, like <c>"Abc"</c> -> <c>"cbA"</c>.
	/// </summary>
	/// <returns>The result string.</returns>
	/// <param name="t"></param>
	/// <param name="raw">Ignore <b>char</b> sequences such as Unicode surrogates and grapheme clusters. Faster, but if the string contains these sequences, the result string is incorrect.</param>
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
	/// Returns <c>true</c> if does not contain non-ASCII characters.
	/// </summary>
	/// <seealso cref="IsAscii(RStr)"/>
	public static bool IsAscii(this string t) => t.AsSpan().IsAscii();

	/// <summary>
	/// Returns <c>true</c> if does not contain non-ASCII characters.
	/// </summary>
	public static bool IsAscii(this RStr t) => !t.ContainsAnyExceptInRange((char)0, (char)127);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int LengthThrowIfNull_(this RStr t) {
		int n = t.Length;
		if (n == 0 && t == default) throw new ArgumentNullException();
		return n;
	}

	/// <summary>
	/// Returns <c>true</c> if equals to string <i>s</i>, case-sensitive.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="s">Other string. Can be <c>null</c>.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Eq(this RStr t, RStr s) => t.Equals(s, StringComparison.Ordinal);

	/// <summary>
	/// Returns <c>true</c> if equals to string <i>s</i>, case-insensitive.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="s">Other string. Can be <c>null</c>.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Eqi(this RStr t, RStr s) => t.Equals(s, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Compares part of this span with string <i>s</i>. Returns <c>true</c> if equal.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="startIndex">Offset in this span. If invalid, returns <c>false</c>.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
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
	/// Returns <c>true</c> if the specified character is at the specified position in this span.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="index">Offset in this span. If invalid, returns <c>false</c>.</param>
	/// <param name="c">Character.</param>
	public static bool Eq(this RStr t, int index, char c) {
		if ((uint)index >= t.Length) return false;
		return t[index] == c;
	}

	/// <summary>
	/// Returns <c>true</c> if starts with string <i>s</i>.
	/// </summary>
	/// <param name="t">This span.</param>
	/// <param name="s">Other string.</param>
	/// <param name="ignoreCase">Case-insensitive.</param>
	/// <remarks>
	/// Uses ordinal comparison (does not depend on current culture/locale).
	/// </remarks>
	public static bool Starts(this RStr t, RStr s, bool ignoreCase = false) => t.StartsWith(s, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

	/// <summary>
	/// Returns <c>true</c> if starts with string <i>s</i>.
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
	/// </summary>
	/// <returns>Character index in this span, or -1 if not found.</returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static int IndexOf(this RStr t, int index, char c) {
		int i = t[index..].IndexOf(c);
		return i < 0 ? i : i + index;
	}

	/// <summary>
	/// Finds character <i>c</i> in <i>range</i> of this span.
	/// </summary>
	/// <returns>Character index in this span, or -1 if not found.</returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static int IndexOf(this RStr t, Range range, char c) {
		int i = t[range].IndexOf(c);
		if (i < 0) return i;
		return i + range.Start.GetOffset(t.Length);
	}

	/// <summary>
	/// Finds string <i>s</i> in this span, starting from <i>index</i>.
	/// </summary>
	/// <returns>Character index in this span, or -1 if not found.</returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
	public static int IndexOf(this RStr t, int index, RStr s, bool ignoreCase = false) {
		if (s == default) throw new ArgumentNullException();
		int i = t[index..].IndexOf(s, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		return i < 0 ? i : i + index;
	}

	/// <summary>
	/// Finds string <i>s</i> in <i>range</i> of this span.
	/// </summary>
	/// <returns>Character index in this span, or -1 if not found.</returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="ArgumentNullException"><i>s</i> is <c>null</c>.</exception>
	public static int IndexOf(this RStr t, Range range, RStr s, bool ignoreCase = false) {
		if (s == default) throw new ArgumentNullException();
		int i = t[range].IndexOf(s, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		if (i < 0) return i;
		return i + range.Start.GetOffset(t.Length);
	}

#if !NET8_0_OR_GREATER
	/// <summary>
	/// Creates read-only span from range of this string.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"/>
	public static RStr AsSpan(this string t, Range r) {
		var (i, len) = r.GetOffsetAndLength(t.Length);
		return t.AsSpan(i, len);
	}
#endif

	internal static void CopyTo_(this string t, char* p)
		=> t.AsSpan().CopyTo(new Span<char>(p, t.Length));

	/// <summary>
	/// Converts to UTF-8 (<c>Encoding.UTF8.GetBytes</c>).
	/// </summary>
	public static byte[] ToUTF8(this string t)
		=> Encoding.UTF8.GetBytes(t);

	/// <summary>
	/// Converts to UTF-8.
	/// </summary>
	/// <param name="append0">Return 0-terminated UTF-8 string.</param>
	public static byte[] ToUTF8(this RStr t, bool append0 = false)
		=> Convert2.Utf8Encode(t, append0 ? "\0" : null);

	/// <summary>
	/// Converts to UTF-8.
	/// </summary>
	/// <param name="append0">Return 0-terminated UTF-8 string.</param>
	public static byte[] ToUTF8(this Span<char> t, bool append0 = false)
		=> Convert2.Utf8Encode(t, append0 ? "\0" : null);

	/// <summary>
	/// Converts UTF-8 string to string.
	/// </summary>
	public static string ToStringUTF8(this byte[] t)
		=> Encoding.UTF8.GetString(t);

	/// <summary>
	/// Converts UTF-8 string to string.
	/// </summary>
	public static string ToStringUTF8(this RByte t)
		=> Encoding.UTF8.GetString(t);

	/// <summary>
	/// Converts UTF-8 string to string.
	/// </summary>
	public static string ToStringUTF8(this Span<byte> t)
		=> Encoding.UTF8.GetString(t);

	/// <summary>
	/// Splits this string. Trims, and removes empty.
	/// </summary>
	/// <returns>Array containing 0 or more strings.</returns>
	internal static string[] Split_(this string t, char c) {
		return t.Split(c, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
	}

	/// <summary>
	/// Splits this string and creates <b>HashSet</b>. Trims, and removes empty.
	/// </summary>
	/// <returns><b>HashSet</b> containing 0 or more strings.</returns>
	internal static HashSet<string> SplitHS_(this string t, char c, bool ignoreCase) {
		return t.Split_(c).ToHashSet(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
	}

	/// <summary>
	/// Splits string <i>s</i> into 2 strings.
	/// Tip: there are 2 overloads: for string and for <b>RStr</b>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="c">Separator character.</param>
	/// <param name="s1">String at the left.</param>
	/// <param name="s2">String at the right.</param>
	/// <param name="minLen1">Minimal <i>s1</i> length.</param>
	/// <param name="minLen2">Minimal <i>s2</i> length.</param>
	/// <returns>Returns <c>false</c> if character <i>c</i> not found or if strings are too short.</returns>
	/// <remarks>
	///	Can be used to split strings like <c>"name=value"</c> or <c>"name: value"</c> or <c>"s1 s2"</c> etc. Trims spaces.
	/// </remarks>
	internal static bool Split2_(this string t, char c, out string s1, out string s2, int minLen1, int minLen2) {
		if (!Split2_(t, c, out RStr k1, out RStr k2, minLen1, minLen2)) {
			s1 = s2 = null;
			return false;
		}
		s1 = k1.ToString();
		s2 = k2.ToString();
		return true;
	}

	/// <inheritdoc cref="Split2_(string, char, out string, out string, int, int)"/>
	internal static bool Split2_(this RStr t, char c, out RStr s1, out RStr s2, int minLen1, int minLen2) {
		t = t.Trim();
		int i = t.IndexOf(c);
		if (i >= 0) {
			s1 = t[..i].TrimEnd();
			s2 = t[++i..].TrimStart();
			if (s1.Length >= minLen1 && s2.Length >= minLen2) return true;
		}
		s1 = default;
		s2 = default;
		return false;
	}

	/// <summary>
	/// If <i>index</i> is in this string, returns character at <i>index</i>. Else <c>'\0'</c>.
	/// </summary>
	internal static char At_(this string t, int index) => (uint)index < t.Length ? t[index] : default;

	/// <summary>
	/// If <i>index</i> is in this string span, returns character at <i>index</i>. Else <c>'\0'</c>.
	/// </summary>
	internal static char At_(this RStr t, int index) => (uint)index < t.Length ? t[index] : default;
}

/// <summary>
/// Flags for <see cref="ExtString.ToInt"/> and similar functions.
/// </summary>
[Flags]
public enum STIFlags {
	/// <summary>
	/// Don't support hexadecimal numbers (numbers with prefix <c>"0x"</c>).
	/// </summary>
	NoHex = 1,

	/// <summary>
	/// The number in string is hexadecimal without a prefix, like <c>"1A"</c>.
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
