using System.Windows;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Au.More;

/// <summary>
/// Loads WPF and GDI+ images from file, resource or string.
/// </summary>
/// <seealso cref="ResourceUtil"/>
public static partial class ImageUtil {
	/// <summary>
	/// Returns <c>true</c> if string starts with <c>"image:"</c>.
	/// </summary>
	public static bool HasImageStringPrefix(string s) => s.Starts("image:");
	
	/// <summary>
	/// Returns <c>true</c> if string starts with <c>"resource:"</c>, <c>"resources/"</c>, <c>"image:"</c> (Base64 encoded image), <c>"imagefile:"</c> (file path), <c>"*"</c> (XAML icon name) or <c>"&lt;"</c> (possibly XAML image).
	/// </summary>
	public static bool HasImageOrResourcePrefix(string s) => s.Starts('*') || s.Starts('<') || s.Starts("image:") || s.Starts("imagefile:") || ResourceUtil.HasResourcePrefix(s);
	
	/// <summary>
	/// Loads image file data as stream from Base64 string.
	/// </summary>
	/// <param name="s">Base64 encoded image string with prefix <c>"image:"</c>.</param>
	/// <exception cref="ArgumentException">String does not start with <c>"image:"</c> or is invalid Base64.</exception>
	/// <exception cref="Exception"><see cref="Convert2.BrotliDecompress"/> exceptions (when compressed <c>.bmp</c>).</exception>
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
	/// <param name="s">Base64 encoded image string with prefix <c>"image:"</c>.</param>
	/// <exception cref="Exception">Exceptions of <see cref="LoadImageStreamFromString"/> and <see cref="System.Drawing.Bitmap(Stream)"/>.</exception>
	static System.Drawing.Bitmap _LoadGdipBitmapFromString(string s)
		=> new(LoadImageStreamFromString(s));
	
	/// <summary>
	/// Loads WPF image from Base64 string.
	/// </summary>
	/// <param name="s">Base64 encoded image string with prefix <c>"image:"</c>.</param>
	/// <exception cref="Exception">Exceptions of <see cref="LoadImageStreamFromString"/> and <see cref="BitmapFrame.Create(Stream)"/>.</exception>
	static BitmapFrame _LoadWpfImageFromString(string s)
		=> BitmapFrame.Create(LoadImageStreamFromString(s));
	
	//not used in library
	///// <summary>
	///// Calls <see cref="LoadGdipBitmapFromString"/> and handles exceptions. On exception returns <c>null</c> and optionally prints a warning.
	///// </summary>
	//public static System.Drawing.Bitmap TryLoadGdipBitmapFromString(string s, bool warning) {
	//	try { return LoadGdipBitmapFromString(s); }
	//	catch (Exception ex) { if (warning) print.warning(ex.ToStringWithoutStack()); }
	//	return null;
	//}
	
