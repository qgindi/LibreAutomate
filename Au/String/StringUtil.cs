using System.Text.Json;

namespace Au.More;

/// <summary>
/// Miscellaneous rarely used string functions. Parsing etc.
/// </summary>
public static class StringUtil {
	/// <summary>
	/// Parses a function parameter that can optionally have a <c>"***name "</c> prefix, like <c>"***value xyz"</c>.
	/// </summary>
	/// <returns>0 - <i>s</i> does not start with <c>"***"</c>; <c>i+1</c> - <i>s</i> starts with <c>"***names[i] "</c>; -1 - <i>s</i> is invalid.</returns>
	/// <param name="s">Parameter. If starts with <c>"***"</c> and is valid, receives the <c>"value"</c> part; else unchanged. Can be <c>null</c>.</param>
	/// <param name="names">List of supported <c>"name"</c>.</param>
	/// <remarks>
	/// Used to parse parameters like <i>name</i> of <see cref="wnd.Child"/>.
	/// </remarks>
	internal static int ParseParam3Stars(ref string s, params string[] names) {
		if (s == null || !s.Starts("***")) return 0;
		for (int i = 0; i < names.Length; i++) {
			var ni = names[i];
			if (s.Length - 3 <= ni.Length || !s.Eq(3, ni)) continue;
			int j = 3 + ni.Length;
			char c = s[j]; if (c != ' ') break;
			s = s[(j + 1)..];
			return i + 1;
		}
		return -1;
	}
	
	/// <summary>
	/// Removes characters used to underline next character when the text is displayed in UI. Replaces two such characters with single.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	/// <param name="underlineChar"></param>
	/// <remarks>
	/// Character <c>'&amp;'</c> (in WPF <c>'_'</c>) is used to underline next character in displayed text of dialog controls and menu items. Two such characters are used to display single.
	/// The underline is displayed when using the keyboard with <c>Alt</c> key to select dialog controls and menu items.
	/// </remarks>
	[SkipLocalsInit]
	public static unsafe string RemoveUnderlineChar(string s, char underlineChar = '&') {
		if (s != null && s.Contains(underlineChar)) {
			using FastBuffer<char> b = new(s.Length);
			int j = 0; bool was = false;
			for (int i = 0; i < s.Length; i++) {
				if (s[i] == underlineChar) {
					if (i < s.Length - 1 && s[i + 1] == underlineChar) i++;
					else if (!was) { was = underlineChar == '_'; continue; } //WPF removes only first single _
				}
				b.p[j++] = s[i];
			}
			s = new string(b.p, 0, j);
		}
		return s;
	}
	
	/// <summary>
	/// Finds character used to underline next character when the text is displayed in UI.
	/// </summary>
	/// <returns>Character index, or -1 if not found.</returns>
	/// <param name="s">Can be <c>null</c>.</param>
	/// <param name="underlineChar"></param>
	public static int FindUnderlineChar(string s, char underlineChar = '&') {
		if (s != null) {
			for (int i = 0; i < s.Length; i++) {
				if (s[i] == underlineChar) {
					if (++i < s.Length && s[i] != underlineChar) return i;
				}
			}
		}
		return -1;
	}
	
