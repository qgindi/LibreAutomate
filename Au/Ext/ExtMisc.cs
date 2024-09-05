//note: be careful when adding functions to this class. Eg something may load winforms dlls although it seems not used.

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;

namespace Au.Types;

/// <summary>
/// Adds extension methods for some .NET types.
/// </summary>
[DebuggerStepThrough]
public static unsafe partial class ExtMisc {
	#region value types
	
	/// <summary>
	/// Converts to <b>int</b> with rounding.
	/// Calls <see cref="Convert.ToInt32(double)"/>.
	/// </summary>
	/// <exception cref="OverflowException"></exception>
	public static int ToInt(this double t) => Convert.ToInt32(t);
	
	/// <summary>
	/// Converts to <b>int</b> with rounding.
	/// Calls <see cref="Convert.ToInt32(float)"/>.
	/// </summary>
	/// <exception cref="OverflowException"></exception>
	public static int ToInt(this float t) => Convert.ToInt32(t);
	
	/// <summary>
	/// Converts to <b>int</b> with rounding.
	/// Calls <see cref="Convert.ToInt32(decimal)"/>.
	/// </summary>
	/// <exception cref="OverflowException"></exception>
	public static int ToInt(this decimal t) => Convert.ToInt32(t);
	
	//rejected. Too simple, and nobody would find and use.
	///// <summary>
	///// Converts to <b>int</b>.
	///// Can be used like <c>0xff123456.ToInt()</c> instead of <c>unchecked((int)0xff123456)</c>.
	///// </summary>
	//public static int ToInt(this uint t) => unchecked((int)t);
	
	///// <summary>
	///// Converts to <b>Color</b>.
	///// Can be used like <c>0xff123456.ToColor_()</c> instead of <c>Color.FromArgb(unchecked((int)0xff123456))</c>.
	///// </summary>
	///// <param name="t"></param>
	///// <param name="makeOpaque">Add 0xff000000.</param>
	//internal static Color ToColor_(this uint t, bool makeOpaque = true)
	//	=> Color.FromArgb(unchecked((int)(t | (makeOpaque ? 0xff000000 : 0))));
	
	/// <summary>
	/// Converts to <b>Color</b>. Makes opaque (alpha 0xff).
	/// Can be used like <c>0x123456.ToColor_()</c> instead of <c>Color.FromArgb(unchecked((int)0xff123456))</c>.
	/// </summary>
	internal static Color ToColor_(this int t, bool bgr = false) {
		if (bgr) t = ColorInt.SwapRB(t);
		return Color.FromArgb(unchecked(0xff << 24 | t));
	}
	
	/// <summary>
	/// Converts <b>double</b> to <b>string</b>.
	/// Uses invariant culture, therefore decimal point is always <c>'.'</c>, not <c>','</c> etc.
	/// Calls <see cref="double.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this double t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}
	
	/// <summary>
	/// Converts <b>float</b> to <b>string</b>.
	/// Uses invariant culture, therefore decimal point is always <c>'.'</c>, not <c>','</c> etc.
	/// Calls <see cref="float.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this float t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}
	
	/// <summary>
	/// Converts <b>decimal</b> to <b>string</b>.
	/// Uses invariant culture, therefore decimal point is always <c>'.'</c>, not <c>','</c> etc.
	/// Calls <see cref="decimal.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this decimal t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}
	
	/// <summary>
	/// Converts <b>int</b> to <b>string</b>.
	/// Uses invariant culture, therefore minus sign is always ASCII <c>'-',</c> not <c>'−'</c> etc.
	/// Calls <see cref="int.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this int t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}
	
	/// <summary>
	/// Converts <b>long</b> to <b>string</b>.
	/// Uses invariant culture, therefore minus sign is always ASCII <c>'-'</c>, not <c>'−'</c> etc.
	/// Calls <see cref="double.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this long t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}
	
