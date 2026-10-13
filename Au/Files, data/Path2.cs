namespace Au.More;

/// <summary>
/// File path string functions. Complements <see cref="System.IO.Path"/>.
/// </summary>
/// <remarks>
/// Most functions of this class work with strings and don't access the file system. Several functions query file system info.
/// 
/// Functions of this class don't throw exceptions when path is invalid (path format, invalid characters). Only <see cref="Normalize"/> throws exception if not full path.
/// </remarks>
public static unsafe class Path2 {
	/// <summary>
	/// If <i>path</i> starts with <c>"%"</c> or <c>"\"%"</c>, expands environment variables enclosed in <c>%</c>, else just returns <i>path</i>.
	/// Also supports known folder names, like <c>"%folders.Documents%"</c>. More info in Remarks.
	/// </summary>
	/// <param name="path">Any string. Can be <c>null</c>.</param>
	/// <param name="strict">
	/// What to do if <i>path</i> looks like starts with and environment variable or known folder but the variable/folder does not exist:
	/// <br/>• <c>true</c> - throw <see cref="ArgumentException"/>;
	/// <br/>• <c>false</c> - return unexpanded path;
	/// <br/>• <c>null</c> (default) - call <see cref="print.warning"/> and return unexpanded path.
	/// </param>
	/// <remarks>
	/// Supports known folder names. See <see cref="folders"/>.
	/// Example: <c>@"%folders.Documents%\file.txt"</c>.
	/// Example: <c>@"%folders.shell.ControlPanel%" //gets ":: ITEMIDLIST"</c>.
	/// Usually known folders are used like <c>string path = folders.Documents + "file.txt"</c>. However it cannot be used when you want to store paths in text files, registry, etc. Then this feature is useful.
	/// To get known folder path, this function calls <see cref="folders.getFolder"/>.
	/// 
	/// This function is called by many functions of classes <see cref="Path2"/>, <see cref="filesystem"/>, <see cref="icon"/>, some others, therefore all they support environment variables and known folders in path string.
	/// </remarks>
	/// <seealso cref="Environment.ExpandEnvironmentVariables"/>
	/// <seealso cref="Environment.GetEnvironmentVariable"/>
	/// <seealso cref="Environment.SetEnvironmentVariable"/>
	public static string Expand(string path, bool? strict = null) {
		var s = path;
		if (s.Lenn() < 3) return s;
		if (s[0] != '%') {
			if (s[0] == '"' && s[1] == '%') return "\"" + Expand(s[1..], strict);
			return s;
		}
		int i = s.IndexOf('%', 2); if (i < 0) return s;
		//return Environment.ExpandEnvironmentVariables(s); //5 times slower
		
		//support known folders, like @"%folders.Documents%\...".
		//	rejected: without "folders", like @"%%.Documents%\...". If need really short, can set and use environment variables.
		//if ((i > 10 && s.Starts("%folders.")) || (i > 4 && s.Starts("%%"))) {
		//	var prop = s[(s[1] == '%' ? 2 : 10)..i];
		if (i > 10 && s.Starts("%folders.")) {
			var prop = s[9..i];
			var k = folders.getFolder(prop);
			if (k != null) {
				s = s[++i..];
				string ks = k.Path; if (ks.Starts(":: ")) return ks + s; //don't need \
				return k + s; //add \ if need
			}
			//throw new AuException("folders does not have property " + prop);
		}
		
		if (!Api.ExpandEnvironmentStrings(s, out s)) {
			var err = "Failed to expand path: " + s;
			if (strict == true) throw new ArgumentException(err);
			if (strict != false) print.warning(err);
			return s;
		}
		return Expand(s, strict); //can be %envVar2% in envVar1 value
	}
	
