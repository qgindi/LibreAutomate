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
	
	/// <summary>
	/// Appends hex like "1A".
	/// Like <c>t.Append(x.ToString("X"))</c> or <c>t.AppendFormat(...)</c>, but faster and no garbage.
	/// </summary>
	public static StringBuilder AppendHex(this StringBuilder t, uint x) {
		if (x == 0) {
			t.Append('0');
		} else {
			Span<char> p = stackalloc char[8];
			int i = 8;
			for (; x != 0; x >>= 4) {
				uint h = x & 15;
				p[--i] = (char)(h + (h < 10 ? 48 : 55));
			}
			t.Append(p[i..]);
		}
		return t;
	}
	
	/// <inheritdoc cref="AppendHex(StringBuilder, uint)"/>
	public static StringBuilder AppendHex(this StringBuilder t, int x) => AppendHex(t, (uint)x);
}