	/// <summary>
	/// Converts <b>nint</b> to <b>string</b>.
	/// Uses invariant culture, therefore minus sign is always ASCII <c>'-'</c>, not <c>'−'</c> etc.
	/// Calls <see cref="IntPtr.ToString(string, IFormatProvider)"/>.
	/// </summary>
	public static string ToS(this nint t, string format = null) {
		return t.ToString(format, NumberFormatInfo.InvariantInfo);
	}
	//cref not nint.ToString because DocFX does not support it.
	
	//rare
	///// <summary>
	///// Returns <c>true</c> if <c>t.Width &lt;= 0 || t.Height &lt;= 0</c>.
	///// Note: <b>Rectangle.IsEmpty</b> returns <c>true</c> only when all fields are 0.
	///// </summary>
	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
	//public static bool NoArea(this Rectangle t) {
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
	/// If this is <c>null</c>, returns <c>(0, length)</c>. Else calls <see cref="Range.GetOffsetAndLength"/> and returns start and end instead of start and length.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="length"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static (int start, int end) GetStartEnd(this Range? t, int length)
		=> t?.GetStartEnd(length) ?? (0, length);
	
	/// <summary>
	/// If this is <c>null</c>, returns <c>(0, length)</c>. Else calls <see cref="Range.GetOffsetAndLength"/>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="length"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static (int Offset, int Length) GetOffsetAndLength(this Range? t, int length)
		=> t?.GetOffsetAndLength(length) ?? (0, length);
	
	/// <summary>
	/// Returns <c>true</c> if null pointer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNull<T>(this ReadOnlySpan<T> t) => t == ReadOnlySpan<T>.Empty;
	
	//currently not used. Creates shorter string than ToString.
	///// <summary>
	///// Converts this <b>Guid</b> to Base64 string.
	///// </summary>
	//public static string ToBase64(this Guid t) => Convert.ToBase64String(new RByte((byte*)&t, sizeof(Guid)));
	
	//rejected: too simple. We have print.it(uint), also can use $"0x{t:X}" or "0x" + t.ToString("X").
	///// <summary>
	///// Converts <b>int</b> to hexadecimal string like <c>"0x3A"</c>.
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
	/// Returns <c>true</c> if this enum variable has all flag bits specified in <i>flag</i>.
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
	/// Returns <c>true</c> if this enum variable has one or more flag bits specified in <i>flags</i>.
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
	/// Adds or removes a flag or flags.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="flag">One or more flags to add or remove.</param>
	/// <param name="add">If <c>true</c>, adds flag, else removes flag.</param>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static void SetFlag<T>(ref this T t, T flag, bool add) where T : unmanaged, Enum {
		long a = _ToLong(t), b = _ToLong(flag);
		if (add) a |= b; else a &= ~b;
		t = *(T*)&a;
	}
	
	/// <summary>
	/// Adds or removes a flag or flags.
	/// </summary>
	/// <param name="flag">Flag(s) to add or remove.</param>
	/// <param name="add">If <c>true</c>, adds the flag(s) (<c>t |= flag</c>), else removes (<c>t &amp;= ~flag</c>).</param>
	internal static void SetFlag_(this ref int t, int flag, bool add) {
		if (add) t |= flag; else t &= ~flag;
	}
	
	/// <inheritdoc cref="SetFlag_(ref int, int, bool)"/>
	internal static void SetFlag_(this ref uint t, uint flag, bool set) {
		if (set) t |= flag; else t &= ~flag;
	}
	
	/// <inheritdoc cref="SetFlag_(ref int, int, bool)"/>
	internal static void SetFlag_(this ref ushort t, ushort flag, bool set) {
		if (set) t |= flag; else t = (ushort)(t & ~flag);
	}
	
	/// <inheritdoc cref="SetFlag_(ref int, int, bool)"/>
	internal static void SetFlag_(this ref byte t, byte flag, bool set) {
		if (set) t |= flag; else t = (byte)(t & ~flag);
	}

	#endregion

	#region char

