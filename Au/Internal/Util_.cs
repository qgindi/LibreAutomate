using System.Windows;

namespace Au.More;

static unsafe class Not_ {
	//internal static void NullCheck<T>(this T t, string paramName = null) where T : class {
	//	if (t is null) throw new ArgumentNullException(paramName);
	//}

	/// <summary>
	/// Same as <b>ArgumentNullException.ThrowIfNull</b>.
	/// It's pitty, they removed operator !! from C# 11.
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
	/// true if SystemParameters.HighContrast and ColorInt.GetPerceivedBrightness(SystemColors.ControlColor)&lt;=0.5.
	/// </summary>
	public static bool IsHighContrastDark {
		get {
			if (!SystemParameters.HighContrast) return false; //fast, cached
			var col = (ColorInt)SystemColors.ControlColor; //fast, cached
			var v = ColorInt.GetPerceivedBrightness(col.argb, false);
			return v <= .5;
		}
	}

	/// <summary>
	/// From icon string like "*name color|color2" or "color|color2" removes "|color2" or "color|" depending on high contrast.
	/// Does nothing if s does not contain '|'.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="onlyColor">true - s is like "color|color2". false - s is like "*name color|color2".</param>
	public static string NormalizeIconStringColor(string s, bool onlyColor) {
		int i = s.IndexOf('|');
		if (i >= 0) {
			bool dark = IsHighContrastDark;
			if (onlyColor) {
				s = dark ? s[++i..] : s[..i];
			} else {
				int j = s.IndexOf(' ');
				if ((uint)j < i) s = dark ? s.Remove(++j..++i) : s[..i];
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
}

#if !true
namespace Au
{
/// <summary>
/// 
/// </summary>
public struct TestDoc
{
	/// <summary>
	/// Sum One.
	/// </summary>
	/// <param name="i">Iiii.</param>
	/// <returns>Retto.</returns>
	/// <exception cref="ArgumentException">i is invalid.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>Remmo.</remarks>
	/// <example>
	/// <code>TestDoc.One(1);</code>
	/// </example>
	public static int One(int i) {
		print.it(i);
		return 0;
	}

	/// <inheritdoc cref="One(int)"/>
	public static int One(int i, string s) {
		print.it(i, s);
		return 0;
	}

	/// <param name="b">Boo.</param>
	/// <exception cref="NotFoundException">Not found.</exception>
	/// <inheritdoc cref="One"/>
	public static int One(bool b, int i) {
		print.it(i, b);
		return 0;
	}

	/// <param name="b">Boo no except.</param>
	/// <inheritdoc cref="One"/>
	public static int One(string b, int i) {
		print.it(i, b);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One" path="/param"/>
	/// <param name="s">Last.</param>
	public static int One(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}
	///// <inheritdoc cref="One" path="/param[@name='i']"/>

	/// <summary>
	/// Sum Two.
	/// </summary>
	/// <param name="b">Boo.</param>
	/// <exception cref="TimeoutException">Timeout.</exception>
	/// <inheritdoc cref="One"/>
	public static int Two(double b, int i) {
		print.it(i, b);
		return 0;
	}

	/// <summary>
	/// Sum Two 2.
	/// </summary>
	/// <exception cref="TimeoutException">Timeout.</exception>
	/// <inheritdoc cref="One"/>
	public static int TwoNoParam(int i) {
		print.it(i);
		return 0;
	}

	/// <summary>
	/// Sum Two 2.
	/// </summary>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One"/>
	public static int TwoNoExcept(double b, int i) {
		print.it(i, b);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One" path="/param"/>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One(int)" path="/param"/>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara2(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One" path="/param[@name='i']"/>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara3(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <inheritdoc cref="One(int)" path="/param[@name='i']"/>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara4(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <param name="i"><inheritdoc cref="One"/></param>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara5(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <param name="i"><inheritdoc cref="One" path="/param"/></param>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara6(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <param name="i"><inheritdoc cref="One" path="/param[@name='i']"/></param>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara7(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}

	/// <inheritdoc cref="One" path="/summary"/>
	/// <param name="b">Boo.</param>
	/// <param name="i"><inheritdoc cref="One(int)" path="/param[@name='i']"/></param>
	/// <param name="s">Last.</param>
	public static int TwoInheritPara8(double b, int i, string s) {
		print.it(i, b, s);
		return 0;
	}
}
}
#endif
