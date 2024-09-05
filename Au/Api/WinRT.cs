//WinRT common API and utils. Others are in files where used.

//This library does not use C#/WinRT (Microsoft.Windows.SDK.NET.dll and WinRT.Runtime.dll).
//Would be easier, but it has problems:
//	- Adds 2 files, 22 MB.
//	- OCR: if once used in a STA thread, then does not work in MTA threads.
//	- OCR: slower.

namespace Au.Types;

#pragma warning disable 649, 169 //field never assigned/used
static unsafe partial class WinRT {
	[DllImport("combase.dll", PreserveSig = true)]
	internal static extern int WindowsCreateString(string s, int length, out IntPtr hstring);
	
	[DllImport("combase.dll", PreserveSig = true)]
	internal static extern int WindowsDeleteString(IntPtr hstring);
	
	[DllImport("combase.dll")]
	internal static extern char* WindowsGetStringRawBuffer(IntPtr hstring, out int length);
	
	//probably don't need. Always returns 1 (already initialized) when called with current apartment state.
	//[DllImport("combase.dll", PreserveSig = true)]
	//internal static extern int RoInitialize(int initType);
	
	[DllImport("combase.dll", PreserveSig = true)]
	internal static extern int RoGetActivationFactory(IntPtr activatableClassId, in Guid iid, out IntPtr factory);
	
	internal static T Create<T>(string progId) where T : struct {
		using var hs = new _Hstring(progId);
		HR(RoGetActivationFactory(hs, typeof(T).GUID, out var r));
		return Unsafe.As<IntPtr, T>(ref r);
	}
	//rejected: use static factories. Now fast.
	
	internal static void HR(int hr) {
		if (hr < 0) throw Marshal.GetExceptionForHR(hr);
	}
	
	internal static T As<T>(IntPtr ip) where T : unmanaged {
		Debug.Assert(sizeof(T) == sizeof(IntPtr));
		return Unsafe.As<IntPtr, T>(ref ip);
	}
	
	/// <summary>
	/// A COM interface pointer.
	/// </summary>
	internal struct IUnknown : IDisposable {
		IntPtr _u;
		
		IUnknown(IntPtr iunknown) { _u = iunknown; }
		
		public void Dispose() {
			if (_u != default) { Marshal.Release(_u); _u = default; }
		}

		public bool IsNull => _u == default;
		
		int _QI(Type type, out IntPtr r) {
#if NET9_0_OR_GREATER //changed ref -> in
			return Marshal.QueryInterface(_u, type.GUID, out r);
#else
			var guid = type.GUID;
			return Marshal.QueryInterface(_u, ref guid, out r);
#endif
		}

		/// <summary>
		/// Calls <b>QueryInterface</b>. Throws exception if failed.
		/// </summary>
		public T QI<T>() where T : unmanaged {
			HR(_QI(typeof(T), out var r));
			return As<T>(r);
		}
		
		/// <summary>
		/// Calls <b>QueryInterface</b>. Returns <c>false</c> if failed.
		/// </summary>
		public bool QI<T>(out T r) where T : unmanaged {
			bool ok = 0 == _QI(typeof(T), out var v);
			r = ok ? As<T>(v) : default;
			return ok;
		}
		
		/// <summary>
		/// Gets COM interface function at index <i>i</i> in vtbl.
		/// </summary>
		public nint this[int i] => (*(nint**)_u)[i];
		
		public static implicit operator IUnknown(IntPtr p) => new(p);
		
		public static implicit operator IntPtr(IUnknown p) => p._u;
		
		public override string ToString() => _u.ToString();
		
		/// <summary>
		/// Calls a 0-param function that returns <b>HSTRING</b>.
		/// </summary>
		/// <param name="i">Interface function index in vtbl.</param>
		public string GetString(int i) {
			using var s1 = new _Hstring(_GetPtr(i));
			return s1.ToString();
		}
		
		IntPtr _GetPtr(int i) {
			HR(((delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)this[i])(_u, out var r));
			return r;
		}
		
		/// <summary>
		/// Calls a 0-param function with a pointer-size return type (COM pointer, etc).
		/// </summary>
		/// <param name="i">Interface function index in vtbl.</param>
		public T GetPtr<T>(int i) where T : unmanaged => As<T>(_GetPtr(i));
		
		//when calling: System.BadImageFormatException: Bad element type in SizeOf.
		//public IntPtr GetPtr<T1>(int i, T1 p1) where T1 : unmanaged {
		//	HR(((delegate* unmanaged[Stdcall]<IntPtr, T1, out IntPtr, int>)this[i])(_u, p1, out var r));
		//	return r;
		//}
		
		/// <summary>
		/// <c>QI(IClosable).Close()</c>
		/// </summary>
		public void Close() {
			using var c = QI<IClosable>();
			c.Close();
		}
	}
	
