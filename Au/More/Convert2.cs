
using System.IO.Compression;

namespace Au.More {
	/// <summary>
	/// Data conversion functions. Compression, hex encoding, UTF8.
	/// </summary>
	public static unsafe class Convert2 {
		#region hex encode

		/// <summary>
		/// Converts binary data in any memory to hex encoded string.
		/// </summary>
		/// <param name="data">The data. Can be any valid memory of specified size, for example a struct address.</param>
		/// <param name="size">data memory size (bytes).</param>
		/// <param name="upperCase">Let the hex string contain A-F, not a-f.</param>
		/// <remarks>
		/// The result string length is 2 * data length.
		/// Often it's better to use <see cref="Convert.ToBase64String"/>, then result is 4/3 of data length. But cannot use Base64 in file names and URLs because it is case-sensitive and may contain character '/'. Both functions are fast.
		/// </remarks>
		public static string HexEncode(void* data, int size, bool upperCase = false) {
			int u = (upperCase ? 'A' : 'a') - 10;
			var bytes = (byte*)data;
			string R = new('\0', size * 2);
			fixed (char* cp = R) {
				int* p = (int*)cp;
				for (int i = 0; i < size; i++) {
					int b = bytes[i];
					int t = b & 0xf; t += t < 10 ? '0' : u;
					b >>= 4; b += b < 10 ? '0' : u;
					p[i] = t << 16 | b;
				}
			}
			return R;

			//tested: ~50% of time takes string allocation.
			//tested: with string.Create same speed.
		}

		/// <summary>
		/// Converts data in byte[] or other memory to hex encoded string.
		/// </summary>
		/// <param name="data">Data. See also: <see cref="MemoryMarshal.AsBytes"/>, <see cref="CollectionsMarshal.AsSpan"/>.</param>
		/// <param name="upperCase">Let the hex string contain A-F, not a-f.</param>
		/// <remarks>
		/// The result string length is 2 * data length.
		/// Often it's better to use <see cref="Convert.ToBase64String"/>, then result is 4/3 of data length. But cannot use Base64 in file names and URLs because it is case-sensitive and may contain character '/'. Both functions are fast.
		/// </remarks>
		public static string HexEncode(ReadOnlySpan<byte> data, bool upperCase = false) {
			fixed (byte* p = data) {
				return HexEncode(p, data.Length, upperCase);
			}
		}

		/// <summary>
		/// Converts a struct variable to hex encoded string.
		/// </summary>
		/// <param name="x">Variable.</param>
		/// <param name="upperCase">Let the hex string contain A-F, not a-f.</param>
		/// <remarks>
		/// The result string length is 2 * data length.
		/// Often it's better to use <see cref="Convert.ToBase64String"/>, then result is 4/3 of data length. But cannot use Base64 in file names and URLs because it is case-sensitive and may contain character '/'. Both functions are fast.
		/// </remarks>
		public static string HexEncode<T>(T x, bool upperCase = false) where T : unmanaged
			=> HexEncode(&x, sizeof(T), upperCase);

		/// <summary>
		/// Converts hex encoded string to binary data.
		/// </summary>
		/// <param name="encoded">String or char[] or span of string/array/memory containing hex encoded data.</param>
		/// <remarks>
		/// Skips spaces and other non-hex-digit characters. Example: "01 23 46 67" is the same as "01234667".
		/// The number of hex digit characters should be divisible by 2, else the last character is ignored.
		/// </remarks>
		[SkipLocalsInit]
		public static byte[] HexDecode(RStr encoded) {
			using FastBuffer<byte> b = new(encoded.Length / 2);
			int n = HexDecode(encoded, b.p, b.n);
			var r = new byte[n];
			Marshal.Copy((IntPtr)b.p, r, 0, n);
			return r;
		}