	/// <summary>
	/// Returns <c>true</c> if the string is full path, like <c>@"C:\a\b.txt"</c> or <c>@"C:"</c> or <c>@"\\server\share\..."</c>:
	/// </summary>
	/// <param name="path">Any string. Can be <c>null</c>.</param>
	/// <param name="orEnvVar">Also return <c>true</c> if starts with <c>"%environmentVariable%"</c> or <c>"%folders.Folder%"</c>. Note: this function does not check whether the variable/folder exists; for it use <see cref="IsFullPathExpand"/> instead.</param>
	/// <remarks>
	/// Returns <c>true</c> if <i>path</i> matches one of these wildcard patterns (with <c>'\\'</c> or <c>'/'</c>):
	/// - <c>@"?:\*"</c> - local path, like <c>@"C:\a\b.txt"</c>. Here <c>?</c> is A-Z, a-z.
	/// - <c>@"?:"</c> - drive name, like <c>@"C:"</c>. Here <c>?</c> is A-Z, a-z.
	/// - <c>@"\\*"</c> - network path, like <c>@"\\server\share\..."</c>. Or has prefix <c>@"\\?\"</c>.
	/// 
	/// Supports only filesystem paths. Returns <c>false</c> if <i>path</i> is URL (<see cref="IsUri"/>) or starts with <c>"::"</c>.
	/// </remarks>
	public static bool IsFullPath(RStr path, bool orEnvVar = false) {
		int len = path.Length;
		if (len >= 2) {
			if (path[1] == ':' && path[0].IsAsciiAlpha()) {
				return len == 2 || IsSepChar_(path[2]);
				//info: returns false if eg "c:abc" which means "abc" in current directory of drive "c:"
			}
			switch (path[0]) {
			case '\\' or '/':
				return IsSepChar_(path[1]);
			case '%' when orEnvVar:
				return path[1..].IndexOf('%') > 1;
			}
		}
		return false;
	}
	
	/// <summary>
	/// Expands environment variables and calls/returns <see cref="IsFullPath"/>.
	/// </summary>
	/// <returns><c>true</c> if the string is full path, like <c>@"C:\a\b.txt"</c> or <c>@"C:"</c> or <c>@"\\server\share\..."</c>.</returns>
	/// <param name="path">
	/// Any string. Can be <c>null</c>.
	/// If starts with <c>'%'</c> character, calls <see cref="Expand"/> and then <see cref="IsFullPath"/> with expanded environment variables. If it returns <c>true</c>, replaces the passed variable with the expanded path string.
	/// </param>
	/// <param name="strict"><inheritdoc cref="Expand(string, bool?)" path="/param[@name='strict']/node()"/></param>
	/// <remarks>
	/// Returns <c>true</c> if <i>path</i> matches one of these wildcard patterns (with <c>'\\'</c> or <c>'/'</c>):
	/// - <c>@"?:\*"</c> - local path, like <c>@"C:\a\b.txt"</c>. Here <c>?</c> is A-Z, a-z.
	/// - <c>@"?:"</c> - drive name, like <c>@"C:"</c>. Here <c>?</c> is A-Z, a-z.
	/// - <c>@"\\*"</c> - network path, like <c>@"\\server\share\..."</c>. Or has prefix <c>@"\\?\"</c>.
	/// 
	/// Supports only file-system paths. Returns <c>false</c> if <i>path</i> is URL (<see cref="IsUri"/>) or starts with <c>"::"</c>.
	/// </remarks>
	public static bool IsFullPathExpand(ref string path, bool? strict = null) => IsFullPathExpand(path, out path, strict);
	
	/// <param name="path">
	/// Any string. Can be <c>null</c>.
	/// If starts with <c>'%'</c> character, calls <see cref="Expand"/> and then <see cref="IsFullPath"/> with expanded environment variables. If it returns <c>true</c>, sets <i>path2</i> = the expanded path string.
	/// </param>
	/// <param name="path2">Receives the final path.</param>
	/// <inheritdoc cref="IsFullPathExpand(ref string, bool?)"/>
	public static bool IsFullPathExpand(string path, out string path2, bool? strict = null) {
		var s = path2 = path;
		if (s.Lenn() < 2) return false;
		if (s[0] != '%') return IsFullPath(s);
		s = Expand(s, strict);
		if (s[0] == '%') return false;
		if (!IsFullPath(s)) return false;
		path2 = s;
		return true;
	}
	