	internal interface IComPtr : IDisposable {  }
	
	internal struct IVectorView<T> : IComPtr where T : unmanaged {
		IUnknown _u; public IUnknown U => _u;
		public void Dispose() => _u.Dispose();
		
		IntPtr _GetAt(int i) {
			HR(((delegate* unmanaged[Stdcall]<IntPtr, int, out IntPtr, int>)_u[6])(_u, i, out var r));
			return r;
		}
		
		public T this[int i] => As<T>(_GetAt(i));
		
		public int Size {
			get {
				HR(((delegate* unmanaged[Stdcall]<IntPtr, out int, int>)_u[7])(_u, out int r));
				return r;
			}
		}
		
		public IEnumerable<T> Items(bool disposeItems = true) {
			for (int i = 0, n = Size; i < n; i++) {
				var v = _GetAt(i);
				yield return As<T>(v);
				if (disposeItems) Marshal.Release(v);
			}
		}
	}
	
#if true
	internal struct IAsyncOperation : IComPtr {
		IUnknown _u; public IUnknown U => _u;
		public void Dispose() => _u.Dispose();
		
		public T Await<T>(bool dispose = true) where T : unmanaged {
			try {
				using (var ai = _u.QI<IAsyncInfo>()) {
					for (int i = 4; ; i++) {
						var status = ai.Status;
						//print.it(status, i / 4);
						if (status == AsyncStatus.Completed) break;
						if (status != AsyncStatus.Started) throw new AuException();
						wait.ms(i / 4);
					}
				}
				
				return _u.GetPtr<T>(8); //GetResults
			}
			finally { if (dispose) Dispose(); }
		}
		
		[Guid("00000036-0000-0000-C000-000000000046")]
		struct IAsyncInfo : IComPtr {
		IUnknown _u; public IUnknown U => _u;
		public void Dispose() => _u.Dispose();
			
			public AsyncStatus Status {
				get {
					HR(((delegate* unmanaged[Stdcall]<IntPtr, out AsyncStatus, int>)_u[7])(_u, out var r));
					return r;
				}
			}
		}
		
		enum AsyncStatus { Canceled = 2, Completed = 1, Error = 3, Started = 0 }
	}
#else //tried to use Completed callback, unsuccessfully
		internal struct IAsyncOperation : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();
			
			public T Await<T>(bool dispose = true) where T : unmanaged {
				try {
					//static void _Handler(IAsyncOperation ai, AsyncStatus status) { print.it("completed"); }
					//delegate*<IntPtr, int, void> p1 = &_Handler;
					//HR(((delegate* unmanaged[Stdcall]<IntPtr, void*, int>)_u[6])(_u, p1)); //put_Completed
					
					var del = new AsyncOperationCompletedHandler(static (IAsyncOperation ai, AsyncStatus status) => { print.it("completed"); });
					HR(((delegate* unmanaged[Stdcall]<IntPtr, AsyncOperationCompletedHandler, int>)_u[6])(_u, del)); //put_Completed
					
					dialog.show("");
					GC.KeepAlive(del);
					
					return _u.GetPtr<T>(8); //GetResults
				}
				finally { if (dispose) Dispose(); }
			}
			
			enum AsyncStatus { Canceled = 2, Completed = 1, Error = 3, Started = 0 }
			
			delegate void AsyncOperationCompletedHandler(IAsyncOperation ao, AsyncStatus status);
		}
#endif
	
	[Guid("30d5a829-7fa4-4026-83bb-d75bae4ea99e")]
	internal struct IClosable : IComPtr {
		IUnknown _u; public IUnknown U => _u;
		public void Dispose() => _u.Dispose();
		
		public void Close() {
			HR(((delegate* unmanaged[Stdcall]<IntPtr, int>)_u[6])(_u));
		}
	}
	
	unsafe class _Hstring : IDisposable {
		IntPtr _h;
		
		public _Hstring(IntPtr hstring) {
			_h = hstring;
		}
		
		public _Hstring(string s) {
			WindowsCreateString(s, s.Lenn(), out _h);
		}
		
		public void Dispose() {
			if (_h != default) {
				WindowsDeleteString(_h);
				_h = default;
			}
			GC.SuppressFinalize(this);
		}
		
		~_Hstring() {
			if (_h != default) WindowsDeleteString(_h);
		}
		
		//public static implicit operator _Hstring(string s) => new(s);
		public static implicit operator IntPtr(_Hstring s) => s._h;
		public static implicit operator string(_Hstring s) => s.ToString();
		
		public override string ToString() {
			if (_h == default) return null;
			char* p = WindowsGetStringRawBuffer(_h, out int len);
			return new(p, 0, len);
		}
	}
}
