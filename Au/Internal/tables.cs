namespace Au.More
{
	/// <summary>
	/// Lookup tables for various functions of this library.
	/// </summary>
	unsafe static class Tables_
	{
		static Tables_() {
			var t = new byte[55];
			for (int u = 0; u < t.Length; u++) {
				char c = (char)(u + '0');
				if (c >= '0' && c <= '9') t[u] = (byte)u;
				else if (c >= 'A' && c <= 'F') t[u] = (byte)(c - ('A' - 10));
				else if (c >= 'a' && c <= 'f') t[u] = (byte)(c - ('a' - 10));
				else t[u] = 0xFF;
			}
			Hex = t;
		}

		/// <summary>
		/// Table for <see cref="Convert2.HexDecode"/> and co.
		/// </summary>
		public static readonly byte[] Hex;

		/// <summary>
		/// Native-memory <c>char[0x10000]</c> containing lower-case versions of the first 0x10000 characters.
		/// </summary>
		public static char* LowerCase {
			get { var v = _lcTable; if (v == null) _lcTable = v = Cpp.Cpp_LowercaseTable(); return v; } //why operator ??= cannot be used with pointers?
		}
		static char* _lcTable;
		//never mind: this library does not support ucase/lcase chars 0x10000-0x100000 (surrogate pairs).
		//	Tested with IsUpper/IsLower: about 600 such chars exist. ToUpper/ToLower can convert 40 of them. Equals/StartsWith/IndexOf/etc fail.
	}
}
