//CONSIDER: System.Buffers.ArrayPool<T>.

namespace Au.More;

/// <summary>
/// Like List or StringBuilder, used as a temporary variable-size array to create final fixed-size array.
/// To avoid much garbage (and many reallocations when growing), uses native memory heap. See <see cref="MemoryUtil"/>.
/// Must be explicitly disposed to free the native memory. Does not have a finalizer because is struct (to avoid garbage).
/// Does not support reference types. Does not call T.Dispose.
/// </summary>
//[DebuggerStepThrough]
internal unsafe struct ArrayBuilder_<T> : IDisposable where T : unmanaged {
	T* _p;
	int _len, _cap;
	
	static int s_minCap;
	
	static ArrayBuilder_() {
		var r = 16384 / sizeof(T); //above 16384 the memory allocation API become >=2 times slower
		if (r > 500) r = 500; else if (r < 8) r = 8;
		s_minCap = r;
		
		//info: 500 is optimal for getting all top-level windows (and invisible) as ArrayBuilder_<wnd>.
		//	Normally there are 200-400 windows on my PC, rarely > 500.
	}
	
	public void Dispose() => Free();
	
	/// <summary>
	/// Gets array memory address (address of element 0).
	/// </summary>
	public T* Ptr => _p;
	
	/// <summary>
	/// Gets the number of elements.
	/// </summary>
	public int Count => _len;
	
	/// <summary>
	/// Gets the number of bytes in the array (Count*sizeof(T)).
	/// </summary>
	public int ByteCount => _len * sizeof(T);
	
	/// <summary>
	/// Gets or sets the total number of elements (not bytes) the internal memory can hold without resizing.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">(the <c>set</c> function) value less than Count. Instead use ReAlloc or Free.</exception>
	public int Capacity {
		get => _cap;
		set {
			if (value != _cap) {
				if (value < _len) throw new ArgumentOutOfRangeException();
				MemoryUtil.ReAlloc(ref _p, value);
				_cap = value;
			}
		}
	}
	
	/// <summary>
	/// Allocates count elements. Sets Count=count.
	/// Frees previously allocated memory.
	/// Returns memory address (address of element 0).
	/// </summary>
	/// <param name="count">Element count.</param>
	/// <param name="zeroInit">Set all bytes = 0. If <c>false</c>, the memory is uninitialized, ie random byte values. Default <c>true</c>. Slower when <c>true</c>.</param>
	/// <param name="noExtra">Set Capacity = count. If <c>false</c>, allocates more if count is less thah the minimal capacity for this type.</param>
	public T* Alloc(int count, bool zeroInit = true, bool noExtra = false) {
		if (_cap != 0) Free();
		int cap = count; if (cap < s_minCap && !noExtra) cap = s_minCap;
		_p = MemoryUtil.Alloc<T>(cap, zeroInit);
		_cap = cap; _len = count;
		return _p;
	}
	
	/// <summary>
	/// Adds or removes elements at the end. Sets Count=count.
	/// Preserves Math.Min(Count, count) existing elements.
	/// Returns memory address (address of element 0).
	/// </summary>
	/// <param name="count">New element count.</param>
	/// <param name="zeroInit">Set all added bytes = 0. If <c>false</c>, the added memory is uninitialized, ie random byte values. Default <c>true</c>. Slower when <c>true</c>.</param>
	/// <param name="noExtra">Set Capacity = count. If <c>false</c>, allocates more if count is less thah the minimal capacity for this type.</param>
	/// <remarks>
	/// The new memory usually is at a new location. The preserved elements are copied there.
	/// Sets Count=count. To allocate more memory without changing Count, change Capacity instead.
	/// </remarks>
	public T* ReAlloc(int count, bool zeroInit = true, bool noExtra = false) {
		int cap = count; if (cap < s_minCap && !noExtra) cap = s_minCap;
		MemoryUtil.ReAlloc(ref _p, cap, zeroInit);
		_cap = cap; _len = count;
		return _p;
	}
	
	/// <summary>
	/// Frees memory. Sets Count and Capacity = 0.
	/// </summary>
	public void Free() {
		if (_cap == 0) return;
		_len = _cap = 0;
		var p = _p; _p = null;
		MemoryUtil.Free(p);
	}
	
	/// <summary>
	/// Adds one element.
	/// The same as Add, but uses <c>in</c>. Use to avoid copying values of big types.
	/// </summary>
	public void AddR(in T value) {
		if (_len == _cap) _EnsureCapacity();
		_p[_len++] = value;
	}
	
	/// <summary>
	/// Adds one element.
	/// </summary>
	public void Add(T value) {
		if (_len == _cap) _EnsureCapacity();
		_p[_len++] = value;
	}
	
	/// <summary>
	/// Adds one zero-inited element and returns its reference.
	/// </summary>
	public ref T Add() {
		if (_len == _cap) _EnsureCapacity();
		ref T r = ref _p[_len];
		r = default;
		_len++;
		return ref r;
	}
	
	/// <summary>
	/// Capacity = Math.Max(_cap * 2, s_minCap).
	/// </summary>
	void _EnsureCapacity() {
		Capacity = Math.Max(_cap * 2, s_minCap);
	}
	
	/// <summary>
	/// Gets element reference.
	/// </summary>
	/// <param name="i">Element index.</param>
	/// <exception cref="IndexOutOfRangeException"></exception>
	public ref T this[int i] {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			if ((uint)i >= (uint)_len) _ThrowBadIndex();
			return ref _p[i];
		}
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	static void _ThrowBadIndex() {
		throw new IndexOutOfRangeException();
	}
	
	/// <summary>
	/// Copies elements to a new managed array.
	/// </summary>
	public T[] ToArray() {
		if (_len == 0) return Array.Empty<T>();
		var r = new T[_len];
		for (int i = 0; i < r.Length; i++) r[i] = _p[i];
		return r;
	}
}
