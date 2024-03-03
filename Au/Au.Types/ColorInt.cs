using System.Drawing;
using System.Text.Json.Serialization;

namespace Au.Types;

//rejected: /// <completionlist cref="Color"/>

/// <summary>
/// Color, as <b>int</b> in 0xAARRGGBB format.
/// Can convert from/to <see cref="Color"/>, <see cref="System.Windows.Media.Color"/>, <b>int</b> (0xAARRGGBB), Windows <b>COLORREF</b> (0xBBGGRR), string.
/// </summary>
public record struct ColorInt {
	/// <summary>
	/// Color value in 0xAARRGGBB format.
	/// </summary>
	[JsonInclude]
	public int argb;
	
	/// <param name="colorARGB">Color value in 0xAARRGGBB or 0xRRGGBB format.</param>
	/// <param name="makeOpaque">Set alpha = 0xFF. If <c>null</c> (default), sets alpha = 0xFF if it is 0 in <i>colorBGR</i>.</param>
	public ColorInt(int colorARGB, bool? makeOpaque = null) {
		if (makeOpaque == true || (makeOpaque == null && (colorARGB & ~0xFFFFFF) == 0)) colorARGB |= 0xFF << 24;
		argb = colorARGB;
	}
	
	/// <param name="colorARGB">Color value in 0xAARRGGBB or 0xRRGGBB format.</param>
	/// <param name="makeOpaque">Set alpha = 0xFF. If <c>null</c> (default), sets alpha = 0xFF if it is 0 in <i>colorBGR</i>.</param>
	public ColorInt(uint colorARGB, bool? makeOpaque) : this((int)colorARGB, makeOpaque) { }
	
	/// <summary>
	/// Converts from an <b>int</b> color value in 0xRRGGBB or 0xAARRGGBB format.
	/// Sets alpha = 0xFF if it is 0 in <i>color</i>.
	/// </summary>
	//[Obsolete] //to find all references
	public static implicit operator ColorInt(int color) => new(color);
	
	/// <summary>
	/// Converts from an <b>uint</b> color value in 0xRRGGBB or 0xAARRGGBB format.
	/// Sets alpha = 0xFF if it is 0 in <i>color</i>.
	/// </summary>
	//[Obsolete] //to find all references
	public static implicit operator ColorInt(uint color) => new((int)color);
	
	/// <summary>
	/// Converts from <see cref="Color"/>.
	/// </summary>
	public static implicit operator ColorInt(Color color) => new(color.ToArgb(), false);
	
