using System.Drawing;
using System.Drawing.Imaging;

#pragma warning disable 649

namespace Au.Controls {
	public static unsafe class KImageUtil {
		#region API
		
		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		internal struct BITMAPFILEHEADER {
			public ushort bfType;
			public int bfSize;
			public ushort bfReserved1;
			public ushort bfReserved2;
			public int bfOffBits;
		}
		
		internal struct BITMAPCOREHEADER {
			public int bcSize;
			public ushort bcWidth;
			public ushort bcHeight;
			public ushort bcPlanes;
			public ushort bcBitCount;
		}
		
		internal struct BITMAPV5HEADER {
			public int bV5Size;
			public int bV5Width;
			public int bV5Height;
			public ushort bV5Planes;
			public ushort bV5BitCount;
			public int bV5Compression;
			public int bV5SizeImage;
			public int bV5XPelsPerMeter;
			public int bV5YPelsPerMeter;
			public int bV5ClrUsed;
			public int bV5ClrImportant;
			public uint bV5RedMask;
			public uint bV5GreenMask;
			public uint bV5BlueMask;
			public uint bV5AlphaMask;
			public uint bV5CSType;
			public CIEXYZTRIPLE bV5Endpoints;
			public uint bV5GammaRed;
			public uint bV5GammaGreen;
			public uint bV5GammaBlue;
			public uint bV5Intent;
			public uint bV5ProfileData;
			public uint bV5ProfileSize;
			public uint bV5Reserved;
		}
		
		internal struct CIEXYZTRIPLE {
			public CIEXYZ ciexyzRed;
			public CIEXYZ ciexyzGreen;
			public CIEXYZ ciexyzBlue;
		}
		
		internal struct CIEXYZ {
			public int ciexyzX;
			public int ciexyzY;
			public int ciexyzZ;
		}
		
		#endregion
		
		internal struct BitmapFileInfo_ {
			/// <summary>
			/// Can be BITMAPINFOHEADER/BITMAPV5HEADER or BITMAPCOREHEADER.
			/// </summary>
			public void* biHeader;
			public int width, height, bitCount;
			public bool isCompressed;
		}
		
		/// <summary>
		/// Gets some info from BITMAPINFOHEADER or BITMAPCOREHEADER.
		/// Checks if it is valid bitmap file header. Returns false if invalid.
		/// </summary>
		internal static bool GetBitmapFileInfo_(byte[] mem, out BitmapFileInfo_ x) {
			x = new BitmapFileInfo_();
			fixed (byte* bp = mem) {
				BITMAPFILEHEADER* f = (BITMAPFILEHEADER*)bp;
				int minHS = sizeof(BITMAPFILEHEADER) + sizeof(BITMAPCOREHEADER);
				if (mem.Length <= minHS || f->bfType != (((byte)'M' << 8) | (byte)'B') || f->bfOffBits >= mem.Length || f->bfOffBits < minHS)
					return false;
				
				Api.BITMAPINFOHEADER* h = (Api.BITMAPINFOHEADER*)(f + 1);
				int siz = h->biSize;
				if (siz >= sizeof(Api.BITMAPINFOHEADER) && siz <= sizeof(BITMAPV5HEADER) * 2) {
					x.width = h->biWidth;
					x.height = h->biHeight;
					x.bitCount = h->biBitCount;
					x.isCompressed = h->biCompression != 0;
				} else if (siz == sizeof(BITMAPCOREHEADER)) {
					BITMAPCOREHEADER* ch = (BITMAPCOREHEADER*)h;
					x.width = ch->bcWidth;
					x.height = ch->bcHeight;
					x.bitCount = ch->bcBitCount;
				} else return false;
				
				//x.height = Math.Abs(x.height); //no, then draws incorrectly if top-down
				if (x.height <= 0) return false; //rare
				
				x.biHeader = h;
				return true;
				//note: don't check f->bfSize. Sometimes it is incorrect (> or < memSize). All programs open such files. Instead check other data later.
			}
		}
		
		/// <summary>
		/// Image type detected by <see cref="ImageTypeFromString(out int, string)"/>.
		/// </summary>
		public enum ImageType {
			/// <summary>The string isn't image.</summary>
			None,
			
			/// <summary>Base64 encoded image file data with prefix "image:" (.png/gif/jpg) or "image:WkJN" (compressed .bmp). See <see cref="ImageToString(string)"/>.</summary>
			Base64Image,
			
