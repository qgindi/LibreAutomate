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