	/// <summary>
	/// Converts array of command line arguments to string that can be passed to a "start process" function, for example <see cref="run.it"/>, <see cref="Process.Start"/>.
	/// </summary>
	/// <returns><c>null</c> if <i>a</i> is <c>null</c> or empty.</returns>
	/// <param name="a"></param>
	public static string CommandLineFromArray(string[] a) {
		if (a == null || a.Length == 0) return null;
		StringBuilder b = null;
		foreach (var v in a) {
			int esc = 0;
			if (v.NE()) esc = 1; else if (v.Contains('"')) esc = 2; else foreach (var c in v) if (c <= ' ') { esc = 1; break; }
			if (esc == 0 && a.Length == 1) return a[0];
			if (b == null) b = new StringBuilder(); else b.Append(' ');
			if (esc == 0) b.Append(v);
			else {
				b.Append('"');
				var s = v;
				if (esc == 2) {
					if (s.Find(@"\""") < 0) s = s.Replace(@"""", @"\""");
					else s = s.RxReplace(@"(\\*)""", @"$1$1\""");
				}
				if (s.Ends('\\')) s = s.RxReplace(@"(\\+)$", "$1$1");
				b.Append(s).Append('"');
			}
		}
		return b.ToString();
	}
	
	/// <summary>
	/// Parses command line arguments.
	/// Calls API <msdn>CommandLineToArgvW</msdn>.
	/// </summary>
	/// <returns>Empty array if <i>s</i> is <c>null</c> or <c>""</c>.</returns>
	public static unsafe string[] CommandLineToArray(string s) {
		if (s.NE()) return Array.Empty<string>();
		char** p = Api.CommandLineToArgvW(s, out int n);
		var a = new string[n];
		for (int i = 0; i < n; i++) a[i] = new string(p[i]);
		Api.LocalFree(p);
		return a;
	}
	
	/// <summary>
	/// If string contains a number at <i>startIndex</i>, gets that number as <b>int</b>, also gets the string part that follows it, and returns <c>true</c>.
	/// For example, for string <c>"25text"</c> or <c>"25 text"</c> gets <i>num</i> = <c>25</c>, <i>tail</i> = <c>"text"</c>.
	/// Everything else is the same as with <see cref="ExtString.ToInt(string, int, out int, STIFlags)"/>.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="num">Receives the number. Receives 0 if no number.</param>
	/// <param name="tail">Receives the string part that follows the number, or <c>""</c>. Receives <c>null</c> if no number. Can be this variable.</param>
	/// <param name="startIndex">Offset in this string where to start parsing.</param>
	/// <param name="flags"></param>
	public static bool ParseIntAndString(string s, out int num, out string tail, int startIndex = 0, STIFlags flags = 0) {
		num = s.ToInt(startIndex, out int end, flags);
		if (end == 0) {
			tail = null;
			return false;
		}
		if (end < s.Length && s[end] == ' ') end++;
		tail = s[end..];
		return true;
	}
	
	/// <summary>
	/// Creates <b>int[]</b> from string containing space-separated numbers, like <c>"4 100 -8 0x10"</c>.
	/// </summary>
	/// <param name="s">Decimal or/and hexadecimal numbers separated by single space. If <c>null</c> or <c>""</c>, returns empty array.</param>
	/// <remarks>
	/// For vice versa use <c>string.Join(" ", array)</c>.
	/// </remarks>
	public static int[] StringToIntArray(string s) {
		if (s.NE()) return Array.Empty<int>();
		int n = 1; foreach (var v in s) if (v == ' ') n++;
		var a = new int[n];
		a[0] = s.ToInt(0, STIFlags.DontSkipSpaces);
		for (int i = 0, j = 0; j < s.Length;) if (s[j++] == ' ') a[++i] = s.ToInt(j, STIFlags.DontSkipSpaces);
		return a;
	}
	
	/// <summary>
	/// Converts character index in string to line index and character index in that line.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="index">Character index in string <i>s</i>.</param>
	/// <param name="lineIndex">Receives 0-based line index.</param>
	/// <param name="indexInLine">Receives 0-based character index in that line.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static void LineAndColumn(string s, int index, out int lineIndex, out int indexInLine) {
		if ((uint)index > s.Length) throw new ArgumentOutOfRangeException();
		int line = 0, lineStart = 0;
		for (int i = 0; i < index; i++) {
			char c = s[i];
			if (c > '\r') continue;
			if (c != '\n') {
				if (c != '\r') continue;
				if (i < s.Length - 1 && s[i + 1] == '\n') continue;
			}
			
			lineStart = i + 1;
			line++;
		}
		lineIndex = line;
		indexInLine = index - lineStart;
	}
	
	/// <summary>
	/// Calculates the Levenshtein distance between two strings, which tells how much they are different.
	/// </summary>
	/// <remarks>
	/// It is the number of character edits (removals, inserts, replacements) that must occur to get from string <i>s1</i> to string <i>s2</i>.
	/// Can be used to measure similarity and match approximate strings with fuzzy logic.
	/// Uses code and info from <see href="https://www.dotnetperls.com/levenshtein"/>.
	/// </remarks>
	public static int LevenshteinDistance(string s1, string s2) {
		int n = s1.Length;
		int m = s2.Length;
		
		// Step 1
		if (n == 0) return m;
		if (m == 0) return n;
		
		// Step 2
		int[,] d = new int[n + 1, m + 1];
		for (int i = 0; i <= n; d[i, 0] = i++) { }
		for (int j = 0; j <= m; d[0, j] = j++) { }
		
		// Step 3
		for (int i = 1; i <= n; i++) {
			//Step 4
			for (int j = 1; j <= m; j++) {
				// Step 5
				int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
				
				// Step 6
				d[i, j] = Math.Min(
					Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
					d[i - 1, j - 1] + cost);
			}
		}
		// Step 7
		return d[n, m];
	}
	
	/// <summary>
	/// Returns the number of characters common to the start of each string.
	/// </summary>
	public static int CommonPrefix(string s1, string s2) {
		int n = Math.Min(s1.Length, s2.Length);
		for (int i = 0; i < n; i++) {
			if (s1[i] != s2[i]) return i;
		}
		return n;
	}
	
	/// <summary>
	/// Returns the number of characters common to the end of each string.
	/// </summary>
	public static int CommonSuffix(string s1, string s2) {
		int len1 = s1.Length;
		int len2 = s2.Length;
		int n = Math.Min(len1, len2);
		for (int i = 1; i <= n; i++) {
			if (s1[len1 - i] != s2[len2 - i]) return i - 1;
		}
		return n;
	}
	
	/// <summary>
	/// Converts JSON element to multiline indented JSON string.
	/// </summary>
	public static string JsonMultiline(JsonElement json) {
		var so = s_jsOptions.Value;
		return JsonSerializer.Serialize(json, so);
	}
	
	/// <summary>
	/// Converts single-line JSON string to multiline indented JSON string.
	/// </summary>
	public static string JsonMultiline(string json) {
		var so = s_jsOptions.Value;
		var v = JsonSerializer.Deserialize<JsonElement>(json, so);
		return JsonSerializer.Serialize(v, so);
	}
	
	static readonly Lazy<JsonSerializerOptions> s_jsOptions = new(() => new() {
		WriteIndented = true
		//Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	});
	
	internal static JsonSerializerOptions JsonOptions => s_jsOptions.Value;
	
	/// <summary>Calls <see cref="Encoding.GetEncoding(string)"/>, and <b>Encoding.RegisterProvider</b> if need.</summary>
	/// <returns><c>null</c> if failed.</returns>
	public static Encoding GetEncoding(string name) => _GetEncoding(name ?? throw new ArgumentNullException());
	
	/// <summary>Calls <see cref="Encoding.GetEncoding(int)"/>, and <b>Encoding.RegisterProvider</b> if need.</summary>
	/// <returns><c>null</c> if failed.</returns>
	public static Encoding GetEncoding(int codepage) => _GetEncoding(null, codepage);
	
	static Encoding _GetEncoding(string name = null, int codepage = 0) {
		try { return name != null ? Encoding.GetEncoding(name) : Encoding.GetEncoding(codepage); }
		catch {
			if (Interlocked.CompareExchange(ref s_encodingInited, 1, 0) == 0) {
				Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
				try { return name != null ? Encoding.GetEncoding(name) : Encoding.GetEncoding(codepage); }
				catch {  }
			}
		}
		return null;
	}
	static int s_encodingInited;
}
