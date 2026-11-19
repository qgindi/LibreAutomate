namespace Au.More;

/// <summary>
/// Performs file system enumeration include/exclude/recurse filtering using glob patterns or/and callback functions.
/// </summary>
/// <remarks>
/// A glob pattern is a string with wildcard characters to match multiple file paths in a directory.
/// <para/>
/// Glob pattern syntax:
/// <br/>• A glob pattern consists of one or more parts separated by <c>/</c>, like <c>"dir1/dir2/*.txt"</c>. The parts match path components separated by <c>\</c> or <c>/</c>.
/// <br/>• Character <c>?</c> matches any single character except <c>\</c> and <c>/</c>.
/// <br/>• Character <c>*</c> matches zero or more characters except <c>\</c> and <c>/</c>.
/// <br/>• Pattern that ends like <c>abc</c> matches file or directory <c>abc</c>, but not its descendants.
/// <br/>• Pattern that ends like <c>abc/**</c> matches only descendants of directory <c>abc</c> (everything in it and its subdirectories), but not <c>abc</c> itself.
/// <br/>• Pattern that ends like <c>abc/</c> (with a trailing separator) matches directory <c>abc</c> and all its descendants.
/// <br/>• <c>**/</c> in patterns like <c>**/abc</c> and <c>ab/**/cd</c> matches zero or more path components (directory names), including their trailing separators.
/// <br/>• A pattern part starting with <c>**</c> is a [wildcard expression](xref:wildcard_expression).
/// <br/>• Case-insensitive.
/// <para/>
/// Examples:
/// <br/>• <c>*.txt</c> matches txt files, except those in subdirectories.
/// <br/>• <c>**/*.txt</c> matches txt files anywhere.
/// <br/>• <c>abc/*.txt</c> matches txt files in directory <c>abc</c> but not in its subdirectories.
/// <br/>• <c>abc/**/*.txt</c> matches txt files in directory <c>abc</c> and its subdirectories.
/// <br/>• <c>abc/**</c> matches everything in directory <c>abc</c>.
/// <br/>• <c>abc/</c> matches directory <c>abc</c> and everything in it. Note the <c>/</c> at the end.
/// <br/>• <c>**/abc/</c> matches directory <c>abc</c> (which can be anywhere) and everything in it. Note the <c>/</c> at the end.
/// <para/>
/// The glob part separator character is <c>/</c>, but also can be used <c>\</c> if the pattern does not contain regular expressions containing <c>\</c>. All separators in a pattern must be the same character. It does not have to mach separators in paths.
/// <para/>
/// The glob matching is performed on path strings only, without accessing the file system.
/// <para/>
/// When used with methods that know whether a path is a file or directory (eg <c>filesystem.list</c>, <c>filesystem.copy</c>), you can specify whether a pattern must match only files or only directories:
/// <br/>• pattern starts with prefix <c>f:</c> - match only files.
/// <br/>• pattern starts with prefix <c>d:</c> - match only directories.
/// </remarks>
public class PathFilter {
	enum _GPKind { Globstar, Star, Text, Ends, Wildcard, Wildex }
	record struct _GlobPart(_GPKind kind, string s = null, wildex w = null);
	
	List<(bool include, bool? isDir, _GlobPart[] parts)> _rules = [];
	bool _hasIncludes;
	
	///
	public PathFilter() { }
	
	/// <summary>
	/// This constructor calls <see cref="AddIncludePatterns"/> and <see cref="AddExcludePatterns"/>.
	/// </summary>
	/// <param name="include">One or more include patterns, like <c>"pattern1|pattern2"</c> or <c>["pattern1", "pattern2"]</c>. See <see cref="PathFilter"/>, <see cref="AddIncludePatterns"/>.</param>
	/// <param name="exclude">One or more exclude patterns, like <c>"pattern1|pattern2"</c> or <c>["pattern1", "pattern2"]</c>. See <see cref="PathFilter"/>, <see cref="AddExcludePatterns"/>.</param>
	/// <exception cref="ArgumentException">An invalid glob pattern. Empty or contains an invalid wildcard expression.</exception>
	public PathFilter(Strings include = default, Strings exclude = default) {
		AddIncludePatterns(include.ToArray());
		AddExcludePatterns(exclude.ToArray());
	}
	
	/// <summary>
	/// Implicit conversion string to <c>PathFilter</c>.
	/// </summary>
	/// <param name="s">Zero or more include patterns separated by <c>|</c>, optionally followed by <c>||</c> and exclude patterns separated by <c>|</c>. Like <c>"include1|include2||exclude1|exclude2"</c>.</param>
	public static implicit operator PathFilter(string s) {
		if (s.NE()) return null;
		string s2 = null;
		int i = s.Find("||");
		if (i >= 0) (s2, s) = (s[(i + 2)..], s[..i]);
		return new(s, s2);
	}
	
