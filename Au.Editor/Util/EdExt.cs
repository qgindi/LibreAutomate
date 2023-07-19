/// <summary>
/// Misc extension methods.
/// </summary>
static class EdExt {
	/// <summary>
	/// Returns true if this starts with <i>s</i> and then follows '\\'.
	/// Case-insensitive.
	/// </summary>
	/// <param name="orEquals">Also return true if this equals <i>s</i>.</param>
	public static bool PathStarts(this string t, ReadOnlySpan<char> s, bool orEquals = false) {
		if (!t.Starts(s, true)) return false;
		if (t.Length > s.Length) return t[s.Length] =='\\';
		return orEquals;
	}
	
	
}