			/// <summary>.bmp file path.</summary>
			Bmp,
			
			/// <summary>.png, .gif or .jpg file path.</summary>
			PngGifJpg,
			
			/// <summary>.ico file path.</summary>
			Ico,
			
			/// <summary>.cur or .ani file path.</summary>
			Cur,
			
			/// <summary>Icon from a .dll or other file containing icons, like @"C:\a\b.dll,15".</summary>
			IconLib,
			
			/// <summary>None of other image types.</summary>
			ShellIcon,
			
			/// <summary>XAML image data.</summary>
			Xaml,
			
			/// <summary>XAML icon name "*Pack.Icon color".</summary>
			XamlIconName,
			
			//rejected. Where it used, we cannot know the assembly; maybe it is script's assembly and in editor we cannot get its resources or it is difficult (need to use meta).
			///// <summary>"resource:name". An image resource name from managed resources of the entry assembly. In Visual Studio use build action "Resource".</summary>
			//Resource,
		}
		
		/// <summary>
		/// Gets image type from string.
		/// </summary>
		/// <param name="prefixLength">Length of prefix "imagefile:" or "image:".</param>
		/// <param name="s">File path etc. See <see cref="ImageType"/>. It is UTF-8 because used directly with Scintilla text.</param>
		internal static ImageType ImageTypeFromString(out int prefixLength, RByte s) {
			prefixLength = 0;
			if (s.Length < 2) return default; //C:\x.bmp or .h
			char c1 = (char)s[0], c2 = (char)s[1];
			
			//special strings
			switch (c1) {
			case 'i':
				if (s.StartsWith("image:"u8)) {
					if (s.Length < 10) return default;
					prefixLength = 6;
					return ImageType.Base64Image;
				}
				if (s.StartsWith("imagefile:"u8)) {
					s = s[10..];
					if (s.Length < 8) return default;
					prefixLength = 10;
					c1 = (char)s[0]; c2 = (char)s[1];
				}
				break;
			//case 'r': if (s.StartsWith("resource:"u8)) return ImageType.Resource; break;
			case '<':
				if (s.IndexOf("xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'"u8) > 0) {
					if (s.IndexOf("<Path "u8) >= 0) return ImageType.Xaml;
					if (s.IndexOf("<GeometryDrawing "u8) >= 0) return ImageType.Xaml;
				}
				return default;
			case '*':
				if (_DetectIconString(s)) return ImageType.XamlIconName;
				return default;
			}
			
			//file path
			if (s.Length >= 8 && (c1 == '%' || (c2 == ':' && c1.IsAsciiAlpha()) || (c1 == '\\' && c2 == '\\'))) { //is image file path?
				if (s[^4] == '.') {
					Span<byte> e = stackalloc byte[3];
					for (int j = 0, k = s.Length - 3; j < 3; ) e[j++] = (byte)char.ToLowerInvariant((char)s[k++]);
					if (e.SequenceEqual("bmp"u8)) return ImageType.Bmp;
					if (e.SequenceEqual("png"u8)) return ImageType.PngGifJpg;
					if (e.SequenceEqual("gif"u8)) return ImageType.PngGifJpg;
					if (e.SequenceEqual("jpg"u8)) return ImageType.PngGifJpg;
					if (e.SequenceEqual("ico"u8)) return ImageType.Ico;
					if (e.SequenceEqual("cur"u8)) return ImageType.Cur;
					if (e.SequenceEqual("ani"u8)) return ImageType.Cur;
				} else if (((char)s[^1]).IsAsciiDigit()) { //can be like C:\x.dll,10
					int i = s.LastIndexOf((byte)',');
					if (i >= 8 && s[i - 4] == '.' && char.IsAsciiLetter((char)s[i - 1])) {
						if (s[++i] == '-') i++;
						if (!s[i..].ContainsAnyExceptInRange((byte)'0', (byte)'9')) return ImageType.IconLib;
					}
				}
			}
			
			return ImageType.ShellIcon; //can be other file type, URL, .ext, :: ITEMIDLIST, ::{CLSID}
			
			//copied from WpfUtil_.DetectIconString(RStr)
			static bool _DetectIconString(RByte s) {
				if (s.Length < 8 || s[0] != '*') return false;
				int pack = 1;
				if (s[1] == '<') { // *<library>*icon
					pack = s.IndexOf((byte)'>');
					if (pack++ < 0 || s.Length - pack < 8 || s[pack] != '*') return false;
					pack++;
				}
				int name = pack; while (name < s.Length && s[name] != '.') name++; if (name < pack + 4) return false;
				int end = ++name; while (end < s.Length && s[end] != ' ') end++;
				return end > name;
			}
		}
		
