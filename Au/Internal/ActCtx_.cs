namespace Au.More;

/// <summary>
/// Activates our manifest which tells to use <c>comctl32.dll</c> version 6.
/// The manifest is embedded in this dll, resource id 2.
/// </summary>
internal sealed class ActCtx_ : IDisposable {
	IntPtr _cookie;
	
	public static ActCtx_ Activate() {
		if (s_actCtx.Handle == default || !Api.ActivateActCtx(s_actCtx.Handle, out var cookie)) return default;
		return new ActCtx_() { _cookie = cookie };
	}
	
	public void Dispose() {
		if (_cookie != default) {
			Api.DeactivateActCtx(0, _cookie);
			_cookie = default;
		}
	}
	
	static _ActCtx s_actCtx = new _ActCtx();
	
	class _ActCtx {
		public IntPtr Handle;
		
		public _ActCtx() {
			Api.ACTCTX a = default;
			a.cbSize = Api.SizeOf<Api.ACTCTX>();
#if true //the manifest is in AuCpp.dll
			a.dwFlags = Api.ACTCTX_FLAG_RESOURCE_NAME_VALID | Api.ACTCTX_FLAG_HMODULE_VALID;
			a.hModule = Cpp.Cpp_ModuleHandle();
#else //old code. The manifest was in Au.dll. If <PublishSingleFile>, Location returns "".
				a.dwFlags = Api.ACTCTX_FLAG_RESOURCE_NAME_VALID;
				a.lpSource = Assembly.GetExecutingAssembly().Location;
#endif
			a.lpResourceName = (IntPtr)2;
			
			var h = Api.CreateActCtx(a);
			if (h != (IntPtr)(-1)) Handle = h;
		}
		
		~_ActCtx() {
			if (Handle != default) {
				Api.ReleaseActCtx(Handle);
				Handle = default;
			}
		}
	}
}