	/// <summary>
	/// Calls <c>Path.GetPathRoot</c>. If no <c>'\\'</c> or <c>'/'</c> at the end, appends <c>"\\"</c>.
	/// Tested: <c>Path.GetPathRoot</c> returns network path like <c>@"\\server\share"</c>. API <c>PathSkipRoot</c> returns with <c>'\\'</c>.
	/// </summary>
	internal static string GetRootBS_(string s) {
		s = Path.GetPathRoot(s);
		if (!Path.EndsInDirectorySeparator(s)) s += "\\";
		return s;
	}
	
	/// <summary>
	/// Returns the length of the URI scheme in string, including <c>':'</c>.
	/// Returns 0 if the string does not start with a scheme.
	/// </summary>
	/// <param name="s">A URI or path or any string or <c>null</c>.</param>
	internal static int GetUriSchemeLengthWithColon_(RStr s) {
		int len = s.Length;
		if (len > 2 && s[0].IsAsciiAlpha() && s[1] != ':') {
			len = s.IndexOf(':');
			if (len > 1) {
				for (int i = 1; i < len; i++)
					if (!(s[i].IsAsciiAlphaDigit() || s[i] is '.' or '-' or '+')) return 0;
			}
			return len + 1;
		}
		return 0;
	}
	
	/// <summary>
	/// Returns <c>true</c> if the string starts with a URI scheme (known or not) and <c>':'</c> character.
	/// </summary>
	/// <param name="s">A URI or path or any string or <c>null</c>.</param>
	/// <remarks>
	/// URI examples: <c>"https:"</c>, <c>"https://www.x.com"</c>, <c>"file:///path"</c>, <c>"shell:something"</c>.
	/// </remarks>
	public static bool IsUri(RStr s)
		=> 0 != GetUriSchemeLengthWithColon_(s);
	
	/// <summary>
	/// Concatenates two path parts using character <c>'\\'</c>. For example directory path and file name.
	/// </summary>
	/// <param name="s1">First part. Usually a directory. Can optionally end with a directory separator character. Cannot be <c>null</c>/empty.</param>
	/// <param name="s2">Second part. Usually a filename or relative path.</param>
	/// <param name="s2CanBeFullPath">If <i>s2</i> is full path, return <i>s2</i>.</param>
	/// <param name="prefixLongPath">Call <see cref="PrefixLongPathIfNeed"/> which may prepend <c>@"\\?\"</c> if the result path is very long. Default <c>true</c>.</param>
	/// <exception cref="ArgumentException"><i>s1</i> is <c>null</c> or empty.</exception>
	/// <remarks>
	/// Calls <see cref="Path.Join"/>, unless <i>s2CanBeFullPath</i> true and <i>s2</i> is full path.
	/// 
	/// With default values of <i>s2CanBeFullPath</i> and <i>prefixLongPath</i>, this method is the same as the string extension operator <c>/</c>.
	/// </remarks>
	/// <seealso cref="Normalize"/>
	public static string Join(string s1, string s2, bool s2CanBeFullPath = false, bool prefixLongPath = true) {
		ArgumentException.ThrowIfNullOrEmpty(s1);
		string r = s2CanBeFullPath && IsFullPath(s2) ? s2 : Path.Join(s1, s2);
		if (prefixLongPath) r = PrefixLongPathIfNeed(r);
		return r;
	}
	
	//FUTURE: test HTML doc generation after updating DocFX. Now DocFX (updated 2026-10-05) skips extension members.
	extension(string) {
		/// <summary>
		/// Calls <see cref="Path.Join(string, string)"/> and <see cref="PrefixLongPathIfNeed"/>.
		/// The same as <see cref="Join"/> with default parameter values.
		/// </summary>
		/// <exception cref="ArgumentException"><i>s1</i> is <c>null</c> or empty.</exception>
		public static string operator /(string s1, string s2) {
			ArgumentException.ThrowIfNullOrEmpty(s1);
			string r = Path.Join(s1, s2);
			return PrefixLongPathIfNeed(r);
		}
	}
	