		/// <summary>
		/// Converts hex encoded string to binary data. Writes to caller's memory buffer.
		/// Returns the number of bytes written in <i>decoded</i> memory. It is equal or less than <c>Math.Min(bufferSize, encoded.Length/2)</c>.
		/// </summary>
		/// <param name="encoded">String or char[] or span of string/array/memory containing hex encoded data.</param>
		/// <param name="decoded">Memory buffer for result.</param>
		/// <param name="bufferSize">Max number of bytes that can be written to the <i>decoded</i> memory buffer.</param>
		/// <remarks>
		/// Skips spaces and other non-hex-digit characters. Example: "01 23 46 67" is the same as "01234667".
		/// The number of hex digit characters should be divisible by 2, else the last character is ignored.
		/// </remarks>
		public static int HexDecode(RStr encoded, void* decoded, int bufferSize) {
			if (encoded.Length == 0) return 0;
			var t = Tables_.Hex;
			byte* r = (byte*)decoded, rTo = r + bufferSize;
			uint k = 1;
			for (int i = 0; i < encoded.Length; i++) {
				uint c = (uint)(encoded[i] - '0'); if (c >= 55) continue;
				c = t[c]; if (c == 0xFF) continue;
				k <<= 4;
				k |= c;
				if (0 != (k & 0x100)) {
					if (r >= rTo) break;
					*r++ = (byte)k;
					k = 1;
				}
			}

			return (int)(r - (byte*)decoded);

			//speed: slightly slower than Base64Decode for the same binary data size.
		}

		/// <summary>
		/// Converts hex encoded string to a struct variable.
		/// Returns false if decoded size != <c>sizeof(T)</c>.
		/// </summary>
		/// <param name="encoded">String or char[] or span of string/array/memory containing hex encoded data.</param>
		/// <param name="decoded">The result variable.</param>
		public static bool HexDecode<T>(RStr encoded, out T decoded) where T : unmanaged {
			T t;
			if (HexDecode(encoded, &t, sizeof(T)) != sizeof(T)) { decoded = default; return false; }
			decoded = t;
			return true;
		}

		#endregion

		#region base64

		///// <summary>
		///// Gets Base64 encoded string length for non-encoded length.
		///// It is <c>(length + 2) / 3 * 4</c>.
		///// </summary>
		//public static int Base64EncodeLength(int length) => checked((length + 2) / 3 * 4);

		///// <summary>
		///// Gets decoded data length from Base64 encoded string length, assuming there are no newlines and other whitespace characters.
		///// It is <c>(int)(len * 3L / 4)</c> minus the number of padding '=' characters (max 2).
		///// </summary>
		//public static int Base64DecodeLength(RStr encoded) {
		//	int len = encoded.Length;
		//	if (len == 0) return 0;
		//	int r = (int)(len * 3L / 4);
		//	if (0 == (len & 3)) {
		//		if (encoded[len - 1] == '=') {
		//			r--;
		//			if (encoded[len - 2] == '=') r--;
		//		}
		//	}
		//	return r;
		//}

		//currently not used in this lib. Not tested speed.
		///// <summary>
		///// Converts string span containing Base64 encoded data to byte[].
		///// </summary>
		///// <param name="encoded">String or char[] or span of string/array/memory containing Base64 encoded data.</param>
		///// <param name="result"></param>
		///// <returns>false if fails.</returns>
		///// <remarks>
		///// Uses <see cref="Convert.TryFromBase64Chars"/>.
		///// </remarks>
		//public static bool TryBase64Decode(RStr encoded, out byte[] result) {
		//	result = null;
		//	encoded = encoded.Trim();
		//	//todo: calc length: skip whitespace. Maybe bool parameter. Or use FastBuffer and copy, like in HexDecode.
		//	var a = new byte[Base64DecodeLength(encoded)];
		//	if (!Convert.TryFromBase64Chars(encoded, a, out int len)) return false;
		//	if (len != a.Length) {
		//		Debug_.Print($"{a.Length} {len}");
		//		a = a.AsSpan(0, len).ToArray();
		//	}
		//	result = a;
		//	return true;
		//}

		//rejected: URL-safe Base64.
		//	Not used in this lib.
		//	Cannot be used in file names because case-sensitive.
		//	If somebody wants URL-safe Base64, it's easy/fast to replace unsafe characters. Nobody would find and use these functions.

