
using System.Security.Cryptography;

namespace Au.More
{
	/// <summary>
	/// Data hash functions.
	/// </summary>
	public static unsafe class Hash
	{
		#region FNV1

		/// <summary>
		/// 32-bit FNV-1 hash.
		/// Useful for fast hash table and checksum use, not cryptography. Similar to CRC32; faster but creates more collisions.
		/// </summary>
		public static int Fnv1(string data) {
			if (data == null) return 0;
			fixed (char* p = data) return Fnv1(p, data.Length);
		}

		/// <summary>
		/// 32-bit FNV-1 hash.
		/// Useful for fast hash table and checksum use, not cryptography. Similar to CRC32; faster but creates more collisions.
		/// </summary>
		public static int Fnv1(char* data, int lengthChars) {
			uint hash = 2166136261;

			for (int i = 0; i < lengthChars; i++)
				hash = (hash * 16777619) ^ data[i];

			return (int)hash;

			//note: using i is slightly faster than pointers. Compiler knows how to optimize.
		}

		/// <summary>
		/// 32-bit FNV-1 hash.
		/// Useful for fast hash table and checksum use, not cryptography. Similar to CRC32; faster but creates more collisions.
		/// </summary>
		public static int Fnv1(ReadOnlySpan<byte> data) {
			if (data == null) return 0;
			fixed (byte* p = data) return Fnv1(p, data.Length);
		}

		/// <summary>
		/// 32-bit FNV-1 hash.
		/// Useful for fast hash table and checksum use, not cryptography. Similar to CRC32; faster but creates more collisions.
		/// </summary>
		public static int Fnv1(byte* data, int lengthBytes) {
			uint hash = 2166136261;

			for (int i = 0; i < lengthBytes; i++)
				hash = (hash * 16777619) ^ data[i];

			return (int)hash;

			//note: could be void* data, then callers don't have to cast other types to byte*, but then can accidentally pick wrong overload when char*. Also now it's completely clear that it hashes bytes, not the passed type directly (like the char* overload does).
		}

		/// <summary>
		/// 32-bit FNV-1 hash.
		/// Useful for fast hash table and checksum use, not cryptography. Similar to CRC32; faster but creates more collisions.
		/// </summary>
		public static int Fnv1<T>(T data) where T : unmanaged
				=> Fnv1((byte*)&data, sizeof(T));

		/// <summary>
		/// 64-bit FNV-1 hash.
		/// </summary>
		public static long Fnv1Long(string data) {
			if (data == null) return 0;
			fixed (char* p = data) return Fnv1Long(p, data.Length);
		}

		/// <summary>
		/// 64-bit FNV-1 hash.
		/// </summary>
		public static long Fnv1Long(char* data, int lengthChars) {
			ulong hash = 14695981039346656037;

			for (int i = 0; i < lengthChars; i++)
				hash = (hash * 1099511628211) ^ data[i];

			return (long)hash;

			//speed: ~4 times slower than 32-bit
		}

		/// <summary>
		/// 64-bit FNV-1 hash.
		/// </summary>
		public static long Fnv1Long(ReadOnlySpan<byte> data) {
			if (data == null) return 0;
			fixed (byte* p = data) return Fnv1Long(p, data.Length);
		}

		/// <summary>
		/// 64-bit FNV-1 hash.
		/// </summary>
		public static long Fnv1Long(byte* data, int lengthBytes) {
			ulong hash = 14695981039346656037;

			for (int i = 0; i < lengthBytes; i++)
				hash = (hash * 1099511628211) ^ data[i];

			return (long)hash;
		}

		/// <summary>
		/// 64-bit FNV-1 hash.
		/// </summary>
		public static long Fnv1Long<T>(T data) where T : unmanaged
				=> Fnv1Long((byte*)&data, sizeof(T));