	/// <summary>
	/// Converts from <see cref="System.Windows.Media.Color"/>.
	/// </summary>
	public static implicit operator ColorInt(System.Windows.Media.Color color)
		=> new((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B, false);
	
	/// <summary>
	/// Converts from a color name (<see cref="Color.FromName(string)"/>) or string <c>"0xRRGGBB"</c> or <c>"#RRGGBB"</c>.
	/// </summary>
	/// <remarks>
	/// If <i>s</i> is a hex number that contains 6 or less hex digits, makes opaque (alpha 0xFF).
	/// If <i>s</i> is <c>null</c> or invalid, sets <c>c.argb = 0</c> and returns <c>false</c>.
	/// </remarks>
	public static bool FromString(string s, out ColorInt c) {
		c.argb = 0;
		if (s == null || s.Length < 2) return false;
		if (s[0] == '0' && s[1] == 'x') {
			c.argb = s.ToInt(0, out int end);
			if (end < 3) return false;
			if (end <= 8) c.argb |= unchecked((int)0xFF000000);
		} else if (s[0] == '#') {
			c.argb = s.ToInt(1, out int end, STIFlags.IsHexWithout0x);
			if (end < 2) return false;
			if (end <= 7) c.argb |= unchecked((int)0xFF000000);
		} else {
			c.argb = Color.FromName(s).ToArgb();
			if (c.argb == 0) return false; //invalid is 0, black is 0xFF000000
		}
		return true;
	}
	
	/// <summary>
	/// Converts from Windows native <b>COLORREF</b> (0xBBGGRR to 0xAARRGGBB).
	/// </summary>
	/// <param name="colorBGR">Color in 0xBBGGRR format.</param>
	/// <param name="makeOpaque">Set alpha = 0xFF. If <c>null</c> (default), sets alpha = 0xFF if it is 0 in <i>colorBGR</i>.</param>
	public static ColorInt FromBGR(int colorBGR, bool? makeOpaque = null) => new(SwapRB(colorBGR), makeOpaque);
	
	/// <summary>
	/// Converts to Windows native <b>COLORREF</b> (0xBBGGRR from 0xAARRGGBB).
	/// </summary>
	/// <returns>color in <b>COLORREF</b> format. Does not modify this variable.</returns>
	/// <param name="zeroAlpha">Set the alpha byte = 0.</param>
	public int ToBGR(bool zeroAlpha = true) {
		var r = SwapRB(argb);
		if (zeroAlpha) r &= 0xFFFFFF;
		return r;
	}
	
	//rejected. Easy to create bugs when actually need BGR. Let use ToBGR() when need BGR, or argb field when need ARGB.
	///// <summary>Returns <c>c.argb</c>.</summary>
	//public static explicit operator int(ColorInt c) => c.argb;
	
	///// <summary>Returns <c>(uint)c.argb</c>.</summary>
	//public static explicit operator uint(ColorInt c) => (uint)c.argb;
	
	/// <summary>Converts to <see cref="Color"/>.</summary>
	public static explicit operator Color(ColorInt c) => Color.FromArgb(c.argb);
	
	/// <summary>Converts to <see cref="System.Windows.Media.Color"/>.</summary>
	public static explicit operator System.Windows.Media.Color(ColorInt c) {
		uint k = (uint)c.argb;
		return System.Windows.Media.Color.FromArgb((byte)(k >> 24), (byte)(k >> 16), (byte)(k >> 8), (byte)k);
	}
	
	internal static System.Windows.Media.Color WpfColor_(int rgb)
		=> System.Windows.Media.Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
	
	internal static System.Windows.Media.SolidColorBrush WpfBrush_(int rgb)
		=> new(WpfColor_(rgb));
	
	///// <summary>
	///// <c>FromBGR(GetSysColor)</c>.
	///// </summary>
	//internal static ColorInt FromSysColor_(int colorIndex) => FromBGR(Api.GetSysColor(colorIndex), true);
	
	///
	public override string ToString() => "#" + argb.ToString("X8");
	
	/// <summary>
	/// Converts color from ARGB (0xAARRGGBB) to ABGR (0xAABBGGRR) or vice versa (swaps the red and blue bytes).
	/// ARGB is used in .NET, GDI+ and HTML/CSS.
	/// ABGR is used by most Windows API; aka <b>COLORREF</b>.
	/// </summary>
	public static int SwapRB(int color) => (color & unchecked((int)0xff00ff00)) | (color << 16 & 0xff0000) | (color >> 16 & 0xff);
	
	/// <inheritdoc cref="SwapRB(int)"/>
	public static uint SwapRB(uint color) => (color & 0xff00ff00) | (color << 16 & 0xff0000) | (color >> 16 & 0xff);
	
	//rejected. Unclear usage. Instead let users call ToHLS, change L how they want, and call FromHLS.
	///// <summary>
	///// Changes color's luminance (makes darker or brighter).
	///// Returns new color. Does not modify this variable.
	///// </summary>
	///// <param name="n">The luminance in units of 0.1 percent of the range (which depends on <i>totalRange</i>). Can be from -1000 to 1000.</param>
	///// <param name="totalRange">If <c>true</c>, <i>n</i> is in whole luminance range (from minimal to maximal possible). If <c>false</c>, n is in the range from current luminance of the color to the maximal (if n positive) or minimal (if n negative) luminance.</param>
	///// <remarks>
	///// Calls API <msdn>ColorAdjustLuma</msdn>.
	///// Does not change hue and saturation. Does not use alpha.
	///// </remarks>
	//internal ColorInt AdjustLuminance(int n, bool totalRange = false) {
	//	uint u = (uint)argb;
	//	u = Api.ColorAdjustLuma(u & 0xffffff, n, !totalRange) | (u & 0xFF000000);
	//	return new((int)u, false);
	//	//tested: with SwapRB the same.
	//}
	
	/// <summary>
	/// Converts from hue-luminance-saturation (HLS).
	/// </summary>
	/// <param name="H">Hue, 0 to 240.</param>
	/// <param name="L">Luminance, 0 to 240.</param>
	/// <param name="S">Saturation, 0 to 240.</param>
	/// <param name="bgr">Return color in 0xBBGGRR format. If <c>false</c>, 0xRRGGBB.</param>
	/// <returns>Color in 0xRRGGBB or 0xBBGGRR format, depending on <b>bgr</b>. Alpha 0.</returns>
	public static int FromHLS(int H, int L, int S, bool bgr) {
		if (S == 0) { //ColorHLSToRGB bug: returns 0 if S 0
			int i = L * 255 / 240;
			return i | (i << 8) | (i << 16);
		}
		int color = Api.ColorHLSToRGB((ushort)H, (ushort)L, (ushort)S);
		if (!bgr) color = SwapRB(color);
		return color;
	}
	
	/// <summary>
	/// Converts to hue-luminance-saturation (HLS).
	/// </summary>
	/// <param name="color">Color in 0xRRGGBB or 0xBBGGRR format, depending on <b>bgr</b>. Ignores alpha.</param>
	/// <param name="bgr"><i>color</i> is in 0xBBGGRR format. If <c>false</c>, 0xRRGGBB.</param>
	/// <returns>Hue, luminance and saturation. All 0 to 240.</returns>
	public static (int H, int L, int S) ToHLS(int color, bool bgr) {
		if (!bgr) color = SwapRB(color);
		Api.ColorRGBToHLS(color, out var H, out var L, out var S);
		return (H, L, S);
	}
	
	/// <summary>
	/// Calculates color's perceived brightness.
	/// </summary>
	/// <returns>0 to 1.</returns>
	/// <param name="color">Color in 0xRRGGBB or 0xBBGGRR format, depending on <b>bgr</b>. Ignores alpha.</param>
	/// <param name="bgr"><i>color</i> is in 0xBBGGRR format. If <c>false</c>, 0xRRGGBB.</param>
	/// <remarks>
	/// Unlike <see cref="ToHLS"/> and <see cref="Color.GetBrightness"/>, this function uses different weights for red, green and blue components.
	/// Ignores alpha.
	/// </remarks>
	public static float GetPerceivedBrightness(int color, bool bgr) {
		uint u = (uint)color;
		if (bgr) u = SwapRB(u);
		uint R = u >> 16 & 0xff, G = u >> 8 & 0xff, B = u & 0xff;
		return (float)(Math.Sqrt(R * R * .299 + G * G * .587 + B * B * .114) / 255);
	}
	
	//same result as ColorAdjustLuma. Probably slower.
	//internal static int SetLuminance(int color, bool bgr, double percent, bool totalRange) {
	//	var (H, L, S) = ToHLS(color, bgr);
	//	L = (int)Math2.PercentToValue(totalRange ? 240 : L, percent);
	//	return (int)((uint)FromHLS(H, Math.Clamp(L, 0, 240), S, bgr) | (color & 0xFF000000));
	//}
}
