namespace Au.More
{
	/// <summary>
	/// String functions that work with unmanaged char* strings.
	/// See also <see cref="BytePtr_"/>, it works with byte* strings.
	/// </summary>
	internal static unsafe class CharPtr_
	{
		/// <summary>
		/// Gets the number of characters in p until '\0'.
		/// </summary>
		/// <param name="p">'\0'-terminated string. Can be null.</param>
		public static int Length(char* p) {
			if (p == null) return 0;
			for (int i = 0; ; i++) if (p[i] == '\0') return i;
		}

		/// <summary>
		/// Gets the number of characters in p until '\0' or <i>max</i>.
		/// </summary>
		/// <param name="p">'\0'-terminated string. Can be null.</param>
		/// <param name="max">Max length to scan. Returns max if does not find '\0'.</param>
		public static int Length(char* p, int max) {
			if (p == null) return 0;
			for (int i = 0; i < max; i++) if (p[i] == '\0') return i;
			return max;
		}

		//not used. Now could be replaced with ReadOnlySpan<char>.Equals.
		///// <summary>
		///// Case-sensitive compares string with managed string and returns true if they are equal.
		///// </summary>
		///// <param name="p">Unmanaged string.</param>
		///// <param name="len">p length. Returns false if it is != s.Length.</param>
		///// <param name="s">Managed string.</param>
		//public static bool Eq(char* p, int len, string s)
		//{
		//	if(p == null) return s == null; if(s == null) return false;
		//	if(len != s.Length) return false;
		//	int i;
		//	for(i = 0; i < s.Length; i++) if(s[i] != p[i]) break;
		//	return i == s.Length;
		//}
	}

	/// <summary>
	/// String functions that work with unmanaged byte* strings.
	/// See also <see cref="CharPtr_"/>, it works with char* strings.
	/// </summary>
	static unsafe class BytePtr_
	{
		/// <summary>
		/// Gets the number of bytes in p until '\0'.
		/// </summary>
		/// <param name="p">'\0'-terminated string. Can be null.</param>
		public static int Length(byte* p) {
			if (p == null) return 0;
			for (int i = 0; ; i++) if (p[i] == 0) return i;
		}

		/// <summary>
		/// Gets the number of bytes in p until '\0' or <i>max</i>.
		/// </summary>
		/// <param name="p">'\0'-terminated string. Can be null.</param>
		/// <param name="max">Max length to scan. Returns max if does not find '\0'.</param>
		public static int Length(byte* p, int max) {
			if (p == null) return 0;
			for (int i = 0; i < max; i++) if (p[i] == 0) return i;
			return max;
		}

		/// <summary>
		/// Returns true if unmanaged string p starts with string s. Case-sensitive.
		/// </summary>
		/// <param name="p">'\0'-terminated string.</param>
		/// <param name="s">Must contain only ASCII characters.</param>
		public static bool AsciiStarts(byte* p, string s) {
			int i, n = s.Length;
			for (i = 0; i < n; i++) {
				int b = *p++;
				if (b != s[i] || b == 0) return false;
			}
			return true;
		}
		/// <summary>
		/// Returns true if unmanaged string p starts with string s. Case-insensitive.
		/// </summary>
		/// <param name="p">'\0'-terminated string.</param>
		/// <param name="s">Must contain only ASCII characters.</param>
		public static bool AsciiStartsi(byte* p, string s) {
			var t = Tables_.LowerCase;
			int i, n = s.Length;
			for (i = 0; i < n; i++) {
				int b = *p++;
				if (t[b] != t[s[i]] || b == 0) return false;
			}
			return true;
		}

		/// <summary>
		/// Returns true if unmanaged string p and string s are equal. Case-sensitive.
		/// </summary>
		/// <param name="p">'\0'-terminated string.</param>
		/// <param name="s">Managed string. Must contain only ASCII characters.</param>
		public static bool AsciiEq(byte* p, string s) => AsciiStarts(p, s) && p[s.Length] == 0;

		/// <summary>
		/// Returns true if unmanaged string p and string s are equal. Case-insensitive.
		/// </summary>
		/// <param name="p">'\0'-terminated string.</param>
		/// <param name="s">Must contain only ASCII characters.</param>
		public static bool AsciiEqi(byte* p, string s) => AsciiStartsi(p, s) && p[s.Length] == 0;

		/// <summary>
		/// Case-sensitive compares unmanaged string p with byte[] s and returns true if they are equal.
		/// </summary>
		/// <param name="p">'\0'-terminated string.</param>
		/// <param name="s">Managed string. Must contain only ASCII characters.</param>
		public static bool Eq(byte* p, byte[] s) {
			int i;
			for (i = 0; i < s.Length; i++) if (s[i] != p[i]) return false;
			return p[i] == 0;
		}

		/// <summary>
		/// Finds character in string which can be binary.
		/// </summary>
		/// <param name="p"></param>
		/// <param name="len">Length of p to search in.</param>
		/// <param name="ch">ASCII character.</param>
		public static int AsciiFindChar(byte* p, int len, byte ch) {
			for (int i = 0; i < len; i++) if (p[i] == ch) return i;
			return -1;
		}

		/// <summary>
		/// Finds substring in string which can be binary.
		/// Returns -1 if not found.
		/// </summary>
		/// <param name="p"></param>
		/// <param name="len">Length of p to search in.</param>
		/// <param name="s">Substring to find. Must contain only ASCII characters.</param>
		public static int AsciiFindString(byte* p, int len, string s) {
			int len2 = s.Length;
			if (len2 <= len && len2 > 0) {
				var ch = s[0];
				for (int i = 0, n = len - len2; i <= n; i++) if (p[i] == ch) {
						for (int j = 1; j < len2; j++) if (p[i + j] != s[j]) goto g1;
						return i;
						g1:;
					}
			}
			return -1;

			//speed: with long strings slightly slower than strstr.
		}

		///// <summary>
		///// Finds substring in '\0'-terminated string.
		///// Returns -1 if not found.
		///// </summary>
		///// <param name="s">'\0'-terminated string.</param>
		///// <param name="s2">Substring to find. Must contain only ASCII characters.</param>
		//public static int AsciiFindString(byte* s, string s2)
		//{
		//	int len2 = s2.Length;
		//	if(len2 > 0) {
		//		var ch = s2[0];
		//		for(int i = 0, n = len - len2; i <= n; i++) if(s[i] == ch) {
		//				for(int j = 1; j < len2; j++) if(s[i + j] != s2[j]) goto g1;
		//				return i;
		//				g1:;
		//			}
		//	}
		//	return -1;

		//	//speed: with long strings slightly slower than strstr.
		//}

	}
}