#if NET8_0_OR_GREATER
	/// <summary>
	/// Returns <c>true</c> if character is ASCII <c>'0'</c> to <c>'9'</c>.
	/// </summary>
	public static bool IsAsciiDigit(this char c) => char.IsAsciiDigit(c);
	
	/// <summary>
	/// Returns <c>true</c> if character is ASCII <c>'A'</c> to <c>'Z'</c> or <c>'a'</c> to <c>'z'</c>.
	/// </summary>
	//public static bool IsAsciiAlpha(this char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
	public static bool IsAsciiAlpha(this char c) => char.IsAsciiLetter(c);
	
	/// <summary>
	/// Returns <c>true</c> if character is ASCII <c>'A'</c> to <c>'Z'</c> or <c>'a'</c> to <c>'z'</c> or <c>'0'</c> to <c>'9'</c>.
	/// </summary>
	public static bool IsAsciiAlphaDigit(this char c) => char.IsAsciiLetterOrDigit(c);
#else
	/// <summary>
	/// Returns <c>true</c> if character is ASCII <c>'0'</c> to <c>'9'</c>.
	/// </summary>
	public static bool IsAsciiDigit(this char c) => c <= '9' && c >= '0';
	
	/// <summary>
	/// Returns <c>true</c> if character is ASCII <c>'A'</c> to <c>'Z'</c> or <c>'a'</c> to <c>'z'</c>.
	/// </summary>
	public static bool IsAsciiAlpha(this char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
	
	/// <summary>
	/// Returns <c>true</c> if character is ASCII <c>'A'</c> to <c>'Z'</c> or <c>'a'</c> to <c>'z'</c> or <c>'0'</c> to <c>'9'</c>.
	/// </summary>
	public static bool IsAsciiAlphaDigit(this char c) => IsAsciiAlpha(c) || IsAsciiDigit(c);
#endif

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
		if (index == -1) index = t.Length; else if ((uint)index > t.Length) throw new ArgumentOutOfRangeException();
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
	/// <param name="index">Where to insert. If -1, adds to the end.</param>
	/// <param name="values"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static T[] InsertAt<T>(this T[] t, int index, params ReadOnlySpan<T> values) {
		if (index == -1) index = t.Length; else if ((uint)index > t.Length) throw new ArgumentOutOfRangeException();
		int n = values.Length; if (n == 0) return t;
		
		var r = new T[t.Length + n];
		for (int i = 0; i < index; i++) r[i] = t[i];
		for (int i = index; i < t.Length; i++) r[i + n] = t[i];
		for (int i = 0; i < n; i++) r[i + index] = values[i];
		return r;
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
	/// Gets a reference to a <b>TValue</b> in this dictionary, adding a new entry with a default value if the key does not exist.
	/// This extension method just calls <see cref="CollectionsMarshal.GetValueRefOrAddDefault"/>.
	/// </summary>
	/// <inheritdoc cref="CollectionsMarshal.GetValueRefOrAddDefault"/>
	/// <example>
	/// <code><![CDATA[
	/// var d = new Dictionary<string, int>();
	/// for (int i = 0; i < 3; i++) {
	/// 	ref var r = ref d.GetValueRefOrAddDefault("a", out bool exists);
	/// 	print.it(exists);
	/// 	if(!exists) r = 100; else r++;
	/// }
	/// print.it(d);
	/// ]]></code>
	/// </example>
	internal static ref TValue GetValueRefOrAddDefault_<TKey, TValue>(this Dictionary<TKey, TValue> t, TKey key, out bool exists) {
#pragma warning disable 9088 //weird and undocumented: "This returns a parameter by reference 'exists' but it is scoped to the current method"
		return ref CollectionsMarshal.GetValueRefOrAddDefault(t, key, out exists);
	}
	
	/// <summary>
	/// Gets a reference to a <b>TValue</b> in this dictionary. If the key does not exist, sets <i>exists</i> = <c>false</c> and returns a reference <c>null</c>.
	/// This extension method just calls <see cref="CollectionsMarshal.GetValueRefOrNullRef"/> and <see cref="Unsafe.IsNullRef"/>.
	/// </summary>
	/// <param name="exists">Receives <c>true</c> if the key exists.</param>
	/// <inheritdoc cref="CollectionsMarshal.GetValueRefOrNullRef"/>
	internal static ref TValue GetValueRefOrNullRef_<TKey, TValue>(this Dictionary<TKey, TValue> t, TKey key, out bool exists) {
		ref TValue r = ref CollectionsMarshal.GetValueRefOrNullRef(t, key);
		exists = !Unsafe.IsNullRef(ref r);
		return ref r;
	}
	
	/// <inheritdoc cref="CollectionsMarshal.AsSpan"/>
	public static Span<T> AsSpan<T>(this List<T> t) where T : struct
		=> CollectionsMarshal.AsSpan(t);
	
	/// <summary>
	/// Gets a reference to an item.
	/// List items must not be added or removed while it is in use.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="i">Item index.</param>
	public static ref T Ref<T>(this List<T> t, int i) where T : struct
		=> ref CollectionsMarshal.AsSpan(t)[i];
	
	/// <summary>
	/// Adds key/value to dictionary. If the key already exists, adds the value to the same key as <b>List</b> item and returns the <b>List</b>; else returns <c>null</c>.
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
	/// If dictionary contains key <i>k</i> that contains value <i>v</i> (as single value or in <b>List</b>), removes the value (and key if it was single value) and returns <c>true</c>.
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
	/// If dictionary contains key <i>k</i>, gets its value (<i>v</i>) or list of values (<i>a</i>) and returns <c>true</c>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="k"></param>
	/// <param name="v">Receives single value, or <c>null</c> if the key has multiple values.</param>
	/// <param name="a">Receives multiple values, or <c>null</c> if the key has single value.</param>
	internal static bool MultiGet_<TKey, TValue>(this Dictionary<TKey, object> t, TKey k, out TValue v, out List<TValue> a) where TValue : class {
		bool r = t.TryGetValue(k, out var o);
		v = o as TValue;
		a = o as List<TValue>;
		if (v == null && a == null && o != null) throw new ArgumentException("bad type");
		return r;
	}
	
	/// <summary>
	/// Returns <b>Length</b>, or 0 if <c>null</c>.
	/// </summary>
	internal static int Lenn_<T>(this T[] t) => t?.Length ?? 0;
	//internal static int Lenn_(this System.Collections.ICollection t) => t?.Count ?? 0; //slower, as well as Array
	
	/// <summary>
	/// Returns <b>Count</b>, or 0 if <c>null</c>.
	/// </summary>
	internal static int Lenn_<T>(this List<T> t) => t?.Count ?? 0;
	
	/// <summary>
	/// Returns <c>true</c> if <c>null</c> or <b>Length</b> == 0.
	/// </summary>
	internal static bool NE_<T>(this T[] t) => (t?.Length ?? 0) == 0;
	
	/// <summary>
	/// Returns <c>true</c> if <c>null</c> or <b>Count</b> == 0.
	/// </summary>
	internal static bool NE_<T>(this List<T> t) => (t?.Count ?? 0) == 0;
	
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
	/// If <i>s</i> is <c>null</c> or <c>""</c>, does nothing.
	/// If this is not empty, appends space.
	/// If <i>s</i> starts with a lowercase character, makes it uppercase, unless this ends with a character other than <c>'.'</c>.
	/// Appends <c>'.'</c> if <i>s</i> does not end with <c>'.'</c>, <c>';'</c>, <c>':'</c>, <c>','</c>, <c>'!'</c> or <c>'?'</c>.
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
	/// <param name="t">A <b>Control</b> or <b>Form</b> etc. Cannot be <c>null</c>.</param>
	/// <param name="create">
	/// Create handle if still not created. Default <c>false</c> (return <c>default(wnd)</c>).
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
	/// Calls <see cref="Graphics.DrawRectangle"/> with arguments corrected so that it draws inside or outside <i>r</i>. Does not use <see cref="System.Drawing.Drawing2D.PenAlignment"/>, it is unreliable.
	/// </remarks>
	public static void DrawRectangleInset(this Graphics t, Pen pen, RECT r, bool outset = false) {
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
	public static void DrawRectangleInset(this Graphics t, Color penColor, int penWidth, RECT r, bool outset = false) {
		using var pen = new Pen(penColor, penWidth);
		DrawRectangleInset(t, pen, r, outset);
	}
	
	/// <summary>
	/// Creates solid brush and calls <see cref="Graphics.FillRectangle"/>.
	/// </summary>
	public static void FillRectangle(this Graphics t, Color color, RECT r) {
		using var brush = new SolidBrush(color);
		t.FillRectangle(brush, r);
	}
	
	/// <summary>
	/// Calls <c>b.LockBits</c> in ctor and <c>b.UnlockBits</c> in <b>Dispose</b>.
	/// </summary>
	internal struct BitmapData_ : IDisposable {
		Bitmap _b;
		BitmapData _d;
		
		public BitmapData_(Bitmap b, ImageLockMode mode, PixelFormat? pf = null) {
			_b = b;
			_d = _b.LockBits(new(default, b.Size), mode, pf ?? _b.PixelFormat);
		}
		
		public BitmapData_(Bitmap b, Rectangle r, ImageLockMode mode, PixelFormat? pf = null) {
			_b = b;
			_d = _b.LockBits(r, mode, pf ?? _b.PixelFormat);
		}
		
		public void Dispose() {
			_b?.UnlockBits(_d);
			_b = null;
			_d = null;
		}
		
		public int Width => _d.Width;
		public int Height => _d.Height;
		public int Stride => _d.Stride;
		public PixelFormat PixelFormat => _d.PixelFormat;
		public IntPtr Scan0 => _d.Scan0;
	}
	
	/// <summary>
	/// Creates a <b>BitmapData_</b> object that calls <c>b.LockBits</c> in ctor and <c>b.UnlockBits</c> in <b>Dispose</b>.
	/// </summary>
	/// <param name="pf">If <c>null</c>, uses <c>b.PixelFormat</c>.</param>
	internal static BitmapData_ Data(this Bitmap b, ImageLockMode mode, PixelFormat? pf = null)
		=> new BitmapData_(b, mode, pf);
	
	/// <summary>
	/// Creates a <b>BitmapData_</b> object that calls <c>b.LockBits</c> in ctor and <c>b.UnlockBits</c> in <b>Dispose</b>.
	/// </summary>
	/// <param name="pf">If <c>null</c>, uses <c>b.PixelFormat</c>.</param>
	internal static BitmapData_ Data(this Bitmap b, Rectangle r, ImageLockMode mode, PixelFormat? pf = null)
		=> new BitmapData_(b, r, mode, pf);
	
	#endregion
	
	#region other
	
	/// <summary>
	/// Gets a value from a subkey of this registry key.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="subkey">The name or relative path of the subkey.</param>
	/// <param name="name">The name of the value to retrieve.</param>
	/// <param name="defaultValue">The value to return if <i>subkey</i> or <i>name</i> does not exist.</param>
	/// <param name="options"></param>
	/// <returns>The value associated with <i>name</i>, or <i>defaultValue</i> if <i>subkey</i> or <i>name</i> not found.</returns>
	/// <exception cref="Exception">Exceptions of <see cref="RegistryKey.OpenSubKey(string)"/> and <see cref="RegistryKey.GetValue(string?, object?, RegistryValueOptions)"/>.</exception>
	/// <remarks>
	/// Calls <see cref="RegistryKey.OpenSubKey(string)"/> and <see cref="RegistryKey.GetValue(string?, object?, RegistryValueOptions)"/>.
	/// </remarks>
	public static object GetValue2(this RegistryKey t, string subkey, string name, object defaultValue = null, RegistryValueOptions options = default) {
		using var k = t.OpenSubKey(subkey);
		if (k == null) return defaultValue;
		return k.GetValue(name, defaultValue, options);
		
		//tested: RegGetValue same speed.
	}
	
	#endregion
}
