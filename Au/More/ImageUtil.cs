
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;

namespace Au.More
{
	/// <summary>
	/// Loads WPF and GDI+ images from file, resource or string.
	/// </summary>
	/// <seealso cref="ResourceUtil"/>
	public static class ImageUtil
	{
		/// <summary>
		/// Returns true if string starts with "image:".
		/// </summary>
		public static bool HasImageStringPrefix(string s) => s.Starts("image:");

		/// <summary>
		/// Returns true if string starts with "resource:", "resources/", "image:" (Base64 encoded image), "imagefile:" (file path), "*" (XAML icon name) or "&lt;" (possibly XAML image).
		/// </summary>
		public static bool HasImageOrResourcePrefix(string s) => s.Starts('*') || s.Starts('<') || s.Starts("image:") || s.Starts("imagefile:") || ResourceUtil.HasResourcePrefix(s);

		/// <summary>
		/// Loads image file data as stream from Base64 string.
		/// </summary>
		/// <param name="s">Base64 encoded image string with prefix "image:".</param>
		/// <exception cref="ArgumentException">String does not start with "image:" or is invalid Base64.</exception>
		/// <exception cref="Exception"><see cref="Convert2.BrotliDecompress"/> exceptions (when compressed .bmp).</exception>
		public static MemoryStream LoadImageStreamFromString(string s) {
			if (!HasImageStringPrefix(s)) throw new ArgumentException("String must start with \"image:\".");
			int start = 6; while (start < s.Length && s[start] <= ' ') start++; //can be eg "image:\r\n..."
			bool compressedBmp = s.Eq(start, "WkJN");
			if (compressedBmp) start += 4;
			int n = (int)((s.Length - start) * 3L / 4);
			var b = new byte[n];
			if (!Convert.TryFromBase64Chars(s.AsSpan(start), b, out n)) throw new ArgumentException("Invalid Base64 string");
			if (!compressedBmp) return new MemoryStream(b, 0, n, false);
			return new MemoryStream(Convert2.BrotliDecompress(b.AsSpan(0, n)), false);
		}

		/// <summary>
		/// Loads GDI+ image from Base64 string.
		/// </summary>
		/// <param name="s">Base64 encoded image string with prefix "image:".</param>
		/// <exception cref="Exception">Exceptions of <see cref="LoadImageStreamFromString"/> and <see cref="System.Drawing.Bitmap(Stream)"/>.</exception>
		static System.Drawing.Bitmap _LoadGdipBitmapFromString(string s)
			=> new(LoadImageStreamFromString(s));

		/// <summary>
		/// Loads WPF image from Base64 string.
		/// </summary>
		/// <param name="s">Base64 encoded image string with prefix "image:".</param>
		/// <exception cref="Exception">Exceptions of <see cref="LoadImageStreamFromString"/> and <see cref="BitmapFrame.Create(Stream)"/>.</exception>
		static BitmapFrame _LoadWpfImageFromString(string s)
			=> BitmapFrame.Create(LoadImageStreamFromString(s));

		//not used in library
		///// <summary>
		///// Calls <see cref="LoadGdipBitmapFromString"/> and handles exceptions. On exception returns null and optionally writes warning to the output.
		///// </summary>
		//public static System.Drawing.Bitmap TryLoadGdipBitmapFromString(string s, bool warning) {
		//	try { return LoadGdipBitmapFromString(s); }
		//	catch (Exception ex) { if (warning) print.warning(ex.ToStringWithoutStack()); }
		//	return null;
		//}

		///// <summary>
		///// Calls <see cref="LoadWpfImageFromString"/> and handles exceptions. On exception returns null and optionally writes warning to the output.
		///// </summary>
		//public static BitmapFrame TryLoadWpfImageFromString(string s, bool warning) {
		//	try { return LoadWpfImageFromString(s); }
		//	catch (Exception ex) { if (warning) print.warning(ex.ToStringWithoutStack()); }
		//	return null;
		//}

