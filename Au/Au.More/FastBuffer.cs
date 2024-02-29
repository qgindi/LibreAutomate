namespace Au.More {
	/// <summary>
	/// Memory buffer on stack with ability to expand and use heap memory.
	/// Can be used for calling Windows API or building arrays.
	/// Must be used with <c>[SkipLocalsInit]</c> attribute; add it to the caller function or class.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// print.it(api.GetEnvironmentVariable("temp"));
	/// 
	/// unsafe class api : NativeApi {
	/// 	[DllImport("kernel32.dll", EntryPoint = "GetEnvironmentVariableW", SetLastError = true)]
	/// 	static extern int _GetEnvironmentVariable(string lpName, char* lpBuffer, int nSize);
	/// 
	/// 	[SkipLocalsInit]
	/// 	internal static string GetEnvironmentVariable(string name) {
	/// 		using FastBuffer<char> b = new();
	/// 		for (; ; ) if (b.GetString(_GetEnvironmentVariable(name, b.p, b.n), out var s)) return s;
	/// 	}
	/// }
	/// ]]></code>
	/// </example>
	[StructLayout(LayoutKind.Sequential, Size = 16 + StackSize + 16)] //16 for other fields + 16 for safety
	public unsafe ref struct FastBuffer<T> where T : unmanaged {
		T* _p; //buffer pointer (on stack or native heap)
		int _n; //buffer length (count of T elements)
		bool _free; //if false, buffer is on stack in this variable (_p=&_stack). If true, buffer is allocated with MemoryUtil.Alloc.
		long _stack; //start of buffer of StackSize size

		/// <summary>
		/// A <b>FastBuffer</b> variable contains a field of this size. It is a memory buffer on stack.
		/// It is byte count and does not depend on T. To get count of T elements on stack: <c>StackSize/sizeof(T)</c>.
		/// </summary>
		public const int StackSize = 2048;
		//const int StackSize = 16; //test More() and GetString()

		//Also tested:
		//	1. Struct of normal size, when caller passes stackalloc'ed Span<T>. Slow in Debug. And more caling code.
		//	2. See the commented out Buffer_.
		//	3. Callback. Good: easy to use, less calling code, don't need [SkipLocalsInit]. Bad: problem with captured variables (garbage, slow); slower in any case.
		//	4. foreach. Nothing good.

		/// <summary>
		/// Memory buffer pointer.
		/// </summary>
		public T* p => _p;

		/// <summary>
		/// Returns memory buffer pointer (<see cref="p"/>).
		/// </summary>
		public static implicit operator T*(in FastBuffer<T> b) => b._p;

		/// <summary>
		/// Gets reference to <c>p[i]</c>. Does not check bounds.
		/// </summary>
		public ref T this[int i] => ref _p[i];

		/// <summary>
		/// Memory buffer length as number of elements of type T.
		/// </summary>
		public int n => _n;

		/// <summary>
		/// Allocates first buffer of default size. It is on stack (in this variable), and its length is <c>StackSize/sizeof(T)</c> elements of type T (2048 bytes or 1024 chars or 512 ints...).
		/// </summary>
		public FastBuffer() {
			//With this overload slightly faster. Also, the int overload is confusing when need buffer of default size.

			_stack = 0;
			fixed (long* t = &_stack) { _p = (T*)t; }
			//_p = (T*)Unsafe.AsPointer(ref _stack); //slower in Debug, same speed in Release
			_n = StackSize / sizeof(T);
			_free = false;
		}

		/// <summary>
		/// Allocates first buffer of specified size.
		/// </summary>
		/// <param name="n">
		/// Buffer length (number of elements of type T).
		/// If <c>&lt;= StackSize/sizeof(T)</c>, the buffer contains<c> StackSize/sizeof(T)</c> elements on stack (in this variable); it is 2048 bytes or 1024 chars or 512 ints... Else allocates native memory (much slower).
		/// </param>
		public FastBuffer(int n) {
			_stack = 0;
			int nStack = StackSize / sizeof(T);
			if (_free = n > nStack) {
				_p = MemoryUtil.Alloc<T>(n + 1); //+1 for safety
				_n = n;
			} else {
				_n = nStack;
				fixed (long* t = &_stack) { _p = (T*)t; }
				//_p = (T*)Unsafe.AsPointer(ref _stack);
			}

			//rejected: for medium-size buffers use ArrayPool.
			//	It is usually much faster than MemoryUtil, but getting pinned pointer from array is slow.
			//	To get pinned pointer, I know 3 ways.
			//		1. fixed(...){...}. Here cannot be used.
			//		2. GCHandle. Makes 3 times slower than just ArrayPool Rent/Return. Then MemoryUtil is only 50% slower. With this buffer size it does not matter.
			//		3. Memory<T>/MemoryHandle. Same speed as GCHandle. MemoryHandle is bigger and managed.
			//	Tested: MemoryPool is slower than ArrayPool and creates garbage.

			//tested: heap memory allocation becomes much slower starting from 1 MB. Then virtual memory is several times faster (else much slower). But with this buffer size it does not matter.
		}

		/// <summary>
		/// Allocates new bigger buffer of specified length. Frees old buffer if need.
		/// </summary>
		/// <param name="n">Number of elements of type T.</param>
		/// <param name="preserve">Copy previous buffer contents to the new buffer.</param>
		/// <exception cref="ArgumentException"><i>n</i> &lt;= current buffer length.</exception>
		public void More(int n, bool preserve = false) {
			if (_n == 0) throw new ArgumentNullException(null, "No buffer. Use another constructor."); //with many API would still work, but very slow
			if (n <= _n) throw new ArgumentException("n <= this.n");
			if (!preserve) {
				Dispose();
				_p = MemoryUtil.Alloc<T>(n + 1); //+1 for safety
			} else if (_free) {
				MemoryUtil.ReAlloc<T>(ref _p, n + 1);
			} else {
				var p = MemoryUtil.Alloc<T>(n + 1);
				MemoryUtil.Copy(_p, p, _n * sizeof(T));
				_p = p;
			}
			_n = n;
			_free = true;
		}

		/// <summary>
		/// Allocates new bigger buffer of at least <c>n*2</c> length. Frees old buffer if need.
		/// </summary>
		/// <param name="preserve">Copy previous buffer contents to the new buffer.</param>
		public void More(bool preserve = false) => More(Math.Max(checked(_n * 2), 0x4000 / sizeof(T)), preserve); //16 KB = StackSize * 8

		/// <summary>
		/// Frees allocated memory if need.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose() {
			if (_free) { MemoryUtil.Free(_p); _p = null; _n = 0; }
		}

		/// <summary>
		/// Gets API result as string, or allocates bigger buffer if old buffer was too small.
		/// This function can be used when T is char.
		/// </summary>
		/// <param name="r">The return value of the called Windows API function, if it returns string length or required buffer length. Or you can call <see cref="FindStringLength"/>.</param>
		/// <param name="s">Receives the result string if succeeded, else <i>sDefault</i> (default <c>null</c>).</param>
		/// <param name="flags">
		/// Use if the API function isn't like this:
		/// <br/>• If succeeds, returns string length without the terminating <c>'\0'</c> character.
		/// <br/>• If buffer too small, returns required buffer length.
		/// <br/>• If fails, returns 0.
		/// </param>
		/// <param name="sDefault">Set <i>s</i> = this string if buffer too small or <i>r</i> &lt; 1 or if the retrieved string == this string (avoid creating new string).</param>
		/// <returns>
		/// If <i>r</i> &gt; <b>n</b>, calls <c>More(r);</c> and returns <c>false</c>.
		/// Else creates new string of <i>r</i> length and returns <c>true</c>.
		/// </returns>
		public bool GetString(int r, out string s, BSFlags flags = 0, string sDefault = null) {
			if (sizeof(T) != 2) throw new InvalidOperationException(); //cannot use extension method that would be added only to FastBuffer<char>. See GetString2 comments below.
			s = sDefault;

			if (r >= _n - 1) {
				if (0 != (flags & BSFlags.Truncates)) {
					if (r >= (0 != (flags & BSFlags.ReturnsLengthWith0) ? _n : _n - 1)) {
						More();
						return false;
					}
				} else if (r > _n) {
					More(r);
					return false;
				}
			}

			if (r > 0) {
				if (0 != (flags & BSFlags.ReturnsLengthWith0)) r--;
				if (sDefault == null || !new Span<char>(_p, r).SequenceEqual(sDefault)) s = new string((char*)_p, 0, r);
			}

			return true;
		}

		/// <summary>
		/// Finds length of <c>'\0'</c>-terminated UTF-16 string in buffer and converts to C# string.
		/// This function can be used when T is char. Use when length is unknown.
		/// </summary>
		/// <remarks>
		/// If there is no <c>'\0'</c> character, gets whole buffer, and the string probably is truncated.
		/// </remarks>
		public string GetStringFindLength() {
			return new((char*)_p, 0, FindStringLength());
		}

		/// <summary>
		/// Finds length of <c>'\0'</c>-terminated char string in buffer.
		/// Returns <see cref="n"/> if there is no <c>'\0'</c> character.
		/// </summary>
		public int FindStringLength() {
			if (sizeof(T) != 2) throw new InvalidOperationException();
			return CharPtr_.Length((char*)_p, _n);
		}

		/// <summary>
		/// Finds length of <c>'\0'</c>-terminated byte string in buffer.
		/// Returns <see cref="n"/> if there is no <c>'\0'</c> character.
		/// </summary>
		public int FindByteStringLength() {
			if (sizeof(T) != 1) throw new InvalidOperationException();
			return BytePtr_.Length((byte*)_p, _n);
		}
	}
}

