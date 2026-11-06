namespace Au.Types;

public static partial class ExtString {
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
	public static bool RxIsMatch(this RStr t,
		[ParamString(PSFormat.Regexp)] string rx,
		RXFlags flags = 0, Range? range = null) {
		var x = _RegexpCache.AddOrGet(rx, flags);
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
		var x = _RegexpCache.AddOrGet(rx, flags);
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
		var x = _RegexpCache.AddOrGet(rx, flags);
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
		var x = _RegexpCache.AddOrGet(rx, flags);
		return x.Match(t, group, out result, range);
	}
	
	/// <summary>
	/// Finds all match instances of PCRE regular expression <i>rx</i>.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <c>PARTIAL_</c> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.FindAll(string, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static IEnumerable<RXMatch> RxFindAll(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _RegexpCache.AddOrGet(rx, flags);
		return x.FindAll(t, range);
	}
	
	/// <summary>
	/// Finds all match instances of PCRE regular expression <i>rx</i>. Gets array of <see cref="RXMatch"/>.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <c>PARTIAL_</c> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.FindAll(string, out RXMatch[], Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static bool RxFindAll(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		out RXMatch[] result, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _RegexpCache.AddOrGet(rx, flags);
		return x.FindAll(t, out result, range);
	}
	
	/// <summary>
	/// Finds all match instances of PCRE regular expression <i>rx</i>.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>group</i> or <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <c>PARTIAL_</c> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.FindAll(string, int, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static IEnumerable<string> RxFindAll(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		int group, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _RegexpCache.AddOrGet(rx, flags);
		return x.FindAll(t, group, range);
	}
	
	/// <summary>
	/// Finds all match instances of PCRE regular expression <i>rx</i>. Gets array of strings.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>group</i> or <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <c>PARTIAL_</c> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.FindAll(string, int, out string[], Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static bool RxFindAll(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		int group, out string[] result, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _RegexpCache.AddOrGet(rx, flags);
		return x.FindAll(t, group, out result, range);
	}
	
	//rejected. Rarely used.
	///// <summary>
	///// Finds all match instances of PCRE regular expression <i>rx</i>. Gets array of <see cref="RXGroup"/>.
	///// </summary>
	///// <param name="t">This string.</param>
	///// <exception cref="ArgumentOutOfRangeException">Invalid <i>group</i> or <i>range</i>.</exception>
	///// <exception cref="ArgumentException">Invalid regular expression. Or used a <c>PARTIAL_</c> flag.</exception>
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
	/// - Used a <c>PARTIAL_</c> flag.
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
		var x = _RegexpCache.AddOrGet(rx, flags);
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
	/// - Used a <c>PARTIAL_</c> flag.
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
		var x = _RegexpCache.AddOrGet(rx, flags);
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
	/// - Used a <c>PARTIAL_</c> flag.
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
		var x = _RegexpCache.AddOrGet(rx, flags);
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
	/// - Used a <c>PARTIAL_</c> flag.
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
		var x = _RegexpCache.AddOrGet(rx, flags);
		return x.Replace(t, replFunc, out result, maxCount, range);
	}
	
	/// <summary>
	/// Returns an array of substrings that in this string are delimited by regular expression matches.
	/// </summary>
	/// <param name="t">This string.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid <i>range</i>.</exception>
	/// <exception cref="ArgumentException">Invalid regular expression. Or used a <c>PARTIAL_</c> flag.</exception>
	/// <exception cref="AuException">Failed (unlikely).</exception>
	/// <remarks>More info and examples: <see cref="regexp"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="regexp.Split(string, int, Range?, RXMatchFlags)"/>
	/// <inheritdoc cref="regexp(string, RXFlags)" path="/param"/>
	public static string[] RxSplit(this string t,
		[ParamString(PSFormat.Regexp)] string rx,
		int maxCount = 0, RXFlags flags = 0, Range? range = null) {
		if (t == null) throw new NullReferenceException();
		var x = _RegexpCache.AddOrGet(rx, flags);
		return x.Split(t, maxCount, range);
	}
	
	struct _RegexpCache {
		class _Item(string rx, RXFlags flags) {
			public _Item next;
			public regexp obj;
			readonly string _rx = rx;
			readonly RXFlags _flags = flags;
			public ulong accessed;
			
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal bool Equals_(string rx, RXFlags flags) => _rx == rx && _flags == flags;
		}
		
		volatile _Item _first;
		ulong _accessCounter;
		const int c_maxCount = 20;
		
		static _RegexpCache[] _buckets = new _RegexpCache[32];
		
		/// <summary>
		/// If <i>rx</i>/<i>flags</i> is in the cache, returns the cached object.
		/// Else compiles <i>rx</i>/<i>flags</i>, adds to the cache and returns the object.
		/// </summary>
		/// <exception cref="ArgumentException">Invalid regular expression. Or failed to compile it for some other reason.</exception>
		public static regexp AddOrGet(string rx, RXFlags flags) {
			int k = rx?.Length ?? throw new ArgumentNullException();
			if (k > 0) k ^= (int)rx[k / 2];
			return _buckets[k & 31]._AddOrGet(rx, flags);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		regexp _AddOrGet(string rx, RXFlags flags) {
			//this loop must be as fast as possible
			for (_Item x = _first; x != null; x = x.next) {
				if (x.Equals_(rx, flags)) {
					x.accessed = _accessCounter++; //another way would be to move x to the start of the linked list, but it makes this loop much slower
					return x.obj;
				}
			}
			
			var r = new regexp(rx, flags);
			
			_RemoveOld();
			
			//add y to the start of the linked list
			var y = new _Item(rx, flags) { obj = r, next = _first, accessed = _accessCounter++ };
			_first = y;
			
			return r;
			
			//not thread-perfect (may not cache some), but thread-safe (no exceptions or something bad that really matters)
		}
		
		[MethodImpl(MethodImplOptions.NoInlining)]
		void _RemoveOld() {
			int n = 0;
			for (_Item x = _first; x != null; x = x.next) n++;
			for (; n >= c_maxCount; n--) {
				_Item xOldest = null, xOldestPrev = null;
				ulong accessedOldest = ulong.MaxValue;
				for (_Item x = _first, xp = null; x != null; xp = x, x = x.next) {
					if (x.accessed < accessedOldest) { accessedOldest = x.accessed; xOldest = x; xOldestPrev = xp; }
				}
				if (xOldest == null) return;
				if (xOldestPrev != null) xOldestPrev.next = xOldest.next; else _first = xOldest.next;
			}
		}
	}
}