		/// <summary>
		/// Gets image type from string.
		/// </summary>
		/// <param name="prefixLength">Length of prefix "imagefile:" or "image:".</param>
		/// <param name="s">File path etc. See <see cref="ImageType"/>.</param>
		public static ImageType ImageTypeFromString(out int prefixLength, string s) {
			return ImageTypeFromString(out prefixLength, Encoding.UTF8.GetBytes(s));
		}
		
		/// <summary>
		/// Loads image and returns its data in .bmp file format.
		/// Returns null if fails, for example file not found or invalid Base64 string.
		/// </summary>
		/// <param name="s">Depends on t. File path or resource name without prefix "imagefile:" or Base64 image data without prefix "image:".</param>
		/// <param name="t">Image type and string format.</param>
		/// <param name="searchPath">Use <see cref="filesystem.searchPath"/></param>
		/// <param name="xaml">If not null, supports XAML images. See <see cref="ImageUtil.LoadGdipBitmapFromXaml"/>.</param>
		/// <remarks>Supports environment variables etc. If not full path, searches in <see cref="folders.ThisAppImages"/>.</remarks>
		public static byte[] BmpFileDataFromString(string s, ImageType t, bool searchPath = false, (int dpi, SIZE? size)? xaml = null) {
			//print.qm2.write(t);
			try {
				switch (t) {
				case ImageType.Bmp or ImageType.PngGifJpg or ImageType.Cur:
					if (searchPath) {
						s = filesystem.searchPath(s, folders.ThisAppImages);
						if (s == null) return null;
					} else {
						if (!pathname.isFullPathExpand(ref s)) return null;
						s = pathname.normalize(s, folders.ThisAppImages);
						if (!filesystem.exists(s).File) return null;
					}
					break;
				}
				g1:
				switch (t) {
				case ImageType.Base64Image:
					var a = Convert.FromBase64String(s);
					if (s.AsSpan().TrimStart().StartsWith("WkJN")) return Convert2.BrotliDecompress(a.AsSpan(3));
					return _ImageToBytes(new(new MemoryStream(a, false)));
				//case ImageType.Resource:
				//	return _ImageToBytes(ResourceUtil.GetGdipBitmap(s));
				case ImageType.Bmp:
					return File.ReadAllBytes(s);
				case ImageType.PngGifJpg:
					return _ImageToBytes(new(s));
				case ImageType.Ico or ImageType.IconLib or ImageType.ShellIcon or ImageType.Cur:
					return _IconToBytes(s, t == ImageType.Cur, searchPath);
				case ImageType.XamlIconName when xaml != null:
					s = ScriptEditor.GetIcon(s, EGetIcon.IconNameToXaml);
					if (s == null) break;
					t = ImageType.Xaml;
					goto g1;
				case ImageType.Xaml when xaml != null:
					var xv = xaml.Value;
					return _ImageToBytes(ImageUtil.LoadGdipBitmapFromXaml(s, xv.dpi, xv.size));
				}
			}
			catch (Exception ex) { Debug_.PrintIf(t != ImageType.Xaml, ex.Message + "    " + s); }
			return null;
		}
		
		static byte[] _ImageToBytes(Bitmap im) {
			if (im == null) return null;
			try {
				//workaround for the black alpha problem. Does not make slower.
				var flags = (ImageFlags)im.Flags;
				if (0 != (flags & ImageFlags.HasAlpha)) { //most png have it
					var t = new Bitmap(im.Width, im.Height);
					t.SetResolution(im.HorizontalResolution, im.VerticalResolution);
					
					using (var g = Graphics.FromImage(t)) {
						g.Clear(Color.White);
						g.DrawImageUnscaled(im, 0, 0);
					}
					
					var old = im; im = t; old.Dispose();
				}
				
				var m = new MemoryStream();
				im.Save(m, ImageFormat.Bmp);
				return m.ToArray();
			}
			finally { im.Dispose(); }
		}
		
