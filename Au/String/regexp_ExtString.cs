namespace Au.Types;

public static partial class ExtString
{
	/// <summary>
	/// Returns <c>true</c> if this string matches PCRE regular expression <i>rx</i>.
	/// </summary>
	/// <param name="t">This string. If <c>null</c>, returns <c>false</c>.</param>
	/// <exception cref="ArgumentException">Invalid regular expression.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.IsMatch"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static bool RxIsMatch(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		RXFlags flags = 0, Range? range = null) {
		var x = _cache.AddOrGet(rx, flags);
		return x.IsMatch(t, range);
	}

	/// <summary>
	/// Returns <c>true</c> if this string matches PCRE regular expression <i>rx</i>.
	/// Gets match info as <see cref="RXMatch"/>.
	/// </summary>
	/// <param name="t">This string. If <c>null</c>, returns <c>false</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Match(string, out RXMatch, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static bool RxMatch(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		out RXMatch result, RXFlags flags = 0, Range? range = null) {
		var x = _cache.AddOrGet(rx, flags);
		return x.Match(t, out result, range);
	}

	/// <summary>
	/// Returns <c>true</c> if this string matches PCRE regular expression <i>rx</i>.
	/// Gets whole match or some group, as string.
	/// </summary>
	/// <param name="t">This string. If <c>null</c>, returns <c>false</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>group</i> or <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Match(string, int, out string, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static bool RxMatch(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		int group, out string result, RXFlags flags = 0, Range? range = null) {
		var x = _cache.AddOrGet(rx, flags);
		return x.Match(t, group, out result, range);
	}

	/// <summary>
	/// Returns <c>true</c> if this string matches PCRE regular expression <i>rx</i>.
	/// Gets whole match or some group, as index and length.
	/// </summary>
	/// <param name="t">This string. If <c>null</c>, returns <c>false</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>group</i> or <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Match(string, int, out RXGroup, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static bool RxMatch(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		int group, out RXGroup result, RXFlags flags = 0, Range? range = null) {
		var x = _cache.AddOrGet(rx, flags);
		return x.Match(t, group, out result, range);
	}

	/// <summary>
	/// Finds all match instances of PCRE regular expression <i>rx</i>.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <b>PARTIAL_</b> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.FindAll(string, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static IEnumerable<RXMatch> RxFindAll(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.FindAll(t, range);
	}

	/// <summary>
	/// Finds all match instances of PCRE regular expression <i>rx</i>. Gets array of <see cref="RXMatch"/>.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <b>PARTIAL_</b> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.FindAll(string, out RXMatch[], Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static bool RxFindAll(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		out RXMatch[] result, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.FindAll(t, out result, range);
	}

	/// <summary>
	/// Finds all match instances of PCRE regular expression <i>rx</i>.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>group</i> or <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <b>PARTIAL_</b> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.FindAll(string, int, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static IEnumerable<string> RxFindAll(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		int group, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.FindAll(t, group, range);
	}

	/// <summary>
	/// Finds all match instances of PCRE regular expression <i>rx</i>. Gets array of strings.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>group</i> or <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <b>PARTIAL_</b> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.FindAll(string, int, out string[], Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static bool RxFindAll(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		int group, out string[] result, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.FindAll(t, group, out result, range);
	}

	//rejected. Rarely used.
	///// <summary>
	///// Finds all match instances of PCRE regular expression <i>rx</i>. Gets array of <see cref="RXGroup"/>.
	///// </summary>
	///// <param name="t">This string.</param>
	///// <exception cref="ArgumentOutOfRangeException">Invalid <i>group</i> or <i>range</i>.</exception>
	///// <exception cref="ArgumentException">Invalid regular expression. Or used a PARTIAL_ flag.</exception>
	///// <exception cref="AuException">Failed (unlikely).</exception>
	///// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	///// <example></example>
	///// <inheritdoc cref="regexp.FindAllG(string, int, out RXGroup[], Range?, RXMatchFlags)"/>
	///// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	//public static bool RxFindAll(this string t,
	//	[ParamString(PSFormat.regexp)] string rx,
	//	int group, out RXGroup[] result, RXFlags flags = 0, Range? range = null) {
	//	if (t == null) throw new NullReferenceException();
	//	var x = _cache.AddOrGet(rx, flags);
	//	return x.FindAllG(t, group, out result, range);
	//}

	/// <summary>
	/// Finds and replaces all match instances of PCRE regular expression <i>rx</i>.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">
	/// - Invalid regular expression.
	/// - Invalid <c>$replacement</c>.
	/// - Used a <b>PARTIAL_</b> flag.
	/// - The regular expression contains <c>(?=...\K)</c>.
	/// </exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Replace(string, string, int, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static string RxReplace(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		[ParamString(PSFormat.RegexpReplacement)] string repl,
		int maxCount = -1, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.Replace(t, repl, maxCount, range);
	}

	/// <summary>
	/// Finds and replaces all match instances of PCRE regular expression <i>rx</i>.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">
	/// - Invalid regular expression.
	/// - Invalid <c>$replacement</c>.
	/// - Used a <b>PARTIAL_</b> flag.
	/// - The regular expression contains <c>(?=...\K)</c>.
	/// </exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Replace(string, string, out string, int, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static int RxReplace(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		[ParamString(PSFormat.RegexpReplacement)] string repl,
		out string result, int maxCount = -1, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.Replace(t, repl, out result, maxCount, range);
	}

	/// <summary>
	/// Finds and replaces all match instances of PCRE regular expression <i>rx</i>. Uses a callback function.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">
	/// - Invalid regular expression.
	/// - Invalid <c>$replacement</c>.
	/// - Used a <b>PARTIAL_</b> flag.
	/// - The regular expression contains <c>(?=...\K)</c>.
	/// </exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Replace(string, Func{RXMatch, string}, int, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static string RxReplace(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		Func<RXMatch, string> replFunc, int maxCount = -1, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.Replace(t, replFunc, maxCount, range);
	}

	/// <summary>
	/// Finds and replaces all match instances of PCRE regular expression <i>rx</i>. Uses a callback function.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">
	/// - Invalid regular expression.
	/// - Invalid <c>$replacement</c>.
	/// - Used a <b>PARTIAL_</b> flag.
	/// - The regular expression contains <c>(?=...\K)</c>.
	/// </exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Replace(string, Func{RXMatch, string}, out string, int, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static int RxReplace(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		Func<RXMatch, string> replFunc, out string result, int maxCount = -1, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.Replace(t, replFunc, out result, maxCount, range);
	}

	/// <summary>
	/// Returns an array of substrings that in this string are delimited by regular expression matches.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <b>PARTIAL_</b> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Split(string, int, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static string[] RxSplit(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		int maxCount = 0, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _cache.AddOrGet(rx, flags);
		return x.Split(t, maxCount, range);
	}

	static _RegexCache _cache = new();

	//Cache of compiled regular expressions.
	//Can make ~10 times faster when the subject string is short.
	//The algorithm is from .NET Regex source code.
	class _RegexCache
	{
		struct _RXCode
		{
			public string regex;
			public regexp code; //note: could instead cache only PCRE code (nint), but it makes quite difficult
			public RXFlags flags;
		}

		LinkedList<_RXCode> _list = new();
		const int c_maxCount = 15;

		/// <summary>
		/// If rx/flags is in the cache, returns the cached code.
		/// Else compiles rx/flags, adds to the cache and returns the code.
		/// </summary>
		/// <param name="rx"></param>
		/// <param name="flags"></param>
		/// <exception cref="ArgumentException">Invalid regular expression. Or failed to compile it for some other reason.</exception>
		public regexp AddOrGet(string rx, RXFlags flags) {
			lock (this) {
				int len = rx.Length;
				for (var x = _list.First; x != null; x = x.Next) {
					var v = x.Value.regex;
					if (v.Length == len && v == rx && x.Value.flags == flags) {
						if (x != _list.First) {
							_list.Remove(x);
							_list.AddFirst(x);
						}
						return x.Value.code;
					}
				}
				{
					var code = new regexp(rx, flags);

					var x = new _RXCode() { code = code, regex = rx, flags = flags };
					_list.AddFirst(x);
					if (_list.Count > c_maxCount) _list.RemoveLast();
					//note: now cannot free the PCRE code, because another thread may be using it. GC will do it safely.

					return code;
				}
			}
		}
	}
}