		///// <summary>
		///// Converts byte[] or other memory to Base64 encoded string that can be used in URL.
		///// </summary>
		///// <param name="data">Data to encode.</param>
		///// <remarks>Like <see cref="Convert.ToBase64String(byte[])"/>, but instead of '/' and '+' uses '_' and '-'.</remarks>
		//public static string Base64UrlEncode(ReadOnlySpan<byte> data) {
		//	fixed (byte* p = data) return Base64UrlEncode(p, data.Length);

		//	//speed: same as Convert.ToBase64String
		//}

		///// <summary>
		///// Converts binary data stored in any memory to Base64 encoded string that can be used in URL.
		///// </summary>
		///// <param name="data">Data to encode.</param>
		///// <param name="length">Number of bytes to encode.</param>
		///// <remarks>Instead of '/' and '+' uses '_' and '-'.</remarks>
		//public static string Base64UrlEncode(void* data, int length) {
		//	var ip = (IntPtr)data;
		//	return string.Create(Base64EncodeLength(length), (ip, length), static (span, tu) => {
		//		Convert.TryToBase64Chars(new ReadOnlySpan<byte>((byte*)tu.ip, tu.length), span, out _);
		//		for (int i = 0; i < span.Length; i++) {
		//			switch (span[i]) { case '/': span[i] = '_'; break; case '+': span[i] = '-'; break; }
		//		}
		//	});
		//}

		///// <summary>
		///// Converts a struct variable to Base64 encoded string that can be used in URL.
		///// </summary>
		///// <param name="x">Variable.</param>
		///// <remarks>Instead of '/' and '+' uses '_' and '-'.</remarks>
		//public static string Base64UrlEncode<T>(T x) where T : unmanaged {
		//	return Base64UrlEncode(&x, sizeof(T));
		//}

		//static void _Base64_Replace(RStr encoded, char[] a) {
		//	for (int i = 0; i < encoded.Length; i++) {
		//		char c = encoded[i];
		//		a[i] = c switch { '_' => '/', '-' => '+', _ => c, };
		//	}
		//}

		///// <summary>
		///// Converts string containing Base64 encoded data to byte[]. Supports standard encoding and URL-safe encoding.
		///// </summary>
		///// <param name="encoded">String or char[] or span of string/array/memory containing Base64 encoded data.</param>
		///// <remarks>Like <see cref="Convert.FromBase64String(string)"/>, but the string can contain '_' and '-' instead of '/' and '+'.</remarks>
		///// <exception cref="Exception">Exceptions of <see cref="Convert.FromBase64CharArray"/>.</exception>
		//public static byte[] Base64UrlDecode(RStr encoded) {
		//	char[] a = ArrayPool<char>.Shared.Rent(encoded.Length);
		//	try {
		//		_Base64_Replace(encoded, a);
		//		return Convert.FromBase64CharArray(a, 0, encoded.Length);
		//	}
		//	finally { ArrayPool<char>.Shared.Return(a); }
		//	//never mind: almost 2 times slower than Convert.FromBase64CharArray.
		//	//	Normally this func is used with short strings and not with many strings in loop.
		//	//	ArrayPool isn't as fast as should be. And copying to new array takes time.
		//}

		///// <summary>
		///// Converts string containing Base64 encoded data to bytes and stores in memory of a Span variable. Supports standard encoding and URL-safe encoding.
		///// Returns false if the encoded string is invalid or the buffer is too small.
		///// </summary>
		///// <param name="encoded">String or char[] or span of string/array/memory containing Base64 encoded data.</param>
		///// <param name="decoded">Memory buffer for the result.</param>
		///// <param name="decodedLength"></param>
		///// <remarks>The string can contain '_' and '-' instead of '/' and '+'.</remarks>
		//public static bool Base64UrlDecode(RStr encoded, Span<byte> decoded, out int decodedLength) {
		//	char[] a = ArrayPool<char>.Shared.Rent(encoded.Length);
		//	try {
		//		_Base64_Replace(encoded, a);
		//		return Convert.TryFromBase64Chars(new RStr(a, 0, encoded.Length), decoded, out decodedLength);
		//	}
		//	finally { ArrayPool<char>.Shared.Return(a); }
		//}