		static byte[] _IconToBytes(string s, bool isCursor, bool searchPath) {
			//info: would be much easier with Icon.ToBitmap(), but it creates bitmap that is displayed incorrectly when we draw it in Scintilla.
			//	Mask or alpha areas then are black.
			//	Another way - draw it like in the png case. Not tested. Maybe would not work with cursors. Probably slower.
			
			IntPtr hi; int siz;
			if (isCursor) {
				hi = Api.LoadImage(default, s, Api.IMAGE_CURSOR, 0, 0, Api.LR_LOADFROMFILE | Api.LR_DEFAULTSIZE);
				siz = Api.GetSystemMetrics(Api.SM_CXCURSOR);
				//note: if LR_DEFAULTSIZE, uses SM_CXCURSOR, normally 32. It may be not what Explorer displays eg in Cursors folder. But without it gets the first cursor, which often is large, eg 128.
			} else {
				hi = icon.of(s, 16, searchPath ? 0 : IconGetFlags.DontSearch)?.Detach() ?? default;
				siz = 16;
			}
			if (hi == default) return null;
			try {
				using var m = new MemoryBitmap(siz, siz);
				RECT r = new(0, 0, siz, siz);
				Api.FillRect(m.Hdc, r, Api.GetStockObject(0)); //WHITE_BRUSH
				if (!Api.DrawIconEx(m.Hdc, 0, 0, hi, siz, siz)) return null;
				
				int headersSize = sizeof(BITMAPFILEHEADER) + sizeof(Api.BITMAPINFOHEADER);
				var a = new byte[headersSize + siz * siz * 3];
				fixed (byte* p = a) {
					var f = (BITMAPFILEHEADER*)p;
					f->bfType = ((byte)'M' << 8) | (byte)'B'; f->bfOffBits = headersSize; f->bfSize = a.Length;
					var bi = new Api.BITMAPINFO(siz, siz, 24);
					if (siz != Api.GetDIBits(m.Hdc, m.Hbitmap, 0, siz, p + headersSize, ref bi, 0)) return null; //DIB_RGB_COLORS
					MemoryUtil.Copy(&bi, f + 1, bi.biSize);
				}
				return a;
			}
			finally {
				Api.DestroyIcon(hi);
			}
		}
		
#if false //currently not used. If using, move to icon class.

	/// <summary>
	/// Gets icon or cursor size from its native handle.
	/// Returns 0 if failed.
	/// </summary>
	public static int GetIconSizeFromHandle(IntPtr hi)
	{
		if(!Api.GetIconInfo(hi, out var ii)) return 0;
		try {
			var hb = (ii.hbmColor != default) ? ii.hbmColor : ii.hbmMask;
			Api.BITMAP b;
			if(0 == Api.GetObject(hb, sizeof(BITMAP), &b)) return 0;
			print.it(b.bmWidth, b.bmHeight, b.bmBits);
			return b.bmWidth;
		}
		finally {
			Api.DeleteObject(ii.hbmColor);
			Api.DeleteObject(ii.hbmMask);
		}
	}
#endif
		
		/// <summary>
		/// Compresses .bmp file data (<see cref="Convert2.BrotliCompress"/>) and Base64-encodes.
		/// Returns string with "image:" prefix.
		/// </summary>
		public static string BmpFileDataToString(byte[] bmpFileData) {
			if (bmpFileData == null) return null;
			return "image:WkJN" + Convert.ToBase64String(Convert2.BrotliCompress(bmpFileData));
			//WkJN is Base64 of "ZBM"
		}
		
		/// <summary>
		/// Converts image file data to string that can be used in source code instead of file path. It is supported by some functions of this library, for example <see cref="uiimage.find"/>.
		/// Supports all <see cref="ImageType"/> formats. For non-image files gets icon. Converts icons to bitmap.
		/// If <i>path</i> is of <see cref="ImageType"/> type <b>Base64Image</b>, <b>Xaml</b> or <b>XamlIconName</b>, returns <i>path</i>. Else returns Base64 encoded file data with prefix "image:" (.png/gif/jpg) or "image:WkJN" (compressed .bmp).
		/// Returns null if path is not a valid image string or the file does not exist or failed to load.
		/// </summary>
		/// <remarks>Supports environment variables etc. If not full path, searches in <see cref="folders.ThisAppImages"/> and standard directories.</remarks>
		public static string ImageToString(string path) {
			var t = ImageTypeFromString(out _, path);
			switch (t) {
			case ImageType.None: return null;
			case ImageType.Base64Image or ImageType.Xaml or ImageType.XamlIconName: return path;
			//case ImageType.Resource: return path;
			case ImageType.PngGifJpg:
				path = filesystem.searchPath(path, folders.ThisAppImages); if (path == null) return null;
				try { return "image:" + Convert.ToBase64String(filesystem.loadBytes(path)); }
				catch (Exception ex) { Debug_.Print(ex); return null; }
			}
			return BmpFileDataToString(BmpFileDataFromString(path, t, true));
		}
		
