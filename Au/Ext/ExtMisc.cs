//note: be careful when adding functions to this class. Eg something may load winforms dlls although it seems not used.

namespace Au.Types;

/// <summary>
/// Adds extension methods for some .NET types.
/// </summary>
public static unsafe partial class ExtMisc {
	#region value types

	/// <summary>
	/// Converts to int with rounding.
	/// Calls <see cref="Convert.ToInt32(double)"/>.
	/// </summary>
	/// <exception cref="OverflowException"></exception>
	public static int ToInt(this double t) => Convert.ToInt32(t);

	/// <summary>
	/// Converts to int with rounding.
	/// Calls <see cref="Convert.ToInt32(float)"/>.
	/// </summary>
	/// <exception cref="OverflowException"></exception>
	public static int ToInt(this float t) => Convert.ToInt32(t);

	/// <summary>
	/// Converts to int with rounding.
	/// Calls <see cref="Convert.ToInt32(decimal)"/>.
	/// </summary>
	/// <exception cref="OverflowException"></exception>
	public static int ToInt(this decimal t) => Convert.ToInt32(t);

	//rejected. Too simple, and nobody would find and use.
	///// <summary>
	///// Converts to int.
	///// Can be used like <c>0xff123456.ToInt()</c> instead of <c>unchecked((int)0xff123456)</c>.
	///// </summary>
	//public static int ToInt(this uint t) => unchecked((int)t);

	///// <summary>
	///// Converts to System.Drawing.Color.
	///// Can be used like <c>0xff123456.ToColor_()</c> instead of <c>Color.FromArgb(unchecked((int)0xff123456))</c>.
	///// </summary>
	///// <param name="t"></param>
	///// <param name="makeOpaque">Add 0xff000000.</param>
	//internal static System.Drawing.Color ToColor_(this uint t, bool makeOpaque = true)
	//	=> System.Drawing.Color.FromArgb(unchecked((int)(t | (makeOpaque ? 0xff000000 : 0))));

	/// <summary>
	/// Converts to System.Drawing.Color. Makes opaque (alpha 0xff).
	/// Can be used like <c>0x123456.ToColor_()</c> instead of <c>Color.FromArgb(unchecked((int)0xff123456))</c>.
	/// </summary>
	internal static System.Drawing.Color ToColor_(this int t, bool bgr = false) {
		if (bgr) t = ColorInt.SwapRB(t);
		return System.Drawing.Color.FromArgb(unchecked(0xff << 24 | t));
	}

	/// <summary>
	/// Converts double to string.
	/// Uses invariant culture, therefore decimal point is always '.', not ',' etc.
	/// Calls <see cref="double.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this double t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}

	/// <summary>
	/// Converts double to string.
	/// Uses invariant culture, therefore decimal point is always '.', not ',' etc.
	/// Calls <see cref="float.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this float t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}

	/// <summary>
	/// Converts double to string.
	/// Uses invariant culture, therefore decimal point is always '.', not ',' etc.
	/// Calls <see cref="decimal.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this decimal t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}

	/// <summary>
	/// Converts int to string.
	/// Uses invariant culture, therefore minus sign is always ASCII '-', not '−' etc.
	/// Calls <see cref="int.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this int t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}

	/// <summary>
	/// Converts long to string.
	/// Uses invariant culture, therefore minus sign is always ASCII '-', not '−' etc.
	/// Calls <see cref="double.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this long t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}

	/// <summary>
	/// Converts nint to string.
	/// Uses invariant culture, therefore minus sign is always ASCII '-', not '−' etc.
	/// Calls <see cref="IntPtr.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this nint t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}
	//cref not nint.ToString because DocFX does not support it.

	//rare
	///// <summary>
	///// Returns true if t.Width &lt;= 0 || t.Height &lt;= 0.
	///// Note: <b>Rectangle.IsEmpty</b> returns true only when all fields are 0.
	///// </summary>
	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
	//public static bool NoArea(this System.Drawing.Rectangle t) {
	//	return t.Width <= 0 || t.Height <= 0;
	//}

