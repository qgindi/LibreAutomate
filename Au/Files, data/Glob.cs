using System.ComponentModel;

namespace Au.More;

/// <summary>
/// Filters file paths based on glob patterns.
/// </summary>
/// <remarks>
/// A glob pattern is a string with wildcard characters to match multiple file paths in a directory.
/// <para/>
/// Glob pattern syntax:
/// <br/>• A glob pattern consists of one or more parts separated by <c>/</c>, like <c>"dir1/dir2/*.txt"</c>. The parts match path components separated by <c>\</c> or <c>/</c>.
/// <br/>• Character <c>?</c> matches any single character except <c>\</c> and <c>/</c>.
/// <br/>• Character <c>*</c> matches zero or more characters except <c>\</c> and <c>/</c>.
/// <br/>• <c>**/</c> in patterns like <c>**/abc</c> and <c>ab/**/cd</c> matches zero or more path components (directory names), including their trailing separators.
/// <br/>• Pattern like <c>abc/</c> or <c>abc/**</c> matches everything in directory <c>abc</c> and its subdirectories.
/// <br/>• A pattern part starting with <c>**</c> is a [wildcard expression](xref:wildcard_expression).
/// <br/>• Case-insensitive.
/// <para/>
/// Examples:
/// <br/>• <c>*.txt</c> matches txt files, except those in subdirectories.
/// <br/>• <c>**/*.txt</c> matches txt files anywhere.
/// <br/>• <c>abc/*.txt</c> matches txt files in directory <c>abc</c> but not in its subdirectories.
/// <br/>• <c>abc/**/*.txt</c> matches txt files in directory <c>abc</c> and its subdirectories.
/// <br/>• <c>**/abc/</c> matches everything in directory <c>abc</c> which can be anywhere. Note the <c>/</c> at the end.
/// <para/>
/// The class works with strings, and does not access the file system.
/// </remarks>
public class Glob {
	enum _SegKind { Globstar, Star, Text, Ends, Wildcard, Wildex }
	record struct _Segment(_SegKind kind, string s = null, wildex w = null);
	
	List<(bool include, _Segment[] segments)> _rules = [];
	bool _hasIncludes;
	
	///
	public Glob() { }
	
	/// <summary>
	/// This constructor calls <see cref="Include"/> and <see cref="Exclude"/>.
	/// </summary>
	/// <param name="include">One or more include patterns. See <see cref="Glob"/>.</param>
	/// <param name="exclude">One or more exclude patterns. See <see cref="Glob"/>.</param>
	public Glob(string[] include, string[] exclude = null) { Include(include).Exclude(exclude); }
	
	//rejected. Makes code less readable. Two kinds of separators in single string (| and /).
	///// <summary>
	///// This constructor calls <see cref="Strings.ToArray"/>, <see cref="Include"/> and <see cref="Exclude"/>.
	///// </summary>
	///// <param name="include">One or more include patterns. See <see cref="Glob"/>. Can be a string like <c>"pattern1|pattern2"</c> or collection like <c>["pattern1", "pattern2"]</c>.</param>
	///// <param name="exclude">One or more exclude patterns. See <see cref="Glob"/>. Can be a string like <c>"pattern1|pattern2"</c> or collection like <c>["pattern1", "pattern2"]</c>.</param>
	//public Glob(Strings include, Strings exclude = default) : this(include.ToArray(), exclude.ToArray()) { }
	
	/// <summary>
	/// Adds one or more include patterns.
	/// </summary>
	/// <param name="patterns">One or more glob paterns. See <see cref="Glob"/>.</param>
	/// <returns>Self.</returns>
	/// <remarks>
	/// It is not possible to re-include a file if a parent directory of that file is excluded. See <see cref="Exclude"/>.
	/// 
	/// If no include patterns are specified, will be included everything except excluded paths.
	/// </remarks>
	public Glob Include(params string[] patterns) => _AddRule(true, patterns);
	
	/// <summary>
	/// Adds one or more exclude patterns.
	/// </summary>
	/// <param name="patterns">One or more glob paterns. See <see cref="Glob"/>.</param>
	/// <returns>Self.</returns>
	public Glob Exclude(params string[] patterns) => _AddRule(false, patterns);
	