		/// <summary>
		/// FNV-1 hash, modified to make faster with long strings (then takes every n-th character).
		/// </summary>
		public static int Fast(char* data, int lengthChars) {
			//Also we take the last 1-2 characters (in the second loop), because often there are several strings like Chrome_WidgetWin_0, Chrome_WidgetWin_1...
			//Also we hash uints, not chars, unless the string is very short.
			//Tested speed with 400 unique strings (window/control names/classnames/programnames). The time was 7 mcs. For single call 17 ns.

			uint hash = 2166136261;
			int i = 0;

			if (lengthChars > 8) {
				int lc = lengthChars--;
				lengthChars /= 2; //we'll has uints, not chars
				int every = lengthChars / 8 + 1;

				for (; i < lengthChars; i += every)
					hash = (hash * 16777619) ^ ((uint*)data)[i];

				i = lengthChars * 2;
				lengthChars = lc;
			}

			for (; i < lengthChars; i++)
				hash = (hash * 16777619) ^ data[i];

			return (int)hash;
		}

		/// <summary>
		/// FNV-1 hash, modified to make faster with long strings (then takes every n-th character).
		/// </summary>
		/// <param name="s">The string to hash. Can be null.</param>
		public static int Fast(string s) {
			if (s == null) return 0;
			fixed (char* p = s) return Fast(p, s.Length);
		}

		/// <summary>
		/// FNV-1 hash, modified to make faster with long strings (then takes every n-th character).
		/// This overload hashes a substring.
		/// </summary>
		/// <param name="s">The string containing the substring. Can be null/"" if other parameters are 0.</param>
		/// <param name="startIndex">Start of substring in s.</param>
		/// <param name="count">Length of substring.</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public static int Fast(string s, int startIndex, int count) {
			int len = s?.Length ?? 0;
			if ((uint)startIndex > len || (uint)count > len - startIndex) throw new ArgumentOutOfRangeException();
			if (s == null) return 0;
			fixed (char* p = s) return Fast(p + startIndex, count);
		}

		#endregion

		#region MD5

		/// <summary>
		/// Computes MD5 hash of data.
		/// Multiple datas can be hashed, producing single result.
		/// Call <b>Add</b> one or more times. Finally use <see cref="Hash"/> to get result.
		/// </summary>
		/// <remarks>
		/// Faster than the .NET MD5 hash functions.
		/// </remarks>
		[StructLayout(LayoutKind.Explicit)]
		public struct MD5Context //MD5_CTX + _state
		{
			[FieldOffset(88)] MD5Result _result;
			[FieldOffset(104)] long _state; //1 inited/added, 2 finalled

			/// <summary>
			/// true if no data was added.
			/// </summary>
			public bool IsEmpty => _state == 0;

			/// <summary>Adds data.</summary>
			/// <exception cref="ArgumentNullException">data is null.</exception>
			/// <exception cref="ArgumentOutOfRangeException">size &lt; 0.</exception>
			public void Add(void* data, int size) {
				if (data == null) throw new ArgumentNullException();
				if (size < 0) throw new ArgumentOutOfRangeException();
				if (_state != 1) { Api.MD5Init(out this); _state = 1; }
				Api.MD5Update(ref this, data, size);
			}

			/// <summary>Adds data.</summary>
			public void Add<T>(T data) where T : unmanaged
				=> Add(&data, sizeof(T));

			/// <summary>Adds data.</summary>
			/// <exception cref="ArgumentNullException">data is null.</exception>
			public void Add(ReadOnlySpan<byte> data) {
				fixed (byte* p = data) Add(p, data.Length);
			}

			/// <summary>Adds string converted to UTF8.</summary>
			/// <exception cref="ArgumentNullException">data is null.</exception>
			public void Add(string data) => Add(Encoding.UTF8.GetBytes(data));

			//rejected. Better use unsafe address, then will not need to copy data.
			///// <summary>Adds data.</summary>
			//public void Add<T>(T data) where T: unmanaged
			//{
			//	Add(&data, sizeof(T));
			//}

			/// <summary>
			/// Computes final hash of datas added with <b>Add</b>.
			/// </summary>
			/// <exception cref="InvalidOperationException"><b>Add</b> was not called.</exception>
			/// <remarks>
			/// Resets state, so that if <b>Add</b> called again, it will start adding new datas.
			/// </remarks>
			public MD5Result Hash {
				get {
					if (_state != 2) {
						if (_state != 1) throw new InvalidOperationException();
						Api.MD5Final(ref this);
						_state = 2;
					}
					return _result;
				}
			}
		}