		//currently not used.
		//	When tried to display the WPF image in Image control, was blurry when high DPI, although size is correct (because bitmap's DPI is correct).
		//	UseLayoutRounding did not help. It seems WPF scales/unscales the image don't know how many times.
		///// <summary>
		///// Converts GDI+ bitmap to WPF bitmap image.
		///// </summary>
		//public static System.Windows.Media.Imaging.BitmapSource WinformsBitmapToWpf(Bitmap bmp, bool dispose, int? dpi = null) {
		//	var size = bmp.Size;
		//	var bd = bmp.LockBits(new Rectangle(default, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		//	try {
		//		return System.Windows.Media.Imaging.BitmapSource.Create(
		//			size.Width, size.Height,
		//			dpi ?? bmp.HorizontalResolution.ToInt(),
		//			dpi ?? bmp.VerticalResolution.ToInt(),
		//			pixelFormat: System.Windows.Media.PixelFormats.Bgra32,
		//			null, bd.Scan0, bd.Height * bd.Stride, bd.Stride);
		//	}
		//	finally {
		//		bmp.UnlockBits(bd);
		//		if (dispose) bmp.Dispose();
		//	}
		//}
		
		///// <summary>
		///// If <i>dpi</i> &gt; 96 (100%) and image resolution is different, returns scaled image.Size. Else returns image.Size.
		///// </summary>
		//public static SIZE ImageSize(Image image, int dpi) {
		//	if (image == null) return default;
		//	SIZE z = image.Size;
		//	if (dpi > 96) {
		//		z.width = Math2.MulDiv(z.width, dpi, image.HorizontalResolution.ToInt());
		//		z.height = Math2.MulDiv(z.height, dpi, image.VerticalResolution.ToInt());
		//	}
		//	return z;
		//}
		
		///// <summary>
		///// If <i>dpi</i> &gt; 96 (100%) and image resolution is different, returns scaled copy of <i>image</i>. Else returns <i>image</i>.
		///// </summary>
		///// <param name="image"></param>
		///// <param name="dpi"></param>
		///// <param name="disposeOld">If performed scaling (it means created new image), dispose old image.</param>
		///// <remarks>
		///// Unlike <see cref="System.Windows.Forms.Control.ScaleBitmapLogicalToDevice"/>, returns same object if don't need scaling.
		///// </remarks>
		//public static Image ScaleImage(Image image, int dpi, bool disposeOld) {
		//	if (image != null && dpi > 96) {
		//		int xRes = image.HorizontalResolution.ToInt(), yRes = image.VerticalResolution.ToInt();
		//		//print.it(xRes, yRes, dpi);
		//		if (xRes != dpi || yRes != dpi) {
		//			var z = image.Size;
		//			var r = _ScaleBitmap(image, Math2.MulDiv(z.Width, dpi, xRes), Math2.MulDiv(z.Height, dpi, yRes), z);
		//			if (disposeOld) image.Dispose();
		//			image = r;
		//		}
		//	}
		//	return image;
		//}
		
		////From .NET DpiHelper.ScaleBitmapToSize, which is used by Control.ScaleBitmapLogicalToDevice.
		//private static Bitmap _ScaleBitmap(Image oldImage, int width, int height, Size oldSize) {
		//	//note: could simply return new Bitmap(oldImage, width, height). It uses similar code, but lower quality.
		
		//	var r = new Bitmap(width, height, oldImage.PixelFormat);
		
		//	using var graphics = Graphics.FromImage(r);
		//	var mode = InterpolationMode.HighQualityBicubic;
		//	//if(width % oldSize.Width == 0 && height % oldSize.Height == 0) mode = InterpolationMode.NearestNeighbor; //DpiHelper does it, but maybe it isn't a good idea
		//	graphics.InterpolationMode = mode;
		//	graphics.CompositingQuality = CompositingQuality.HighQuality;
		
		//	var sourceRect = new RectangleF(-0.5f, -0.5f, oldSize.Width, oldSize.Height);
		//	var destRect = new RectangleF(0, 0, width, height);
		
		//	graphics.DrawImage(oldImage, destRect, sourceRect, GraphicsUnit.Pixel);
		
		//	return r;
		//}
		