	Glob _AddRule(bool include, string[] patterns) {
		if (patterns is null or []) return this;
		foreach (var v in patterns) {
			var pattern = v;
			var segments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries); //note: allow only /, because \ can be used in regex
			if (segments is []) throw new ArgumentException("Empty pattern.");
			int n = segments.Length; if (pattern[^1] is '/') n++; //_SegKind.Globstar
			var a = new _Segment[n];
			for (int i = 0; i < segments.Length; i++) {
				_Segment g;
				string s = segments[i];
				if (s[0] != '*') g = new(wildex.hasWildcardChars(s) ? _SegKind.Wildcard : _SegKind.Text, s);
				else if (s.Length == 1) g = new(_SegKind.Star);
				else if (s == "**") g = new(_SegKind.Globstar);
				else if (s[1] == '*') g = new(_SegKind.Wildex, null, new wildex(s));
				else if (wildex.hasWildcardChars(s.AsSpan(1))) g = new(_SegKind.Wildcard, s);
				else g = new(_SegKind.Ends, s[1..]);
				a[i] = g;
			}
			_rules.Add((include, a));
		}
		if (include) _hasIncludes = true;
		return this;
	}
	
	/// <summary>
	/// Filters a sequence of file info objects. Returns only those whose paths match glob patterns added with <see cref="Include"/> and/or <see cref="Exclude"/>.
	/// </summary>
	/// <param name="paths">Results of <see cref="DirectoryInfo.EnumerateFiles()"/> or similar method.</param>
	/// <param name="rootDir">The directory in which the enumeration is performed. Glob patterns are relative to this directory.</param>
	public IEnumerable<FileSystemInfo> Filter(IEnumerable<FileSystemInfo> paths, string rootDir)
		=> Filter(paths, static f => f.FullName, rootDir);
	
	/// <summary>
	/// Filters a sequence of paths. Returns only those that match glob patterns added with <see cref="Include"/> and/or <see cref="Exclude"/>.
	/// </summary>
	/// <param name="paths">Paths. Paths can be full (then use <i>rootDir</i>) or relative (<i>rootDir</i> <c>null</c>).</param>
	/// <param name="rootDir">The directory in which the enumeration is performed. Glob patterns are relative to this directory. Must be <c>null</c> if paths are relative.</param>
	public IEnumerable<string> Filter(IEnumerable<string> paths, string rootDir)
		=> Filter(paths, static f => f, rootDir);
	
	/// <summary>
	/// Filters a sequence of objects of any type that can provide a path. Returns only those whose paths match glob patterns added with <see cref="Include"/> and/or <see cref="Exclude"/>.
	/// </summary>
	/// <param name="paths">Objects of a type that can provide a path.</param>
	/// <param name="getPath">Provides paths. Paths can be full (then use <i>rootDir</i>) or relative (<i>rootDir</i> <c>null</c>).</param>
	/// <param name="rootDir">The directory in which the enumeration is performed. Glob patterns are relative to this directory. Must be <c>null</c> if paths are relative.</param>
	/// <exception cref="ArgumentException">paths not in <i>rootDir</i>.</exception>
	public IEnumerable<T> Filter<T>(IEnumerable<T> paths, Func<T, string> getPath, string rootDir) {
		ArgumentNullException.ThrowIfNull(paths);
		
		var aSeg = new Range[100];
		List<bool> aBool = [];
		int rootLen = 0;
		
		foreach (var obj in paths) {
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
			
			RStr relPath = path.AsSpan(rootLen);
			retry:
			int nSeg = relPath.SplitAny(aSeg, @"\/", StringSplitOptions.RemoveEmptyEntries);
			if (nSeg == aSeg.Length) { aSeg = new Range[nSeg * 2]; goto retry; }
			var segments = aSeg.AsSpan(0, nSeg);
			
			bool included = !_hasIncludes;
			foreach (var (include, pattern) in _rules) {
				if (include && !included && _Match(pattern, relPath, segments, aBool)) included = true;
				else if (!include && _Match(pattern, relPath, segments, aBool)) {
					included = false;
					break;
				}
			}
			
			if (included) yield return obj;
		}
	}
	
	//The algorithm is mostly AI-created.
	
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static bool _Match(_Segment[] pattern, RStr path, ReadOnlySpan<Range> segments, List<bool> a) {
		int n = segments.Length, next = n + 1, curr = 0;
		CollectionsMarshal.SetCount(a, next * 2);
		CollectionsMarshal.AsSpan(a).Slice(next, next).Clear();
		a[next + n] = true;
		
		for (int i = pattern.Length; --i >= 0;) {
			if (pattern[i].kind == _SegKind.Globstar) {
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
					a[curr + j] = _Match2(path[segments[j]], pattern[i])
						&& a[next + j + 1];
				}
			}
			
			(curr, next) = (next, curr);
		}
		
		return a[next];
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool _Match2(RStr pathSegment, _Segment patternSegment)
			=> patternSegment.kind switch {
				_SegKind.Text => pathSegment.Eqi(patternSegment.s),
				_SegKind.Ends => pathSegment.Ends(patternSegment.s, ignoreCase: true),
				_SegKind.Wildcard => pathSegment.Like(patternSegment.s, ignoreCase: true),
				_SegKind.Wildex => patternSegment.w.Match(pathSegment),
				_ => true //_SegKind.Star
			};
	}
}