	/// <summary>
	/// Adds include patterns.
	/// </summary>
	/// <param name="patterns">Zero or more glob paterns. See <see cref="PathFilter"/>.</param>
	/// <exception cref="ArgumentException">A pattern is empty or contains an empty part or invalid wildcard expression.</exception>
	/// <remarks>
	/// It is not possible to re-include a file if a parent directory of that file is excluded (see <see cref="AddExcludePatterns"/>).
	/// 
	/// If no include patterns are specified, will be included everything that is not excluded (see <see cref="AddExcludePatterns"/>).
	/// </remarks>
	public void AddIncludePatterns(params ReadOnlySpan<string> patterns) => _AddPatterns(true, patterns);
	
	/// <summary>
	/// Adds exclude patterns.
	/// </summary>
	/// <param name="patterns">Zero or more glob paterns. See <see cref="PathFilter"/>.</param>
	/// <exception cref="ArgumentException">A pattern is empty or contains an empty part or invalid wildcard expression.</exception>
	public void AddExcludePatterns(params ReadOnlySpan<string> patterns) => _AddPatterns(false, patterns);
	
	void _AddPatterns(bool include, ReadOnlySpan<string> patterns) {
		if (patterns.IsEmpty) return;
		foreach (string p in patterns) _AddPattern(include, p);
	}
	
	void _AddPattern(bool include, RStr p) {
		bool? isDir = null;
		if (!FilePrefix.NE() && p.Starts(FilePrefix)) {
			isDir = false;
			p = p[FilePrefix.Length..];
		} else if (!DirectoryPrefix.NE() && p.Starts(DirectoryPrefix)) {
			isDir = true;
			p = p[DirectoryPrefix.Length..];
		}
		
		if (p.IsEmpty) throw new ArgumentException("Empty glob pattern.");
		
		char splitChar = '/'; //prefer /, because \ can be used in wildex regex. But allow \, because users will wonder why does not work with \.
		if (!p.Contains('/') && p.Contains('\\') && p is not ['*', '*', not '\\', ..] /*not wildex*/) splitChar = '\\';
		
		var a = new _GlobPart[p.Count(splitChar) + 1];
		int i = 0;
		foreach (var range in p.Split(splitChar)) {
			_GlobPart g;
			RStr s = p[range];
			if (s.IsEmpty) {
				if (i < a.Length - 1) throw new ArgumentException("Empty part of glob pattern");
				if (isDir != false) _rules.Add((include, true, a[..^1])); //"dir/" = "dir" + "dir/**"
				g = new(_GPKind.Globstar);
			} else if (s[0] == '*') {
				if (s.Length == 1) g = new(_GPKind.Star);
				else if (s is "**") g = new(_GPKind.Globstar);
				else if (s[1] == '*') g = new(_GPKind.Wildex, null, new wildex(s.ToString()));
				else if (wildex.hasWildcardChars(s[1..])) g = new(_GPKind.Wildcard, s.ToString());
				else g = new(_GPKind.Ends, s[1..].ToString());
			} else {
				g = new(wildex.hasWildcardChars(s) ? _GPKind.Wildcard : _GPKind.Text, s.ToString());
			}
			a[i++] = g;
		}
		Debug.Assert(i == a.Length);
		
		_rules.Add((include, isDir, a));
		
		if (include) _hasIncludes = true;
	}

	///
	protected virtual string FilePrefix => "f:";
	
	///
	protected virtual string DirectoryPrefix => "d:";
	
	/// <summary>
	/// Returns <c>true</c> if the filter is empty (no patterns or filter functions).
	/// </summary>
	public virtual bool IsEmpty() {
		if (this is { _rules.Count: 0, FileFilter: null, DirectoryFilter: null }) return true;
		if (_hasIncludes) {
			if (_rules.Count == 1 && _rules[0].parts is [{ kind: _GPKind.Star }]) print.warning("Glob pattern * is unnecessary. Use no patterns instead.");
			//else if (recursive && !_rules.Any(static o => o.include && o.parts.Length > 1)) print.warning("Possibly incorrect glob pattern(s). The enumeration is recursive (all descendants), but the include patterns match only immediate children."); //no, there are cases when code uses the Recurse flag but does not control the input, which may specify multi-part patterns in some cases but only single-part patterns in other cases, and it is valid
		}
		return false;
	}
	
