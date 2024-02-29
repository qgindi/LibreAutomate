using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;

namespace Au.Types;

/// <summary>
/// Used by <see cref="ocr"/> and <see cref="ocrFinder"/>.
/// </summary>
public interface IOcrEngine {
	/// <summary>
	/// Recognizes text in image.
	/// </summary>
	/// <param name="b">Image.</param>
	/// <param name="dispose">Call <c>b.Dispose()</c>. The image will not be used by the caller.</param>
	/// <param name="scale">Scale factor.</param>
	/// <returns>Recognized words.</returns>
	/// <remarks>
	/// The class that implements this function can use static <see cref="IOcrEngine"/> functions to prepare the image (scale etc) and get image data.
	/// </remarks>
	OcrWord[] Recognize(Bitmap b, bool dispose, double scale);

	/// <summary>
	/// Let OCR functions scale images captured from a window or screen, 1-2 times depending on DPI, unless non-zero <i>scale</i> parameter is specified.
	/// </summary>
	bool DpiScale { get; set; }

	#region util

	/// <summary>
	/// If need, resizes the image and/or ensures it isn't too small.
	/// </summary>
	/// <param name="dispose">Call <c>b.Dispose()</c> if created new image. The input image will not be used by the caller.</param>
	/// <param name="scale">Scale factor.</param>
	/// <param name="minWidth">Minimal image width accepted by the OCR engine.</param>
	/// <param name="minHeight">Minimal image height accepted by the OCR engine.</param>
	protected static Bitmap PrepareBitmap(Bitmap b, bool dispose, double scale, int minWidth = 0, int minHeight = 0) {
		var b0 = b;
		if (scale > 1) b = b.Resize(scale, BRFilter.Lanczos3, dispose);

		//Microsoft OCR (Win10 and Azure) reject small images
		if (minWidth > 0 && minHeight > 0) {
			var zi = b.Size;
			if (zi.Width < minWidth || zi.Height < minHeight) {
				int wid = Math.Max(zi.Width, minWidth), hei = Math.Max(zi.Height, minHeight);
				var b2 = new Bitmap(wid, hei, b.PixelFormat);
				b2.SetResolution(b.HorizontalResolution, b.VerticalResolution);
				using var g = Graphics.FromImage(b2);
				g.Clear(Color.White);
				g.DrawImage(b, 0, 0);
				if (dispose || b != b0) b.Dispose();
				b = b2;
			}
		}

		//var file = folders.Temp + "test.png"; b.Save(file); run.it(file);
		return b;
	}

	/// <summary>
	/// Gets image pixels.
	/// </summary>
	/// <remarks>The pixel format is either <b>Format32bppRgb</b> (if it's <i>b</i> format) or <b>Format32bppArgb</b>.</remarks>
	protected static unsafe byte[] GetBitmapData(Bitmap b) {
		using var d = b.Data(ImageLockMode.ReadOnly, b.PixelFormat == PixelFormat.Format32bppRgb ? PixelFormat.Format32bppRgb : PixelFormat.Format32bppArgb);
		return new Span<byte>((void*)d.Scan0, d.Width * d.Height * 4).ToArray();
	}

	/// <summary>
	/// Gets image data in .png file format.
	/// </summary>
	protected static unsafe byte[] GetBitmapPngFileData(Bitmap b) {
		using var ms = new MemoryStream();
		b.Save(ms, ImageFormat.Png);
		var a = ms.ToArray();
		return a;
	}

	/// <summary>
	/// Converts word polygon JSON nodes to <b>RECT</b>.
	/// </summary>
	protected static RECT PolyToRect(JsonNode xTL, JsonNode yTL, JsonNode xTR, JsonNode yTR, JsonNode xBR, JsonNode yBR, JsonNode xBL, JsonNode yBL) {
		return RECT.FromLTRB((i(xTL) + i(xBL) + 1) / 2, (i(yTL) + i(yTR) + 1) / 2, (i(xTR) + i(xBR)) / 2, (i(yBR) + i(yBL)) / 2);
		static int i(JsonNode n) => n != null ? (int)n : 0; //the node may be missing in JSON if has default value
	}

	#endregion
}

/// <summary>
/// Stores text and rectangle of a word in OCR results.
/// See <see cref="ocr.Words"/>.
/// </summary>
public record class OcrWord {
	/// <param name="separator">Separator before the word (space, new line, etc). Also can be <c>""</c> or <c>null</c>.</param>
	/// <param name="text">Word text.</param>
	/// <param name="rect">Word rectangle in <i>area</i>, possibly scaled.</param>
	/// <param name="scale">The <i>scale</i> parameter of the OCR function. This function unscales <i>rect</i> if need.</param>
	public OcrWord(string separator, string text, RECT rect, double scale) {
		Separator = separator;
		Text = text;
		Rect = scale > 1 ? RECT.FromLTRB(i(rect.left), i(rect.top), i(rect.right), i(rect.bottom)) : rect;

		int i(int x) => (x / scale).ToInt();
	}

	/// <summary>Separator before the word (space, new line, etc). Also can be <c>""</c> or <c>null</c>.</summary>
	public string Separator { get; }

	/// <summary>Word text.</summary>
	public string Text { get; }

	/// <summary>Word rectangle in <i>area</i>.</summary>
	public RECT Rect { get; internal set; }

	/// <summary>Word offset in <see cref="ocr.TextForFind"/>.</summary>
	public int Offset { get; internal set; }
}

/// <summary>
/// Flags for <see cref="ocr"/> and <see cref="ocrFinder"/>.
/// </summary>
[Flags]
public enum OcrFlags {
	/// <inheritdoc cref="IFFlags.WindowDC"/>
	WindowDC = 1,

	/// <inheritdoc cref="IFFlags.PrintWindow"/>
	PrintWindow = 2,

	///// <inheritdoc cref="IFFlags.WindowDwm"/>
	//WindowDwm = 4,

	//note: the above values must be the same in CIFlags, CIUFlags, IFFlags, OcrFlags.

	//rejected. Maybe in the future.
	///// <summary>
	///// Sort words by word rectangle position, left-to-right and top-to-bottom.
	///// </summary>
	//Sort = 0x100,
}