	/// <summary>
	/// Calls <see cref="Range.GetOffsetAndLength"/> and returns start and end instead of start and length.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="length"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static (int start, int end) GetStartEnd(this Range t, int length) {
		var v = t.GetOffsetAndLength(length);
		return (v.Offset, v.Offset + v.Length);
	}

	/// <summary>
	/// If this is null, returns <c>(0, length)</c>. Else calls <see cref="Range.GetOffsetAndLength"/> and returns start and end instead of start and length.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="length"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static (int start, int end) GetStartEnd(this Range? t, int length)
		=> t?.GetStartEnd(length) ?? (0, length);

	/// <summary>
	/// If this is null, returns <c>(0, length)</c>. Else calls <see cref="Range.GetOffsetAndLength"/>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="length"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static (int Offset, int Length) GetOffsetAndLength(this Range? t, int length)
		=> t?.GetOffsetAndLength(length) ?? (0, length);

	//currently not used. Creates shorter string than ToString.
	///// <summary>
	///// Converts this <b>Guid</b> to Base64 string.
	///// </summary>
	//public static string ToBase64(this Guid t) => Convert.ToBase64String(new ReadOnlySpan<byte>((byte*)&t, sizeof(Guid)));

	//rejected: too simple. We have print.it(uint), also can use $"0x{t:X}" or "0x" + t.ToString("X").
	///// <summary>
	///// Converts int to hexadecimal string like "0x3A".
	///// </summary>
	//public static string ToHex(this int t)
	//{
	//	return "0x" + t.ToString("X");
	//}

	#endregion

	#region enum

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static long _ToLong<T>(T v) where T : unmanaged, Enum {
		if (sizeof(T) == 4) return *(int*)&v;
		if (sizeof(T) == 8) return *(long*)&v;
		if (sizeof(T) == 2) return *(short*)&v;
		return *(byte*)&v;
		//Compiler removes the if(sizeof(T) == n) and code that is unused with that size, because sizeof(T) is const.
		//Faster than with switch(sizeof(T)). It seems the switch code is considered too big to be inlined.
	}

	//same. Was faster when tested in the past.
	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
	//static long _ToLong2<T>(T v) where T : unmanaged, Enum
	//{
	//	if(sizeof(T) == 4) return Unsafe.As<T, int>(ref v);
	//	if(sizeof(T) == 8) return Unsafe.As<T, long>(ref v);
	//	if(sizeof(T) == 2) return Unsafe.As<T, short>(ref v);
	//	return Unsafe.As<T, byte>(ref v);
	//}

	/// <summary>
	/// Returns true if this enum variable has all flag bits specified in <i>flag</i>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="flag">One or more flags.</param>
	/// <remarks>
	/// The same as code <c>(t &amp; flag) == flag</c> or <b>Enum.HasFlag</b>.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Has<T>(this T t, T flag) where T : unmanaged, Enum {
#if false //Enum.HasFlag used to be slow, but now compiler for it creates the same code as with operator
			return t.HasFlag(flag);
			//However cannot use this because of JIT compiler bug: in some cases Has returns true when no flag.
			//Noticed it in TriggerActionThreads.Run in finally{} of actionWrapper, code o.flags.Has(TOFlags.Single).
			//It was elusive, difficult to debug, only in Release, and only after some time/times, when tiered JIT fully optimizes.
			//When Has returned true, print.it showed that flags is 0.
			//No bug if HasFlag called directly, not in extension method.
#elif true //slightly slower than Enum.HasFlag and code as with operator
		var m = _ToLong(flag);
		return (_ToLong(t) & m) == m;