		/// <summary>
		/// Loads GDI+ image from file, resource or string.
		/// </summary>
		/// <param name="image">
		/// Can be:
		/// - file path. Can have prefix "imagefile:".
		/// - resource path that starts with "resources/" or has prefix "resource:" (<see cref="ResourceUtil.GetGdipBitmap"/>)
		/// - Base64 encoded image string with prefix "image:".
		/// </param>
		/// <param name="xaml">If not null, supports XAML images. See <see cref="LoadGdipBitmapFromXaml"/>.</param>
		/// <exception cref="Exception">Depending on <i>image</i> string format, exceptions of <see cref="File.OpenRead(string)"/>, <see cref="System.Drawing.Bitmap(Stream)"/>, etc.</exception>
		public static System.Drawing.Bitmap LoadGdipBitmap(string image, (int dpi, SIZE? size)? xaml = null) {
			if (HasImageStringPrefix(image))
				return _LoadGdipBitmapFromString(image);
			if (xaml != null && (image.Starts('<') || image.Ends(".xaml", true)))
				return LoadGdipBitmapFromXaml(image, xaml.Value.dpi, xaml.Value.size);
			if (ResourceUtil.HasResourcePrefix(image))
				return ResourceUtil.GetGdipBitmap(image);
			if (image.Starts("imagefile:")) image = image[10..];
			image = pathname.normalize(image, folders.ThisAppImages);
			//return new(image); //no, the file remains locked until the Bitmap is disposed (documented, tested)
			using var fs = File.OpenRead(image);
			return new(fs);
		}

		/// <summary>
		/// Loads WPF image or icon from file, resource or string.
		/// </summary>
		/// <param name="image">
		/// Can be:
		/// - file path. Can have prefix "imagefile:".
		/// - resource path that starts with "resources/" or has prefix "resource:" (<see cref="ResourceUtil.GetWpfImage"/>)
		/// - Base64 encoded image string with prefix "image:".
		/// </param>
		/// <exception cref="Exception"></exception>
		public static BitmapFrame LoadWpfImage(string image) {
			if (HasImageStringPrefix(image)) return _LoadWpfImageFromString(image);
			if (ResourceUtil.HasResourcePrefix(image)) return ResourceUtil.GetWpfImage(image);
			if (image.Starts("imagefile:")) image = image[10..];
			image = pathname.normalize(image, folders.ThisAppImages, flags: PNFlags.CanBeUrlOrShell); //CanBeUrlOrShell: support "pack:"
			return BitmapFrame.Create(new Uri(image));
			//rejected: support XAML and "*iconName". Possible but not easy. Probably would be blurred when autoscaled.
		}

		/// <summary>
		/// Loads WPF image element from file, resource or string. Supports xaml, png and other image formats supported by WPF.
		/// </summary>
		/// <param name="image">
		/// Can be:
		/// - file path; can be .xaml, .png etc; supports environment variables etc, see <see cref="pathname.expand"/>; can have prefix "imagefile:".
		/// - resource path that starts with "resources/" or has prefix "resource:"; uses <see cref="ResourceUtil.GetXamlObject"/> if ends with ".xaml", else <see cref="ResourceUtil.GetWpfImage"/>.
		/// - Base64 encoded image string with prefix "image:"; uses<see cref="LoadImageStreamFromString"/>.
		/// - XAML string that starts with "&lt;".
		/// - XAML icon name, like "*Pack.Icon color" (you can get it from the Icons dialog); if literal string and using default compiler, the compiler adds XAML to the assembly as a string resource, else this function tries to get XAML from database and fails if editor isn't running.
		/// </param>
		/// <exception cref="Exception"></exception>
		/// <remarks>
		/// If <i>image</i> is XAML icon name or starts with "&lt;" or ends with ".xaml" (case-insensitive), returns object created from XAML root element. Else returns <see cref="Image"/> with <b>Source</b> = <b>BitmapFrame</b>.
		/// </remarks>
		public static FrameworkElement LoadWpfImageElement(string image) {
			if (image.Starts('*')) {
				image = script.editor.GetIcon(image, EGetIcon.IconNameToXaml) ?? throw new AuException("*get icon " + image);
			}
			if (image.Starts('<')) return (FrameworkElement)XamlReader.Parse(image);
			if (image.Ends(".xaml", true)) {
				if (ResourceUtil.HasResourcePrefix(image)) return (FrameworkElement)ResourceUtil.GetXamlObject(image);
				if (image.Starts("imagefile:")) image = image[10..];
				using var stream = filesystem.loadStream(image);
				return (FrameworkElement)XamlReader.Load(stream);
			} else {
				var bf = LoadWpfImage(image);
				return new Image { Source = bf };
			}
			//Could set UseLayoutRounding=true as a workaround for blurry images, but often it does not work and have to be set on parent element.
			//	Then does not work even if wrapped eg in a Border with UseLayoutRounding.
		}