	/// <summary>
	/// Filters a sequence of objects of any type that can provide a path (for example <c>FileSystemInfo</c> or <c>string</c>).
	/// Returns only objects whose paths match the glob patterns.
	/// </summary>
	/// <param name="source">The input.</param>
	/// <param name="getPath">A function that provides paths. Paths can be full (then use <i>rootDir</i>) or relative (<i>rootDir</i> <c>null</c>).</param>
	/// <param name="rootDir">The directory in which the enumeration is performed. The glob patterns are relative to this directory. Must be <c>null</c> if paths are relative.</param>
	/// <exception cref="ArgumentException">Some paths are not in <i>rootDir</i>.</exception>
	public IEnumerable<T> Filter<T>(IEnumerable<T> source, Func<T, string> getPath, string rootDir) {
		ArgumentNullException.ThrowIfNull(source);
		
		int rootLen = 0;
		
		foreach (var obj in source) {
			string path = getPath(obj);
			if (path.NE()) continue;
			if (!rootDir.NE()) {
				if (rootLen == 0) {
					if (path.PathStarts_(rootDir)) {
						rootLen = rootDir.Length; while (rootDir[rootLen - 1] is '\\' or '/') rootLen--;
					} else {
						var rp = Path.GetRelativePath(rootDir, path);
						if (rp.Length >= path.Length || rp is "." or ".." || rp.Starts(@"..\")) throw new ArgumentException("paths not in rootDir");
						rootDir = path[..^(rp.Length + 1)];
						rootLen = rootDir.Length;
					}
					rootLen++;
				} else {
					if (!path.PathStarts_(rootDir)) throw new ArgumentException("paths not in rootDir");
				}
			}
			
			if (ShouldInclude(path.AsSpan(rootLen))) yield return obj;
		}
	}
	
	/// <summary>
	/// Returns <c>true</c> if the path matches the glob patterns.
	/// </summary>
	/// <param name="relativePath">Relative path.</param>
	/// <param name="isDirectory">
	/// If not <c>null</c>:
	/// <br/>• <c>true</c> - the path is a directory. It will not match patterns that start with suffix <c>"f:"</c>.
	/// <br/>• <c>false</c> - the path is a file. It will not match patterns that start with suffix <c>"d:"</c>.
	/// </param>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public bool ShouldInclude(RStr relativePath, bool? isDirectory = null) {
		Span<Range> parts = stackalloc Range[500];
		split:
		int n = relativePath.SplitAny(parts, @"\/", StringSplitOptions.RemoveEmptyEntries);
		if (n == parts.Length) { parts = new Range[n * 2]; goto split; }
		if (n == 0) return false;
		parts = parts[..n];
		
		bool included = !_hasIncludes;
		foreach (var (include, isDir, pattern) in _rules) {
			if (isDirectory.HasValue && isDir.HasValue && isDir != isDirectory) continue;
			
			if (include && !included && _MatchParts(pattern, relativePath, parts)) included = true;
			else if (!include && _MatchParts(pattern, relativePath, parts)) return false;
		}
		
		return included;
	}
	
	//The algorithms are mostly AI-created.
	
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static bool _MatchParts(ReadOnlySpan<_GlobPart> pattern, RStr path, ReadOnlySpan<Range> parts) {
		int n = parts.Length, next = n + 1, curr = 0;
		Span<bool> a = next * 2 <= 8000 ? stackalloc bool[next * 2] : new bool[next * 2];
		a[next + n] = true;
		
		for (int i = pattern.Length; --i >= 0;) {
			if (pattern[i].kind == _GPKind.Globstar) {
				if (i == pattern.Length - 1) {
					// A trailing ** must match at least one path component.
					a[curr + n] = false;
					for (int j = n; --j >= 0;) {
						a[curr + j] = a[next + j + 1] || a[curr + j + 1];
					}
				} else {
					// A non-trailing ** matches zero or more path components.
					a[curr + n] = a[next + n];
					for (int j = n; --j >= 0;) {
						a[curr + j] = a[next + j] || a[curr + j + 1];
					}
				}
			} else {
				a[curr + n] = false;
				for (int j = n; --j >= 0;) {
					a[curr + j] = _MatchPart(path[parts[j]], pattern[i])
						&& a[next + j + 1];
				}
			}
			
			(curr, next) = (next, curr);
		}
		
		return a[next];
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _MatchPart(RStr pathSegment, _GlobPart patternSegment)
		=> patternSegment.kind switch {
			_GPKind.Text => pathSegment.Eqi(patternSegment.s),
			_GPKind.Ends => pathSegment.Ends(patternSegment.s, ignoreCase: true),
			_GPKind.Wildcard => pathSegment.Like(patternSegment.s, ignoreCase: true),
			_GPKind.Wildex => patternSegment.w.Match(pathSegment),
			_ => true //_SegKind.Star
		};
	
	/// <summary>
	/// Returns <c>true</c> if the directory should be enumerated because some paths inside it can match.
	/// </summary>
	/// <param name="relativeDirPath">Relative path.</param>
	/// <remarks>Assumes top-down enumeration, i.e. parent directories were already checked.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	public bool ShouldRecurse(RStr relativeDirPath) {
		Span<Range> parts = stackalloc Range[500];
		split:
		int n = relativeDirPath.SplitAny(parts, @"\/", StringSplitOptions.RemoveEmptyEntries);
		if (n == parts.Length) { parts = new Range[n * 2]; goto split; }
		if (n == 0) return true;
		parts = parts[..n];
		
		// 1. Whole directory excluded?
		foreach (var (include, isDir, p) in _rules) {
			if (include) continue;
			if (p[^1].kind == _GPKind.Globstar) {
				if (p.Length == 1) return false;
				if (_MatchParts(p.AsSpan(0, p.Length - 1), relativeDirPath, parts)) return false;
			}
		}
		
		// 2. Can some include pattern still match something deeper?
		if (!_hasIncludes) return true;
		foreach (var (include, isDir, p) in _rules) {
			if (include && _CanMatchDeeper(p, relativeDirPath, parts)) return true;
		}
		return false;
	}
	
	static bool _CanMatchDeeper(_GlobPart[] p, RStr path, ReadOnlySpan<Range> parts) {
		int m = p.Length;
		Span<bool> cur = m < 128 ? stackalloc bool[m + 1] : new bool[m + 1];
		Span<bool> next = m < 128 ? stackalloc bool[m + 1] : new bool[m + 1];
		cur[0] = true;
		
		foreach (var r in parts) {
			_Closure(p, cur);
			next.Clear();
			bool any = false;
			for (int i = 0; i < m; i++) {
				if (!cur[i]) continue;
				if (p[i].kind == _GPKind.Globstar) { next[i] = true; any = true; } // consume, stay
				else if (_MatchPart(path[r], p[i])) { next[i + 1] = true; any = true; }
			}
			if (!any) return false;
			var t = cur; cur = next; next = t;
		}
		_Closure(p, cur);
		for (int i = 0; i < m; i++) if (cur[i]) return true; // something still left to match deeper
		return false;
		
		// non-trailing ** may match zero components
		static void _Closure(_GlobPart[] p, Span<bool> s) {
			for (int i = 0; i < p.Length - 1; i++)
				if (s[i] && p[i].kind == _GPKind.Globstar) s[i + 1] = true;
		}
	}
	
	/// <summary>
	/// This callback function is called for each file (and not directory) that would be included in results.
	/// Return <c>true</c> to confirm, or <c>false</c> to exclude it.
	/// </summary>
	/// <remarks>
	/// Called by methods that enumerate files/directories.
	/// 
	/// Not called for files excluded by the specified flags or glob patterns.
	/// </remarks>
	public Func<FLEntry, bool> FileFilter { get; set; }
	
	/// <summary>
	/// This callback function is called for each directory (and not file) that would be included in results.
	/// Return:
	/// <br/>• <c>true</c> - include the directory (if not already excluded) and its descendants (if enumerating recursively and descendants would be included). See also: <see cref="FLEntry.DirInclude"/>, <see cref="FLEntry.DirRecurse"/>.
	/// <br/>• <c>false</c> - exclude the directory and its descendants.
	/// </summary>
	/// <remarks>
	/// Called by methods that enumerate files/directories.
	/// 
	/// When enumerating recursively, called even if the directory entry is excluded, to determine whether its descendands can be included.
	/// 
	/// Called only when the directory or its descendants would be included, after passing other filters (flags, file/directory, glob patterns).
	/// 
	/// Cannot re-include anything that is excluded. For example, when getting only files, returning <c>true</c> includes descendant files but not the directory.
	/// </remarks>
	public Func<FLEntry, bool> DirectoryFilter { get; set; }
	
	//rejected. Rarely used. Can instead use filter functions.
	//public int MaxRecursionDepth { get; set; } = int.MaxValue;
	
	//rejected. Rarely used. Can instead use flags SkipHidden and SkipHiddenSystem, can use filter functions.
	//public FileAttributes AttributesToSkip { get; set; }
}
