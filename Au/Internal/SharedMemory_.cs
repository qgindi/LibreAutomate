namespace Au.More;

/// <summary>
/// Memory shared by all processes using this library.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = c_size)]
unsafe struct SharedMemory_ {
	#region variables used by our library classes
	//Declare variables used by our library classes.
	//Be careful:
	//1. Some type sizes are different in 32 and 64 bit process.
	//	Solution: Use long and cast to IntPtr etc. For wnd use int.
	//2. The memory may be used by processes that use different library versions.
	//	Solution: In new library versions don't change struct sizes and old members.
	//		Maybe reserve some space for future members. If need more, add new struct.
	//		Use eg [StructLayout(LayoutKind.Sequential, Size = 16)].

	//reserve 16 for some header, eg shared memory version.
	[StructLayout(LayoutKind.Sequential, Size = 16)] struct _Header { }
	_Header _h;

	internal PrintServer.SharedMemoryData_ outp;
	internal Triggers.ActionTriggers.SharedMemoryData_ triggers;
	internal WindowsHook.SharedMemoryData_ winHook;
	internal perf.Instance perf;
	internal script.SharedMemoryData_ script;
	//internal ScriptEditor.SharedMemoryData_ editor;

	//public const int TasksDataSize_ = 0x4000;
	//internal struct TasksData_ { public int size; public fixed byte data[TasksDataSize_]; }
	//internal TasksData_ tasks;

	#endregion

	const int c_size = 0x200000; //2 MB

	static SharedMemory_() {
		Ptr = (SharedMemory_*)Mapping.CreateOrOpen("Au-memory-lib", c_size).Mem;
	}

	/// <summary>
	/// Pointer to the shared memory.
	/// </summary>
	public static readonly SharedMemory_* Ptr;

	/// <summary>
	/// Gets pointer to the shared memory "return data" buffer.
	/// Used by <see cref="WndCopyData.Return"/>.
	/// </summary>
	public static byte* ReturnDataPtr => (byte*)Ptr + c_size / 2;

	/// <summary>
	/// Size of <see cref="ReturnDataPtr"/> buffer, 1 MB.
	/// </summary>
	public const int ReturnDataSize = c_size / 2;

	/// <summary>
	/// Shared memory pointer and mapping handle.
	/// </summary>
	public struct Mapping : IDisposable {
		IntPtr _hMapping;
		void* _mem;

		public void* Mem => _mem;

		/// <summary>
		/// Created new memory. If <c>false</c> - opened existing.
		/// </summary>
		public bool Created { get; }

		internal Mapping(IntPtr h, void* m, bool created) {
			_hMapping = h;
			_mem = m;
			Created = created;
		}

		public void Dispose() {
			if (_mem != null) { Api.UnmapViewOfFile(_mem); _mem = null; }
			if (_hMapping != default) { Api.CloseHandle(_hMapping); _hMapping = default; }
		}

		/// <summary>
		/// Creates named shared memory of specified size. Opens if already exists.
		/// Returns <b>Mapping</b> variable that contains shared memory address in this process.
		/// </summary>
		/// <param name="name">Shared memory name. Case-insensitive.</param>
		/// <param name="size">Shared memory size. Ignored if the shared memory already exists.</param>
		/// <exception cref="AuException">The API failed.</exception>
		/// <remarks>
		/// Calls API <msdn>CreateFileMapping</msdn> and API <msdn>MapViewOfFile</msdn>.
		/// The shared memory is alive at least until this process ends or the returned <b>Mapping</b> variable disposed. Other processes can keep the memory alive even after that.
		/// </remarks>
		public static Mapping CreateOrOpen(string name, int size) {
			//CONSIDER: don't use Api.SECURITY_ATTRIBUTES.ForLowIL. Speed with it 2.7 ms, without 1.7 ms.
			//	But then cannot use this library in low IL processes. Probably never used anyway.
			//	Try to move everything to the cpp dll.

			var hm = Api.CreateFileMapping((IntPtr)~0, Api.SECURITY_ATTRIBUTES.ForLowIL, Api.PAGE_READWRITE, 0, (uint)size, name);
			if (!hm.Is0) {
				bool created = lastError.code != Api.ERROR_ALREADY_EXISTS;
				var mem = Api.MapViewOfFile(hm, 0x000F001F, 0, 0, 0);
				if (mem != default) return new(hm, mem, created);
				hm.Dispose();
			}
			throw new AuException(0, "*open shared memory");
		}
	}
}