		///// <summary>
		///// Converts string containing Base64 encoded data to bytes and stores in any memory. Supports standard encoding and URL-safe encoding.
		///// Returns false if the encoded string is invalid or the buffer is too small.
		///// </summary>
		///// <param name="encoded">String or char[] or span of string/array/memory containing Base64 encoded data.</param>
		///// <param name="decoded">Memory buffer for the result.</param>
		///// <param name="bufferSize">The max number of bytes that can be written to the <i>decoded</i> memory buffer.</param>
		///// <param name="decodedLength">Receives the number of bytes written to the <i>decoded</i> memory buffer.</param>
		///// <remarks>The string can contain '_' and '-' instead of '/' and '+'.</remarks>
		//public static bool Base64UrlDecode(RStr encoded, void* decoded, int bufferSize, out int decodedLength) {
		//	return Base64UrlDecode(encoded, new Span<byte>(decoded, bufferSize), out decodedLength);
		//}

		///// <summary>
		///// Converts string containing Base64 encoded data to a struct variable. Supports standard encoding and URL-safe encoding.
		///// Returns false if the encoded string is invalid or decoded size != <c>sizeof(T)</c>.
		///// </summary>
		///// <param name="encoded">String or char[] or span of string/array/memory containing Base64 encoded data.</param>
		///// <param name="decoded">The result variable.</param>
		///// <remarks>The string can contain '_' and '-' instead of '/' and '+'.</remarks>
		//public static bool Base64UrlDecode<T>(RStr encoded, out T decoded) where T : unmanaged {
		//	T t;
		//	if (!Base64UrlDecode(encoded, &t, sizeof(T), out int n) || n != sizeof(T)) { decoded = default; return false; }
		//	decoded = t;
		//	return true;
		//}

		#endregion

		#region compress

		/// <summary>
		/// Compresses data. Uses <see cref="DeflateStream"/>.
		/// </summary>
		/// <param name="data">Data. See also: <see cref="MemoryMarshal.AsBytes"/>, <see cref="CollectionsMarshal.AsSpan"/>.</param>
		/// <exception cref="Exception">Exceptions of <b>DeflateStream</b>.</exception>
		public static byte[] DeflateCompress(ReadOnlySpan<byte> data) {
			using var ms = new MemoryStream();
			using (var ds = new DeflateStream(ms, CompressionLevel.Optimal)) ds.Write(data); //note: must dispose before ToArray
			return ms.ToArray();
			//tested: GZipStream same compression but adds 18 bytes header. DeflateStream does not add any header.
			//tested: bz2 and 7z compression isn't much better with single 15 kb bmp file.
		}

		/// <summary>
		/// Decompresses data. Uses <see cref="DeflateStream"/>.
		/// </summary>
		/// <returns>Decompressed data.</returns>
		/// <param name="compressed">Compressed data.</param>
		/// <exception cref="Exception">Exceptions of <b>DeflateStream</b>.</exception>
		public static byte[] DeflateDecompress(ReadOnlySpan<byte> compressed) {
			using var stream = new MemoryStream();
			DeflateDecompress(compressed, stream);
			return stream.ToArray();
		}

		/// <summary>
		/// Decompresses data to a caller-provided memory stream. Uses <see cref="DeflateStream"/>.
		/// </summary>
		/// <param name="compressed">Compressed data.</param>
		/// <param name="decompressed">Stream for decompressed data.</param>
		/// <exception cref="Exception">Exceptions of <b>DeflateStream</b>.</exception>
		public static void DeflateDecompress(ReadOnlySpan<byte> compressed, Stream decompressed) {
			fixed (byte* p = compressed) {
				using var compressStream = new UnmanagedMemoryStream(p, compressed.Length);
				using var deflateStream = new DeflateStream(compressStream, CompressionMode.Decompress);
				deflateStream.CopyTo(decompressed);
			}

			//note: cannot deflateStream.Read directly to array because its Length etc are not supported.
			//note: also cannot use decompressedStream.GetBuffer because it can be bigger.
		}