		/// <summary>
		/// Converts XAML image to native icon file data.
		/// </summary>
		/// <param name="stream">Stream to write icon file data. Writes from start.</param>
		/// <param name="image">Image XAML. See <see cref="ImageUtil.LoadWpfImageElement"/>.</param>
		/// <param name="sizes">Sizes of icon images to add to the ico file. For example 16, 24, 32, 48, 64. Sizes can be 1 to 256 inclusive.</param>
		/// <exception cref="ArgumentOutOfRangeException">An invalid size.</exception>
		/// <exception cref="Exception"></exception>
		public static unsafe void XamlImageToIconFile(Stream stream, string image, params int[] sizes) {
			var e = ImageUtil.LoadWpfImageElement(image);
			stream.Position = Math2.AlignUp(sizeof(Api.NEWHEADER) + sizeof(Api.ICONDIRENTRY) * sizes.Length, 4);
			var a = stackalloc Api.ICONDIRENTRY[sizes.Length];
			for (int i = 0; i < sizes.Length; i++) {
				int size = sizes[i];
				if (size < 1 || size > 256) throw new ArgumentOutOfRangeException();
				using var b = ImageUtil.ConvertWpfImageElementToGdipBitmap(e, 96, (size, size));
				int pos = (int)stream.Position;
				b.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
				byte bsize = (byte)(size == 256 ? 0 : checked((byte)size));
				a[i] = new Api.ICONDIRENTRY { bWidth = bsize, bHeight = bsize, wBitCount = 32, dwBytesInRes = (int)stream.Position - pos, dwImageOffset = pos };
			}
			var posEnd = stream.Position;
			stream.Position = 0;
			var h = new Api.NEWHEADER { wResType = 1, wResCount = (ushort)sizes.Length };
			stream.Write(new(&h, sizeof(Api.NEWHEADER)));
			stream.Write(new(a, sizeof(Api.ICONDIRENTRY) * sizes.Length));
			stream.Position = posEnd;
		}
		
		///
		public static void XamlImageToIconFile(string file, string image, params int[] sizes) {
			file = pathname.NormalizeMinimally_(file);
			using var stream = File.OpenWrite(file);
			XamlImageToIconFile(stream, image, sizes);
		}
		
		//currently not used.
		///// <summary>
		///// Creates GDI+ <b>Bitmap</b> from a GDI bitmap.
		///// </summary>
		///// <param name="hbitmap">GDI bitmap handle.</param>
		///// <remarks>
		///// How this function is different from <see cref="System.Drawing.Image.FromHbitmap"/>:
		///// 1. <b>Image.FromHbitmap</b> usually creates bottom-up bitmap, which is incompatible with <see cref="uiimage.find"/>. This function creates normal top-down bitmap, like <c>new Bitmap(...)</c>, <c>Bitmap.FromFile(...)</c> etc.
		///// 2. This function always creates bitmap of <b>PixelFormat</b> <b>Format32bppRgb</b>.
		///// </remarks>
		///// <exception cref="AuException">Failed. For example <i>hbitmap</i> is default(IntPtr).</exception>
		///// <exception cref="Exception">Exceptions of Bitmap(int, int, PixelFormat) constructor.</exception>
		//public static unsafe System.Drawing.Bitmap ConvertHbitmapToGdipBitmap(IntPtr hbitmap) {
		//	var bh = new Api.BITMAPINFOHEADER() { biSize = sizeof(Api.BITMAPINFOHEADER) };
		//	using (var dcs = new ScreenDC_()) {
		//		if (0 == Api.GetDIBits(dcs, hbitmap, 0, 0, null, &bh, 0)) goto ge;
		//		int wid = bh.biWidth, hei = bh.biHeight;
		//		if (hei > 0) bh.biHeight = -bh.biHeight; else hei = -hei;
		//		bh.biBitCount = 32;
		
		//		var R = new System.Drawing.Bitmap(wid, hei, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
		//		var d = R.LockBits(new System.Drawing.Rectangle(0, 0, wid, hei), System.Drawing.Imaging.ImageLockMode.ReadWrite, R.PixelFormat);
		//		bool ok = hei == Api.GetDIBits(dcs, hbitmap, 0, hei, (void*)d.Scan0, &bh, 0);
		//		R.UnlockBits(d);
		//		if (!ok) { R.Dispose(); goto ge; }
		//		return R;
		//	}
		//	ge:
		//	throw new AuException();
		//}
	}
}