		/// <summary>
		/// Loads GDI+ image from WPF XAML file or string.
		/// </summary>
		/// <param name="image">XAML file, resource or string. See <see cref="LoadWpfImageElement"/>.</param>
		/// <param name="dpi">DPI of window that will display the image.</param>
		/// <param name="size">Final image size in logical pixels (not DPI-scaled). If null, uses element's <b>DesiredSize</b> property, max 1024x1024.</param>
		/// <exception cref="Exception"></exception>
		/// <remarks>
		/// Calls <see cref="LoadWpfImageElement"/> and <see cref="ConvertWpfImageElementToGdipBitmap"/>.
		/// Don't use the <b>Tag</b> property of the bitmap. It keeps bitmap data.
		/// </remarks>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static System.Drawing.Bitmap LoadGdipBitmapFromXaml(string image, int dpi, SIZE? size = null) {
			var e = LoadWpfImageElement(image);
			//s_cwt.Add(e, new());
			return ConvertWpfImageElementToGdipBitmap(e, dpi, size);
		}

		//This unfinished version creates icon element without XAML parser, if possible.
		//	That part then 5 times faster, and whole function 2 times faster (1500 -> 750 mcs).
		//	But in reality this speed improvement isnt' very useful. Eg loading WPF is much slower than loading icons, although slightly faster without XAML reader. Better use a good cache.
		//public static System.Drawing.Bitmap LoadGdipBitmapFromXaml(string image, int dpi, SIZE? size = null) {
		//	using var p1 = perf.local();
		//	if (keys.isScrollLock) {
		//		var e = LoadWpfImageElement(image);
		//		p1.Next('X');
		//		return ConvertWpfImageElementToGdipBitmap(e, dpi, size);
		//	} else {
		//		FrameworkElement e = _GetPathFaster(image, size ?? new(16, 16));
		//		p1.Next();
		//		e ??= LoadWpfImageElement(image);
		//		p1.Next('x');
		//		//s_cwt.Add(e, new());
		//		return ConvertWpfImageElementToGdipBitmap(e, dpi, size);
		//	}

		//	static FrameworkElement _GetPathFaster(string image, SIZE size) {
		//		//if (!image.Starts('<')) return null;
		//		//int i = image.Find("<Path "); if (i < 0) return null;
		//		//if (i > 0) {
		//		//	int j = image.LastIndexOf("></") + 1;
		//		//	if (j <= i) return null;
		//		//	image = image[i..j];
		//		//}
		//		if (!image.Starts("<Path ")) return null;
		//		try {
		//			var x = XElement.Parse(image);
		//			bool flip = false;
		//			if (x.HasElements) {
		//				flip = true;//todo
		//			}
		//			var g = Geometry.Parse(x.Attr("Data"));
		//			var e = new System.Windows.Shapes.Path {
		//				Data = g,
		//				Stretch = Stretch.Uniform,
		//			};
		//			if (x.Attr(out string fill, "Fill")) {
		//				e.Fill = s_brushConverter.ConvertFromInvariantString(fill) as Brush;
		//				e.SnapsToDevicePixels = true;
		//				e.UseLayoutRounding = true;
		//			} else if (x.Attr(out string stroke, "Stroke")) {
		//				e.Stroke = s_brushConverter.ConvertFromInvariantString(stroke) as Brush;
		//				if (x.Attr(out double sThick, "StrokeThickness")) e.StrokeThickness = sThick;
		//				if (x.Attr(out string sStartCap, "StrokeStartLineCap")) e.StrokeStartLineCap = Enum.Parse<PenLineCap>(sStartCap);
		//				if (x.Attr(out string sEndCap, "StrokeEndLineCap")) e.StrokeEndLineCap = Enum.Parse<PenLineCap>(sEndCap);
		//				if (x.Attr(out string sJoin, "StrokeLineJoin")) e.StrokeLineJoin = Enum.Parse<PenLineJoin>(sJoin);
		//			}
		//			if (flip) e.LayoutTransform = new ScaleTransform(1, -1, 0.5, 0.5);
		//			if (x.HasAttr("Width") || x.HasAttr("Height")) {
		//				if (x.Attr(out double wid, "Width")) e.Width = wid;
		//				if (x.Attr(out double hei, "Height")) e.Height = hei;
		//				return new Viewbox { Width = size.width, Height = size.height, Child = e };
		//			}
		//			e.Width = size.width;
		//			e.Height = size.height;
		//			return e;
		//		}
		//		catch (Exception e1) { Debug_.Print(e1.ToStringWithoutStack()); }
		//		return null;
		//	}
		//}

