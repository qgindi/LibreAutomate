namespace Au.More;

/// <summary>
/// Creates and manages native font handle.
/// </summary>
internal sealed class NativeFont_ : IDisposable {
	IntPtr _h;
	public IntPtr Handle => _h;

	public NativeFont_(IntPtr handle) { _h = handle; }

	public static implicit operator IntPtr(NativeFont_ f) => f?._h ?? default;

	~NativeFont_() { _Dispose(); }
	public void Dispose() { _Dispose(); GC.SuppressFinalize(this); }
	void _Dispose() {
		if (_h != default) { Api.DeleteObject(_h); _h = default; }
	}

	public NativeFont_(int dpi, string name, int height, bool bold = false, bool italic = false) {
		_h = Api.CreateFont(
			-Math2.MulDiv(height, dpi, 72),
			cWeight: bold ? 700 : 0, //FW_BOLD
			bItalic: italic ? 1 : 0,
			//bUnderline: underline ? 1 : 0,
			//bStrikeOut: strikeout ? 1 : 0,
			iCharSet: 1,
			pszFaceName: name);
	}

	public unsafe NativeFont_(int dpi, bool bold, bool italic, int height = 0) {
		Api.LOGFONT m = default;
		Dpi.SystemParametersInfo(Api.SPI_GETICONTITLELOGFONT, sizeof(Api.LOGFONT), &m, dpi);
		if (bold) m.lfWeight = 700;
		if (italic) m.lfItalic = 1;
		if (height != 0) m.lfHeight = -Math2.MulDiv(height, dpi, 72);
		_h = Api.CreateFontIndirect(m);
	}

	public int HeightOnScreen {
		get {
			if (_heightOnScreen == 0) {
				using var dc = new FontDC_(_h);
				_heightOnScreen = dc.MeasureEP("A").height;
			}
			return _heightOnScreen;
		}
	}
	int _heightOnScreen;

	//flags: loword dpi, 0x10000 bold, 0x20000 italic
	static unsafe NativeFont_ _CreateCached(int flags) {
		int dpi = flags & 0xffff;
		bool bold = 0 != (flags & 0x10000), italic = 0 != (flags & 0x20000);
		//if (0 != (flags & 0x100000)) return new NativeFont_(dpi, "Verdana", 9, bold, italic);
		return new NativeFont_(dpi, bold, italic);
	}

	static readonly ConcurrentDictionary<int, NativeFont_> _d = new();

	/// <summary>
	/// Cached standard font used by most windows and controls.
	/// On Windows 10 it is <c>"Segoe UI"</c> 9 by default.
	/// </summary>
	internal static NativeFont_ RegularCached(int dpi) => _d.GetOrAdd(dpi, i => _CreateCached(i));

	/// <summary>
	/// Cached standard bold font used by most windows and controls.
	/// </summary>
	internal static NativeFont_ BoldCached(int dpi) => _d.GetOrAdd(dpi | 0x10000, i => _CreateCached(i));
}

//currently not used
///// <summary>
///// Provides various versions of standard UI font.
///// Per-monitor DPI-aware.
///// </summary>
///// <remarks>
///// The properties return non-cached <b>Font</b> objects. It's safe to dispose them. It's OK to not dispose (GC will do; GDI+ fonts don't use much unmanaged memory).
///// </remarks>
//internal static class Fonts_
//{
//	//info: we don't return cached Font objects, because we cannot protect them from disposing. The Font class is sealed.
//	//	SystemFonts too, always creates new object.
//	//	But eg Brushes and SystemBrushes use cached object. It is not protected from disposing (would be exception later).

//	/// <summary>
//	/// Standard font used by most windows and controls.
//	/// On Windows 10 it is "Segoe UI" 9 by default.
//	/// </summary>
//	public static Font Regular(int dpi) => Font.FromHfont(NativeFont_.RegularCached(dpi));

//	/// <summary>
//	/// Bold version of <see cref="Regular"/> font.
//	/// </summary>
//	public static Font Bold(int dpi) => Font.FromHfont(NativeFont_.BoldCached(dpi));
//}
