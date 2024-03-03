namespace Au.More {
	/// <summary>
	/// Allocates, writes and reads memory in other process.
	/// </summary>
	/// <remarks>
	/// Must be disposed. Example: <c>using(var pm=new ProcessMemory(...)) { ... }</c>.
	/// </remarks>
	public unsafe class ProcessMemory : IDisposable {
		Handle_ _hproc;
		HandleRef _HprocHR => new(this, _hproc);

		///
		protected virtual void Dispose(bool disposing) {
			if (_hproc.Is0) return;
			if (MemAllocated != default) {
				var mem = MemAllocated; MemAllocated = default;
				if (!Api.VirtualFreeEx(_HprocHR, mem)) print.warning("Failed to free process memory. " + lastError.message);
			}
			Mem = default;
			_hproc.Dispose();
		}

		///
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		///
		~ProcessMemory() => Dispose(false);

		/// <summary>
		/// Process handle.
		/// Opened with access <c>PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE</c>.
		/// </summary>
		public IntPtr ProcessHandle => _hproc;

		/// <summary>
		/// Address in that process used by the read and write functions.
		/// </summary>
		/// <remarks>
		/// Most read/write functions of this class don't have a parameter "address in that process". Instead they use <b>Mem</b>, which initially is == <see cref="MemAllocated"/>. But you can set <b>Mem</b> = any valid address in that process; usually you do it when no memory is allocated by the constructor (<i>nBytes</i> 0).
		/// The address is invalid in this process.
		/// </remarks>
		public IntPtr Mem { get; set; }

		/// <summary>
		/// Address of memory allocated in that process.
		/// </summary>
		/// <remarks>
		/// The constructor allocates memory with API <msdn>VirtualAllocEx</msdn> if <i>nBytes</i> != 0. Finally <b>Dispose</b> will free it with API <msdn>VirtualFreeEx</msdn>.
		/// The setter normally isn't used; if you set <c>MemAllocated = default</c>, <b>Dispose</b> will not free the memory.
		/// The address is invalid in this process.
		/// </remarks>
		public IntPtr MemAllocated { get; set; }

		void _Alloc(int pid, wnd w, int nBytes, bool noException) {
			string err;
			const uint fl = Api.PROCESS_VM_OPERATION | Api.PROCESS_VM_READ | Api.PROCESS_VM_WRITE;
			_hproc = w.Is0 ? Handle_.OpenProcess(pid, fl) : Handle_.OpenProcess(w, fl);
			if (_hproc.Is0) { err = "Failed to open process handle."; goto ge; }

			if (nBytes != 0) {
				Mem = MemAllocated = Api.VirtualAllocEx(_HprocHR, default, nBytes);
				if (MemAllocated == default) { err = "Failed to allocate process memory."; goto ge; }
			}
			return;
		ge:
			if (noException) Dispose();
			else {
				var e = new AuException(0, err);
				Dispose();
				throw e;
			}
		}

		/// <summary>
		/// Opens process handle and optionally allocates memory in that process.
		/// </summary>
		/// <param name="processId">Process id.</param>
		/// <param name="nBytes">If not 0, allocates memory of this size in that process.</param>
		/// <param name="noException">Don't throw exceptions. If failed, sets <see cref="ProcessHandle"/> = default.</param>
		/// <exception cref="AuException">Failed to open process handle (usually because of [](xref:uac)) or allocate memory.</exception>
		public ProcessMemory(int processId, int nBytes, bool noException = false) {
			_Alloc(processId, default, nBytes, noException);
		}

		/// <summary>
		/// Opens process handle and optionally allocates memory in that process.
		/// </summary>
		/// <param name="w">A window of that process.</param>
		/// <param name="nBytes">If not 0, allocates memory of this size in that process.</param>
		/// <param name="noException">Don't throw exceptions. If failed, sets <see cref="ProcessHandle"/> = default.</param>
		/// <exception cref="AuWndException"><i>w</i> invalid.</exception>
		/// <exception cref="AuException">Failed to open process handle or allocate memory.</exception>
		public ProcessMemory(wnd w, int nBytes, bool noException = false) {
			if (!noException) w.ThrowIfInvalid();
			_Alloc(0, w, nBytes, noException);
		}

		/// <summary>
		/// Copies a string from this process to that process (memory address <see cref="Mem"/>).
		/// In that process writes the string as <c>'\0'</c>-terminated <b>char</b> string (UTF-16).
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		/// <param name="s">A string in this process.</param>
		/// <param name="offsetBytes">Offset in <see cref="Mem"/>.</param>
		/// <remarks>
		/// In that process is used <c>(s.Length+1)*2</c> bytes of memory (+1 for the <c>'\0'</c>, *2 because UTF-16 character size is 2 bytes).
		/// </remarks>
		public bool WriteCharString(string s, int offsetBytes = 0) {
			if (Mem == default) return false;
			if (s.NE()) return true;
			fixed (char* p = s) {
				return Api.WriteProcessMemory(_HprocHR, Mem + offsetBytes, p, (s.Length + 1) * 2, null);
			}
		}

		/// <summary>
		/// Copies a string from this process to that process (memory address <see cref="Mem"/>).
		/// In that process writes the string as <c>'\0'</c>-terminated <b>byte</b> string.
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		/// <param name="s">A string in this process. Normal C# string (UTF-16).</param>
		/// <param name="offsetBytes">Offset in <see cref="Mem"/>.</param>
		/// <param name="enc">Encoding for converting <b>char</b> string to <b>byte</b> string. If <c>null</c>, uses <see cref="Encoding.Default"/> (UTF-8).</param>
		public bool WriteByteString(string s, int offsetBytes = 0, Encoding enc = null) {
			if (Mem == default) return false;
			if (s.NE()) return true;
			enc ??= Encoding.Default;
			var a = enc.GetBytes(s).InsertAt(-1);
			fixed (byte* p = a) {
				return Api.WriteProcessMemory(_HprocHR, Mem + offsetBytes, p, a.Length, null);
			}
		}

		[SkipLocalsInit]
		string _ReadString(bool ansiString, int nChars, int offsetBytes, bool findLength, Encoding enc = null) {
			if (Mem == default) return null;
			int n = nChars + 1; if (!ansiString) n *= 2; //bytes with '\0'
			using FastBuffer<byte> b = new(n);
			if (!Api.ReadProcessMemory(_HprocHR, Mem + offsetBytes, b.p, n, null)) return null;
			if (findLength) nChars = ansiString ? BytePtr_.Length(b.p, nChars) : CharPtr_.Length((char*)b.p, nChars);
			enc ??= Encoding.Default;
			return ansiString ? new((sbyte*)b.p, 0, nChars, enc) : new((char*)b.p, 0, nChars);
		}

		/// <summary>
		/// Copies a <b>char</b> string from that process (memory address <see cref="Mem"/>) to this process.
		/// In that process the string must be in Unicode UTF-16 format.
		/// </summary>
		/// <returns>The copied string, or <c>null</c> if failed.</returns>
		/// <param name="length">Number of characters to copy, not including the terminating <c>'\0'</c>. In both processes a character is 2 bytes.</param>
		/// <param name="offsetBytes">Offset in <see cref="Mem"/>.</param>
		/// <param name="findLength">Find string length by searching for <c>'\0'</c> character in <i>length</i> range. If <c>false</c>, the returned string is of <i>length</i> length even if contains <c>'\0'</c> characters.</param>
		public string ReadCharString(int length, int offsetBytes = 0, bool findLength = false) {
			return _ReadString(false, length, offsetBytes, findLength);
		}

		/// <summary>
		/// Copies a <b>byte</b> string from that process (memory address <see cref="Mem"/>) to this process.
		/// In that process the string must be array of bytes (not Unicode UTF-16).
		/// </summary>
		/// <returns>The copied string, or <c>null</c> if failed.</returns>
		/// <param name="length">Number bytes to copy, not including the terminating <c>'\0'</c>. In that process a character is 1 or more bytes (depending on encoding). In this process will be 2 bytes (normal C# string).</param>
		/// <param name="offsetBytes">Offset in <see cref="Mem"/>.</param>
		/// <param name="findLength">Find string length by searching for <c>'\0'</c> character in <i>length</i> range.</param>
		/// <param name="enc">Encoding for converting <b>byte</b> string to <b>char</b> string. If <c>null</c>, uses <see cref="Encoding.Default"/> (UTF-8).</param>
		public string ReadByteString(int length, int offsetBytes = 0, bool findLength = false, Encoding enc = null) {
			return _ReadString(true, length, offsetBytes, findLength, enc);
		}

		/// <summary>
		/// Copies memory from this process to that process (memory address <see cref="Mem"/>).
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		/// <param name="ptrFrom">Address of memory in this process.</param>
		/// <param name="nBytes">Number of bytes to copy.</param>
		/// <param name="offsetBytes">Offset in <see cref="Mem"/>.</param>
		public bool Write(void* ptrFrom, int nBytes, int offsetBytes = 0) {
			if (Mem == default) return false;
			return Api.WriteProcessMemory(_HprocHR, Mem + offsetBytes, ptrFrom, nBytes, null);
		}

		/// <summary>
		/// Copies memory from this process to a known address in that process.
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		/// <param name="ptrTo">Address of memory in that process.</param>
		/// <param name="ptrFrom">Address of memory in this process.</param>
		/// <param name="nBytes">Number of bytes to copy.</param>
		public bool Write(IntPtr ptrTo, void* ptrFrom, int nBytes) {
			return Api.WriteProcessMemory(_HprocHR, ptrTo, ptrFrom, nBytes, null);
		}

		/// <summary>
		/// Copies memory from that process (memory address <see cref="Mem"/>) to this process.
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		/// <param name="ptrTo">Address of memory in this process.</param>
		/// <param name="nBytes">Number of bytes to copy.</param>
		/// <param name="offsetBytes">Offset in <see cref="Mem"/>.</param>
		public bool Read(void* ptrTo, int nBytes, int offsetBytes = 0) {
			if (Mem == default) return false;
			return Api.ReadProcessMemory(_HprocHR, Mem + offsetBytes, ptrTo, nBytes, null);
		}

		/// <summary>
		/// Copies memory from a known address in that process to this process.
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		/// <param name="ptrFrom">Address of memory in that process.</param>
		/// <param name="ptrTo">Address of memory in this process.</param>
		/// <param name="nBytes">Number of bytes to copy.</param>
		public bool Read(IntPtr ptrFrom, void* ptrTo, int nBytes) {
			return Api.ReadProcessMemory(_HprocHR, ptrFrom, ptrTo, nBytes, null);
		}
	}
}