		/// <summary>
		/// Result of <see cref="MD5Context.Hash"/>.
		/// It is 16 bytes stored in 2 long fields r1 and r2.
		/// If need, can be converted to byte[] with <see cref="MD5Result.ToArray"/> or to hex string with <see cref="MD5Result.ToString"/>.
		/// </summary>
		public record struct MD5Result
		{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
			public readonly long r1, r2;

			//rejected. Not much shorter than hex.
			//public string ToBase64() => Convert.ToBase64String(ToArray());
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

			/// <summary>
			/// Converts this to hex string.
			/// </summary>
			public override string ToString() => Convert2.HexEncode(this);

			/// <summary>
			/// Converts this to byte[16].
			/// </summary>
			public byte[] ToArray() {
				var r = new byte[16];
				fixed (byte* p = r) {
					*(long*)p = r1;
					*(long*)(p + 8) = r2;
				}
				return r;
			}

			/// <summary>
			/// Creates <b>MD5Result</b> from hex string returned by <see cref="ToString"/>.
			/// Returns false if <i>encoded</i> is invalid.
			/// </summary>
			public static bool FromString(RStr encoded, out MD5Result r) => Convert2.HexDecode(encoded, out r);
		}

		/// <summary>
		/// Computes MD5 hash of data.
		/// Uses <see cref="MD5Context"/>.
		/// </summary>
		public static MD5Result MD5(ReadOnlySpan<byte> data) {
			MD5Context md = default;
			md.Add(data);
			return md.Hash;
		}

		/// <summary>
		/// Computes MD5 hash of string converted to UTF8.
		/// Uses <see cref="MD5Context"/>.
		/// </summary>
		public static MD5Result MD5(string data) {
			MD5Context md = default;
			md.Add(data);
			return md.Hash;
		}

		/// <summary>
		/// Computes MD5 hash of data. Returns result as hex or base64 string.
		/// Uses <see cref="MD5Context"/>.
		/// </summary>
		public static string MD5(ReadOnlySpan<byte> data, bool base64) {
			var h = MD5(data);
			return base64 ? Convert.ToBase64String(new ReadOnlySpan<byte>((byte*)&h, 16)) : h.ToString();
		}

		/// <summary>
		/// Computes MD5 hash of string converted to UTF8. Returns result as hex or base64 string.
		/// Uses <see cref="MD5Context"/>.
		/// </summary>
		public static string MD5(string data, bool base64) {
			var h = MD5(data);
			return base64 ? Convert.ToBase64String(new ReadOnlySpan<byte>((byte*)&h, 16)) : h.ToString();
		}

		#endregion

		#region other

		/// <summary>
		/// Computes data hash using the specified cryptographic algorithm.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="algorithm">Algorithm name, eg "SHA256". See <see cref="CryptoConfig"/>.</param>
		public static byte[] Crypto(ReadOnlySpan<byte> data, string algorithm) {
			using var x = (HashAlgorithm)CryptoConfig.CreateFromName(algorithm);
			var r = new byte[x.HashSize / 8];
			x.TryComputeHash(data, r, out _);
			return r;
		}

		/// <summary>
		/// Computes hash of string converted to UTF8, using the specified cryptographic algorithm.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="algorithm">Algorithm name, eg "SHA256". See <see cref="CryptoConfig"/>.</param>
		public static byte[] Crypto(string data, string algorithm)
			=> Crypto(Encoding.UTF8.GetBytes(data), algorithm);

		/// <summary>
		/// Computes data hash using the specified cryptographic algorithm. Returns result as hex or base64 string.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="algorithm">Algorithm name, eg "SHA256". See <see cref="CryptoConfig"/>.</param>
		/// <param name="base64"></param>
		public static string Crypto(ReadOnlySpan<byte> data, string algorithm, bool base64) {
			var b = Crypto(data, algorithm);
			return base64 ? Convert.ToBase64String(b) : Convert2.HexEncode(b);
		}

		/// <summary>
		/// Computes hash of string converted to UTF8, using the specified cryptographic algorithm. Returns result as hex or base64 string.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="algorithm">Algorithm name, eg "SHA256". See <see cref="CryptoConfig"/>.</param>
		/// <param name="base64"></param>
		public static string Crypto(string data, string algorithm, bool base64) {
			var b = Crypto(data, algorithm);
			return base64 ? Convert.ToBase64String(b) : Convert2.HexEncode(b);
		}

		#endregion

	}
}