	///// <summary>
	///// Calls <see cref="LoadWpfImageFromString"/> and handles exceptions. On exception returns <c>null</c> and optionally prints warning.
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
	/// <br/>• file path. Can have prefix <c>"imagefile:"</c>.
	/// <br/>• resource path that starts with <c>"resources/"</c> or has prefix <c>"resource:"</c> (<see cref="ResourceUtil.GetGdipBitmap"/>)
	/// <br/>• Base64 encoded image string with prefix <c>"image:"</c>.
	/// </param>
	/// <param name="xaml">If not <c>null</c>, supports XAML images. See <see cref="LoadGdipBitmapFromXaml"/>.</param>
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
	/// <br/>• file path. Can have prefix <c>"imagefile:"</c>.
	/// <br/>• resource path that starts with <c>"resources/"</c> or has prefix <c>"resource:"</c> (<see cref="ResourceUtil.GetWpfImage"/>)
	/// <br/>• Base64 encoded image string with prefix <c>"image:"</c>.
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
	/// <br/>• file path. Can be <c>.xaml</c>, <c>.png</c> etc. Supports environment variables etc, see <see cref="pathname.expand"/>. Can have prefix <c>"imagefile:"</c>.
	/// <br/>• resource path that starts with <c>"resources/"</c> or has prefix <c>"resource:"</c>. This function calls <see cref="ResourceUtil.GetXamlObject"/> if ends with <c>".xaml"</c>, else <see cref="ResourceUtil.GetWpfImage"/>.
	/// <br/>• Base64 encoded image with prefix <c>"image:"</c>. See <see cref="LoadImageStreamFromString"/>.
	/// <br/>• XAML string that starts with <c>"&lt;"</c>. For example from the <b>Icons</b> tool of LibreAutomate.
	/// <br/>• XAML icon name like <c>"*Pack.Icon color"</c> or <c>"*Pack.Icon color @size"</c> or <c>"*Pack1.Icon1 color1; *Pack2.Icon2 color2 %8,8,,"</c>. More info in Remarks.
	/// </param>
	/// <returns>
	/// If <i>image</i> is XAML icon name or starts with <c>"&lt;"</c> or ends with <c>".xaml"</c> (case-insensitive), returns new WPF element of type specified by the XAML root element (uses <see cref="XamlReader"/>). Else returns <see cref="Image"/> with <c>Source</c> = <see cref="BitmapFrame"/> (uses <see cref="LoadWpfImage"/>).
	/// </returns>
	/// <remarks>
	/// <i>image</i> can be an XAML icon name from the <b>Icons</b> tool of LibreAutomate (LA), like <c>"*Pack.Icon color"</c>. Full format: <c>"[*&lt;library&gt;]*pack.name[ color][ @size][ %margin][;more icons]"</c>. Here parts enclosed in <c>[]</c> are optional. The color, size and margin parts can be in any order.
	/// <br/>• color - <c>#RRGGBB</c> or color name (WPF). If 2 colors like <c>"#008000|#00FF00"</c>, the second color is for high contrast dark theme. If omitted, will use the system color of control text. Also can be like <c>"#008000|"</c> to use control text only for dark contrast theme, or <c>"|#00FF00"</c> for vice versa.
	/// <br/>• size - icon size 1 to 16, like <c>"*Pack.Icon blue @12"</c>. Can be used to make the displayed icon smaller or in some cases less blurry. It is the logical width and height of the icon rendered at the center of a box of logical size 16x16. To make icon bigger, instead set properties <c>Width</c> and <c>Height</c> of the returned element; or <see cref="MTBase.ImageSize"/> for a toolbar or menu.
	/// <br/>• margin - icon margins inside a box of logical size 16x16. Format: <c>%left,top,right,bottom,stretch,snap</c>. All parts are optional. Examples: <c>"*Pack.Icon blue %,,8,8"</c>, <c>"*Pack.Icon blue %8,8"</c>, <c>"*Pack.Icon blue %4,,4,,f"</c>. The stretch part can be <c>f</c> (fill) or <c>m</c> (move); default is uniform. The snap part can be <c>p</c> (sets <c>SnapsToDevicePixels=True</c>). Can be used either margin or size, not both.
	/// <br/>• more icons - can be specified multiple icons separated by semicolon, like <c>"*Pack1.Icon1 color1; *Pack2.Icon2 color2"</c>. It allows to create multi-color icons (for example a "filled" icon of one color + an "outline" icon of another color) or to add a small overlay icon (eg to indicate disabled state) at a corner (use margin).
	/// <br/>• library - name of assembly containing the resource. If omitted, uses <see cref="Assembly.GetEntryAssembly"/>.
	/// <br/>The LA compiler finds icon strings anywhere in code, gets their XAML from the database, and adds the XAML to the assembly as a string resource (see <b>Properties > Resource > Options</b>). This function gets the XAML from resources (<see cref="ResourceUtil.GetString"/>). If fails, then tries to get XAML from database, and fails if LA isn't running. Uses <see cref="ScriptEditor.GetIcon"/>.
	/// </remarks>
	/// <exception cref="Exception"></exception>
	public static FrameworkElement LoadWpfImageElement(string image) {
		if (image.Starts('*')) {
			image = ScriptEditor.GetIcon(image, EGetIcon.IconNameToXaml) ?? throw new AuException("*get icon " + image);
		}
		if (image.Starts('<')) return (FrameworkElement)XamlReader.Parse(image);
		if (image.Ends(".xaml", true)) {
			if (ResourceUtil.HasResourcePrefix(image)) return (FrameworkElement)ResourceUtil.GetXamlObject(image);
			if (image.Starts("imagefile:")) image = image[10..];
			using var stream = File.OpenRead(image);
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
	/// <param name="size">Final image size in logical pixels (not DPI-scaled). If <c>null</c>, uses element's <c>DesiredSize</c> property, max 1024x1024.</param>
	/// <returns>New <c>Bitmap</c>. Note: its pixel format is <c>Format32bppPArgb</c> (premultiplied ARGB).</returns>
	/// <exception cref="Exception"></exception>
	/// <remarks>
	/// Calls <see cref="LoadWpfImageElement"/> and <see cref="ConvertWpfImageElementToGdipBitmap"/>.
	/// Don't use the <c>Tag</c> property of the bitmap. It keeps bitmap data.
	/// </remarks>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static System.Drawing.Bitmap LoadGdipBitmapFromXaml(string image, int dpi, SIZE? size = null) {
		var e = LoadWpfImageElement(image);
		//s_cwt.Add(e, new());
		return ConvertWpfImageElementToGdipBitmap(e, dpi, size);
	}
	
	/// <summary>
	/// Converts WPF image element to GDI+ image.
	/// </summary>
	/// <param name="e">For example <see cref="Viewbox"/>.</param>
	/// <param name="dpi">DPI of window that will display the image.</param>
	/// <param name="size">
	/// Final image size in logical pixels (not DPI-scaled).
	/// If <c>null</c>, uses element's <c>DesiredSize</c> property, max 1024x1024.
	/// If not <c>null</c>, sets element's <c>Width</c> and <c>Height</c>; the element should not be used in UI.
	/// </param>
	/// <returns>New <c>Bitmap</c>. Note: its pixel format is <c>Format32bppPArgb</c> (premultiplied ARGB).</returns>
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
		//note: if Bgra32, throws exception "'Bgra32' PixelFormat is not supported for this operation".
		rtb.Render(e);
		if (!arranged) e.InvalidateArrange(); //prevent huge memory leak
		if (!measured) e.InvalidateMeasure();
		int stride = wid * 4, msize = hei * stride;
		var b = new System.Drawing.Bitmap(wid, hei, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
		using var d = b.Data(System.Drawing.Imaging.ImageLockMode.ReadWrite);
		rtb.CopyPixels(new(0, 0, wid, hei), d.Scan0, msize, stride);
		b.SetResolution(dpi, dpi);
		return b;
		//tested: GC OK. Don't need GC_.AddObjectMemoryPressure. WPF makes enough garbage to trigger GC when need.
	}
	
	/// <summary>
	/// Converts WPF image element to native icon file data.
	/// </summary>
	/// <param name="stream">Stream to write icon file data. Writes from start.</param>
	/// <param name="e">Image element. See <see cref="LoadWpfImageElement"/>.</param>
	/// <param name="sizes">Sizes of icon images to add. For example 16, 24, 32, 48, 64. Sizes can be 1 to 256 inclusive.</param>
	/// <exception cref="ArgumentOutOfRangeException">An invalid size.</exception>
	/// <exception cref="Exception"></exception>
	internal static unsafe void ConvertWpfImageElementToIcon_(Stream stream, FrameworkElement e, int[] sizes) {
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
	
	/// <summary>
	/// Converts WPF image element to native icon file.
	/// </summary>
	/// <inheritdoc cref="ConvertWpfImageElementToIcon_(Stream, FrameworkElement, int[])"/>
	internal static void ConvertWpfImageElementToIcon_(string icoFile, FrameworkElement e, int[] sizes) {
		icoFile = pathname.NormalizeMinimally_(icoFile);
		using var stream = File.OpenWrite(icoFile);
		ConvertWpfImageElementToIcon_(stream, e, sizes);
	}
	
	/// <summary>
	/// Converts XAML icon to GDI+ icon.
	/// </summary>
	/// <param name="s">Icon name or XAML etc. See <see cref="ImageUtil.LoadWpfImageElement"/>.</param>
	/// <param name="size"></param>
	/// <returns>If fails, prints warning and returns null.</returns>
	internal static System.Drawing.Icon XamlIconToGdipIcon_(string s, int size) {
		try {
			var e = ImageUtil.LoadWpfImageElement(s);
			var ms = new MemoryStream();
			ImageUtil.ConvertWpfImageElementToIcon_(ms, e, [size]);
			ms.Position = 0;
			return new System.Drawing.Icon(ms);
		}
		catch (Exception ex) { print.warning(ex); return null; }
	}
}