#else //slower
			switch(sizeof(T)) {
			case 4: {
				var a = Unsafe.As<T, uint>(ref t);
				var b = Unsafe.As<T, uint>(ref flag);
				return (a & b) == b;
			}
			case 8: {
				var a = Unsafe.As<T, ulong>(ref t);
				var b = Unsafe.As<T, ulong>(ref flag);
				return (a & b) == b;
			}
			case 2: {
				var a = Unsafe.As<T, ushort>(ref t);
				var b = Unsafe.As<T, ushort>(ref flag);
				return (a & b) == b;
			}
			default: {
				var a = Unsafe.As<T, byte>(ref t);
				var b = Unsafe.As<T, byte>(ref flag);
				return (a & b) == b;
			}
			}
			//compiler removes the switch/case, because sizeof(T) is const
#endif
	}

	/// <summary>
	/// Returns true if this enum variable has one or more flag bits specified in <i>flags</i>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="flags">One or more flags.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool HasAny<T>(this T t, T flags) where T : unmanaged, Enum {
		return (_ToLong(t) & _ToLong(flags)) != 0;
	}

	//slower
	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
	//public static bool HasAny5<T>(this T t, T flags) where T : unmanaged, Enum
	//{
	//	if(sizeof(T) == 4) return (*(int*)&t & *(int*)&flags) != 0;
	//	if(sizeof(T) == 8) return (*(long*)&t & *(long*)&flags) != 0;
	//	if(sizeof(T) == 2) return (*(short*)&t & *(short*)&flags) != 0;
	//	return (*(byte*)&t & *(byte*)&flags) != 0;
	//}

	/// <summary>
	/// Adds or removes a flag.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="flag">One or more flags to add or remove.</param>
	/// <param name="add">If true, adds flag, else removes flag.</param>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static void SetFlag<T>(ref this T t, T flag, bool add) where T : unmanaged, Enum {
		long a = _ToLong(t), b = _ToLong(flag);
		if (add) a |= b; else a &= ~b;
		t = *(T*)&a;
	}

	//rejected. Rarely used. Adds many garbage in compiled documentation for enums. Maybe C# 10 will have a list pattern for it.
	//	Can istead write: if(e is EnumX.Val1 or EnumX.Val2 ...). But currently problems with intellisense; works better with (): if(e is (EnumX.Val1 or EnumX.Val2 ...)).
	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn<T>(this T t, T v1, T v2) where T : unmanaged, Enum //could be IConvertible, it includes int etc, but also double, float, bool, DateTime and any IConvertible struct of any size. Types other than enum and int actually are not useful.
	//		{
	//			var a = _ToLong(t);
	//			return a == _ToLong(v1) || a == _ToLong(v2);
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn<T>(this T t, T v1, T v2, T v3) where T : unmanaged, Enum {
	//			var a = _ToLong(t);
	//			return a == _ToLong(v1) || a == _ToLong(v2) || a == _ToLong(v3);
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn<T>(this T t, T v1, T v2, T v3, T v4) where T : unmanaged, Enum {
	//			//return t.Equals(v1) || t.Equals(v2) || t.Equals(v3) || t.Equals(v4); //very slow, slower than with params
	//			var a = _ToLong(t);
	//			return a == _ToLong(v1) || a == _ToLong(v2) || a == _ToLong(v3) || a == _ToLong(v4);
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn<T>(this T t, T v1, T v2, T v3, T v4, T v5) where T : unmanaged, Enum {
	//			var a = _ToLong(t);
	//			return a == _ToLong(v1) || a == _ToLong(v2) || a == _ToLong(v3) || a == _ToLong(v4) || a == _ToLong(v5);
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn<T>(this T t, T v1, T v2, T v3, T v4, T v5, T v6) where T : unmanaged, Enum {
	//			var a = _ToLong(t);
	//			return a == _ToLong(v1) || a == _ToLong(v2) || a == _ToLong(v3) || a == _ToLong(v4) || a == _ToLong(v5) || a == _ToLong(v6);
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn<T>(this T t, T v1, T v2, T v3, T v4, T v5, T v6, T v7) where T : unmanaged, Enum {
	//			var a = _ToLong(t);
	//			return a == _ToLong(v1) || a == _ToLong(v2) || a == _ToLong(v3) || a == _ToLong(v4) || a == _ToLong(v5) || a == _ToLong(v6) || a == _ToLong(v7);
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn<T>(this T t, T v1, T v2, T v3, T v4, T v5, T v6, T v7, T v8) where T : unmanaged, Enum {
	//			var a = _ToLong(t);
	//			return a == _ToLong(v1) || a == _ToLong(v2) || a == _ToLong(v3) || a == _ToLong(v4) || a == _ToLong(v5) || a == _ToLong(v6) || a == _ToLong(v7) || a == _ToLong(v8);
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn<T>(this T t, params T[] values) where T : unmanaged, Enum {
	//#if true //slightly faster, especially with multitiered JIT. The slow part is creating the params array.
	//			var a = _ToLong(t);
	//			for (int i = 0; i < values.Length; i++) {
	//				if (a == _ToLong(values[i])) return true;
	//			}
	//			return false;
	//#else
	//        return values.Contains(t);
	//#endif
	//		}

	//		//same for int

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn(int t, int v1, int v2) {
	//			return t == v1 || t == v2;
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn(int t, int v1, int v2, int v3) {
	//			return t == v1 || t == v2 || t == v3;
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn(int t, int v1, int v2, int v3, int v4) {
	//			return t == v1 || t == v2 || t == v3 || t == v4;
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn(int t, int v1, int v2, int v3, int v4, int v5) {
	//			return t == v1 || t == v2 || t == v3 || t == v4 || t == v5;
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn(int t, int v1, int v2, int v3, int v4, int v5, int v6) {
	//			return t == v1 || t == v2 || t == v3 || t == v4 || t == v5 || t == v6;
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn(int t, int v1, int v2, int v3, int v4, int v5, int v6, int v7) {
	//			return t == v1 || t == v2 || t == v3 || t == v4 || t == v5 || t == v6 || t == v7;
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn(int t, int v1, int v2, int v3, int v4, int v5, int v6, int v7, int v8) {
	//			return t == v1 || t == v2 || t == v3 || t == v4 || t == v5 || t == v6 || t == v7 || t == v8;
	//		}

	//		/// <summary>Returns true if this is equal to a value in list.</summary>
	//		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	//		public static bool IsIn(int t, params int[] values) {
	//			for (int i = 0; i < values.Length; i++) {
	//				if (t == values[i]) return true;
	//			}
	//			return false;
	//		}

	#endregion

	#region char

	/// <summary>
	/// Returns true if character is ASCII '0' to '9'.
	/// </summary>
	public static bool IsAsciiDigit(this char c) => c <= '9' && c >= '0';

	/// <summary>
	/// Returns true if character is ASCII 'A' to 'Z' or 'a' to 'z'.
	/// </summary>
	public static bool IsAsciiAlpha(this char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

	/// <summary>
	/// Returns true if character is ASCII 'A' to 'Z' or 'a' to 'z' or '0' to '9'.
	/// </summary>
	public static bool IsAsciiAlphaDigit(this char c) => IsAsciiAlpha(c) || IsAsciiDigit(c);

	#endregion

	#region array

	/// <summary>
	/// Creates a copy of this array with one or more removed elements.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="t"></param>
	/// <param name="index"></param>
	/// <param name="count"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static T[] RemoveAt<T>(this T[] t, int index, int count = 1) {
		if ((uint)index > t.Length || count < 0 || index + count > t.Length) throw new ArgumentOutOfRangeException();
		int n = t.Length - count;
		if (n == 0) return Array.Empty<T>();
		var r = new T[n];
		for (int i = 0; i < index; i++) r[i] = t[i];
		for (int i = index; i < n; i++) r[i] = t[i + count];
		return r;
	}

	/// <summary>
	/// Creates a copy of this array with one inserted element.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="t"></param>
	/// <param name="index">Where to insert. If -1, adds to the end.</param>
	/// <param name="value"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static T[] InsertAt<T>(this T[] t, int index, T value = default) {
		if (index == -1) index = t.Length;
		if ((uint)index > t.Length) throw new ArgumentOutOfRangeException();
		var r = new T[t.Length + 1];
		for (int i = 0; i < index; i++) r[i] = t[i];
		for (int i = index; i < t.Length; i++) r[i + 1] = t[i];
		r[index] = value;
		return r;
	}

	/// <summary>
	/// Creates a copy of this array with several inserted elements.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="t"></param>
	/// <param name="index"></param>
	/// <param name="values"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static T[] InsertAt<T>(this T[] t, int index, params T[] values) {
		if ((uint)index > t.Length) throw new ArgumentOutOfRangeException();
		int n = values?.Length ?? 0; if (n == 0) return t;

		var r = new T[t.Length + n];
		for (int i = 0; i < index; i++) r[i] = t[i];
		for (int i = index; i < t.Length; i++) r[i + n] = t[i];
		for (int i = 0; i < n; i++) r[i + index] = values[i];
		return r;
	}

	internal static void WriteInt(this byte[] t, int x, int index) {
		if (index < 0 || index > t.Length - 4) throw new ArgumentOutOfRangeException();
		fixed (byte* p = t) *(int*)(p + index) = x;
	}

	internal static int ReadInt(this byte[] t, int index) {
		if (index < 0 || index > t.Length - 4) throw new ArgumentOutOfRangeException();
		fixed (byte* p = t) return *(int*)(p + index);
	}

	#endregion

	#region IEnumerable

	/// <summary>
	/// Removes items based on a predicate. For example, all items that have certain value.
	/// </summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
	/// <param name="t"></param>
	/// <param name="predicate"></param>
	public static void RemoveWhere<TKey, TValue>(this Dictionary<TKey, TValue> t, Func<KeyValuePair<TKey, TValue>, bool> predicate) {
		foreach (var k in t.Where(predicate).Select(kv => kv.Key).ToArray()) { t.Remove(k); }
	}

	/// <summary>
	/// Adds key/value to dictionary. If the key already exists, adds the value to the same key as <b>List</b> item and returns the <b>List</b>; else returns null.
	/// </summary>
	/// <exception cref="ArgumentException">key/value already exists.</exception>
	internal static List<TValue> MultiAdd_<TKey, TValue>(this Dictionary<TKey, object> t, TKey k, TValue v) where TValue : class {
		if (t.TryAdd(k, v)) return null;
		var o = t[k];
		if (o is List<TValue> a) {
			if (!a.Contains(v)) { a.Add(v); return a; }
		} else {
			var g = o as TValue;
			if (g == null && o != null) throw new ArgumentException("bad type");
			if (v != g) { t[k] = a = new List<TValue> { g, v }; return a; }
		}
		throw new ArgumentException("key/value already exists");
	}

	/// <summary>
	/// If dictionary contains key <i>k</i> that contains value <i>v</i> (as single value or in <b>List</b>), removes the value (and key if it was single value) and returns true.
	/// </summary>
	internal static bool MultiRemove_<TKey, TValue>(this Dictionary<TKey, object> t, TKey k, TValue v) where TValue : class {
		if (!t.TryGetValue(k, out var o)) return false;
		if (o is List<TValue> a) {
			if (!a.Remove(v)) return false;
			if (a.Count == 1) t[k] = a[0];
		} else {
			var g = o as TValue;
			if (g == null && o != null) throw new ArgumentException("bad type");
			if (v != g) return false;
			t.Remove(k);
		}
		return true;
	}

	/// <summary>
	/// If dictionary contains key <i>k</i>, gets its value (<i>v</i>) or list of values (<i>a</i>) and returns true.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="k"></param>
	/// <param name="v">Receives single value, or null if the key has multiple values.</param>
	/// <param name="a">Receives multiple values, or null if the key has single value.</param>
	internal static bool MultiGet_<TKey, TValue>(this Dictionary<TKey, object> t, TKey k, out TValue v, out List<TValue> a) where TValue : class {
		bool r = t.TryGetValue(k, out var o);
		v = o as TValue;
		a = o as List<TValue>;
		if (v == null && a == null && o != null) throw new ArgumentException("bad type");
		return r;
	}

	/// <summary>
	/// Returns <b>Length</b>, or 0 if null.
	/// </summary>
	internal static int Lenn_<T>(this T[] t) => t?.Length ?? 0;
	//internal static int Lenn_(this System.Collections.ICollection t) => t?.Count ?? 0; //slower, as well as Array

	/// <summary>
	/// Returns <b>Count</b>, or 0 if null.
	/// </summary>
	internal static int Lenn_<T>(this List<T> t) => t?.Count ?? 0;

	/// <summary>
	/// Returns true if null or <b>Length</b> == 0.
	/// </summary>
	internal static bool NE_<T>(this T[] t) => (t?.Length ?? 0) == 0;

	/// <summary>
	/// Returns true if null or <b>Count</b> == 0.
	/// </summary>
	internal static bool NE_<T>(this List<T> t) => (t?.Count ?? 0) == 0;

	/// <summary>
	/// Gets range 0..Count of the internal array. Calls <see cref="CollectionsMarshal.AsSpan"/>.
	/// </summary>
	internal static Span<T> AsSpan_<T>(this List<T> t) => CollectionsMarshal.AsSpan(t);

	/// <summary>
	/// Efficiently recursively gets descendants of this tree.
	/// <see href="https://stackoverflow.com/a/30441479/2547338"/>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="t"></param>
	/// <param name="childSelector"></param>
	internal static IEnumerable<T> Descendants_<T>(this IEnumerable<T> t, Func<T, IEnumerable<T>> childSelector) {
		var stack = new Stack<IEnumerator<T>>();
		var enumerator = t.GetEnumerator();

		try {
			while (true) {
				if (enumerator.MoveNext()) {
					T element = enumerator.Current;
					yield return element;

					var e = childSelector(element)?.GetEnumerator();
					if (e != null) {
						stack.Push(enumerator);
						enumerator = e;
					}
				} else if (stack.Count > 0) {
					enumerator.Dispose();
					enumerator = stack.Pop();
				} else {
					yield break;
				}
			}
		}
		finally {
			enumerator.Dispose();

			while (stack.Count > 0) // Clean up in case of an exception.
			{
				enumerator = stack.Pop();
				enumerator.Dispose();
			}
		}
	}

	/// <summary>
	/// Efficiently recursively gets descendants of this tree.
	/// <see href="https://stackoverflow.com/a/30441479/2547338"/>
	/// </summary>
	/// <param name="t"></param>
	/// <param name="childSelector"></param>
	internal static System.Collections.IEnumerable Descendants_(this System.Collections.IEnumerable t, Func<object, System.Collections.IEnumerable> childSelector) {
		var stack = new Stack<System.Collections.IEnumerator>();
		var enumerator = t.GetEnumerator();

		while (true) {
			if (enumerator.MoveNext()) {
				object element = enumerator.Current;
				yield return element;

				var e = childSelector(element)?.GetEnumerator();
				if (e != null) {
					stack.Push(enumerator);
					enumerator = e;
				}
			} else if (stack.Count > 0) {
				enumerator = stack.Pop();
			} else {
				yield break;
			}
		}
	}

	#endregion

	#region StringBuilder

	/// <summary>
	/// Appends string as new correctly formatted sentence.
	/// </summary>
	/// <returns>this.</returns>
	/// <param name="t"></param>
	/// <param name="s"></param>
	/// <param name="noUcase">Don't make the first character uppercase.</param>
	/// <remarks>
	/// If s is null or "", does nothing.
	/// If this is not empty, appends space.
	/// If s starts with a lowercase character, makes it uppercase, unless this ends with a character other than '.'.
	/// Appends '.' if s does not end with '.', ';', ':', ',', '!' or '?'.
	/// </remarks>
	public static StringBuilder AppendSentence(this StringBuilder t, string s, bool noUcase = false) {
		if (!s.NE()) {
			bool makeUcase = !noUcase && Char.IsLower(s[0]);
			if (t.Length > 0) {
				if (makeUcase && t[^1] != '.') makeUcase = false;
				t.Append(' ');
			}
			if (makeUcase) { t.Append(Char.ToUpper(s[0])).Append(s, 1, s.Length - 1); } else t.Append(s);
			switch (s[^1]) {
			case '.': case ';': case ':': case ',': case '!': case '?': break;
			default: t.Append('.'); break;
			}
		}
		return t;
	}

	#endregion

	#region winforms

	/// <summary>
	/// Gets window handle as <see cref="wnd"/>.
	/// </summary>
	/// <param name="t">A <b>Control</b> or <b>Form</b> etc. Cannot be null.</param>
	/// <param name="create">
	/// Create handle if still not created. Default false (return default(wnd)).
	/// Unlike <see cref="System.Windows.Forms.Control.CreateControl"/>, creates handle even if invisible. Does not create child control handles.
	/// </param>
	/// <remarks>
	/// Should be called in control's thread. Calls <see cref="System.Windows.Forms.Control.IsHandleCreated"/> and <see cref="System.Windows.Forms.Control.Handle"/>.
	/// </remarks>
	public static wnd Hwnd(this System.Windows.Forms.Control t, bool create = false)
		=> create || t.IsHandleCreated ? new wnd(t.Handle) : default;

	#endregion

	#region System.Drawing

	/// <summary>
	/// Draws inset or outset rectangle.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="pen">Pen with integer width and default alignment.</param>
	/// <param name="r"></param>
	/// <param name="outset">Draw outset.</param>
	/// <remarks>
	/// Calls <see cref="System.Drawing.Graphics.DrawRectangle"/> with arguments corrected so that it draws inside or outside <i>r</i>. Does not use <see cref="System.Drawing.Drawing2D.PenAlignment"/>, it is unreliable.
	/// </remarks>
	public static void DrawRectangleInset(this System.Drawing.Graphics t, System.Drawing.Pen pen, RECT r, bool outset = false) {
		if (r.NoArea) return;
		//pen.Alignment = PenAlignment.Inset; //no. Eg ignored if 1 pixel width.
		//	MSDN: "A Pen that has its alignment set to Inset will yield unreliable results, sometimes drawing in the inset position and sometimes in the centered position.".
		var r0 = r;
		int w = (int)pen.Width, d = w / 2;
		r.left += d; r.top += d;
		r.right -= d = w - d; r.bottom -= d;
		if (outset) r.Inflate(w, w);
		if (!r.NoArea) {
			t.DrawRectangle(pen, r);
		} else { //DrawRectangle does not draw if width or height 0, even if pen alignment is Outset
			t.FillRectangle(pen.Brush, r0); //never mind dash style etc
		}
	}

	/// <summary>
	/// Draws inset rectangle of specified pen color and width.
	/// </summary>
	/// <remarks>
	/// Creates pen and calls other overload.
	/// </remarks>
	public static void DrawRectangleInset(this System.Drawing.Graphics t, System.Drawing.Color penColor, int penWidth, RECT r, bool outset = false) {
		using var pen = new System.Drawing.Pen(penColor, penWidth);
		DrawRectangleInset(t, pen, r, outset);
	}

	/// <summary>
	/// Creates solid brush and calls <see cref="System.Drawing.Graphics.FillRectangle"/>.
	/// </summary>
	public static void FillRectangle(this System.Drawing.Graphics t, System.Drawing.Color color, RECT r) {
		using var brush = new System.Drawing.SolidBrush(color);
		t.FillRectangle(brush, r);
	}

	#endregion
}
