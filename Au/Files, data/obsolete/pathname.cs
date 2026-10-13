//TODO: test all. Test doc generation.

namespace Au;

/// <summary>
/// File path string functions.
/// In new code use <see cref="Path2"/> instead; most methods of this class just call methods of <c>Path2</c>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static unsafe class pathname {
	/// <inheritdoc cref="Path2.Expand"/>
	public static string expand(string path, bool? strict = null)
		=> Path2.Expand(path, strict);
	
	/// <inheritdoc cref="Path2.IsFullPath"/>
	public static bool isFullPath(RStr path, bool orEnvVar = false)
		=> Path2.IsFullPath(path, orEnvVar);
	
	/// <inheritdoc cref="Path2.IsFullPathExpand(ref string, bool?)"/>
	public static bool isFullPathExpand(ref string path, bool? strict = null)
		=> Path2.IsFullPathExpand(ref path, strict);
	
	/// <inheritdoc cref="Path2.IsFullPathExpand(string, out string, bool?)"/>
	public static bool isFullPathExpand(string path, out string path2, bool? strict = null)
		=> Path2.IsFullPathExpand(path, out path2, strict);
	
	/// <summary>
	/// Gets the length of the drive or network folder part in <i>path</i>, like <c>@"C:\"</c>, <c>@"\\server\share\"</c>, <c>@"\\?\C:\"</c>, <c>@"\\?\UNC\server\share\"</c>, etc.
	/// </summary>
	/// <param name="path">Full path or any string. Can be <c>null</c>. Should not be <c>@"%environmentVariable%\..."</c>.</param>
	/// <remarks>
	/// See <see cref="Path.GetPathRoot"/>.
	/// </remarks>
	public static int getRootLength(RStr path) {
		int i = Path.GetPathRoot(path).Length; //Span, no alloc
		if (i > 0 && i < path.Length && !Path2.IsSepChar_(path[i - 1]) && Path2.IsSepChar_(path[i])) i++; //@"\\server\share" -> @"\\server\share\"
		return i;
	}
	
	/// <summary>
	/// Gets the length of the URL protocol name (also known as URI scheme) in string, including <c>':'</c>.
	/// If the string does not start with a protocol name, returns 0.
	/// </summary>
	/// <param name="s">A URL or path or any string. Can be <c>null</c>.</param>
	public static int getUrlProtocolLength(RStr s)
		=> Path2.GetUriSchemeLengthWithColon_(s);
	
	/// <inheritdoc cref="Path2.IsUri"/>
	public static bool isUrl(RStr s)
		=> 0 != Path2.GetUriSchemeLengthWithColon_(s);
	
	/// <inheritdoc cref="Path2.Join"/>
	public static string combine(string s1, string s2, bool s2CanBeFullPath = false, bool prefixLongPath = true)
		=> Path2.Join(s1, s2, s2CanBeFullPath, prefixLongPath);
	
	/// <inheritdoc cref="Path2.Normalize"/>
	public static string normalize(string path, string defaultParentDirectory = null, PNFlags flags = 0)
		=> Path2.Normalize(path, defaultParentDirectory, flags);
	
	/// <inheritdoc cref="Path2.PrefixLongPath"/>
	public static string prefixLongPath(string path)
		=> Path2.PrefixLongPath(path);
	
	/// <inheritdoc cref="Path2.PrefixLongPathIfNeed"/>
	public static string prefixLongPathIfNeed(string path)
		=> Path2.PrefixLongPathIfNeed(path);
	
	/// <inheritdoc cref="Path2.UnprefixLongPath"/>
	public static string unprefixLongPath(string path)
		=> Path2.UnprefixLongPath(path);
	
	/// <inheritdoc cref="Path2.MakeValidName"/>
	public static string correctName(string name, string invalidCharReplacement = "-")
		=> Path2.MakeValidName(name, invalidCharReplacement);
	
	/// <inheritdoc cref="Path2.IsInvalidName"/>
	public static bool isInvalidName(string name)
		=> Path2.IsInvalidName(name);
	
	/// <inheritdoc cref="Path2.IsInvalidNameChar"/>
	public static bool isInvalidNameChar(char c)
		=> Path2.IsInvalidNameChar(c);
	
	/// <inheritdoc cref="Path2.IsInvalidPathChar"/>
	public static bool isInvalidPathChar(char c)
		=> Path2.IsInvalidPathChar(c);
	
	/// <inheritdoc cref="Path2.GetName"/>
	public static string getName(string path)
		=> Path2.GetName(path);
	
	/// <summary>
	/// Gets filename without extension.
	/// </summary>
	public static string getNameNoExt(string path)
		=> Path.EndsInDirectorySeparator(path) ? Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) : Path.GetFileNameWithoutExtension(path);
	
	/// <inheritdoc cref="Path.GetExtension"/>
	public static string getExtension(string path)
		=> Path.GetExtension(path);
	
	/// <summary>
	/// Gets filename extension and path part without the extension.
	/// More info: <see cref="Path.GetExtension"/>.
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
	
	/// <inheritdoc cref="Path2.GetDirectory"/>
	public static string getDirectory(string path, bool withSeparator = false)
		=> Path2.GetDirectory(path, withSeparator);
	
	/// <inheritdoc cref="Path2.MakeUnique"/>
	public static string makeUnique(string path, bool isDirectory)
		=> Path2.MakeUnique(path, isDirectory);
}