	/// <summary>
	/// Combines two path parts. Also calls <c>PrefixLongPathIfNeed</c>.
	/// Unlike <see cref="Join"/>, fails if some part is empty or <c>@"\"</c> or if <i>s2</i> is <c>@"\\"</c>. Also does not check <i>s2</i> full path.
	/// If fails, throws exception or returns <c>null</c> (if <i>noException</i>).
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	internal static string Join_(string s1, string s2, bool noException = false) {
		if (!s1.NE() && !s2.NE()) {
			int k = 0;
			if (IsSepChar_(s1[^1])) {
				if (s1.Length == 1) goto ge;
				k |= 1;
			}
			if (IsSepChar_(s2[0])) {
				if (s2.Length == 1 || IsSepChar_(s2[1])) goto ge;
				k |= 2;
			}
			var r = k switch {
				0 => s1 + @"\" + s2,
				3 => s1 + s2[1..],
				_ => s1 + s2,
			};
			return PrefixLongPathIfNeed(r);
		}
		ge:
		if (noException) return null;
		throw new ArgumentException("Empty filename or path.");
	}
	
	/// <summary>
	/// Returns <c>true</c> if character <c>c is '\\' or '/'</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsSepChar_(char c) { return c is '\\' or '/'; }
	
	/// <summary>
	/// Returns <c>true</c> if <i>s</i> starts with two directory separator characters.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	internal static bool StartsWithTwoSep_(string s) => s.Lenn() >= 2 && IsSepChar_(s[0]) && IsSepChar_(s[1]);
	
	///// <summary>
	///// Returns <c>true</c> if ends with <c>'\\'</c> or <c>'/'</c>.
	///// </summary>
	//internal static bool EndsWithSep_(RStr s) { return s.Length > 0 && s[^1] is '\\' or '/'; }
	
	/// <summary>
	/// Returns <c>true</c> if ends with <c>':'</c> preceded by a drive letter, like <c>"C:"</c> or <c>"more\C:"</c>, but not like <c>"moreC:"</c>.
	/// </summary>
	static bool _EndsWithDriveWithoutSep(RStr s) {
		int i = s.Length - 1;
		if (i < 1 || s[i] != ':') return false;
		if (!s[--i].IsAsciiAlpha()) return false;
		if (i > 0 && !IsSepChar_(s[i - 1])) return false;
		return true;
	}
	
	/// <summary>
	/// If <i>s</i> is like <c>"C:"</c>, returns <c>"C:\"</c>, else returns <i>s</i>.
	/// </summary>
	static string _AddSepToDrive(string s) => _EndsWithDriveWithoutSep(s) ? s + "\\" : s;
	