		/// <summary>
		/// Compresses data. Uses <see cref="BrotliEncoder"/>.
		/// </summary>
		/// <param name="data">Data. See also: <see cref="MemoryMarshal.AsBytes"/>, <see cref="CollectionsMarshal.AsSpan"/>.</param>
		/// <param name="level">Compression level, 0 (no compression) to 11 (maximal compression). Default 6. Bigger levels don't make much smaller but can make much slower.</param>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <i>level</i>.</exception>
		/// <exception cref="OutOfMemoryException"></exception>
		public static unsafe byte[] BrotliCompress(ReadOnlySpan<byte> data, int level = 6) {
			int n = BrotliEncoder.GetMaxCompressedLength(data.Length);
			var b = MemoryUtil.Alloc(n);
			try {
				if (!BrotliEncoder.TryCompress(data, new(b, n), out n, level, 22)) throw new AuException();
				return new Span<byte>(b, n).ToArray();
			}
			finally { MemoryUtil.Free(b); }
		}

		/// <summary>
		/// Decompresses data. Uses <see cref="BrotliDecoder"/>.
		/// </summary>
		/// <returns>Decompressed data.</returns>
		/// <param name="compressed">Compressed data.</param>
		/// <exception cref="ArgumentException">Invalid data.</exception>
		/// <exception cref="OutOfMemoryException"></exception>
		public static unsafe byte[] BrotliDecompress(ReadOnlySpan<byte> compressed) {
			int n = checked(compressed.Length * 4 + 8000);
			for (int i = 0; i < 3; i++) if (n < 512_000) n *= 2;
			//print.it(compressed.Length, n, n/compressed.Length); //usually ~ 80 KB
			for (; ; n = checked(n * 2)) {
				byte* b = null;
				try {
					b = MemoryUtil.Alloc(n);
					if (BrotliDecoder.TryDecompress(compressed, new(b, n), out int nw)) return new Span<byte>(b, nw).ToArray();
					if (nw == 0) throw new ArgumentException("cannot decompress this data");
					//print.it(n);
				}
				finally { MemoryUtil.Free(b); }
			}
		}

		#endregion

		#region utf8

		/// <summary>
		/// Converts string to UTF-8 byte[]. Can append "\0" (default) or some other string.
		/// </summary>
		/// <param name="chars">String or char[] or span of string/array/memory.</param>
		/// <param name="append">A string to append, or null. For example "\0" (default) or "\r\n". Must contain only ASCII characters.</param>
		/// <exception cref="ArgumentException"><i>append</i> contains non-ASCII characters.</exception>
		public static byte[] Utf8Encode(RStr chars, string append = "\0") {
			int n = Encoding.UTF8.GetByteCount(chars);
			int nAppend = append.Lenn();
			var r = new byte[n + nAppend];
			int nn = Encoding.UTF8.GetBytes(chars, r);
			Debug.Assert(nn == n);
			if (nAppend > 0 && !(nAppend == 1 && append[0] == '\0')) {
				foreach (char c in append) {
					if (c > 127) throw new ArgumentException("append must be ASCII");
					r[nn++] = (byte)c;
				}
			}
			return r;

			//speed: faster than WideCharToMultiByte. Same as System.Text.Unicode.Utf8.FromUtf16.
		}

		/// <summary>
		/// Converts '\0'-terminated UTF8 string to C# string (UTF16).
		/// </summary>
		/// <param name="utf8">UTF8 string. If null, returns null.</param>
		/// <remarks>
		/// Finds '\0' and calls <c>Encoding.UTF8.GetString</c>. Don't use this function when UTF8 string length is known; call <c>Encoding.UTF8.GetString</c> directly.
		/// </remarks>
		public static string Utf8Decode(byte* utf8) => utf8 == null ? null : Encoding.UTF8.GetString(utf8, BytePtr_.Length(utf8));

		#endregion
	}
}
