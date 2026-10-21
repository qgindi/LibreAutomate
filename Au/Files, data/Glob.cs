using System.ComponentModel;

namespace Au.More;

/// <summary>
/// Filters file paths based on glob patterns.
/// </summary>
/// <remarks>
/// A glob pattern is a string with wildcard characters to match multiple file paths.
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
	
	static readonly char[] s_sep = ['\\', '/'];
	
	/// <summary>
	/// Filters a sequence of paths. Returns only those that match glob patterns added with <see cref="Include"/> and/or <see cref="Exclude"/>.
	/// </summary>
	/// <param name="paths">Paths. If glob patterns specify relative paths (recommended), then paths here must be relative paths too, unless <i>rootDirectory</i> is specified.</param>
	/// <param name="rootDir">Root directory path. Glob patterns are relative to this directory. <i>paths</i> are full paths starting with this directory.</param>
	public IEnumerable<string> Filter(IEnumerable<string> paths, string rootDir = null)
		=> Filter(paths, static f => f, rootDir);
	
	/// <summary>
	/// Filters a sequence of file info objects. Returns only those whose paths match glob patterns added with <see cref="Include"/> and/or <see cref="Exclude"/>.
	/// </summary>
	/// <param name="rootDir">The directory in which the enumeration is performed. Glob patterns are relative to this directory.</param>
	/// <param name="paths">Results of <see cref="filesystem.enumerate"/> or similar method.</param>
	public IEnumerable<FEFile> Filter(string rootDir, IEnumerable<FEFile> paths)
		=> Filter(paths, static f => f.FullPath, rootDir);
	
	/// <summary>
	/// Filters a sequence of file info objects. Returns only those whose paths match glob patterns added with <see cref="Include"/> and/or <see cref="Exclude"/>.
	/// </summary>
	/// <param name="rootDir">The directory in which the enumeration is performed. Glob patterns are relative to this directory.</param>
	/// <param name="paths">Results of <see cref="DirectoryInfo.EnumerateFiles()"/> or similar method.</param>
	public IEnumerable<FileSystemInfo> Filter(string rootDir, IEnumerable<FileSystemInfo> paths)
		=> Filter(paths, static f => f.FullName, rootDir);
	
	
	/// <summary>
	/// This public but hidden method can be used for other file system info object types.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public IEnumerable<T> Filter<T>(IEnumerable<T> paths, Func<T, string> getPath, string rootDir) {
		ArgumentNullException.ThrowIfNull(paths);
		
		var aSeg = new Range[100];
		List<bool> aBool = [];
		
		foreach (var obj in paths) {
			string path = getPath(obj);
			if (path.NE()) continue;
			int rootLen = 0;
			if (!rootDir.NE()) {
				if (!path.PathStarts_(rootDir)) continue;
				rootLen = rootDir.Length; while (rootDir[rootLen - 1] is '\\' or '/') rootLen--;
				rootLen++;
			}
			
			RStr relPath = path.AsSpan(rootLen);
			retry:
			int nSeg = relPath.SplitAny(aSeg, s_sep, StringSplitOptions.RemoveEmptyEntries);
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
	
	//The algorithm is AI-created.
	
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