	/// <summary>
	/// Makes normal full path from path that can contain special substrings etc.
	/// </summary>
	/// <param name="path">Any path.</param>
	/// <param name="defaultParentDirectory">If <i>path</i> is not full path, combine it with <i>defaultParentDirectory</i> to make full path.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentException"><i>path</i> is not full path, and <i>defaultParentDirectory</i> is not used or does not make it full path.</exception>
	/// <remarks>
	/// The sequence of actions:
	/// 1. If <i>path</i> starts with <c>'%'</c> character, expands environment variables and special folder names. See <see cref="Expand"/>.
	/// 2. If <i>path</i> is not full path but looks like URL, and used flag <c>CanBeUrlOrShell</c>, returns <i>path</i>.
	/// 3. If <i>path</i> is not full path, and <i>defaultParentDirectory</i> is not <c>null</c>/<c>""</c>, combines <i>path</i> with <c>Path2.Expand(defaultParentDirectory)</c>.
	/// 4. If <i>path</i> is not full path, throws exception.
	/// 5. If <i>path</i> is like <c>"C:"</c> makes like <c>"C:\"</c>.
	/// 6. Calls API <ms>GetFullPathName</ms>. It replaces <c>'/'</c> with <c>'\\'</c>, replaces multiple <c>'\\'</c> with single (where need), processes <c>@"\.."</c> etc, trims spaces, etc.
	/// 7. If no flag <c>DontExpandDosPath</c>, if <i>path</i> looks like a short DOS path version (contains <c>'~'</c> etc), calls API <ms>GetLongPathName</ms>. It converts short DOS path to normal path, if possible, for example <c>@"c:\progra~1"</c> to <c>@"c:\program files"</c>. It is slow. It converts path only if the file exists.
	/// 8. If no flag <c>DontRemoveEndSeparator</c>, and string ends with <c>'\\'</c> character, and length > 4, removes the <c>'\\'</c>, unless then it would be a path to an existing file (not directory).
	/// 9. If no flag <c>DontPrefixLongPath</c>, calls <see cref="PrefixLongPathIfNeed"/>, which adds <c>@"\\?\"</c> etc prefix if path is very long.
	/// 
	/// Similar to <see cref="Path.GetFullPath"/>. Main differences: this function expands environment variables, does not support relative paths (unless used <i>defaultParentDirectory</i>), trims <c>'\\'</c> at the end if need.
	/// </remarks>
	/// <seealso cref="filesystem.more.getFinalPath"/>
	public static string Normalize(string path, string defaultParentDirectory = null, PNFlags flags = 0) {
		if (!IsFullPathExpand(ref path)) {
			if (0 != (flags & PNFlags.CanBeUrlOrShell)) if (IsShellPathOrUri_(path)) return path;
			if (defaultParentDirectory.NE()) goto ge;
			path = Join_(Expand(defaultParentDirectory), path);
			if (!IsFullPath(path)) goto ge;
		}
		
		return Normalize_(path, flags, noExpandEV: true);
		ge:
		throw new ArgumentException($"Not full path: '{path}'.");
	}
	
	/// <summary>
	/// Same as <see cref="Normalize"/>, but skips full-path checking.
	/// <i>s</i> should be full path. If not full and not <c>null</c>/<c>""</c>, combines with current directory.
	/// </summary>
	internal static string Normalize_(string s, PNFlags flags = 0, bool noExpandEV = false) {
		if (!s.NE()) {
			if (!noExpandEV) s = Expand(s);
			Debug_.PrintIf(IsShellPathOrUri_(s), s);
			
			s = _AddSepToDrive(s); //API would append current directory
			
			//note: although slower, call GetFullPathName always, not just when contains @"..\" etc.
			//	Because it does many things (see Normalize doc), not all documented.
			//	We still ~2 times faster than Path.GetFullPath (tested before Core).
			Api.GetFullPathName(s, out s);
			
			if (0 == (flags & PNFlags.DontExpandDosPath) && IsPossiblyDos_(s)) s = ExpandDosPath_(s);
			
			if (0 == (flags & PNFlags.DontRemoveEndSeparator) && IsSepChar_(s[^1]) && s.Length > 4) {
				var s2 = s[..^1];
				if (Api.GetFileAttributes(s2).Has(FileAttributes.Directory)) s = s2; //if does not exist as file
			}
			
			if (0 == (flags & PNFlags.DontPrefixLongPath)) s = PrefixLongPathIfNeed(s);
		}
		return s;
	}
	
	/// <summary>
	/// Prepares path for passing to API and .NET functions that support <c>".."</c>, DOS path etc.
	/// Calls <c>Expand</c>, <c>_AddSepToDrive</c>, <see cref="PrefixLongPathIfNeed"/>. By default throws exception if <c>!IsFullPath(path)</c>.
	/// </summary>
	/// <exception cref="ArgumentException">Not full path (only if <i>throwIfNotFullPath</i> is <c>true</c>).</exception>
	internal static string NormalizeMinimally_(string path, bool throwIfNotFullPath = true) {
		var s = Expand(path);
		Debug_.PrintIf(IsShellPathOrUri_(s), s);
		if (throwIfNotFullPath && !IsFullPath(s)) throw new ArgumentException($"Not full path: '{path}'.");
		s = _AddSepToDrive(s);
		s = PrefixLongPathIfNeed(s);
		return s;
	}
	