		//static readonly BrushConverter s_brushConverter = new();

		//static ConditionalWeakTable<FrameworkElement, _DebugGC> s_cwt = new();
		//class _DebugGC { ~_DebugGC() { print.it("~"); } }

		/// <summary>
		/// Converts WPF image element to GDI+ image.
		/// </summary>
		/// <param name="e">For example <b>Viewbox</b>.</param>
		/// <param name="dpi">DPI of window that will display the image.</param>
		/// <param name="size">
		/// Final image size in logical pixels (not DPI-scaled).
		/// If null, uses element's <b>DesiredSize</b> property, max 1024x1024.
		/// If not null, sets element's <b>Width</b> and <b>Height</b>; the element should not be used in UI.
		/// </param>
		public static unsafe System.Drawing.Bitmap ConvertWpfImageElementToGdipBitmap(FrameworkElement e, int dpi, SIZE? size = null) {
			bool measured = e.IsMeasureValid;
			if (size != null) {
				measured = false;
				e.Width = size.Value.width;
				e.Height = size.Value.height;
			}
			if (!measured) e.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			bool arranged = measured && e.IsArrangeValid; //initially !measured but arranged; after measuring measured and !arranged
			if (!arranged) e.Arrange(new Rect(e.DesiredSize));
			if (size == null) {
				var z = e.DesiredSize; //if using RenderSize or ActualX, if element height!=width, draws in wrong place, clipped
				size = new(Math.Min(1024d, z.Width).ToInt(), Math.Min(1024d, z.Height).ToInt());
			}
			var (wid, hei) = Dpi.Scale(size.Value, dpi);
			var rtb = new RenderTargetBitmap(wid, hei, dpi, dpi, PixelFormats.Pbgra32);
			//var rtb = t_rtb ??= new RenderTargetBitmap(wid, hei, dpi, dpi, PixelFormats.Pbgra32); rtb.Clear(); //not better
			rtb.Render(e);
			if (!arranged) e.InvalidateArrange(); //prevent huge memory leak
			if (!measured) e.InvalidateMeasure();
			int stride = wid * 4, msize = hei * stride;
#if true
			var R = new System.Drawing.Bitmap(wid, hei, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			var d = R.LockBits(new(0, 0, wid, hei), System.Drawing.Imaging.ImageLockMode.ReadWrite, R.PixelFormat);
			try { rtb.CopyPixels(new(0, 0, wid, hei), d.Scan0, msize, stride); }
			finally { R.UnlockBits(d); }
			R.SetResolution(dpi, dpi);
			return R;
			//tested: GC OK. Don't need GC_.AddObjectMemoryPressure. WPF makes enough garbage to trigger GC when need.
#else //no. See uuimage._Capture.
			var m = GC.AllocateUninitializedArray<uint>(wid * hei, pinned: true);
			fixed (uint* pixels = m) {
				rtb.CopyPixels(new Int32Rect(0, 0, wid, hei), (IntPtr)pixels, msize, stride);
				var b = new System.Drawing.Bitmap(wid, hei, stride, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, (IntPtr)pixels) { Tag = m };
				//only this Bitmap creation method preserves alpha.
				//	Update: But now with LockBits OK. Maybe pixel format was wrong (without P) when tested previously.
				b.SetResolution(dpi, dpi);
				return b;
			}
#endif
		}
		//[ThreadStatic] static RenderTargetBitmap t_rtb;
	}
}
