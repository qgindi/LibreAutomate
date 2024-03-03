//tested: System.IO.Path functions improved in Core.
//	No exceptions if path contains invalid characters. Although the exceptions are still documented in MSDN.
//	Support long paths and file streams.
//	Faster, etc.

namespace Au {
	/// <summary>
	/// File path string functions. Parse, combine, make full, make unique, make valid, expand variables, etc.
	/// </summary>
	/// <remarks>
	/// Most functions of this class work with strings and don't access the file system. Several functions query file system info.
	/// 
	/// Functions of this class don't throw exceptions when path is invalid (path format, invalid characters). Only <see cref="normalize"/> throws exception if not full path.
	/// 
	/// Also you can use .NET class <see cref="Path"/>. In its documentation you'll find more info about paths.
	/// </remarks>
	public static unsafe class pathname { //BAD: why pathname? Better would be eg filepath.
		/// <summary>
		/// If <i>path</i> starts with <c>"%"</c> or <c>"\"%"</c>, expands environment variables enclosed in %, else just returns <i>path</i>.
		/// Also supports known folder names, like <c>"%folders.Documents%"</c>. More info in Remarks.
		/// </summary>
		/// <param name="path">Any string. Can be <c>null</c>.</param>
		/// <param name="strict">
		/// What to do if <i>path</i> looks like starts with and environment variable or known folder but the variable/folder does not exist:
		/// <br/>• <c>true</c> - throw <b>ArgumentException</b>;
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
		/// This function is called by many functions of classes <b>pathname</b>, <b>filesystem</b>, <b>icon</b>, some others, therefore all they support environment variables and known folders in path string.
		/// </remarks>
		/// <seealso cref="Environment.ExpandEnvironmentVariables"/>
		/// <seealso cref="Environment.GetEnvironmentVariable"/>
		/// <seealso cref="Environment.SetEnvironmentVariable"/>
		public static string expand(string path, bool? strict = null) {
			var s = path;
			if (s.Lenn() < 3) return s;
			if (s[0] != '%') {
				if (s[0] == '"' && s[1] == '%') return "\"" + expand(s[1..], strict);
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
			return expand(s, strict); //can be %envVar2% in envVar1 value
		}

		/// <summary>
		/// Returns <c>true</c> if the string is full path, like <c>@"C:\a\b.txt"</c> or <c>@"C:"</c> or <c>@"\\server\share\..."</c>:
		/// </summary>
		/// <param name="path">Any string. Can be <c>null</c>.</param>
		/// <param name="orEnvVar">Also return <c>true</c> if starts with <c>"%environmentVariable%"</c> or <c>"%folders.Folder%"</c>. Note: this function does not check whether the variable/folder exists; for it use <see cref="isFullPathExpand"/> instead.</param>
		/// <remarks>
		/// Returns <c>true</c> if <i>path</i> matches one of these wildcard patterns:
		/// - <c>@"?:\*"</c> - local path, like <c>@"C:\a\b.txt"</c>. Here ? is A-Z, a-z.
		/// - <c>@"?:"</c> - drive name, like <c>@"C:"</c>. Here ? is A-Z, a-z.
		/// - <c>@"\\*"</c> - network path, like <c>@"\\server\share\..."</c>. Or has prefix <c>@"\\?\"</c>.
		/// 
		/// Supports <c>'/'</c> characters too.
		/// 
		/// Supports only file-system paths. Returns <c>false</c> if <i>path</i> is URL (<see cref="isUrl"/>) or starts with <c>"::"</c>.
		/// </remarks>
		public static bool isFullPath(RStr path, bool orEnvVar = false) {
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
		/// Expands environment variables and calls/returns <see cref="isFullPath"/>.
		/// </summary>
		/// <returns><c>true</c> if the string is full path, like <c>@"C:\a\b.txt"</c> or <c>@"C:"</c> or <c>@"\\server\share\..."</c>.</returns>
		/// <param name="path">
		/// Any string. Can be <c>null</c>.
		/// If starts with <c>'%'</c> character, calls <see cref="isFullPath"/> with expanded environment variables (<see cref="expand"/>). If it returns <c>true</c>, replaces the passed variable with the expanded path string.
		/// </param>
		/// <param name="strict"><inheritdoc cref="expand(string, bool?)" path="/param[@name='strict']/node()"/></param>
		/// <remarks>
		/// Returns <c>true</c> if <i>path</i> matches one of these wildcard patterns:
		/// - <c>@"?:\*"</c> - local path, like <c>@"C:\a\b.txt"</c>. Here ? is A-Z, a-z.
		/// - <c>@"?:"</c> - drive name, like <c>@"C:"</c>. Here ? is A-Z, a-z.
		/// - <c>@"\\*"</c> - network path, like <c>@"\\server\share\..."</c>. Or has prefix <c>@"\\?\"</c>.
		/// 
		/// Supports <c>'/'</c> characters too.
		/// Supports only file-system paths. Returns <c>false</c> if <i>path</i> is URL (<see cref="isUrl"/>) or starts with <c>"::"</c>.
		/// </remarks>
		public static bool isFullPathExpand(ref string path, bool? strict = null) {
			var s = path;
			if (s == null || s.Length < 2) return false;
			if (s[0] != '%') return isFullPath(s);
			s = expand(s, strict);
			if (s[0] == '%') return false;
			if (!isFullPath(s)) return false;
			path = s;
			return true;
		}

		/// <summary>
		/// Gets the length of the drive or network folder part in <i>path</i>, like <c>@"C:\"</c>, <c>@"\\server\share\"</c>, <c>@"\\?\C:\"</c>, <c>@"\\?\UNC\server\share\"</c>, etc.
		/// </summary>
		/// <param name="path">Full path or any string. Can be <c>null</c>. Should not be <c>@"%environmentVariable%\..."</c>.</param>
		/// <remarks>
		/// See <see cref="Path.GetPathRoot"/>.
		/// </remarks>
		public static int getRootLength(RStr path) {
			int i = Path.GetPathRoot(path).Length; //Span, no alloc
			if (i > 0 && i < path.Length && !IsSepChar_(path[i - 1]) && IsSepChar_(path[i])) i++; //@"\\server\share" -> @"\\server\share\"
			return i;
		}

		/// <summary>
		/// Calls <b>Path.GetPathRoot</b>. If no '\\' or '/' at the end, appends "\\".
		/// Tested: <b>Path.GetPathRoot</b> returns network path like @"\\server\share". API <b>PathSkipRoot</b> returns with '\\'.
		/// </summary>
		internal static string GetRootBS_(string s) {
			s = Path.GetPathRoot(s);
			if (!Path.EndsInDirectorySeparator(s)) s += "\\";
			return s;
		}

		/// <summary>
		/// Gets the length of the URL protocol name (also known as URI scheme) in string, including <c>':'</c>.
		/// If the string does not start with a protocol name, returns 0.
		/// </summary>
		/// <param name="s">A URL or path or any string. Can be <c>null</c>.</param>
		/// <remarks>
		/// URL examples: <c>"http:"</c> (returns 5), <c>"http://www.x.com"</c> (returns 5), <c>"file:///path"</c> (returns 5), <c>"shell:etc"</c> (returns 6).
		/// 
		/// The protocol can be unknown. The function just checks string format, which is an ASCII alpha character followed by one or more ASCII alpha-numeric, <c>'.'</c>, <c>'-'</c>, <c>'+'</c> characters, followed by <c>':'</c> character.
		/// </remarks>
		public static int getUrlProtocolLength(RStr s) {
			int len = s.Length;
			if (len > 2 && s[0].IsAsciiAlpha() && s[1] != ':') {
				for (int i = 1; i < len; i++) {
					var c = s[i];
					if (c == ':') return i + 1;
					if (!(c.IsAsciiAlphaDigit() || c == '.' || c == '-' || c == '+')) break;
				}
			}
			return 0;
			//info: API PathIsURL lies, like most shlwapi.dll functions.
		}

		/// <summary>
		/// Returns <c>true</c> if the string starts with a URL protocol name (existing or not) and <c>':'</c> character.
		/// Calls <see cref="getUrlProtocolLength"/> and returns <c>true</c> if it's not 0.
		/// </summary>
		/// <param name="s">A URL or path or any string. Can be <c>null</c>.</param>
		/// <remarks>
		/// URL examples: <c>"http:"</c>, <c>"http://www.x.com"</c>, <c>"file:///path"</c>, <c>"shell:etc"</c>.
		/// </remarks>
		public static bool isUrl(RStr s) {
			return 0 != getUrlProtocolLength(s);
		}

		/// <summary>
		/// Combines two path parts using character <c>'\\'</c>. For example directory path and file name.
		/// </summary>
		/// <param name="s1">First part. Usually a directory.</param>
		/// <param name="s2">Second part. Usually a filename or relative path.</param>
		/// <param name="s2CanBeFullPath"><i>s2</i> can be full path. If it is, ignore <i>s1</i> and return <i>s2</i> with expanded environment variables. If <c>false</c> (default), simply combines <i>s1</i> and <i>s2</i>.</param>
		/// <param name="prefixLongPath">Call <see cref="prefixLongPathIfNeed"/> which may prepend <c>@"\\?\"</c> if the result path is very long. Default <c>true</c>.</param>
		/// <remarks>
		/// If <i>s1</i> and <i>s2</i> are <c>null</c> or <c>""</c>, returns <c>""</c>. Else if <i>s1</i> is <c>null</c> or <c>""</c>, returns <i>s2</i>. Else if <i>s2</i> is <c>null</c> or <c>""</c>, returns <i>s1</i>.
		/// Does not expand environment variables. For it use <see cref="expand"/> before, or <see cref="normalize"/> instead. Path that starts with an environment variable here is considered not full path.
		/// Similar to <see cref="Path.Combine"/>. Main differences: has some options; supports <c>null</c> arguments.
		/// </remarks>
		/// <seealso cref="normalize"/>
		public static string combine(string s1, string s2, bool s2CanBeFullPath = false, bool prefixLongPath = true) {
			string r;
			if (s1.NE()) r = s2 ?? "";
			else if (s2.NE()) r = s1 ?? "";
			else if (s2CanBeFullPath && isFullPath(s2)) r = s2;
			else {
				int k = 0;
				if (IsSepChar_(s1[^1])) k |= 1;
				if (IsSepChar_(s2[0])) k |= 2;
				switch (k) {
				case 0: r = s1 + @"\" + s2; break;
				case 3: r = s1 + s2[1..]; break;
				default: r = s1 + s2; break;
				}
			}
			if (prefixLongPath) r = prefixLongPathIfNeed(r);
			return r;
		}

		/// <summary>
		/// Combines two path parts.
		/// Unlike <see cref="combine"/>, fails if some part is empty or <c>@"\"</c> or if <i>s2</i> is <c>@"\\"</c>. Also does not check <i>s2</i> full path.
		/// If fails, throws exception or returns <c>null</c> (if <i>noException</i>).
		/// </summary>
		/// <exception cref="ArgumentException"></exception>
		internal static string Combine_(string s1, string s2, bool noException = false) {
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
				return prefixLongPathIfNeed(r);
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
		/// Returns <c>true</c> if s starts with "\\". Supports '/'.
		/// </summary>
		/// <param name="s">Can be <c>null</c>.</param>
		internal static bool StartsWithTwoSlash_(string s) => s.Lenn() >= 2 && IsSepChar_(s[0]) && IsSepChar_(s[1]);

		///// <summary>
		///// Returns <c>true</c> if ends with '\\' or '/'.
		///// </summary>
		//internal static bool EndsWithSep_(RStr s) { return s.Length > 0 && s[^1] is '\\' or '/'; }

		/// <summary>
		/// Returns <c>true</c> if ends with <c>':'</c> preceded by a drive letter, like "C:" or "more\C:", but not like "moreC:".
		/// </summary>
		static bool _EndsWithDriveWithoutSep(RStr s) {
			if (s == null) return false;
			int i = s.Length - 1;
			if (i < 1 || s[i] != ':') return false;
			if (!s[--i].IsAsciiAlpha()) return false;
			if (i > 0 && !IsSepChar_(s[i - 1])) return false;
			return true;
		}

		/// <summary>
		/// If s is like "C:", returns "C:\", else returns s.
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
		/// 1. If <i>path</i> starts with <c>'%'</c> character, expands environment variables and special folder names. See <see cref="expand"/>.
		/// 2. If <i>path</i> is not full path but looks like URL, and used flag <b>CanBeUrl</b>, returns <i>path</i>.
		/// 3. If <i>path</i> is not full path, and <i>defaultParentDirectory</i> is not <c>null</c>/<c>""</c>, combines <i>path</i> with <c>expand(defaultParentDirectory)</c>.
		/// 4. If <i>path</i> is not full path, throws exception.
		/// 5. If <i>path</i> is like <c>"C:"</c> makes like <c>"C:\"</c>.
		/// 6. Calls API <msdn>GetFullPathName</msdn>. It replaces <c>'/'</c> with <c>'\\'</c>, replaces multiple <c>'\\'</c> with single (where need), processes <c>@"\.."</c> etc, trims spaces, etc.
		/// 7. If no flag <b>DontExpandDosPath</b>, if <i>path</i> looks like a short DOS path version (contains <c>'~'</c> etc), calls API <msdn>GetLongPathName</msdn>. It converts short DOS path to normal path, if possible, for example <c>@"c:\progra~1"</c> to <c>@"c:\program files"</c>. It is slow. It converts path only if the file exists.
		/// 8. If no flag <b>DontRemoveEndSeparator</b>, and string ends with <c>'\\'</c> character, and length &gt; 4, removes the <c>'\\'</c>, unless then it would be a path to an existing file (not directory).
		/// 9. If no flag <b>DontPrefixLongPath</b>, calls <see cref="prefixLongPathIfNeed"/>, which adds <c>@"\\?\"</c> etc prefix if path is very long.
		/// 
		/// Similar to <see cref="Path.GetFullPath"/>. Main differences: this function expands environment variables, does not support relative paths (unless used <i>defaultParentDirectory</i>), trims <c>'\\'</c> at the end if need.
		/// </remarks>
		/// <seealso cref="filesystem.more.getFinalPath"/>
		public static string normalize(string path, string defaultParentDirectory = null, PNFlags flags = 0) {
			if (!isFullPathExpand(ref path)) {
				if (0 != (flags & PNFlags.CanBeUrlOrShell)) if (IsShellPathOrUrl_(path)) return path;
				if (defaultParentDirectory.NE()) goto ge;
				path = Combine_(expand(defaultParentDirectory), path);
				if (!isFullPath(path)) goto ge;
			}

			return Normalize_(path, flags, noExpandEV: true);
			ge:
			throw new ArgumentException($"Not full path: '{path}'.");
		}

		/// <summary>
		/// Same as <see cref="normalize"/>, but skips full-path checking.
		/// s should be full path. If not full and not <c>null</c>/<c>""</c>, combines with current directory.
		/// </summary>
		internal static string Normalize_(string s, PNFlags flags = 0, bool noExpandEV = false) {
			if (!s.NE()) {
				if (!noExpandEV) s = expand(s);
				Debug_.PrintIf(IsShellPathOrUrl_(s), s);

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

				if (0 == (flags & PNFlags.DontPrefixLongPath)) s = prefixLongPathIfNeed(s);
			}
			return s;
		}

		/// <summary>
		/// Prepares path for passing to API and .NET functions that support "..", DOS path etc.
		/// Calls expand, _AddSepToDrive, prefixLongPathIfNeed. By default throws exception if !isFullPath(path).
		/// </summary>
		/// <exception cref="ArgumentException">Not full path (only if throwIfNotFullPath is <c>true</c>).</exception>
		internal static string NormalizeMinimally_(string path, bool throwIfNotFullPath = true) {
			var s = expand(path);
			Debug_.PrintIf(IsShellPathOrUrl_(s), s);
			if (throwIfNotFullPath && !isFullPath(s)) throw new ArgumentException($"Not full path: '{path}'.");
			s = _AddSepToDrive(s);
			s = prefixLongPathIfNeed(s);
			return s;
		}

		/// <summary>
		/// Calls API GetLongPathName.
		/// Does not check whether s contains <c>'~'</c> character etc. Note: the API is slow.
		/// </summary>
		/// <param name="s">Can be <c>null</c>.</param>
		internal static string ExpandDosPath_(string s) {
			if (!s.NE()) Api.GetLongPathName(s, out s);
			return s;
			//CONSIDER: the API fails if the file does not exist.
			//	Workaround: if filename does not contain '~', pass only the part that contains.
		}

		/// <summary>
		/// Returns <c>true</c> if pathOrFilename looks like a DOS filename or path.
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
		/// Returns <c>true</c> if starts with "::".
		/// </summary>
		/// <param name="s">Can be <c>null</c>.</param>
		internal static bool IsShellPath_(RStr s) {
			return s.Length >= 2 && s[0] == ':' && s[1] == ':';
		}

		/// <summary>
		/// Returns <c>true</c> if <c>IsShellPath_(s) || isUrl(s)</c>.
		/// </summary>
		/// <param name="s">Can be <c>null</c>.</param>
		internal static bool IsShellPathOrUrl_(RStr s) => IsShellPath_(s) || isUrl(s);

		/// <summary>
		/// If <i>path</i> is full path (see <see cref="isFullPath"/>) and does not start with <c>@"\\?\"</c>, prepends <c>@"\\?\"</c>.
		/// If <i>path</i> is network path (like <c>@"\\computer\folder\..."</c>), makes like <c>@"\\?\UNC\computer\folder\..."</c>.
		/// </summary>
		/// <param name="path">
		/// Path. Can be <c>null</c>.
		/// Must not start with <c>"%environmentVariable%"</c>. This function does not expand it. See <see cref="expand"/>.
		/// </param>
		/// <remarks>
		/// Windows API kernel functions support extended-length paths, ie longer than 259 characters. But the path must have this prefix. Windows API shell functions don't support it.
		/// </remarks>
		public static string prefixLongPath(string path) {
			var s = path;
			if (isFullPath(s) && 0 == _GetPrefixLength(s)) {
				if (s.Length >= 2 && IsSepChar_(s[0]) && IsSepChar_(s[1])) s = s.ReplaceAt(0, 2, @"\\?\UNC\");
				else s = @"\\?\" + s;
			}
			return s;
		}

		/// <summary>
		/// Calls <see cref="prefixLongPath"/> if <i>path</i> is longer than <see cref="maxDirectoryPathLength"/> (247).
		/// </summary>
		/// <param name="path">
		/// Path. Can be <c>null</c>.
		/// Must not start with <c>"%environmentVariable%"</c>. This function does not expand it. See <see cref="expand"/>.
		/// </param>
		public static string prefixLongPathIfNeed(string path) {
			if (path.Lenn() > maxDirectoryPathLength) path = prefixLongPath(path);
			return path;

			//info: MaxDirectoryPathLength is max length supported by API CreateDirectory.
		}

		/// <summary>
		/// If <i>path</i> starts with <c>@"\\?\"</c> prefix, removes it.
		/// If <i>path</i> starts with <c>@"\\?\UNC\"</c> prefix, removes <c>@"?\UNC\"</c>.
		/// </summary>
		/// <param name="path">
		/// Path. Can be <c>null</c>.
		/// Must not start with <c>"%environmentVariable%"</c>. This function does not expand it. See <see cref="expand"/>.
		/// </param>
		public static string unprefixLongPath(string path) {
			if (!path.NE()) {
				switch (_GetPrefixLength(path)) {
				case 4: return path[4..];
				case 8: return path.Remove(2, 6);
				}
			}
			return path;
		}

		/// <summary>
		/// If s starts with <c>@"\\?\UNC\"</c>, returns 8.
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
		public const int maxFilePathLength = 259;

		/// <summary>
		/// Maximal directory path length supported by all functions (native, .NET and this library).
		/// For longer paths need <c>@"\\?\"</c> prefix. It is supported by most native kernel API (but not shell API) and most functions of this library and .NET.
		/// </summary>
		public const int maxDirectoryPathLength = 247;

		/// <summary>
		/// Replaces characters that cannot be used in file names.
		/// </summary>
		/// <param name="name">Initial filename.</param>
		/// <param name="invalidCharReplacement">A string that will replace each invalid character. Default <c>"-"</c>.</param>
		/// <remarks>
		/// Also corrects other forms of invalid or problematic filename: trims spaces and other blank characters; replaces <c>"."</c> at the end; prepends <c>"@"</c> if a reserved name like <c>"CON"</c> or <c>"CON.txt"</c>; returns <c>"-"</c> if <i>name</i> is <c>null</c>/empty/whitespace.
		/// Usually returns valid filename, however it can be too long (itself or when combined with a directory path).
		/// </remarks>
		public static string correctName(string name, string invalidCharReplacement = "-") {
			if (name == null || (name = name.Trim()).Length == 0) return "-";
			name = _rxInvalidFN1.Replace(name, invalidCharReplacement).Trim();
			if (_rxInvalidFN2.IsMatch(name)) name = "@" + name;
			return name;
		}

		static regexp _rxInvalidFN1 = new(@"\.$|[\\/|<>?*:""\x00-\x1f]");
		static regexp _rxInvalidFN2 = new(@"(?i)^(CON|PRN|AUX|NUL|COM\d|LPT\d)(\.|$)");

		/// <summary>
		/// Returns <c>true</c> if name cannot be used for a file name, eg contains <c>'\\'</c> etc characters or is empty.
		/// More info: <see cref="correctName"/>.
		/// </summary>
		/// <param name="name">Any string. Example: <c>"name.txt"</c>. Can be <c>null</c>.</param>
		public static bool isInvalidName(string name) {
			if (name == null || (name = name.Trim()).Length == 0) return true;
			return _rxInvalidFN1.IsMatch(name) || _rxInvalidFN2.IsMatch(name);
		}

		/// <summary>
		/// Returns <c>true</c> if character <i>c</i> is invalid in file names (the filename part).
		/// </summary>
		public static bool isInvalidNameChar(char c)
			=> c is < ' ' or '"' or '<' or '>' or '|' or '*' or '?' or ':' or '\\' or '/';

		/// <summary>
		/// Returns <c>true</c> if character <i>c</i> is invalid in file paths.
		/// </summary>
		public static bool isInvalidPathChar(char c)
			=> c is < ' ' or '"' or '<' or '>' or '|' or '*' or '?';

		/// <summary>
		/// Gets filename from <i>path</i>. Does not remove extension.
		/// </summary>
		/// <returns>Returns <c>""</c> if there is no filename. Returns <c>null</c> if <i>path</i> is <c>null</c>.</returns>
		/// <param name="path">Path or filename. Can be <c>null</c>.</param>
		/// <remarks>
		/// Similar to <see cref="Path.GetFileName"/>. Some differences: if ends with <c>'\\'</c> or <c>'/'</c>, gets part before it, eg <c>"B"</c> from <c>@"C:\A\B\"</c>.
		/// 
		/// Supports separators <c>'\\'</c> and <c>'/'</c>.
		/// Also supports URL and shell parsing names like <c>@"::{CLSID-1}\0\::{CLSID-2}"</c>.
		/// 
		/// Examples:
		/// 
		/// | <i>path</i> | result
		/// | -
		/// | <c>@"C:\A\B\file.txt"</c> | <c>"file.txt"</c>
		/// | <c>"file.txt"</c> | <c>"file.txt"</c>
		/// | <c>"file"</c> | <c>"file"</c>
		/// | <c>@"C:\A\B"</c> | <c>"B"</c>
		/// | <c>@"C:\A\B\"</c> | <c>"B"</c>
		/// | <c>@"C:\A\/B\/"</c> | <c>"B"</c>
		/// | <c>@"C:\"</c> | <c>""</c>
		/// | <c>@"C:"</c> | <c>""</c>
		/// | <c>@"\\network\share"</c> | <c>"share"</c>
		/// | <c>@"C:\aa\file.txt:alt.stream"</c> | <c>"file.txt:alt.stream"</c>
		/// | <c>"http://a.b.c"</c> | <c>"a.b.c"</c>
		/// | <c>"::{A}\::{B}"</c> | <c>"::{B}"</c>
		/// | <c>""</c> | <c>""</c>
		/// | <c>null</c> | <c>null</c>
		/// </remarks>
		public static string getName(string path) {
			return _GetPathPart(path, _PathPart.NameWithExt);
		}

		/// <summary>
		/// Gets filename without extension.
		/// </summary>
		/// <returns>Returns <c>""</c> if there is no filename. Returns <c>null</c> if <i>path</i> is <c>null</c>.</returns>
		/// <param name="path">Path or filename (then just removes extension). Can be <c>null</c>.</param>
		/// <remarks>
		/// The same as <see cref="getName"/>, just removes extension.
		/// Similar to <see cref="Path.GetFileNameWithoutExtension"/>. Some differences: if ends with <c>'\\'</c> or <c>'/'</c>, gets part before it, eg <c>"B"</c> from <c>@"C:\A\B\"</c>.
		/// 
		/// Supports separators <c>'\\'</c> and <c>'/'</c>.
		/// Also supports URL and shell parsing names like <c>@"::{CLSID-1}\0\::{CLSID-2}"</c>.
		/// 
		/// Examples:
		/// 
		/// | <i>path</i> | result
		/// | -
		/// | <c>@"C:\A\B\file.txt"</c> | <c>"file"</c>
		/// | <c>"file.txt"</c> | <c>"file"</c>
		/// | <c>"file"</c> | <c>"file"</c>
		/// | <c>@"C:\A\B"</c> | <c>"B"</c>
		/// | <c>@"C:\A\B\"</c> | <c>"B"</c>
		/// | <c>@"C:\A\B.B\"</c> | <c>"B.B"</c>
		/// | <c>@"C:\aa\file.txt:alt.stream"</c> | <c>"file.txt:alt"</c>
		/// | <c>"http://a.b.c"</c> | <c>"a.b"</c>
		/// </remarks>
		public static string getNameNoExt(string path) {
			return _GetPathPart(path, _PathPart.NameWithoutExt);
		}

		/// <summary>
		/// Gets filename extension, like <c>".txt"</c>.
		/// </summary>
		/// <returns>Returns <c>""</c> if there is no extension. Returns <c>null</c> if <i>path</i> is <c>null</c>.</returns>
		/// <param name="path">Path or filename. Can be <c>null</c>.</param>
		/// <remarks>
		/// Supports separators <c>'\\'</c> and <c>'/'</c>.
		/// </remarks>
		public static string getExtension(string path) {
			return _GetPathPart(path, _PathPart.Ext);
		}

		/// <summary>
		/// Gets filename extension and path part without the extension.
		/// More info: <see cref="getExtension(string)"/>.
		/// </summary>
		/// <param name="path">Path or filename. Can be <c>null</c>.</param>
		/// <param name="pathWithoutExtension">Receives path part without the extension. Can be the same variable as <i>path</i>.</param>
		public static string getExtension(string path, out string pathWithoutExtension) {
			var ext = getExtension(path);
			if (ext != null && ext.Length > 0) pathWithoutExtension = path[..^ext.Length];
			else pathWithoutExtension = path;
			return ext;
		}

		/// <summary>
		/// Finds filename extension, like <c>".txt"</c>.
		/// </summary>
		/// <returns>Index of <c>'.'</c> character, or -1 if there is no extension.</returns>
		/// <param name="path">Path or filename. Can be <c>null</c>.</param>
		public static int findExtension(RStr path) {
			int i;
			for (i = path.Length; --i >= 0;) {
				switch (path[i]) {
				case '.': return i;
				case '\\': case '/': /*case ':':*/ return -1;
				}
			}
			return i;
		}

		/// <summary>
		/// Removes filename part from <i>path</i>.
		/// By default also removes separator (<c>'\\'</c> or <c>'/'</c>) if it is not after drive name (eg <c>"C:"</c>).
		/// </summary>
		/// <returns>Returns <c>""</c> if the string is a filename. Returns <c>null</c> if the string is <c>null</c> or a root (like <c>@"C:\"</c> or <c>"C:"</c> or <c>@"\\server\share"</c> or <c>"http:"</c>).</returns>
		/// <param name="path">Path or filename. Can be <c>null</c>.</param>
		/// <param name="withSeparator">Don't remove separator character(s) (<c>'\\'</c> or <c>'/'</c>). See examples.</param>
		/// <remarks>
		/// Similar to <see cref="Path.GetDirectoryName"/>. Some differences: skips <c>'\\'</c> or <c>'/'</c> at the end (eg from <c>@"C:\A\B\"</c> gets <c>@"C:\A"</c>, not <c>@"C:\A\B"</c>); does not replace / with \.
		/// 
		/// Parses raw string. You may want to <see cref="normalize"/> it at first.
		/// 
		/// Supports separators <c>'\\'</c> and <c>'/'</c>.
		/// Also supports URL and shell parsing names like <c>@"::{CLSID-1}\0\::{CLSID-2}"</c>.
		/// 
		/// Examples:
		/// 
		/// | <i>path</i> | result
		/// | -
		/// | <c>@"C:\A\B\file.txt"</c> | <c>@"C:\A\B"</c>
		/// | <c>"file.txt"</c> | <c>""</c>
		/// | <c>@"C:\A\B\"</c> | <c>@"C:\A"</c>
		/// | <c>@"C:\A\/B\/"</c> | <c>@"C:\A"</c>
		/// | <c>@"C:\"</c> | <c>null</c>
		/// | <c>@"\\network\share"</c> | <c>null</c>
		/// | <c>"http:"</c> | <c>null</c>
		/// | <c>@"C:\aa\file.txt:alt.stream"</c> | <c>"C:\aa"</c>
		/// | <c>"http://a.b.c"</c> | <c>"http:"</c>
		/// | <c>"::{A}\::{B}"</c> | <c>"::{A}"</c>
		/// | <c>""</c> | <c>""</c>
		/// | <c>null</c> | <c>null</c>
		/// 
		/// Examples when <i>withSeparator</i> <c>true</c>:
		/// 
		/// | <i>path</i> | result
		/// | -
		/// | <c>@"C:\A\B"</c> | <c>@"C:\A\"</c> (not <c>@"C:\A"</c>)
		/// | <c>"http://x.y"</c> | <c>"http://"</c> (not <c>"http:"</c>)
		/// </remarks>
		public static string getDirectory(string path, bool withSeparator = false) {
			return _GetPathPart(path, _PathPart.Dir, withSeparator);
		}

		enum _PathPart { Dir, NameWithExt, NameWithoutExt, Ext, };

		static string _GetPathPart(string s, _PathPart what, bool withSeparator = false) {
			if (s == null) return null;
			int len = s.Length, i, iExt = -1;

			//rtrim '\\' and '/' etc
			for (i = len; i > 0 && IsSepChar_(s[i - 1]); i--) {
				if (what == _PathPart.Ext) return "";
				if (what == _PathPart.NameWithoutExt) what = _PathPart.NameWithExt;
			}
			len = i;

			//if ends with ":" or @":\", it is either drive or URL root or invalid
			if (len > 0 && s[len - 1] == ':' && !IsShellPath_(s)) return (what == _PathPart.Dir) ? null : "";

			//find '\\' or '/'. Also '.' if need.
			//Note: we don't split at ':', which could be used for alt stream or URL port or in shell parsing name as non-separator. This library does not support paths like "C:relative path".
			while (--i >= 0) {
				char c = s[i];
				if (c == '.') {
					if (what < _PathPart.NameWithoutExt) continue;
					if (iExt < 0) iExt = i;
					if (what == _PathPart.Ext) break;
				} else if (c == '\\' || c == '/') {
					break;
				}
			}
			if (iExt >= 0 && iExt == len - 1) iExt = -1; //eg ends with ".."
			if (what == _PathPart.NameWithoutExt && iExt < 0) what = _PathPart.NameWithExt;

			switch (what) {
			case _PathPart.Ext:
				if (iExt >= 0) return s[iExt..];
				break;
			case _PathPart.NameWithExt:
				len -= ++i; if (len == 0) return "";
				return s.Substring(i, len);
			case _PathPart.NameWithoutExt:
				i++;
				return s[i..iExt];
			case _PathPart.Dir:
				//skip multiple separators
				if (!withSeparator && i > 0) {
					for (; i > 0; i--) { var c = s[i - 1]; if (!(c == '\\' || c == '/')) break; }
					if (i == 0) return null;
				}
				if (i > 0) {
					//returns null if i is in root
					int j = getRootLength(s); if (j > 0 && IsSepChar_(s[j - 1])) j--;
					if (i < j) return null;

					if (withSeparator || _EndsWithDriveWithoutSep(s.AsSpan(0, i))) i++;
					return s[..i];
				}
				break;
			}
			return "";
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
		/// Returns <c>true</c> if <i>s</i> is like <c>"protocol:"</c> and not like <c>"c:"</c> or <c>"protocol:more"</c>.
		/// </summary>
		/// <param name="s">Can be <c>null</c>.</param>
		internal static bool IsProtocol_(RStr s) {
			return s.Length > 2 && s[^1] == ':' && getUrlProtocolLength(s) == s.Length;
		}

		/// <summary>
		/// Creates path with unique filename for a new file or directory. 
		/// If the specified path is of an existing file or directory, returns path where the filename part is modified like <c>"file 2.txt"</c>, <c>"file 3.txt"</c> etc. Else returns unchanged <i>path</i>.
		/// </summary>
		/// <param name="path">Suggested full path.</param>
		/// <param name="isDirectory">The path is for a directory. The number is always appended at the very end, not before <c>.extension</c>.</param>
		public static string makeUnique(string path, bool isDirectory) {
			if (!filesystem.exists(path)) return path;
			string ext = isDirectory ? null : getExtension(path, out path);
			for (int i = 2; ; i++) {
				var s = path + " " + i + ext;
				if (!filesystem.exists(s)) return s;
			}
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="pathname.normalize"/>.
	/// </summary>
	[Flags]
	public enum PNFlags {
		/// <summary>Don't call API <msdn>GetLongPathName</msdn>.</summary>
		DontExpandDosPath = 1,

		/// <summary>Don't call <see cref="pathname.prefixLongPathIfNeed"/>.</summary>
		DontPrefixLongPath = 2,

		/// <summary>Don't remove <c>\</c> character at the end.</summary>
		DontRemoveEndSeparator = 4,

		/// <summary>If path is not a file-system path but looks like URL (eg <c>"http:..."</c> or <c>"file:..."</c>) or starts with <c>"::"</c>, don't throw exception and don't process more (only expand environment variables).</summary>
		CanBeUrlOrShell = 8,
	}
}
