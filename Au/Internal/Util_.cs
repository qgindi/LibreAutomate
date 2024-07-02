using System.Windows;

namespace Au.More;

static unsafe class Not_ {
	//internal static void NullCheck<T>(this T t, string paramName = null) where T : class {
	//	if (t is null) throw new ArgumentNullException(paramName);
	//}
	
	/// <summary>
	/// Same as <b>ArgumentNullException.ThrowIfNull</b>.
	/// It's pity, they removed operator <b>!!</b> from C# 11.
	/// </summary>
	internal static void Null(object o,
		[CallerArgumentExpression("o")] string paramName = null) {
		if (o is null) throw new ArgumentNullException(paramName);
	}
	internal static void Null(object o1, object o2,
		[CallerArgumentExpression("o1")] string paramName1 = null,
		[CallerArgumentExpression("o2")] string paramName2 = null) {
		if (o1 is null) throw new ArgumentNullException(paramName1);
		if (o2 is null) throw new ArgumentNullException(paramName2);
	}
	internal static void Null(object o1, object o2, object o3,
		[CallerArgumentExpression("o1")] string paramName1 = null,
		[CallerArgumentExpression("o2")] string paramName2 = null,
		[CallerArgumentExpression("o3")] string paramName3 = null) {
		if (o1 is null) throw new ArgumentNullException(paramName1);
		if (o2 is null) throw new ArgumentNullException(paramName2);
		if (o3 is null) throw new ArgumentNullException(paramName3);
	}
	internal static void Null(object o1, object o2, object o3, object o4,
		[CallerArgumentExpression("o1")] string paramName1 = null,
		[CallerArgumentExpression("o2")] string paramName2 = null,
		[CallerArgumentExpression("o3")] string paramName3 = null,
		[CallerArgumentExpression("o4")] string paramName4 = null) {
		if (o1 is null) throw new ArgumentNullException(paramName1);
		if (o2 is null) throw new ArgumentNullException(paramName2);
		if (o3 is null) throw new ArgumentNullException(paramName3);
		if (o4 is null) throw new ArgumentNullException(paramName4);
	}
	internal static void Null(void* o,
		[CallerArgumentExpression("o")] string paramName = null) {
		if (o is null) throw new ArgumentNullException(paramName);
	}
	internal static T NullRet<T>(T o,
		[CallerArgumentExpression("o")] string paramName = null) where T : class {
		if (o is null) throw new ArgumentNullException(paramName);
		return o;
	}
}

static class WpfUtil_ {
	/// <summary>
	/// Parses icon string like <c>"[*&lt;library&gt;]*pack.name[ etc]"</c>.
	/// </summary>
	/// <returns><c>true</c> if starts with <c>"*pack.name"</c> (possibly with library).</returns>
	public static bool DetectIconString(RStr s, out (int pack, int endPack, int name, int endName) r) {
		r = default;
		if (s.Length < 8 || s[0] != '*') return false;
		int pack = 1;
		if (s[1] == '<') { // *<library>*icon
			pack = s.IndexOf('>');
			if (pack++ < 0 || s.Length - pack < 8 || s[pack] != '*') return false;
			pack++;
		}
		int name = s.IndexOf(pack, '.'); if (name < pack + 4) return false; //shortest names currently are like "Modern.At", but in the future may add a pack with a shorter name.
		int end = ++name; while (end < s.Length && s[end] != ' ') end++;
		if (end == name) return false;
		r = (pack, name - 1, name, end);
		return true;
	}
	
	/// <summary>
	/// Parses icon string like <c>"[*&lt;library&gt;]*pack.name[ color][ @size]"</c>.
	/// </summary>
	/// <returns><c>true</c> if starts with <c>"*pack.name"</c> (possibly with library).</returns>
	public static bool ParseIconString(string s, out (string pack, string name, string color, int size) r) {
		r = default;
		if (!DetectIconString(s, out var d)) return false;
		r.pack = s[d.pack..d.endPack];
		r.name = s[d.name..d.endName];
		for (int end = d.endName; end < s.Length;) {
			while (++end < s.Length && s[end] == ' ') { }
			int start = end; if (start == s.Length) break;
			while (++end < s.Length && s[end] != ' ') { }
			char c = s[start];
			if (c == '@') r.size = s.ToInt(++start, STIFlags.DontSkipSpaces);
			else if (c == '#' || c.IsAsciiAlpha()) r.color ??= s[start..end];
		}
		return true;
	}
	
	/// <summary>
	/// Eg <c>"*pack.name color"</c> -> <c>"*pack.name"</c>.
	/// Supports library prefix, <c>@size</c>, no-color.
	/// </summary>
	public static string RemoveColorFromIconString(string s) {
		int i = 0;
		if (s.Starts("*<")) { i = s.IndexOf('>'); if (i++ < 0) return s; }
		return s.RxReplace(@" ++[^@]\S+", "", 1, range: i..);
	}
	
	/// <summary>
	/// From icon string like <c>"*name color|color2"</c> or <c>"color|color2"</c> removes <c>"|color2"</c> or <c>"color|"</c> depending on high contrast.
	/// Does nothing if <i>s</i> does not contain <c>'|'</c>.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="onlyColor"><c>true</c> - <i>s</i> is like <c>"color|color2"</c>. <c>false</c> - <i>s</i> is like <c>"*name color|color2"</c>.</param>
	public static string NormalizeIconStringColor(string s, bool onlyColor) {
		int i = s.IndexOf('|');
		if (i >= 0) {
			bool dark = IsHighContrastDark;
			if (onlyColor) {
				s = dark ? s[++i..] : s[..i];
			} else {
				int j = i;
				if (dark) {
					for (j++; i > 0 && s[i - 1] != ' ';) i--;
				} else {
					while (j < s.Length && s[j] != ' ') j++;
				}
				s = s.Remove(i..j);
			}
		}
		return s;
	}
	
	public static bool SetColorInXaml(ref string xaml, string color) {
		if (color.NE()) color = SystemColors.ControlTextColor.ToString();
		else color = NormalizeIconStringColor(color, true); //color can be "normal|highContrast"
		
		s_rxColor ??= new(@"(?:Fill|Stroke)=""\K[^""]+");
		return 0 != s_rxColor.Replace(xaml, color, out xaml, 1);
	}
	
	static regexp s_rxColor;
	
	/// <summary>
	/// <c>true</c> if <b>SystemParameters.HighContrast</b> and <c>ColorInt.GetPerceivedBrightness(SystemColors.ControlColor)&lt;=0.5</c>.
	/// </summary>
	public static bool IsHighContrastDark {
		get {
			if (!SystemParameters.HighContrast) return false; //fast, cached
			var col = (ColorInt)SystemColors.ControlColor; //fast, cached
			var v = ColorInt.GetPerceivedBrightness(col.argb, false);
			return v <= .5;
		}
	}
}