namespace Au.Types {
	//error CS1657: Cannot use 'b' as a ref or out value because it is a 'using variable'.
	//If in, compiles, but very slow. Probably copies t because calls More() which isn't readonly.
	//public static partial class ExtAu
	//{
	//	public static unsafe bool GetString2(this ref FastBuffer<char> t, int r, out string s, BSFlags flags = 0, string sDefault = null) {
	//		...
	//	}
	//}

	/// <summary>
	/// Flags for <see cref="FastBuffer{T}.GetString"/>.
	/// </summary>
	[Flags]
	public enum BSFlags {
		/// <summary>
		/// If buffer too small, the API gets part of string instead of returning required buffer length.
		/// </summary>
		Truncates = 1,

		/// <summary>
		/// The API returns string length including the terminating <c>'\0'</c> character.
		/// </summary>
		ReturnsLengthWith0 = 2,
	}
}

//rejected. Maybe 5% faster, but not so easy to use. Need more code, and easy to forget something. Also VS warning if: for(var p = stackalloc ...).
///// <summary>
///// Allocates and frees native memory buffers for calling Windows API and other functions.
///// Used when need to retry when the primary stackalloc'ed buffer was too small.
///// </summary>
///// <example>
///// <code><![CDATA[
///// [SkipLocalsInit]
///// unsafe static string CurDir() {
///// 	using Buffer_<char> b = new(); int n, r;
///// 	for (var p = stackalloc char[n = 1024]; ; p = b.Alloc(n = r)) {
///// 		r = api.GetCurrentDirectory(n, p);
///// 		if(r < n) return r > 0 ? new(p, 0, r) : null;
///// 	}
///// }
///// 
///// [SkipLocalsInit]
///// unsafe static string WinText(wnd w) {
///// 	using Buffer_<char> b = new(); int n, r;
///// 	for (var p = stackalloc char[n = 1024]; ; p = b.Alloc(checked(n *= 2))) {
///// 		r = api.InternalGetWindowText(w, p, n);
///// 		if (r < n - 1) return new string(p, 0, r);
///// 	}
///// }
///// ]]></code>
///// </example>
//unsafe ref struct Buffer_<T> where T : unmanaged
//{
//	T* _p;

//	/// <summary>
//	/// Allocates n elements of type T of native memory. Frees old memory.
//	/// </summary>
//	public T* Alloc(int n) {
//		if (_p != null) { var p = _p; _p = null; MemoryUtil.Free(p); }
//		return _p = MemoryUtil.Alloc<T>(n);
//	}

//	/// <summary>
//	/// Frees allocated memory.
//	/// </summary>
//	public void Dispose() {
//		if (_p != null) { var p = _p; _p = null; MemoryUtil.Free(p); }
//	}
//}
