namespace Au.More;

/// <summary>
/// Creates and manages native bitmap handle and memory DC (GDI device context).
/// The bitmap is selected in the DC.
/// </summary>
public class MemoryBitmap : IDisposable {
	IntPtr _dc, _bm, _oldbm;
	bool _disposed;

	/// <summary>
	/// DC handle.
	/// </summary>
	public IntPtr Hdc => _dc;

	/// <summary>
	/// Bitmap handle.
	/// </summary>
	public IntPtr Hbitmap => _bm;

	/// <summary>
	/// Does nothing. Later you can call <see cref="Create"/> or <see cref="Attach"/>.
	/// </summary>
	public MemoryBitmap() { }

	/// <summary>
	/// Calls <see cref="Create"/>.
	/// </summary>
	/// <exception cref="ArgumentException"><i>width</i> or <i>height</i> is less than 1.</exception>
	/// <exception cref="AuException">Failed. Probably there is not enough memory for bitmap of specified size (need <c>with*height*4</c> bytes).</exception>
	public MemoryBitmap(int width, int height) {
		if (width <= 0 || height <= 0) throw new ArgumentException();
		if (!Create(width, height)) throw new AuException("*create memory bitmap of specified size");
	}

	//rejected: not obvious, whether it attaches or copies. Also, attaching is rarely used.
	///// <summary>
	///// Calls <see cref="Attach"/>.
	///// </summary>
	//public MemoryBitmap(IntPtr hBitmap)
	//{
	//	Attach(hBitmap);
	//}

	///
	protected virtual void Dispose(bool disposing) {
		if (_disposed) return;
		_disposed = true;
		Delete();
	}

	/// <summary>
	/// Deletes the bitmap and DC.
	/// </summary>
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	///
	~MemoryBitmap() => Dispose(false);
	//Calls DeleteDC. MSDN says that ReleaseDC must be called from the same thread. But does not say it about DeleteDC and others.
	//	Tested: DeleteDC returns true in finalizer (other thread).

	/// <summary>
	/// Deletes the bitmap and DC.
	/// </summary>
	public void Delete() {
		if (_dc == default) return;
		if (_bm != default) {
			Api.SelectObject(_dc, _oldbm);
			Api.DeleteObject(_bm);
			_bm = default;
		}
		Api.DeleteDC(_dc);
		_dc = default;
	}

	/// <summary>
	/// Creates new memory DC and bitmap of specified size and selects it into the DC.
	/// </summary>
	/// <returns><c>false</c> if failed. In any case deletes previous bitmap and DC.</returns>
	/// <param name="width">Width, pixels. Must be &gt; 0.</param>
	/// <param name="height">Height, pixels. Must be &gt; 0.</param>
	public bool Create(int width, int height) {
		if (_disposed) throw new ObjectDisposedException(nameof(MemoryBitmap));
		using var dcs = new ScreenDC_();
		Attach(Api.CreateCompatibleBitmap(dcs, width, height));
		return _bm != default;
	}

	/// <summary>
	/// Sets this variable to manage an existing bitmap.
	/// Selects the bitmap into a memory DC.
	/// Deletes previous bitmap and DC.
	/// </summary>
	/// <param name="hBitmap">Native bitmap handle.</param>
	public void Attach(IntPtr hBitmap) {
		if (_disposed) throw new ObjectDisposedException(nameof(MemoryBitmap));
		Delete();
		if (hBitmap != default) {
			_dc = Api.CreateCompatibleDC(default);
			_oldbm = Api.SelectObject(_dc, _bm = hBitmap);
		}
	}

	/// <summary>
	/// Deletes memory DC, clears this variable and returns its bitmap (native bitmap handle).
	/// The returned bitmap is not selected into a DC. Will need to delete it with API <msdn>DeleteObject</msdn>.
	/// </summary>
	public IntPtr Detach() {
		IntPtr bret = _bm;
		if (_bm != default) {
			Api.SelectObject(_dc, _oldbm);
			Api.DeleteDC(_dc);
			_dc = default; _bm = default;
		}
		return bret;
	}
}
