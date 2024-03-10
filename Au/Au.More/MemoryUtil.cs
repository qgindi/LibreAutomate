namespace Au.More {
	/// <summary>
	/// Allocates memory from native heap of this process using heap API.
	/// Also has more functions to work with memory: copy, move, virtual alloc.
	/// </summary>
	/// <remarks>
	/// Uses the common heap of this process, API <msdn>GetProcessHeap</msdn>.
	/// </remarks>
	public static unsafe class MemoryUtil {
		static IntPtr _processHeap = Api.GetProcessHeap();
		
		/// <summary>
		/// Allocates new memory block and returns its address.
		/// </summary>
		/// <param name="size">Byte count.</param>
		/// <param name="zeroInit">Set all bytes = 0.</param>
		/// <exception cref="OutOfMemoryException">Failed. Probably <i>size</i> is too big.</exception>
		/// <remarks>
		/// Calls API <msdn>HeapAlloc</msdn>.
		/// The memory is unmanaged and will not be freed automatically. Always call <see cref="Free"/> when done. Call <see cref="ReAlloc"/> or <see cref="FreeAlloc"/> if need to resize.
		/// </remarks>
		public static byte* Alloc(nint size, bool zeroInit = false)
			=> _ReAllocBytes(null, size, zeroInit);
		
		/// <param name="count">Count of elements of type <b>T</b>.</param>
		/// <inheritdoc cref="Alloc(nint, bool)"/>
		public static T* Alloc<T>(nint count, bool zeroInit = false) where T : unmanaged
			=> (T*)_ReAllocBytes(null, count * sizeof(T), zeroInit);
		
		//Rejected. With the above overload the calling code is easier to read. Not so often used.
		//public static void Alloc<T>(out T* mem, nint count, bool zeroInit = false) where T : unmanaged
		//	=> mem = (T*)_ReAllocBytes(null, count * sizeof(T), zeroInit);
		
		static byte* _ReAllocBytes(void* mem, nint size, bool zeroInit = false) {
			uint flag = zeroInit ? 8u : 0u;
			for (int i = 0; i < 5; i++) {
				if (i > 0) {
					GC.Collect();
					GC.WaitForPendingFinalizers();
					Thread.Sleep(i * 100);
				}
				void* r;
				if (mem == null) r = Api.HeapAlloc(_processHeap, flag, size);
				else r = Api.HeapReAlloc(_processHeap, flag, mem, size);
				if (r != null) return (byte*)r;
			}
			throw new OutOfMemoryException();
			
			//note: don't need GC.AddMemoryPressure.
			//	Native memory usually is used for temporary buffers etc and is soon released eg with try/finally.
			//	Marshal.AllocHGlobal does not do it too.
		}
		
		/// <summary>
		/// Reallocates a memory block to make it bigger or smaller.
		/// </summary>
		/// <param name="mem">Input: old memory address; if <c>null</c>, allocates new memory like <see cref="Alloc{T}"/>. Output: new memory address. Unchanged if exception.</param>
		/// <param name="count">New count of elements of type <b>T</b>.</param>
		/// <param name="zeroInit">When size is growing, set all added bytes = 0.</param>
		/// <exception cref="OutOfMemoryException">Failed. Probably <i>count</i> is too big.</exception>
		/// <remarks>
		/// Calls API <msdn>HeapReAlloc</msdn> or <msdn>HeapAlloc</msdn>.
		/// Preserves data in <c>Math.Min(oldCount, newCount)</c> elements of old memory (copies from old memory if need).
		/// The memory is unmanaged and will not be freed automatically. Always call <see cref="Free"/> when done. Call <b>ReAlloc</b> or <see cref="FreeAlloc"/> if need to resize.
		/// </remarks>
		public static void ReAlloc<T>(ref T* mem, nint count, bool zeroInit = false) where T : unmanaged
			=> mem = (T*)_ReAllocBytes(mem, count * sizeof(T), zeroInit);
		
		/// <summary>
		/// Frees a memory block.
		/// Does nothing if <i>mem</i> is <c>null</c>.
		/// </summary>
		/// <remarks>
		/// Calls API <msdn>HeapFree</msdn>.
		/// </remarks>
		public static void Free(void* mem) {
			if (mem != null) Api.HeapFree(_processHeap, 0, mem);
		}
		
		/// <summary>
		/// Frees a memory block (if not <c>null</c>) and allocates new.
		/// </summary>
		/// <param name="mem">Input: old memory address or <c>null</c>. Output: new memory address; <c>null</c> if exception (it prevents freeing twice).</param>
		/// <param name="count">New count of elements of type <b>T</b>.</param>
		/// <param name="zeroInit">Set all bytes = 0.</param>
		/// <exception cref="OutOfMemoryException">Failed. Probably <i>count</i> is too big.</exception>
		/// <remarks>
		/// At first sets <i>mem</i> = <c>null</c>, to avoid double <b>Free</b> if this function throws exception. Then calls <b>Free</b> and <b>Alloc</b>.
		/// </remarks>
		public static void FreeAlloc<T>(ref T* mem, nint count, bool zeroInit = false) where T : unmanaged {
			var m = mem; mem = null;
			Free(m);
			mem = Alloc<T>(count, zeroInit);
		}
		
		/// <summary>
		/// Allocates new virtual memory block with API <msdn>VirtualAlloc</msdn> and returns its address: <c>VirtualAlloc(default, size, MEM_COMMIT|MEM_RESERVE, PAGE_READWRITE)</c>.
		/// </summary>
		/// <param name="size">Byte count.</param>
		/// <exception cref="OutOfMemoryException">Failed. Probably <i>size</i> is too big.</exception>
		/// <remarks>
		/// Faster than managed and <see cref="Alloc"/> when memory size is large, more than 1 MB; else slower.
		/// The memory is initialized to zero (all bytes 0).
		/// </remarks>
		public static byte* VirtualAlloc(nint size) {
			for (int i = 0; i < 5; i++) {
				if (i > 0) {
					GC.Collect();
					GC.WaitForPendingFinalizers();
					Thread.Sleep(i * 100);
				}
				var r = (byte*)Api.VirtualAlloc(default, size, Api.MEM_COMMIT | Api.MEM_RESERVE, Api.PAGE_READWRITE);
				if (r != null) return r;
			}
			throw new OutOfMemoryException();
			
			//note: don't need GC.AddMemoryPressure.
			//	Native memory usually is used for temporary buffers etc and is soon released eg with try/finally.
			//	Marshal.AllocHGlobal does not do it too.
		}
		
		/// <summary>
		/// Frees a memory block allocated with <see cref="VirtualAlloc"/>.
		/// Does nothing if <i>mem</i> is <c>null</c>.
		/// </summary>
		public static void VirtualFree(void* mem) {
			if (mem != null) Api.VirtualFree(mem);
		}
		
		/// <summary>
		/// Copies memory with <see cref="Buffer.MemoryCopy"/>.
		/// </summary>
		/// <remarks>
		/// If some part of memory blocks overlaps, this function is much slower than <see cref="Move"/>. Else same speed or slightly faster.
		/// </remarks>
		public static void Copy(void* from, void* to, nint size) => Buffer.MemoryCopy(from, to, size, size);
		//speed Buffer.MemoryCopy vs memcpy, non-overlapped: same if small, slightly faster if big.
		//speed Span.CopyTo vs Buffer.MemoryCopy: same if non-overlapped, slower if overlapped.
		
		/// <summary>
		/// Copies memory with API <msdn>memmove</msdn>.
		/// </summary>
		/// <remarks>
		/// If some part of memory blocks overlaps, this function is much faster than <see cref="Copy"/>. Else same speed or slightly slower.
		/// </remarks>
		public static void Move(void* from, void* to, nint size) => Api.memmove(to, from, size);
	}
}