	/// <summary>
	/// Calls API <c>GetLongPathName</c>.
	/// Does not check whether <i>s</i> contains <c>'~'</c> character etc. Note: the API is slow.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	internal static string ExpandDosPath_(string s) {
		if (!s.NE()) Api.GetLongPathName(s, out s);
		return s;
		//CONSIDER: the API fails if the file does not exist.
		//	Workaround: if filename does not contain '~', pass only the part that contains.
	}
	
	/// <summary>
	/// Returns <c>true</c> if <i>s</i> looks like a DOS filename or path.
	/// Examples: <c>"abcde~12"</c>, <c>"abcde~12.txt"</c>, <c>@"c:\path\abcde~12.txt"</c>, <c>"c:\abcde~12\path"</c>.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	internal static bool IsPossiblyDos_(string s) {
		//print.it(s);
		if (s != null && s.Length >= 8) {
			for (int i = 0; (i = s.IndexOf('~', i + 1)) > 0;) {
				int j = i + 1, k = 0;
				for (; k < 6 && j < s.Length; k++, j++) if (!s[j].IsAsciiDigit()) break;
				if (k == 0) continue;
				char c = j < s.Length ? s[j] : '\\';
				if (c == '\\' || c == '/' || (c == '.' && j == s.Length - 4)) {
					for (j = i; j > 0; j--) {
						c = s[j - 1]; if (c == '\\' || c == '/') break;
					}
					if (j == i - (7 - k)) return true;
				}
			}
		}
		return false;
	}
	
	/// <summary>
	/// Returns <c>true</c> if starts with <c>"::"</c>.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	internal static bool IsShellPath_(RStr s) {
		return s.Length >= 2 && s[0] == ':' && s[1] == ':';
	}
	
	/// <summary>
	/// Returns <c>true</c> if <c>IsShellPath_(s) || IsUri(s)</c>.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	internal static bool IsShellPathOrUri_(RStr s) => IsShellPath_(s) || IsUri(s);
	
	/// <summary>
	/// If <i>path</i> is full path (see <see cref="IsFullPath"/>) and does not start with <c>@"\\?\"</c>, prepends <c>@"\\?\"</c>.
	/// If <i>path</i> is network path (like <c>@"\\computer\folder\..."</c>), makes like <c>@"\\?\UNC\computer\folder\..."</c>.
	/// </summary>
	/// <param name="path">
	/// Path. Can be <c>null</c>.
	/// Must not start with <c>"%environmentVariable%"</c>. This function does not expand it. See <see cref="Expand"/>.
	/// </param>
	/// <remarks>
	/// Windows API kernel functions support extended-length paths, ie longer than 259 characters. But the path must have this prefix. Windows API shell functions don't support it.
	/// </remarks>
	public static string PrefixLongPath(string path) {
		var s = path;
		if (IsFullPath(s) && 0 == _GetPrefixLength(s)) {
			if (s.Length >= 2 && IsSepChar_(s[0]) && IsSepChar_(s[1])) s = s.ReplaceAt(0, 2, @"\\?\UNC\");
			else s = @"\\?\" + s;
		}
		return s;
	}
	
	/// <summary>
	/// Calls <see cref="PrefixLongPath"/> if <i>path</i> is longer than <see cref="MaxSafeDirectoryPathLength"/> (247).
	/// </summary>
	/// <param name="path">
	/// Path. Can be <c>null</c>.
	/// Must not start with <c>"%environmentVariable%"</c>. This function does not expand it. See <see cref="Expand"/>.
	/// </param>
	public static string PrefixLongPathIfNeed(string path) {
		if (path.Lenn() > MaxSafeDirectoryPathLength) path = PrefixLongPath(path);
		return path;
		
		//info: MaxDirectoryPathLength is max length supported by API CreateDirectory.
	}
	
	/// <summary>
	/// If <i>path</i> starts with <c>@"\\?\"</c> prefix, removes it.
	/// If <i>path</i> starts with <c>@"\\?\UNC\"</c> prefix, removes <c>@"?\UNC\"</c>.
	/// </summary>
	/// <param name="path">
	/// Path. Can be <c>null</c>.
	/// Must not start with <c>"%environmentVariable%"</c>. This function does not expand it. See <see cref="Expand"/>.
	/// </param>
	public static string UnprefixLongPath(string path) {
		if (!path.NE()) {
			switch (_GetPrefixLength(path)) {
			case 4: return path[4..];
			case 8: return path.Remove(2, 6);
			}
		}
		return path;
	}
	
	/// <summary>
	/// If <i>s</i> starts with <c>@"\\?\UNC\"</c>, returns 8.
	/// Else if starts with <c>@"\\?\"</c>, returns 4.
	/// Else returns 0.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	static int _GetPrefixLength(RStr s) {
		int len = s.Length;
		if (len >= 4 && s[2] == '?' && IsSepChar_(s[0]) && IsSepChar_(s[1]) && IsSepChar_(s[3])) {
			if (len >= 8 && IsSepChar_(s[7]) && s[4..7].Eqi("UNC")) return 8;
			return 4;
		}
		return 0;
	}
	
	/// <summary>
	/// Maximal file (not directory) path length supported by all functions (native, .NET and this library).
	/// For longer paths need <c>@"\\?\"</c> prefix. It is supported by most native kernel API (but not shell API) and most functions of this library and .NET.
	/// </summary>
	public const int MaxSafeFilePathLength = 259;
	
	/// <summary>
	/// Maximal directory path length supported by all functions (native, .NET and this library).
	/// For longer paths need <c>@"\\?\"</c> prefix. It is supported by most native kernel API (but not shell API) and most functions of this library and .NET.
	/// </summary>
	public const int MaxSafeDirectoryPathLength = 247;
	
	/// <summary>
	/// Replaces characters that cannot be used in file names.
	/// </summary>
	/// <param name="name">Initial filename.</param>
	/// <param name="invalidCharReplacement">A string that will replace each invalid character. Default <c>"-"</c>.</param>
	/// <remarks>
	/// Also corrects other forms of invalid or problematic filename: trims spaces and other blank characters; replaces <c>"."</c> at the end; prepends <c>"@"</c> if a reserved name like <c>"CON"</c> or <c>"CON.txt"</c>; returns <c>"-"</c> if <i>name</i> is <c>null</c>/empty/whitespace.
	/// Usually returns valid filename, however it can be too long (itself or when combined with a directory path).
	/// </remarks>
	public static string MakeValidName(string name, string invalidCharReplacement = "-") {
		if (name == null || (name = name.Trim()).Length == 0) return invalidCharReplacement;
		name = _rxInvalidFN1.Replace(name, invalidCharReplacement).Trim();
		if (_rxInvalidFN2.IsMatch(name)) name = "@" + name;
		return name;
	}
	
	static regexp _rxInvalidFN1 = new(@"\.$|[\\/|<>?*:""\x00-\x1f]");
	static regexp _rxInvalidFN2 = new(@"(?i)^(CON|PRN|AUX|NUL|COM\d|LPT\d)(\.|$)");
	
	/// <summary>
	/// Returns <c>true</c> if name cannot be used for a file name, eg contains <c>'\\'</c> etc characters or is empty.
	/// More info: <see cref="MakeValidName"/>.
	/// </summary>
	/// <param name="name">Any string. Example: <c>"name.txt"</c>. Can be <c>null</c>.</param>
	public static bool IsInvalidName(string name) {
		if (name == null || (name = name.Trim()).Length == 0) return true;
		return _rxInvalidFN1.IsMatch(name) || _rxInvalidFN2.IsMatch(name);
	}
	
	/// <summary>
	/// Returns <c>true</c> if character <i>c</i> is invalid in file names (the filename part).
	/// </summary>
	public static bool IsInvalidNameChar(char c)
		=> c is < ' ' or '"' or '<' or '>' or '|' or '*' or '?' or ':' or '\\' or '/';
	
	/// <summary>
	/// Returns <c>true</c> if character <i>c</i> is invalid in file paths.
	/// </summary>
	public static bool IsInvalidPathChar(char c)
		=> c is < ' ' or '"' or '<' or '>' or '|' or '*' or '?';
	
	/// <summary>
	/// Gets the filename from <i>path</i>.
	/// Examples: <c>@"C:\path\name.ext"</c> -> <c>@"name.ext"</c>; <c>@"C:\path\name\"</c> -> <c>@"name"</c>; <c>@"name.ext"</c> -> <c>@"name.ext"</c>.
	/// </summary>
	/// <param name="path">Path or filename. Can be <c>null</c>.</param>
	/// <returns>Returns <i>path</i> if there is no directory separator.</returns>
	/// <remarks>
	/// Calls <see cref="Path.GetFileName"/>, after removing trailing directory separators from <i>path</i>.
	/// </remarks>
	public static string GetName(string path) {
		if (Path.EndsInDirectorySeparator(path)) return Path.GetFileName(path.AsSpan().TrimEnd(['/', '\\'])).ToString();
		return Path.GetFileName(path);
	}
	//note: don't need GetNameNoExt. For file paths, Path.GetFileNameWithoutExtension is good. For directory paths, GetName is good.
	
	/// <summary>
	/// Gets the parent directory path from <i>path</i>.
	/// Examples: <c>@"C:\path\name.ext"</c> -> <c>@"C:\path"</c>; <c>@"C:\path\name\"</c> -> <c>@"C:\path"</c>; <c>@"C:\path"</c> -> <c>@"C:\"</c>; <c>@"name.ext"</c> -> <c>null</c>.
	/// </summary>
	/// <param name="withSeparator">Don't remove path separator character(s) at the end of the result.</param>
	/// <param name="path">Path or filename. Can be <c>null</c>.</param>
	/// <returns><c>null</c> if there is no parent directory.</returns>
	/// <remarks>
	/// Calls <see cref="Path.GetDirectoryName(ReadOnlySpan{char})"/>, after removing trailing directory separators from <i>path</i>.
	/// </remarks>
	public static string GetDirectory(string path, bool withSeparator = false) {
		var s = path.AsSpan().TrimEnd(['/', '\\']);
		var r = Path.GetDirectoryName(s);
		if (r.IsEmpty) return null;
		if (withSeparator) while (r.Length < s.Length && IsSepChar_(s[r.Length])) r = s.Slice(0, r.Length + 1);
		return r.ToString();
	}
	
	/// <summary>
	/// Returns <c>true</c> if <i>s</i> is like <c>".ext"</c> and the ext part does not contain characters <c>.\\/:</c> and does not start/end with whitespace.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	internal static bool IsExtension_(RStr s) {
		if (s.Length < 2 || s[0] != '.') return false;
		for (int i = 1; i < s.Length; i++) {
			switch (s[i]) {
			case '.' or '\\' or '/' or ':': return false;
			case <= ' ' when i == 1 || i == s.Length - 1: return false;
			}
		}
		return true;
	}
	
	/// <summary>
	/// Returns <c>true</c> if <i>s</i> is like <c>"scheme:"</c> and not like <c>"c:"</c> or <c>"scheme:more"</c>.
	/// </summary>
	/// <param name="s">Can be <c>null</c>.</param>
	internal static bool IsUriSchemeAndColon_(RStr s) {
		return s.Length > 2 && s[^1] == ':' && GetUriSchemeLengthWithColon_(s) == s.Length;
	}
	
	/// <summary>
	/// Creates path with unique filename for a new file or directory. 
	/// If the specified path is of an existing file or directory, returns path where the filename part is modified like <c>"file 2.txt"</c>, <c>"file 3.txt"</c> etc. Else returns unchanged <i>path</i>.
	/// </summary>
	/// <param name="path">Full path.</param>
	/// <param name="isDirectory">The path is for a directory. Appends the number at the very end, not before <c>.extension</c>.</param>
	public static string MakeUnique(string path, bool isDirectory) { //TODO: test
		if (!filesystem.exists(path)) return path;
		string ext = null;
		if (!isDirectory && Path.GetExtension(path.AsSpan()) is { Length: > 0 } ext2) (ext, path) = (ext2.ToString(), path[..^ext2.Length]);
		for (int i = 2; ; i++) {
			var s = path + " " + i + ext;
			if (!filesystem.exists(s)) return s;
		}
	}
}
